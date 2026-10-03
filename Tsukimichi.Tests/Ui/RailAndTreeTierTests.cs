using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Journal tree's width tiers and the rail's arithmetic (feature plan v4 L3 and L7, design v4 §7.1 and §8.2):
/// the tier for a pane width with its hysteresis, the compact rail's switch on a narrow window, and how the rail
/// gives up height on a short one.
/// </summary>
public class RailAndTreeTierTests
{
    // ---- Tree tiers ----

    [Theory]
    [InlineData(400f, TreeTier.Full)]
    [InlineData(300f, TreeTier.Full)]
    [InlineData(299f, TreeTier.Trim)]
    [InlineData(240f, TreeTier.Trim)]
    [InlineData(239f, TreeTier.Compact)]
    [InlineData(200f, TreeTier.Compact)]
    [InlineData(199f, TreeTier.Slim)]
    [InlineData(180f, TreeTier.Slim)]
    [InlineData(56f, TreeTier.Slim)]
    public void A_narrowing_tree_takes_the_tier_of_its_width(float width, TreeTier expected)
    {
        Assert.Equal(expected, LayoutBudgets.TreeTierFor(width, TreeTier.Full));
    }

    [Fact]
    public void A_widening_tree_returns_only_past_the_breakpoint_and_the_hysteresis()
    {
        var h = LayoutBudgets.HysteresisLogical;
        Assert.Equal(TreeTier.Slim, LayoutBudgets.TreeTierFor(LayoutBudgets.TreeCompactLogical + 1f, TreeTier.Slim));
        Assert.Equal(TreeTier.Compact, LayoutBudgets.TreeTierFor(LayoutBudgets.TreeCompactLogical + h, TreeTier.Slim));
        Assert.Equal(TreeTier.Trim, LayoutBudgets.TreeTierFor(LayoutBudgets.TreeFullLogical + h - 1f, TreeTier.Trim));
        Assert.Equal(TreeTier.Full, LayoutBudgets.TreeTierFor(LayoutBudgets.TreeFullLogical + h, TreeTier.Trim));

        // From the narrowest tier straight to the widest when the pane jumps (a double-click reset).
        Assert.Equal(TreeTier.Full, LayoutBudgets.TreeTierFor(PaneLayout.TreeDefaultLogical + h, TreeTier.Slim));
    }

