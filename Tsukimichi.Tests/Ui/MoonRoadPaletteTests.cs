using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Moon Road palette (moon-road proposal §3, §10): the six new tokens hold the contrast the proposal's §10.1 table
/// claims (WCAG 2.x ratios, to one decimal), the Night surface carries them, the "Follow Dalamud colours" mapping
/// derives readable equivalents on dark and light host styles, and the high-contrast palette has its own versions.
/// </summary>
public class MoonRoadPaletteTests
{
    private static float Ratio(Vector4 a, Vector4 b) => ColorMath.Contrast(a, b);

    [Fact]
    public void The_six_tokens_have_the_proposal_hex_values()
    {
        Assert.Equal(0x080B16u, GlyphTokens.AbyssHex);
        Assert.Equal(0x151C33u, GlyphTokens.NightTopHex);
        Assert.Equal(0xA88B52u, GlyphTokens.GiltHex);
        Assert.Equal(0xD9BE82u, GlyphTokens.GiltHighHex);
        Assert.Equal(0x6F8FD0u, GlyphTokens.TideHex);
        Assert.Equal(0x24345Cu, GlyphTokens.TideDeepHex);
    }

    // §10.1 and §3 ("Contrast on Night / Raised / Abyss"), to one decimal.
    [Theory]
    [InlineData(GlyphTokens.SilverHex, GlyphTokens.NightHex, 14.2f)]
    [InlineData(GlyphTokens.SilverHex, GlyphTokens.NightTopHex, 13.1f)]
    [InlineData(GlyphTokens.SilverHex, GlyphTokens.AbyssHex, 15.3f)]
    [InlineData(GlyphTokens.MistHex, GlyphTokens.NightHex, 8.7f)]
    [InlineData(GlyphTokens.MistHex, GlyphTokens.NightTopHex, 8.0f)]
    [InlineData(GlyphTokens.DuskHex, GlyphTokens.NightHex, 5.1f)]
    [InlineData(GlyphTokens.DuskHex, GlyphTokens.NightTopHex, 4.7f)]
    [InlineData(GlyphTokens.MoonHex, GlyphTokens.AbyssHex, 13.3f)]
    [InlineData(GlyphTokens.GiltHex, GlyphTokens.NightHex, 5.7f)]
    [InlineData(GlyphTokens.GiltHex, GlyphTokens.AbyssHex, 6.1f)]
    [InlineData(GlyphTokens.GiltHex, GlyphTokens.MoonHex, 2.2f)]
    [InlineData(GlyphTokens.GiltHighHex, GlyphTokens.NightHex, 10.2f)]
    [InlineData(GlyphTokens.GiltHighHex, GlyphTokens.AbyssHex, 10.9f)]
    [InlineData(GlyphTokens.TideHex, GlyphTokens.NightHex, 5.7f)]
    [InlineData(GlyphTokens.TideHex, GlyphTokens.AbyssHex, 6.1f)]
    [InlineData(GlyphTokens.TideDeepHex, GlyphTokens.NightHex, 1.5f)]
    public void Token_contrast_matches_the_proposal_table(uint fg, uint bg, float expected)
    {
        Assert.Equal(expected, Ratio(ColorMath.FromHex(fg), ColorMath.FromHex(bg)), 1);
    }

    [Fact]
    public void Pairs_on_the_raised_surface_match_the_proposal()
    {
        // NightRaised is Night a quarter of the way to Veil as Theme draws it, a fraction off the rounded #1E2437 the
        // proposal measured, so the ratios hold to within 0.1.
        Assert.InRange(Ratio(GlyphTokens.Mist, GlyphTokens.NightRaised), 7.2f, 7.4f);
        Assert.InRange(Ratio(GlyphTokens.Gilt, GlyphTokens.NightRaised), 4.7f, 4.9f);
        Assert.InRange(Ratio(GlyphTokens.GiltHigh, GlyphTokens.NightRaised), 8.4f, 8.6f);
        Assert.InRange(Ratio(GlyphTokens.Tide, GlyphTokens.NightRaised), 4.7f, 4.9f);
    }

