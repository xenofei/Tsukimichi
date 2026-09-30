using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The confirm popup both Moonlit verdicts go through ("Mark as unique…" in the detail pane, "Not unique (hide)…" in a
/// Moonlit row's context menu) and the "Marked unique · Undo" line shown for eight seconds after either. The popup is
/// begun by <see cref="Draw"/> in the owning pane's ID scope, so <see cref="Open"/> may be called from inside a
/// context menu: it only records the request, and the next <see cref="Draw"/> opens the popup (the pattern the
/// settings window uses for its second delete confirm). The note field takes focus when the popup appears and Enter
/// confirms; the confirm button is a <see cref="Chrome.HoldButton"/> (Shift and click, or hold 600 ms); Escape cancels.
/// </summary>
internal sealed class VerdictPrompt(string popupId)
{
    private const int NoteLength = 120;
    private const double UndoSeconds = 8.0;

    private static string ConfirmUniqueLabel => confirmUniqueLabelText.Value;

    private static readonly Localization.LocText confirmUniqueLabelText = new(static () => Strings.MarkUniqueConfirm + Chrome.HoldIdSuffix);
    private static string ConfirmHideLabel => confirmHideLabelText.Value;

    private static readonly Localization.LocText confirmHideLabelText = new(static () => Strings.MarkNotUniqueConfirm + Chrome.HoldIdSuffix);

    private readonly string popupId = popupId ?? throw new ArgumentNullException(nameof(popupId));
    private readonly ConfirmGate gate = new();

    private string noteBuffer = string.Empty;
    private uint rowId;
    private bool unique;
    private string question = string.Empty;
    private bool pendingOpen;

    private uint undoRowId;
    private bool undoUnique;
    private double undoUntil = -1.0;

    /// <summary>Asks for the popup on the next <see cref="Draw"/>. Safe from inside a menu.</summary>
    /// <param name="unique">True to vouch for the quest, false to hide it as not unique.</param>
    public void Open(uint rowId, bool unique, string questName)
    {
        this.rowId = rowId;
        this.unique = unique;
        question = unique
            ? string.Format(CultureInfo.CurrentCulture, Strings.VerdictQuestionUniqueFormat, questName)
            : string.Format(CultureInfo.CurrentCulture, Strings.VerdictQuestionHideFormat, questName);
        noteBuffer = string.Empty;
        gate.Cancel();
        pendingOpen = true;
    }

    /// <summary>
    /// Opens a requested popup and draws it while it is open. Call every frame from the pane's own scope (not from a
    /// menu or a table's inner window). Returns true on the frame a verdict was stored.
    /// </summary>
    public bool Draw(IUniqueOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (pendingOpen)
        {
            pendingOpen = false;
            ImGui.OpenPopup(popupId);
        }

        using var popup = ImRaii.Popup(popupId);
        if (!popup)
        {
            return false;
        }

        // Opened from a child whose own font scale is 1 (it inherits the window's), so the popup scales itself.
        UiMetrics.ApplyFontScale();
        ImGui.TextUnformatted(question);
        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(UiMetrics.Px(240f));
        var entered = ImGui.InputTextWithHint(
            "##verdictNote",
            unique ? Strings.MarkUniqueNoteHint : Strings.MarkNotUniqueNoteHint,
            ref noteBuffer,
            NoteLength,
            ImGuiInputTextFlags.EnterReturnsTrue);

        var confirmed = Chrome.HoldButton(unique ? ConfirmUniqueLabel : ConfirmHideLabel, gate);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.VerdictConfirmTooltip);
        }

        ImGui.SameLine();
        var cancelled = ImGui.Button(Strings.Cancel) || (ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.Escape, false));
        if (confirmed || entered)
        {
            overrides.Set(rowId, unique, noteBuffer);
            undoRowId = rowId;
            undoUnique = unique;
            undoUntil = ImGui.GetTime() + UndoSeconds;
            gate.Cancel();
            ImGui.CloseCurrentPopup();
            return true;
        }

        if (cancelled)
        {
            gate.Cancel();
            ImGui.CloseCurrentPopup();
        }

        return false;
    }

    /// <summary>"Marked unique · Undo" (or "Hidden as not unique · Undo") for eight seconds after a verdict; nothing otherwise. With <paramref name="forRowId"/> the line shows only while that quest is the one the verdict was given for.</summary>
    public void DrawUndo(IUniqueOverrides overrides, uint? forRowId = null)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (undoUntil < 0.0 || (forRowId is { } only && only != undoRowId))
        {
            return;
        }

        if (ImGui.GetTime() >= undoUntil)
        {
            undoUntil = -1.0;
            return;
        }

        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(undoUnique ? Strings.VerdictUndoMarkedUnique : Strings.VerdictUndoMarkedNotUnique);
        }

        ImGui.SameLine(0f, 0f);
        ImGui.TextDisabled(Strings.VerdictUndoSeparator);
        ImGui.SameLine(0f, 0f);
        if (ImGui.SmallButton(Strings.VerdictUndo))
        {
            overrides.Clear(undoRowId);
            undoUntil = -1.0;
            return;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.VerdictUndoTooltip);
        }
    }

    /// <summary>Whether the undo line is currently showing (the pane may want to keep space for it).</summary>
    public bool UndoShowing => undoUntil >= 0.0 && ImGui.GetTime() < undoUntil;
}
