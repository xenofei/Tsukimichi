using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Procedural moon-phase glyphs for quest states (spec §2.1), drawn with ImDrawList primitives so they scale with
/// the UI and need no textures. Geometry comes from <see cref="MoonGeometry"/>; this class only paints.
///
/// The look follows docs/design/glyphs/proposal.md v2.1 §3.1 for the parts that need no new art (release 0.5.1, T9a):
/// a Shadow base disc, a rim stroke of <see cref="Rim"/> px inside the rim in the state's colour, the lit part as the
/// intersection of the base disc with a terminator disc, a soft glow for Ready (a single thin ring below r 9), a
/// dashed rim for Unknown and a diagonal Eclipse bar (plus a notch from r 12) for Foreclosed. Draw order is
/// disc → rim → lit part, except the ring states (Accepted, ReadyOnOtherJob, Blocked) where the ring goes last, and
/// marks after the lit part. From <see cref="ShadingMinRadius"/> up the lit part also gets three inset discs that
/// brighten toward its centre and a faint highlight arc on the upper-left, so larger moons read as spheres.
/// </summary>
public static class MoonGlyph
{
    /// <summary>Radius from which shading and the highlight arc are drawn; smaller glyphs stay flat.</summary>
    public const float ShadingMinRadius = 9f;

    /// <summary>Radius of an inline glyph as a fraction of its square, leaving room for rings, the notch and the glow.</summary>
    public const float InlineRadiusFraction = 0.42f;

    /// <summary>Radius from which the Foreclosed notch is bitten out of the rim; below it the bar alone carries the state.</summary>
    public const float NotchMinRadius = 12f;

    /// <summary>Radius from which the Unknown rim has twelve 16° dashes; below it eight 22° dashes stay readable.</summary>
    public const float FineDashMinRadius = 10f;

    /// <summary>Glow discs for Ready at r ≥ 9, largest first: (radius multiplier, alpha), approximating the proposal's blurred gold disc.</summary>
    private static readonly (float Scale, float Alpha)[] Glow = [(1.70f, 0.05f), (1.42f, 0.09f), (1.20f, 0.15f)];

    /// <summary>Ready below r 9: one 1 px Moon ring at 1.25 r, 35 % — the glow discs vanish at that size.</summary>
    private const float SmallReadyRingScale = 1.25f;
    private static readonly uint SmallReadyRingColor = Theme.WithAlpha(Theme.Moon, 0.35f);

    /// <summary>Unknown's disc: Shadow at 60 %, so the veiled state stays the faintest.</summary>
    private static readonly uint VeiledDiscColor = Theme.WithAlpha(Theme.Shadow, 0.60f);

    /// <summary>Foreclosed bar endpoints (× r) and width rule: a diagonal that survives every colour deficiency.</summary>
    private static readonly Vector2 BarEnd = new(0.636f, 0.636f);
    private const float BarWidthFraction = 0.22f;
    private const float BarMinWidth = 2f;

    /// <summary>Foreclosed notch: a Night disc biting the rim at the upper right.</summary>
    private static readonly Vector2 NotchOffset = new(0.72f, -0.72f);
    private const float NotchRadius = 0.30f;

    /// <summary>Inset shading layers, outermost first: (scale about the lit region's centroid, alpha).</summary>
    private static readonly (float Scale, float Alpha)[] Shading = [(0.78f, 0.09f), (0.55f, 0.11f), (0.32f, 0.13f)];

    /// <summary>Highlight arc: radius fraction, start and end angle (radians, y down: 180° is left, 270° is top).</summary>
    private const float ArcRadius = 0.76f;
    private const float ArcStart = 200f * (MathF.PI / 180f);
    private const float ArcEnd = 252f * (MathF.PI / 180f);
    private const float ArcAlpha = 0.30f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    /// <summary>Highlight tones for the two lit colours: inset discs and the arc, precomputed so drawing packs nothing.</summary>
    private static readonly Tone GoldTone = Tone.For(Theme.Moon);
    private static readonly Tone SilverTone = Tone.For(Theme.Silver);

    // Scratch polygons; UI drawing happens on one thread. FillDisc always writes Scratch, so anything that must
    // survive the base disc (the filling overlay) lives in its own list.
    private static readonly List<Vector2> Scratch = new(256);
    private static readonly List<Vector2> Overlay = new(256);
    private static readonly List<Vector2> Inset = new(256);

    /// <summary>Rim stroke for a state moon of radius <paramref name="radius"/> px: 0.12 r clamped to 1.5–3 px (ui-revamp §2.7).</summary>
    public static float Rim(float radius) => Math.Clamp(0.12f * radius, 1.5f, 3f);