    [Fact]
    public void Text_tokens_meet_their_verdicts_on_every_background_they_are_drawn_on()
    {
        // AAA: Silver on Night, NightTop and Abyss; Mist on Night, NightTop and Raised; Moon on Abyss.
        foreach (var bg in new[] { GlyphTokens.Night, GlyphTokens.NightTop, GlyphTokens.Abyss })
        {
            Assert.True(Ratio(GlyphTokens.Silver, bg) >= 7f);
        }

        foreach (var bg in new[] { GlyphTokens.Night, GlyphTokens.NightTop, GlyphTokens.NightRaised })
        {
            Assert.True(Ratio(GlyphTokens.Mist, bg) >= 7f);
        }

        Assert.True(Ratio(GlyphTokens.Moon, GlyphTokens.Abyss) >= 7f);

        // AA: Dusk captions on Night and NightTop only; Tide text on Night and Raised.
        Assert.True(Ratio(GlyphTokens.Dusk, GlyphTokens.Night) >= 4.5f);
        Assert.True(Ratio(GlyphTokens.Dusk, GlyphTokens.NightTop) >= 4.5f);
        Assert.True(Ratio(GlyphTokens.Tide, GlyphTokens.Night) >= 4.5f);
        Assert.True(Ratio(GlyphTokens.Tide, GlyphTokens.NightRaised) >= 4.5f);

        // The bead at the orbit's tip.
        Assert.True(Ratio(GlyphTokens.MoonHigh, GlyphTokens.Night) >= 7f);
    }

    [Fact]
    public void Gilt_ornament_reads_as_a_graphic_but_not_as_the_gold_signal()
    {
        // WCAG 1.4.11: at least 3 : 1 for graphics, even at the top of its usual alpha range over Night (≈ 4.0).
        var gilt08 = ColorMath.Over(GlyphTokens.Gilt with { W = 0.8f }, GlyphTokens.Night);
        Assert.True(Ratio(gilt08, GlyphTokens.Night) >= 3f);

        // Brass is darker than Moon by a luminance step that survives colour blindness, and near Dusk in greyscale.
        Assert.True(Ratio(GlyphTokens.Gilt, GlyphTokens.Moon) >= 2f);
        Assert.InRange(ColorMath.Luminance(GlyphTokens.Gilt) - ColorMath.Luminance(GlyphTokens.Dusk), -0.05f, 0.05f);
    }

    [Fact]
    public void Surfaces_step_from_abyss_to_night_top()
    {
        var abyss = ColorMath.Luminance(GlyphTokens.Abyss);
        var night = ColorMath.Luminance(GlyphTokens.Night);
        var top = ColorMath.Luminance(GlyphTokens.NightTop);
        var raised = ColorMath.Luminance(GlyphTokens.NightRaised);

        Assert.True(abyss < night);
        Assert.True(night < top);
        Assert.True(top < raised);
    }

    [Fact]
    public void Night_surface_carries_the_six_tokens()
    {
        var s = SurfaceColors.Night;

        Assert.Equal(GlyphTokens.Abyss, s.Deep);
        Assert.Equal(GlyphTokens.NightTop, s.Top);
        Assert.Equal(GlyphTokens.Gilt, s.Ornament);
        Assert.Equal(GlyphTokens.GiltHigh, s.OrnamentHigh);
        Assert.Equal(GlyphTokens.Tide, s.Cool);
        Assert.Equal(GlyphTokens.TideDeep, s.CoolDeep);
        Assert.Equal(GlyphTokens.Night, s.Window);
        Assert.Equal(GlyphTokens.Mist, s.TextSecondary);
    }

    [Fact]
    public void A_night_like_host_keeps_the_brass_and_tide_colours()
    {
        var s = SurfaceColors.FromHost(GlyphTokens.Night, GlyphTokens.NightRaised, GlyphTokens.NightHover, GlyphTokens.NightLine, GlyphTokens.Silver, GlyphTokens.Dusk);

        Assert.Equal(GlyphTokens.Gilt, s.Ornament);
        Assert.Equal(GlyphTokens.GiltHigh, s.OrnamentHigh);
        Assert.Equal(GlyphTokens.Tide, s.Cool);
        Assert.True(ColorMath.Luminance(s.Deep) < ColorMath.Luminance(s.Window));
        Assert.True(ColorMath.Luminance(s.Top) > ColorMath.Luminance(s.Window));
        Assert.True(Ratio(s.Top, s.Window) < 1.25f, "the gradient is a hint of sky, not a band");
    }

