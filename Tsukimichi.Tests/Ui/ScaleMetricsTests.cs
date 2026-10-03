using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class ScaleMetricsTests
{
    /// <summary>Row glyph radius and reward icon size before the scale system, at global scale 1 (17 px line height).</summary>
    private const float OldRowGlyphRadius = 17f * 0.42f;
    private const float OldRowIconSize = 17f;

    // The logical sizes UiMetrics multiplies by the icon factor.
    private const float RowGlyphLogical = 6f;
    private const float RowIconLogical = 14f;

    [Fact]
    public void Defaults_lie_inside_their_ranges()
    {
        Assert.InRange(ScaleMetrics.DefaultUiScale, ScaleMetrics.MinUiScale, ScaleMetrics.MaxUiScale);
        Assert.InRange(ScaleMetrics.DefaultIconScale, ScaleMetrics.MinIconScale, ScaleMetrics.MaxIconScale);
        Assert.Equal(ScaleMetrics.DefaultUiScale, ScaleMetrics.ClampUiScale(ScaleMetrics.DefaultUiScale));
        Assert.Equal(ScaleMetrics.DefaultIconScale, ScaleMetrics.ClampIconScale(ScaleMetrics.DefaultIconScale));
    }

    [Theory]
    [InlineData(0f, ScaleMetrics.MinUiScale)]
    [InlineData(0.9f, 0.9f)]
    [InlineData(1.3f, 1.3f)]
    [InlineData(1.6f, 1.6f)]
    [InlineData(9f, ScaleMetrics.MaxUiScale)]
    [InlineData(float.NaN, ScaleMetrics.DefaultUiScale)]
    [InlineData(float.PositiveInfinity, ScaleMetrics.DefaultUiScale)]
    public void Ui_scale_is_clamped_and_non_finite_values_fall_back(float value, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.ClampUiScale(value));
    }

    [Theory]
    [InlineData(0f, ScaleMetrics.MinIconScale)]
    [InlineData(0.8f, 0.8f)]
    [InlineData(1.5f, 1.5f)]
    [InlineData(2f, 2f)]
    [InlineData(5f, ScaleMetrics.MaxIconScale)]
    [InlineData(float.NaN, ScaleMetrics.DefaultIconScale)]
    [InlineData(float.NegativeInfinity, ScaleMetrics.DefaultIconScale)]
    public void Icon_scale_is_clamped_and_non_finite_values_fall_back(float value, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.ClampIconScale(value));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void Bad_global_scale_counts_as_one(float globalScale)
    {
        Assert.Equal(1f, ScaleMetrics.SafeGlobalScale(globalScale));
        Assert.Equal(ScaleMetrics.ClampUiScale(1.2f), ScaleMetrics.LayoutFactor(globalScale, 1.2f));
    }

    [Fact]
    public void Layout_factor_multiplies_global_and_ui_scale()
    {
        Assert.Equal(2f * 1.25f, ScaleMetrics.LayoutFactor(2f, 1.25f), 5);
        Assert.Equal(1.5f * ScaleMetrics.MaxUiScale, ScaleMetrics.LayoutFactor(1.5f, 3f), 5);
    }

    [Fact]
    public void Icon_factor_multiplies_the_layout_factor_by_the_icon_scale()
    {
        var layout = ScaleMetrics.LayoutFactor(1f, 1.15f);
        Assert.Equal(layout * 1.25f, ScaleMetrics.IconFactor(1f, 1.15f, 1.25f), 5);
        Assert.Equal(layout, ScaleMetrics.IconFactor(1f, 1.15f, 1f), 5);
    }

    [Fact]
    public void Factors_grow_monotonically_with_each_slider()
    {
        var previous = 0f;
        for (var ui = ScaleMetrics.MinUiScale; ui <= ScaleMetrics.MaxUiScale + 0.001f; ui += 0.05f)
        {
            var factor = ScaleMetrics.LayoutFactor(1f, ui);
            Assert.True(factor > previous, $"layout factor at {ui} did not grow");
            previous = factor;
        }

        previous = 0f;
        for (var icon = ScaleMetrics.MinIconScale; icon <= ScaleMetrics.MaxIconScale + 0.001f; icon += 0.05f)
        {
            var factor = ScaleMetrics.IconFactor(1f, ScaleMetrics.DefaultUiScale, icon);
            Assert.True(factor > previous, $"icon factor at {icon} did not grow");
            previous = factor;
        }
    }

    [Fact]
    public void Defaults_make_glyphs_and_icons_15_to_35_percent_larger_than_before()
    {
        var icon = ScaleMetrics.IconFactor(1f, ScaleMetrics.DefaultUiScale, ScaleMetrics.DefaultIconScale);
        Assert.InRange(RowGlyphLogical * icon / OldRowGlyphRadius, 1.15f, 1.35f);
        Assert.InRange(RowIconLogical * icon / OldRowIconSize, 1.15f, 1.35f);
    }

    [Fact]
    public void Min_window_size_at_scale_one_is_the_rail_the_pane_floors_the_gutters_and_the_padding()
    {
        var size = ScaleMetrics.MinWindowSize(1f);
        Assert.Equal(70f + 180f + 320f + 260f + (3f * 6f) + 16f + PaneLayout.RoundingReservePx, size.X);
        Assert.Equal(500f, size.Y);
    }

    [Fact]
    public void Min_window_size_at_the_default_ui_scale_is_narrower_than_before_the_splitter()
    {
        // 1,076 before L1 (rail 136 + columns 240 and 360 + a 200 centre floor, at 1.15); the splitter's floors and
        // gutters beside the 70 px rail of plan v7 UI-4 need about 991 (995 with the whole-pixel reserve), and about 965
        // while the rail is compact.
        var size = ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale);
        Assert.InRange(size.X, 985f, 1000f);
        Assert.InRange(ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale, ScaleMetrics.RailCompactLogical).X, 955f, 970f);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.15f)]
    [InlineData(1.3f)]
    [InlineData(1.6f)]
    public void The_compact_rail_lets_the_window_narrow_by_what_it_gave_up(float uiScale)
    {
        var size = ScaleMetrics.MinWindowSize(uiScale, ScaleMetrics.RailCompactLogical);
        var fixedPart = ((ScaleMetrics.RailCompactLogical + PaneLayout.FloorsLogical + (PaneLayout.GutterCount * PaneLayout.GutterLogical)) * uiScale) + ScaleMetrics.WindowPaddingX + PaneLayout.RoundingReservePx;
        Assert.Equal(fixedPart, size.X, 3);
        Assert.Equal(ScaleMetrics.MinWindowSize(uiScale).X - ((ScaleMetrics.RailLogical - ScaleMetrics.RailCompactLogical) * uiScale), size.X, 3);
    }

    [Theory]
    [InlineData(100f, 70f)]
    [InlineData(10f, 44f)]
    [InlineData(float.NaN, 70f)]
    public void A_rail_width_outside_the_two_modes_is_taken_as_the_nearer_one(float rail, float taken)
    {
        Assert.Equal(ScaleMetrics.MinWindowSize(1f, taken), ScaleMetrics.MinWindowSize(1f, rail));
    }

    [Theory]
    [InlineData(0.9f, ScaleMetrics.RailLogical)]
    [InlineData(1.15f, ScaleMetrics.RailLogical)]
    [InlineData(1.6f, ScaleMetrics.RailLogical)]
    [InlineData(0.9f, ScaleMetrics.RailCompactLogical)]
    [InlineData(1.15f, ScaleMetrics.RailCompactLogical)]
    [InlineData(1.6f, ScaleMetrics.RailCompactLogical)]
    public void Min_window_size_leaves_every_pane_its_floor_at_every_ui_scale(float uiScale, float rail)
    {
        var size = ScaleMetrics.MinWindowSize(uiScale, rail);
        var widths = PaneLayout.Solve(size.X - ScaleMetrics.WindowPaddingX, rail * uiScale, PaneLayout.TreeDefaultLogical, PaneLayout.DetailDefaultLogical, uiScale);
        Assert.False(widths.TreeStrip);
        // The tree gives way last of the sides, so it keeps what the rounding reserve leaves over.
        Assert.InRange(widths.Tree, (PaneLayout.TreeFloorLogical * uiScale) - 0.01f, (PaneLayout.TreeFloorLogical * uiScale) + PaneLayout.RoundingReservePx + 1f);
        Assert.InRange(widths.Detail, (PaneLayout.DetailFloorLogical * uiScale) - 0.01f, (PaneLayout.DetailFloorLogical * uiScale) + 1f);
        Assert.True(widths.Centre >= (PaneLayout.CentreFloorLogical * uiScale) - 0.01f, $"centre {widths.Centre}");
        Assert.Equal(ScaleMetrics.MinWindowHeightLogical * uiScale, size.Y, 3);
    }

    [Theory]
    [InlineData(1.15f, 0.75f)]
    [InlineData(1.3f, 1.25f)]
    [InlineData(1.6f, 2f)]
    public void Min_window_size_leaves_every_pane_its_floor_at_every_global_scale(float uiScale, float globalScale)
    {
        // Dalamud multiplies the minimum by its global scale; the rounding reserve is pixels at that scale.
        var huge = new Vector2(float.NaN, float.NaN);
        var size = ScaleMetrics.MinWindowSize(uiScale, globalScale, huge) * globalScale;
        var s = uiScale * globalScale;
        var widths = PaneLayout.Solve(size.X - (ScaleMetrics.WindowPaddingX * globalScale), ScaleMetrics.RailLogical * s, 180f, 260f, s);
        Assert.True(widths.Tree >= (PaneLayout.TreeFloorLogical * s) - 0.01f, $"tree {widths.Tree}");
        Assert.True(widths.Detail >= (PaneLayout.DetailFloorLogical * s) - 0.01f, $"detail {widths.Detail}");
        Assert.True(widths.Centre >= (PaneLayout.CentreFloorLogical * s) - 0.01f, $"centre {widths.Centre}");
    }

    [Fact]
    public void Min_window_size_clamps_the_ui_scale_like_everything_else()
    {
        Assert.Equal(ScaleMetrics.MinWindowSize(ScaleMetrics.MaxUiScale), ScaleMetrics.MinWindowSize(9f));
        Assert.Equal(ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale), ScaleMetrics.MinWindowSize(float.NaN));
    }

    private static readonly Vector2 Screen1080 = new(1920f, 1080f);

    [Fact]
    public void Default_window_at_the_default_ui_scale_is_1320_by_760()
    {
        var size = ScaleMetrics.DefaultWindowSize(ScaleMetrics.DefaultUiScale, 1f, Screen1080);
        Assert.Equal(1320f, size.X, 3);
        Assert.Equal(760f, size.Y, 3);
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1.15f)]
    [InlineData(1.3f)]
    public void Default_window_holds_the_default_panes_with_room_for_the_quest_list(float uiScale)
    {
        // Rail, tree and detail at their defaults plus the gutters, and the quest list at least 54 logical over its floor
        // (60 before the rail grew from 64 to 70 in plan v7 UI-4).
        var size = ScaleMetrics.DefaultWindowSize(uiScale, 1f, new Vector2(3840f, 2160f));
        var needed = ((ScaleMetrics.RailLogical + PaneLayout.TreeDefaultLogical + PaneLayout.CentreFloorLogical + 54f
            + PaneLayout.DetailDefaultLogical + (PaneLayout.GutterCount * PaneLayout.GutterLogical)) * uiScale)
            + ScaleMetrics.WindowPaddingX + PaneLayout.RoundingReservePx;
        Assert.True(size.X >= needed, $"default width {size.X} under {needed}");
    }

    [Theory]
    [InlineData(0.9f, 1f)]
    [InlineData(1.15f, 1f)]
    [InlineData(1.6f, 1f)]
    [InlineData(1.6f, 1.25f)]
    public void Default_window_fits_a_1080p_viewport_with_its_margin(float uiScale, float global)
    {
        var size = ScaleMetrics.DefaultWindowSize(uiScale, global, Screen1080) * global;
        Assert.True(size.X <= 1920f - 2f * ScaleMetrics.ViewportMarginPx + 0.01f, $"width {size.X}");
        Assert.True(size.Y <= 1080f - 2f * ScaleMetrics.ViewportMarginPx + 0.01f, $"height {size.Y}");
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1.15f)]
    [InlineData(1.6f)]
    public void Default_window_is_never_under_the_minimum_when_the_screen_has_room(float uiScale)
    {
        var size = ScaleMetrics.DefaultWindowSize(uiScale, 1f, new Vector2(3840f, 2160f));
        var min = ScaleMetrics.MinWindowSize(uiScale);
        Assert.True(size.X >= min.X && size.Y >= min.Y);
    }

    [Fact]
    public void Default_window_grows_with_the_ui_scale()
    {
        var big = new Vector2(3840f, 2160f);
        Assert.True(ScaleMetrics.DefaultWindowSize(1.6f, 1f, big).X > ScaleMetrics.DefaultWindowSize(1.15f, 1f, big).X);
    }

    [Fact]
    public void Default_window_ignores_an_unknown_viewport()
    {
        var size = ScaleMetrics.DefaultWindowSize(ScaleMetrics.DefaultUiScale, 1f, new Vector2(float.NaN, 0f));
        Assert.Equal(new Vector2(1320f, 760f), size);
    }

    [Fact]
    public void Min_window_size_is_clamped_to_a_small_viewport()
    {
        var small = new Vector2(1280f, 720f);
        var size = ScaleMetrics.MinWindowSize(ScaleMetrics.MaxUiScale, 1f, small);
        Assert.Equal(1280f - 2f * ScaleMetrics.ViewportMarginPx, size.X, 3);
        Assert.Equal(720f - 2f * ScaleMetrics.ViewportMarginPx, size.Y, 3);
        Assert.Equal(ScaleMetrics.MinWindowSize(1f), ScaleMetrics.MinWindowSize(1f, 1f, new Vector2(3840f, 2160f)));
    }

    [Fact]
    public void Center_crop_of_a_matching_aspect_shows_the_whole_image()
    {
        var (uv0, uv1) = ScaleMetrics.CenterCropUv(400f, 200f, 800f, 400f);
        Assert.Equal(Vector2.Zero, uv0);
        Assert.Equal(Vector2.One, uv1);
    }

    [Fact]
    public void Center_crop_of_a_taller_image_trims_top_and_bottom_evenly()
    {
        // A 2:1 image in a 4:1 box shows the middle half of its height.
        var (uv0, uv1) = ScaleMetrics.CenterCropUv(400f, 100f, 800f, 400f);
        Assert.Equal(0f, uv0.X);
        Assert.Equal(1f, uv1.X);
        Assert.Equal(0.25f, uv0.Y, 5);
        Assert.Equal(0.75f, uv1.Y, 5);
    }

    [Fact]
    public void Center_crop_of_a_wider_image_trims_the_sides_evenly()
    {
        // A 4:1 image in a 2:1 box shows the middle half of its width.
        var (uv0, uv1) = ScaleMetrics.CenterCropUv(400f, 200f, 800f, 200f);
        Assert.Equal(0.25f, uv0.X, 5);
        Assert.Equal(0.75f, uv1.X, 5);
        Assert.Equal(0f, uv0.Y);
        Assert.Equal(1f, uv1.Y);
    }

    [Fact]
    public void Center_crop_keeps_the_aspect_for_the_banner_case()
    {
        // The right column at Px(344) with the Px(200) height clamp on a 1024x384 journal banner.
        var (uv0, uv1) = ScaleMetrics.CenterCropUv(344f, 200f, 1024f, 384f);
        var shownWidth = (uv1.X - uv0.X) * 1024f;
        var shownHeight = (uv1.Y - uv0.Y) * 384f;
        Assert.Equal(344f / 200f, shownWidth / shownHeight, 3);
        Assert.Equal(uv0.X, 1f - uv1.X, 5);
        Assert.Equal(uv0.Y, 1f - uv1.Y, 5);
    }

    [Theory]
    [InlineData(0f, 100f, 100f, 100f)]
    [InlineData(100f, 100f, 0f, 100f)]
    [InlineData(100f, 100f, 100f, float.NaN)]
    public void Center_crop_with_a_degenerate_size_shows_the_whole_image(float boxW, float boxH, float texW, float texH)
    {
        var (uv0, uv1) = ScaleMetrics.CenterCropUv(boxW, boxH, texW, texH);
        Assert.Equal(Vector2.Zero, uv0);
        Assert.Equal(Vector2.One, uv1);
    }

    // imgui-notes §4 table: (UiScale, IconScale, L) → halo R and row height (layout scale = UiScale at global 1).
    [Theory]
    [InlineData(1.00f, 0.80f, 16.0f, 12.0f, 30f)]
    [InlineData(1.00f, 1.00f, 16.0f, 12.0f, 30f)]
    [InlineData(1.15f, 1.00f, 18.4f, 12.0f, 30.9f)]
    [InlineData(1.15f, 1.25f, 18.4f, 12.0f, 30.9f)]
    [InlineData(1.15f, 1.60f, 18.4f, 14.72f, 36.34f)]
    [InlineData(1.40f, 1.25f, 22.4f, 14.0f, 36.4f)]
    [InlineData(1.60f, 2.00f, 25.6f, 18.0f, 45.6f)]
    public void Tree_glyph_is_never_under_a_24_px_box_and_rows_never_under_30_px(float uiScale, float iconScale, float line, float radius, float row)
    {
        var r = ScaleMetrics.TreeGlyphRadius(line, iconScale);
        Assert.Equal(radius, r, 2);
        Assert.Equal(row, ScaleMetrics.TreeRowHeight(line, r, uiScale), 1);
    }

    [Theory]
    [InlineData(RowDensity.Dense, 1f, 20.7f, 2f, 20.7f)]        // content already fills 24 px with padding
    [InlineData(RowDensity.Dense, 1f, 16f, 2f, 20f)]
    [InlineData(RowDensity.Comfortable, 1f, 20.7f, 2f, 28f)]
    [InlineData(RowDensity.Comfortable, 2f, 20.7f, 4f, 56f)]    // 4K: the host's global scale doubles the target
    [InlineData(RowDensity.Comfortable, 1f, 40f, 2f, 40f)]       // UiScale 1.6 + IconScale 2: content wins
    [InlineData((RowDensity)7, 1f, 16f, 2f, 28f)]                // an unknown value reads as Comfortable
    [InlineData(RowDensity.Dense, 0.8f, 12f, 2f, 20f)]           // a global scale under 1 keeps the 24 px row (B4)
    [InlineData(RowDensity.Comfortable, 0.5f, 12f, 2f, 20f)]
    public void Table_rows_follow_the_density_but_never_clip_their_content(RowDensity density, float global, float content, float padding, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.TableRowContent(density, global, content, padding), 3);
    }

    [Fact]
    public void Comfortable_is_the_default_density()
    {
        Assert.Equal(RowDensity.Comfortable, default(RowDensity));
        Assert.Equal(24f, ScaleMetrics.TableRowTarget(RowDensity.Dense));
        Assert.Equal(32f, ScaleMetrics.TableRowTarget(RowDensity.Comfortable));
    }

    [Fact]
    public void Tree_glyph_guards_bad_input()
    {
        Assert.Equal(ScaleMetrics.TreeGlyphMinRadius, ScaleMetrics.TreeGlyphRadius(float.NaN, 1.25f));
        Assert.Equal(ScaleMetrics.TreeRowMinHeight, ScaleMetrics.TreeRowHeight(float.NaN, 12f, float.NaN) - 0f, 1);
    }

    [Fact]
    public void A_size_given_from_a_position_fits_a_large_screen_unchanged()
    {
        var size = ScaleMetrics.FitFromPosition(new Vector2(880f, 720f), 1f, new Vector2(100f, 100f), Vector2.Zero, new Vector2(2560f, 1400f));

        Assert.Equal(new Vector2(880f, 720f), size);
    }

    [Fact]
    public void A_size_given_from_a_position_stops_at_the_work_area_counting_the_global_scale()
    {
        // 1280 × 720 work area at global scale 1.25, window at (400, 100): 880 px of room across, 620 down, in scaled units.
        var size = ScaleMetrics.FitFromPosition(new Vector2(880f, 720f), 1.25f, new Vector2(400f, 100f), Vector2.Zero, new Vector2(1280f, 720f));

        Assert.Equal(880f / 1.25f, size.X, 3);
        Assert.Equal(620f / 1.25f, size.Y, 3);
    }

    [Fact]
    public void A_window_off_the_work_area_counts_from_its_edge_and_bad_input_keeps_the_size()
    {
        var offLeft = ScaleMetrics.FitFromPosition(new Vector2(880f, 720f), 1f, new Vector2(-200f, 40f), new Vector2(0f, 40f), new Vector2(800f, 600f));
        Assert.Equal(new Vector2(800f, 600f), offLeft);

        var past = ScaleMetrics.FitFromPosition(new Vector2(880f, 720f), 1f, new Vector2(5000f, 5000f), Vector2.Zero, new Vector2(800f, 600f));
        Assert.Equal(Vector2.Zero, past);

        var unknown = ScaleMetrics.FitFromPosition(new Vector2(880f, 720f), 1f, Vector2.Zero, Vector2.Zero, new Vector2(float.NaN, 0f));
        Assert.Equal(new Vector2(880f, 720f), unknown);
    }

    [Theory]
    [InlineData(Flair.Full, 30f)]
    [InlineData(Flair.Quiet, 28f)]
    [InlineData(Flair.Plain, 22f)]
    [InlineData((Flair)99, 30f)]
    public void The_table_header_has_a_floor_per_level(Flair flair, float height)
    {
        Assert.Equal(height, ScaleMetrics.TableHeaderMin(flair));
    }

    [Fact]
    public void The_table_header_centres_its_label_in_the_row()
    {
        // A 25 px label in a 30 px row: 2 px over it, 3 under (ImGui rounds the cell down), never under the padding.
        Assert.Equal((30f, 2f), ScaleMetrics.TableHeaderRow(30f, 25f, 2f));
        Assert.Equal((28f, 5f), ScaleMetrics.TableHeaderRow(28f, 18f, 2f));

        // A label taller than the floor makes the row taller, with the normal padding.
        Assert.Equal((42f, 2f), ScaleMetrics.TableHeaderRow(30f, 38f, 2f));
        Assert.Equal((4f, 2f), ScaleMetrics.TableHeaderRow(float.NaN, float.NaN, 2f));
    }
}
