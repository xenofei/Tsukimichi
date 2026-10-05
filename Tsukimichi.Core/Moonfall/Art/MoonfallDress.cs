using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The board being dressed (dress2.Ctx): its pixels a unit, the pieces' clearance, and what the framing has covered,
/// rim-lit and lit so far. Every organic element asks <see cref="Ok"/> before it is drawn, so foliage parts round the
/// pegs as real foliage would (F3a), and every small light asks for 8 units (F5).
/// </summary>
internal sealed class MoonfallDressContext
{
    public MoonfallDressContext(float s, MoonfallClearance clearance)
    {
        S = s;
        Clearance = clearance;
        W = (int)MathF.Round(800 * s);
        H = (int)MathF.Round(600 * s);
        Cover = new MoonfallPlane(W, H);
        Rim = new MoonfallPlane(W, H);
        ClearanceAtS = clearance.ToPlane(s);
    }

    public float S { get; }

    public int W { get; }

    public int H { get; }

    public MoonfallClearance Clearance { get; }

    /// <summary>The clearance sampled at this tier's pixels.</summary>
    public MoonfallPlane ClearanceAtS { get; }

    public MoonfallPlane Cover { get; }

    public MoonfallPlane Rim { get; }

    /// <summary>The small lights placed: (x, y, halo reach), board units.</summary>
    public List<Vector3> Lights { get; } = [];

    public int Dropped { get; set; }

    /// <summary>Distance from (x, y) to the nearest piece's edge; huge off the board.</summary>
    public float Clear(float x, float y) => x >= 0 && y >= 0 && x < 800 && y < 600 ? Clearance.At(x, y) : 1e9f;

    /// <summary>An element of radius <paramref name="reach"/> at (x, y) keeps 6 units (plus half a unit of anti-aliasing) from every piece.</summary>
    public bool Ok(float x, float y, float reach)
    {
        if (!(x > MoonfallFramingCheck.WallL - 40 && x < MoonfallFramingCheck.WallR + 40 && y > MoonfallFramingCheck.Top - 40 && y < 640))
        {
            return true;
        }

        if (MoonfallFramingCheck.WallL - reach > x || x > MoonfallFramingCheck.WallR + reach || y < MoonfallFramingCheck.Top - reach)
        {
            return true;
        }

        var ok = Clear(x, y) - reach >= 6.5f;
        if (!ok)
        {
            Dropped++;
        }

        return ok;
    }

    /// <summary>The pixel window (x0, y0, x1, y1 exclusive) round (cx, cy) of radius <paramref name="rad"/> units; empty off the board.</summary>
    public (int X0, int Y0, int X1, int Y1) Window(float cx, float cy, float rad)
    {
        var x0 = Math.Max(0, (int)((cx - rad) * S));
        var x1 = Math.Min(W, (int)((cx + rad) * S) + 1);
        var y0 = Math.Max(0, (int)((cy - rad) * S));
        var y1 = Math.Min(H, (int)((cy + rad) * S) + 1);
        return (x0, y0, Math.Max(x0, x1), Math.Max(y0, y1));
    }

    /// <summary>Coverage of a signed distance (units) at this tier: <c>clip(0.5 − sd × S, 0, 1)</c>.</summary>
    public float Cov(float sd) => Math.Clamp(0.5f - (sd * S), 0f, 1f);

    /// <summary>Stamps a disc (board units) into <paramref name="mask"/>.</summary>
    public void StampCircle(MoonfallPlane mask, float cx, float cy, float r)
    {
        var (x0, y0, x1, y1) = Window(cx, cy, r + 1);
        for (var y = y0; y < y1; y++)
        {
            var dy = ((y + 0.5f) / S) - cy;
            var o = y * W;
            for (var x = x0; x < x1; x++)
            {
                var dx = ((x + 0.5f) / S) - cx;
                var c = Cov(MathF.Sqrt((dx * dx) + (dy * dy)) - r);
                if (c > mask.Data[o + x])
                {
                    mask.Data[o + x] = c;
                }
            }
        }
    }

    /// <summary>Stamps an ellipse (board units, turned by <paramref name="rot"/>) into <paramref name="mask"/>, over its own bounding box.</summary>
    public void StampEllipse(MoonfallPlane mask, float cx, float cy, float rx, float ry, float rot)
    {
        var c = MathF.Cos(rot);
        var sn = MathF.Sin(rot);
        var ex = MathF.Sqrt((rx * c * rx * c) + (ry * sn * ry * sn)) + 1;
        var ey = MathF.Sqrt((rx * sn * rx * sn) + (ry * c * ry * c)) + 1;
        var x0 = Math.Max(0, (int)((cx - ex) * S));
        var x1 = Math.Min(W, (int)((cx + ex) * S) + 1);
        var y0 = Math.Max(0, (int)((cy - ey) * S));
        var y1 = Math.Min(H, (int)((cy + ey) * S) + 1);
        var k = MathF.Min(rx, ry);
        for (var y = y0; y < y1; y++)
        {
            var dy = ((y + 0.5f) / S) - cy;
            var o = y * W;
            for (var x = x0; x < x1; x++)
            {
                var dx = ((x + 0.5f) / S) - cx;
                var u = ((dx * c) + (dy * sn)) / rx;
                var v = ((-dx * sn) + (dy * c)) / ry;
                var cov = Cov((MathF.Sqrt((u * u) + (v * v)) - 1) * k);
                if (cov > mask.Data[o + x])
                {
                    mask.Data[o + x] = cov;
                }
            }
        }
    }

    /// <summary>Stamps a shape (its signed distance in board units) into <paramref name="mask"/> round (cx, cy).</summary>
    public void Stamp(MoonfallPlane mask, Func<float, float, float> sd, float cx, float cy, float rad)
    {
        var (x0, y0, x1, y1) = Window(cx, cy, rad);
        for (var y = y0; y < y1; y++)
        {
            var by = (y + 0.5f) / S;
            for (var x = x0; x < x1; x++)
            {
                var c = Cov(sd((x + 0.5f) / S, by));
                var i = (y * W) + x;
                if (c > mask.Data[i])
                {
                    mask.Data[i] = c;
                }
            }
        }
    }
}

