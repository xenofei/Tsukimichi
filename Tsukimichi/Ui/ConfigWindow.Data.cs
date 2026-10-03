using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Export;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Characters &amp; data, the data half (feature plan v6 U7): what Tsukimichi keeps, the Moonlit verdicts
/// with Restore and a hold-to-confirm Restore all, the export rows, and the Danger zone card with Delete all behind two
/// confirmations, the last one press and hold.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray ExportFormatOptions = new(static () => [Strings.ExportFormatJson, Strings.ExportFormatCsv]);

    private static string RestoreAllLabel => restoreAllLabelText.Value;

    private static readonly LocText restoreAllLabelText = new(static () => Strings.ConfigVerdictRestoreAll + Chrome.HoldIdSuffix);

    private static string DeleteConfirmLabel => deleteConfirmLabelText.Value;

    private static readonly LocText deleteConfirmLabelText = new(static () => Strings.ConfigDeleteConfirm + Chrome.HoldIdSuffix);

    private bool openSecondConfirm;
    private bool openFirstConfirm;
    private string? toast;
    private bool toastFailed;
    private DateTime toastUntilUtc;

    // "Your Moonlit verdicts": one row per override, rebuilt when the overrides or the quest catalog change.
    private readonly ConfirmGate restoreAllGate = new();
    private readonly ClickGuard verdictRestoreGuard = new();
    private readonly ConfirmGate deleteAllGate = new();
    private VerdictRow[] verdictRows = [];
    private int verdictVersion = -1;
    private CatalogBundle? verdictBundle;
    private int verdictSpoilers;
    private int verdictLanguage = -1;
    private string verdictsHeader = string.Empty;

    /// <summary>The Moonlit verdicts table's column plan (<see cref="PaneFit.VerdictColumns"/>).</summary>
    private readonly ColumnFit verdictColumns = new(5);

    /// <summary>Settings › Characters &amp; data › Your data: what is kept, the verdicts, and the export rows.</summary>
    private void DrawData()
    {
        Header(Strings.ConfigSectionData);
        Note(Strings.SettingsDataKept, Strings.ConfigDataRetention, "data storage privacy snapshot files folder");

        if (Overrides is { } overrides)
        {
            RefreshVerdictRows(overrides);
        }

        if (Setting(Strings.SettingsVerdicts, Strings.SettingsVerdictsHint, "moonlit verdicts overrides unique restore", 0f, shown: Overrides is null ? null : verdictsHeader))
        {
            SettingBelow();
            DrawVerdicts();
            EndSetting();
        }

        DrawExportRows();
    }

    /// <summary>The export rows: the format, the two options, the folder, and the buttons with the last result.</summary>
    private void DrawExportRows()
    {
        if (Export is not { } export)
        {
            return;
        }

        var format = settings.ExportFormat == ExportFormat.Csv ? 1 : 0;
        if (Choice(Strings.ExportFormatLabel, Strings.SettingsExportFormatHint, ref format, ExportFormatOptions.Value, "export json csv spreadsheet format file"))
        {
            settings.ExportFormat = format == 1 ? ExportFormat.Csv : ExportFormat.Json;
            Save();
        }

        var includeName = settings.ExportIncludeCharacterName;
        if (Toggle(Strings.ExportIncludeName, Strings.ExportIncludeNameHint, ref includeName, "export character name file header"))
        {
            settings.ExportIncludeCharacterName = includeName;
            Save();
        }

        var incomplete = settings.ExportIncludeIncomplete;
        if (Toggle(Strings.ExportIncludeIncomplete, Strings.ExportIncludeIncompleteHint, ref incomplete, "export every quest completed flag full journal"))
        {
            settings.ExportIncludeIncomplete = incomplete;
            Save();
        }

        if (Setting(Strings.ExportFolderLabel, Strings.SettingsExportFolderHint, "export folder path directory output"))
        {
            export.DrawFolder(ControlWidth);
            EndSetting();
        }

        if (Setting(Strings.ExportHeader, Strings.SettingsExportHint, "export json csv spreadsheet collection tracker file write", 0f))
        {
            SettingBelow();
            export.DrawButtons();
            EndSetting();
        }
    }

    /// <summary>
    /// The Danger zone card (feature plan v6 U7): Delete all Tsukimichi data, behind a first confirmation that says what
    /// goes and a second one that asks again with a press-and-hold button. The confirmations and the result line outlive
    /// a search that hides the row.
    /// </summary>
    private void DrawDangerZone()
    {
        Header(Strings.SettingsDangerZoneHeading);
        DangerCard();
        if (Setting(Strings.ConfigDeleteAll, Strings.ConfigDeleteStep1Text, "delete erase remove reset wipe forget data danger"))
        {
            using (Theme.PushDestructiveButton())
            {
                if (ImGui.Button(Strings.SettingsDeleteButton))
                {
                    // Opened outside the row, where the confirmation is drawn, so the two share one id.
                    openFirstConfirm = true;
                }
            }

            if (toast is not null)
            {
                if (DateTime.UtcNow >= toastUntilUtc)
                {
                    toast = null;
                }
                else
                {
                    SettingNote(toast, toastFailed ? Theme.EclipseText : Theme.Silver);
                }
            }

            EndSetting();
        }

        DrawDeleteConfirms();
    }

    /// <summary>
    /// "Your Moonlit verdicts": quest, verdict, note and date per stored override with a Restore button each (armed:
    /// Ctrl or Shift and click), and Restore all behind the hold-to-confirm gate (feature plan v6 S2). Both put up the
    /// floating Undo, which puts the verdicts back as they were. Restoring goes through the Moonlit pane, so its catalog
    /// rebuilds exactly as after a verdict.
    /// </summary>
    private void DrawVerdicts()
    {
        if (Overrides is not { } overrides)
        {
            ImGui.TextDisabled(Strings.ConfigVerdictsUnavailable);
            return;
        }

        if (verdictRows.Length == 0)
        {
            using (Typography.Caption())
            {
                ImGui.TextDisabled(Strings.ConfigVerdictsNone);
            }

            return;
        }

        // Restore always shows: the note hides first, then the date, and the quest and the note end in an ellipsis
        // (feature plan v4 L6).
        var restoreWidth = Chrome.ArmedButtonWidth(Strings.ConfigVerdictRestore);
        var verdictWidth = MathF.Max(ImGui.CalcTextSize(Strings.ConfigVerdictUnique).X, ImGui.CalcTextSize(Strings.ConfigVerdictNotUnique).X);
        Span<ColumnSpec> specs = stackalloc ColumnSpec[5];
        PaneFit.VerdictColumns(UiMetrics.Px(LayoutBudgets.RowNameMinLogical), verdictWidth, ImGui.CalcTextSize("0000-00-00").X, restoreWidth, specs);
        ColumnFit.FitHeader(specs, 1, Strings.ConfigVerdictColumnVerdict);
        ColumnFit.FitHeader(specs, 3, Strings.ConfigVerdictColumnDate);
        verdictColumns.Plan(ImGui.GetContentRegionAvail().X, specs);
        const ImGuiTableFlags Flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit;
        using (var table = verdictColumns.Begin("##verdicts", Flags))
        {
            if (table.Success)
            {
                verdictColumns.Setup(0, Strings.ConfigVerdictColumnQuest);
                verdictColumns.Setup(1, Strings.ConfigVerdictColumnVerdict);
                verdictColumns.Setup(2, Strings.ConfigVerdictColumnNote);
                verdictColumns.Setup(3, Strings.ConfigVerdictColumnDate);
                verdictColumns.Setup(4, Strings.ConfigVerdictColumnRestore, ImGuiTableColumnFlags.NoHeaderLabel);
                ImGui.TableHeadersRow();

                foreach (var verdict in verdictRows)
                {
                    using var id = ImRaii.PushId((int)verdict.RowId);
                    ImGui.TableNextRow();
                    if (verdictColumns.Next(0))
                    {
                        Chrome.FitText(verdict.QuestName, ImGui.GetColorU32(ImGuiCol.Text));
                    }

                    if (verdictColumns.Next(1))
                    {
                        using (Theme.PushText(verdict.Color))
                        {
                            ImGui.TextUnformatted(verdict.Verdict);
                        }
                    }

                    if (verdictColumns.Next(2))
                    {
                        Chrome.FitText(verdict.Note, ImGui.GetColorU32(ImGuiCol.Text));
                    }

                    if (verdictColumns.Next(3))
                    {
                        ImGui.TextDisabled(verdict.Date);
                    }

                    if (!verdictColumns.Next(4))
                    {
                        continue;
                    }

                    // The row cache refreshes next frame from the bumped version; the array is not touched here.
                    if (Chrome.ArmedButton(Strings.ConfigVerdictRestore, verdictRestoreGuard, GuardedAction.RestoreVerdict, Strings.ConfigVerdictRestoreTooltip, verdict.RowId))
                    {
                        VerdictPrompt.Restore(overrides, verdict.RowId);
                    }
                }
            }
        }

        if (Chrome.HoldButton(RestoreAllLabel, restoreAllGate))
        {
            // Every verdict as it was, dates included, so Undo puts them all back.
            var before = new List<KeyValuePair<uint, UniqueOverride>>(overrides.All);
            overrides.ClearAll();
            UndoToast.Show(
                string.Format(CultureInfo.CurrentCulture, Strings.UndoToastVerdictsRestoredFormat, before.Count),
                () => overrides.PutBack(before));
        }

        if (ImGui.IsItemHovered())
        {
            Safety.Tooltip(Strings.ConfigVerdictRestoreAllTooltip, GuardedAction.RestoreAllVerdicts);
        }
    }

    /// <summary>Rebuilds the verdict rows (sorted by quest name) when the overrides or the catalog changed.</summary>
    private void RefreshVerdictRows(IUniqueOverrides overrides)
    {
        var bundle = session.Bundle;
        var spoilers = session.Spoilers;
        if (overrides.Version == verdictVersion && ReferenceEquals(bundle, verdictBundle) && spoilers.Fingerprint == verdictSpoilers && verdictLanguage == Loc.Version)
        {
            return;
        }

        verdictLanguage = Loc.Version;
        verdictVersion = overrides.Version;
        verdictBundle = bundle;
        verdictSpoilers = spoilers.Fingerprint;
        var all = overrides.All;
        var list = new List<VerdictRow>(all.Count);
        foreach (var (rowId, stored) in all)
        {
            var name = bundle?.Catalog.GetByRowId(rowId) is { } quest ? spoilers.DisplayName(quest) : string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, rowId);
            list.Add(new VerdictRow(
                rowId,
                name,
                stored.Unique ? Strings.ConfigVerdictUnique : Strings.ConfigVerdictNotUnique,
                stored.Unique ? Theme.Silver : Theme.Dusk,
                stored.Note ?? string.Empty,
                stored.MarkedUtc?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty));
        }

        list.Sort(static (a, b) => string.Compare(a.QuestName, b.QuestName, StringComparison.CurrentCultureIgnoreCase));
        verdictRows = list.ToArray();
        verdictsHeader = string.Format(CultureInfo.InvariantCulture, Strings.ConfigVerdictsHeaderFormat, verdictRows.Length);
    }

    /// <summary>Two modals in a row: the first explains, the second asks again; only the second deletes.</summary>
    private void DrawDeleteConfirms()
    {
        if (openFirstConfirm)
        {
            openFirstConfirm = false;
            ImGui.OpenPopup(Strings.ConfigDeleteStep1Popup);
        }

        using (var first = ImRaii.PopupModal(Strings.ConfigDeleteStep1Popup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (first)
            {
                // A popup is its own window: it scales itself.
                UiMetrics.ApplyFontScale();
                ImGui.TextWrapped(Strings.ConfigDeleteStep1Text);
                ImGui.Spacing();
                if (ImGui.Button(Strings.ConfigDeleteContinue))
                {
                    openSecondConfirm = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button(Strings.ConfigCancel))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        if (openSecondConfirm)
        {
            openSecondConfirm = false;
            deleteAllGate.Cancel();
            ImGui.OpenPopup(Strings.ConfigDeleteStep2Popup);
        }

        using var second = ImRaii.PopupModal(Strings.ConfigDeleteStep2Popup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!second)
        {
            return;
        }

        UiMetrics.ApplyFontScale();

        ImGui.TextWrapped(Strings.ConfigDeleteStep2Text);
        ImGui.Spacing();
        using (Theme.PushDestructiveButton())
        {
            // The last step is press and hold (feature plan v6 S2): a double-click through the two modals deletes nothing.
            var confirmed = Chrome.HoldButton(DeleteConfirmLabel, deleteAllGate);
            if (ImGui.IsItemHovered())
            {
                Safety.Tooltip(Strings.ConfigDeleteConfirmTooltip, GuardedAction.DeleteAllData);
            }

            if (confirmed)
            {
                // A listener that failed may still hold pins or overrides in memory and save them again: say so.
                var failed = session.DeleteAllData();
                toast = failed == 0 ? Strings.ConfigDeleteDone : Strings.ConfigDeletePartial;
                toastFailed = failed != 0;
                toastUntilUtc = DateTime.UtcNow + ToastDuration;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ConfigCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private readonly record struct VerdictRow(uint RowId, string QuestName, string Verdict, Vector4 Color, string Note, string Date);
}
