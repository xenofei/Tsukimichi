using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using M = Tsukimichi.Core.Ui.GlyphTokens.Medallion;
using X = Tsukimichi.Core.Ui.GlyphTokens.MedallionDetail;

namespace Tsukimichi.Ui;

/// <summary>
/// The progress gauges in the medal's material (feature plan v6 G5): the halo gauge (<see cref="MoonGlyph.DrawHalo"/>)
/// and the orbit ring of the rail and the Journal (<see cref="Orbit"/>) as a lapis-enamel groove between Abyss keylines
/// with a gilt arc lit from the upper left, as the medal's bezel is (two slopes meeting at a crest), and the filling
/// moon as moonstone over its dark side. Geometry is unchanged (<see cref="GaugeGeometry"/>); only the material is new.
/// The high-contrast palette keeps its flat 1.11 gauges. Allocation-free.
/// </summary>
internal static class MedalGauge
{
    /// <summary>The groove's enamel: the lit lapis's deep sea, distinct from Night and from the gilt arc.</summary>
    public static readonly uint GrooveU32 = Theme.U32(M.LapisSeaBottom);

    /// <summary>The keyline round grooves and moons.</summary>
    public static readonly uint KeylineU32 = Theme.U32(M.Keyline);

    /// <summary>The arc's base (its anti-aliased edge) and the round caps' fallback.</summary>
    public static readonly uint GiltU32 = Theme.U32(M.Gilt);

    /// <summary>A finished gauge that steps back (the Journal tree): the gilt's shaded side.</summary>
    public static readonly uint GiltDimU32 = Theme.U32(M.GiltShade);

    /// <summary>The complete gauge's soft glow rings.</summary>
    public static readonly uint GlowOuterU32 = Theme.WithAlpha(M.GiltHigh, 0.10f);
    public static readonly uint GlowInnerU32 = Theme.WithAlpha(M.GiltHigh, 0.16f);

    /// <summary>The moon bead at the arc's head, and the full-moon pip.</summary>
    public static readonly uint PearlU32 = Theme.U32(M.MoonstoneSpecular);

    private static readonly uint DarkSideU32 = Theme.U32(X.DarkSide);
    private static readonly uint MoonstoneU32 = Theme.U32(M.MoonstoneHigh);
    private static readonly uint MoonstoneDimU32 = Theme.U32(M.MoonstoneMid);

    /// <summary>The bezel's slopes across a gauge's box (gen5 <c>bezel</c>, as fractions of the half-size from the centre).</summary>
    private static readonly (float, Vector4)[] OuterSlope = [(0f, M.GiltHigh), (0.42f, M.GiltMid), (0.78f, M.GiltShade), (1f, M.GiltDeep)];
    private static readonly (float, Vector4)[] InnerSlope = [(0f, M.GiltDeep), (0.55f, M.GiltShade), (1f, M.Gilt)];
    private static readonly Vector2 SlopeFrom = new(-50f / 64f, -54f / 64f);
    private static readonly Vector2 SlopeTo = new(50f / 64f, 54f / 64f);

    /// <summary>Moonstone's lit ramp for the filling moon: glint at the highlight, lit face, body.</summary>
    private static readonly Vector4[] MoonRamp = [M.MoonstoneSpecular, M.MoonstoneHigh, M.Moonstone];

    private static readonly List<Vector2> Scratch = new(128);
    private static readonly List<Vector2> Overlay = new(128);

    /// <summary>The groove a gauge runs in: Abyss keylines either side of a lapis track <paramref name="stroke"/> px wide.</summary>
    public static void Groove(ImDrawListPtr dl, Vector2 center, float radius, float stroke, int segments)
    {
        var keyline = MathF.Max(1f, MathF.Round(stroke * 0.25f));
        dl.AddCircle(center, radius, KeylineU32, segments, stroke + 2f * keyline);
        dl.AddCircle(center, radius, GrooveU32, segments, stroke);
    }

