using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// WCAG 2.x contrast for every palette and form (theme-system §8.3, spec-1.16 §A8): every text-on-surface pair the
/// palette promises (<see cref="UiPalette.TextPairs"/>) reads at 4.5 : 1, and every line, stripe, ornament point and
/// ornament heading (<see cref="UiPalette.LinePairs"/>) at 3 : 1. One row per registered palette and its high-contrast
/// form; the Follow Dalamud hook is held to the same bar on a dark and a light host.
/// </summary>
public sealed class PaletteContrastTests
{
    public static IEnumerable<object[]> Palettes()
    {
        foreach (var palette in UiPalettes.All)
        {
            yield return [palette.Key, false];
            yield return [palette.Key, true];
        }
    }

    private static UiPalette Find(string key, bool highContrast)
    {
        var palette = UiPalettes.All.Single(p => p.Key == key);
        return highContrast ? palette.HighContrast : palette;
    }

    private static void AssertPairs(UiPalette palette, IEnumerable<(string Role, System.Numerics.Vector4 Ink, System.Numerics.Vector4 Ground)> pairs, float min)
    {
        var failures = pairs
            .Select(p => (p.Role, Ratio: ColorMath.Contrast(p.Ink, p.Ground), p.Ink, p.Ground))
            .Where(p => p.Ratio < min)
            .Select(p => $"{palette.Key}: {p.Role} #{ColorMath.ToHex(p.Ink):X6} on #{ColorMath.ToHex(p.Ground):X6} is {p.Ratio:0.00} : 1, under {min} : 1")
            .ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Every_text_pair_reads_at_AA(string key, bool highContrast)
    {
        var palette = Find(key, highContrast);
        AssertPairs(palette, palette.TextPairs(), ColorMath.AaText);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Every_line_stripe_and_ornament_pair_reads_at_three_to_one(string key, bool highContrast)
    {
        var palette = Find(key, highContrast);
        AssertPairs(palette, palette.LinePairs(), ColorMath.AaNonText);
    }

    [Fact]
    public void Night_tertiary_text_and_strong_line_clear_the_bar_on_every_surface()
    {
        // The two spec-1.16 §A2 fixes, to the spec's figures: tertiary 4.5 on Hover, strong line 3.0 on Raised.
        var s = UiPalettes.Night.Surface;
        Assert.InRange(ColorMath.Contrast(s.TextTertiary, s.Hover), 4.5f, 4.7f);
        Assert.InRange(ColorMath.Contrast(s.StrongLine, s.Raised), 3.0f, 3.2f);
        Assert.Equal(GlyphTokens.Dusk, UiPalettes.Night.States.Tone(Core.Model.QuestState.Blocked));
    }

    [Fact]
    public void Every_palette_and_form_has_a_row()
    {
        // spec-1.16 §A8: one row per palette and form, four in 1.16.
        Assert.Equal(["night", "night", "ishgard-snow", "ishgard-snow"], Palettes().Select(static r => (string)r[0]));
    }

    [Fact]
    public void Snow_meets_the_spec_figures()
    {
        // spec-1.16 §A8, Snow column, worst surface: tertiary 4.8 on Hover, Locked out 5.7 on Hover, the strong line 3.3,
        // the gold stripe 3.0 on Hover, and the gauge arc's highlight end (the supervisor's #8A6A1C) 3.3 on the groove.
        var p = UiPalettes.IshgardSnow;
        var s = p.Surface;
        Assert.InRange(ColorMath.Contrast(s.TextTertiary, s.Hover), 4.75f, 4.9f);
        Assert.InRange(ColorMath.Contrast(p.States.Text(Core.Model.QuestState.Foreclosed), s.Hover), 5.6f, 5.8f);
        Assert.InRange(ColorMath.Contrast(s.StrongLine, s.Window), 3.25f, 3.35f);
        Assert.InRange(ColorMath.Contrast(p.States.Stripe(Core.Model.QuestState.Ready), s.Hover), 3.0f, 3.1f);
        Assert.InRange(ColorMath.Contrast(p.Gauges.OuterSlope[0].Color, p.Gauges.Groove), 3.3f, 3.4f);
        Assert.InRange(ColorMath.Contrast(p.Gauges.OuterSlope[0].Color, p.Scene.Zenith), 3.7f, 3.8f);
        Assert.Contains(p.LinePairs(), static pair => pair.Role.StartsWith("Gauge arc #8A6A1C on Groove", StringComparison.Ordinal));
    }

    [Fact]
    public void The_pair_lists_cover_every_surface_and_state()
    {
        var roles = UiPalettes.Night.TextPairs().Select(p => p.Role).ToList();
        foreach (var surface in new[] { "Window", "Raised", "Sunken", "Hover" })
        {
            Assert.Contains($"TextTertiary on {surface}", roles);
        }

        Assert.Equal(StateInks.Order.Length * 2, roles.Count(r => r.Contains(" word on ", StringComparison.Ordinal)));
        Assert.Contains("StrongLine on Raised", UiPalettes.Night.LinePairs().Select(p => p.Role));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Follow_Dalamud_keeps_its_text_readable_on_a_dark_and_a_light_host(bool light)
    {
        var p = UiPalettes.FollowDalamud(
            ColorMath.FromHex(light ? 0xF0F0F0u : 0x202020u),
            ColorMath.FromHex(light ? 0xE0E0E0u : 0x2A2A2Au),
            ColorMath.FromHex(light ? 0xD0D0D0u : 0x3A3A3Au),
            ColorMath.FromHex(light ? 0xB0B0B0u : 0x444444u),
            ColorMath.FromHex(light ? 0x101010u : 0xF0F0F0u),
            ColorMath.FromHex(0x808080));
        var s = p.Surface;
        (string, System.Numerics.Vector4, System.Numerics.Vector4)[] window =
        [
            ("Text", s.Text, s.Window),
            ("TextSecondary", s.TextSecondary, s.Window),
            ("Accent", p.Accent, s.Window),
            ("AccentDim", p.AccentDim, s.Window),
            ("Cool", s.Cool, s.Window),
            ("DangerText", p.Inks.DangerText, s.Window),
            ("UnknownText", p.Inks.UnknownText, s.Window),
        ];
        AssertPairs(p, window, ColorMath.AaText);
    }
}
