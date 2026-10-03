namespace Tsukimichi.Core.Ui;

/// <summary>
/// The window font scale as one frame sees it (feature plan v6 U7): the UI scale and text size from the settings, and
/// whether the body font pushed around the windows is already built at that text size. The settings may be read again
/// within a frame (<see cref="SetScales"/>) without forgetting the font state, which only the typography's own per-frame
/// update sets (<see cref="SetTextFontBuilt"/>): forgetting it would scale a font already at the text size by the text
/// size again (130% drawn as 169%).
/// </summary>
public sealed class FontScaleState
{
    /// <summary>The UI scale, clamped.</summary>
    public float UiScale { get; private set; } = ScaleMetrics.DefaultUiScale;

    /// <summary>The text size, clamped and stepped.</summary>
    public float TextScale { get; private set; } = ScaleMetrics.DefaultTextScale;

    /// <summary>Whether the body font pushed this frame is the one built at the text size.</summary>
    public bool TextFontBuilt { get; private set; }

    /// <summary>What the windows set as their font scale (<see cref="ScaleMetrics.WindowFontScale"/>).</summary>
    public float WindowFontScale => ScaleMetrics.WindowFontScale(UiScale, TextScale, TextFontBuilt);

    /// <summary>The size text is drawn at relative to Dalamud's default font: the pushed font's factor × <see cref="WindowFontScale"/>.</summary>
    public float EffectiveTextScale => ScaleMetrics.PushedFontFactor(TextScale, TextFontBuilt) * WindowFontScale;

    /// <summary>Takes the scales from the settings; the font state stays as the typography last set it.</summary>
    public void SetScales(float uiScale, float textScale)
    {
        UiScale = ScaleMetrics.ClampUiScale(uiScale);
        TextScale = ScaleMetrics.ClampTextScale(textScale);
    }

    /// <summary>Whether the body font pushed this frame is built at the text size (the typography, once per frame).</summary>
    public void SetTextFontBuilt(bool built) => TextFontBuilt = built;
}
