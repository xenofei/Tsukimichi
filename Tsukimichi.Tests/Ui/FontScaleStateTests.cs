using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class FontScaleStateTests
{
    [Theory]
    [InlineData(1f, 1.3f, false)]
    [InlineData(1f, 1.3f, true)]
    [InlineData(1.3f, 0.8f, false)]
    [InlineData(1.3f, 0.8f, true)]
    [InlineData(0.9f, 1.5f, true)]
    public void Pushed_font_times_window_scale_counts_the_text_size_once(float uiScale, float textScale, bool built)
    {
        var effective = ScaleMetrics.PushedFontFactor(textScale, built) * ScaleMetrics.WindowFontScale(uiScale, textScale, built);

        Assert.Equal(uiScale * textScale, effective, 4);
    }

    [Fact]
    public void Window_scale_is_the_ui_scale_alone_once_the_text_font_is_built()
    {
        Assert.Equal(1.15f, ScaleMetrics.WindowFontScale(1.15f, 1.3f, textFontBuilt: true), 4);
        Assert.Equal(1.15f * 1.3f, ScaleMetrics.WindowFontScale(1.15f, 1.3f, textFontBuilt: false), 4);
    }

    [Fact]
    public void Reading_the_settings_again_within_a_frame_keeps_the_built_font()
    {
        // The frame: the settings are read, the typography reports its font built at 130%, then a window reads the
        // settings again before drawing (what MainWindow.Draw did). 130% must stay 130%, not 169%.
        var state = new FontScaleState();
        state.SetScales(1f, 1.3f);
        state.SetTextFontBuilt(true);

        state.SetScales(1f, 1.3f);

        Assert.True(state.TextFontBuilt);
        Assert.Equal(1f, state.WindowFontScale, 4);
        Assert.Equal(1.3f, state.EffectiveTextScale, 4);
    }

    [Fact]
    public void Before_the_font_is_built_the_window_scale_carries_the_text_size()
    {
        var state = new FontScaleState();
        state.SetScales(1.3f, 1.2f);

        Assert.False(state.TextFontBuilt);
        Assert.Equal(1.3f * 1.2f, state.WindowFontScale, 4);
        Assert.Equal(1.3f * 1.2f, state.EffectiveTextScale, 4);

        state.SetTextFontBuilt(true);
        Assert.Equal(1.3f, state.WindowFontScale, 4);
        Assert.Equal(1.3f * 1.2f, state.EffectiveTextScale, 4);
    }

    [Fact]
    public void Scales_are_clamped()
    {
        var state = new FontScaleState();
        state.SetScales(float.NaN, 9f);

        Assert.Equal(ScaleMetrics.DefaultUiScale, state.UiScale);
        Assert.Equal(ScaleMetrics.MaxTextScale, state.TextScale);
    }
}
