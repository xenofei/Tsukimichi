using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The glyph palettes (Settings › Display › Glyph palette; accessibility panel §2.2, A1–A4; glyph proposal §3.7): the
/// high-contrast palette keeps every state apart without colour, on a luminance ladder, with every ink at 3 : 1.
/// </summary>
public class GlyphPaletteTests
{
    private static readonly Vector4 Night = GlyphTokens.Night;
    private static readonly Vector4 NightRaised = Vector4.Lerp(GlyphTokens.Night, GlyphTokens.Veil, 0.25f);
    private static readonly Vector4 White = ColorMath.FromHex(0xFFFFFF);

    private static readonly QuestState[] States = Enum.GetValues<QuestState>();

    public static TheoryData<string> Variants => new() { "dark", "light" };

    public static TheoryData<string, ColorVision> VariantsUnderSimulation()
    {
        var data = new TheoryData<string, ColorVision>();
        foreach (var variant in new[] { "dark", "light" })
        {
            foreach (var mode in ColorVisionSimulation.All)
            {
                data.Add(variant, mode);
            }
        }

        return data;
    }

    private static GlyphPalette Variant(string name) => name == "light" ? GlyphPalette.HighContrastLight : GlyphPalette.HighContrastDark;

    /// <summary>The background each variant is made for: Night for the dark one, a white host window for the light one.</summary>
    private static Vector4 HomeBackground(string name) => name == "light" ? White : Night;

    private static IEnumerable<(GlyphStyle A, GlyphStyle B)> Pairs(GlyphPalette palette)
    {
        for (var i = 0; i < States.Length; i++)
        {
            for (var j = i + 1; j < States.Length; j++)
            {
                yield return (palette.Style(States[i]), palette.Style(States[j]));
            }
        }
    }

    private static float Grey(Vector4 a, Vector4 b, ColorVision mode = ColorVision.None) =>
        ColorMath.Contrast(ColorVisionSimulation.Simulate(a, mode), ColorVisionSimulation.Simulate(b, mode));

    [Fact]
    public void Standard_resolves_to_itself_on_any_host()
    {
        Assert.Same(GlyphPalette.Standard, GlyphPalette.Resolve(GlyphPaletteKind.Standard, Night));
        Assert.Same(GlyphPalette.Standard, GlyphPalette.Resolve(GlyphPaletteKind.Standard, White));
        Assert.False(GlyphPalette.Standard.HighContrast);
        Assert.Equal(0f, GlyphPalette.Standard.Keyline(24f));
    }

