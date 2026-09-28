using System.Numerics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>Moon phase behind a state glyph (spec §2.1). Ring, glow and notch decorations are not part of the phase.</summary>
public enum MoonPhase
{
    /// <summary>Whole disc lit.</summary>
    Full,

    /// <summary>Right side lit; terminator is a disc of the same radius offset right by <see cref="MoonGeometry.GibbousOffset"/>.</summary>
    WaxingGibbous,

    /// <summary>Right half lit, straight terminator.</summary>
    FirstQuarter,

    /// <summary>Left side lit; mirror of <see cref="WaxingGibbous"/>.</summary>
    WaningGibbous,

    /// <summary>Nothing lit.</summary>
    New,
}

/// <summary>
/// Allocating description of a moon as two layers: a base disc in one tone and, optionally, one convex
/// overlay polygon in the opposite tone. Produced by <see cref="MoonGeometry.Filling"/>,
/// <see cref="MoonGeometry.Terminator"/> and <see cref="MoonGeometry.ForPhase"/>; mainly for tests and one-off
/// callers. Per-frame drawing uses the list-filling overloads instead.
/// </summary>
public readonly record struct MoonLayers(Vector2 Center, float Radius, int Segments, bool BaseLit, Vector2[] Overlay)
{
    /// <summary>The overlay is always the opposite tone of the base disc.</summary>
    public bool OverlayLit => !BaseLit;

    public bool HasOverlay => Overlay.Length >= 3;

    /// <summary>
    /// Lit area divided by the drawn disc area. Both are polygon areas: the base disc is rendered as the
    /// <see cref="Segments"/>-gon from <see cref="MoonGeometry.CirclePolygon"/>, so measuring against π r² would
    /// mix an inscribed polygon with the true circle and jump at the half.
    /// </summary>
    public float LitAreaFraction
    {
        get
        {
            var disc = MoonGeometry.DiscPolygonArea(Radius, Segments);
            var overlay = HasOverlay ? MoonGeometry.PolygonArea(Overlay) : 0f;
            var lit = BaseLit ? disc - overlay : overlay;
            return Math.Clamp(lit / disc, 0f, 1f);
        }
    }

    /// <summary>
    /// Width in pixels of the lit part along the equator (the horizontal line through the centre), measured on the
    /// drawn polygons: a lit lens is as wide as its equator crossing; a shadow lens on a lit disc leaves the rim to
    /// its right lit. This is what the terminator floor guarantees, so tests measure it rather than the formula.
    /// </summary>
    public float LitEquatorWidth
    {
        get
        {
            if (!HasOverlay) return BaseLit ? 2f * Radius : 0f;
            if (!MoonGeometry.EquatorCrossings(Overlay, Center.Y, out var min, out var max))
                return BaseLit ? 2f * Radius : 0f;
            return BaseLit ? Math.Max(0f, Center.X + Radius - max) : Math.Max(0f, max - min);
        }
    }

    /// <summary>Width in pixels of the dark part along the equator; the complement of <see cref="LitEquatorWidth"/>.</summary>
    public float DarkEquatorWidth => Math.Max(0f, 2f * Radius - LitEquatorWidth);
}

/// <summary>
/// Pure geometry for the moon glyphs. No ImGui dependency.
///
/// Every phase is drawn with the two-disc technique from assets/icons/render_icons.py: a base disc, then a
/// second disc whose rim is the terminator. ImGui has no clipping for draw primitives, so instead of clipping
/// the second disc to the first this class builds the intersection of the two discs as a polygon. The
/// intersection of two discs is always convex, so <c>AddConvexPolyFilled</c> renders it correctly. Crescents
/// (filling moon below one half) are not convex; they are drawn as a lit base disc with the convex
/// <em>shadow</em> lens overlaid in the unlit tone, which needs no non-convex fill either.
///
/// Polygons are emitted in increasing-angle order. With ImGui's y-down screen space that is clockwise on
/// screen, which is the winding ImGui's anti-aliased fill expects. Points on the base rim are sampled on the
/// same angular grid (2π·k/segments) as <see cref="CirclePolygon"/> so an overlay's rim chords coincide with
/// the base disc's chords and no slivers show at the edge.
/// </summary>
public static class MoonGeometry
{
    /// <summary>Terminator disc offset for the gibbous phases as a fraction of the radius (Pillow script: 0.5·r).</summary>
    public const float GibbousOffset = 0.5f;

