using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The filter drawer's geometry (plan v7 UI-2, spec §2.1): exactly the tree column, sized to its content and capped at
/// the body, and the tree under it fading out with the drawer's fade and skipped once the drawer is opaque.
/// </summary>
public class DrawerLayoutTests
{
    [Theory]
    [InlineData(292f, 1000f, 292f)]
    [InlineData(180f, 1000f, 180f)]
    [InlineData(262f, 1000f, 262f)]
    [InlineData(400f, 300f, 300f)]
    public void The_drawer_is_exactly_the_tree_column(float tree, float room, float expected)
    {
        // No 300 px least width: a narrow tree's drawer stays over the tree and never reaches the list.
        Assert.Equal(expected, DrawerLayout.Width(tree, treeStrip: false, DrawerLayout.StripFloorLogical, room));
    }

    [Fact]
    public void Over_the_icon_strip_the_drawer_takes_the_narrowest_column()
    {
        Assert.Equal(DrawerLayout.StripFloorLogical, DrawerLayout.Width(56f, treeStrip: true, DrawerLayout.StripFloorLogical, 1000f));
        Assert.Equal(200f, DrawerLayout.Width(56f, treeStrip: true, DrawerLayout.StripFloorLogical, 200f));
    }

    [Theory]
    [InlineData(float.NaN, 500f)]
    [InlineData(-20f, 500f)]
    [InlineData(300f, float.NaN)]
    [InlineData(0f, 0f)]
    public void A_broken_width_never_collapses_below_a_pixel(float tree, float room)
    {
        var width = DrawerLayout.Width(tree, treeStrip: false, DrawerLayout.StripFloorLogical, room);
        Assert.True(width >= 1f);
        Assert.True(float.IsFinite(width));
    }

    [Fact]
    public void The_sheet_ends_where_its_content_ends()
    {
        var h = DrawerLayout.Heights(header: 52f, content: 420f, footer: 48f, cap: 1070f);
        Assert.Equal(520f, h.Sheet);
        Assert.Equal(420f, h.Body);
        Assert.False(h.Scrolls);
    }

    [Fact]
    public void Past_the_body_the_sheet_stops_and_its_body_scrolls()
    {
        var h = DrawerLayout.Heights(header: 52f, content: 1400f, footer: 48f, cap: 800f);
        Assert.Equal(800f, h.Sheet);
        Assert.Equal(700f, h.Body);
        Assert.True(h.Scrolls);
    }

    [Fact]
    public void Content_not_yet_measured_takes_the_whole_body_for_its_unseen_frame()
    {
        var h = DrawerLayout.Heights(header: 52f, content: 0f, footer: 48f, cap: 800f);
        Assert.Equal(800f, h.Sheet);
        Assert.False(h.Scrolls);
    }

    [Fact]
    public void A_fraction_of_a_pixel_never_shows_a_scrollbar()
    {
        var h = DrawerLayout.Heights(header: 50f, content: 700.3f, footer: 50f, cap: 800f);
        Assert.False(h.Scrolls);
    }

    [Fact]
    public void A_body_shorter_than_the_header_and_footer_leaves_no_room_but_never_goes_negative()
    {
        var h = DrawerLayout.Heights(header: 52f, content: 300f, footer: 48f, cap: 60f);
        Assert.Equal(60f, h.Sheet);
        Assert.Equal(0f, h.Body);
        Assert.True(h.Scrolls);
    }

    [Fact]
    public void The_fade_runs_over_its_seconds_and_is_at_once_under_reduce_motion()
    {
        Assert.Equal(0f, DrawerLayout.Fade(10.0, 10.0, 0.16f, reduceMotion: false));
        Assert.Equal(0.5f, DrawerLayout.Fade(10.0, 10.08, 0.16f, reduceMotion: false), 3);
        Assert.Equal(1f, DrawerLayout.Fade(10.0, 11.0, 0.16f, reduceMotion: false));
        Assert.Equal(1f, DrawerLayout.Fade(10.0, 10.0, 0.16f, reduceMotion: true));
    }

    [Fact]
    public void Before_the_drawer_first_draws_the_tree_is_whole()
    {
        var fade = DrawerLayout.Fade(-1.0, 10.0, 0.16f, reduceMotion: true);
        Assert.Equal(0f, fade);
        Assert.Equal(1f, DrawerLayout.TreeAlpha(fade));
        Assert.False(DrawerLayout.TreeHidden(fade));
    }

    [Fact]
    public void Each_level_has_the_specs_header_footer_and_rows()
    {
        var full = DrawerLayout.MetricsFor(Flair.Full);
        var quiet = DrawerLayout.MetricsFor(Flair.Quiet);
        var plain = DrawerLayout.MetricsFor(Flair.Plain);
        Assert.Equal((52f, 48f, 36f, 6f), (full.Header, full.Footer, full.ToggleRow, full.Rounding));
        Assert.Equal((46f, 42f, 34f, 6f), (quiet.Header, quiet.Footer, quiet.ToggleRow, quiet.Rounding));
        Assert.Equal((26f, 24f, 24f, 0f), (plain.Header, plain.Footer, plain.ToggleRow, plain.Rounding));

        // Only Full draws the moon-road divider under its header; the summary lines are 30 px where they are lines.
        Assert.True(full.Divider > 0f);
        Assert.Equal(0f, quiet.Divider);
        Assert.Equal(0f, plain.Divider);
        Assert.Equal(30f, full.SummaryLine);
        Assert.Equal(30f, quiet.SummaryLine);
    }

    [Theory]
    [InlineData(0f, 1f, false)]
    [InlineData(0.25f, 0.75f, false)]
    [InlineData(0.999f, 0.001f, false)]
    [InlineData(1f, 0f, true)]
    public void The_tree_fades_out_as_the_drawer_fades_in_and_is_skipped_once_it_is_opaque(float fade, float alpha, bool hidden)
    {
        Assert.Equal(alpha, DrawerLayout.TreeAlpha(fade), 4);
        Assert.Equal(hidden, DrawerLayout.TreeHidden(fade));
    }
}
