using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The distance from every point of the board to the nearest piece's edge (framecheck.piece_distance): pegs, bricks
/// (straight and curved capsules, as the engine shapes them) and every mover along its whole path (48 places a period).
/// One cell a board unit (800 × 600, sampled at cell centres), computed only within <see cref="Far"/> of each piece; beyond
/// that the distance reads <see cref="Far"/>. The framing (F3a), the small lights (F5), the motion masks and the veil
/// all read it. Built off the framework thread with the scene; read-only after.
/// </summary>
public sealed class MoonfallClearance
{
    public const int Width = 800;
    public const int Height = 600;

    /// <summary>The farthest distance kept, units: anything beyond reads this.</summary>
    public const float Far = 96f;

    private readonly float[] distance;

    private MoonfallClearance(float[] distance) => this.distance = distance;

    /// <summary>The field of every piece of <paramref name="level"/>.</summary>
    public static MoonfallClearance For(MoonfallLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return For(level.Pegs);
    }

    /// <summary>The field of <paramref name="pieces"/>.</summary>
    public static MoonfallClearance For(IReadOnlyList<MoonfallPeg> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        var d = new float[Width * Height];
        Array.Fill(d, Far);
        foreach (var piece in pieces)
        {
            switch (piece.Shape)
            {
                case PegShape.Line:
                    Stamp(d, MathF.Min((float)piece.X, (float)piece.X2), MathF.Min((float)piece.Y, (float)piece.Y2),
                        MathF.Max((float)piece.X, (float)piece.X2), MathF.Max((float)piece.Y, (float)piece.Y2),
                        (x, y) => LineCapsule(x, y, piece));
                    break;
                case PegShape.Arc:
                    var r = (float)(piece.Radius + (piece.Thickness * 0.5));
                    Stamp(d, (float)piece.X - r, (float)piece.Y - r, (float)piece.X + r, (float)piece.Y + r, (x, y) => ArcCapsule(x, y, piece), (x, y) => ArcBound(x, y, piece));
                    break;
                default:
                    foreach (var (px, py) in Path(piece))
                    {
                        StampCircle(d, px, py, (float)piece.Radius);
                    }

                    break;
            }
        }

        return new MoonfallClearance(d);
    }

    /// <summary>The places a round peg takes: its own, or 48 along a mover's path (as the engine moves it).</summary>
    public static IEnumerable<(float X, float Y)> Path(MoonfallPeg peg)
    {
        ArgumentNullException.ThrowIfNull(peg);
        if (peg.Mover.Kind == MoverKind.None)
        {
            yield return ((float)peg.X, (float)peg.Y);
            yield break;
        }

        var m = peg.Mover;
        for (var k = 0; k < 48; k++)
        {
            var phase = 2 * Math.PI * k / 48;
            if (m.Kind == MoverKind.Orbit)
            {
                var r = Math.Sqrt(((peg.X - m.X) * (peg.X - m.X)) + ((peg.Y - m.Y) * (peg.Y - m.Y)));
                var a = Math.Atan2(peg.Y - m.Y, peg.X - m.X) + ((m.Clockwise ? 1 : -1) * phase);
                yield return ((float)(m.X + (r * Math.Cos(a))), (float)(m.Y + (r * Math.Sin(a))));
            }
            else
            {
                var s = (1 - Math.Cos(phase)) * 0.5;
                yield return ((float)(peg.X + ((m.X - peg.X) * s)), (float)(peg.Y + ((m.Y - peg.Y) * s)));
            }
        }
    }

    /// <summary>
    /// A lower bound of <see cref="ArcCapsule"/>: the distance to the arc's whole circle (its ends lie on it), less half
    /// the thickness. Cheap (no angle), so a cell that cannot come nearer is passed over without the arc's own measure.
    /// </summary>
    private static float ArcBound(float x, float y, MoonfallPeg p)
    {
        float dx = x - (float)p.X, dy = y - (float)p.Y;
        return MathF.Abs(MathF.Sqrt((dx * dx) + (dy * dy)) - (float)p.Radius) - ((float)p.Thickness * 0.5f);
    }

    private static void Stamp(float[] d, float x0, float y0, float x1, float y1, Func<float, float, float> sd, Func<float, float, float>? bound = null)
    {
        var ix0 = Math.Max(0, (int)MathF.Floor(x0 - Far));
        var iy0 = Math.Max(0, (int)MathF.Floor(y0 - Far));
        var ix1 = Math.Min(Width - 1, (int)MathF.Ceiling(x1 + Far));
        var iy1 = Math.Min(Height - 1, (int)MathF.Ceiling(y1 + Far));
        for (var y = iy0; y <= iy1; y++)
        {
            for (var x = ix0; x <= ix1; x++)
            {
                var i = (y * Width) + x;
                if (bound is not null && bound(x + 0.5f, y + 0.5f) >= d[i])
                {
                    continue;
                }

                var v = sd(x + 0.5f, y + 0.5f);
                if (v < d[i])
                {
                    d[i] = v;
                }
            }
        }
    }

