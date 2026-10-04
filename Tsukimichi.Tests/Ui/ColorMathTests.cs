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
    public void Ensure_contrast_against_several_grounds_reads_on_every_one()
    {
        // The Mix list's amber note (#C9A866) sits on an option at rest, hovered, selected and pressed: on every palette it
        // must read at 4.5 : 1 on all four, not only on the popup's window.
        var amber = ColorMath.FromHex(0xC9A866);
        foreach (var palette in Tsukimichi.Core.Ui.Themes.UiPalettes.All)
        {
            var s = palette.Surface;
            var window = s.Window with { W = 1f };
            Vector4[] grounds =
            [
                window,
                ColorMath.Over(s.Hover, window),
                ColorMath.Over(s.Text with { W = 0.10f }, window),
                ColorMath.Over(s.Text with { W = 0.16f }, window),
            ];
            var note = ColorMath.EnsureContrast(amber, s.Text, grounds, SurfaceColors.TextMinContrast);
            foreach (var ground in grounds)
            {
                Assert.True(ColorMath.Contrast(note, ground) >= SurfaceColors.TextMinContrast, $"{palette.Id}: {ColorMath.Contrast(note, ground):0.00}");
            }

            Assert.Equal(amber.W, note.W);
        }

        // A colour that already reads everywhere is kept; one ground that cannot be met leaves the target itself.
        Assert.Equal(Mist, ColorMath.EnsureContrast(Mist, Silver, [Night, NightRaised, NightHover], 4.5f));
        Assert.Equal(Silver, ColorMath.EnsureContrast(VeilLine, Silver, [Night, Silver], 4.5f));
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

    [Fact]
    public void Wcag_ratio_matches_the_reference_values()
    {
        var black = new Vector4(0f, 0f, 0f, 1f);
        Assert.Equal(21f, ColorMath.Contrast(Vector4.One, black), 2);
        Assert.Equal(1f, ColorMath.Contrast(Night, Night), 3);
        Assert.Equal(ColorMath.Contrast(Silver, Night), ColorMath.Contrast(Night, Silver), 4);

        // The WebAIM reference: #777777 on white is 4.48 : 1, just under AA; #767676 is 4.54 : 1, just over.
        Assert.Equal(4.48f, ColorMath.Contrast(ColorMath.FromHex(0x777777), Vector4.One), 2);
        Assert.False(ColorMath.ReadsAsText(ColorMath.FromHex(0x777777), Vector4.One));
        Assert.True(ColorMath.ReadsAsText(ColorMath.FromHex(0x767676), Vector4.One));
    }

    [Fact]
    public void Contrast_on_a_translucent_ground_composites_it_first()
    {
        // A clear ground is the backdrop itself; an opaque one hides it.
        Assert.Equal(ColorMath.Contrast(Silver, Night), ColorMath.ContrastOn(Silver, Night with { W = 0f }, Night), 3);
        Assert.Equal(ColorMath.Contrast(Silver, NightRaised), ColorMath.ContrastOn(Silver, NightRaised, Vector4.One), 3);

        // Half white over Night lightens the ground, so silver text on it reads worse than on Night.
        Assert.True(ColorMath.ContrastOn(Silver, Vector4.One with { W = 0.5f }, Night) < ColorMath.Contrast(Silver, Night));
    }

    [Fact]
    public void ToHex_round_trips_FromHex()
    {
        foreach (var hex in new uint[] { 0x000000, 0xFFFFFF, 0x0F1424, 0xF2D27A, 0x8B94B3 })
        {
            Assert.Equal(hex, ColorMath.ToHex(ColorMath.FromHex(hex)));
        }
    }
}
