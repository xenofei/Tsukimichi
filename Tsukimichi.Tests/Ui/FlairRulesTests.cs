using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// What each Decoration level draws (moon-road proposal P5, §9, §10.2; feature plan v4 V1; docs/design/flair-v13 §1 and
/// §2): three looks that differ in pane tone, art, card material, row density and medal finish.
/// </summary>
public class FlairRulesTests
{
    [Fact]
    public void Full_is_the_moon_road()
    {
        const Flair full = Flair.Full;
        Assert.Equal(RuleStyle.MoonRoad, FlairRules.Rule(full));
        Assert.Equal(PaneTone.Gradient, FlairRules.Panes(full));
        Assert.True(FlairRules.PaneGradient(full));
        Assert.True(FlairRules.StarField(full));
        Assert.True(FlairRules.CornerMarks(full));
        Assert.True(FlairRules.Glow(full));
        Assert.True(FlairRules.Banner(full));
        Assert.True(FlairRules.BannerGrade(full));
        Assert.True(FlairRules.Motion(full, reduceMotion: false));
        Assert.True(FlairRules.UiMotion(full, reduceMotion: false));
        Assert.Equal(CardFrame.BrassCorners, FlairRules.Card(full));
        Assert.Equal(TableHeaderStyle.MoonRoad, FlairRules.TableHeader(full));
        Assert.True(FlairRules.ReadyRoad(full));
        Assert.Equal(HeroStyle.Banner, FlairRules.Hero(full));
        Assert.Equal(MedalFinish.Gilt, FlairRules.Medal(full, MoonStyle.Medallion));
        Assert.Equal(TreeGauge.Orbit, FlairRules.Gauge(full));
        Assert.Equal(StatusBarStyle.MoonRoad, FlairRules.StatusBar(full));
        Assert.Equal(TooltipStyle.Brass, FlairRules.Tooltip(full));
        Assert.False(FlairRules.CompactRail(full));
    }

    [Fact]
    public void Quiet_is_still_water()
    {
        const Flair quiet = Flair.Quiet;
        Assert.Equal(RuleStyle.Hairline, FlairRules.Rule(quiet));
        Assert.Equal(PaneTone.Tonal, FlairRules.Panes(quiet));
        Assert.False(FlairRules.PaneGradient(quiet));
        Assert.False(FlairRules.StarField(quiet));
        Assert.False(FlairRules.CornerMarks(quiet));
        Assert.False(FlairRules.Glow(quiet));
        Assert.False(FlairRules.Banner(quiet));
        Assert.False(FlairRules.BannerGrade(quiet));
        Assert.False(FlairRules.Motion(quiet, reduceMotion: false));
        Assert.True(FlairRules.UiMotion(quiet, reduceMotion: false));
        Assert.Equal(CardFrame.Tonal, FlairRules.Card(quiet));
        Assert.False(FlairRules.CardBorder(quiet, highContrast: false));
        Assert.Equal(TableHeaderStyle.Eyebrow, FlairRules.TableHeader(quiet));
        Assert.False(FlairRules.ReadyRoad(quiet));
        Assert.Equal(HeroStyle.Plate, FlairRules.Hero(quiet));
        Assert.Equal(MedalFinish.LightRim, FlairRules.Medal(quiet, MoonStyle.Medallion));
        Assert.Equal(TreeGauge.Ring, FlairRules.Gauge(quiet));
        Assert.Equal(StatusBarStyle.Quiet, FlairRules.StatusBar(quiet));
        Assert.Equal(TooltipStyle.Flat, FlairRules.Tooltip(quiet));
        Assert.False(FlairRules.CompactRail(quiet));
    }

    [Fact]
    public void Plain_is_the_ledger()
    {
        const Flair plain = Flair.Plain;
        Assert.Equal(RuleStyle.Line, FlairRules.Rule(plain));
        Assert.Equal(PaneTone.Flat, FlairRules.Panes(plain));
        Assert.False(FlairRules.PaneGradient(plain));
        Assert.False(FlairRules.StarField(plain));
        Assert.False(FlairRules.CornerMarks(plain));
        Assert.False(FlairRules.Glow(plain));
        Assert.False(FlairRules.Banner(plain));
        Assert.False(FlairRules.Motion(plain, reduceMotion: false));
        Assert.False(FlairRules.UiMotion(plain, reduceMotion: false));
        Assert.Equal(CardFrame.None, FlairRules.Card(plain));
        Assert.False(FlairRules.CardBorder(plain, highContrast: true));
        Assert.Equal(TableHeaderStyle.Raised, FlairRules.TableHeader(plain));
        Assert.True(FlairRules.Zebra(plain, RowDensity.Comfortable));
        Assert.Equal(HeroStyle.Ledger, FlairRules.Hero(plain));
        Assert.Equal(MedalFinish.Plain, FlairRules.Medal(plain, MoonStyle.Medallion));
        Assert.Equal(TreeGauge.None, FlairRules.Gauge(plain));
        Assert.Equal(StatusBarStyle.Text, FlairRules.StatusBar(plain));
        Assert.Equal(TooltipStyle.Plain, FlairRules.Tooltip(plain));
        Assert.True(FlairRules.CompactRail(plain));
    }

