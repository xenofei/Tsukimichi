using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>Settings › General › Text size (feature plan v6 U7): 80–150% in 10% steps, 100% by default.</summary>
public class TextScaleTests
{
    [Fact]
    public void The_default_is_100_percent_inside_the_range()
    {
        Assert.Equal(1f, ScaleMetrics.DefaultTextScale);
        Assert.InRange(ScaleMetrics.DefaultTextScale, ScaleMetrics.MinTextScale, ScaleMetrics.MaxTextScale);
        Assert.Equal(100, ScaleMetrics.TextScalePercent(ScaleMetrics.DefaultTextScale));
    }

    [Theory]
    [InlineData(0.8f, 80)]
    [InlineData(0.9f, 90)]
    [InlineData(1.0f, 100)]
    [InlineData(1.1f, 110)]
    [InlineData(1.2f, 120)]
    [InlineData(1.3f, 130)]
    [InlineData(1.4f, 140)]
    [InlineData(1.5f, 150)]
    public void Every_step_is_kept(float value, int percent)
    {
        Assert.Equal(percent, ScaleMetrics.TextScalePercent(value));
        Assert.Equal(value, ScaleMetrics.ClampTextScale(value), 4);
    }

    [Theory]
    [InlineData(1.04f, 1.0f)]
    [InlineData(1.06f, 1.1f)]
    [InlineData(1.149f, 1.1f)]
    [InlineData(1.15f, 1.2f)]
    [InlineData(1.33f, 1.3f)]
    public void A_value_between_steps_snaps_to_the_nearest(float value, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.ClampTextScale(value), 4);
    }

    [Theory]
    [InlineData(0.1f, 0.8f)]
    [InlineData(0.79f, 0.8f)]
    [InlineData(-3f, 0.8f)]
    [InlineData(1.51f, 1.5f)]
    [InlineData(9f, 1.5f)]
    public void Values_outside_the_range_are_clamped(float value, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.ClampTextScale(value), 4);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void A_corrupt_value_reads_as_the_default(float value)
    {
        Assert.Equal(ScaleMetrics.DefaultTextScale, ScaleMetrics.ClampTextScale(value));
    }

    [Theory]
    [InlineData(80, 0.8f)]
    [InlineData(125, 1.3f)]
    [InlineData(150, 1.5f)]
    [InlineData(10, 0.8f)]
    [InlineData(400, 1.5f)]
    public void A_percentage_comes_back_clamped_and_stepped(int percent, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.TextScaleFromPercent(percent), 4);
    }

    [Fact]
    public void Font_sizes_are_whole_pixels_and_need_a_known_base()
    {
        Assert.Equal(17f, ScaleMetrics.TextFontPx(17f, 1f));
        Assert.Equal(26f, ScaleMetrics.TextFontPx(17f, 1.5f));
        Assert.Equal(14f, ScaleMetrics.TextFontPx(17f, 0.8f));
        Assert.Equal(0f, ScaleMetrics.TextFontPx(0f, 1.2f));
        Assert.Equal(0f, ScaleMetrics.TextFontPx(float.NaN, 1.2f));
    }

    [Fact]
    public void A_live_change_is_saved_once_it_is_still_and_let_go()
    {
        var save = new SaveDebounce();
        Assert.False(save.Due(10.0, holding: false), "nothing changed");
        save.Changed(10.0);
        Assert.True(save.Pending);
        Assert.False(save.Due(10.2, holding: false), "still moving");
        save.Changed(10.3);
        Assert.False(save.Due(10.8, holding: false), "the wait restarts with each change");
        Assert.False(save.Due(11.0, holding: true), "a held slider waits");
        Assert.True(save.Due(11.0, holding: false));
        Assert.False(save.Due(12.0, holding: false), "saved once");
    }

    [Fact]
    public void A_clock_that_went_back_saves_rather_than_waiting_forever()
    {
        var save = new SaveDebounce();
        save.Changed(100.0);
        Assert.True(save.Due(1.0, holding: false));
    }

    [Fact]
    public void Closing_flushes_a_waiting_change()
    {
        var save = new SaveDebounce();
        Assert.False(save.Flush());
        save.Changed(5.0);
        Assert.True(save.Flush());
        Assert.False(save.Pending);
    }
}