/// <summary>
/// The dressing kit, ported from the design (docs/design/v9/rich2/src/dress2.py, rich_lib.py and the airship road's
/// route): the framing shapes (fronds, trunks, firs, outcrops, rocks, crystals, ropes) and their silhouette shading,
/// the light layers (shafts, glows, the moon, aurora, nebula, the compass rose, the neat-line, an engraved route), the
/// small lights and the veil. Placement noise and jitter come from <see cref="MoonfallNoise"/>, so the same recipe
/// dresses the same way every time; the shapes follow the Python's construction, not its random draws.
/// </summary>
internal static class MoonfallDress
{
    private static readonly Vector2 Light = new(-0.707f, -0.707f);

    // ---- Framing shapes ----

    public static float SdEllipse(float x, float y, float cx, float cy, float rx, float ry, float rot)
    {
        var c = MathF.Cos(rot);
        var s = MathF.Sin(rot);
        var u = (((x - cx) * c) + ((y - cy) * s)) / rx;
        var v = ((-(x - cx) * s) + ((y - cy) * c)) / ry;
        return (MathF.Sqrt((u * u) + (v * v)) - 1) * MathF.Min(rx, ry);
    }

    /// <summary>dress2.frond: a curved stem with leaves; each leaf dropped if it would come near a piece, the stem ending where it would.</summary>
    public static void Frond(MoonfallDressContext ctx, MoonfallPlane mask, MoonfallFrond f) =>
        Frond(ctx, mask, f.X, f.Y, f.Length, f.Angle, f.Droop, f.Leaf, f.Leaves, f.Style, f.Seed, f.Width, f.Twigs);

    private static void Frond(MoonfallDressContext ctx, MoonfallPlane mask, float x, float y, float length, float angle, float droop, float leaf, int n,
        string style, int seed, float width, int twigs)
    {
        var rng = new MoonfallNoise.Random(seed);
        var a = angle * MathF.PI / 180f;
        var dx = MathF.Cos(a);
        var dy = MathF.Sin(a);
        Vector2 P(float t) => new(x + (dx * length * t), y + (dy * length * t) + (droop * length * t * t));
        Vector2 T(float t) => Vector2.Normalize(new Vector2(dx * length, (dy * length) + (2 * droop * length * t)));
        var steps = Math.Max(40, (int)(length / MathF.Max(width * 0.6f, 0.6f)));
        var end = 1f;
        for (var k = 0; k <= steps; k++)
        {
            var t = k / (float)steps;
            var p = P(t);
            if (!ctx.Ok(p.X, p.Y, width + 1))
            {
                end = MathF.Max(0f, t - 0.03f);
                break;
            }
        }

        for (var k = 0; k <= (int)(steps * end); k++)
        {
            var t = k / (float)steps;
            var p = P(t);
            var wdt = width * (1 - (0.6f * t));
            ctx.StampCircle(mask, p.X, p.Y, wdt);
        }

        for (var j = 0; j < twigs; j++)
        {
            var t = 0.15f + (0.7f * (j + 0.5f) / twigs);
            if (t > end)
            {
                break;
            }

            var p = P(t);
            var tg = T(t);
            var side = j % 2 == 1 ? 1 : -1;
            var ta = (MathF.Atan2(tg.Y, tg.X) * 180f / MathF.PI) + (side * 38);
            Frond(ctx, mask, p.X, p.Y, length * 0.36f * (1.1f - (t * 0.5f)), ta, droop * 1.2f, leaf * 0.85f, Math.Max(4, n / 3), style, (seed * 13) + j, width * 0.6f, 0);
        }

        var (spread, drop, ryk) = style switch
        {
            "willow" => (14f, 1.2f, 0.10f),
            "fir" => (62f, 0.15f, 0.10f),
            "fern" => (70f, 0.25f, 0.16f),
            "oak" => (50f, 0.4f, 0.62f),
            _ => (34f, 0.55f, 0.30f),
        };
        for (var i = 0; i < n; i++)
        {
            var t = 0.06f + (0.92f * i / Math.Max(1, n - 1));
            if (t > end + 0.02f)
            {
                break;
            }

            var p = P(t);
            var tg = T(t);
            var size = leaf * (1.05f - (0.55f * t)) * (0.8f + (0.4f * rng.Next()));
            var oneSide = style == "oak" ? (rng.Next() < 0.5f ? -1 : 1) : 0;
            for (var pass = 0; pass < (style == "oak" ? 1 : 2); pass++)
            {
                var side = style == "oak" ? oneSide : (pass == 0 ? -1 : 1);
                var sp = (spread + rng.Normal(0, 6)) * MathF.PI / 180f * side;
                var lx = (tg.X * MathF.Cos(sp)) - (tg.Y * MathF.Sin(sp));
                var ly = (tg.X * MathF.Sin(sp)) + (tg.Y * MathF.Cos(sp)) + drop;
                var nl = MathF.Sqrt((lx * lx) + (ly * ly));
                lx /= nl;
                ly /= nl;
                var cx = p.X + (lx * size * 0.55f);
                var cy = p.Y + (ly * size * 0.55f);
                var rx = size * 0.55f;
                var ry = size * 0.55f * ryk;
                if (!ctx.Ok(cx, cy, rx))
                {
                    continue;
                }

                var rot = MathF.Atan2(ly, lx);
                if (style == "oak")
                {
                    foreach (var (o, rr) in (ReadOnlySpan<(float, float)>)[(0f, 1f), (0.45f, 0.72f), (-0.45f, 0.72f)])
                    {
                        var ox = cx + (MathF.Cos(rot) * rx * o);
                        var oy = cy + (MathF.Sin(rot) * rx * o);
                        ctx.StampEllipse(mask, ox, oy, rx * 0.55f * rr, rx * 0.48f * rr, rot + (0.6f * o));
                    }
                }
                else
                {
                    ctx.StampEllipse(mask, cx, cy, rx, MathF.Max(ry, 0.7f), rot);
                }
            }
        }
    }

