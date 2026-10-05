namespace Tsukimichi.Core.Moonfall;

/// <summary>The closest-point queries the collisions and the level checks share. Pure and allocation-free.</summary>
public static class MoonfallGeometry
{
    private const double TwoPi = Math.PI * 2;

    /// <summary>The point of the segment (ax, ay)–(bx, by) closest to (px, py).</summary>
    public static (double X, double Y) ClosestOnSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var length2 = (dx * dx) + (dy * dy);
        if (length2 <= 0)
        {
            return (ax, ay);
        }

        var t = Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / length2, 0, 1);
        return (ax + (t * dx), ay + (t * dy));
    }

    /// <summary>
    /// The point of the arc of <paramref name="radius"/> about (cx, cy), from <paramref name="start"/> through
    /// <paramref name="sweep"/> radians (0 &lt; sweep ≤ 2π), closest to (px, py).
    /// </summary>
    public static (double X, double Y) ClosestOnArc(double px, double py, double cx, double cy, double radius, double start, double sweep)
    {
        var angle = Math.Atan2(py - cy, px - cx);
        var relative = Wrap(angle - start);
        if (relative <= sweep)
        {
            return (cx + (radius * Math.Cos(angle)), cy + (radius * Math.Sin(angle)));
        }

        var ax = cx + (radius * Math.Cos(start));
        var ay = cy + (radius * Math.Sin(start));
        var bx = cx + (radius * Math.Cos(start + sweep));
        var by = cy + (radius * Math.Sin(start + sweep));
        var da = ((px - ax) * (px - ax)) + ((py - ay) * (py - ay));
        var db = ((px - bx) * (px - bx)) + ((py - by) * (py - by));
        return da <= db ? (ax, ay) : (bx, by);
    }

    /// <summary>An angle wrapped into [0, 2π).</summary>
    public static double Wrap(double radians)
    {
        var wrapped = radians % TwoPi;
        return wrapped < 0 ? wrapped + TwoPi : wrapped;
    }

    /// <summary>Degrees to radians.</summary>
    public static double Radians(double degrees) => degrees * (Math.PI / 180);

    /// <summary>
    /// The distance from (px, py) to the surface of <paramref name="peg"/> placed with its centre (or first point) at
    /// (x, y): negative inside it. Used by the level checks, not the hot path.
    /// </summary>
    public static double SurfaceDistance(MoonfallPeg peg, double x, double y, double px, double py)
    {
        ArgumentNullException.ThrowIfNull(peg);
        switch (peg.Shape)
        {
            case PegShape.Line:
            {
                var (qx, qy) = ClosestOnSegment(px, py, x, y, peg.X2 + (x - peg.X), peg.Y2 + (y - peg.Y));
                return Hypot(px - qx, py - qy) - (peg.Thickness * 0.5);
            }

            case PegShape.Arc:
            {
                var start = Radians(peg.StartDegrees);
                var (qx, qy) = ClosestOnArc(px, py, x, y, peg.Radius, start, Radians(peg.SweepDegrees));
                return Hypot(px - qx, py - qy) - (peg.Thickness * 0.5);
            }

            default:
                return Hypot(px - x, py - y) - peg.Radius;
        }
    }

    /// <summary>√(x² + y²).</summary>
    public static double Hypot(double x, double y) => Math.Sqrt((x * x) + (y * y));
}
