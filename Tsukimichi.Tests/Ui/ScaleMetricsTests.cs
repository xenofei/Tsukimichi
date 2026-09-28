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
}
