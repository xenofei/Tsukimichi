using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>How the lit part of a moon is bounded, in unit-disc coordinates (x right, y down, fractions of r).</summary>
public enum LitShape
{
    /// <summary>Nothing lit.</summary>
    None,

    /// <summary>The whole disc lit: no terminator.</summary>
    Full,

    /// <summary>Right side lit up to a terminator circle through both poles (the filling moon, Ready, Accepted).</summary>
    Terminator,

    /// <summary>Left side lit: the disc intersected with a disc of the same radius offset left by <see cref="MoonGeometry.GibbousOffset"/> (DoneThisCycle).</summary>
    WaningLens,
}

/// <summary>
/// The lit region of a moon glyph, as a pure function of a unit-disc point, so the interior detail can be clipped to
/// it without ImGui clipping (imgui-notes §2b). Every region's boundary crosses each horizontal line inside the lit
/// part once, so a point is lit when it lies on the lit side of <see cref="BoundaryX"/>; <see cref="Depth"/> is that
/// horizontal distance, positive on the lit side.
/// </summary>
public readonly struct LitRegion
{
    private readonly float xe;   // terminator's equator crossing (Terminator only): 1 − 2·litWidth
    private readonly float h;    // terminator circle centre x (Terminator only; 0 for a straight terminator)
    private readonly float rt;   // terminator circle radius (Terminator only; 0 for a straight terminator)

    private LitRegion(LitShape shape, float litWidth)
    {
        Shape = shape;
        LitWidth = litWidth;
        xe = 1f - 2f * litWidth;
        if (shape == LitShape.Terminator && MathF.Abs(xe) > 1e-4f)
        {
            h = (xe * xe - 1f) / (2f * xe);
            rt = MathF.Sqrt(h * h + 1f);
        }
    }

    public LitShape Shape { get; }

    /// <summary>Lit width at the equator as a fraction of the diameter (1 for full, 0.75 for the waning lens).</summary>
    public float LitWidth { get; }

    /// <summary>Whether the region has a terminator (the glow band follows it); full and empty moons have none.</summary>
    public bool HasTerminator => Shape is LitShape.Terminator or LitShape.WaningLens;

    /// <summary>Whether the region is lit on the left (the boundary is to its right).</summary>
    public bool LitOnLeft => Shape == LitShape.WaningLens;

    public static LitRegion None => new(LitShape.None, 0f);

    public static LitRegion Full => new(LitShape.Full, 1f);

    public static LitRegion WaningLens => new(LitShape.WaningLens, 0.75f);

    /// <summary>
    /// The right-lit region of the exact terminator geometry (<see cref="MoonGeometry.TerminatorLayers"/>) for a lit
    /// equator width: 0.5 is the first quarter, 0.6 the Accepted gibbous, anything else a filling moon's floored width.
    /// Widths within the geometry's epsilon of 0 or 1 are empty or full, as the drawn layers are.
    /// </summary>
    public static LitRegion ForWidth(float litWidth)
    {
        litWidth = float.IsNaN(litWidth) ? 0f : Math.Clamp(litWidth, 0f, 1f);
        if (litWidth <= 0.005f) return None;
        if (litWidth >= 0.995f) return Full;
        return new LitRegion(LitShape.Terminator, litWidth);
    }

    /// <summary>
    /// Half-height of the lit part's boundary: the terminator runs pole to pole (1); the waning lens meets the rim at
    /// its cusps, y = ±√(1 − 0.25²) ≈ 0.968.
    /// </summary>
    public float BoundaryHalfHeight => Shape == LitShape.WaningLens ? MathF.Sqrt(1f - 0.25f * 0.25f) : 1f;

    /// <summary>
    /// x of the region's boundary at height <paramref name="y"/> (|y| ≤ 1): the terminator for right-lit regions, the
    /// offset disc's rim for the waning lens. Full and empty regions return the rim on the dark side (−∞ / +∞ clamp).
    /// </summary>
    public float BoundaryX(float y)
    {
        var yy = Math.Clamp(y * y, 0f, 1f);
        switch (Shape)
        {
            case LitShape.Full:
                return -2f;
            case LitShape.None:
                return 2f;
            case LitShape.WaningLens:
                return -MoonGeometry.GibbousOffset + MathF.Sqrt(1f - yy);
            default:
                if (rt == 0f) return 0f;
                // Circle (h, 0) radius rt through the poles; the arc inside the disc is the branch nearer the centre.
                return h - MathF.Sign(h) * MathF.Sqrt(MathF.Max(0f, rt * rt - yy));
        }
    }

    /// <summary>
    /// Signed horizontal distance from the boundary into the lit side for a unit-disc point: positive lit, negative
    /// dark. Only the boundary is considered; the caller keeps points inside the disc.
    /// </summary>
    public float Depth(Vector2 p)
    {
        switch (Shape)
        {
            case LitShape.Full:
                return 2f;
            case LitShape.None:
                return -2f;
            case LitShape.WaningLens:
                return BoundaryX(p.Y) - p.X;
            default:
                return p.X - BoundaryX(p.Y);
        }
    }

    /// <summary>Whether a unit-disc point is lit.</summary>
    public bool IsLit(Vector2 p) => Depth(p) >= 0f;

    /// <summary>
    /// The point itself when lit, otherwise moved horizontally onto the boundary and kept inside the unit disc. Used
    /// to clip soft detail meshes to the lit part: vertices on the dark side collapse onto the terminator.
    /// </summary>
    public Vector2 Clamp(Vector2 p)
    {
        if (Shape == LitShape.Full || IsLit(p)) return p;
        if (Shape == LitShape.None) return Vector2.Zero;

        var q = new Vector2(BoundaryX(p.Y), p.Y);
        var length = q.Length();
        return length > 1f ? q / length : q;
    }
}