    [Fact]
    public void The_three_looks_differ_in_every_dimension_a_thumbnail_shows()
    {
        // The owner's complaint: "barely look any different". Pane tone, art, card material, density and medal finish
        // each take a different value at every level.
        Flair[] levels = [Flair.Full, Flair.Quiet, Flair.Plain];
        Assert.Equal(3, levels.Select(FlairRules.Panes).Distinct().Count());
        Assert.Equal(3, levels.Select(FlairRules.Card).Distinct().Count());
        Assert.Equal(3, levels.Select(FlairRules.Rule).Distinct().Count());
        Assert.Equal(3, levels.Select(FlairRules.Hero).Distinct().Count());
        Assert.Equal(3, levels.Select(l => FlairRules.Medal(l, MoonStyle.Medallion)).Distinct().Count());
        Assert.Equal(3, levels.Select(l => ScaleMetrics.TableRowTarget(l, RowDensity.Comfortable)).Distinct().Count());
        Assert.Equal(3, levels.Select(ScaleMetrics.TreeRowTarget).Distinct().Count());
    }

    [Fact]
    public void Reduce_motion_stops_every_moment_and_loop_at_every_level()
    {
        foreach (var flair in Enum.GetValues<Flair>())
        {
            Assert.False(FlairRules.Motion(flair, reduceMotion: true));
            Assert.False(FlairRules.UiMotion(flair, reduceMotion: true));
        }
    }

    [Theory]
    [InlineData(Flair.Full, false, Flair.Full)]
    [InlineData(Flair.Quiet, false, Flair.Quiet)]
    [InlineData(Flair.Plain, false, Flair.Plain)]
    [InlineData(Flair.Full, true, Flair.Quiet)]
    [InlineData(Flair.Quiet, true, Flair.Quiet)]
    [InlineData(Flair.Plain, true, Flair.Plain)]
    [InlineData((Flair)42, false, Flair.Full)]
    public void High_contrast_draws_at_most_quiet(Flair setting, bool highContrast, Flair expected)
    {
        Assert.Equal(expected, FlairRules.Effective(setting, highContrast));
    }

    [Fact]
    public void High_contrast_cards_are_tonal_with_a_border()
    {
        // Tone alone is not a boundary for low-vision users: the tonal card takes a VeilLine border.
        var effective = FlairRules.Effective(Flair.Full, highContrast: true);
        Assert.Equal(CardFrame.Tonal, FlairRules.Card(effective));
        Assert.True(FlairRules.CardBorder(effective, highContrast: true));
        Assert.False(FlairRules.CardBorder(Flair.Quiet, highContrast: false));
        Assert.True(FlairRules.CardBorder(Flair.Full, highContrast: false));
    }

    [Fact]
    public void High_contrast_draws_no_ready_road_glow_stars_or_banner_grade()
    {
        var effective = FlairRules.Effective(Flair.Full, highContrast: true);
        Assert.False(FlairRules.ReadyRoad(effective));
        Assert.False(FlairRules.Glow(effective));
        Assert.False(FlairRules.StarField(effective));
        Assert.False(FlairRules.BannerGrade(effective));
    }

    [Theory]
    [InlineData(Flair.Full, true, true)]
    [InlineData(Flair.Quiet, true, false)]
    [InlineData(Flair.Plain, true, false)]
    [InlineData(Flair.Full, false, false)]
    [InlineData((Flair)42, true, true)]
    public void Game_heading_fonts_follow_the_toggle_at_full_only(Flair flair, bool toggle, bool expected)
    {
        Assert.Equal(expected, FlairRules.GameHeadingFonts(flair, toggle));
    }

    [Fact]
    public void The_hero_halo_is_light_on_what_can_be_taken_now_only()
    {
        // Supervisor fix 4: nothing blocked, done or completed glows; only Ready and Ready on another job, and only where
        // the level glows at all.
        foreach (var state in Enum.GetValues<QuestState>())
        {
            var lit = state is QuestState.Ready or QuestState.ReadyOnOtherJob;
            Assert.Equal(lit, FlairRules.HeroHalo(Flair.Full, state));
            Assert.False(FlairRules.HeroHalo(Flair.Quiet, state));
            Assert.False(FlairRules.HeroHalo(Flair.Plain, state));
            Assert.False(FlairRules.HeroHalo(FlairRules.Effective(Flair.Full, highContrast: true), state));
        }
    }

    [Fact]
    public void Moon_style_classic_keeps_the_classic_glyphs_at_every_level()
    {
        foreach (var flair in Enum.GetValues<Flair>())
        {
            Assert.Equal(MedalFinish.Classic, FlairRules.Medal(flair, MoonStyle.Classic));
        }

        Assert.Equal(MedalFinish.Gilt, FlairRules.Medal(Flair.Full, (MoonStyle)42));
    }

