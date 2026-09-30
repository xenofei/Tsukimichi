using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The main window's pane splitter arithmetic (feature plan v4 L1): floors, collapse order, the strip, scale.</summary>
public class PaneLayoutTests
{
    private const float Rail = 136f;

    /// <summary>A body wide enough for everything at its default at scale 1: rail, gutters, 300 + 600 + 360.</summary>
    private const float Roomy = Rail + (3f * PaneLayout.GutterLogical) + 300f + 600f + 360f;

    [Fact]
    public void The_floors_and_defaults_are_the_approved_ones()
    {
        Assert.Equal(180f, PaneLayout.TreeFloorLogical);
        Assert.Equal(320f, PaneLayout.CentreFloorLogical);
        Assert.Equal(260f, PaneLayout.DetailFloorLogical);
        Assert.Equal(300f, PaneLayout.TreeDefaultLogical);
        Assert.Equal(360f, PaneLayout.DetailDefaultLogical);
        Assert.Equal(56f, PaneLayout.TreeStripLogical);
        Assert.True(PaneLayout.StripSnapLogical < PaneLayout.TreeFloorLogical);
        Assert.True(PaneLayout.StripSnapLogical + PaneLayout.StripHysteresisLogical <= PaneLayout.TreeFloorLogical);
        Assert.True(PaneLayout.TreeStripLogical < PaneLayout.StripSnapLogical);
    }

    [Fact]
    public void A_roomy_body_gives_each_side_what_was_asked_and_the_centre_the_rest()
    {
        var w = PaneLayout.Solve(Roomy, Rail, 300f, 360f, 1f);
        Assert.Equal(300f, w.Tree);
        Assert.Equal(360f, w.Detail);
        Assert.Equal(600f, w.Centre, 3);
        Assert.Equal(Rail, w.Rail);
        Assert.Equal(PaneLayout.GutterLogical, w.Gutter);
        Assert.Equal(Roomy, w.Total, 3);
        Assert.False(w.TreeStrip);
    }

    [Fact]
    public void Widening_the_body_widens_only_the_centre()
    {
        var a = PaneLayout.Solve(Roomy, Rail, 300f, 360f, 1f);
        var b = PaneLayout.Solve(Roomy + 200f, Rail, 300f, 360f, 1f);
        Assert.Equal(a.Tree, b.Tree);
        Assert.Equal(a.Detail, b.Detail);
        Assert.Equal(a.Centre + 200f, b.Centre, 3);
    }

    [Theory]
    [InlineData(10f, 20f)]
    [InlineData(0f, 0f)]
    [InlineData(-50f, -1f)]
    [InlineData(179f, 259f)]
    public void Asked_widths_under_a_floor_are_the_floor(float left, float right)
    {
        var w = PaneLayout.Solve(Roomy, Rail, left, right, 1f);
        Assert.True(left <= 0f ? w.Tree == PaneLayout.TreeDefaultLogical : w.Tree == PaneLayout.TreeFloorLogical, $"tree {w.Tree}");
        Assert.True(right <= 0f ? w.Detail == PaneLayout.DetailDefaultLogical : w.Detail == PaneLayout.DetailFloorLogical, $"detail {w.Detail}");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Unreadable_asked_widths_are_the_defaults(float value)
    {
        var w = PaneLayout.Solve(Roomy, Rail, value, value, 1f);
        Assert.Equal(PaneLayout.TreeDefaultLogical, w.Tree);
        Assert.Equal(PaneLayout.DetailDefaultLogical, w.Detail);
    }

    [Fact]
    public void Running_out_of_room_shrinks_the_detail_pane_first()
    {
        // 100 px less than the roomy body's centre needs beyond its floor... take 380 away: the centre drops to its floor
        // (600 → 320 is 280 px), then the detail pane gives 100.
        var w = PaneLayout.Solve(Roomy - 380f, Rail, 300f, 360f, 1f);
        Assert.Equal(300f, w.Tree);
        Assert.Equal(260f, w.Detail);
        Assert.Equal(PaneLayout.CentreFloorLogical, w.Centre, 3);
    }

    [Fact]
    public void The_tree_shrinks_only_once_the_detail_pane_is_at_its_floor()
    {
        var w = PaneLayout.Solve(Roomy - 380f - 50f, Rail, 300f, 360f, 1f);
        Assert.Equal(PaneLayout.DetailFloorLogical, w.Detail);
        Assert.Equal(250f, w.Tree);
        Assert.Equal(PaneLayout.CentreFloorLogical, w.Centre, 3);
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1f)]
    [InlineData(1.15f)]
    [InlineData(1.3f)]
    [InlineData(1.6f)]
    [InlineData(2.5f)]
    public void Floors_hold_down_to_the_sum_of_the_floors(float scale)
    {
        // No slack: the whole-pixel floors and gutters are what MinContentPx adds up, within the reserve.
        var minimum = (Rail * scale) + PaneLayout.MinContentPx(scale);
        Assert.InRange(PaneLayout.MinContentPx(scale) - (PaneLayout.MinContentLogical * scale), -1.5f, PaneLayout.RoundingReservePx);
        for (var total = minimum; total < minimum + (900f * scale); total += 7f)
        {
            var w = PaneLayout.Solve(total, Rail * scale, 700f, 700f, scale);
            Assert.True(w.Tree >= (PaneLayout.TreeFloorLogical * scale) - 0.01f, $"tree {w.Tree} at {total}");
            Assert.True(w.Detail >= (PaneLayout.DetailFloorLogical * scale) - 0.01f, $"detail {w.Detail} at {total}");
            Assert.True(w.Centre >= (PaneLayout.CentreFloorLogical * scale) - 0.01f, $"centre {w.Centre} at {total}");
            Assert.Equal(total, w.Total, 2);
        }
    }