    /// <summary>
    /// Terminator floor (glyph proposal v2.1 §3.2, imgui-notes §1): the lit side of a filling moon is never narrower
    /// at the equator than this fraction of the diameter, nor than <see cref="MinLitWidthPx"/>, for any fraction
    /// strictly between 0 and 1; the same holds for the dark side near 1. A 17/612 node shows a crescent, not a full disc.
    /// </summary>
    public const float MinLitFraction = 0.10f;

    /// <summary>Pixel floor for the lit (or dark) equator width of a filling moon; 1.5 px survives anti-aliasing.</summary>
    public const float MinLitWidthPx = 1.5f;

    /// <summary>Smallest circle segment count; keeps a 6 px moon from looking like a decagon.</summary>
    public const int MinSegments = 12;

    public const int MaxSegments = 96;

    /// <summary>Maximum chord error in pixels used to pick the segment count (ImGui's default is 0.30).</summary>
    private const float CircleMaxError = 0.30f;

    private const float TwoPi = MathF.PI * 2f;

    /// <summary>Maps a quest state to the phase behind its glyph (spec §2.1).</summary>
    public static MoonPhase PhaseOf(QuestState state) => state switch
    {
        QuestState.Completed => MoonPhase.Full,
        QuestState.Accepted => MoonPhase.WaxingGibbous,
        QuestState.Ready => MoonPhase.FirstQuarter,
        QuestState.ReadyOnOtherJob => MoonPhase.FirstQuarter,
        QuestState.DoneThisCycle => MoonPhase.WaningGibbous,
        QuestState.Blocked => MoonPhase.New,
        QuestState.Foreclosed => MoonPhase.New,
        QuestState.Unknown => MoonPhase.New,
        _ => MoonPhase.New,
    };

    /// <summary>
    /// Segment count for a circle of the given pixel radius, following ImGui's own auto-segment formula, rounded up
    /// to a multiple of four so the poles and the equator points fall on the angular grid.
    /// </summary>
    public static int SegmentsFor(float radius)
    {
        if (!(radius > 0f)) return MinSegments;
        var err = MathF.Min(CircleMaxError, radius);
        var n = (int)MathF.Ceiling(MathF.PI / MathF.Acos(1f - err / radius));
        n = (n + 3) / 4 * 4;
        return Math.Clamp(n, MinSegments, MaxSegments);
    }

    /// <summary>Shoelace area of a simple polygon (absolute value).</summary>
    public static float PolygonArea(ReadOnlySpan<Vector2> polygon)
    {
        if (polygon.Length < 3) return 0f;
        double sum = 0;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            sum += (double)polygon[j].X * polygon[i].Y - (double)polygon[i].X * polygon[j].Y;
        }

