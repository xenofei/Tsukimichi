using System;
using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>Quest table row density (Settings › Display). Only the table's rows change; the tree keeps its 30 px floor.</summary>
public enum RowDensity
{
    /// <summary>32 px rows, the default.</summary>
    Comfortable = 0,

    /// <summary>24 px rows.</summary>
    Dense = 1,
}

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

    /// <summary>
    /// Logical width of the main window's tab rail (T14): its own fixed column left of the navigation column, so the
    /// tree keeps <see cref="LeftColumnLogical"/>. Holds a 16 px icon, the longest tab label and the Journal badge.
    /// </summary>
    public const float RailLogical = 136f;

    /// <summary>Logical height floor of the main window.</summary>
    public const float MinWindowHeightLogical = 500f;

    /// <summary>The main window's default size in logical units at <see cref="DefaultUiScale"/>.</summary>
    public static readonly Vector2 DefaultWindowLogical = new(1100f, 700f);

    /// <summary>Pixels the default window leaves free on every side of the viewport (accessibility B6).</summary>
    public const float ViewportMarginPx = 48f;

    /// <summary>
    /// The main window's minimum size in Dalamud-scaled units for a UI scale: the tab rail, the two fixed side columns
    /// and the centre floor, and the height floor, each multiplied by the clamped UI scale. Dalamud multiplies the
    /// result by its global scale, so the window can never shrink below what the fixed columns need.
    /// </summary>
    /// <param name="uiScale">The UI scale.</param>
    /// <param name="railLogical">
    /// The rail's logical width when a translated tab label widened it (<see cref="LayoutBudgets.RailWidth"/>), so the
    /// wider rail does not come out of the centre floor; never less than <see cref="RailLogical"/>.
    /// </param>
    public static Vector2 MinWindowSize(float uiScale, float railLogical = RailLogical)
    {
        var scale = ClampUiScale(uiScale);
        var rail = float.IsFinite(railLogical) ? MathF.Max(railLogical, RailLogical) : RailLogical;
        return new Vector2((rail + LeftColumnLogical + RightColumnLogical + CentreFloorLogical) * scale, MinWindowHeightLogical * scale);
    }

    /// <summary>
    /// <see cref="MinWindowSize(float, float)"/> never larger than the viewport less <see cref="ViewportMarginPx"/> on each side
    /// (in Dalamud-scaled units, so divided by <paramref name="globalScale"/>): at UiScale 1.6 on a small screen the
    /// window can still be placed whole. A viewport that is not known (non-finite or non-positive) leaves it unclamped.
    /// </summary>
    public static Vector2 MinWindowSize(float uiScale, float globalScale, Vector2 viewport, float railLogical = RailLogical) =>
        FitViewport(MinWindowSize(uiScale, railLogical), globalScale, viewport);

    /// <summary>
    /// The main window's first-use size in Dalamud-scaled units (T14, accessibility B6): 1100 × 700 at the default UI
    /// scale, growing and shrinking with the UI scale, never under <see cref="MinWindowSize(float, float)"/>, and never larger
    /// than the viewport less <see cref="ViewportMarginPx"/> on each side. At UiScale 1.6 on a 1080p screen it fits.
    /// </summary>
    public static Vector2 DefaultWindowSize(float uiScale, float globalScale, Vector2 viewport)
    {
        var relative = ClampUiScale(uiScale) / DefaultUiScale;
        var size = Vector2.Max(DefaultWindowLogical * relative, MinWindowSize(uiScale));
        return FitViewport(size, globalScale, viewport);
    }

    private static Vector2 FitViewport(Vector2 size, float globalScale, Vector2 viewport)
    {
        var global = SafeGlobalScale(globalScale);
        var roomX = float.IsFinite(viewport.X) ? (viewport.X - 2f * ViewportMarginPx) / global : float.NaN;
        var roomY = float.IsFinite(viewport.Y) ? (viewport.Y - 2f * ViewportMarginPx) / global : float.NaN;
        return new Vector2(
            roomX > 0f ? MathF.Min(size.X, roomX) : size.X,
            roomY > 0f ? MathF.Min(size.Y, roomY) : size.Y);
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

    /// <summary>Quest table row height in Dalamud-scaled pixels for each density (T12): Dense 24, Comfortable 32.</summary>
    public static float TableRowTarget(RowDensity density) => density == RowDensity.Dense ? 24f : 32f;

    /// <summary>Smallest quest table row in pixels whatever the scales: rows are contiguous click targets (WCAG 2.5.8, accessibility B4).</summary>
    public const float TableRowMinPx = 24f;

    /// <summary>
    /// A quest table row's content height (the row minus its cell padding): the density's target at the host's global
    /// scale, never less than what the moon, icon and text need (<paramref name="minContent"/>), so large UI scales
    /// still fit, and never a row under <see cref="TableRowMinPx"/> (a global scale under 1). Unknown density values
    /// read as Comfortable.
    /// </summary>
    public static float TableRowContent(RowDensity density, float globalScale, float minContent, float cellPaddingY)
    {
        var target = MathF.Max(TableRowMinPx, TableRowTarget(Enum.IsDefined(density) ? density : RowDensity.Comfortable) * SafeGlobalScale(globalScale));
        var padding = float.IsFinite(cellPaddingY) ? MathF.Max(0f, cellPaddingY) : 0f;
        var floor = float.IsFinite(minContent) ? MathF.Max(0f, minContent) : 0f;
        return MathF.Max(floor, target - 2f * padding);
    }

    /// <summary>
    /// Texture coordinates that show a <paramref name="textureWidth"/> × <paramref name="textureHeight"/> image in a
    /// <paramref name="boxWidth"/> × <paramref name="boxHeight"/> box at the image's own aspect, cropping the excess
    /// evenly at both ends (an object-fit "cover"). Non-positive sizes yield the full image.
    /// </summary>
    public static (Vector2 Uv0, Vector2 Uv1) CenterCropUv(float boxWidth, float boxHeight, float textureWidth, float textureHeight) =>
        ImageCover.Uv(boxWidth, boxHeight, textureWidth, textureHeight);

    /// <summary>Smallest moon diameter, in pixels, on the surfaces read while playing (the Todo overlay, Nearby): 14 px (glyph proposal floor, UX panel finding 5).</summary>
    public const float PlayingGlyphMinDiameter = 14f;

    /// <summary>
    /// An inline glyph box grown, if needed, so the moon it holds (radius = box × <paramref name="radiusFraction"/>) is
    /// at least <paramref name="minDiameter"/> pixels across. A non-positive fraction leaves the box as it is.
    /// </summary>
    public static float GlyphBoxWithFloor(float box, float radiusFraction, float minDiameter = PlayingGlyphMinDiameter)
    {
        if (!(radiusFraction > 0f) || !float.IsFinite(minDiameter))
        {
            return box;
        }

        var floor = minDiameter / (2f * radiusFraction);
        return float.IsFinite(box) ? MathF.Max(box, floor) : floor;
    }

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