    [Theory]
    [InlineData(1.3f, 234f, 338f)]
    [InlineData(1.15f, 207f, 299f)]
    [InlineData(1f, 180f, 260f)]
    public void Floors_round_up_to_whole_pixels(float scale, float tree, float detail)
    {
        Assert.Equal(tree, PaneLayout.FloorPx(PaneLayout.TreeFloorLogical, scale));
        Assert.Equal(detail, PaneLayout.FloorPx(PaneLayout.DetailFloorLogical, scale));
        var w = PaneLayout.Solve((Rail * scale) + PaneLayout.MinContentPx(scale), Rail * scale, 180f, 260f, scale);
        Assert.Equal(tree, w.Tree);
        Assert.Equal(detail, w.Detail);
    }

    [Fact]
    public void Under_the_floors_the_centre_gives_way_last_and_nothing_goes_negative()
    {
        var floors = Rail + PaneLayout.MinContentLogical;
        var w = PaneLayout.Solve(floors - 100f, Rail, 300f, 360f, 1f);
        Assert.Equal(PaneLayout.TreeFloorLogical, w.Tree);
        Assert.Equal(PaneLayout.DetailFloorLogical, w.Detail);
        Assert.Equal(PaneLayout.CentreFloorLogical - 100f, w.Centre, 3);

        var tiny = PaneLayout.Solve(Rail + 20f, Rail, 300f, 360f, 1f);
        Assert.True(tiny.Tree >= 0f && tiny.Centre >= 0f && tiny.Detail >= 0f);
        Assert.Equal(0f, tiny.Centre);

        var none = PaneLayout.Solve(0f, Rail, 300f, 360f, 1f);
        Assert.Equal(0f, none.Tree);
        Assert.Equal(0f, none.Centre);
        Assert.Equal(0f, none.Detail);
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1.15f)]
    [InlineData(1.6f)]
    public void Asked_widths_are_logical_so_they_follow_the_scale(float scale)
    {
        var w = PaneLayout.Solve(3000f * scale, Rail * scale, 300f, 400f, scale);
        Assert.Equal(MathF.Floor(300f * scale), w.Tree);
        Assert.Equal(MathF.Floor(400f * scale), w.Detail);
        Assert.Equal(MathF.Round(PaneLayout.GutterLogical * scale), w.Gutter);
    }

    [Fact]
    public void Side_widths_are_whole_pixels_and_the_centre_takes_the_remainder()
    {
        var w = PaneLayout.Solve(1333.7f, 156.4f, 301.3f, 359.9f, 1.15f);
        Assert.Equal(MathF.Floor(w.Tree), w.Tree);
        Assert.Equal(MathF.Floor(w.Detail), w.Detail);
        Assert.Equal(1333.7f, w.Total, 2);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void An_unreadable_scale_counts_as_one(float scale)
    {
        Assert.Equal(PaneLayout.Solve(Roomy, Rail, 300f, 360f, 1f), PaneLayout.Solve(Roomy, Rail, 300f, 360f, scale));
    }

    [Fact]
    public void The_strip_is_56_logical_and_cannot_shrink()
    {
        var w = PaneLayout.Solve(Roomy, Rail, 300f, 360f, 1.15f, treeStrip: true);
        Assert.True(w.TreeStrip);
        Assert.Equal(MathF.Ceiling(56f * 1.15f), w.Tree);

        var tight = PaneLayout.Solve(Rail + (3f * PaneLayout.GutterLogical) + 56f + 320f + 260f, Rail, 300f, 360f, 1f, treeStrip: true);
        Assert.Equal(56f, tight.Tree);
        Assert.Equal(PaneLayout.DetailFloorLogical, tight.Detail);
        Assert.Equal(PaneLayout.CentreFloorLogical, tight.Centre, 3);
    }

    [Fact]
    public void The_strip_gives_its_width_to_the_centre()
    {
        var open = PaneLayout.Solve(Roomy, Rail, 300f, 360f, 1f);
        var strip = PaneLayout.Solve(Roomy, Rail, 300f, 360f, 1f, treeStrip: true);
        Assert.Equal(open.Centre + (300f - 56f), strip.Centre, 3);
        Assert.Equal(open.Detail, strip.Detail);
    }

    [Theory]
    [InlineData(300f, false, false)]
    [InlineData(180f, false, false)]
    [InlineData(150f, false, false)]
    [InlineData(149.9f, false, true)]
    [InlineData(100f, false, true)]
    [InlineData(-20f, false, true)]
    [InlineData(56f, true, true)]
    [InlineData(150f, true, true)]
    [InlineData(165.9f, true, true)]
    [InlineData(166f, true, false)]
    [InlineData(400f, true, false)]
    public void The_strip_snaps_under_150_and_opens_past_166(float wanted, bool wasStrip, bool expected)
    {
        Assert.Equal(expected, PaneLayout.Strip(wanted, wasStrip));
    }

    [Fact]
    public void An_unreadable_drag_keeps_the_strip_state()
    {
        Assert.True(PaneLayout.Strip(float.NaN, wasStrip: true));
        Assert.False(PaneLayout.Strip(float.NaN, wasStrip: false));
    }

    [Fact]
    public void A_drag_back_and_forth_across_the_snap_does_not_flicker()
    {
        // The mouse wobbles ±5 around 150 after the tree snapped shut: it stays shut until it passes 166.
        var strip = false;
        foreach (var wanted in new[] { 200f, 170f, 151f, 149f, 153f, 147f, 155f, 160f, 165f })
        {
            strip = PaneLayout.Strip(wanted, strip);
        }

        Assert.True(strip);
        strip = PaneLayout.Strip(170f, strip);
        Assert.False(strip);
        strip = PaneLayout.Strip(160f, strip);
        Assert.False(strip);
    }

    [Theory]
    [InlineData(300f, 115f, 1.15f, 400f)]
    [InlineData(300f, -46f, 1.15f, 260f)]
    [InlineData(56f, 0f, 2f, 56f)]
    public void A_drag_moves_the_width_by_the_mouse_in_logical_units(float start, float delta, float scale, float expected)
    {
        Assert.Equal(expected, PaneLayout.Dragged(start, delta, scale), 3);
    }

    [Fact]
    public void A_drag_with_an_unreadable_delta_stays_put()
    {
        Assert.Equal(300f, PaneLayout.Dragged(300f, float.NaN, 1f));
        Assert.Equal(300f, PaneLayout.Dragged(300f, 10f, 0f) - 10f);
    }

    [Theory]
    [InlineData(300f, 300f)]
    [InlineData(100f, 180f)]
    [InlineData(5000f, 1600f)]
    [InlineData(0f, 300f)]
    [InlineData(float.NaN, 300f)]
    public void Stored_tree_widths_are_sanitised(float stored, float expected)
    {
        Assert.Equal(expected, PaneLayout.SanitizeTree(stored));
    }

    [Theory]
    [InlineData(360f, 360f)]
    [InlineData(100f, 260f)]
    [InlineData(float.PositiveInfinity, 360f)]
    public void Stored_detail_widths_are_sanitised(float stored, float expected)
    {
        Assert.Equal(expected, PaneLayout.SanitizeDetail(stored));
    }
}
