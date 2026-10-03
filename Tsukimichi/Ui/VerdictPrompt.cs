using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Moonlit verdict actions both panes share (feature plan v6 S1, owner point 9): "Mark as unique" in the detail
/// pane, "Not unique (hide)" in a Moonlit row's menu, and "Restore shipped verdict" in both. Each is an armed item of the
/// safety table (<see cref="SafetyRules"/>): it acts only while Ctrl or Shift is held (or on two clicks, if the user
/// chose that), saves at once and puts up the floating Undo (<see cref="UndoToast"/>); a verdict's toast also offers
/// "Add note", which opens the small note popup. The popup is begun by <see cref="Draw"/> in the owning pane's ID scope,
/// so <see cref="OpenNote"/> may be called from anywhere (the toast, a menu): it only records the request, and the next
/// <see cref="Draw"/> opens it. The note field takes focus, Enter never saves (only the Save button does), and Escape
/// cancels.
/// </summary>
internal sealed class VerdictPrompt(string popupId)
{
    private const int NoteLength = 120;

    private readonly string popupId = popupId ?? throw new ArgumentNullException(nameof(popupId));
    private readonly ClickGuard verdictGuard = new();
    private readonly ClickGuard restoreGuard = new();

    private string noteBuffer = string.Empty;
    private uint noteRowId;
    private bool noteUnique;
    private string question = string.Empty;
    private bool pendingOpen;

    /// <summary>"Mark as unique": the armed button of the detail pane. <paramref name="questName"/> is the shown (spoiler-safe) name.</summary>
    public void DrawMarkUniqueButton(IUniqueOverrides overrides, uint rowId, string questName)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (Chrome.ArmedButton(Strings.MarkUnique, verdictGuard, GuardedAction.MarkUnique, Strings.MarkUniqueTooltip, rowId))
        {
            Give(overrides, rowId, unique: true, questName);
        }
    }

    /// <summary>"Not unique (hide)": the armed item of a Moonlit row's context menu.</summary>
    public void DrawNotUniqueMenuItem(IUniqueOverrides overrides, uint rowId, string questName)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (Chrome.ArmedMenuItem(Strings.MoonlitMarkNotUnique, verdictGuard, GuardedAction.MarkNotUnique, Strings.MoonlitMarkNotUniqueTooltip, rowId))
        {
            Give(overrides, rowId, unique: false, questName);
        }
    }

    /// <summary>"Restore shipped verdict" as an armed button (the detail pane, Settings' verdict list).</summary>
    public void DrawRestoreButton(IUniqueOverrides overrides, uint rowId, string label, string tooltip)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (Chrome.ArmedButton(label, restoreGuard, GuardedAction.RestoreVerdict, tooltip, rowId))
        {
            Restore(overrides, rowId);
        }
    }

    /// <summary>"Restore shipped verdict" as an armed item of a Moonlit row's context menu.</summary>
    public void DrawRestoreMenuItem(IUniqueOverrides overrides, uint rowId)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (Chrome.ArmedMenuItem(Strings.MoonlitRestoreOverride, restoreGuard, GuardedAction.RestoreVerdict, Strings.RestoreOverrideTooltip, rowId))
        {
            Restore(overrides, rowId);
        }
    }

    /// <summary>Stores the verdict at once, with no note, and puts up "Marked unique · Undo · Add note".</summary>
    private void Give(IUniqueOverrides overrides, uint rowId, bool unique, string questName)
    {
        var before = overrides.Get(rowId);
        overrides.Set(rowId, unique, null);
        UndoToast.Show(
            unique ? Strings.VerdictUndoMarkedUnique : Strings.VerdictUndoMarkedNotUnique,
            () => PutBack(overrides, rowId, before),
            Strings.UndoToastAddNote,
            () => OpenNote(rowId, unique, questName));
    }

    /// <summary>Clears the user's verdict (the shipped data applies again) and puts up "Verdict restored · Undo".</summary>
    public static void Restore(IUniqueOverrides overrides, uint rowId)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (overrides.Get(rowId) is not { } before)
        {
            return;
        }

        overrides.Clear(rowId);
        UndoToast.Show(Strings.UndoToastVerdictRestored, () => PutBack(overrides, rowId, before));
    }

    /// <summary>The verdict as it was: the earlier one back, or none.</summary>
    private static void PutBack(IUniqueOverrides overrides, uint rowId, UniqueOverride? before)
    {
        if (before is null)
        {
            overrides.Clear(rowId);
        }
        else
        {
            overrides.PutBack([KeyValuePair.Create(rowId, before)]);
        }
    }

    /// <summary>Asks for the note popup on the next <see cref="Draw"/>. Safe from inside a menu or the Undo toast.</summary>
    public void OpenNote(uint rowId, bool unique, string questName)
    {
        noteRowId = rowId;
        noteUnique = unique;
        question = string.Format(CultureInfo.CurrentCulture, Strings.VerdictNoteQuestionFormat, questName);
        noteBuffer = string.Empty;
        pendingOpen = true;
    }

    /// <summary>
    /// Opens a requested note popup and draws it while it is open. Call every frame from the pane's own scope (not from
    /// a menu or a table's inner window). Returns true on the frame a note was saved.
    /// </summary>
    public bool Draw(IUniqueOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (pendingOpen)
        {
            pendingOpen = false;
            noteBuffer = overrides.Get(noteRowId)?.Note ?? string.Empty;
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

        // Enter in the field never saves (owner point 9): the field reports no Enter, only the Save button saves.
        ImGui.SetNextItemWidth(UiMetrics.Px(240f));
        ImGui.InputTextWithHint(
            "##verdictNote",
            noteUnique ? Strings.MarkUniqueNoteHint : Strings.MarkNotUniqueNoteHint,
            ref noteBuffer,
            NoteLength);

        var saved = ImGui.Button(Strings.VerdictNoteSave);
        ImGui.SameLine();
        var cancelled = ImGui.Button(Strings.Cancel) || (ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.Escape, false));
        if (saved)
        {
            ImGui.CloseCurrentPopup();
            if (overrides.Get(noteRowId) is not { } before)
            {
                // The verdict was undone (here or in another game client) while the popup was open: nothing to note.
                return false;
            }

            overrides.Set(noteRowId, before.Unique, noteBuffer);
            var rowId = noteRowId;
            UndoToast.Show(Strings.UndoToastNoteSaved, () => PutBack(overrides, rowId, before));
            return true;
        }

        if (cancelled)
        {
            ImGui.CloseCurrentPopup();
        }

        return false;
    }
}