    /// <summary>
    /// A gilt arc from <paramref name="start"/> through <paramref name="sweep"/> radians on the circle of
    /// <paramref name="radius"/>, <paramref name="stroke"/> wide, with round caps: an anti-aliased base stroke, then from
    /// a 3 px stroke the two lit slopes as one vertex-coloured mesh inset half a pixel, coloured across the gauge's box of
    /// half-size <paramref name="box"/> as the medal's bezel is across its own.
    /// </summary>
    public static void GiltArc(ImDrawListPtr dl, Vector2 center, float radius, float stroke, float start, float sweep, float box, int segments)
    {
        if (!(sweep > 0f))
        {
            return;
        }

        var steps = GaugeGeometry.ArcSegments(segments, sweep);
        dl.PathClear();
        dl.PathArcTo(center, radius, start, start + sweep, steps);
        dl.PathStroke(GiltU32, ImDrawFlags.None, stroke);

        var (s0, c0) = MathF.SinCos(start);
        var (s1, c1) = MathF.SinCos(start + sweep);
        dl.AddCircleFilled(center + new Vector2(c0, s0) * radius, stroke * 0.5f, Slope(OuterSlope, center, box, center + new Vector2(c0, s0) * radius));
        dl.AddCircleFilled(center + new Vector2(c1, s1) * radius, stroke * 0.5f, Slope(OuterSlope, center, box, center + new Vector2(c1, s1) * radius));

        if (stroke < 3f)
        {
            return;
        }

        // Rows: the inner slope's foot, the crest twice (a crisp edge between the slopes), the outer slope's foot.
        var inner = radius - stroke * 0.5f + 0.5f;
        var outer = radius + stroke * 0.5f - 0.5f;
        var uv = ImGui.GetFontTexUvWhitePixel();
        var count = steps + 1;
        dl.PrimReserve(steps * 12, count * 4);
        var i0 = dl.VtxCurrentIdx;
        for (var k = 0; k <= steps; k++)
        {
            var (s, c) = MathF.SinCos(start + sweep * k / steps);
            var d = new Vector2(c, s);
            dl.PrimWriteVtx(center + d * inner, uv, Slope(InnerSlope, center, box, center + d * inner));
            dl.PrimWriteVtx(center + d * radius, uv, Slope(InnerSlope, center, box, center + d * radius));
            dl.PrimWriteVtx(center + d * radius, uv, Slope(OuterSlope, center, box, center + d * radius));
            dl.PrimWriteVtx(center + d * outer, uv, Slope(OuterSlope, center, box, center + d * outer));
        }

        for (var k = 0; k < steps; k++)
        {
            var a = i0 + (uint)(4 * k);
            var b = a + 4;
            Quad(dl, a, b, b + 1, a + 1);
            Quad(dl, a + 2, b + 2, b + 3, a + 3);
        }
    }

    /// <summary>
    /// The filling moon in moonstone (G5): lit <paramref name="width"/> from 0 (new) to 1 (full), waxing from the right
    /// (<see cref="MoonGeometry.TerminatorLayers"/>), on its dark side, inside an Abyss keyline; from r 9 the lit part
    /// takes a radial gradient lit from the upper left. A new moon keeps a fine gilt outline so an empty node still shows.
    /// With <paramref name="dim"/> the lit part is flat moonstone mid (a finished gauge stepping back).
    /// </summary>
    public static void FillingMoon(ImDrawListPtr dl, Vector2 center, float radius, float width, bool dim = false)
    {
        if (!(radius > 0.5f))
        {
            return;
        }

        var segments = MoonGeometry.SegmentsFor(radius);
        var keyline = MathF.Max(1f, 0.08f * radius);
        FillDisc(dl, center, radius + keyline, segments, KeylineU32);

        // The overlay lives in its own list: FillDisc rewrites Scratch.
        var baseLit = MoonGeometry.TerminatorLayers(center, radius, width, segments, Overlay);
        var hasOverlay = Overlay.Count >= 3;
        var lit = dim ? MoonstoneDimU32 : MoonstoneU32;
        if (baseLit)
        {
            FillDisc(dl, center, radius, segments, lit);
            if (!dim)
            {
                Shade(dl, center, radius, Scratch);
            }

            if (hasOverlay)
            {
                FillPolygon(dl, Overlay, DarkSideU32);
            }
        }
        else if (hasOverlay)
        {
            FillDisc(dl, center, radius, segments, DarkSideU32);
            FillPolygon(dl, Overlay, lit);
            if (!dim)
            {
                Shade(dl, center, radius, Overlay);
            }
        }
        else
        {
            FillDisc(dl, center, radius, segments, DarkSideU32);
            dl.AddCircle(center, radius - 0.5f, GiltDimU32, segments, 1f);
        }
    }

