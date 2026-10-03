using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// A kit's sprite ornaments (plan v7 T15; ATLAS-CONTRACT §8): the sigil's size rule (the Sumi to Kinpaku concept, Round 2
/// tidy-up 3), the corner mark's box, and the strip's layout checks.
/// </summary>
public sealed class KitOrnamentsTests
{
    [Theory]
    [InlineData(32f, 1f, KitOrnament.Sigil, 32)]
    [InlineData(13f, 1f, KitOrnament.Sigil, 13)]
    [InlineData(12.6f, 1f, KitOrnament.Sigil, 13)]
    [InlineData(12.4f, 1f, KitOrnament.SigilSmall, 12)]
    [InlineData(10f, 1f, KitOrnament.SigilSmall, 10)]
    [InlineData(9.4f, 1f, KitOrnament.Lozenge, 2)]
    [InlineData(8f, 1f, KitOrnament.Lozenge, 2)]
    [InlineData(8f, 1.5f, KitOrnament.Lozenge, 3)]
    [InlineData(9f, 2f, KitOrnament.Lozenge, 4)]
    [InlineData(0f, float.NaN, KitOrnament.Lozenge, 2)]
    [InlineData(float.NaN, 1f, KitOrnament.Lozenge, 2)]
    public void The_sigil_is_the_crest_from_13_px_the_small_crest_at_10_to_12_and_never_a_crest_below(float size, float scale, KitOrnament sprite, int cell)
    {
        Assert.Equal((sprite, cell), KitOrnaments.Sigil(size, scale));
    }

    [Fact]
    public void A_corner_marks_L_sits_on_the_frames_corner_mirrored_into_each_corner()
    {
        // An 8 px card corner still draws 15 px across, so the leaf bar (2.2 of 32 units) is a whole device pixel.
        Assert.Equal((-1f, -1f, 15), KitOrnaments.CornerBox(0f, 0f, right: false, bottom: false, armPx: 8f));
        Assert.Equal((86f, 46f, 15), KitOrnaments.CornerBox(100f, 60f, right: true, bottom: true, armPx: 8f));

        // Larger arms grow the box with them: 26 of its 32 units are the L.
        var (x, y, side) = KitOrnaments.CornerBox(10f, 20f, right: true, bottom: false, armPx: 26f);
        Assert.Equal(32, side);
        Assert.Equal((-19f, 17f), (x, y));
        Assert.Equal(KitOrnaments.CornerMinPx, KitOrnaments.CornerBox(0f, 0f, false, false, float.NaN).Side);
    }

    [Fact]
    public void The_strip_parses_clamps_to_its_range_and_rejects_a_broken_layout()
    {
        const string json = """
            {"size": [64, 40], "sprites": {
              "sigil": {"13": [1, 1, 13, 13], "14": [16, 1, 14, 14]},
              "sigil-small": {"10": [32, 1, 10, 10]},
              "lozenge": {"2": [44, 1, 2, 2]},
              "corner": {"15": [1, 20, 15, 15]}}}
            """;
        Assert.True(KitOrnamentLayout.TryParse(json, out var layout, out var error), error);
        Assert.Equal((13, 14), layout!.Range(KitOrnament.Sigil));
        Assert.True(layout.TryRect(KitOrnament.Sigil, 40, out var big));
        Assert.Equal(new AtlasRect(16, 1, 14, 14), big);
        Assert.True(layout.TryRect(KitOrnament.Sigil, 2, out var small));
        Assert.Equal(new AtlasRect(1, 1, 13, 13), small);
        Assert.False(layout.TryRect((KitOrnament)9, 13, out _));
        Assert.Equal(64L * 40 * 4, layout.Bytes);

        Assert.False(KitOrnamentLayout.TryParse(json.Replace("\"corner\"", "\"crest\"", StringComparison.Ordinal), out _, out error));
        Assert.Contains("corner", error, StringComparison.Ordinal);
        Assert.False(KitOrnamentLayout.TryParse(json.Replace("\"14\": [16, 1, 14, 14]", "\"15\": [16, 1, 15, 15]", StringComparison.Ordinal), out _, out error));
        Assert.Contains("contiguous", error, StringComparison.Ordinal);
        Assert.False(KitOrnamentLayout.TryParse(json.Replace("[16, 1, 14, 14]", "[14, 1, 14, 14]", StringComparison.Ordinal), out _, out _));
        Assert.False(KitOrnamentLayout.TryParse(json.Replace("[44, 1, 2, 2]", "[63, 1, 2, 2]", StringComparison.Ordinal), out _, out _));
        Assert.False(KitOrnamentLayout.TryParse("{", out _, out _));
    }
}
