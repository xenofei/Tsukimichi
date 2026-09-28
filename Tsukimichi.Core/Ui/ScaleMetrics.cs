using System;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The pure arithmetic behind the main window's size system: the user's UI scale multiplies Dalamud's global scale
/// for every layout pixel and the font, and the icon scale multiplies that again for moons and icons. Lives in Core
/// so the clamping and the defaults are testable without ImGui; the plugin's <c>UiMetrics</c> reads the live values.
/// </summary>
public static class ScaleMetrics
{
    /// <summary>Default UI scale: text and layout about 15% larger than Dalamud's global scale alone.</summary>
    public const float DefaultUiScale = 1.15f;
    public const float MinUiScale = 0.9f;
    public const float MaxUiScale = 1.6f;

    /// <summary>Default icon scale on top of the UI scale, so glyphs and reward icons grow a little more than text.</summary>
    public const float DefaultIconScale = 1.25f;
    public const float MinIconScale = 0.8f;
    public const float MaxIconScale = 2.0f;

    /// <summary>The UI scale within its bounds; a non-finite value (a corrupt config) becomes the default.</summary>
    public static float ClampUiScale(float value) => Clamp(value, MinUiScale, MaxUiScale, DefaultUiScale);

    /// <summary>The icon scale within its bounds; a non-finite value becomes the default.</summary>
    public static float ClampIconScale(float value) => Clamp(value, MinIconScale, MaxIconScale, DefaultIconScale);

    /// <summary>Global scale as reported by the host, guarded: non-finite or non-positive values count as 1.</summary>
    public static float SafeGlobalScale(float globalScale) => float.IsFinite(globalScale) && globalScale > 0f ? globalScale : 1f;

    /// <summary>Pixels per logical unit for layout: global scale × clamped UI scale.</summary>
    public static float LayoutFactor(float globalScale, float uiScale) => SafeGlobalScale(globalScale) * ClampUiScale(uiScale);

    /// <summary>Pixels per logical unit for moons and icons: the layout factor × clamped icon scale.</summary>
    public static float IconFactor(float globalScale, float uiScale, float iconScale) => LayoutFactor(globalScale, uiScale) * ClampIconScale(iconScale);

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