    /// <summary>Draws the glyph for <paramref name="state"/> centred at <paramref name="center"/> with the given pixel radius.</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state)
    {
        if (!(radius > 0.5f)) return;

        center = Snap(center, radius);
        var segments = MoonGeometry.SegmentsFor(radius);
        var rim = Rim(radius);

        switch (state)
        {
            case QuestState.Completed:                       // full moon: the only rimless disc
                FillDisc(dl, center, radius, segments, Theme.MoonU32);
                ShadeLit(dl, center, radius, Scratch, null, GoldTone);
                break;

            case QuestState.Accepted:                        // waxing gibbous, gold, Moon ring last
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                FillPhase(dl, center, radius, MoonPhase.WaxingGibbous, segments, Theme.MoonU32);
                ShadeLit(dl, center, radius, Scratch, null, GoldTone);
                Ring(dl, center, radius, segments, rim, Theme.MoonU32);
                break;

            case QuestState.Ready:                           // first quarter, gold, Dusk rim under the lit half, glow or thin outer ring
                if (radius < ShadingMinRadius)
                    dl.AddCircle(center, radius * SmallReadyRingScale, SmallReadyRingColor, segments, 1f);
                else
                    foreach (var (scale, alpha) in Glow)
                        dl.AddCircleFilled(center, radius * scale, Theme.WithAlpha(Theme.Moon, alpha));
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.DuskU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.MoonU32);
                ShadeLit(dl, center, radius, Scratch, null, GoldTone);
                break;

            case QuestState.ReadyOnOtherJob:                 // first quarter, silver, Moon ring last
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.SilverU32);
                ShadeLit(dl, center, radius, Scratch, null, SilverTone);
                Ring(dl, center, radius, segments, rim, Theme.MoonU32);
                break;