    /// <summary>The bezel slope's colour at <paramref name="p"/> in a gauge of half-size <paramref name="box"/>.</summary>
    private static uint Slope((float, Vector4)[] stops, Vector2 center, float box, Vector2 p)
    {
        var from = center + SlopeFrom * box;
        var d = (SlopeTo - SlopeFrom) * box;
        var len2 = d.LengthSquared();
        var t = len2 > 0f ? Vector2.Dot(p - from, d) / len2 : 0f;
        return Theme.U32(MeshBuilder.Stop(stops, t));
    }

    /// <summary>A radial gradient over the convex lit polygon <paramref name="lit"/> (already filled flat): one fan mesh in three rings, from r 9.</summary>
    private static void Shade(ImDrawListPtr dl, Vector2 center, float radius, List<Vector2> lit)
    {
        var n = lit.Count;
        if (radius < LegacyMoonGlyph.ShadingMinRadius || n < 3)
        {
            return;
        }

        var highlight = center + new Vector2(-0.32f, -0.34f) * radius;
        var hub = Centroid(lit);
        var span = 1f / (1.25f * radius);
        var uv = ImGui.GetFontTexUvWhitePixel();
        ReadOnlySpan<float> rings = [1f / 3f, 2f / 3f, 1f];
        dl.PrimReserve(3 * n + 6 * n * (rings.Length - 1), 1 + rings.Length * n);
        var i0 = dl.VtxCurrentIdx;
        dl.PrimWriteVtx(hub, uv, Ramp(Vector2.Distance(hub, highlight) * span));
        foreach (var t in rings)
        {
            foreach (var p in lit)
            {
                var q = hub + (p - hub) * t;
                dl.PrimWriteVtx(q, uv, Ramp(Vector2.Distance(q, highlight) * span));
            }
        }

        for (var k = 0; k < n; k++)
        {
            Tri(dl, i0, i0 + 1 + (uint)k, i0 + 1 + (uint)((k + 1) % n));
        }

        for (var ring = 0; ring < rings.Length - 1; ring++)
        {
            var inner = i0 + 1 + (uint)(ring * n);
            var outer = inner + (uint)n;
            for (var k = 0; k < n; k++)
            {
                var next = (uint)((k + 1) % n);
                Quad(dl, inner + (uint)k, inner + next, outer + next, outer + (uint)k);
            }
        }
    }

    private static uint Ramp(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var c = t <= 0.42f ? Vector4.Lerp(MoonRamp[0], MoonRamp[1], t / 0.42f) : Vector4.Lerp(MoonRamp[1], MoonRamp[2], (t - 0.42f) / 0.58f);
        return Theme.WithAlpha(c, 1f);
    }

    private static Vector2 Centroid(List<Vector2> polygon)
    {
        var sum = Vector2.Zero;
        foreach (var p in polygon)
        {
            sum += p;
        }

        return sum / polygon.Count;
    }

    private static void FillDisc(ImDrawListPtr dl, Vector2 center, float radius, int segments, uint color)
    {
        MoonGeometry.CirclePolygon(center, radius, segments, Scratch);
        FillPolygon(dl, Scratch, color);
    }

    private static void FillPolygon(ImDrawListPtr dl, List<Vector2> polygon, uint color)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        var span = CollectionsMarshal.AsSpan(polygon);
        dl.AddConvexPolyFilled(ref MemoryMarshal.GetReference(span), span.Length, color);
    }

    private static void Tri(ImDrawListPtr dl, uint a, uint b, uint c)
    {
        dl.PrimWriteIdx((ushort)a);
        dl.PrimWriteIdx((ushort)b);
        dl.PrimWriteIdx((ushort)c);
    }

    private static void Quad(ImDrawListPtr dl, uint a, uint b, uint c, uint d)
    {
        Tri(dl, a, b, c);
        Tri(dl, a, c, d);
    }
}
