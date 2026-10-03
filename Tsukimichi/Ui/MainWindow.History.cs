using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Back and forward through the quests the player looked at (feature plan v7 N1). The window watches the selection
/// once a frame, after every pane drew, so every way a quest gets selected is recorded the same: a row click, a
/// prerequisite or "unlocks next" link, the search, Path, Next stops, a command or another plugin's IPC (a change made
/// while the window was closed is seen on its next draw). Back and Forward land without recording. The inputs are the
/// toolbar's two round buttons, mouse buttons 4 and 5 while the pointer is over the window, and Alt+Left and Alt+Right
/// while it has the keys and no text field is active. The history logic is <see cref="QuestHistory"/> (Core).
/// </summary>
public sealed partial class MainWindow
{
    private static readonly string BackIcon = FontAwesomeIcon.AngleLeft.ToIconString();
    private static readonly string ForwardIcon = FontAwesomeIcon.AngleRight.ToIconString();

    // ImGui's mouse buttons 4 and 5 (X1 and X2); the binding names only the first three.
    private const ImGuiMouseButton MouseBack = (ImGuiMouseButton)3;
    private const ImGuiMouseButton MouseForward = (ImGuiMouseButton)4;

    private readonly QuestHistory history = new();

    /// <summary>The selection the history last saw, so a change is recorded once.</summary>
    private uint? historySeen;

    private Func<uint, bool>? questExists;

    /// <summary>Whether the loaded catalog still has a quest: the history steps over quests it no longer has.</summary>
    private Func<uint, bool> QuestExists => questExists ??= id => plugin.Session?.Bundle?.Catalog.GetByRowId(id) is not null;

    /// <summary>
    /// Records a selection change since the last frame as a visit: a quest the catalog has, chosen by any means but Back
    /// and Forward. Nothing selected records nothing (Back then shows the last quest again), and neither does the tour,
    /// which selects its sample quest and puts the selection back when it ends.
    /// </summary>
    private void ObserveSelection()
    {
        var shown = ui.SelectedRowId;
        if (shown == historySeen)
        {
            return;
        }

        historySeen = shown;
        if (shown is { } rowId && !tourWasActive && QuestExists(rowId))
        {
            history.Visit(rowId);
        }
    }

    /// <summary>
    /// Mouse buttons 4 and 5 while the pointer is over this window (players often bind them in the game or a mouse
    /// tool, so only then), and Alt+Left and Alt+Right while it has the keys and no text field is active, unless Settings ›
    /// Advanced › Keyboard turned them off. Called from Draw while no tour runs; an open popup keeps them.
    /// </summary>
    private void HandleHistoryInput()
    {
        if (!plugin.Settings.ShortcutHistory)
        {
            return;
        }

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            if (ImGui.IsMouseClicked(MouseBack))
            {
                GoBack();
                return;
            }

            if (ImGui.IsMouseClicked(MouseForward))
            {
                GoForward();
                return;
            }
        }

        if (!Keyboard.WindowHasKeys())
        {
            return;
        }

        if (Keyboard.AltPressed(ImGuiKey.LeftArrow))
        {
            GoBack();
        }
        else if (Keyboard.AltPressed(ImGuiKey.RightArrow))
        {
            GoForward();
        }
    }

    /// <summary>Shows the quest before the one shown; nothing when there is none.</summary>
    private void GoBack()
    {
        if (history.Back(ui.SelectedRowId, QuestExists) is { } rowId)
        {
            Land(rowId);
        }
    }

    /// <summary>Shows the quest after the one shown; nothing when there is none.</summary>
    private void GoForward()
    {
        if (history.Forward(ui.SelectedRowId, QuestExists) is { } rowId)
        {
            Land(rowId);
        }
    }

    /// <summary>Selects <paramref name="rowId"/> without recording it; the tab, the scope and the filters stay as they are.</summary>
    private void Land(uint rowId)
    {
        ui.SelectedRowId = rowId;
        historySeen = rowId;
    }

    /// <summary>
    /// The toolbar's Back and Forward round buttons at <paramref name="min"/>, always drawn so the toolbar never moves:
    /// disabled when there is nowhere to go. The tooltip names the quest each would show (through the spoiler shield)
    /// and the keys, composed on hover only.
    /// </summary>
    private void DrawHistoryButtons(SessionState session, Vector2 min)
    {
        var shown = ui.SelectedRowId;
        var backTo = history.PeekBack(shown, QuestExists);
        var forwardTo = history.PeekForward(shown, QuestExists);

        ImGui.SetCursorScreenPos(min);
        if (Chrome.IconButtonRound("##historyBack", BackIcon, null, enabled: backTo is not null))
        {
            GoBack();
        }

        HistoryTooltip(session, backTo, forward: false);

        ImGui.SetCursorScreenPos(min + new Vector2(UiMetrics.MinTarget + UiMetrics.Px(ChromeBands.HistoryGapLogical), 0f));
        if (Chrome.IconButtonRound("##historyForward", ForwardIcon, null, enabled: forwardTo is not null))
        {
            GoForward();
        }

        HistoryTooltip(session, forwardTo, forward: true);
    }

    /// <summary>
    /// "Back to Blue Collar Work" over its keys, or "Nothing to go back to" while disabled; the keys line only while
    /// Settings › Advanced › Keyboard has them on.
    /// </summary>
    private void HistoryTooltip(SessionState session, uint? target, bool forward)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            return;
        }

        var title = forward ? Strings.HistoryForwardNone : Strings.HistoryBackNone;
        if (target is { } rowId && session.Bundle is { } bundle)
        {
            var name = session.Spoilers.DisplayName(bundle.Catalog, rowId, rowId.ToString(CultureInfo.InvariantCulture));
            title = string.Format(CultureInfo.CurrentCulture, forward ? Strings.HistoryForwardFormat : Strings.HistoryBackFormat, name);
        }

        var keys = plugin.Settings.ShortcutHistory ? (forward ? Strings.HistoryForwardKeys : Strings.HistoryBackKeys) : null;
        UiMetrics.Tooltip(title, keys);
    }
}