/// <summary>A soft sea: an ellipse in unit-disc coordinates, rotated by <see cref="RotationDegrees"/> (y down, so positive is clockwise).</summary>
public readonly record struct Mare(Vector2 Center, Vector2 Radii, float RotationDegrees, float GoldAlpha, float SilverAlpha);

/// <summary>A crater ring in unit-disc coordinates; <see cref="HasShadowArc"/> for the largest only.</summary>
public readonly record struct Crater(Vector2 Center, float Radius, bool HasShadowArc);

/// <summary>
/// The one fixed "map" of the moons' interior detail (glyph proposal v2.1 §3.6, imgui-notes §2b): three maria, three
/// craters, the terminator glow band and the rim vignette, drawn on lit parts from r 12 and clipped to the lit region.
/// Pure data and arithmetic; MoonGlyph paints it.
/// </summary>
public static class MoonDetail
{
    /// <summary>Radius from which the detail is drawn on a lit part.</summary>
    public const float MinRadius = 12f;

    /// <summary>Most draw-list primitives the detail may add to one glyph (3 maria, 7 crater parts, the band, the vignette).</summary>
    public const int PrimitiveBudget = 12;

    public static readonly Mare[] Maria =
    [
        new(new Vector2(-0.30f, -0.24f), new Vector2(0.32f, 0.24f), -25f, 0.20f, 0.14f),
        new(new Vector2(0.30f, 0.06f), new Vector2(0.24f, 0.20f), -15f, 0.18f, 0.13f),
        new(new Vector2(-0.10f, 0.40f), new Vector2(0.30f, 0.14f), 10f, 0.18f, 0.13f),
    ];

    public static readonly Crater[] Craters =
    [
        new(new Vector2(0.34f, -0.46f), 0.120f, HasShadowArc: true),
        new(new Vector2(-0.50f, 0.30f), 0.095f, HasShadowArc: false),
        new(new Vector2(0.10f, 0.60f), 0.075f, HasShadowArc: false),
    ];

    /// <summary>Mare edge: alpha is full out to this fraction of the ellipse radius, then ramps to 0 at the edge.</summary>
    public const float MareSoftStart = 0.70f;

    /// <summary>Crater floor, shadow wall and highlight wall alphas (gold; the highlight is 0.30 on silver).</summary>
    public const float CraterFloorAlpha = 0.14f;
    public const float CraterShadowAlpha = 0.30f;
    public const float CraterHighlightGold = 0.38f;
    public const float CraterHighlightSilver = 0.30f;

    /// <summary>Shadow wall: radius fraction and arc (degrees clockwise from 3 o'clock) on the upper-left inner wall.</summary>
    public const float CraterShadowRadius = 0.84f;
    public const float CraterShadowFrom = 170f;
    public const float CraterShadowTo = 290f;

    /// <summary>Highlight wall on the lower-right inner wall.</summary>
    public const float CraterHighlightRadius = 0.96f;
    public const float CraterHighlightFrom = -10f;
    public const float CraterHighlightTo = 110f;

    /// <summary>Crater wall stroke as a fraction of r, never under 1 px.</summary>
    public const float CraterLineFraction = 0.035f;

    /// <summary>Terminator glow band width (fraction of r) on the lit side, and its alpha (MoonHigh on gold, white on silver).</summary>
    public const float BandWidth = 0.12f;
    public const float BandGold = 0.30f;
    public const float BandSilver = 0.25f;

    /// <summary>Rim vignette: Umbra rising quadratically from 0 at <see cref="VignetteStart"/>·r to the alpha at the rim.</summary>
    public const float VignetteStart = 0.72f;
    public const float VignetteGold = 0.16f;
    public const float VignetteSilver = 0.12f;

    /// <summary>
    /// How a mark of radius <paramref name="extent"/> at <paramref name="center"/> meets the lit region: 0 when its
    /// centre is dark (skip it), 0.5 when it straddles the boundary (half alpha), 1 when wholly lit.
    /// </summary>
    public static float Visibility(LitRegion region, Vector2 center, float extent)
    {
        var depth = region.Depth(center);
        if (depth < 0f) return 0f;
        return depth < extent ? 0.5f : 1f;
    }

    /// <summary>Vignette alpha factor (0..1) at a radius fraction: quadratic from <see cref="VignetteStart"/> to the rim.</summary>
    public static float VignetteRamp(float radiusFraction)
    {
        var t = Math.Clamp((radiusFraction - VignetteStart) / (1f - VignetteStart), 0f, 1f);
        return t * t;
    }

    /// <summary>
    /// Band width at height <paramref name="y"/>: <see cref="BandWidth"/> at the equator, tapering to 0 where the
    /// boundary meets the rim, and never wider than the lit part at that height.
    /// </summary>
    public static float BandWidthAt(LitRegion region, float y)
    {
        if (!region.HasTerminator) return 0f;
        var half = region.BoundaryHalfHeight;
        var t = Math.Clamp(y / half, -1f, 1f);
        var taper = BandWidth * MathF.Sqrt(1f - t * t);
        var boundary = region.BoundaryX(y);
        var rim = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
        var room = region.LitOnLeft ? boundary + rim : rim - boundary;
        return Math.Clamp(taper, 0f, MathF.Max(0f, room));
    }
}