    private static void StampCircle(float[] d, float cx, float cy, float radius)
    {
        var ix0 = Math.Max(0, (int)MathF.Floor(cx - Far));
        var iy0 = Math.Max(0, (int)MathF.Floor(cy - Far));
        var ix1 = Math.Min(Width - 1, (int)MathF.Ceiling(cx + Far));
        var iy1 = Math.Min(Height - 1, (int)MathF.Ceiling(cy + Far));
        for (var y = iy0; y <= iy1; y++)
        {
            var dy = y + 0.5f - cy;
            var o = y * Width;
            for (var x = ix0; x <= ix1; x++)
            {
                // Most cells already hold something nearer: compared squared first, the root is taken only for a cell
                // this circle may bring nearer (every cell stays at least the circle's own distance, so nothing changes
                // but the work).
                var dx = x + 0.5f - cx;
                var d2 = (dx * dx) + (dy * dy);
                var reach = d[o + x] + radius;
                if (reach > 0 && d2 >= reach * reach * 1.0001f)
                {
                    continue;
                }

                var v = MathF.Sqrt(d2) - radius;
                if (v < d[o + x])
                {
                    d[o + x] = v;
                }
            }
        }
    }

    private static float LineCapsule(float x, float y, MoonfallPeg p)
    {
        float x1 = (float)p.X, y1 = (float)p.Y;
        float dx = (float)p.X2 - x1, dy = (float)p.Y2 - y1;
        float px = x - x1, py = y - y1;
        var t = Math.Clamp(((px * dx) + (py * dy)) / MathF.Max((dx * dx) + (dy * dy), 1e-6f), 0f, 1f);
        var ex = px - (dx * t);
        var ey = py - (dy * t);
        return MathF.Sqrt((ex * ex) + (ey * ey)) - ((float)p.Thickness * 0.5f);
    }

    private static float ArcCapsule(float x, float y, MoonfallPeg p)
    {
        float cx = (float)p.X, cy = (float)p.Y, r = (float)p.Radius;
        var a0 = (float)(p.StartDegrees * Math.PI / 180);
        var sw = (float)(p.SweepDegrees * Math.PI / 180);
        float dx = x - cx, dy = y - cy;
        var rel = MathF.Atan2(dy, dx) - a0;
        rel -= 2 * MathF.PI * MathF.Floor(rel / (2 * MathF.PI));
        float d;
        if (rel <= sw)
        {
            d = MathF.Abs(MathF.Sqrt((dx * dx) + (dy * dy)) - r);
        }
        else
        {
            float e0x = cx + (r * MathF.Cos(a0)), e0y = cy + (r * MathF.Sin(a0));
            float e1x = cx + (r * MathF.Cos(a0 + sw)), e1y = cy + (r * MathF.Sin(a0 + sw));
            d = MathF.Min(MathF.Sqrt(((x - e0x) * (x - e0x)) + ((y - e0y) * (y - e0y))), MathF.Sqrt(((x - e1x) * (x - e1x)) + ((y - e1y) * (y - e1y))));
        }

        return d - ((float)p.Thickness * 0.5f);
    }

    /// <summary>The distance at board point (x, y), units (the cell's value); <see cref="Far"/> off the board.</summary>
    public float At(float x, float y)
    {
        if (!(x >= 0 && y >= 0 && x < Width && y < Height))
        {
            return Far;
        }

        return distance[((int)y * Width) + (int)x];
    }

    /// <summary>The distance at (x, y), interpolated between cell centres.</summary>
    public float Sample(float x, float y)
    {
        x -= 0.5f;
        y -= 0.5f;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        var a = Cell(x0, y0) + ((Cell(x0 + 1, y0) - Cell(x0, y0)) * fx);
        var b = Cell(x0, y0 + 1) + ((Cell(x0 + 1, y0 + 1) - Cell(x0, y0 + 1)) * fx);
        return a + ((b - a) * fy);
    }

    private float Cell(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? Far : distance[(y * Width) + x];

    /// <summary>The field at <paramref name="s"/> pixels a unit (bilinear between cell centres), for the scene build.</summary>
    public MoonfallPlane ToPlane(float s)
    {
        var w = (int)MathF.Round(Width * s);
        var h = (int)MathF.Round(Height * s);
        var plane = new MoonfallPlane(w, h);
        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                plane.Data[(y * w) + x] = Sample((x + 0.5f) / s, (y + 0.5f) / s);
            }
        });
        return plane;
    }

    /// <summary>The smallest distance along the closed loop <paramref name="points"/> (a firefly's wander) less <paramref name="reach"/>.</summary>
    public float Clearance(ReadOnlySpan<Vector2> points, float reach)
    {
        var least = float.MaxValue;
        foreach (var p in points)
        {
            least = MathF.Min(least, At(p.X, p.Y) - reach);
        }

        return least;
    }
}
