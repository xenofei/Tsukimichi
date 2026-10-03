using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The selected row keeps its place on screen when the rows change (feature plan v6 U3): after a filter, a search, a
/// re-sort or a live update, the quest the player was looking at stays where it was instead of the list holding its pixel
/// offset and the row sliding away.
/// </summary>
public class ScrollAnchorTests
{
    private const float RowHeight = 30f;
    private const float View = 300f;

    private static QuestRow[] Rows(params uint[] ids) =>
        ids.Select(id => new QuestRow(Fixture.Quest(id), QuestState.Ready, string.Empty)).ToArray();

    private static QuestRow[] Range(uint from, int count) =>
        Rows(Enumerable.Range(0, count).Select(i => from + (uint)i).ToArray());

    private static Dictionary<uint, int> Index(QuestRow[] rows) =>
        rows.Select((r, i) => (r.Quest.RowId, i)).ToDictionary(p => p.RowId, p => p.i);

    [Fact]
    public void A_list_scrolled_to_its_end_stays_there_when_a_row_changes()
    {
        // The table's clip rectangle is 330 px tall and its frozen header takes the top 30: the rows see 300.
        const float clipTop = 100f;
        const float header = 30f;
        var view = ScrollAnchor.RowsView(clipTop, clipTop + View + header, clipTop + header, RowHeight);
        Assert.Equal(View, view);

        // Scrolled to the very end, the selection on the last row; a live update changes it but not the count.
        var rows = Range(1000, 100);
        var bottom = (rows.Length * RowHeight) - View;
        Assert.True(ScrollAnchor.TryCapture(rows, selectedIndex: 99, bottom, RowHeight, view, out var anchor));
        var updated = Range(1000, 100);
        var y = ScrollAnchor.Restore(in anchor, rows, Index(updated), updated.Length, RowHeight, view, out _);

        // It stays at the end; a view that counted the header would clamp one row short and the list would jump.
        Assert.Equal(bottom, y);
        Assert.NotEqual(bottom, ScrollAnchor.Restore(in anchor, rows, Index(updated), updated.Length, RowHeight, View + header, out _));
    }

    [Fact]
    public void The_rows_view_is_never_less_than_a_row()
    {
        Assert.Equal(RowHeight, ScrollAnchor.RowsView(0f, 20f, 15f, RowHeight));
        Assert.Equal(200f, ScrollAnchor.RowsView(0f, 200f, float.MinValue, RowHeight));
    }

    [Fact]
    public void The_selected_row_on_screen_is_the_anchor()
    {
        var rows = Range(1000, 100);
        Assert.True(ScrollAnchor.TryCapture(rows, selectedIndex: 40, scrollY: 1000f, RowHeight, View, out var anchor));
        Assert.Equal(1040u, anchor.RowId);
        Assert.True(anchor.Selected);
        Assert.Equal((40 * RowHeight) - 1000f, anchor.Offset);
    }

    [Fact]
    public void Without_a_selection_on_screen_the_first_visible_row_is()
    {
        var rows = Range(1000, 100);
        Assert.True(ScrollAnchor.TryCapture(rows, selectedIndex: 90, scrollY: 1000f, RowHeight, View, out var anchor));
        Assert.False(anchor.Selected);
        Assert.Equal(1033u, anchor.RowId);
        Assert.Equal((33 * RowHeight) - 1000f, anchor.Offset);

        Assert.True(ScrollAnchor.TryCapture(rows, selectedIndex: -1, scrollY: 0f, RowHeight, View, out anchor));
        Assert.Equal(1000u, anchor.RowId);
        Assert.False(ScrollAnchor.TryCapture([], 0, 0f, RowHeight, View, out _));
    }

    [Fact]
    public void A_filter_that_removes_rows_above_keeps_the_selection_in_place()
    {
        var old = Range(1000, 100);
        ScrollAnchor.TryCapture(old, 40, 1000f, RowHeight, View, out var anchor);
        var screenY = (40 * RowHeight) - 1000f;

        // Every even row goes: the selected 1040 is now at index 20.
        var filtered = old.Where(r => r.Quest.RowId % 2 == 0).ToArray();
        var scroll = ScrollAnchor.Restore(in anchor, old, Index(filtered), filtered.Length, RowHeight, View, out var landed);

        Assert.Equal(20, landed);
        Assert.Equal(screenY, (landed * RowHeight) - scroll, 3);
    }

    [Fact]
    public void A_re_sort_keeps_the_selection_in_place()
    {
        var old = Range(1000, 100);
        ScrollAnchor.TryCapture(old, 70, 1900f, RowHeight, View, out var anchor);
        var reversed = old.Reverse().ToArray();
        var scroll = ScrollAnchor.Restore(in anchor, old, Index(reversed), reversed.Length, RowHeight, View, out var landed);

        Assert.Equal(29, landed);
        Assert.True(anchor.Selected);
        Assert.Equal((70 * RowHeight) - 1900f, (landed * RowHeight) - scroll, 3);
    }

    [Fact]
    public void A_filtered_out_anchor_hands_its_place_to_the_nearest_neighbour()
    {
        var old = Range(1000, 100);
        ScrollAnchor.TryCapture(old, 50, 1350f, RowHeight, View, out var anchor);
        var screenY = (50 * RowHeight) - 1350f;

        // 1050 and 1051 go: 1049, one row above, is the nearest that is still listed, and stays where it was on screen.
        var filtered = old.Where(r => r.Quest.RowId is not (1050 or 1051)).ToArray();
        var scroll = ScrollAnchor.Restore(in anchor, old, Index(filtered), filtered.Length, RowHeight, View, out var landed);

        Assert.Equal(1049u, filtered[landed].Quest.RowId);
        Assert.Equal(screenY - RowHeight, (landed * RowHeight) - scroll, 3);
    }

    [Fact]
    public void The_scroll_stays_inside_the_new_list()
    {
        var old = Range(1000, 100);
        ScrollAnchor.TryCapture(old, 90, 2600f, RowHeight, View, out var anchor);

        // Only a few rows are left, all of them near the top: the list cannot scroll past its own end.
        var few = Rows(1088, 1089, 1090);
        var scroll = ScrollAnchor.Restore(in anchor, old, Index(few), few.Length, RowHeight, View, out _);
        Assert.Equal(0f, scroll);

        Assert.Equal(0f, ScrollAnchor.Restore(in anchor, old, new Dictionary<uint, int>(), 0, RowHeight, View, out var none));
        Assert.Equal(-1, none);
    }

    [Fact]
    public void A_reveal_puts_the_row_forty_percent_down_the_view()
    {
        Assert.Equal((50 * RowHeight) - (View * 0.4f), ScrollAnchor.Reveal(50, 100, RowHeight, View), 3);
        Assert.Equal(0f, ScrollAnchor.Reveal(1, 100, RowHeight, View));
        Assert.Equal((100 * RowHeight) - View, ScrollAnchor.Reveal(99, 100, RowHeight, View), 3);
    }
}