    [Theory]
    [InlineData(0x0F1424u, false)] // Night
    [InlineData(0x0F0F0Fu, false)] // Dalamud's default dark window
    [InlineData(0x1E2437u, false)] // NightRaised
    [InlineData(0xFFFFFFu, true)]
    [InlineData(0xF0F0F0u, true)]
    [InlineData(0xE6E6E6u, true)]
    public void High_contrast_resolves_its_variant_by_host_luminance(uint window, bool light)
    {
        var palette = GlyphPalette.Resolve(GlyphPaletteKind.HighContrast, ColorMath.FromHex(window));
        Assert.True(palette.HighContrast);
        Assert.Equal(light, palette.Light);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Every_state_has_its_own_mark(string variant)
    {
        var palette = Variant(variant);
        var marks = States.Select(s => palette.Style(s).Mark).ToArray();
        Assert.Equal(marks.Length, marks.Distinct().Count());
        Assert.DoesNotContain(GlyphMark.None, marks);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Every_pair_differs_in_greyscale_luminance_by_one_and_a_half_or_in_mark_kind(string variant)
    {
        foreach (var (a, b) in Pairs(Variant(variant)))
        {
            Assert.True(
                a.Mark != b.Mark || Grey(a.Identity, b.Identity, ColorVision.Greyscale) >= 1.5f,
                $"{a.State} and {b.State} share the mark {a.Mark} and differ by only {Grey(a.Identity, b.Identity, ColorVision.Greyscale):0.00} : 1 in greyscale");
        }
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void States_that_share_a_silhouette_sit_three_to_one_apart_on_the_ladder(string variant)
    {
        var palette = Variant(variant);
        var shared = Pairs(palette).Where(p => p.A.Silhouette == p.B.Silhouette).ToList();

        // Ready / Ready on another job (half lit) and In journal / Done (gibbous, mirror images).
        Assert.Equal(2, shared.Count);
        foreach (var (a, b) in shared)
        {
            var ratio = Grey(a.Identity, b.Identity);
            Assert.True(ratio >= 3f, $"{a.State} and {b.State} share {a.Silhouette} but are {ratio:0.00} : 1");
        }
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Blocked_rim_stands_three_to_one_from_the_other_empty_discs(string variant)
    {
        var palette = Variant(variant);
        var blocked = palette.Style(QuestState.Blocked).Identity;
        Assert.True(Grey(blocked, palette.Style(QuestState.Foreclosed).Identity) >= 3f);
        Assert.True(Grey(blocked, palette.Style(QuestState.Unknown).Identity) >= 3f);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Every_ink_clears_three_to_one_against_its_home_background(string variant)
    {
        var palette = Variant(variant);
        var background = HomeBackground(variant);
        foreach (var (part, color) in palette.Inks())
        {
            var ratio = ColorMath.Contrast(color, background);
            Assert.True(ratio >= 3f, $"{palette.Name}: {part} is {ratio:0.00} : 1");
        }
    }

    [Fact]
    public void Every_glyph_colour_clears_three_to_one_on_Night_and_on_a_white_host()
    {
        // The setting resolves per host: the palette drawn on Night and the one drawn on a white Dalamud theme.
        foreach (var background in new[] { Night, White })
        {
            var palette = GlyphPalette.Resolve(GlyphPaletteKind.HighContrast, background);
            foreach (var (part, color) in palette.Inks())
            {
                Assert.True(ColorMath.Contrast(color, background) >= 3f, $"{palette.Name}: {part} on {background}");
            }
        }
    }

    [Theory]
    [InlineData(0x0F1424u)] // Night
    [InlineData(0x1E2437u)] // NightRaised: cards
    [InlineData(0x0B0F1Cu)] // NightSunken: wells
    [InlineData(0x000000u)]
    [InlineData(0x0F0F0Fu)] // Dalamud default
    [InlineData(0x1A1A1Au)]
    [InlineData(0xFFFFFFu)]
    [InlineData(0xF0F0F0u)]
    [InlineData(0xE6E6E6u)]
    public void Every_ink_clears_three_to_one_on_realistic_host_windows(uint window)
    {
        var host = ColorMath.FromHex(window);
        var palette = GlyphPalette.Resolve(GlyphPaletteKind.HighContrast, host);
        foreach (var (part, color) in palette.Inks())
        {
            var ratio = ColorMath.Contrast(color, host);
            Assert.True(ratio >= 3f, $"{palette.Name} on #{window:X6}: {part} is {ratio:0.00} : 1");
        }
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Every_part_clears_three_to_one_against_what_it_is_drawn_on(string variant)
    {
        var palette = Variant(variant);
        foreach (var state in States)
        {
            var style = palette.Style(state);
            if (style.HasLit && style.Phase != MoonPhase.Full)
            {
                Assert.True(ColorMath.Contrast(style.Lit, palette.Ground) >= 3f, $"{state}: terminator");
            }

            if (style.RimStyle != RimStyle.None)
            {
                Assert.True(ColorMath.Contrast(style.Rim, palette.Ground) >= 3f, $"{state}: rim on the disc");
            }

            if (style.MarkOnLit)
            {
                Assert.True(ColorMath.Contrast(style.MarkColor, style.Lit) >= 3f, $"{state}: {style.Mark} on the lit part");
            }
            else if (style.Mark is GlyphMark.Bar or GlyphMark.HollowBar or GlyphMark.ThickDiagonalBar)
            {
                Assert.True(ColorMath.Contrast(style.MarkColor, palette.Ground) >= 3f, $"{state}: {style.Mark} on the disc");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void The_keyline_carries_each_glyph_onto_the_other_background(string variant)
    {
        // A high-contrast glyph brings its own ground as a keyline, so the light variant still reads on the Night
        // hover card and the dark one over a bright scene: the ground clears 3 : 1 on the opposite background.
        var palette = Variant(variant);
        var opposite = variant == "light" ? Night : White;
        Assert.True(ColorMath.Contrast(palette.Ground, opposite) >= 3f);
        Assert.True(palette.Keyline(6f) >= 1f);
        Assert.True(palette.Keyline(24f) > 1f);
    }

    [Theory]
    [InlineData("dark", 0x0F1424u, false)]
    [InlineData("dark", 0x1E2437u, true)]
    [InlineData("dark", 0x0F0F0Fu, false)]
    [InlineData("light", 0xFFFFFFu, false)]
    [InlineData("light", 0xE6E6E6u, true)]
    public void Halo_track_and_arc_clear_three_to_one_against_the_background_and_each_other(string variant, uint background, bool onCard)
    {
        var palette = Variant(variant);
        var bg = ColorMath.FromHex(background);
        var track = onCard ? palette.HaloTrackOnCard : palette.HaloTrack;
        Assert.True(ColorMath.Contrast(track, bg) >= 3f, "track");
        Assert.True(ColorMath.Contrast(palette.HaloArc, bg) >= 3f, "arc");
        Assert.True(ColorMath.Contrast(palette.HaloArc, track) >= 3f, "arc on track");
        Assert.True(ColorMath.Contrast(palette.HaloCompleteDim, bg) >= 3f, "dim complete ring");
    }

    [Fact]
    public void High_contrast_rims_and_halo_are_thicker()
    {
        foreach (var palette in new[] { GlyphPalette.HighContrastDark, GlyphPalette.HighContrastLight })
        {
            for (var r = 4f; r <= 64f; r += 0.5f)
            {
                foreach (var state in States)
                {
                    var style = palette.Style(state);
                    Assert.True(style.RimWidth(r) >= 2f, $"{state} at r {r}");
                    Assert.True(style.RimWidth(r) >= GlyphPalette.Standard.Style(state).RimWidth(r), $"{state} at r {r}");
                }

                Assert.True(palette.Style(QuestState.Blocked).RimWidth(r) > palette.Style(QuestState.Ready).RimWidth(r));
                Assert.True(palette.HaloStroke(r) > GlyphPalette.Standard.HaloStroke(r));
            }

            Assert.True(palette.StripeWidth > GlyphPalette.Standard.StripeWidth);
        }
    }

    [Theory]
    [MemberData(nameof(VariantsUnderSimulation))]
    public void Simulated_colour_blindness_keeps_the_high_contrast_states_apart(string variant, ColorVision mode)
    {
        var palette = Variant(variant);
        var background = HomeBackground(variant);

        foreach (var (a, b) in Pairs(palette))
        {
            var ratio = Grey(a.Identity, b.Identity, mode);
            if (a.Silhouette == b.Silhouette)
            {
                Assert.True(ratio >= 3f, $"{mode}: {a.State} and {b.State} share {a.Silhouette} at {ratio:0.00} : 1");
            }

            Assert.True(a.Mark != b.Mark || ratio >= 1.5f, $"{mode}: {a.State} and {b.State}");
        }

        foreach (var (part, color) in palette.Inks())
        {
            var ratio = Grey(color, background, mode);
            Assert.True(ratio >= 3f, $"{mode}: {palette.Name} {part} is {ratio:0.00} : 1");
        }

        Assert.True(Grey(palette.HaloArc, palette.HaloTrack, mode) >= 3f, $"{mode}: halo arc on track");
        foreach (var state in States)
        {
            var style = palette.Style(state);
            if (style.HasLit && style.Phase != MoonPhase.Full)
            {
                Assert.True(Grey(style.Lit, palette.Ground, mode) >= 3f, $"{mode}: {state} terminator");
            }

            if (style.RimStyle != RimStyle.None)
            {
                Assert.True(Grey(style.Rim, palette.Ground, mode) >= 3f, $"{mode}: {state} rim on the disc");
            }
        }
    }

    [Fact]
    public void Standard_gold_and_silver_collapse_in_greyscale_which_is_why_the_palette_exists()
    {
        // Panel A2: Moon vs Silver is 1.14 : 1 under achromatopsia, so Standard's Ready and Ready on another job share a
        // silhouette with nothing but hue between their lit halves. High contrast puts them on different rungs.
        var standard = GlyphPalette.Standard;
        Assert.True(Grey(standard.Style(QuestState.Ready).Identity, standard.Style(QuestState.ReadyOnOtherJob).Identity, ColorVision.Greyscale) < 1.2f);

        var contrast = GlyphPalette.HighContrastDark;
        Assert.True(Grey(contrast.Style(QuestState.Ready).Identity, contrast.Style(QuestState.ReadyOnOtherJob).Identity, ColorVision.Greyscale) >= 3f);
    }

    [Fact]
    public void Standard_describes_the_shipped_glyphs()
    {
        var p = GlyphPalette.Standard;
        Assert.Equal(GlyphMark.Seal, p.Style(QuestState.Accepted).Mark);
        Assert.Equal(GlyphMark.DiagonalBar, p.Style(QuestState.Foreclosed).Mark);
        Assert.Equal(RimStyle.Dashed, p.Style(QuestState.Unknown).RimStyle);
        Assert.Equal(RimStyle.None, p.Style(QuestState.Completed).RimStyle);
        Assert.Equal(1.5f, p.Style(QuestState.Blocked).RimWidth(6f));
        Assert.Equal(3f, p.Style(QuestState.Blocked).RimWidth(40f));
        foreach (var state in States)
        {
            Assert.Equal(MoonGeometry.PhaseOf(state), p.Style(state).Phase);
            Assert.Equal(MoonGeometry.PhaseOf(state), GlyphPalette.HighContrastDark.Style(state).Phase);
        }
    }

    [Fact]
    public void High_contrast_stripes_follow_the_state_identity()
    {
        foreach (var palette in new[] { GlyphPalette.HighContrastDark, GlyphPalette.HighContrastLight })
        {
            foreach (var state in States)
            {
                Assert.Equal(palette.Style(state).Identity, palette.Stripe(state));
            }
        }
    }

    [Fact]
    public void High_contrast_subtitles_name_every_mark_and_reach_the_tooltips()
    {
        var subtitles = States.Select(StateNames.HighContrastSubtitle).ToArray();
        Assert.All(subtitles, s => Assert.False(string.IsNullOrWhiteSpace(s)));
        Assert.Equal(subtitles.Length, subtitles.Distinct().Count());

        Assert.Equal("Ready · bright half, bold bar", StateNames.Tooltip(QuestState.Ready, 0, highContrast: true));
        Assert.Equal(StateNames.Tooltip(QuestState.Ready, 0), StateNames.Tooltip(QuestState.Ready, 0, highContrast: false));
        Assert.Equal("Done today · dim gibbous, check", StateNames.Tooltip(QuestState.DoneThisCycle, StateNames.DailyInterval, highContrast: true));
    }
}
