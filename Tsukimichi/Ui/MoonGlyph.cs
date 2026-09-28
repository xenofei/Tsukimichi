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
/// The look replicates assets/icons/render_icons.py: a dark base disc, the lit part as the intersection of the base
/// disc with an offset terminator disc, rings whose thickness scales with the radius, a soft glow for Ready, a
/// dashed ring for Unknown and a notch bitten out of the Foreclosed ring. From <see cref="ShadingMinRadius"/> up the
/// lit part also gets three inset discs that brighten toward its centre and a faint highlight arc on the upper-left
/// of the lit region, so larger moons read as spheres; below that the flat fill stays crisp.
/// </summary>
public static class MoonGlyph
{
    /// <summary>Radius from which shading and the highlight arc are drawn; smaller glyphs stay flat.</summary>
    public const float ShadingMinRadius = 9f;

    /// <summary>Radius of an inline glyph as a fraction of its square, leaving room for rings, the notch and the glow.</summary>
    public const float InlineRadiusFraction = 0.42f;

    /// <summary>Ring thickness as a fraction of the radius (never thinner than one pixel).</summary>
    private const float RingFraction = 0.07f;

    /// <summary>Glow discs for Ready, largest first: (radius multiplier, alpha). Approximates the Pillow quadratic halo.</summary>
    private static readonly (float Scale, float Alpha)[] Glow = [(1.6f, 0.04f), (1.35f, 0.07f), (1.15f, 0.10f)];

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

    /// <summary>Draws the glyph for <paramref name="state"/> centred at <paramref name="center"/> with the given pixel radius.</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state)
    {
        if (!(radius > 0.5f)) return;

        var segments = MoonGeometry.SegmentsFor(radius);
        var ring = RingThickness(radius);

        switch (state)
        {
            case QuestState.Completed:                       // full moon
                FillDisc(dl, center, radius, segments, Theme.MoonU32);
                ShadeLit(dl, center, radius, Scratch, null, GoldTone);
                break;

            case QuestState.Accepted:                        // waxing gibbous, gold, thin gold ring
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.WaxingGibbous, segments, Theme.MoonU32);
                ShadeLit(dl, center, radius, Scratch, null, GoldTone);
                Ring(dl, center, radius, segments, ring, Theme.MoonU32);
                break;

            case QuestState.Ready:                           // first quarter, gold, outer glow
                foreach (var (scale, alpha) in Glow)
                    dl.AddCircleFilled(center, radius * scale, Theme.WithAlpha(Theme.Moon, alpha));
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.MoonU32);
                ShadeLit(dl, center, radius, Scratch, null, GoldTone);
                break;

            case QuestState.ReadyOnOtherJob:                 // first quarter, silver, gold ring
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.SilverU32);
                ShadeLit(dl, center, radius, Scratch, null, SilverTone);
                Ring(dl, center, radius, segments, ring, Theme.MoonU32);
                break;

            case QuestState.DoneThisCycle:                   // waning gibbous, silver
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.WaningGibbous, segments, Theme.SilverU32);
                ShadeLit(dl, center, radius, Scratch, null, SilverTone);
                break;

            case QuestState.Blocked:                         // new moon, thin silver ring
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                Ring(dl, center, radius, segments, ring, Theme.SilverU32);
                break;

            case QuestState.Foreclosed:                      // eclipsed: dark, eclipse ring, notch at the upper right
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                Ring(dl, center, radius, segments, ring * 1.3f, Theme.EclipseU32);
                dl.AddCircleFilled(center + new Vector2(0.72f, -0.72f) * radius, radius * 0.30f, Theme.NightU32);
                break;

            case QuestState.Unknown:                         // veiled: dark, dashed ring
            default:
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                DashedRing(dl, center, radius, ring, Theme.DuskU32);
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
    /// Progress moon for tree nodes: lit <paramref name="fraction"/> from 0 (new) to 1 (full), filling right to left like a
    /// waxing moon, with a thin Veil ring for definition on any background.
    /// </summary>
    public static void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction)
    {
        if (!(radius > 0.5f)) return;

        var segments = MoonGeometry.SegmentsFor(radius);

        // The overlay goes into its own list: FillDisc rewrites Scratch with the base polygon, and painting that
        // polygon in the overlay tone is exactly the bug that showed every partial moon as full (or new).
        var baseLit = MoonGeometry.FillingLayers(center, radius, fraction, segments, Overlay);

        FillDisc(dl, center, radius, segments, baseLit ? Theme.MoonU32 : Theme.UnlitDiscU32);
        if (baseLit)
        {
            // Crescent (or full): the lit disc is shaded first, then the shadow lens covers the dark side.
            ShadeLit(dl, center, radius, Scratch, Overlay.Count >= 3 ? Overlay : null, GoldTone);
            FillPolygon(dl, Overlay, Theme.UnlitDiscU32);
        }
        else
        {
            // Gibbous, half or new: the lit lens sits on the dark disc.
            FillPolygon(dl, Overlay, Theme.MoonU32);
            if (Overlay.Count >= 3)
                ShadeLit(dl, center, radius, Overlay, null, GoldTone);
        }

        Ring(dl, center, radius, segments, RingThickness(radius), Theme.VeilU32);
    }

    /// <summary>Inline form of <see cref="DrawFilling"/>: reserves a square item and draws at the cursor.</summary>
    public static void DrawFillingInline(float fraction, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        DrawFilling(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * 0.42f, fraction);
    }

    private static float RingThickness(float radius) => MathF.Max(1f, radius * RingFraction);

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

    /// <summary>Ring just inside the rim, like Pillow's inward outline.</summary>
    private static void Ring(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color)
    {
        dl.AddCircle(center, radius - thickness * 0.5f, color, segments, thickness);
    }

    /// <summary>Twelve 16° dashes every 30°, starting at the top (Pillow: start = k·30 − 90).</summary>
    private static void DashedRing(ImDrawListPtr dl, Vector2 center, float radius, float thickness, uint color)
    {
        const int Dashes = 12;
        const float DashDegrees = 16f;
        var r = radius - thickness * 0.5f;
        var arcSegments = Math.Max(2, (int)MathF.Ceiling(radius / 8f));

        dl.PathClear();
        for (var k = 0; k < Dashes; k++)
        {
            var start = (k * 360f / Dashes - 90f) * (MathF.PI / 180f);
            var end = start + DashDegrees * (MathF.PI / 180f);
            dl.PathArcTo(center, r, start, end, arcSegments);
            dl.PathStroke(color, ImDrawFlags.None, thickness);
        }
    }

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
