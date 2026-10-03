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

    /// <summary>
    /// Default text size (feature plan v6 U7, the owner's 2026-10-03 note): Tsukimichi's fonts at the size the window
    /// scale gives them. Text size multiplies the font alone, so text can grow without the layout around it.
    /// </summary>
    public const float DefaultTextScale = 1f;
    public const float MinTextScale = 0.8f;
    public const float MaxTextScale = 1.5f;

    /// <summary>The text size moves in steps of 10%, so each step is one font build rather than one per pixel dragged.</summary>
    public const float TextScaleStep = 0.1f;

    private const float StepsPerUnit = 10f;

    /// <summary>The UI scale within its bounds; a non-finite value (a corrupt config) becomes the default.</summary>
    public static float ClampUiScale(float value) => Clamp(value, MinUiScale, MaxUiScale, DefaultUiScale);

    /// <summary>
    /// The text size within 80–150% and on its 10% step (the nearest one; a half step rounds up); a non-finite value
    /// becomes the default 100%.
    /// </summary>
    public static float ClampTextScale(float value)
    {
        if (!float.IsFinite(value))
        {
            return DefaultTextScale;
        }

        // Counted in whole steps (a hair added so 1.15, stored as 1.1499999, still rounds up as written).
        var steps = MathF.Round((Math.Clamp(value, MinTextScale, MaxTextScale) * StepsPerUnit) + 1e-4f, MidpointRounding.AwayFromZero);
        return Math.Clamp(steps / StepsPerUnit, MinTextScale, MaxTextScale);
    }

    /// <summary>The text size as a whole percentage (80–150), what Settings shows and steps through.</summary>
    public static int TextScalePercent(float value) => (int)MathF.Round(ClampTextScale(value) * 100f);

    /// <summary>A percentage from Settings back to a text size, clamped and stepped like <see cref="ClampTextScale"/>.</summary>
    public static float TextScaleFromPercent(int percent) => ClampTextScale(percent / 100f);

    /// <summary>
    /// The pixel size of a font built for <paramref name="textScale"/> from a body size of <paramref name="basePx"/>
    /// (Dalamud's font at global scale 1), rounded to a whole pixel so two nearby values share one build; 0 when the
    /// base is unknown.
    /// </summary>
    public static float TextFontPx(float basePx, float textScale) =>
        float.IsFinite(basePx) && basePx > 0f ? MathF.Round(basePx * ClampTextScale(textScale)) : 0f;

    /// <summary>
    /// The font scale a Tsukimichi window sets on itself (<c>SetWindowFontScale</c>) over the body font pushed around it:
    /// the UI scale alone while that font is built at the text size, the UI scale × the text size while the default font
    /// stands in. Either way the text is drawn at the UI scale × the text size, the text size counted once
    /// (<see cref="PushedFontFactor"/> × this).
    /// </summary>
    public static float WindowFontScale(float uiScale, float textScale, bool textFontBuilt) =>
        ClampUiScale(uiScale) * (textFontBuilt ? 1f : ClampTextScale(textScale));

    /// <summary>How much larger than Dalamud's default font the pushed body font is: the text size once built, 1 until then.</summary>
    public static float PushedFontFactor(float textScale, bool textFontBuilt) => textFontBuilt ? ClampTextScale(textScale) : 1f;

    /// <summary>The icon scale within its bounds; a non-finite value becomes the default.</summary>
    public static float ClampIconScale(float value) => Clamp(value, MinIconScale, MaxIconScale, DefaultIconScale);

    /// <summary>Global scale as reported by the host, guarded: non-finite or non-positive values count as 1.</summary>
    public static float SafeGlobalScale(float globalScale) => float.IsFinite(globalScale) && globalScale > 0f ? globalScale : 1f;

    /// <summary>Pixels per logical unit for layout: global scale × clamped UI scale.</summary>
    public static float LayoutFactor(float globalScale, float uiScale) => SafeGlobalScale(globalScale) * ClampUiScale(uiScale);

    /// <summary>Pixels per logical unit for moons and icons: the layout factor × clamped icon scale.</summary>
    public static float IconFactor(float globalScale, float uiScale, float iconScale) => LayoutFactor(globalScale, uiScale) * ClampIconScale(iconScale);

    /// <summary>
    /// Logical width of the main window's tab rail at Full (feature plan v4 L7; 70 since plan v7 UI-4, spec Revision 3):
    /// its own column left of the tree, a crest on top, one station per tab (a 30 px icon on a plate over a small label,
    /// the stations sharing the rail's height) and the overall gauge, Help and Settings at its foot. A label that does
    /// not fit its plate is tracked, shrunk or wrapped (<see cref="RailLabel.Fit"/>), never cut. The widest rail, so the
    /// main window's minimum is reckoned with it.
    /// </summary>
    public const float RailLogical = 70f;

    /// <summary>The labelled rail at Quiet (plan v7 UI-4, spec Revision 3): a little narrower than Full's, for 28 px icons.</summary>
    public const float RailQuietLogical = 66f;

    /// <summary>
    /// The compact rail: icons only, the labels in tooltips. It turns on by itself on a narrow window
    /// (<see cref="LayoutBudgets.CompactRail"/>) or always by Settings › Display › Compact rail.
    /// </summary>
    public const float RailCompactLogical = 44f;

    /// <summary>
    /// Dalamud's window padding on both sides of the main window together, in Dalamud-scaled units (8 each side; the
    /// UI scale does not change it): the minimum width adds it so the panes' floors hold inside the padding.
    /// </summary>
    public const float WindowPaddingX = 16f;

    /// <summary>Logical height floor of the main window.</summary>
    public const float MinWindowHeightLogical = 500f;

    /// <summary>
    /// The main window's first-use size in Dalamud-scaled units at <see cref="DefaultUiScale"/>: wide enough for the
    /// labelled rail, the tree and the detail pane at their default widths (so the tree opens in its full tier) with
    /// the quest list well above its floor; still inside a 1080p screen, and clamped to smaller ones.
    /// </summary>
    public static readonly Vector2 DefaultWindowLogical = new(1320f, 760f);

    /// <summary>Pixels the default window leaves free on every side of the viewport (accessibility B6).</summary>
    public const float ViewportMarginPx = 48f;

    /// <summary>
    /// The main window's minimum size in Dalamud-scaled units for a UI scale (feature plan v4 L1): the tab rail, the
    /// floors of the tree, the centre and the detail pane and the gutters between them
    /// (<see cref="PaneLayout.MinContentLogical"/>), each multiplied by the clamped UI scale, plus the window's padding
    /// and the pixels the whole-pixel floors and gutters can add (<see cref="PaneLayout.RoundingReservePx"/>, taken at a
    /// global scale of 1); and the height floor. Dalamud multiplies the result by its global scale, so no pane is ever
    /// squeezed under its floor by the window alone.
    /// </summary>
    /// <param name="uiScale">The UI scale.</param>
    /// <param name="railLogical">
    /// The rail's logical width this frame: <see cref="RailLogical"/>, or <see cref="RailCompactLogical"/> while the rail
    /// is compact, so a compact rail lets the window narrow by what it gave up. A width outside the two (or unreadable)
    /// is taken as the nearer of them (<see cref="RailLogical"/> when unreadable).
    /// </param>
    public static Vector2 MinWindowSize(float uiScale, float railLogical = RailLogical) =>
        MinWindowSize(uiScale, railLogical, PaneLayout.RoundingReservePx);

    /// <summary>
    /// <see cref="MinWindowSize(float, float)"/> never larger than the viewport less <see cref="ViewportMarginPx"/> on each side
    /// (in Dalamud-scaled units, so divided by <paramref name="globalScale"/>): at UiScale 1.6 on a small screen the
    /// window can still be placed whole. A viewport that is not known (non-finite or non-positive) leaves it unclamped.
    /// The rounding reserve is <see cref="PaneLayout.RoundingReservePx"/> pixels at this global scale.
    /// </summary>
    public static Vector2 MinWindowSize(float uiScale, float globalScale, Vector2 viewport, float railLogical = RailLogical) =>
        FitViewport(MinWindowSize(uiScale, railLogical, PaneLayout.RoundingReservePx / SafeGlobalScale(globalScale)), globalScale, viewport);

    private static Vector2 MinWindowSize(float uiScale, float railLogical, float reserve)
    {
        var scale = ClampUiScale(uiScale);
        var rail = float.IsFinite(railLogical) ? Math.Clamp(railLogical, RailCompactLogical, RailLogical) : RailLogical;
        return new Vector2(((rail + PaneLayout.MinContentLogical) * scale) + WindowPaddingX + reserve, MinWindowHeightLogical * scale);
    }

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

    /// <summary>
    /// <paramref name="sizeLogical"/> (Dalamud-scaled units, which Dalamud multiplies by <paramref name="globalScale"/>)
    /// never reaching past the work area's right or bottom edge from <paramref name="windowPos"/> (pixels), so a window
    /// given a size from where it stands stays whole on a small screen. A window left of or above the work area counts
    /// from its edge. A work area that is not known (non-finite or non-positive) leaves the size as it is.
    /// </summary>
    public static Vector2 FitFromPosition(Vector2 sizeLogical, float globalScale, Vector2 windowPos, Vector2 workPos, Vector2 workSize)
    {
        if (!float.IsFinite(workSize.X) || !float.IsFinite(workSize.Y) || workSize.X <= 0f || workSize.Y <= 0f
            || !float.IsFinite(workPos.X) || !float.IsFinite(workPos.Y) || !float.IsFinite(windowPos.X) || !float.IsFinite(windowPos.Y))
        {
            return sizeLogical;
        }

        var global = SafeGlobalScale(globalScale);
        var room = Vector2.Max(workPos + workSize - Vector2.Max(windowPos, workPos), Vector2.Zero) / global;
        return Vector2.Min(sizeLogical, room);
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

    /// <summary>
    /// Quest table row height in Dalamud-scaled pixels at a Decoration level (docs/design/flair-v13 §1, "Row height and
    /// density"): Full 34 Comfortable and 28 Dense, Quiet 30 and 24, Plain 24 at either density (the
    /// <see cref="TableRowMinPx"/> floor, so the WCAG 2.5.8 target still holds). Unknown values read as Full and
    /// Comfortable.
    /// </summary>
    public static float TableRowTarget(Flair flair, RowDensity density)
    {
        var dense = density == RowDensity.Dense;
        return (Enum.IsDefined(flair) ? flair : Flair.Full) switch
        {
            Flair.Plain => TableRowMinPx,
            Flair.Quiet => dense ? 24f : 30f,
            _ => dense ? 28f : 34f,
        };
    }

    /// <summary>
    /// A Journal tree row's height in Dalamud-scaled pixels at a Decoration level: Full 34, Quiet 30, Plain 24 (Plain
    /// draws no gauge, so the A4 halo-box floor does not apply to it).
    /// </summary>
    public static float TreeRowTarget(Flair flair) => (Enum.IsDefined(flair) ? flair : Flair.Full) switch
    {
        Flair.Plain => TableRowMinPx,
        Flair.Quiet => 30f,
        _ => 34f,
    };

    /// <summary>
    /// A tree row's height at <paramref name="flair"/>: the level's target at the host's global scale, never less than
    /// the line with a little air, and, where a gauge draws (Full and Quiet), never less than
    /// <see cref="TreeRowHeight(float, float, float)"/> (the halo box plus its padding, the 30 px floor).
    /// </summary>
    public static float TreeRowHeight(Flair flair, float lineHeight, float glyphRadius, float layoutScale, float globalScale)
    {
        var line = float.IsFinite(lineHeight) ? MathF.Max(0f, lineHeight) : 0f;
        var scale = float.IsFinite(layoutScale) && layoutScale > 0f ? layoutScale : 1f;
        var target = TreeRowTarget(flair) * SafeGlobalScale(globalScale);
        if (FlairRules.Gauge(Enum.IsDefined(flair) ? flair : Flair.Full) == TreeGauge.None)
        {
            return MathF.Max(MathF.Max(TableRowMinPx, target), line + (4f * scale));
        }

        return MathF.Max(target, TreeRowHeight(line, glyphRadius, scale));
    }

    /// <summary>Smallest quest table row in pixels whatever the scales: rows are contiguous click targets (WCAG 2.5.8, accessibility B4).</summary>
    public const float TableRowMinPx = 24f;

    /// <summary>
    /// A quest table row's content height (the row minus its cell padding): the density's target at the host's global
    /// scale, never less than what the moon, icon and text need (<paramref name="minContent"/>), so large UI scales
    /// still fit, and never a row under <see cref="TableRowMinPx"/> (a global scale under 1). Unknown density values
    /// read as Comfortable.
    /// </summary>
    public static float TableRowContent(RowDensity density, float globalScale, float minContent, float cellPaddingY) =>
        RowContent(TableRowTarget(Enum.IsDefined(density) ? density : RowDensity.Comfortable), globalScale, minContent, cellPaddingY);

    /// <summary>
    /// <see cref="TableRowContent(RowDensity, float, float, float)"/> at a Decoration level's row height
    /// (<see cref="TableRowTarget(Flair, RowDensity)"/>): never under what the row's content needs, never a row under
    /// <see cref="TableRowMinPx"/>.
    /// </summary>
    public static float TableRowContent(Flair flair, RowDensity density, float globalScale, float minContent, float cellPaddingY) =>
        RowContent(TableRowTarget(flair, Enum.IsDefined(density) ? density : RowDensity.Comfortable), globalScale, minContent, cellPaddingY);

    private static float RowContent(float rowTarget, float globalScale, float minContent, float cellPaddingY)
    {
        var target = MathF.Max(TableRowMinPx, rowTarget * SafeGlobalScale(globalScale));
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
