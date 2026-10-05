using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>What the fuller-board checks measured on one dressed scene (level-method.md §8; framecheck.check).</summary>
public sealed record MoonfallFramingReport
{
    /// <summary>F2: framing's share of the opening, percent (at most 12).</summary>
    public float CoverPercent { get; init; }

    /// <summary>F2: its share of the open middle, percent (under 0.2).</summary>
    public float MiddlePercent { get; init; }

    /// <summary>F2: framing pixels within 100 units of the launcher's pivot (none).</summary>
    public int InSwingPixels { get; init; }

    /// <summary>F3a: the smallest distance from framing (coverage over 0.5) to a piece's edge, units (at least 6); null with no framing.</summary>
    public float? MinClearance { get; init; }

    /// <summary>F3b: the longest connected run of rim light, units (at most 30).</summary>
    public float LongestRimRun { get; init; }

    /// <summary>F3c: the longest straight outline run found (centre, length), or null (none allowed).</summary>
    public (Vector2 At, float Length)? StraightRun { get; init; }

    /// <summary>F3d: peg-sized discs or holes in the framing (none allowed).</summary>
    public IReadOnlyList<Vector2> PegSizedDiscs { get; init; } = [];

    /// <summary>F5: small lights within 8 units of a piece (none allowed).</summary>
    public IReadOnlyList<Vector2> LightsTooClose { get; init; } = [];

    /// <summary>F3a's pixel backstop: pixels 0.06 or more darker than the graded scene within 6 units of a piece (none allowed).</summary>
    public int DarkenedTooClose { get; init; }

    /// <summary>Every rule that failed (F2, F3a, F3a-pixels, F3b, F3c, F3d, F5).</summary>
    public IReadOnlyList<string> Fails { get; init; } = [];

    public bool Ok => Fails.Count == 0;
}

/// <summary>
/// The fuller-board rules measured on a dressed scene, ported from the design's checker (docs/design/v9/rich2/src/
/// framecheck.py), at one cell a unit: F2 (framing at the edges), F3a (6 units clear of every piece, and the pixel
/// backstop), F3b (rim light in short runs), F3c (no straight outline), F3d (no peg-sized disc or hole) and F5 (small
/// lights 8 units clear). The scene build guarantees F3a and F5 by construction (each leaf and light that would come too
/// close is dropped); this check runs on every shipped recipe in the tests and, in Debug builds, on every build.
/// </summary>
public static class MoonfallFramingCheck
{
    public const float WallL = 75f, WallR = 725f, Top = 41f, Foot = 594f;

    /// <summary>The launcher's pivot.</summary>
    public static readonly Vector2 Pivot = new(400f, 87f);

