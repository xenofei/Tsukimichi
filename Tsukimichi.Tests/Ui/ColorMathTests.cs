using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Contrast claims of the Night tokens (ui-revamp §4.3 / §5.1, the hex values in Ui/Theme.cs) and the "Follow Dalamud
/// colours" mapping.
/// </summary>
public class ColorMathTests
{
    private static readonly Vector4 Night = ColorMath.FromHex(0x0F1424);
    private static readonly Vector4 NightRaised = ColorMath.FromHex(0x1E2437);
    private static readonly Vector4 NightHover = ColorMath.FromHex(0x262D45);
    private static readonly Vector4 Silver = ColorMath.FromHex(0xDDE3F0);
    private static readonly Vector4 Mist = ColorMath.FromHex(0xA9B2CC);
    private static readonly Vector4 Dusk = ColorMath.FromHex(0x7C86A8);
    private static readonly Vector4 VeilLine = ColorMath.FromHex(0x5C6584);
    private static readonly Vector4 Snow = ColorMath.FromHex(0xD8DEE6);

    [Fact]
    public void Black_on_white_is_twenty_one_to_one()
    {
        Assert.Equal(21f, ColorMath.Contrast(new Vector4(0, 0, 0, 1), new Vector4(1, 1, 1, 1)), 2);
    }

    [Theory]
    [InlineData(0xDDE3F0, 0x0F1424, 14.2f)] // Silver on Night
    [InlineData(0xA9B2CC, 0x0F1424, 8.7f)] // Mist on Night
    [InlineData(0xA9B2CC, 0x1E2437, 7.3f)] // Mist on NightRaised
    [InlineData(0x7C86A8, 0x0F1424, 5.1f)] // Dusk on Night
    [InlineData(0x5C6584, 0x0F1424, 3.2f)] // VeilLine on Night
    public void Token_contrast_matches_the_proposal_table(uint fg, uint bg, float expected)
    {
        Assert.Equal(expected, ColorMath.Contrast(ColorMath.FromHex(fg), ColorMath.FromHex(bg)), 1);
    }

    [Fact]
    public void Mist_hints_clear_AA_on_every_night_surface()
    {
        Assert.True(ColorMath.Contrast(Mist, Night) >= 4.5f);
        Assert.True(ColorMath.Contrast(Mist, NightRaised) >= 4.5f);
        Assert.True(ColorMath.Contrast(Mist, NightHover) >= 4.5f);
    }

    [Fact]
    public void Overlay_hint_reads_against_its_own_night_outline_even_over_snow()
    {
        // The overlay at its 0.6 floor over Coerthas snow: the composited panel is too light for Mist alone (A8), so
        // the text carries a 1 px Night outline, and the outline is what the glyphs are read against.
        var panel = ColorMath.Over(Night with { W = 0.6f }, Snow);
        Assert.True(ColorMath.Contrast(Mist, panel) < 4.5f);
        Assert.True(ColorMath.Contrast(Mist, Night) >= 4.5f);
        Assert.True(ColorMath.Contrast(Silver, Night) >= 7f);
    }

    [Fact]
    public void Over_composites_with_the_top_alpha()
    {
        var c = ColorMath.Over(new Vector4(1f, 0f, 0f, 0.25f), new Vector4(0f, 0f, 1f, 1f));

        Assert.Equal(new Vector4(0.25f, 0f, 0.75f, 1f), c);
    }

    [Fact]
    public void Ensure_contrast_keeps_a_colour_that_already_passes()
    {
        Assert.Equal(Mist, ColorMath.EnsureContrast(Mist, Silver, Night, 4.5f));
    }

    [Fact]
    public void Ensure_contrast_nudges_a_failing_colour_until_it_passes()
    {
        var nudged = ColorMath.EnsureContrast(VeilLine, Silver, Night, 4.5f);

        Assert.True(ColorMath.Contrast(nudged, Night) >= 4.5f);
        Assert.NotEqual(Silver, nudged);
    }

    [Fact]
    public void Night_like_host_maps_to_readable_roles()
    {
        var s = SurfaceColors.FromHost(Night, NightRaised, NightHover, ColorMath.FromHex(0x2A3149), Silver, Dusk);

        Assert.False(s.Light);
        Assert.Equal(1f, s.Window.W);
        Assert.True(ColorMath.Contrast(s.TextSecondary, s.Window) >= SurfaceColors.TextMinContrast);
        Assert.True(ColorMath.Contrast(s.StrongLine, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(ColorMath.Luminance(s.Sunken) < ColorMath.Luminance(s.Window));
    }

    [Fact]
    public void Dalamud_default_style_with_translucent_frames_gets_opaque_roles()
    {
        // Dalamud's default: a dark translucent window, grey frames at about half alpha, white text.
        var s = SurfaceColors.FromHost(
            new Vector4(0.06f, 0.06f, 0.06f, 0.87f),
            new Vector4(0.29f, 0.29f, 0.29f, 0.54f),
            new Vector4(0.54f, 0.54f, 0.54f, 0.40f),
            new Vector4(0.43f, 0.43f, 0.50f, 0.50f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(0.50f, 0.50f, 0.50f, 1f));

        Assert.Equal(1f, s.Raised.W);
        Assert.Equal(1f, s.Hover.W);
        Assert.Equal(1f, s.Line.W);
        Assert.True(ColorMath.Contrast(s.Raised, s.Window) > 1.08f);
        Assert.True(ColorMath.Contrast(s.TextSecondary, s.Window) >= SurfaceColors.TextMinContrast);
    }

    [Fact]
    public void Light_host_theme_is_detected_and_its_hints_still_pass_AA()
    {
        var s = SurfaceColors.FromHost(
            ColorMath.FromHex(0xE8E8E8),
            ColorMath.FromHex(0xE8E8E8), // frames invisible on the window
            ColorMath.FromHex(0xD0D0D0),
            ColorMath.FromHex(0xB0B0B0),
            ColorMath.FromHex(0x202020),
            ColorMath.FromHex(0xB8B8B8)); // a disabled text that fails on the window

        Assert.True(s.Light);
        Assert.True(ColorMath.Contrast(s.TextSecondary, s.Window) >= SurfaceColors.TextMinContrast);
        Assert.True(ColorMath.Contrast(s.StrongLine, s.Window) >= SurfaceColors.LineMinContrast);
        Assert.True(ColorMath.Contrast(s.Raised, s.Window) > 1.05f);
    }
}
