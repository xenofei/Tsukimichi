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
    public void The_rail_is_64_with_a_44_compact_mode()
    {
        Assert.Equal(64f, ScaleMetrics.RailLogical);
        Assert.Equal(44f, ScaleMetrics.RailCompactLogical);
        Assert.True(ScaleMetrics.RailCompactLogical >= LayoutBudgets.RailButtonLogical + 2f * LayoutBudgets.RailLabelPadLogical);
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
        Assert.Equal(compact ? LayoutBudgets.CompactStationLogical : LayoutBudgets.StationLogical, fit.Station);
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
        Assert.Equal(compact ? LayoutBudgets.CompactStationMinLogical : LayoutBudgets.StationMinLogical, fit.Station);
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
        foreach (var compact in new[] { false, true })
        {
            for (var height = 150f; height <= 1400f; height += 0.75f)
            {
                var place = LayoutBudgets.PlaceRail(height, scale, 5, compact, button);
                if (!place.Fit.FootAnchored)
                {
                    continue;
                }

                var foot = LayoutBudgets.FootHeight(place.Fit.Percent, compact, place.Button / scale) * scale;
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
}