    /// <summary>dress2.trunk: a trunk whose edge wobbles at two scales (no straight runs), knots breaking it.</summary>
    public static void Trunk(MoonfallDressContext ctx, MoonfallPlane mask, MoonfallTrunk t)
    {
        var (x0, y0, x1, y1) = ctx.Window(t.X, (t.Y0 + t.Y1) / 2, ((t.Y1 - t.Y0) / 2) + t.W1 + MathF.Abs(t.Lean) + 10);
        if (x1 <= x0 || y1 <= y0)
        {
            return;
        }

        var noise = MoonfallNoise.Fbm(x1 - x0, y1 - y0, 6 * ctx.S, 3, t.Seed);
        for (var y = y0; y < y1; y++)
        {
            var Y = (y + 0.5f) / ctx.S;
            var tt = Math.Clamp((Y - t.Y0) / (t.Y1 - t.Y0), 0f, 1f);
            var cx = t.X + (t.Lean * (1 - tt) * (1 - tt)) + (2.2f * MathF.Sin((Y / 23f) + t.Seed)) + (1.2f * MathF.Sin((Y / 7.3f) + (t.Seed * 2)));
            var hw = t.W0 + ((t.W1 - t.W0) * tt);
            for (var x = x0; x < x1; x++)
            {
                var X = (x + 0.5f) / ctx.S;
                var n = noise.Data[((y - y0) * (x1 - x0)) + (x - x0)] - 0.5f;
                var sd = MathF.Max(MathF.Abs(X - cx) - hw - (n * 5f), MathF.Max(t.Y0 - Y, Y - t.Y1));
                var i = (y * ctx.W) + x;
                mask.Data[i] = MathF.Max(mask.Data[i], ctx.Cov(sd));
            }
        }
    }

    /// <summary>dress2.pines: fir silhouettes, each shortened until it keeps clear of every piece.</summary>
    public static void Pines(MoonfallDressContext ctx, MoonfallPlane mask, MoonfallPines p)
    {
        var rng = new MoonfallNoise.Random(p.Seed);
        foreach (var tree in p.Trees)
        {
            float x = tree.X, by = tree.Y, h = tree.Z, wdt = tree.W;
            while (h > 30)
            {
                var clear = true;
                for (var k = 0; k < 12 && clear; k++)
                {
                    var t = k / 11f;
                    foreach (var s in (ReadOnlySpan<float>)[-1f, 1f])
                    {
                        if (!ctx.Ok(x + (s * wdt * (0.12f + (0.88f * t))), by - h + (h * t), 4))
                        {
                            clear = false;
                            break;
                        }
                    }
                }

                if (clear)
                {
                    break;
                }

                h *= 0.88f;
                wdt *= 0.9f;
            }

            var (x0, y0, x1, y1) = ctx.Window(x, by - (h / 2), (MathF.Max(h, wdt) / 2) + wdt + 10);
            if (x1 <= x0 || y1 <= y0)
            {
                continue;
            }

            var jag = MoonfallNoise.Fbm(x1 - x0, y1 - y0, 4 * ctx.S, 2, rng.Int(0, 1000));
            var top = by - h;
            for (var y = y0; y < y1; y++)
            {
                var Y = (y + 0.5f) / ctx.S;
                var t = Math.Clamp((Y - top) / h, 0f, 1f);
                var saw = (t * 6) % 1f;
                var half = wdt * (0.12f + (0.88f * t)) * (0.62f + (0.38f * saw));
                for (var xx = x0; xx < x1; xx++)
                {
                    var X = (xx + 0.5f) / ctx.S;
                    var j = (jag.Data[((y - y0) * (x1 - x0)) + (xx - x0)] - 0.5f) * 4;
                    var sd = MathF.Max(MathF.Abs(X - x) - half - j, MathF.Max(top - Y, Y - by));
                    var stub = MathF.Max(MathF.Abs(X - x) - (wdt * 0.08f), MathF.Max(by - Y, Y - by - 30));
                    var i = (y * ctx.W) + xx;
                    mask.Data[i] = MathF.Max(mask.Data[i], MathF.Max(ctx.Cov(sd), ctx.Cov(stub)));
                }
            }
        }
    }

    /// <summary>dress2.outcrop: the region under a jagged ridge, sunk until it keeps 6.5 units from every piece.</summary>
    public static void Outcrop(MoonfallDressContext ctx, MoonfallPlane mask, MoonfallOutcrop o)
    {
        if (o.Ridge.Length < 2)
        {
            return;
        }

        var xs = o.Ridge.Select(static p => p.X).ToArray();
        var ys = o.Ridge.Select(static p => p.Y).ToArray();
        var n1 = MoonfallNoise.Fbm(ctx.W, 1, 14 * ctx.S, 4, o.Seed);
        var n2 = MoonfallNoise.Fbm(ctx.W, 1, 4 * ctx.S, 2, o.Seed + 1);
        var ridge = new float[ctx.W];
        float minX = xs.Min(), maxX = xs.Max();
        for (var x = 0; x < ctx.W; x++)
        {
            var X = (x + 0.5f) / ctx.S;
            ridge[x] = X < minX || X > maxX ? 1e4f : MoonfallColor.Interp(X, xs, ys) + ((n1.Data[x] - 0.5f) * o.Rough) + ((n2.Data[x] - 0.5f) * o.Rough * 0.4f);
        }

        var drop = 0;
        for (; drop < 200; drop += 2)
        {
            var least = float.MaxValue;
            for (var y = 0; y < ctx.H; y++)
            {
                var Y = (y + 0.5f) / ctx.S;
                for (var x = 0; x < ctx.W; x++)
                {
                    if (ridge[x] > 9e3f)
                    {
                        continue;
                    }

                    var X = (x + 0.5f) / ctx.S;
                    if (ctx.Cov(ridge[x] + drop - Y) > 0.4f && MoonfallFramingCheck.Opening(X, Y))
                    {
                        least = MathF.Min(least, ctx.ClearanceAtS.Data[(y * ctx.W) + x]);
                    }
                }
            }

            if (least >= 6.5f)
            {
                break;
            }
        }

        for (var y = 0; y < ctx.H; y++)
        {
            var Y = (y + 0.5f) / ctx.S;
            for (var x = 0; x < ctx.W; x++)
            {
                if (ridge[x] > 9e3f)
                {
                    continue;
                }

                var i = (y * ctx.W) + x;
                mask.Data[i] = MathF.Max(mask.Data[i], ctx.Cov(ridge[x] + drop - Y));
            }
        }
    }

