using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The Follow Umbra palette (plan v8 M3, decision 4; <see cref="UmbraPalette"/>): Umbra's colour profile mapped onto
/// Tsukimichi's palette roles, held to the same bar as every palette (PaletteContrastTests: every text pair at 4.5 : 1,
/// every line pair at 3 : 1, in the palette and its high-contrast form), with the text clamped where a profile's own
/// text does not read, and Night when the profile can't be read.
/// </summary>
public sealed class UmbraPaletteTests
{
    private static UmbraColorProfile Builtin(string name)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "umbra-sample.profile.json"))
            .Replace("\"Umbra (built-in)\"", "\"" + name + "\"", StringComparison.Ordinal);
        return UmbraSettings.Parse(json).Colors!;
    }

    public static IEnumerable<object[]> Profiles() => [["Umbra (built-in)"], ["Metal (built-in)"], ["Clear Blue (built-in)"], ["YoRHa Dark (built-in)"], ["YoRHa Light (built-in)"]];

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Umbras_built_in_profiles_pass_every_contrast_pair(string name)
    {
        var palette = UmbraPalette.From(Builtin(name), out _, out var fellBack);
        Assert.False(fellBack);
        AssertPasses(palette);
        AssertPasses(palette.HighContrast);
    }

    [Fact]
    public void The_mapping_takes_Umbras_window_and_text_and_keeps_gold_gold()
    {
        var dark = UmbraPalette.From(Builtin("Umbra (built-in)"), out var clamped, out _);
        Assert.Equal(UmbraPalette.Key, dark.Key);
        Assert.Equal(UmbraPalette.Name, dark.Name);
        Assert.False(dark.IsLight);
        Assert.False(clamped);
        Assert.Equal(0x212021u, ColorMath.ToHex(dark.Surface.Window));
        Assert.Equal(0xDCDCDCu, ColorMath.ToHex(dark.Surface.Text));

        // Gold keeps its meaning of act now: Umbra's bronze accent never becomes Tsukimichi's gold.
        Assert.Equal(GlyphTokens.Moon, dark.Inks.Gold);
        Assert.NotEqual(0xB98E4Cu, ColorMath.ToHex(dark.Accent));

        var light = UmbraPalette.From(Builtin("YoRHa Light (built-in)"), out _, out _);
        Assert.True(light.IsLight);
        Assert.Equal(0xD1CDB7u, ColorMath.ToHex(light.Surface.Window));
    }

    [Fact]
    public void A_profile_whose_text_barely_reads_is_clamped_until_it_passes()
    {
        // A grey-on-grey profile: #6A6A6A text on a #505050 window is 1.6 : 1.
        var murky = new UmbraColorProfile("Murky", new Dictionary<string, uint>
        {
            ["Window.Background"] = UmbraSettings.ToAbgr(0x505050),
            ["Window.Text"] = UmbraSettings.ToAbgr(0x6A6A6A),
            ["Window.TextDisabled"] = UmbraSettings.ToAbgr(0x5A5A5A),
            ["Window.Border"] = UmbraSettings.ToAbgr(0x555555),
            ["Window.BackgroundLight"] = UmbraSettings.ToAbgr(0x525252),
            ["Widget.BackgroundHover"] = UmbraSettings.ToAbgr(0x585858),
        });
        Assert.True(ColorMath.Contrast(ColorMath.FromHex(0x6A6A6A), ColorMath.FromHex(0x505050)) < 2f);

        var palette = UmbraPalette.From(murky, out var clamped, out var fellBack);
        Assert.True(clamped);
        Assert.False(fellBack);
        Assert.Equal(0x505050u, ColorMath.ToHex(palette.Surface.Window));
        AssertPasses(palette);
        AssertPasses(palette.HighContrast);
    }

    [Fact]
    public void A_light_profile_with_pale_text_is_clamped_towards_black()
    {
        var pale = new UmbraColorProfile("Pale", new Dictionary<string, uint>
        {
            ["Window.Background"] = UmbraSettings.ToAbgr(0xEEEEEE),
            ["Window.Text"] = UmbraSettings.ToAbgr(0xBBBBBB),
            ["Widget.Background"] = UmbraSettings.ToAbgr(0xE4E4E4),
            ["Widget.BackgroundHover"] = UmbraSettings.ToAbgr(0xDDDDDD),
        });
        var palette = UmbraPalette.From(pale, out var clamped, out var fellBack);
        Assert.True(clamped);
        Assert.False(fellBack);
        Assert.True(palette.IsLight);
        Assert.True(ColorMath.Luminance(palette.Surface.Text) < ColorMath.Luminance(ColorMath.FromHex(0xBBBBBB)));
        AssertPasses(palette);
    }

    [Theory]
    [InlineData(0x888888u, 0x000000u)]
    [InlineData(0x888888u, 0x9A9A9Au)]
    [InlineData(0x999999u, 0xAAAAAAu)]
    [InlineData(0x777777u, 0x7A7A7Au)]
    public void A_mid_grey_window_reads_with_whichever_of_black_or_white_reads_better(uint window, uint text)
    {
        // A mid-grey window (luminance under 0.5) reads better with black than white: pushing the text towards white could
        // never reach 4.5 : 1, so the palette went to Night.
        var grey = new UmbraColorProfile("Grey", new Dictionary<string, uint>
        {
            ["Window.Background"] = UmbraSettings.ToAbgr(window),
            ["Window.Text"] = UmbraSettings.ToAbgr(text),
        });
        var palette = UmbraPalette.From(grey, out _, out var fellBack);
        Assert.False(fellBack);
        Assert.Equal(UmbraPalette.Key, palette.Key);
        Assert.Equal(window, ColorMath.ToHex(palette.Surface.Window));
        AssertPasses(palette);
        AssertPasses(palette.HighContrast);
    }

    [Fact]
    public void An_unreadable_or_incomplete_profile_falls_back_to_Night()
    {
        Assert.Same(UiPalettes.Night, UmbraPalette.From(null, out var clamped, out var fellBack));
        Assert.True(fellBack);
        Assert.False(clamped);

        var noText = new UmbraColorProfile("Half", new Dictionary<string, uint> { ["Window.Background"] = UmbraSettings.ToAbgr(0x101010) });
        Assert.Same(UiPalettes.Night, UmbraPalette.From(noText, out _, out fellBack));
        Assert.True(fellBack);
    }

    [Fact]
    public void Translucent_Umbra_colours_are_laid_over_the_window()
    {
        // Umbra's disabled text is translucent (alpha .63); the palette's roles are opaque.
        var palette = UmbraPalette.From(Builtin("Umbra (built-in)"), out _, out _);
        Assert.Equal(1f, palette.Surface.TextTertiary.W);
        Assert.Equal(1f, palette.Surface.Text.W);
    }

    private static void AssertPasses(UiPalette palette)
    {
        var failures = palette.TextPairs()
            .Where(static p => ColorMath.Contrast(p.Ink, p.Ground) < ColorMath.AaText)
            .Select(p => $"{palette.Key}: {p.Role} {ColorMath.Contrast(p.Ink, p.Ground):0.00} : 1")
            .Concat(palette.LinePairs()
                .Where(static p => ColorMath.Contrast(p.Ink, p.Ground) < ColorMath.AaNonText)
                .Select(p => $"{palette.Key}: {p.Role} {ColorMath.Contrast(p.Ink, p.Ground):0.00} : 1"))
            .ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(UmbraPalette.Passes(palette));
    }
}
