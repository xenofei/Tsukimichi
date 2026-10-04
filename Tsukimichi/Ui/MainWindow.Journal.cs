using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Journal;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The journal count in the status bar (feature plan v7, 1.19.0, C9; spec-1.19 "In the status bar"): nothing below 25
/// slots used; "Journal 25/30" in Secondary at 25 and 26; the count in Text from 27 to 29; at 30 a copper dot beside
/// "Journal full · 30/30" in Text and Make room, a quiet button that opens the Make room popover over it
/// (<see cref="MakeRoomView"/>). The hover says what is left ("2 slots left"), or at 30 that Ready quests can't be
/// accepted. It follows the MSQ pill, or the New Game+ line while a session runs (C4 replaces the bar's left side, and
/// the count stays after it); its room is set aside before the other segments are fitted, so they end in an ellipsis
/// rather than it. Also the frame's journal state for the table ("Ready · ● journal full") and the hero. The strings are
/// rebuilt only when the count or the language changes.
/// </summary>
public sealed partial class MainWindow
{
    private MakeRoomView? makeRoom;
    private (int Version, int Language) journalKey = (-1, -1);
    private JournalSlots? journalSlots;
    private string journalText = string.Empty;
    private string journalTooltip = string.Empty;
    private Vector2 makeRoomAnchor;

    /// <summary>
    /// Before the panes draw: the Make room view (built once a session exists), the viewed character's journal slots,
    /// and what the table and the detail pane need of them this frame.
    /// </summary>
    private void PrepareJournal(SessionState session)
    {
        if (makeRoom is null)
        {
            makeRoom = new MakeRoomView(session, links)
            {
                DutyRuns = () => detailPane.DutyRuns?.Invoke(),
                RewardEntries = rowId => detailPane.RewardEntries?.Invoke(rowId) ?? [],
                Stock = () => detailPane.Stock,
            };
            detailPane.MakeRoom = makeRoom;
        }

        charactersPane?.AttachMakeRoom(makeRoom);

        var key = (session.Version, Localization.Loc.Version);
        if (key != journalKey)
        {
            journalKey = key;
            journalSlots = session.ViewedSnapshot is { } snapshot && session.Bundle is { } bundle ? JournalSlots.Of(snapshot, bundle.Catalog) : null;
            journalText = string.Empty;
            journalTooltip = string.Empty;
            if (journalSlots is { Bar: not JournalBar.Hidden } slots)
            {
                var culture = CultureInfo.CurrentCulture;
                journalText = string.Format(culture, slots.Bar == JournalBar.Full ? Strings.JournalBarFullFormat : Strings.JournalBarFormat, slots.Used, slots.Cap);
                journalTooltip = slots.Bar == JournalBar.Full
                    ? Strings.JournalBarFullTooltip
                    : slots.Left == 1 ? Strings.JournalBarLeftOne : string.Format(culture, Strings.JournalBarLeftFormat, slots.Left);
            }
        }

        tablePane.Journal = journalSlots;
    }

    /// <summary>The bar's journal segment as it would be drawn, the separator before it included; 0 when it is hidden.</summary>
    private float JournalSegmentWidth(float separatorWidth, float gap)
    {
        if (journalSlots is not { Bar: not JournalBar.Hidden } slots || journalText.Length == 0)
        {
            return 0f;
        }

        var width = separatorWidth + ImGui.CalcTextSize(journalText).X;
        if (slots.Bar == JournalBar.Full)
        {
            width += UiMetrics.Px(12f) + gap + MakeRoomButtonWidth();
        }

        return width;
    }

    private static float MakeRoomButtonWidth() => ImGui.CalcTextSize(Strings.MakeRoom).X + (2f * UiMetrics.Px(8f));

    /// <summary>
    /// Draws the journal segment from <paramref name="x"/> (a separator first, unless it opens the bar) and returns where
    /// the next segment starts. The copper dot sits beside the words; it never carries the meaning alone.
    /// </summary>
    private float DrawJournalSegment(ImDrawListPtr dl, float x, float originX, float textY, float line, float gap, string separator)
    {
        if (journalSlots is not { Bar: not JournalBar.Hidden } slots || journalText.Length == 0)
        {
            return x;
        }

        var s = Theme.Surface;
        x = x > originX ? StatusSeparatorAt(dl, x, textY, gap, separator) : x;
        var textMin = x;
        if (slots.Bar == JournalBar.Full)
        {
            var radius = UiMetrics.Px(3f);
            dl.AddCircleFilled(new Vector2(x + radius, textY + (line * 0.5f)), radius, Theme.U32(Theme.Copper), 12);
            x += UiMetrics.Px(12f);
        }

        x = StatusText(x, textY, journalText, Theme.U32(slots.Bar == JournalBar.Quiet ? s.TextSecondary : s.Text));
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(textMin, textY), new Vector2(x, textY + line)))
        {
            UiMetrics.Tooltip(journalText, journalTooltip);
        }

        if (slots.Bar != JournalBar.Full || makeRoom is not { } view)
        {
            return x;
        }

        x += gap;
        var width = MakeRoomButtonWidth();
        var min = new Vector2(x, textY - UiMetrics.Px(1f));
        if (QuietBarButton("##makeRoomButton", Strings.MakeRoom, min, new Vector2(width, line + UiMetrics.Px(2f))))
        {
            view.Open();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MakeRoomTooltip);
        }

        // The popover opens over the bar, its left edge on the button's.
        makeRoomAnchor = new Vector2(x, min.Y - UiMetrics.Px(4f));
        return x + width;
    }

    /// <summary>The Make room popover the status bar opened, in the bar's ID scope, every frame it is open.</summary>
    private void DrawMakeRoomPopover()
    {
        makeRoom?.DrawPopover(makeRoomAnchor, above: true, ImGui.GetWindowSize().Y);
    }
}
