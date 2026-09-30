using System;
using System.Numerics;

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

    /// <summary>Logical width of the main window's fixed navigation column.</summary>
    public const float LeftColumnLogical = 240f;

    /// <summary>Logical width of the main window's fixed detail column.</summary>
    public const float RightColumnLogical = 360f;

    /// <summary>The least logical width the centre column (the quest table) may be squeezed to.</summary>
    public const float CentreFloorLogical = 200f;

    /// <summary>Logical height floor of the main window.</summary>
    public const float MinWindowHeightLogical = 500f;

    /// <summary>
    /// The main window's minimum size in Dalamud-scaled units for a UI scale: the two fixed side columns plus the
    /// centre floor, and the height floor, each multiplied by the clamped UI scale. Dalamud multiplies the result by
    /// its global scale, so the window can never shrink below what the fixed columns need.
    /// </summary>
    public static Vector2 MinWindowSize(float uiScale)
    {
        var scale = ClampUiScale(uiScale);
        return new Vector2((LeftColumnLogical + RightColumnLogical + CentreFloorLogical) * scale, MinWindowHeightLogical * scale);
    }

    /// <summary>Smallest halo half-size in the Journal tree: a 24 px box (accessibility A4), whatever the icon scale.</summary>
    public const float TreeGlyphMinRadius = 12f;

    /// <summary>Largest halo half-size in the Journal tree.</summary>
    public const float TreeGlyphMaxRadius = 18f;

    /// <summary>Smallest Journal tree row in pixels, in every density.</summary>
    public const float TreeRowMinHeight = 30f;

    /// <summary>Logical padding around the tree halo inside its row.</summary>
    public const float TreeRowPaddingLogical = 6f;

    /// <summary>
    /// The tree halo's half-size for a text line of <paramref name="lineHeight"/> px: clamp(0.5 · L · icon scale, 12, 18)
    /// (glyph proposal §3.5). The line already carries the global and UI scales, so only the user's icon scale is applied.
    /// </summary>
    public static float TreeGlyphRadius(float lineHeight, float iconScale)
    {
        var line = float.IsFinite(lineHeight) && lineHeight > 0f ? lineHeight : 0f;
        return Math.Clamp(0.5f * line * ClampIconScale(iconScale), TreeGlyphMinRadius, TreeGlyphMaxRadius);
    }

    /// <summary>A tree row's height: max(30 px, the line, the halo box plus 6 logical px).</summary>
    public static float TreeRowHeight(float lineHeight, float glyphRadius, float layoutScale)
    {
        var line = float.IsFinite(lineHeight) ? lineHeight : 0f;
        var scale = float.IsFinite(layoutScale) && layoutScale > 0f ? layoutScale : 1f;
        return MathF.Max(TreeRowMinHeight, MathF.Max(line, 2f * glyphRadius + TreeRowPaddingLogical * scale));
    }

    /// <summary>
    /// Texture coordinates that show a <paramref name="textureWidth"/> × <paramref name="textureHeight"/> image in a
    /// <paramref name="boxWidth"/> × <paramref name="boxHeight"/> box at the image's own aspect, cropping the excess
    /// evenly at both ends (an object-fit "cover"). Non-positive sizes yield the full image.
    /// </summary>
    public static (Vector2 Uv0, Vector2 Uv1) CenterCropUv(float boxWidth, float boxHeight, float textureWidth, float textureHeight)
    {
        if (!(boxWidth > 0f) || !(boxHeight > 0f) || !(textureWidth > 0f) || !(textureHeight > 0f))
        {
            return (Vector2.Zero, Vector2.One);
        }

        var boxAspect = boxWidth / boxHeight;
        var textureAspect = textureWidth / textureHeight;
        if (textureAspect > boxAspect)
        {
            // Image wider than the box: keep the full height, trim the sides.
            var visible = boxAspect / textureAspect;
            var trim = (1f - visible) * 0.5f;
            return (new Vector2(trim, 0f), new Vector2(1f - trim, 1f));
        }

        if (textureAspect < boxAspect)
        {
            // Image taller than the box: keep the full width, trim top and bottom.
            var visible = textureAspect / boxAspect;
            var trim = (1f - visible) * 0.5f;
            return (new Vector2(0f, trim), new Vector2(1f, 1f - trim));
        }

        return (Vector2.Zero, Vector2.One);
    }

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
