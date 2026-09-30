using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>How much of the halo gauge a box of half-size R draws (glyph proposal v2.1 §3.2, accessibility A4).</summary>
public enum HaloMode
{
    /// <summary>R &lt; 8 (under a 16 px box): no gauge at all; the caller writes the number.</summary>
    NumberOnly,

    /// <summary>8 ≤ R &lt; 12: track and arc only; the number goes beside it.</summary>
    Ring,

    /// <summary>R ≥ 12 (a 24 px box and up): track, arc and the filling-moon core.</summary>
    Core,
}

/// <summary>
/// Pure geometry of the halo gauge, the progress glyph (glyph proposal v2.1 §3.2, imgui-notes §3): a track ring at
/// 0.80 R, a gold arc from 12 o'clock clockwise whose length is the floored fraction, round caps, and from R 12 a core
/// disc inside a gap. Angles are radians in ImGui's y-down screen space, so increasing angles run clockwise.
/// </summary>
public static class GaugeGeometry
{
    /// <summary>Track radius as a fraction of R.</summary>
    public const float TrackFraction = 0.80f;

    /// <summary>Smallest visual floor at either end of the arc.</summary>
    public const float MinEpsilon = 0.06f;

    /// <summary>What the floor must show beyond the cap, in pixels: a pip past the round cap is visible from 1.5 px.</summary>
    public const float CapAllowancePx = 1.5f;

    /// <summary>Smallest R that draws a gauge; below it the caller writes the number only.</summary>
    public const float RingMinRadius = 8f;

    /// <summary>Smallest R that draws the core (a 24 px box); below it track and arc only.</summary>
    public const float CoreMinRadius = 12f;

    /// <summary>The arc starts at 12 o'clock.</summary>
    public const float StartAngle = -MathF.PI / 2f;

    private const float TwoPi = 2f * MathF.PI;

    /// <summary>Which parts a box of half-size <paramref name="radius"/> draws.</summary>
    public static HaloMode ModeFor(float radius) =>
        !(radius >= RingMinRadius) ? HaloMode.NumberOnly : radius < CoreMinRadius ? HaloMode.Ring : HaloMode.Core;

    /// <summary>Track and arc stroke: max(2, 0.18 R) px, so low-vision acuity holds at every size (A4).</summary>
    public static float Stroke(float radius) => MathF.Max(2f, 0.18f * radius);

    /// <summary>Radius of the track circle (the centre line of the stroke).</summary>
    public static float TrackRadius(float radius) => TrackFraction * radius;

    /// <summary>Gap between the track's inner edge and the core: max(1, 0.10 R).</summary>
    public static float Gap(float radius) => MathF.Max(1f, 0.10f * radius);

    /// <summary>Core radius: 0.80 R − stroke/2 − gap (0.61 R from R 12 up), and 0 below R 12 where no core is drawn.</summary>
    public static float CoreRadius(float radius) => CoreRadius(radius, Stroke(radius));

    /// <summary>Core radius for a gauge drawn with its own <paramref name="stroke"/> (the high-contrast palette's is thicker).</summary>
    public static float CoreRadius(float radius, float stroke) =>
        radius >= CoreMinRadius ? TrackRadius(radius) - stroke * 0.5f - Gap(radius) : 0f;

    /// <summary>Visual floor for a box of half-size R with its own stroke.</summary>
    public static float FloorEpsilon(float radius) => FloorEpsilon(radius, Stroke(radius));

    /// <summary>
    /// Visual floor ε = max(0.06, (stroke + 1.5 px) / (2π · 0.80 R)): the shortest arc for any fraction above 0 is a
    /// cap plus a visible pip, and the longest below 1 leaves as much dark gap.
    /// </summary>
    public static float FloorEpsilon(float radius, float stroke)
    {
        var circumference = TwoPi * TrackRadius(radius);
        if (!(circumference > 0f)) return 0.5f;
        return MathF.Min(0.5f, MathF.Max(MinEpsilon, (stroke + CapAllowancePx) / circumference));
    }

    /// <summary>The fraction clamped to 0..1, NaN as 0.</summary>
    public static float Clamp01(float fraction) => float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);

    /// <summary>ε + (1 − 2ε)·f for 0 &lt; f &lt; 1; exactly 0 and 1 at the ends.</summary>
    public static float VisualFraction(float fraction, float epsilon)
    {
        var f = Clamp01(fraction);
        if (f <= 0f) return 0f;
        if (f >= 1f) return 1f;
        return epsilon + (1f - 2f * epsilon) * f;
    }

    /// <summary>The arc's sweep in radians for a progress fraction on a box of half-size R: 0 at 0, exactly 2π at 1.</summary>
    public static float Sweep(float fraction, float radius) => Sweep(fraction, radius, Stroke(radius));

    /// <summary>The arc's sweep for a gauge drawn with its own <paramref name="stroke"/>: the floor grows with the cap.</summary>
    public static float Sweep(float fraction, float radius, float stroke)
    {
        var v = VisualFraction(fraction, FloorEpsilon(radius, stroke));
        return v >= 1f ? TwoPi : TwoPi * v;
    }

    /// <summary>Angle of a point at visual fraction <paramref name="visual"/> along the track: −π/2 + 2π·v.</summary>
    public static float AngleAt(float visual) => StartAngle + TwoPi * visual;

    /// <summary>The arc's end angle for a progress fraction.</summary>
    public static float EndAngle(float fraction, float radius) => StartAngle + Sweep(fraction, radius);

    /// <summary>The point on the track at <paramref name="angle"/>.</summary>
    public static Vector2 TrackPoint(Vector2 center, float radius, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return center + new Vector2(cos, sin) * TrackRadius(radius);
    }

    /// <summary>Centres of the two round caps (discs of radius stroke/2): the start at 12 o'clock and the arc's end.</summary>
    public static (Vector2 Start, Vector2 End) Caps(Vector2 center, float radius, float fraction) =>
        Caps(center, radius, fraction, Stroke(radius));

    /// <summary>Cap centres for a gauge drawn with its own <paramref name="stroke"/>.</summary>
    public static (Vector2 Start, Vector2 End) Caps(Vector2 center, float radius, float fraction, float stroke) =>
        (TrackPoint(center, radius, StartAngle), TrackPoint(center, radius, StartAngle + Sweep(fraction, radius, stroke)));

    /// <summary>Arc length in pixels along the track centre line.</summary>
    public static float ArcLength(float fraction, float radius) => Sweep(fraction, radius) * TrackRadius(radius);

    /// <summary>The core moon's own floor: max(0.10, 1.5 px / (2 rc)), capped at one half.</summary>
    public static float CoreFloor(float coreRadius) =>
        !(coreRadius > 0f) ? 0.5f : MathF.Min(0.5f, MathF.Max(0.10f, 1.5f / (2f * coreRadius)));

    /// <summary>Lit equator width of the core's filling moon for a progress fraction (floored; exact at 0 and 1).</summary>
    public static float CoreLitWidth(float fraction, float coreRadius) =>
        VisualFraction(fraction, CoreFloor(coreRadius));

    /// <summary>Segments for the arc: the track circle's density over the sweep, at least six.</summary>
    public static int ArcSegments(int circleSegments, float sweep) =>
        Math.Max(6, (int)MathF.Ceiling(circleSegments * sweep / TwoPi));
}
