using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Abandoned (N)" section of the Characters dashboard (P10): the quests the viewed character dropped from the
/// journal and has not taken up again, newest first, from <see cref="Game.SessionState.Abandoned"/>. Each row: the
/// quest's state moon, its name (click reveals it in the Journal; the tooltip names the giver), "step 3 of 5 · 2 days
/// ago", and Flag, Teleport (through Lifestream; greyed and naming it without), Walk (vnavmesh) and Reveal. "Show in Journal" opens the Journal under the Abandoned filter.
/// Rows are rebuilt once per session version and once a minute (the ages tick).
/// </summary>
public sealed partial class CharactersPane
{
    private AbandonedRow[] abandonedRows = [];
    /// <summary>"Abandoned (N)" with its stable "###abandonedHeader" id, built when the rows are, not per frame.</summary>
    private string abandonedHeader = HeaderLabel(0);
    private int abandonedVersion = -1;
    private long abandonedMinute = -1;

    /// <summary>Map flags and travel for the Abandoned rows; set by the plugin. Null hides Flag, Teleport and Walk.</summary>
    public GameLinks? Links { get; set; }

    private void DrawAbandoned(UiState ui)
    {
        RefreshAbandoned();
        using var id = ImRaii.PushId("abandoned");
        var open = ImGui.CollapsingHeader(abandonedHeader, ImGuiTreeNodeFlags.DefaultOpen);
        if (!open)
        {
            return;
        }

        if (abandonedRows.Length == 0)
        {
            ImGui.TextDisabled(Strings.AbandonedNone);
            return;
        }

        if (TravelControls.RowButton("##showAbandoned", PillIcon.JournalBook, Strings.AbandonedShowInJournal))
        {
            ui.ShowAbandoned();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AbandonedShowInJournalTooltip);
        }

        const ImGuiTableFlags Flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;
        var line = ImGui.GetTextLineHeight();
        var width = ImGui.GetContentRegionAvail().X;

        // Flag, Teleport and Reveal fold into one "…" in a narrow pane (as Flight's); the actions and the status never
        // give way to the name, which ends in an ellipsis instead (feature plan v4 L6).
        var fold = PaneFit.FoldActions(width / UiMetrics.Scale);
        var moreSize = MoreSize(line);
        var glyph = MathF.Max(line * 1.4f, UiMetrics.InlineGlyphSize(line));
        var nameMin = UiMetrics.Px(LayoutBudgets.RowNameMinLogical);
        var statusMin = AbandonedStatusMin();
        var actions = fold ? moreSize : AbandonedActionsWidth();
        Span<ColumnSpec> specs = stackalloc ColumnSpec[4];
        specs[AbandonedState] = new ColumnSpec(0, glyph, glyph);
        specs[AbandonedQuest] = new ColumnSpec(0, nameMin, nameMin, 3f);
        specs[AbandonedStatus] = new ColumnSpec(0, statusMin, statusMin, 2f);
        specs[AbandonedActions] = new ColumnSpec(0, actions, actions);
        abandonedColumns.Plan(width, specs);
        using var table = abandonedColumns.Begin("##abandoned", Flags);
        if (!table.Success)
        {
            return;
        }

        abandonedColumns.Setup(AbandonedState, "##state");
        abandonedColumns.Setup(AbandonedQuest, Strings.CharactersColumnQuest);
        abandonedColumns.Setup(AbandonedStatus, Strings.CharactersColumnStatus);
        abandonedColumns.Setup(AbandonedActions, "##actions");

