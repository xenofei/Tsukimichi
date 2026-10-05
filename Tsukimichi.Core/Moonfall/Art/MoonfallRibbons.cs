using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// Where a style-shot ribbon goes (spec-rich2.md §3; the open Minor "the LONG SHOT callout covers one peg", designer N9
/// and UX m1): by the same clearance rule as the framing, at least <see cref="Keep"/> units from every live peg's edge,
/// never over the launcher's swing nor another ribbon still showing, in the open sky of the board; the nearest such spot to
/// the preferred one wins. Allocation-free: the search walks a fixed grid.
/// </summary>
public static class MoonfallRibbons
{
    /// <summary>The least clearance from a live piece's edge, units.</summary>
    public const float Keep = 6f;

    /// <summary>The launcher's swing, kept clear: the pivot and its reach.</summary>
    public const float SwingReach = 110f;

    /// <summary>The grid step of the search, units.</summary>
    public const float Step = 6f;

    /// <summary>Where a ribbon is preferred: the upper sky, right of the launcher (as the approved mock).</summary>
    public static readonly Vector2 Preferred = new(560f, 150f);

    /// <summary>
    /// The centre for a ribbon of <paramref name="size"/> (units) clear of every live piece in
    /// <paramref name="pieces"/> ((x, y, radius); a brick as its capsule's bounding circles is fine), of the launcher and
    /// of <paramref name="taken"/> ((x0, y0, x1, y1) rectangles of ribbons showing); null when nothing in the opening fits.
    /// </summary>
    public static Vector2? Place(ReadOnlySpan<Vector3> pieces, Vector2 size, ReadOnlySpan<Vector4> taken)
    {
        var half = size / 2;
        Vector2? best = null;
        var bestDistance = float.MaxValue;
        // The opening, less a margin for the ribbon itself, above the bucket's lane.
        for (var y = 41f + half.Y + 4; y <= 520f - half.Y; y += Step)
        {
            for (var x = 75.5f + half.X + 4; x <= 724.5f - half.X - 4; x += Step)
            {
                var c = new Vector2(x, y);
                if (!Fits(c, half, pieces, taken))
                {
                    continue;
                }

                var d = Vector2.DistanceSquared(c, Preferred);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }
        }

        return best;
    }

    /// <summary>Whether a ribbon centred at <paramref name="c"/> with half-size <paramref name="half"/> keeps its clearances.</summary>
    public static bool Fits(Vector2 c, Vector2 half, ReadOnlySpan<Vector3> pieces, ReadOnlySpan<Vector4> taken)
    {
        if (RectDistance(c, half, MoonfallFramingCheck.Pivot) < SwingReach)
        {
            return false;
        }

        foreach (var p in pieces)
        {
            if (RectDistance(c, half, new Vector2(p.X, p.Y)) - p.Z < Keep)
            {
                return false;
            }
        }

        foreach (var t in taken)
        {
            if (c.X + half.X > t.X - 4 && c.X - half.X < t.Z + 4 && c.Y + half.Y > t.Y - 4 && c.Y - half.Y < t.W + 4)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The distance from point <paramref name="p"/> to the rectangle centred at <paramref name="c"/>, 0 inside.</summary>
    public static float RectDistance(Vector2 c, Vector2 half, Vector2 p)
    {
        var dx = MathF.Max(MathF.Abs(p.X - c.X) - half.X, 0);
        var dy = MathF.Max(MathF.Abs(p.Y - c.Y) - half.Y, 0);
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