    /// <summary>An irregular drifting rock (the exp-p3 recipe): five or six lobes, drawn only where it keeps clear.</summary>
    public static void Rock(MoonfallDressContext ctx, MoonfallPlane mask, MoonfallRock r)
    {
        if (!ctx.Ok(r.X, r.Y, r.R * 1.6f))
        {
            return;
        }

        var rng = new MoonfallNoise.Random(r.Seed);
        var k = rng.Int(5, 7);
        var angs = new float[k];
        for (var i = 0; i < k; i++)
        {
            angs[i] = rng.Range(0, 2 * MathF.PI);
        }

        Array.Sort(angs);
        var phase = rng.Range(0, MathF.PI);
        var rads = new float[k];
        for (var i = 0; i < k; i++)
        {
            rads[i] = r.R * rng.Range(0.55f, 1.45f) * (1 + (0.45f * MathF.Cos(angs[i] - phase)));
        }

        // The radius by angle, round the circle (numpy's interp over the angles tiled thrice).
        var xa = new float[k * 3];
        var ya = new float[k * 3];
        for (var i = 0; i < k * 3; i++)
        {
            xa[i] = angs[i % k] + (((i / k) - 1) * 2 * MathF.PI);
            ya[i] = rads[i % k];
        }

        ctx.Stamp(mask, (X, Y) =>
        {
            var a = MathF.Atan2(Y - r.Y, X - r.X);
            if (a < 0)
            {
                a += 2 * MathF.PI;
            }

            return MathF.Sqrt(((X - r.X) * (X - r.X)) + ((Y - r.Y) * (Y - r.Y))) - MoonfallColor.Interp(a, xa, ya);
        }, r.X, r.Y, r.R * 1.4f * 1.45f * 1.45f);
    }

    /// <summary>A rope hanging from <c>From</c> to <c>To</c>, each point dropped where it would come near a piece.</summary>
    public static void Rope(MoonfallDressContext ctx, MoonfallPlane mask, MoonfallRope r)
    {
        for (var k = 0; k < 120; k++)
        {
            var t = k / 119f;
            var x = r.From.X + ((r.To.X - r.From.X) * t);
            var y = r.From.Y + ((r.To.Y - r.From.Y) * t) + (r.Sag * 4 * t * (1 - t));
            if (!ctx.Ok(x, y, r.Width + 0.5f))
            {
                continue;
            }

            ctx.StampCircle(mask, x, y, r.Width);
        }
    }

    /// <summary>dress2.crystal: a faceted spire, lit from the upper left, drawn on the scene and counted as framing.</summary>
    public static void Crystal(MoonfallDressContext ctx, MoonfallImage px, MoonfallCrystal c)
    {
        foreach (var f in (ReadOnlySpan<float>)[0f, 0.35f, 0.7f, 1f])
        {
            if (!ctx.Ok(c.X + (MathF.Sin(c.Tilt) * c.H * f), c.Y - (MathF.Cos(c.Tilt) * c.H * f), c.W + 1))
            {
                return;
            }
        }

        var (x0, y0, x1, y1) = ctx.Window(c.X, c.Y - (c.H / 2), (c.H / 2) + c.W + 6);
        float cs = MathF.Cos(c.Tilt), sn = MathF.Sin(c.Tilt);
        for (var y = y0; y < y1; y++)
        {
            var Y = (y + 0.5f) / ctx.S;
            for (var x = x0; x < x1; x++)
            {
                var X = (x + 0.5f) / ctx.S;
                var u = ((X - c.X) * cs) + ((Y - c.Y) * sn);
                var v = (-(X - c.X) * sn) + ((Y - c.Y) * cs);
                var t = Math.Clamp(-v / c.H, 0f, 1f);
                var half = c.W * (t < 0.72f ? 1f - (0.15f * t) : (1 - t) / 0.28f * 0.89f);
                var m = ctx.Cov(MathF.Max(MathF.Abs(u) - half, MathF.Max(v, -v - c.H)));
                if (m <= 0)
                {
                    continue;
                }

                var col = u < -0.15f * half ? (c.Lit * 0.55f) + (c.Body * 0.45f) : c.Body * 0.8f;
                col *= 0.75f + (0.35f * t);
                var crack = MathF.Exp(-MathF.Pow((u - (0.3f * c.W * MathF.Sin(v / 9f))) / 0.6f, 2)) * (t > 0.2f && t < 0.65f ? 1 : 0);
                col *= 1 - (0.35f * crack);
                var i = (y * ctx.W) + x;
                px.R.Data[i] = (px.R.Data[i] * (1 - m)) + (col.X * m);
                px.G.Data[i] = (px.G.Data[i] * (1 - m)) + (col.Y * m);
                px.B.Data[i] = (px.B.Data[i] * (1 - m)) + (col.Z * m);
                ctx.Cover.Data[i] = MathF.Max(ctx.Cover.Data[i], m);
            }
        }
    }

    /// <summary>
    /// dress2.silhouette: the group's shapes as a near-black foreground with a little of the palette in it, rim-lit in
    /// short runs (F3b) on the edges that face the upper left; thin stems and ropes catch no rim; snow on upper faces.
    /// </summary>
    public static void Silhouette(MoonfallDressContext ctx, MoonfallImage px, MoonfallPlane mask, MoonfallFramingGroup g)
    {
        var n = mask.Data.Length;
        for (var i = 0; i < n; i++)
        {
            mask.Data[i] = Math.Clamp(mask.Data[i], 0f, 1f);
            ctx.Cover.Data[i] = MathF.Max(ctx.Cover.Data[i], mask.Data[i]);
        }

        // Shaded box by box: the framing lives at the edges and corners, and every blur here reaches at most this far.
        var margin = (int)MathF.Ceiling(3 * 14 * ctx.S) + 4;
        foreach (var (x0, y0, x1, y1) in Boxes(mask, ctx.W, ctx.H, margin))
        {
            SilhouetteBox(ctx, px, mask, g, x0, y0, x1, y1);
        }
    }

