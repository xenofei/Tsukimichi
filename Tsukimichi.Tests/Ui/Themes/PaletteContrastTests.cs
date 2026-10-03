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
    /// <summary>The Follow Dalamud hook on a plain dark host style (<see cref="Host"/>).</summary>
    private const string DalamudDark = "dalamud-dark-host";

    /// <summary>The Follow Dalamud hook on a plain light host style (<see cref="Host"/>).</summary>
    private const string DalamudLight = "dalamud-light-host";

    public static IEnumerable<object[]> Palettes()
    {
        foreach (var key in UiPalettes.All.Select(static p => p.Key).Append(DalamudDark).Append(DalamudLight))
        {
            yield return [key, false];
            yield return [key, true];
        }
    }

    private static UiPalette Find(string key, bool highContrast)
    {
        var palette = key switch
        {
            DalamudDark => Host(light: false),
            DalamudLight => Host(light: true),
            _ => UiPalettes.All.Single(p => p.Key == key),
        };
        return highContrast ? palette.HighContrast : palette;
    }

    /// <summary>Follow Dalamud on a plain dark or light host style, with a mid-grey disabled text on both.</summary>
    private static UiPalette Host(bool light) => UiPalettes.FollowDalamud(
        ColorMath.FromHex(light ? 0xF0F0F0u : 0x202020u),
        ColorMath.FromHex(light ? 0xE0E0E0u : 0x2A2A2Au),
        ColorMath.FromHex(light ? 0xD0D0D0u : 0x3A3A3Au),
        ColorMath.FromHex(light ? 0xB0B0B0u : 0x444444u),
        ColorMath.FromHex(light ? 0x101010u : 0xF0F0F0u),
        ColorMath.FromHex(0x808080));

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
        // spec-1.16 §A8: one row per palette and form, four in 1.16, and Follow Dalamud's on a dark and a light host.
        Assert.Equal(
            ["night", "night", "ishgard-snow", "ishgard-snow", DalamudDark, DalamudDark, DalamudLight, DalamudLight],
            Palettes().Select(static r => (string)r[0]));
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
        // spec-1.16 §A8 "every surface": each text ink and status word on the window, cards, wells and hovered rows,
        // but for exactly the three pairs nothing draws on a hover fill (Cool, and the Not checked word in both forms).
        var roles = UiPalettes.Night.TextPairs().Select(p => p.Role).ToHashSet();
        var inks = new[] { "Text", "TextSecondary", "TextTertiary", "Accent", "AccentDim", "Cool", "DangerText", "UnknownText" }
            .Concat(StateInks.Order.Select(static s => $"{s} word"));
        var missing = new[] { "Window", "Raised", "Sunken", "Hover" }
            .SelectMany(surface => inks.Select(ink => $"{ink} on {surface}"))
            .Where(role => !roles.Contains(role))
            .ToArray();
        Assert.Equal(["Cool on Hover", "UnknownText on Hover", "Unknown word on Hover"], missing);
        Assert.Contains("StrongLine on Raised", UiPalettes.Night.LinePairs().Select(p => p.Role));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Follow_Dalamud_keeps_its_text_readable_on_a_dark_and_a_light_host(bool light)
    {
        // The pairs the 1.16.0 review measured under the bar (every pair is in the theories above; these name them):
        // the stripes, the tree ring, the destructive button's ink, the strong line on cards, and the gold, danger,
        // tertiary and Not checked words on cards and hovered rows.
        var p = Host(light);
        var s = p.Surface;
        foreach (var (name, ground) in p.Surfaces())
        {
            Assert.True(ColorMath.Contrast(p.Accent, ground) >= ColorMath.AaText, $"Accent on {name}");
            Assert.True(ColorMath.Contrast(p.Inks.DangerText, ground) >= ColorMath.AaText, $"DangerText on {name}");
            Assert.True(ColorMath.Contrast(s.TextTertiary, ground) >= ColorMath.AaText, $"TextTertiary on {name}");
            Assert.True(ColorMath.Contrast(p.States.Text(Core.Model.QuestState.Unknown), ground) >= ColorMath.AaText, $"Not checked on {name}");
            Assert.True(ColorMath.Contrast(p.States.Stripe(Core.Model.QuestState.Ready), ground) >= ColorMath.AaNonText, $"Ready stripe on {name}");
        }

        Assert.True(ColorMath.Contrast(s.StrongLine, s.Raised) >= ColorMath.AaNonText);
        Assert.True(ColorMath.Contrast(p.Inks.GaugeArc, s.Window) >= ColorMath.AaNonText);
        Assert.True(ColorMath.Contrast(p.Inks.GaugeDone, s.Window) >= ColorMath.AaNonText);
        var button = ColorMath.Over(p.Inks.Danger with { W = PaletteInks.DangerButtonAlpha }, s.Window);
        Assert.True(ColorMath.Contrast(p.Inks.OnDanger, button) >= ColorMath.AaText);
        if (light)
        {
            Assert.Equal(ColorMath.FromHex(GaugeInks.LightArcHighHex), p.Inks.GaugeArc);
            Assert.Equal(ColorMath.FromHex(GaugeInks.LightArcShadeHex), p.Inks.GaugeDone);
        }
        else
        {
            // A dark host keeps Night's inks where they already read: Silver on the danger button, Night's tree ring.
            Assert.Equal(GlyphTokens.Silver, p.Inks.OnDanger);
            Assert.Equal(ColorMath.FromHex(PaletteInks.GaugeArcHex), p.Inks.GaugeArc);
        }
    }
}