    [Fact]
    public void A_tree_resting_on_a_breakpoint_does_not_flicker()
    {
        var tier = TreeTier.Full;
        foreach (var width in new[] { 299f, 301f, 299f, 305f, 299f })
        {
            tier = LayoutBudgets.TreeTierFor(width, tier);
            Assert.Equal(TreeTier.Trim, tier);
        }
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void An_unreadable_width_keeps_the_tier(float width)
    {
        Assert.Equal(TreeTier.Compact, LayoutBudgets.TreeTierFor(width, TreeTier.Compact));
    }

    /// <summary>UI scales 0.9 to 1.6 in steps of 0.05, each at Dalamud global scales 1 to 1.5, as UiMetrics.Scale takes them.</summary>
    public static IEnumerable<object[]> Scales()
    {
        foreach (var global in new[] { 1f, 1.1f, 1.25f, 1.5f })
        {
            for (var step = 0; step <= 14; step++)
            {
                yield return [ScaleMetrics.MinUiScale + (step * 0.05f), global];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void The_default_tree_is_in_the_full_tier(float uiScale, float globalScale)
    {
        // What TreePane measures: the ##left pane PaneLayout gives the tree (whole pixels, so up to a pixel under 300 ×
        // the scale), through the same conversion the pane uses. Its rows' room beside the scrollbar is not the tier's.
        Assert.Equal(300f, PaneLayout.TreeDefaultLogical);
        var scale = ScaleMetrics.LayoutFactor(globalScale, uiScale);
        var widths = PaneLayout.Solve(2400f * scale, MathF.Round(ScaleMetrics.RailLogical * scale), PaneLayout.TreeDefaultLogical, PaneLayout.DetailDefaultLogical, scale);
        Assert.True(widths.Tree <= PaneLayout.TreeDefaultLogical * scale, $"tree {widths.Tree}");
        Assert.Equal(TreeTier.Full, LayoutBudgets.TreeTierForPane(widths.Tree, scale, TreeTier.Full));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void A_pane_two_logical_pixels_under_a_breakpoint_takes_the_narrower_tier(float uiScale, float globalScale)
    {
        // The pixel the floor can take off is all the tolerance there is: a pane really under a breakpoint narrows.
        var scale = ScaleMetrics.LayoutFactor(globalScale, uiScale);
        foreach (var tier in new[] { TreeTier.Full, TreeTier.Trim, TreeTier.Compact })
        {
            var pane = MathF.Floor((LayoutBudgets.TreeTierFloor(tier) - 2f) * scale);
            Assert.Equal(tier + 1, LayoutBudgets.TreeTierForPane(pane, scale, TreeTier.Full));
        }
    }

    [Fact]
    public void An_unreadable_pane_keeps_the_tier()
    {
        Assert.Equal(TreeTier.Compact, LayoutBudgets.TreeTierForPane(float.NaN, 1.15f, TreeTier.Compact));
        Assert.Equal(TreeTier.Full, LayoutBudgets.TreeTierForPane(345f, float.NaN, TreeTier.Full));
    }

    [Theory]
    [InlineData(163f)]
    [InlineData(180f)]
    [InlineData(205f)]
    [InlineData(240f)]
    [InlineData(300f)]
    public void A_tree_row_never_lets_its_parts_run_under_the_name(float row)
    {
        // The overlap of screenshot 6: at 163-205 px the right-aligned count slid over the halo. With RowFit the
        // name's room and the parts that show add up to the row, whatever the widths.
        Span<float> parts = [70f, 30f, 56f, 40f];
        Span<bool> visible = stackalloc bool[4];
        var fit = RowFit.Fit(row - 60f, 200f, LayoutBudgets.RowNameMinLogical, parts, visible);
        Assert.True(fit.NameRoom + fit.PartsWidth <= MathF.Max(0f, row - 60f) + 0.01f);
        Assert.True(fit.NameRoom >= MathF.Min(LayoutBudgets.RowNameMinLogical, MathF.Max(0f, row - 60f)) - 0.01f || fit.Visible == 0);
    }

    // ---- Rail ----

    [Fact]
    public void The_rail_is_70_at_full_66_at_quiet_with_a_44_compact_mode()
    {
        // Plan v7 UI-4, spec Revision 3: wider rails so "Characters" fits its plate.
        Assert.Equal(70f, ScaleMetrics.RailLogical);
        Assert.Equal(66f, ScaleMetrics.RailQuietLogical);
        Assert.Equal(44f, ScaleMetrics.RailCompactLogical);
        Assert.Equal(70f, LayoutBudgets.RailWidthLogical(Flair.Full, compact: false));
        Assert.Equal(66f, LayoutBudgets.RailWidthLogical(Flair.Quiet, compact: false));
        Assert.Equal(44f, LayoutBudgets.RailWidthLogical(Flair.Plain, compact: false));
        Assert.Equal(44f, LayoutBudgets.RailWidthLogical(Flair.Full, compact: true));
        Assert.True(ScaleMetrics.RailCompactLogical >= LayoutBudgets.RailButtonLogical + 2f * LayoutBudgets.RailLabelPadLogical);
    }

    [Fact]
    public void Labels_have_60_px_at_full_and_56_at_quiet_inside_a_3_px_plate_inset()
    {
        Assert.Equal(60f, LayoutBudgets.RailLabelRoom(Flair.Full));
        Assert.Equal(56f, LayoutBudgets.RailLabelRoom(Flair.Quiet));
        Assert.Equal(LayoutBudgets.RailLabelRoomLogical, LayoutBudgets.RailLabelRoom(Flair.Full));
        Assert.Equal(3f, LayoutBudgets.RailPlateInsetLogical);
    }

    [Theory]
    [InlineData(Flair.Full, 30f)]
    [InlineData(Flair.Quiet, 28f)]
    [InlineData(Flair.Plain, 20f)]
    public void Station_icons_are_30_28_and_20(Flair flair, float icon)
    {
        Assert.Equal(icon, LayoutBudgets.StationIcon(flair));
    }

    [Theory]
    [InlineData(12.8f, 10f)]
    [InlineData(14.4f, 10.8f)]
    [InlineData(16f, 12f)]
    [InlineData(24f, 12f)]
    [InlineData(8f, 10f)]
    public void Rail_labels_are_three_quarters_of_the_body_held_to_10_to_12(float body, float label)
    {
        Assert.Equal(label, LayoutBudgets.RailLabelLogical(body), 3);
    }

    [Theory]
    [InlineData(Flair.Full, false)]
    [InlineData(Flair.Quiet, false)]
    [InlineData(Flair.Full, true)]
    [InlineData(Flair.Plain, true)]
    public void Stations_share_the_rail_between_their_least_and_most(Flair flair, bool compact)
    {
        // Plan v7 UI-4: the stations take the height the crest, the foot and the sky reserve leave, never past their most.
        var rule = LayoutBudgets.Stations(flair, compact);
        for (var height = 300f; height <= 2000f; height += 10f)
        {
            var fit = LayoutBudgets.FitRail(height, 5, compact, flair: flair);
            Assert.InRange(fit.Station, rule.Floor, rule.Max);
            if (fit.FootAnchored)
            {
                Assert.True(LayoutBudgets.RailHeight(fit, 5, compact) <= height + 0.05f, $"{height}: {LayoutBudgets.RailHeight(fit, 5, compact)}");
            }
        }

        // Tall: the most, with at least the reserve of sky left under the stations.
        var tall = LayoutBudgets.FitRail(2000f, 5, compact, flair: flair);
        Assert.Equal(rule.Max, tall.Station);
        Assert.True(2000f - LayoutBudgets.RailHeight(tall, 5, compact) >= rule.Reserve);
    }

    [Fact]
    public void A_full_rail_of_middling_height_keeps_its_sky_reserve()
    {
        // Between the least and the most, the stations stop where the 96 px of sky for the stars begins.
        var rule = LayoutBudgets.Stations(Flair.Full, compact: false);
        Assert.Equal(new RailStations(54f, 84f, 96f, 48f), rule);
        var noStations = LayoutBudgets.RailHeight(new RailFit(LayoutBudgets.CrestLogical, 0f, true, true), 5, compact: false);
        var height = noStations + rule.Reserve + (5 * 70f);
        var fit = LayoutBudgets.FitRail(height, 5, compact: false);
        Assert.Equal(70f, fit.Station, 3);
        Assert.Equal(rule.Reserve, height - LayoutBudgets.RailHeight(fit, 5, compact: false), 3);
    }

    [Fact]
    public void The_default_window_has_the_labelled_rail()
    {
        Assert.False(LayoutBudgets.CompactRail(ScaleMetrics.DefaultWindowLogical.X, ScaleMetrics.DefaultUiScale, wasCompact: false, forced: false));
    }

    [Fact]
    public void A_window_under_1040_gets_the_compact_rail_and_leaves_it_past_the_hysteresis()
    {
        var ui = ScaleMetrics.DefaultUiScale;
        Assert.True(LayoutBudgets.CompactRail(1039f, ui, wasCompact: false, forced: false));
        Assert.False(LayoutBudgets.CompactRail(1040f, ui, wasCompact: false, forced: false));
        Assert.True(LayoutBudgets.CompactRail(1050f, ui, wasCompact: true, forced: false));
        Assert.False(LayoutBudgets.CompactRail(1040f + LayoutBudgets.HysteresisLogical, ui, wasCompact: true, forced: false));
    }

    [Fact]
    public void The_threshold_follows_the_ui_scale()
    {
        // At UiScale 1.6 the window's minimum is far above 1,040, so the threshold grows with the scale.
        var min = ScaleMetrics.MinWindowSize(ScaleMetrics.MaxUiScale).X;
        Assert.True(LayoutBudgets.CompactRail(min, ScaleMetrics.MaxUiScale, wasCompact: false, forced: false));
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1.15f)]
    [InlineData(1.6f)]
    public void The_smallest_window_can_always_reach_the_compact_rail(float uiScale)
    {
        Assert.True(LayoutBudgets.CompactRail(ScaleMetrics.MinWindowSize(uiScale).X, uiScale, wasCompact: false, forced: false));
    }

    [Fact]
    public void The_setting_forces_the_compact_rail_at_any_width()
    {
        Assert.True(LayoutBudgets.CompactRail(3000f, ScaleMetrics.DefaultUiScale, wasCompact: false, forced: true));
    }

    [Theory]
    [InlineData(float.NaN, true)]
    [InlineData(float.NaN, false)]
    public void An_unreadable_window_keeps_the_rail_as_it_was(float width, bool wasCompact)
    {
        Assert.Equal(wasCompact, LayoutBudgets.CompactRail(width, ScaleMetrics.DefaultUiScale, wasCompact, forced: false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_tall_rail_has_everything(bool compact)
    {
        var fit = LayoutBudgets.FitRail(1000f, 5, compact);
        Assert.Equal(compact ? LayoutBudgets.CrestSmallLogical : LayoutBudgets.CrestLogical, fit.Crest);
        // The stations share the height (plan v7 UI-4): a tall rail gives them their most.
        Assert.Equal(compact ? LayoutBudgets.CompactStationMaxLogical : LayoutBudgets.StationMaxLogical, fit.Station);
        Assert.True(fit.Percent);
        Assert.True(fit.FootAnchored);
    }

    [Fact]
    public void A_short_rail_gives_up_the_crest_size_then_the_percentage_then_station_height()
    {
        var full = LayoutBudgets.RailHeight(new RailFit(LayoutBudgets.CrestLogical, LayoutBudgets.StationLogical, true, true), 5, compact: false);
        var smallCrest = LayoutBudgets.FitRail(full - 1f, 5, compact: false);
        Assert.Equal(LayoutBudgets.CrestSmallLogical, smallCrest.Crest);
        Assert.True(smallCrest.Percent);
        Assert.Equal(LayoutBudgets.StationLogical, smallCrest.Station);

        var noPercent = LayoutBudgets.FitRail(LayoutBudgets.RailHeight(smallCrest, 5, compact: false) - 1f, 5, compact: false);
        Assert.False(noPercent.Percent);
        Assert.Equal(LayoutBudgets.StationLogical, noPercent.Station);

        var shorter = LayoutBudgets.FitRail(LayoutBudgets.RailHeight(noPercent, 5, compact: false) - 20f, 5, compact: false);
        Assert.InRange(shorter.Station, LayoutBudgets.StationMinLogical, LayoutBudgets.StationLogical - 1f);
        Assert.True(shorter.FootAnchored);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_rail_that_cannot_fit_scrolls_rather_than_overlapping(bool compact)
    {
        var fit = LayoutBudgets.FitRail(120f, 5, compact);
        Assert.False(fit.FootAnchored);
        Assert.Equal(0f, fit.Crest);
        // At its floor: the icon (and on the labelled rail one line of label) on its plate.
        Assert.Equal(LayoutBudgets.Stations(Flair.Full, compact).Floor, fit.Station);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_anchored_foot_always_fits_under_the_stations(bool compact)
    {
        for (var height = 100f; height <= 700f; height += 7f)
        {
            var fit = LayoutBudgets.FitRail(height, 5, compact);
            if (fit.FootAnchored)
            {
                Assert.True(LayoutBudgets.RailHeight(fit, 5, compact) <= height + 0.05f, $"{height}: {LayoutBudgets.RailHeight(fit, 5, compact)}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void A_placed_rail_whose_foot_is_anchored_never_overflows_its_pane(float uiScale, float globalScale)
    {
        // TabStrip draws exactly PlaceRail: whole-pixel stations and the foot buttons at the 24 px click-target floor
        // (UiMetrics.MinTarget), which is more than 26 logical px under a scale of 24/26. Its content (a 1 px item at
        // ContentBottom) must end inside the pane whenever the foot is anchored, or the rail becomes wheel-scrollable.
        var scale = ScaleMetrics.LayoutFactor(globalScale, uiScale);
        var button = MathF.Max(LayoutBudgets.RailButtonLogical * scale, 24f);
        foreach (var (compact, flair) in new[] { (false, Flair.Full), (false, Flair.Quiet), (true, Flair.Full), (true, Flair.Plain) })
        {
            for (var height = 150f; height <= 1400f; height += 0.75f)
            {
                var place = LayoutBudgets.PlaceRail(height, scale, 5, compact, button, flair);
                if (!place.Fit.FootAnchored)
                {
                    continue;
                }

                var foot = LayoutBudgets.FootHeight(place.Fit, compact, place.Button / scale) * scale;
                var stationsBottom = place.StationsTop + (5 * place.Station);
                var where = $"{height} px, compact {compact}";
                Assert.True(place.ContentBottom <= height, $"{where}: content ends at {place.ContentBottom}");
                Assert.True(place.FootTop + foot + (LayoutBudgets.RailPadLogical * scale) <= height + 0.05f, $"{where}: foot ends at {place.FootTop + foot}");
                Assert.True(stationsBottom + (LayoutBudgets.RailGapLogical * scale) <= place.FootTop + 0.001f, $"{where}: stations end at {stationsBottom}, foot at {place.FootTop}");
                Assert.Equal(button, place.Button, 3);
                Assert.Equal(MathF.Floor(place.Station), place.Station);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void The_labelled_rail_only_shows_on_a_window_wider_than_its_own_minimum(float uiScale, float globalScale)
    {
        // So the main window's minimum is the compact rail's always: one that followed the rail would never bind.
        var labelledMin = ScaleMetrics.MinWindowSize(uiScale, globalScale, new System.Numerics.Vector2(float.NaN), ScaleMetrics.RailLogical).X;
        var compactMin = ScaleMetrics.MinWindowSize(uiScale, globalScale, new System.Numerics.Vector2(float.NaN), ScaleMetrics.RailCompactLogical).X;
        Assert.True(compactMin < labelledMin);
        foreach (var wasCompact in new[] { false, true })
        {
            Assert.True(LayoutBudgets.CompactRail(labelledMin - 0.01f, uiScale, wasCompact, forced: false), $"{uiScale} × {globalScale}: labelled rail at {labelledMin - 0.01f}");
        }
    }

    [Fact]
    public void The_labelled_rail_fits_the_smallest_window_without_scrolling()
    {
        // At the minimum height (500 logical) the body keeps about 360 logical px under the toolbar's two rows, the
        // chips and the status bar; the rail fits there by giving up the crest's size, the percentage and some height.
        var fit = LayoutBudgets.FitRail(360f, 5, compact: false);
        Assert.True(fit.FootAnchored);
        Assert.True(fit.Crest > 0f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_foot_holds_four_buttons_two_a_row_or_stacked(bool compact)
    {
        // Overlay and Nearby (1.7.0) join Help and Settings: two rows of two on the labelled rail, four stacked on the compact one.
        var button = LayoutBudgets.RailButtonLogical;
        var gap = LayoutBudgets.RailGapLogical;
        var expected = compact ? (4 * button) + (3 * gap) : (2 * button) + gap;
        Assert.Equal(expected, LayoutBudgets.FootButtonsHeight(compact), 3);
        Assert.Equal(LayoutBudgets.RailGaugeLogical + gap + expected, LayoutBudgets.FootHeight(percent: false, compact), 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_short_rail_gives_up_the_gauge_before_the_crest(bool compact)
    {
        // Just short of what the stations at their minimum need with the gauge: the gauge goes, the crest stays.
        var stationMin = LayoutBudgets.Stations(Flair.Full, compact).Floor;
        var withGauge = LayoutBudgets.RailHeight(new RailFit(LayoutBudgets.CrestSmallLogical, stationMin, false, true), 5, compact);
        var fit = LayoutBudgets.FitRail(withGauge - 1f, 5, compact);
        Assert.True(fit.GaugeHidden);
        Assert.True(fit.Crest > 0f);
        Assert.True(fit.FootAnchored);
        Assert.True(LayoutBudgets.RailHeight(fit, 5, compact) <= withGauge - 1f + 0.05f);

        // A tall rail keeps it.
        Assert.False(LayoutBudgets.FitRail(1000f, 5, compact).GaugeHidden);
    }
}