    /// <summary>The boxes round the mask's pieces (columns apart by more than twice the margin split them), each grown by the margin.</summary>
    private static List<(int X0, int Y0, int X1, int Y1)> Boxes(MoonfallPlane mask, int w, int h, int margin)
    {
        var colTop = new int[w];
        var colBottom = new int[w];
        Array.Fill(colTop, int.MaxValue);
        Array.Fill(colBottom, -1);
        for (var y = 0; y < h; y++)
        {
            var o = y * w;
            for (var x = 0; x < w; x++)
            {
                if (mask.Data[o + x] > 0)
                {
                    colTop[x] = Math.Min(colTop[x], y);
                    colBottom[x] = Math.Max(colBottom[x], y);
                }
            }
        }

        var boxes = new List<(int, int, int, int)>();
        var start = -1;
        int top = int.MaxValue, bottom = -1, lastOn = -1;
        for (var x = 0; x <= w; x++)
        {
            var on = x < w && colBottom[x] >= 0;
            if (!on)
            {
                continue;
            }

            if (start < 0 || x - lastOn > 2 * margin)
            {
                if (start >= 0)
                {
                    boxes.Add((Math.Max(0, start - margin), Math.Max(0, top - margin), Math.Min(w, lastOn + 1 + margin), Math.Min(h, bottom + 1 + margin)));
                }

                start = x;
                top = int.MaxValue;
                bottom = -1;
            }

            top = Math.Min(top, colTop[x]);
            bottom = Math.Max(bottom, colBottom[x]);
            lastOn = x;
        }

        if (start >= 0)
        {
            boxes.Add((Math.Max(0, start - margin), Math.Max(0, top - margin), Math.Min(w, lastOn + 1 + margin), Math.Min(h, bottom + 1 + margin)));
        }

        return boxes;
    }

    private static void SilhouetteBox(MoonfallDressContext ctx, MoonfallImage px, MoonfallPlane full, MoonfallFramingGroup g, int x0, int y0, int x1, int y1)
    {
        var s = ctx.S;
        int bw = x1 - x0, bh = y1 - y0;
        var mask = new MoonfallPlane(bw, bh);
        for (var y = 0; y < bh; y++)
        {
            Array.Copy(full.Data, ((y0 + y) * ctx.W) + x0, mask.Data, y * bw, bw);
        }

        var mb = MoonfallFilters.Blur(mask, g.RimWidth * s * 0.6f);
        var (gy, gx) = MoonfallFilters.Gradient(mb);
        var mWide = MoonfallFilters.Blur(mask, g.RimWidth * s);
        var breaker = MoonfallNoise.FbmAt(x0, y0, bw, bh, 4.5f * s, 2, g.Seed + 101);
        var thick = MoonfallFilters.Blur(mask, 2.5f * s);
        var shade = MoonfallFilters.Blur(mask, 14 * s);
        MoonfallParallel.For(0, bh, y =>
        {
            for (var x = 0; x < bw; x++)
            {
                var j = (y * bw) + x;
                var i = ((y0 + y) * ctx.W) + x0 + x;
                var m = mask.Data[j];
                var brk = MoonfallColor.Smooth(0.36f, 0.64f, breaker.Data[j]);
                var facing = Math.Clamp(-((gx.Data[j] * Light.X) + (gy.Data[j] * Light.Y)) * s * 4f, 0f, 1f);
                var band = facing * m * MoonfallColor.Smooth(0f, 0.6f, mb.Data[j]) * (1 - MoonfallColor.Smooth(0.75f, 1f, mWide.Data[j]));
                var rimA = Math.Clamp(band * 3f, 0f, 1f) * g.RimK * brk * MoonfallColor.Smooth(0.35f, 0.6f, thick.Data[j]);
                ctx.Rim.Data[i] = MathF.Max(ctx.Rim.Data[i], rimA);
                if (m <= 0 && rimA <= 0)
                {
                    continue;
                }

                var k = g.InnerK * (1 - shade.Data[j]);
                var body = (g.Body * (1 - k)) + (g.Inner * k);
                var r = (px.R.Data[i] * (1 - m)) + (body.X * m);
                var gg = (px.G.Data[i] * (1 - m)) + (body.Y * m);
                var b = (px.B.Data[i] * (1 - m)) + (body.Z * m);
                if (g.Snow is { } snow)
                {
                    var up = Math.Clamp(-gy.Data[j] * s * 6f, 0f, 1f) * m;
                    var a = Math.Clamp(up * 2, 0f, 1f) * 0.30f * brk;
                    r = MoonfallColor.Screen(r, snow.X * a);
                    gg = MoonfallColor.Screen(gg, snow.Y * a);
                    b = MoonfallColor.Screen(b, snow.Z * a);
                }

                px.R.Data[i] = MoonfallColor.Screen(r, g.Rim.X * rimA);
                px.G.Data[i] = MoonfallColor.Screen(gg, g.Rim.Y * rimA);
                px.B.Data[i] = MoonfallColor.Screen(b, g.Rim.Z * rimA);
            }
        });
    }

    // ---- Light ----

    /// <summary>
    /// dress2.shafts' amount at every pixel (multiplied by k), the noise shifted by <paramref name="shift"/> units. The
    /// shafts are soft (their narrowest is some 14 units across), so above one pixel a unit they are worked out at one and
    /// interpolated up.
    /// </summary>
    public static MoonfallPlane ShaftAmount(MoonfallShafts sh, float s, int w, int h, Vector2 shift)
    {
        if (s > 1)
        {
            var low = ShaftAmount(sh, 1, (int)MathF.Ceiling(w / s), (int)MathF.Ceiling(h / s), shift);
            var up = new MoonfallPlane(w, h);
            MoonfallParallel.For(0, h, y =>
            {
                for (var x = 0; x < w; x++)
                {
                    up.Data[(y * w) + x] = low.Sample((x + 0.5f) / s, (y + 0.5f) / s);
                }
            });
            return up;
        }

        var pad = (int)(12 * s);
        var noise = MoonfallNoise.FbmSmooth(w + (2 * pad), h + (2 * pad), 90 * s, 3, sh.Seed);
        var ox = (int)MathF.Round(shift.X * s);
        var oy = (int)MathF.Round(shift.Y * s);
        var amount = new MoonfallPlane(w, h);
        var hws = sh.Widths.Select(static wd => MathF.Atan2(wd, 520f) * 180f / MathF.PI).ToArray();
        MoonfallParallel.For(0, h, y =>
        {
            var Y = (y + 0.5f) / s;
            for (var x = 0; x < w; x++)
            {
                var X = (x + 0.5f) / s;
                var dx = X - sh.Origin.X;
                var dy = Y - sh.Origin.Y;
                var ang = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                var dist = MathF.Sqrt((dx * dx) + (dy * dy));
                var acc = 0f;
                for (var i = 0; i < sh.Angles.Length; i++)
                {
                    var z = (ang - sh.Angles[i]) / hws[Math.Min(i, hws.Length - 1)];
                    acc += MathF.Exp(-z * z) * (0.70f + (0.30f * ((i * 0.37f) % 1f)));
                }

                var fall = MoonfallColor.Smooth(sh.Reach, 200f, dist) * MoonfallColor.Smooth(sh.Near, sh.Near + 180f, dist);
                var nx = Math.Clamp(pad + ox + x, 0, w + (2 * pad) - 1);
                var ny = Math.Clamp(pad + oy + y, 0, h + (2 * pad) - 1);
                var nv = noise.Data[(ny * (w + (2 * pad))) + nx];
                amount.Data[(y * w) + x] = acc * fall * (0.55f + (0.6f * nv)) * sh.K;
            }
        });
        return amount;
    }