    [Fact]
    public void Dalamud_default_style_gets_readable_ornament_and_tide()
    {
        var s = SurfaceColors.FromHost(
            new Vector4(0.06f, 0.06f, 0.06f, 0.87f),
            new Vector4(0.29f, 0.29f, 0.29f, 0.54f),
            new Vector4(0.54f, 0.54f, 0.54f, 0.40f),
            new Vector4(0.43f, 0.43f, 0.50f, 0.50f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(0.50f, 0.50f, 0.50f, 1f));

        Assert.True(Ratio(s.Ornament, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.OrnamentHigh, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.Cool, s.Window) >= SurfaceColors.TextMinContrast);
        Assert.Equal(1f, s.Deep.W);
        Assert.Equal(1f, s.Top.W);
    }

    [Theory]
    [InlineData(0xFFFFFFu, 0x101010u)]
    [InlineData(0xE8E8E8u, 0x202020u)]
    [InlineData(0xF4EEDCu, 0x3A2E1Eu)] // a warm parchment theme
    public void A_light_host_derives_sensible_equivalents(uint window, uint text)
    {
        var s = SurfaceColors.FromHost(
            ColorMath.FromHex(window),
            ColorMath.FromHex(window),
            ColorMath.FromHex(0xD0D0D0),
            ColorMath.FromHex(0xB0B0B0),
            ColorMath.FromHex(text),
            ColorMath.FromHex(0x808080));

        Assert.True(s.Light);

        // Inks: brass lines and points reach 3 : 1, the cool accent reads as text.
        Assert.True(Ratio(s.Ornament, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.OrnamentHigh, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.Cool, s.Window) >= SurfaceColors.TextMinContrast);

        // Surfaces: the deep surface is a light step under the window (not a dark slab), the gradient's top no darker.
        Assert.True(ColorMath.Luminance(s.Deep) < ColorMath.Luminance(s.Window));
        Assert.True(ColorMath.Luminance(s.Deep) > 0.5f);
        Assert.True(ColorMath.Luminance(s.Top) >= ColorMath.Luminance(s.Window));

        // Text still reads on the deep surface.
        Assert.True(Ratio(s.Text, s.Deep) >= SurfaceColors.TextMinContrast);
    }

    [Fact]
    public void High_contrast_has_its_own_versions()
    {
        var s = SurfaceColors.Night.ForHighContrast();

        // No gradient, opaque VeilLine lines (3.2 : 1), points and the cool accent pushed up.
        Assert.Equal(s.Window, s.Top);
        Assert.Equal(GlyphTokens.VeilLine, s.Ornament);
        Assert.True(Ratio(s.Ornament, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.OrnamentHigh, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.Cool, s.Window) >= SurfaceColors.HighContrastTextMinContrast);

        // The chrome roles are untouched.
        Assert.Equal(SurfaceColors.Night.Text, s.Text);
        Assert.Equal(SurfaceColors.Night.Raised, s.Raised);
        Assert.Equal(SurfaceColors.Night.Deep, s.Deep);
    }

    [Fact]
    public void High_contrast_on_a_light_host_still_reads()
    {
        var host = SurfaceColors.FromHost(
            ColorMath.FromHex(0xF6F7FB),
            ColorMath.FromHex(0xE0E0E0),
            ColorMath.FromHex(0xD0D0D0),
            ColorMath.FromHex(0xB0B0B0),
            ColorMath.FromHex(0x1C2338),
            ColorMath.FromHex(0x747D9C));
        var s = host.ForHighContrast();

        Assert.Equal(s.Window, s.Top);
        Assert.True(Ratio(s.Ornament, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(Ratio(s.Cool, s.Window) >= SurfaceColors.HighContrastTextMinContrast);
    }
}
