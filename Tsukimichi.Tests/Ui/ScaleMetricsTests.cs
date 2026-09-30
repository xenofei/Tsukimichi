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
    public void Min_window_size_at_scale_one_is_the_rail_the_side_columns_and_the_centre_floor()
    {
        var size = ScaleMetrics.MinWindowSize(1f);
        Assert.Equal(136f + 240f + 360f + 200f, size.X);
        Assert.Equal(500f, size.Y);
    }

    [Theory]
    [InlineData(1f, 200f)]
    [InlineData(1.6f, 170f)]
    public void A_rail_widened_for_a_translation_widens_the_min_window_instead_of_the_centre_losing_it(float uiScale, float rail)
    {
        var size = ScaleMetrics.MinWindowSize(uiScale, rail);
        var columns = (rail + ScaleMetrics.LeftColumnLogical + ScaleMetrics.RightColumnLogical) * uiScale;
        Assert.Equal(ScaleMetrics.CentreFloorLogical * uiScale, size.X - columns, 3);
        Assert.Equal(ScaleMetrics.MinWindowSize(uiScale).X + (rail - ScaleMetrics.RailLogical) * uiScale, size.X, 3);
    }

    [Theory]
    [InlineData(100f)]
    [InlineData(float.NaN)]
    public void A_rail_under_the_default_or_unknown_keeps_the_default_min_window(float rail)
    {
        Assert.Equal(ScaleMetrics.MinWindowSize(1f), ScaleMetrics.MinWindowSize(1f, rail));
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1.15f)]
    [InlineData(1.6f)]
    public void Min_window_size_leaves_the_centre_column_its_floor_at_every_ui_scale(float uiScale)
    {
        var size = ScaleMetrics.MinWindowSize(uiScale);
        var columns = (ScaleMetrics.RailLogical + ScaleMetrics.LeftColumnLogical + ScaleMetrics.RightColumnLogical) * uiScale;
        Assert.Equal(ScaleMetrics.CentreFloorLogical * uiScale, size.X - columns, 3);
        Assert.Equal(ScaleMetrics.MinWindowHeightLogical * uiScale, size.Y, 3);
    }

    [Fact]
    public void Min_window_size_clamps_the_ui_scale_like_everything_else()
    {
        Assert.Equal(ScaleMetrics.MinWindowSize(ScaleMetrics.MaxUiScale), ScaleMetrics.MinWindowSize(9f));
        Assert.Equal(ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale), ScaleMetrics.MinWindowSize(float.NaN));
    }

    private static readonly Vector2 Screen1080 = new(1920f, 1080f);

    [Fact]
    public void Default_window_at_the_default_ui_scale_is_1100_by_700()
    {
        var size = ScaleMetrics.DefaultWindowSize(ScaleMetrics.DefaultUiScale, 1f, Screen1080);
        Assert.Equal(1100f, size.X, 3);
        Assert.Equal(700f, size.Y, 3);
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
        Assert.Equal(new Vector2(1100f, 700f), size);
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
}