    [Theory]
    [InlineData(Flair.Full, RowDensity.Comfortable, 34f)]
    [InlineData(Flair.Full, RowDensity.Dense, 28f)]
    [InlineData(Flair.Quiet, RowDensity.Comfortable, 30f)]
    [InlineData(Flair.Quiet, RowDensity.Dense, 24f)]
    [InlineData(Flair.Plain, RowDensity.Comfortable, 24f)]
    [InlineData(Flair.Plain, RowDensity.Dense, 24f)]
    public void Each_level_has_its_row_height(Flair flair, RowDensity density, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.TableRowTarget(flair, density));
    }

    [Fact]
    public void No_level_or_density_takes_a_row_under_the_minimum_target()
    {
        // WCAG 2.5.8 (accessibility B4): rows are contiguous click targets of at least 24 px, at any scale.
        foreach (var flair in Enum.GetValues<Flair>().Append((Flair)42))
        {
            foreach (var density in Enum.GetValues<RowDensity>().Append((RowDensity)7))
            {
                Assert.True(ScaleMetrics.TableRowTarget(flair, density) >= ScaleMetrics.TableRowMinPx);
                foreach (var global in new[] { 0.5f, 0.75f, 1f, 1.5f, float.NaN })
                {
                    foreach (var padding in new[] { 0f, 2f, 4f, 8f })
                    {
                        var content = ScaleMetrics.TableRowContent(flair, density, global, 10f, padding);
                        Assert.True(content + (2f * padding) >= ScaleMetrics.TableRowMinPx - 0.001f, $"{flair} {density} at {global}: {content} + 2 × {padding}");
                    }
                }
            }

            Assert.True(ScaleMetrics.TreeRowTarget(flair) >= ScaleMetrics.TableRowMinPx);
        }
    }

    [Fact]
    public void A_row_never_shrinks_under_its_content()
    {
        Assert.Equal(40f, ScaleMetrics.TableRowContent(Flair.Plain, RowDensity.Dense, 1f, 40f, 2f));
        Assert.Equal(30f, ScaleMetrics.TableRowContent(Flair.Full, RowDensity.Comfortable, 1f, 10f, 2f));
        Assert.Equal(20f, ScaleMetrics.TableRowContent(Flair.Plain, RowDensity.Comfortable, 1f, 10f, 2f));
    }

    [Fact]
    public void Tree_rows_follow_the_level_and_keep_the_halo_box_where_a_gauge_draws()
    {
        // Full and Quiet draw a gauge, so the A4 halo box (24 px) and its padding hold; Plain has none.
        Assert.Equal(34f, ScaleMetrics.TreeRowHeight(Flair.Full, 17f, 12f, 1f, 1f));
        Assert.Equal(30f, ScaleMetrics.TreeRowHeight(Flair.Quiet, 17f, 12f, 1f, 1f));
        Assert.Equal(24f, ScaleMetrics.TreeRowHeight(Flair.Plain, 17f, 12f, 1f, 1f));
        Assert.Equal(42f, ScaleMetrics.TreeRowHeight(Flair.Quiet, 17f, 18f, 1f, 1f));
        Assert.Equal(34f, ScaleMetrics.TreeRowHeight(Flair.Plain, 30f, 18f, 1f, 1f));
        Assert.Equal(51f, ScaleMetrics.TreeRowHeight(Flair.Full, 17f, 12f, 1f, 1.5f));
    }

    [Fact]
    public void The_spacing_tokens_step_down_from_full_to_plain()
    {
        var full = FlairRules.Spacing(Flair.Full);
        var quiet = FlairRules.Spacing(Flair.Quiet);
        var plain = FlairRules.Spacing(Flair.Plain);
        Assert.True(full.Gap > quiet.Gap && quiet.Gap > plain.Gap);
        Assert.True(full.PanePad > quiet.PanePad && quiet.PanePad > plain.PanePad);
        Assert.True(full.PillHeight > quiet.PillHeight && quiet.PillHeight > plain.PillHeight);
        Assert.Equal(System.Numerics.Vector2.Zero, plain.CardPad);
        Assert.True(FlairRules.RowGlyphFloorLogical(Flair.Full) > FlairRules.RowGlyphFloorLogical(Flair.Quiet));
        Assert.True(FlairRules.RowGlyphFloorLogical(Flair.Quiet) > FlairRules.RowGlyphFloorLogical(Flair.Plain));
    }

    [Fact]
    public void The_status_bar_gets_lower_from_full_to_plain_and_follows_sizes_alone()
    {
        const float line = 17f;
        const float spacing = 4f;
        var full = ChromeBands.StatusBarHeight(line, spacing, StatusBarStyle.MoonRoad);
        var quiet = ChromeBands.StatusBarHeight(line, spacing, StatusBarStyle.Quiet);
        var text = ChromeBands.StatusBarHeight(line, spacing, StatusBarStyle.Text);
        Assert.Equal(ChromeBands.StatusBarHeight(line, spacing), full);
        Assert.True(full > quiet && quiet > text && text >= line);
    }
}