    /// <summary>
    /// Measures the framing <paramref name="cover"/> and its <paramref name="rim"/> light (both at <paramref name="s"/>
    /// pixels a unit, s a whole number), the small <paramref name="lights"/> and, when given, the lightness before and
    /// after the dress (the backstop) against the pieces' <paramref name="clearance"/>.
    /// </summary>
    public static MoonfallFramingReport Check(MoonfallPlane cover, MoonfallPlane rim, IReadOnlyList<Vector3> lights, MoonfallClearance clearance, int s,
        MoonfallPlane? lightnessBefore = null, MoonfallPlane? lightnessAfter = null)
    {
        ArgumentNullException.ThrowIfNull(cover);
        ArgumentNullException.ThrowIfNull(rim);
        ArgumentNullException.ThrowIfNull(lights);
        ArgumentNullException.ThrowIfNull(clearance);
        var m1 = Box(cover, s);
        var r1 = Box(rim, s);
        const int W = 800, H = 600;
        var fails = new List<string>();
        double coverSum = 0, middleSum = 0;
        int opening = 0, middle = 0, swing = 0;
        float? minClear = null;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                float X = x + 0.5f, Y = y + 0.5f;
                if (!Opening(X, Y))
                {
                    continue;
                }

                var v = m1.Data[(y * W) + x];
                opening++;
                coverSum += v;
                if (X > WallL + 120 && X < WallR - 120 && Y > Top + 120 && Y < 470)
                {
                    middle++;
                    middleSum += v;
                }

                if (v > 0.5f)
                {
                    if (Vector2.Distance(new Vector2(X, Y), Pivot) < 100)
                    {
                        swing++;
                    }

                    var d = clearance.At(X, Y);
                    minClear = minClear is { } c ? MathF.Min(c, d) : d;
                }
            }
        }

        var coverPct = (float)(coverSum / opening * 100);
        var middlePct = (float)(middleSum / Math.Max(1, middle) * 100);
        if (coverPct > 12 || middlePct > 0.2f || swing > 0)
        {
            fails.Add("F2");
        }

        if (minClear is < 6)
        {
            fails.Add("F3a");
        }

        // F3b: connected runs of rim light.
        var rimMask = new bool[W * H];
        for (var i = 0; i < rimMask.Length; i++)
        {
            rimMask[i] = r1.Data[i] > 0.12f && Opening((i % W) + 0.5f, (i / W) + 0.5f);
        }

        var longest = 0f;
        foreach (var comp in Components(rimMask, W, H))
        {
            if (comp.Count < 3)
            {
                continue;
            }

            var (x0, y0, x1, y1) = Bounds(comp, W);
            longest = MathF.Max(longest, MathF.Sqrt(((x1 - x0 + 1) * (x1 - x0 + 1)) + ((y1 - y0 + 1) * (y1 - y0 + 1))));
        }

        if (longest > 30)
        {
            fails.Add("F3b");
        }

        var straight = StraightRuns(WithoutThin(m1));
        if (straight is not null)
        {
            fails.Add("F3c");
        }

        var discs = RoundShapes(m1);
        if (discs.Count > 0)
        {
            fails.Add("F3d");
        }

        var close = new List<Vector2>();
        foreach (var light in lights)
        {
            if (clearance.At(Math.Clamp(light.X, 0, W - 1), Math.Clamp(light.Y, 0, H - 1)) < 8)
            {
                close.Add(new Vector2(light.X, light.Y));
            }
        }

        if (close.Count > 0)
        {
            fails.Add("F5");
        }

        var darkened = 0;
        if (lightnessBefore is not null && lightnessAfter is not null)
        {
            var drop = new MoonfallPlane(lightnessBefore.Width, lightnessBefore.Height);
            for (var i = 0; i < drop.Data.Length; i++)
            {
                drop.Data[i] = lightnessBefore.Data[i] - lightnessAfter.Data[i];
            }

            var d1 = Box(drop, s);
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    if (d1.Data[(y * W) + x] >= 0.06f && Opening(x + 0.5f, y + 0.5f) && clearance.At(x + 0.5f, y + 0.5f) < 6)
                    {
                        darkened++;
                    }
                }
            }

            if (darkened > 0)
            {
                fails.Add("F3a-pixels");
            }
        }

        return new MoonfallFramingReport
        {
            CoverPercent = coverPct,
            MiddlePercent = middlePct,
            InSwingPixels = swing,
            MinClearance = minClear,
            LongestRimRun = longest,
            StraightRun = straight,
            PegSizedDiscs = discs,
            LightsTooClose = close,
            DarkenedTooClose = darkened,
            Fails = fails,
        };
    }

    /// <summary>Whether a board point lies in the opening (inside the walls, below the top rail, above the foot).</summary>
    public static bool Opening(float x, float y) => x > WallL && x < WallR && y > Top && y < Foot;

    /// <summary>A plane at s pixels a unit averaged down to one cell a unit (PIL's BOX resize).</summary>
    public static MoonfallPlane Box(MoonfallPlane p, int s)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (s <= 1)
        {
            return p;
        }

        var w = p.Width / s;
        var h = p.Height / s;
        var out_ = new MoonfallPlane(w, h);
        var k = 1f / (s * s);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var sum = 0f;
                for (var dy = 0; dy < s; dy++)
                {
                    for (var dx = 0; dx < s; dx++)
                    {
                        sum += p.Data[(((y * s) + dy) * p.Width) + (x * s) + dx];
                    }
                }

                out_.Data[(y * w) + x] = sum * k;
            }
        }

        return out_;
    }

    /// <summary>
    /// F3c (framecheck.straight_runs): outline points split by the side they face; round every third, the neighbourhood
    /// of radius span/2 + 2 is fitted with a line; a run is straight when its points span 0.95 × span within
    /// <paramref name="tol"/> of the line and 95% of them lie that close, further than <paramref name="inner"/> inside the opening.
    /// </summary>
    public static (Vector2 At, float Length)? StraightRuns(MoonfallPlane mask, float inner = 15f, float span = 36f, float tol = 0.75f)
    {
        ArgumentNullException.ThrowIfNull(mask);
        int w = mask.Width, h = mask.Height;
        bool On(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mask.Data[(y * w) + x] > 0.5f;
        var radius = (span / 2) + 2;
        (Vector2, float)? worst = null;
        foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var xs = new List<float>();
            var ys = new List<float>();
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (On(x, y) && !On(x + ox, y + oy))
                    {
                        float X = x + 0.5f, Y = y + 0.5f;
                        if (X > WallL + inner && X < WallR - inner && Y > Top + inner && Y < Foot - inner)
                        {
                            xs.Add(X);
                            ys.Add(Y);
                        }
                    }
                }
            }

            var near = new List<int>();
            var along = new List<float>();
            for (var i = 0; i < xs.Count; i += 3)
            {
                near.Clear();
                for (var j = 0; j < xs.Count; j++)
                {
                    var dx = xs[j] - xs[i];
                    var dy = ys[j] - ys[i];
                    if ((dx * dx) + (dy * dy) < radius * radius)
                    {
                        near.Add(j);
                    }
                }

                if (near.Count < span * 0.8f)
                {
                    continue;
                }

                double mx = 0, my = 0;
                foreach (var j in near)
                {
                    mx += xs[j];
                    my += ys[j];
                }

                mx /= near.Count;
                my /= near.Count;
                double sxx = 0, syy = 0, sxy = 0;
                foreach (var j in near)
                {
                    var dx = xs[j] - mx;
                    var dy = ys[j] - my;
                    sxx += dx * dx;
                    syy += dy * dy;
                    sxy += dx * dy;
                }

                // The principal axis of the neighbourhood (the first right-singular vector).
                var angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
                double ux = Math.Cos(angle), uy = Math.Sin(angle);
                along.Clear();
                var within = 0;
                foreach (var j in near)
                {
                    var dx = xs[j] - mx;
                    var dy = ys[j] - my;
                    var resid = Math.Abs((-uy * dx) + (ux * dy));
                    if (resid < tol)
                    {
                        within++;
                        along.Add((float)((ux * dx) + (uy * dy)));
                    }
                }

                if (along.Count < 2)
                {
                    continue;
                }

                along.Sort();
                float best = 0, start = along[0];
                for (var k = 1; k < along.Count; k++)
                {
                    if (along[k] - along[k - 1] > 2f)
                    {
                        start = along[k];
                    }

                    best = MathF.Max(best, along[k] - start);
                }

                if (best >= span * 0.95f && within >= near.Count * 0.95f && (worst is null || best > worst.Value.Item2))
                {
                    worst = (new Vector2(xs[i], ys[i]), best);
                }
            }
        }

        return worst;
    }

    /// <summary>The framing less its strokes 4 units wide or narrower (a morphological opening by a 5 × 5 square; framecheck.without_thin).</summary>
    public static MoonfallPlane WithoutThin(MoonfallPlane mask, int width = 5)
    {
        ArgumentNullException.ThrowIfNull(mask);
        int w = mask.Width, h = mask.Height;
        bool On(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mask.Data[(y * w) + x] > 0.5f;
        var eroded = new bool[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var all = true;
                for (var dy = 0; dy < width && all; dy++)
                {
                    for (var dx = 0; dx < width && all; dx++)
                    {
                        all = On(x + dx, y + dy);
                    }
                }

                eroded[(y * w) + x] = all;
            }
        }

        var out_ = new MoonfallPlane(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var any = false;
                for (var dy = 0; dy < width && !any; dy++)
                {
                    for (var dx = 0; dx < width && !any; dx++)
                    {
                        int ex = x - dx, ey = y - dy;
                        any = ex >= 0 && ey >= 0 && eroded[(ey * w) + ex];
                    }
                }

                out_.Data[(y * w) + x] = any ? 1f : 0f;
            }
        }

        return out_;
    }

    /// <summary>F3d (framecheck.round_shapes): framing components, and holes in it, 10–26 units across, near square and filling a disc.</summary>
    public static List<Vector2> RoundShapes(MoonfallPlane m1)
    {
        ArgumentNullException.ThrowIfNull(m1);
        int w = m1.Width, h = m1.Height;
        var found = new List<Vector2>();
        var framing = new bool[w * h];
        var background = new bool[w * h];
        for (var i = 0; i < framing.Length; i++)
        {
            var on = m1.Data[i] > 0.5f;
            background[i] = !on;
            framing[i] = on && Opening((i % w) + 0.5f, (i / w) + 0.5f);
        }

        var targets = new List<List<int>>();
        targets.AddRange(Components(framing, w, h));
        foreach (var hole in Components(background, w, h))
        {
            if (hole.Count > 2500)
            {
                continue;
            }

            targets.Add(hole.Where(i => Opening((i % w) + 0.5f, (i / w) + 0.5f)).ToList());
        }

        foreach (var comp in targets)
        {
            if (comp.Count == 0)
            {
                continue;
            }

            // A target's own pieces (a hole cut by the opening may split).
            var mask = new bool[w * h];
            foreach (var i in comp)
            {
                mask[i] = true;
            }

            foreach (var part in Components(mask, w, h))
            {
                var (x0, y0, x1, y1) = Bounds(part, w);
                int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
                if (bw is >= 10 and <= 26 && bh is >= 10 and <= 26 && Math.Abs(bw - bh) <= 3)
                {
                    var fill = part.Count / (MathF.PI * (bw / 2f) * (bh / 2f));
                    if (fill is >= 0.85f and <= 1.12f)
                    {
                        found.Add(new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f));
                    }
                }
            }
        }

        return found;
    }

    private static (int X0, int Y0, int X1, int Y1) Bounds(List<int> comp, int w)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var i in comp)
        {
            int x = i % w, y = i / w;
            x0 = Math.Min(x0, x);
            y0 = Math.Min(y0, y);
            x1 = Math.Max(x1, x);
            y1 = Math.Max(y1, y);
        }

        return (x0, y0, x1, y1);
    }

    /// <summary>4-neighbour connected components of a mask, as lists of indices.</summary>
    public static List<List<int>> Components(bool[] mask, int w, int h)
    {
        ArgumentNullException.ThrowIfNull(mask);
        var seen = new bool[mask.Length];
        var comps = new List<List<int>>();
        var stack = new Stack<int>();
        for (var start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start])
            {
                continue;
            }

            var comp = new List<int>();
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                comp.Add(i);
                int x = i % w, y = i / w;
                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
            }

            comps.Add(comp);
        }

        return comps;

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                return;
            }

            var j = (y * w) + x;
            if (mask[j] && !seen[j])
            {
                seen[j] = true;
                stack.Push(j);
            }
        }
    }
}
