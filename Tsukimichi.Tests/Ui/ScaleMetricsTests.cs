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
    public void Min_window_size_at_scale_one_is_the_side_columns_plus_the_centre_floor()
    {
        var size = ScaleMetrics.MinWindowSize(1f);
        Assert.Equal(240f + 360f + 200f, size.X);
        Assert.Equal(500f, size.Y);
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(1.15f)]
    [InlineData(1.6f)]
    public void Min_window_size_leaves_the_centre_column_its_floor_at_every_ui_scale(float uiScale)
    {
        var size = ScaleMetrics.MinWindowSize(uiScale);
        var columns = (ScaleMetrics.LeftColumnLogical + ScaleMetrics.RightColumnLogical) * uiScale;
        Assert.Equal(ScaleMetrics.CentreFloorLogical * uiScale, size.X - columns, 3);
        Assert.Equal(ScaleMetrics.MinWindowHeightLogical * uiScale, size.Y, 3);
    }

    [Fact]
    public void Min_window_size_clamps_the_ui_scale_like_everything_else()
    {
        Assert.Equal(ScaleMetrics.MinWindowSize(ScaleMetrics.MaxUiScale), ScaleMetrics.MinWindowSize(9f));
        Assert.Equal(ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale), ScaleMetrics.MinWindowSize(float.NaN));
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
}