        return (float)Math.Abs(sum * 0.5);
    }

    /// <summary>Area of the regular polygon <see cref="CirclePolygon"/> produces: (n/2)·r²·sin(2π/n).</summary>
    public static float DiscPolygonArea(float radius, int segments)
    {
        segments = Math.Max(3, segments);
        return 0.5f * segments * radius * radius * MathF.Sin(TwoPi / segments);
    }

    /// <summary>
    /// Leftmost and rightmost x where the horizontal line y = <paramref name="y"/> crosses the polygon's edges.
    /// False when the line misses it.
    /// </summary>
    public static bool EquatorCrossings(ReadOnlySpan<Vector2> polygon, float y, out float min, out float max)
    {
        min = float.PositiveInfinity;
        max = float.NegativeInfinity;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[j];
            var b = polygon[i];
            if (a.Y == b.Y)
            {
                if (a.Y != y) continue;        // horizontal edge on the line: both ends count
                Widen(a.X, ref min, ref max);
                Widen(b.X, ref min, ref max);
                continue;
            }

            if ((a.Y <= y) == (b.Y <= y)) continue;   // both ends on the same side
            Widen(a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y), ref min, ref max);
        }

        return min <= max;

        static void Widen(float x, ref float min, ref float max)
        {
            if (x < min) min = x;
            if (x > max) max = x;
        }
    }

    /// <summary>Regular polygon approximating the disc, vertices at angles 2π·k/segments.</summary>
    public static void CirclePolygon(Vector2 center, float radius, int segments, List<Vector2> output)
    {
        output.Clear();
        segments = Math.Max(3, segments);
        var step = TwoPi / segments;
        for (var k = 0; k < segments; k++)
        {
            var (sin, cos) = MathF.SinCos(k * step);
            output.Add(new Vector2(center.X + radius * cos, center.Y + radius * sin));
        }
    }

    /// <summary>
    /// Half of the base disc as a convex polygon: the rim arc from the top pole to the bottom pole through the
    /// right (or left) equator point, closed by the vertical chord through the center.
    /// </summary>
    public static void HalfDiscPolygon(Vector2 center, float radius, int segments, bool right, List<Vector2> output)
    {
        output.Clear();
        if (right)
            AppendArcOnGrid(center, radius, -MathF.PI / 2f, MathF.PI / 2f, segments, output);
        else
            AppendArcOnGrid(center, radius, MathF.PI / 2f, 3f * MathF.PI / 2f, segments, output);
    }

    /// <summary>
    /// Intersection of the base disc (center, radius) with another disc as a convex polygon. Empty when the discs
    /// do not overlap; the whole base disc when it lies inside the other disc.
    /// </summary>
    public static void LensPolygon(Vector2 center, float radius, Vector2 otherCenter, float otherRadius, int segments, List<Vector2> output)
    {
        output.Clear();
        if (!(radius > 0f) || !(otherRadius > 0f)) return;

        var delta = otherCenter - center;
        var d = delta.Length();
        if (d >= radius + otherRadius) return;
        if (d + radius <= otherRadius)
        {
            CirclePolygon(center, radius, segments, output);
            return;
        }

        if (d + otherRadius <= radius)
        {
            CirclePolygon(otherCenter, otherRadius, Math.Max(MinSegments, (int)MathF.Ceiling(segments * otherRadius / radius)), output);
            return;
        }

        // Distance from each center to the common chord, along the line of centers.
        var a = (d * d + radius * radius - otherRadius * otherRadius) / (2f * d);
        var b = d - a;
        var alpha = MathF.Acos(Math.Clamp(a / radius, -1f, 1f));      // half-angle of the base arc inside the other disc
        var beta = MathF.Acos(Math.Clamp(b / otherRadius, -1f, 1f));  // half-angle of the other arc inside the base disc
        var thetaU = MathF.Atan2(delta.Y, delta.X);

        // Base rim from cusp to cusp, passing the direction of the other center.
        AppendArcOnGrid(center, radius, thetaU - alpha, thetaU + alpha, segments, output);

        // Terminator: the other disc's rim back from the second cusp to the first, passing the direction of our center.
        // Same arc length per segment as the base circle; endpoints are the cusps already emitted.
        var start = thetaU + MathF.PI - beta;
        var end = thetaU + MathF.PI + beta;
        var arcLength = (end - start) * otherRadius;
        var perSegment = TwoPi * radius / Math.Max(3, segments);
        var n = Math.Clamp((int)MathF.Ceiling(arcLength / perSegment), 3, 4 * Math.Max(3, segments));
        for (var i = 1; i < n; i++)
        {
            var t = start + (end - start) * i / n;
            var (sin, cos) = MathF.SinCos(t);
            output.Add(new Vector2(otherCenter.X + otherRadius * cos, otherCenter.Y + otherRadius * sin));
        }

        if (output.Count < 3) output.Clear();
    }

    /// <summary>
    /// Layers for a state phase. Returns whether the base disc is lit; <paramref name="overlay"/> receives the convex
    /// overlay polygon in the opposite tone (empty when the phase needs none).
    /// </summary>
    public static bool PhaseLayers(Vector2 center, float radius, MoonPhase phase, int segments, List<Vector2> overlay)
    {
        overlay.Clear();
        switch (phase)
        {
            case MoonPhase.Full:
                return true;
            case MoonPhase.WaxingGibbous:
                LensPolygon(center, radius, center + new Vector2(GibbousOffset * radius, 0f), radius, segments, overlay);
                return false;
            case MoonPhase.FirstQuarter:
                HalfDiscPolygon(center, radius, segments, right: true, overlay);
                return false;
            case MoonPhase.WaningGibbous:
                LensPolygon(center, radius, center - new Vector2(GibbousOffset * radius, 0f), radius, segments, overlay);
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Smallest lit (or dark) equator width for a moon of this radius, as a fraction of the diameter:
    /// max(<see cref="MinLitFraction"/>, <see cref="MinLitWidthPx"/> / 2r), never above one half.
    /// </summary>
    public static float TerminatorFloor(float radius)
    {
        if (!(radius > 0f)) return 0.5f;
        return MathF.Min(0.5f, MathF.Max(MinLitFraction, MinLitWidthPx / (2f * radius)));
    }

    /// <summary>
    /// The lit width the filling moon draws for a progress <paramref name="fraction"/>: 0 and 1 stay exact, everything
    /// between is mapped into [floor, 1 − floor] so both the first sliver and the last gap are visible.
    /// </summary>
    public static float FlooredFraction(float fraction, float radius)
    {
        fraction = float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);
        if (fraction <= 0f) return 0f;
        if (fraction >= 1f) return 1f;
        var floor = TerminatorFloor(radius);
        return floor + (1f - 2f * floor) * fraction;
    }

    /// <summary>
    /// Layers for a filling moon: lit <paramref name="fraction"/> from 0 (new) to 1 (full), filling right to left like
    /// a waxing moon, with the terminator floor applied (<see cref="FlooredFraction"/>). Returns whether the base disc
    /// is lit; <paramref name="overlay"/> receives the convex overlay polygon in the opposite tone.
    /// </summary>
    public static bool FillingLayers(Vector2 center, float radius, float fraction, int segments, List<Vector2> overlay) =>
        TerminatorLayers(center, radius, FlooredFraction(fraction, radius), segments, overlay);

    /// <summary>
    /// Exact terminator geometry: a circle through both poles whose equator point sits at
    /// x = center + (1 − 2·<paramref name="litWidth"/>)·radius, so the lit width at the equator is exactly
    /// <paramref name="litWidth"/> of the diameter (no floor). Returns whether the base disc is lit;
    /// <paramref name="overlay"/> receives the convex overlay polygon in the opposite tone.
    /// </summary>
    public static bool TerminatorLayers(Vector2 center, float radius, float litWidth, int segments, List<Vector2> overlay)
    {
        overlay.Clear();
        var fraction = float.IsNaN(litWidth) ? 0f : Math.Clamp(litWidth, 0f, 1f);
        const float Epsilon = 0.005f;

        if (fraction <= Epsilon) return false;          // new: dark disc only
        if (fraction >= 1f - Epsilon) return true;      // full: lit disc only

        var w = (2f * fraction - 1f) * radius;          // signed equator terminator offset; >0 past the half
        if (MathF.Abs(w) < Epsilon * radius)
        {
            HalfDiscPolygon(center, radius, segments, right: true, overlay);
            return false;
        }

        // Circle through the poles (0, ±r) and the equator point (−w, 0) relative to the center, for a lit
        // lens on the right when w > 0. Center at (+h, 0), radius h + w, with h = (r² − w²) / (2w).
        var wa = MathF.Abs(w);
        var h = (radius * radius - wa * wa) / (2f * wa);
        var terminatorRadius = h + wa;

        if (w > 0f)
        {
            // Gibbous: dark base, lit lens bulging left past the center.
            LensPolygon(center, radius, center + new Vector2(h, 0f), terminatorRadius, segments, overlay);
            return false;
        }

        // Crescent: lit base, shadow lens (bulging right past the center) drawn dark on top.
        LensPolygon(center, radius, center - new Vector2(h, 0f), terminatorRadius, segments, overlay);
        return true;
    }

    /// <summary>Allocating form of <see cref="PhaseLayers"/>.</summary>
    public static MoonLayers ForPhase(Vector2 center, float radius, MoonPhase phase, int segments = 0)
    {
        if (segments <= 0) segments = SegmentsFor(radius);
        var overlay = new List<Vector2>();
        var baseLit = PhaseLayers(center, radius, phase, segments, overlay);
        return new MoonLayers(center, radius, segments, baseLit, overlay.ToArray());
    }

    /// <summary>Allocating form of <see cref="FillingLayers"/> (floored).</summary>
    public static MoonLayers Filling(Vector2 center, float radius, float fraction, int segments = 0)
    {
        if (segments <= 0) segments = SegmentsFor(radius);
        var overlay = new List<Vector2>();
        var baseLit = FillingLayers(center, radius, fraction, segments, overlay);
        return new MoonLayers(center, radius, segments, baseLit, overlay.ToArray());
    }

    /// <summary>Allocating form of <see cref="TerminatorLayers"/> (exact, no floor).</summary>
    public static MoonLayers Terminator(Vector2 center, float radius, float litWidth, int segments = 0)
    {
        if (segments <= 0) segments = SegmentsFor(radius);
        var overlay = new List<Vector2>();
        var baseLit = TerminatorLayers(center, radius, litWidth, segments, overlay);
        return new MoonLayers(center, radius, segments, baseLit, overlay.ToArray());
    }

    /// <summary>
    /// The lit region of a state phase as one convex polygon in increasing-angle order: the whole disc polygon for
    /// <see cref="MoonPhase.Full"/>, empty for <see cref="MoonPhase.New"/>.
    /// </summary>
    public static Vector2[] LitPolygon(Vector2 center, float radius, MoonPhase phase, int segments = 0)
    {
        if (segments <= 0) segments = SegmentsFor(radius);
        var points = new List<Vector2>();
        if (phase == MoonPhase.Full)
        {
            CirclePolygon(center, radius, segments, points);
            return points.ToArray();
        }

        PhaseLayers(center, radius, phase, segments, points);
        return points.ToArray();
    }

    /// <summary>
    /// Appends the rim arc from <paramref name="from"/> to <paramref name="to"/> (radians, increasing): the exact start
    /// point, every grid angle 2π·k/segments strictly inside the range, and the exact end point.
    /// </summary>
    private static void AppendArcOnGrid(Vector2 center, float radius, float from, float to, int segments, List<Vector2> output)
    {
        segments = Math.Max(3, segments);
        var step = TwoPi / segments;
        const float GridEpsilon = 1e-3f; // in grid steps; skips grid points that coincide with an endpoint

        output.Add(PointAt(center, radius, from));

        var kStart = (int)MathF.Floor(from / step + GridEpsilon) + 1;
        var kEnd = (int)MathF.Ceiling(to / step - GridEpsilon) - 1;
        for (var k = kStart; k <= kEnd; k++)
        {
            output.Add(PointAt(center, radius, k * step));
        }

        output.Add(PointAt(center, radius, to));
    }

    private static Vector2 PointAt(Vector2 center, float radius, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return new Vector2(center.X + radius * cos, center.Y + radius * sin);
    }
}
