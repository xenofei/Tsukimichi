using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class PaneArtTests
{
    [Fact]
    public void Bead_segments_tile_the_circle_from_twelve_o_clock_with_gaps()
    {
        const int Count = 5;
        var share = 2f * MathF.PI / Count;
        var sweep = BeadRingMath.SegmentSweep(Count);
        Assert.Equal(share - BeadRingMath.GapRadians, sweep, 5);

        // Symmetric about 12 o'clock: the first segment starts half a gap after it, the last ends half a gap before.
        var first = BeadRingMath.SegmentStart(0, Count);
        Assert.Equal(GaugeGeometry.StartAngle + (BeadRingMath.GapRadians * 0.5f), first, 5);
        var lastEnd = BeadRingMath.SegmentStart(Count - 1, Count) + sweep;
        Assert.Equal(GaugeGeometry.StartAngle + (2f * MathF.PI) - (BeadRingMath.GapRadians * 0.5f), lastEnd, 4);

        for (var i = 1; i < Count; i++)
        {
            var gap = BeadRingMath.SegmentStart(i, Count) - (BeadRingMath.SegmentStart(i - 1, Count) + sweep);
            Assert.Equal(BeadRingMath.GapRadians, gap, 4);
        }
    }

    [Fact]
    public void One_segment_is_the_whole_ring_and_many_are_an_arc()
    {
        Assert.Equal(2f * MathF.PI, BeadRingMath.SegmentSweep(1), 5);
        Assert.Equal(GaugeGeometry.StartAngle, BeadRingMath.SegmentStart(0, 1));
        Assert.False(BeadRingMath.Segmented(0));
        Assert.False(BeadRingMath.Segmented(1));
        Assert.True(BeadRingMath.Segmented(5));
        Assert.True(BeadRingMath.Segmented(BeadRingMath.MaxSegments));
        Assert.False(BeadRingMath.Segmented(BeadRingMath.MaxSegments + 1));

        // Even at the most segments a bead keeps a visible length.
        Assert.True(BeadRingMath.SegmentSweep(BeadRingMath.MaxSegments) > 0f);
    }

    [Fact]
    public void New_beads_light_one_after_another()
    {
        // No sequence playing, or a count that fell: the target at once.
        Assert.Equal((4, 1f), BeadRingMath.Shown(1, 4, -1f));
        Assert.Equal((2, 1f), BeadRingMath.Shown(4, 2, 0.5f));
        Assert.Equal((3, 1f), BeadRingMath.Shown(3, 3, 0.2f));

        // From 1 to 4: three steps over four quarters; each quarter lights the next bead, fading in.
        var (lit, head) = BeadRingMath.Shown(1, 4, 0f);
        Assert.Equal(2, lit);
        Assert.Equal(0f, head, 5);
        (lit, head) = BeadRingMath.Shown(1, 4, 0.375f);
        Assert.Equal(3, lit);
        Assert.Equal(0.5f, head, 4);
        Assert.Equal((4, 1f), BeadRingMath.Shown(1, 4, 0.75f));
        Assert.Equal((4, 1f), BeadRingMath.Shown(1, 4, 1f));

        var previous = 0;
        for (var p = 0f; p <= 1f; p += 0.01f)
        {
            var (shown, _) = BeadRingMath.Shown(0, 5, p);
            Assert.InRange(shown, previous, 5);
            previous = shown;
        }

        Assert.Equal(0f, BeadRingMath.SequenceSeconds(3, 3));
        Assert.Equal(4 * BeadRingMath.StepSeconds, BeadRingMath.SequenceSeconds(1, 4), 5);
    }

    [Theory]
    [InlineData(100f, 2)]
    [InlineData(191f, 2)]
    [InlineData(288f, 3)]
    [InlineData(480f, 5)]
    [InlineData(0f, 2)]
    [InlineData(float.NaN, 2)]
    public void Gallery_columns_are_one_per_96_px_and_at_least_two(float width, int expected)
    {
        Assert.Equal(expected, PaneGrid.GalleryColumns(width));
    }

    [Theory]
    [InlineData(100f, 1)]
    [InlineData(360f, 3)]
    [InlineData(600f, 5)]
    [InlineData(-5f, 1)]
    public void Section_columns_are_one_per_120_px_and_at_least_one(float width, int expected)
    {
        Assert.Equal(expected, PaneGrid.SectionColumns(width));
    }

    [Fact]
    public void Rows_and_banner_height()
    {
        Assert.Equal(0, PaneGrid.Rows(0, 3));
        Assert.Equal(1, PaneGrid.Rows(3, 3));
        Assert.Equal(2, PaneGrid.Rows(4, 3));
        Assert.Equal(0, PaneGrid.Rows(4, 0));

        Assert.Equal(70f, PaneGrid.BannerHeight(200f), 3);
        Assert.Equal(PaneGrid.BannerMaxLogical, PaneGrid.BannerHeight(900f));
        Assert.Equal(0f, PaneGrid.BannerHeight(float.NaN));
        Assert.Equal(0f, PaneGrid.BannerHeight(-1f));
    }

    [Fact]
    public void Every_moonlit_kind_has_a_menu_icon_or_a_glyph()
    {
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            var row = MoonlitKindIcons.MainCommandRow(kind);
            var glyph = MoonlitKindIcons.Glyph(kind);
            Assert.True(row != 0 || glyph != OrnamentGlyph.None, kind.ToString());
            Assert.NotEqual(OrnamentGlyph.None, glyph);
        }

        Assert.Equal(MoonlitKindIcons.MainCommandMountGuide, MoonlitKindIcons.MainCommandRow(RewardKind.Mount));
        Assert.Equal(MoonlitKindIcons.MainCommandMinionGuide, MoonlitKindIcons.MainCommandRow(RewardKind.Minion));
        Assert.Equal(MoonlitKindIcons.MainCommandEmotes, MoonlitKindIcons.MainCommandRow(RewardKind.Emote));
        Assert.Equal(MoonlitKindIcons.MainCommandOrchestrionList, MoonlitKindIcons.MainCommandRow(RewardKind.Orchestrion));
        Assert.Equal(MoonlitKindIcons.MainCommandFashionAccessories, MoonlitKindIcons.MainCommandRow(RewardKind.Ornament));
        Assert.Equal(0u, MoonlitKindIcons.MainCommandRow(RewardKind.SystemUnlock));
        Assert.Equal(OrnamentGlyph.Moonlit, MoonlitKindIcons.Glyph(null));
        Assert.Equal(OrnamentGlyph.PlanFallback, MoonlitKindIcons.Glyph(RewardKind.SystemUnlock));
    }
}