            case QuestState.DoneThisCycle:                   // waning gibbous, silver, Dusk rim under the lit part
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.DuskU32);
                FillPhase(dl, center, radius, MoonPhase.WaningGibbous, segments, Theme.SilverU32);
                ShadeLit(dl, center, radius, Scratch, null, SilverTone);
                break;

            case QuestState.Blocked:                         // new moon, Silver ring: the only plain empty ring
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.SilverU32);
                break;

            case QuestState.Foreclosed:                      // eclipsed: Eclipse rim, diagonal bar, notch from r 12
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.EclipseU32);
                dl.AddLine(center - BarEnd * radius, center + BarEnd * radius, Theme.EclipseU32, MathF.Max(BarMinWidth, BarWidthFraction * radius));
                if (radius >= NotchMinRadius)
                    dl.AddCircleFilled(center + NotchOffset * radius, radius * NotchRadius, Theme.NightU32);
                break;

            case QuestState.Unknown:                         // veiled: faint disc, dashed Dusk rim
            default:
                FillDisc(dl, center, radius, segments, VeiledDiscColor);
                if (radius >= FineDashMinRadius)
                    DashedRing(dl, center, radius, segments, rim, Theme.DuskU32, dashes: 12, dashDegrees: 16f);
                else
                    DashedRing(dl, center, radius, segments, rim, Theme.DuskU32, dashes: 8, dashDegrees: 22f);
                break;
        }
    }

    /// <summary>
    /// Reserves a <paramref name="size"/> × <paramref name="size"/> item at the cursor and draws the glyph inside it. The
    /// moon radius is 42% of the box so rings, the notch and the glow stay within the line.
    /// </summary>
    public static void DrawInline(QuestState state, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        Draw(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * 0.42f, state);
    }

    /// <summary>
    /// The veiled (Unknown) glyph faded to <paramref name="alpha"/>, as a stand-in where an icon would go and none
    /// exists (Moonlit rewards of a kind without sheet art). Reserves a square item like <see cref="DrawInline"/>, so
    /// the caller can hang a tooltip on it; the faint disc and dashed rim both carry the alpha, and it says nothing
    /// about state or progress.
    /// </summary>
    public static void DrawVeiledInline(float size, float alpha)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        var radius = size * InlineRadiusFraction;
        if (!(radius > 0.5f)) return;

        var dl = ImGui.GetWindowDrawList();
        var center = Snap(pos + new Vector2(size * 0.5f), radius);
        var segments = MoonGeometry.SegmentsFor(radius);
        var rim = Rim(radius);
        FillDisc(dl, center, radius, segments, Theme.WithAlpha(Theme.Shadow, 0.60f * alpha));
        var rimColor = Theme.WithAlpha(Theme.Dusk, alpha);
        if (radius >= FineDashMinRadius)
            DashedRing(dl, center, radius, segments, rim, rimColor, dashes: 12, dashDegrees: 16f);
        else
            DashedRing(dl, center, radius, segments, rim, rimColor, dashes: 8, dashDegrees: 22f);
    }

    /// <summary>
    /// Progress moon for tree nodes: lit <paramref name="fraction"/> from 0 (new) to 1 (full), filling right to left like a
    /// waxing moon. The dark side is Shadow with a Dusk rim of <see cref="Rim"/> px around it (the terminator always
    /// runs pole to pole, so the dark side's rim is exactly the left half-circle); the lit side is rimless, like Completed.
    /// <see cref="MoonGeometry.FillingLayers"/> applies the terminator floor, so any fraction strictly between 0 and 1
    /// shows a visible sliver on both sides.
    /// </summary>
    public static void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction)
    {
        if (!(radius > 0.5f)) return;

        center = Snap(center, radius);
        var segments = MoonGeometry.SegmentsFor(radius);
        var rim = Rim(radius);

        // The overlay goes into its own list: FillDisc rewrites Scratch with the base polygon, and painting that
        // polygon in the overlay tone is exactly the bug that showed every partial moon as full (or new).
        var baseLit = MoonGeometry.FillingLayers(center, radius, fraction, segments, Overlay);
        var hasOverlay = Overlay.Count >= 3;

        if (baseLit)
        {
            // Crescent (or full): the lit disc is shaded first, then the shadow lens covers the dark side and the
            // rim goes around that side only.
            FillDisc(dl, center, radius, segments, Theme.MoonU32);
            ShadeLit(dl, center, radius, Scratch, hasOverlay ? Overlay : null, GoldTone);
            if (hasOverlay)
            {
                FillPolygon(dl, Overlay, Theme.ShadowU32);
                DarkSideRim(dl, center, radius, segments, rim, Theme.DuskU32);
            }
        }
        else if (hasOverlay)
        {
            // Gibbous or half: dark disc, rim under the lit lens, then the lit lens.
            FillDisc(dl, center, radius, segments, Theme.ShadowU32);
            DarkSideRim(dl, center, radius, segments, rim, Theme.DuskU32);
            FillPolygon(dl, Overlay, Theme.MoonU32);
            ShadeLit(dl, center, radius, Overlay, null, GoldTone);
        }
        else
        {
            // New: a plain rimmed dark disc, so an empty node still has an outline.
            FillDisc(dl, center, radius, segments, Theme.ShadowU32);
            Ring(dl, center, radius, segments, rim, Theme.DuskU32);
        }
    }

    /// <summary>Inline form of <see cref="DrawFilling"/>: reserves a square item and draws at the cursor.</summary>
    public static void DrawFillingInline(float fraction, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        DrawFilling(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * 0.42f, fraction);
    }

    /// <summary>
    /// Snaps the centre so the rim lands on whole pixels: integer centres for an even diameter, half-integer for an
    /// odd one (imgui-notes §1). Moves the glyph by at most half a pixel; the reserved box is not touched.
    /// </summary>
    private static Vector2 Snap(Vector2 center, float radius)
    {
        var offset = (int)MathF.Round(2f * radius) % 2 == 1 ? 0.5f : 0f;
        return new Vector2(MathF.Round(center.X - offset) + offset, MathF.Round(center.Y - offset) + offset);
    }

    /// <summary>Base disc through the same polygon the overlays use, so their rim chords coincide with it.</summary>
    private static void FillDisc(ImDrawListPtr dl, Vector2 center, float radius, int segments, uint color)
    {
        MoonGeometry.CirclePolygon(center, radius, segments, Scratch);
        FillPolygon(dl, Scratch, color);
    }

    private static void FillPhase(ImDrawListPtr dl, Vector2 center, float radius, MoonPhase phase, int segments, uint color)
    {
        MoonGeometry.PhaseLayers(center, radius, phase, segments, Scratch);
        FillPolygon(dl, Scratch, color);
    }

    private static void FillPolygon(ImDrawListPtr dl, List<Vector2> polygon, uint color)
    {
        if (polygon.Count < 3) return;
        var span = CollectionsMarshal.AsSpan(polygon);
        dl.AddConvexPolyFilled(ref MemoryMarshal.GetReference(span), span.Length, color);
    }

    /// <summary>
    /// Shading for a lit region given as a convex polygon <paramref name="lit"/> (the whole disc for a full moon), from
    /// which <paramref name="shadow"/>, when given, is about to be cut away. Three copies of the polygon shrunk about
    /// its centroid, each a little brighter, give a soft dome; a short arc on the upper-left adds the highlight. Both
    /// are skipped below <see cref="ShadingMinRadius"/>.
    /// </summary>
    private static void ShadeLit(ImDrawListPtr dl, Vector2 center, float radius, List<Vector2> lit, List<Vector2>? shadow, Tone tone)
    {
        if (radius < ShadingMinRadius || lit.Count < 3) return;

        var centroid = Centroid(lit);
        for (var i = 0; i < Shading.Length; i++)
        {
            var (scale, alpha) = Shading[i];
            Inset.Clear();
            foreach (var p in lit)
                Inset.Add(centroid + (p - centroid) * scale);
            FillPolygon(dl, Inset, tone.Inner[i]);
        }

        HighlightArc(dl, center, radius, lit, shadow, tone.Arc);
    }

    /// <summary>
    /// Thin arc at <see cref="ArcRadius"/>·r between <see cref="ArcStart"/> and <see cref="ArcEnd"/>, stroked only where
    /// it lies inside <paramref name="lit"/> and outside <paramref name="shadow"/>, so a right-lit moon shows no
    /// highlight on its dark left.
    /// </summary>
    private static void HighlightArc(ImDrawListPtr dl, Vector2 center, float radius, List<Vector2> lit, List<Vector2>? shadow, uint color)
    {
        var r = radius * ArcRadius;
        var samples = Math.Clamp((int)MathF.Ceiling(radius / 3f), 6, 24);
        var thickness = MathF.Max(1f, radius * 0.06f);
        var run = 0;

        dl.PathClear();
        for (var i = 0; i <= samples; i++)
        {
            var angle = ArcStart + (ArcEnd - ArcStart) * i / samples;
            var (sin, cos) = MathF.SinCos(angle);
            var p = new Vector2(center.X + r * cos, center.Y + r * sin);
            var isLit = ContainsPoint(lit, p) && (shadow is null || !ContainsPoint(shadow, p));
            if (isLit)
            {
                dl.PathLineTo(p);
                run++;
                continue;
            }

            if (run >= 2) dl.PathStroke(color, ImDrawFlags.None, thickness);
            else dl.PathClear();
            run = 0;
        }

        if (run >= 2) dl.PathStroke(color, ImDrawFlags.None, thickness);
        else dl.PathClear();
    }

    private static Vector2 Centroid(List<Vector2> polygon)
    {
        var sum = Vector2.Zero;
        foreach (var p in polygon) sum += p;
        return sum / polygon.Count;
    }

    /// <summary>Point inside a convex polygon: every edge cross product has the same sign (either winding).</summary>
    private static bool ContainsPoint(List<Vector2> polygon, Vector2 p)
    {
        var n = polygon.Count;
        if (n < 3) return false;

        var positive = false;
        var negative = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var a = polygon[j];
            var b = polygon[i];
            var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (cross > 0f) positive = true;
            else if (cross < 0f) negative = true;
            if (positive && negative) return false;
        }

        return true;
    }

    /// <summary>Ring just inside the rim, like Pillow's inward outline: stroke centred at r − thickness/2.</summary>
    private static void Ring(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color)
    {
        dl.AddCircle(center, radius - thickness * 0.5f, color, segments, thickness);
    }

    /// <summary>The rim arc over the dark side of a filling moon: the left half-circle, pole to pole, butt ends.</summary>
    private static void DarkSideRim(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color)
    {
        dl.PathClear();
        dl.PathArcTo(center, radius - thickness * 0.5f, MathF.PI / 2f, 3f * MathF.PI / 2f, ArcSegments(segments, MathF.PI));
        dl.PathStroke(color, ImDrawFlags.None, thickness);
    }

    /// <summary><paramref name="dashes"/> dashes of <paramref name="dashDegrees"/> each, evenly spaced from 12 o'clock.</summary>
    private static void DashedRing(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color, int dashes, float dashDegrees)
    {
        var r = radius - thickness * 0.5f;
        var sweep = dashDegrees * (MathF.PI / 180f);
        var arcSegments = ArcSegments(segments, sweep);

        dl.PathClear();
        for (var k = 0; k < dashes; k++)
        {
            var start = (k * 360f / dashes - 90f) * (MathF.PI / 180f);
            dl.PathArcTo(center, r, start, start + sweep, arcSegments);
            dl.PathStroke(color, ImDrawFlags.None, thickness);
        }
    }

    /// <summary>Segments for an arc of <paramref name="sweep"/> radians at the disc's segment density, at least six.</summary>
    private static int ArcSegments(int segments, float sweep) =>
        Math.Max(6, (int)MathF.Ceiling(segments * sweep / (2f * MathF.PI)));

    /// <summary>Packed highlight colours for one lit tone: the inset discs (outermost first) and the arc.</summary>
    private readonly struct Tone(uint[] inner, uint arc)
    {
        public uint[] Inner { get; } = inner;
        public uint Arc { get; } = arc;

        /// <summary>Insets are the lit colour pulled halfway to white at the layer's alpha; the arc is nearly white.</summary>
        public static Tone For(Vector4 lit)
        {
            var bright = Vector4.Lerp(lit, White, 0.55f);
            var inner = new uint[Shading.Length];
            for (var i = 0; i < inner.Length; i++)
                inner[i] = Theme.WithAlpha(bright, Shading[i].Alpha);
            return new Tone(inner, Theme.WithAlpha(Vector4.Lerp(lit, White, 0.85f), ArcAlpha));
        }
    }
}