        for (var i = 0; i < abandonedRows.Length; i++)
        {
            var row = abandonedRows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            if (abandonedColumns.Next(AbandonedState))
            {
                MoonGlyph.DrawInline(row.State, UiMetrics.InlineGlyphSize(line));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest));
                }
            }

            if (abandonedColumns.Next(AbandonedQuest))
            {
                if (row.Quest is { } quest)
                {
                    if (Chrome.EllipsisSelectable(row.Name, false, 0f, out var cut))
                    {
                        Reveal(ui, quest);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        UiMetrics.Tooltip(cut ? row.NameAndTooltip : row.Tooltip);
                    }
                }
                else
                {
                    Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                }
            }

            if (abandonedColumns.Next(AbandonedStatus))
            {
                var s = Theme.Surface;
                Chrome.StatusText(row.Detail, ImGui.GetContentRegionAvail().X, s.Text, s.TextSecondary);
            }

            if (abandonedColumns.Next(AbandonedActions) && row.Quest is { } target)
            {
                if (fold)
                {
                    DrawAbandonedMenu(ui, target, moreSize);
                }
                else
                {
                    DrawAbandonedActions(ui, target);
                }
            }
        }
    }

    // The Abandoned table's columns, in display order.
    private const int AbandonedState = 0;
    private const int AbandonedQuest = 1;
    private const int AbandonedStatus = 2;
    private const int AbandonedActions = 3;
    private const string AbandonedMenuId = "##abandonedMenu";
    private readonly ColumnFit abandonedColumns = new(4);

    /// <summary>The "…" button's side in a table row.</summary>
    private static float MoreSize(float line) => MathF.Min(UiMetrics.MinTarget, MathF.Max(line, UiMetrics.RowIconSize));

    /// <summary>The status' least width: the widest step ("step 3 of 5", never cut) and a little of the age after it.</summary>
    private float AbandonedStatusMin()
    {
        var widest = 0f;
        foreach (var row in abandonedRows)
        {
            widest = MathF.Max(widest, ImGui.CalcTextSize(row.Detail.AsSpan(0, TableGeometry.StateWordLength(row.Detail))).X);
        }

        return widest + UiMetrics.Px(24f);
    }

    /// <summary>Flag, Teleport, Walk (when shown) and Reveal side by side.</summary>
    private float AbandonedActionsWidth()
    {
        var style = ImGui.GetStyle();
        var width = TravelControls.RowButtonWidth(ActionGlyphs.Reveal, Strings.AbandonedReveal);
        if (Links is { } links)
        {
            width += TravelControls.FlagWidth(Strings.AbandonedFlag) + style.ItemSpacing.X;
            width += TravelControls.ButtonsWidth(links, Strings.AbandonedTeleport);
        }

        return width;
    }

    /// <summary>The row's actions folded into one "…" button and its menu (a narrow pane).</summary>
    private void DrawAbandonedMenu(UiState ui, QuestRecord quest, float size)
    {
        Keyboard.MoreButton("##more", AbandonedMenuId, ImGui.GetCursorScreenPos(), size);
        using var popup = ImRaii.Popup(AbandonedMenuId);
        if (!popup)
        {
            return;
        }

        // Opened from the centre column (own font scale 1), so the menu scales itself.
        UiMetrics.ApplyFontScale();
        if (Links is { } links)
        {
            if (ImGui.MenuItem(Strings.AbandonedFlag, enabled: links.CanFlagMap(quest)))
            {
                links.FlagMap(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.AbandonedFlagTooltip);
            }

            TravelControls.MenuItems(links, quest, Strings.AbandonedTeleport);
        }

        if (ImGui.MenuItem(Strings.AbandonedReveal))
        {
            Reveal(ui, quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AbandonedRevealTooltip);
        }
    }

    /// <summary>Flag (when the giver has a map spot), Teleport, Walk (when shown) and Reveal.</summary>
    private void DrawAbandonedActions(UiState ui, QuestRecord quest)
    {
        if (Links is { } links)
        {
            if (TravelControls.FlagButton(Strings.AbandonedFlag, links.CanFlagMap(quest)))
            {
                links.FlagMap(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.AbandonedFlagTooltip);
            }

            TravelControls.Buttons(links, quest, Strings.AbandonedTeleport);
            ImGui.SameLine();
        }

        if (TravelControls.RowButton("##reveal", ActionGlyphs.Reveal, Strings.AbandonedReveal))
        {
            Reveal(ui, quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AbandonedRevealTooltip);
        }
    }

    /// <summary>Rebuilds the rows from the viewed character's ledger when the session changed or a minute passed.</summary>
    private void RefreshAbandoned()
    {
        var now = DateTime.UtcNow;
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (abandonedVersion == session.Version && abandonedMinute == minute)
        {
            return;
        }

        abandonedVersion = session.Version;
        abandonedMinute = minute;
        var ledger = session.Abandoned;
        var catalog = session.Bundle?.Catalog;
        var rows = new List<AbandonedRow>(ledger.Count);
        foreach (var entry in AbandonedLedger.Newest(ledger))
        {
            var quest = catalog?.GetByRowId(entry.RowId);
            var state = session.States.TryGetValue(entry.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            // Through the spoiler shield (T19): an abandoned main scenario quest far ahead reads as its placeholder.
            var name = quest is null ? string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, entry.RowId) : session.Spoilers.DisplayName(quest);
            var when = string.Format(CultureInfo.CurrentCulture, Strings.AbandonedAtFormat, entry.AbandonedUtc.ToLocalTime().ToString(Strings.DateTimeFormat, CultureInfo.CurrentCulture));
            var tooltip = quest?.Issuer is { } issuer && issuer.Name.Length > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.AbandonedGiverFormat, issuer.Name) + "\n" + when
                : when;
            rows.Add(new AbandonedRow(quest, name, state, AbandonedLedger.Describe(entry, now), tooltip, name + "\n" + tooltip));
        }

        abandonedRows = rows.ToArray();
        abandonedHeader = HeaderLabel(abandonedRows.Length);
    }

    private static string HeaderLabel(int count)
    {
        return string.Format(CultureInfo.CurrentCulture, Strings.AbandonedHeaderFormat, count) + "###abandonedHeader";
    }

    /// <param name="NameAndTooltip">The name over the tooltip, the hover text of a name cut short.</param>
    private sealed record AbandonedRow(QuestRecord? Quest, string Name, QuestState State, string Detail, string Tooltip, string NameAndTooltip);
}