    /// <summary>Screens <paramref name="colour"/> × <paramref name="amount"/> × <paramref name="scale"/> onto <paramref name="px"/>.</summary>
    public static void ScreenScaled(MoonfallImage px, Vector3 colour, MoonfallPlane amount, float scale)
    {
        var w = px.Width;
        MoonfallParallel.For(0, px.Height, y =>
        {
            for (var i = y * w; i < (y + 1) * w; i++)
            {
                var k = amount.Data[i] * scale;
                if (k <= 0)
                {
                    continue;
                }

                px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], colour.X * k);
                px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], colour.Y * k);
                px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], colour.Z * k);
            }
        });
    }

    /// <summary>dress2.glow: a soft pool of light, board units.</summary>
    public static void Glow(MoonfallImage px, float s, MoonfallGlow g)
    {
        var amount = new MoonfallPlane(px.Width, px.Height);
        MoonfallParallel.For(0, px.Height, y =>
        {
            for (var x = 0; x < px.Width; x++)
            {
                var X = (x + 0.5f) / s;
                var Y = (y + 0.5f) / s;
                var d = MathF.Sqrt(((X - g.X) * (X - g.X)) + ((Y - g.Y) * (Y - g.Y))) / g.R;
                amount.Data[(y * px.Width) + x] = MathF.Exp(-d * d) * g.K;
            }
        });
        MoonfallGrade.ScreenIn(px, g.Colour, amount);
    }

    /// <summary>rich_lib.moon_emissive: an even bright face with soft limb darkening and maria, no terminator, and a soft cool halo.</summary>
    public static void Moon(MoonfallImage px, float s, MoonfallMoon m)
    {
        var face = MoonfallColor.Hex("#F1F0EA");
        var sea = MoonfallColor.Hex("#AEB0B2");
        var haloCol = MoonfallColor.Hex("#C9D6F4");
        var reach = m.R * 12f;
        var wx0 = Math.Max(0, (int)((m.X - reach) * s));
        var wy0 = Math.Max(0, (int)((m.Y - reach) * s));
        var wx1 = Math.Min(px.Width, (int)((m.X + reach) * s) + 1);
        var wy1 = Math.Min(px.Height, (int)((m.Y + reach) * s) + 1);
        var dx0 = Math.Max(0, (int)((m.X - m.R - 1) * s));
        var dy0 = Math.Max(0, (int)((m.Y - m.R - 1) * s));
        var dx1 = Math.Min(px.Width, (int)((m.X + m.R + 1) * s) + 1);
        var dy1 = Math.Min(px.Height, (int)((m.Y + m.R + 1) * s) + 1);
        var mott = MoonfallNoise.FbmAt(dx0, dy0, Math.Max(1, dx1 - dx0), Math.Max(1, dy1 - dy0), m.R * s * 0.18f, 3, m.Seed);
        ReadOnlySpan<(float Sx, float Sy, float Rx, float Ry, float K)> seas =
        [
            (-0.28f, -0.22f, 0.26f, 0.20f, 1.0f), (0.12f, -0.32f, 0.18f, 0.14f, 0.8f), (0.30f, -0.04f, 0.20f, 0.17f, 0.9f),
            (-0.48f, 0.14f, 0.14f, 0.24f, 0.6f), (-0.10f, 0.30f, 0.13f, 0.09f, 0.5f), (0.42f, 0.26f, 0.10f, 0.12f, 0.5f),
        ];
        var maria = seas.ToArray();
        MoonfallParallel.For(wy0, wy1, y =>
        {
            for (var x = wx0; x < wx1; x++)
            {
                var i = (y * px.Width) + x;
                var u = (((x + 0.5f) / s) - m.X) / m.R;
                var v = (((y + 0.5f) / s) - m.Y) / m.R;
                var d = MathF.Sqrt((u * u) + (v * v));
                if (d > 1)
                {
                    var o = d - 1;
                    var halo = (MathF.Exp(-(o * o) / 0.6f) * 0.20f) + (MathF.Exp(-o / 3.5f) * 0.10f);
                    px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], haloCol.X * halo);
                    px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], haloCol.Y * halo);
                    px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], haloCol.Z * halo);
                }

                var disc = Math.Clamp(((1 - d) * m.R * s) + 0.5f, 0f, 1f);
                if (disc <= 0)
                {
                    continue;
                }

                var nz = MathF.Sqrt(Math.Clamp(1 - (d * d), 0f, 1f));
                var limb = 0.90f + (0.10f * MathF.Sqrt(nz));
                var sm = 0f;
                foreach (var (sx, sy, rx, ry, k) in maria)
                {
                    var a = (u - sx) / rx;
                    var b = (v - sy) / ry;
                    sm = MathF.Max(sm, MathF.Exp(-((a * a) + (b * b)) * 1.6f) * k);
                }

                var mx = Math.Clamp(x - dx0, 0, mott.Width - 1);
                var my = Math.Clamp(y - dy0, 0, mott.Height - 1);
                sm = Math.Clamp(sm * (0.75f + (0.5f * mott[mx, my])), 0f, 1f);
                var c = ((face * (1 - (0.30f * sm))) + (sea * (0.30f * sm))) * limb;
                px.R.Data[i] = (px.R.Data[i] * (1 - disc)) + (c.X * disc);
                px.G.Data[i] = (px.G.Data[i] * (1 - disc)) + (c.Y * disc);
                px.B.Data[i] = (px.B.Data[i] * (1 - disc)) + (c.Z * disc);
            }
        });
    }

    /// <summary>dress2.aurora: a curtain over the sea, aquamarine below, violet at the top, its rays fine.</summary>
    public static void Aurora(MoonfallImage px, float s, MoonfallAurora a)
    {
        var violet = MoonfallColor.Hex("#B07CFF");
        var green = MoonfallColor.Hex("#33F0C0");
        MoonfallParallel.For(0, px.Height, y =>
        {
            var Y = (y + 0.5f) / s;
            for (var x = 0; x < px.Width; x++)
            {
                var X = (x + 0.5f) / s;
                var curve = a.Y + (34 * MathF.Sin((X / 120f) + 0.8f)) + (16 * MathF.Sin((X / 47f) + 2.0f));
                var d = Y - curve;
                var up = MathF.Min(d, 0) / 120f;
                var down = MathF.Max(d, 0) / 16f;
                var curtain = MathF.Exp(-(up * up)) * MathF.Exp(-(down * down));
                var rays = 0.55f + (0.45f * MathF.Sin((X / 7f) + (1.3f * MathF.Sin(X / 37f))));
                var k = curtain * rays * MoonfallColor.Smooth(MoonfallFramingCheck.WallL, 230, X) * MoonfallColor.Smooth(MoonfallFramingCheck.WallR, 570, X) * a.K;
                var tt = MoonfallColor.Smooth(-140, 0, d);
                var col = (violet * (1 - tt)) + (green * tt);
                var i = (y * px.Width) + x;
                px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], col.X * k);
                px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], col.Y * k);
                px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], col.Z * k);
            }
        });
    }

    /// <summary>dress2.nebula: magenta and teal, only in the empty black.</summary>
    public static void Nebula(MoonfallImage px, float s, MoonfallNebula nb)
    {
        var n1 = MoonfallNoise.FbmSmooth(px.Width, px.Height, 120 * s, 5, nb.Seed);
        var n2 = MoonfallNoise.FbmSmooth(px.Width, px.Height, 60 * s, 4, nb.Seed + 1);
        var magenta = MoonfallColor.Hex("#E04FA0");
        var teal = MoonfallColor.Hex("#20B0B8");
        for (var i = 0; i < n1.Data.Length; i++)
        {
            var dark = MoonfallColor.Smooth(0.12f, 0.03f, MoonfallColor.Luma(px.R.Data[i], px.G.Data[i], px.B.Data[i]));
            var a = MoonfallColor.Smooth(0.30f, 0.75f, n1.Data[i]) * (0.6f + (0.6f * n2.Data[i])) * dark * nb.K;
            var col = (magenta * (1 - (0.5f * n2.Data[i]))) + (teal * (0.5f * n2.Data[i]));
            px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], col.X * a);
            px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], col.Y * a);
            px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], col.Z * a);
        }
    }

    /// <summary>dress2.compass_rose: eight points (the cardinal ones long), split lit and shaded as engraving shows them, two rings and ticks; 6 units clear of every piece.</summary>
    public static void CompassRose(MoonfallDressContext ctx, MoonfallImage px, MoonfallCompassRose c)
    {
        var s = ctx.S;
        var (x0, y0, x1, y1) = ctx.Window(c.X, c.Y, c.R * 1.25f);
        int w = x1 - x0, h = y1 - y0;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var lit = new MoonfallPlane(w, h);
        var dark = new MoonfallPlane(w, h);
        var ringTicks = new MoonfallPlane(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var u = ((x0 + x + 0.5f) / s) - c.X;
                var v = ((y0 + y + 0.5f) / s) - c.Y;
                var r = MathF.Sqrt((u * u) + (v * v));
                var ang = MathF.Atan2(v, u);
                for (var k = 0; k < 8; k++)
                {
                    var a0 = (k * MathF.PI / 4) - (MathF.PI / 2);
                    var len = k % 2 == 0 ? c.R : c.R * 0.58f;
                    var half = c.R * (k % 2 == 0 ? 0.13f : 0.10f);
                    float ca = MathF.Cos(a0), sa = MathF.Sin(a0);
                    var al = (u * ca) + (v * sa);
                    var ac = (-u * sa) + (v * ca);
                    if (al > 0 && al < len && MathF.Abs(ac) < half * (1 - (al / len)))
                    {
                        if (ac > 0)
                        {
                            lit[x, y] = 1;
                        }
                        else
                        {
                            dark[x, y] = 1;
                        }
                    }
                }

                var ring = MathF.Exp(-MathF.Pow((r - (c.R * 1.05f)) / 0.6f, 2)) + MathF.Exp(-MathF.Pow((r - (c.R * 0.36f)) / 0.5f, 2));
                var ticks = MathF.Exp(-MathF.Pow((r - (c.R * 1.12f)) / 2.2f, 2)) * (MathF.Abs(MathF.Sin(ang * 36)) < 0.12f ? 1 : 0);
                ringTicks[x, y] = (Math.Clamp(ring, 0f, 1f) * 0.8f) + (ticks * 0.6f);
            }
        }

        var litB = MoonfallFilters.Blur(lit, 0.4f * s);
        var darkB = MoonfallFilters.Blur(dark, 0.4f * s);
        var gilt = MoonfallColor.Hex("#E6C27A");
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = ((y0 + y) * ctx.W) + x0 + x;
                var keep = MoonfallColor.Smooth(6f, 9f, ctx.ClearanceAtS.Data[i]);
                var f = 1 - (0.45f * darkB[x, y] * keep);
                var a = ((litB[x, y] * 0.9f) + ringTicks[x, y]) * keep * 0.55f;
                px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i] * f, gilt.X * a);
                px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i] * f, gilt.Y * a);
                px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i] * f, gilt.Z * a);
            }
        }
    }

    /// <summary>dress2.chart_border: the chart's neat-line on the frame's edge with alternating ticks, kept out of the launcher's span (x 315–485).</summary>
    public static void Neatline(MoonfallDressContext ctx, MoonfallImage px, MoonfallNeatline n)
    {
        const float WL = MoonfallFramingCheck.WallL, WR = MoonfallFramingCheck.WallR, Top = MoonfallFramingCheck.Top, Foot = MoonfallFramingCheck.Foot;
        var gilt = MoonfallColor.Hex("#E6C27A");
        var s = ctx.S;
        for (var y = 0; y < ctx.H; y++)
        {
            var Y = (y + 0.5f) / s;
            for (var x = 0; x < ctx.W; x++)
            {
                var X = (x + 0.5f) / s;
                if (!(X > WL && X < WR && Y > Top && Y < Foot) || (Y < Top + 6 && X > 315 && X < 485))
                {
                    continue;
                }

                var d = MathF.Min(MathF.Min(MathF.Abs(X - (WL + n.Inset)), MathF.Abs(X - (WR - n.Inset))), MathF.Min(MathF.Abs(Y - (Top + n.Inset)), MathF.Abs(Y - (Foot - n.Inset))));
                var i = (y * ctx.W) + x;
                var a = ctx.Cov(d - 0.5f) * 0.45f;
                var edge = MathF.Min(MathF.Min(X - WL, WR - X), MathF.Min(Y - Top, Foot - Y)) < n.Inset;
                var along = (Y - Top < n.Inset) || (Foot - Y < n.Inset) ? X : Y;
                var f = edge && ((int)MathF.Floor(along / 16f) % 2 == 0) ? 0.55f : 1f;
                px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], gilt.X * a) * f;
                px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], gilt.Y * a) * f;
                px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], gilt.Z * a) * f;
            }
        }
    }

    /// <summary>The design's Catmull-Rom through control points (layout.smooth_path), <paramref name="n"/> points a span.</summary>
    public static List<Vector2> SmoothPath(IReadOnlyList<Vector2> pts, int n)
    {
        var p = new List<Vector2>(pts.Count + 2) { pts[0] };
        p.AddRange(pts);
        p.Add(pts[^1]);
        var out_ = new List<Vector2>();
        for (var i = 1; i < p.Count - 2; i++)
        {
            Vector2 p0 = p[i - 1], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2];
            for (var k = 0; k < n; k++)
            {
                var t = k / (float)n;
                out_.Add(0.5f * ((2 * p1) + ((-p0 + p2) * t) + (((2 * p0) - (5 * p1) + (4 * p2) - p3) * t * t) + ((-p0 + (3 * p1) - (3 * p2) + p3) * t * t * t)));
            }
        }

        out_.Add(p[^2]);
        return out_;
    }

    /// <summary>scene_airship_road.dashed: an engraved dashed route, a dark cut whose lower-right lip catches the light.</summary>
    public static void Route(MoonfallDressContext ctx, MoonfallImage px, MoonfallRoute r)
    {
        var path = SmoothPath(r.Points, r.Smooth);
        var cum = new float[path.Count];
        for (var i = 1; i < path.Count; i++)
        {
            cum[i] = cum[i - 1] + Vector2.Distance(path[i], path[i - 1]);
        }

        var m = new MoonfallPlane(ctx.W, ctx.H);
        var half = r.Width / 2;
        for (var start = 0f; start < cum[^1]; start += r.Dash + r.Gap)
        {
            var end = MathF.Min(start + r.Dash, cum[^1]);
            var pts = new List<Vector2> { At(start) };
            for (var i = 0; i < cum.Length; i++)
            {
                if (cum[i] > start && cum[i] < end)
                {
                    pts.Add(path[i]);
                }
            }

            pts.Add(At(end));
            for (var k = 1; k < pts.Count; k++)
            {
                var a = pts[k - 1];
                var b = pts[k];
                var c = (a + b) / 2;
                ctx.Stamp(m, (X, Y) => Segment(new Vector2(X, Y), a, b) - half, c.X, c.Y, (Vector2.Distance(a, b) / 2) + half + 2);
            }
        }

        var shift = Math.Max(1, (int)(ctx.S * 0.6f));
        for (var y = 0; y < ctx.H; y++)
        {
            for (var x = 0; x < ctx.W; x++)
            {
                var i = (y * ctx.W) + x;
                var sh = x >= shift && y >= shift ? m.Data[((y - shift) * ctx.W) + x - shift] : 0f;
                var cut = 1 - (0.35f * Math.Clamp(sh - m.Data[i], 0f, 1f));
                var a = m.Data[i] * r.Alpha;
                px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i] * cut, r.Colour.X * a);
                px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i] * cut, r.Colour.Y * a);
                px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i] * cut, r.Colour.Z * a);
            }
        }

        Vector2 At(float t)
        {
            for (var i = 1; i < cum.Length; i++)
            {
                if (t <= cum[i])
                {
                    var f = (t - cum[i - 1]) / MathF.Max(cum[i] - cum[i - 1], 1e-6f);
                    return Vector2.Lerp(path[i - 1], path[i], f);
                }
            }

            return path[^1];
        }
    }

    public static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return Vector2.Distance(p, a + (ab * t));
    }

    /// <summary>dress2.points: small lights, each a core and a halo, left out within 8 units (plus its halo) of a piece (F5).</summary>
    public static void Points(MoonfallDressContext ctx, MoonfallImage px, MoonfallSmallLight l)
    {
        if (ctx.Clear(l.X, l.Y) < 8 + (l.Halo * l.Size * 0.6f))
        {
            ctx.Dropped++;
            return;
        }

        ctx.Lights.Add(new Vector3(l.X, l.Y, l.Halo * l.Size));
        var (x0, y0, x1, y1) = ctx.Window(l.X, l.Y, MathF.Max(l.Halo * l.Size * 3, l.Core * l.Size * 3));
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var X = (x + 0.5f) / ctx.S;
                var Y = (y + 0.5f) / ctx.S;
                var d = MathF.Sqrt(((X - l.X) * (X - l.X)) + ((Y - l.Y) * (Y - l.Y)));
                var dc = d / (l.Core * l.Size);
                var dh = l.Halo > 0 ? d / (l.Halo * l.Size) : 1e9f;
                var a = (MathF.Exp(-dc * dc) * l.K) + (MathF.Exp(-dh * dh) * l.HaloK);
                var i = (y * ctx.W) + x;
                px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], l.Colour.X * a);
                px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], l.Colour.Y * a);
                px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], l.Colour.Z * a);
            }
        }
    }

    /// <summary>The readability veil (board.veil): the scene dims by up to k behind and just round the layout.</summary>
    public static void Veil(MoonfallDressContext ctx, MoonfallImage px, float k, float grow = 18f)
    {
        if (k <= 0)
        {
            return;
        }

        var m = new MoonfallPlane(ctx.W, ctx.H);
        for (var i = 0; i < m.Data.Length; i++)
        {
            m.Data[i] = MoonfallColor.Smooth(grow, 0f, ctx.ClearanceAtS.Data[i]);
        }

        m = MoonfallFilters.Blur(m, 6 * ctx.S);
        for (var i = 0; i < m.Data.Length; i++)
        {
            var f = 1 - (k * m.Data[i]);
            px.R.Data[i] *= f;
            px.G.Data[i] *= f;
            px.B.Data[i] *= f;
        }
    }
}
