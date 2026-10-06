using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The level pipeline's parts of a scene (tools/moonfall-levels, converted by <c>mflkit/convert.py</c>): erase boxes on the
/// source, the mask terms the pipeline draws (a polygon, a band round a polyline, a map's land), the palette's spared
/// places, a tone on the painting, and the plates that carry its dress. Each is the pipeline's own arithmetic, so the
/// converter's gate (the scene the game builds against the scene the pipeline measured) holds.
/// </summary>
public static partial class MoonfallSceneBuilder
{
    /// <summary>How far the erase reaches beyond a box for the colour it fills with (source pixels).</summary>
    private const int ErasePad = 12;

    /// <summary>The plates a recipe lays (paint and light), by picture name, in order.</summary>
    public static IReadOnlyList<string> PlateNames(MoonfallSceneRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.Paint.Concat(recipe.Light).OfType<MoonfallPlate>().Select(static p => p.Picture).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Fills each box (x, y, w, h, source pixels) from its surroundings (the pipeline's <c>scene._erase</c>): the box takes the
    /// mean of a 12-pixel ring round it, diffused inward by forty blurs that keep the ring fixed, then laid in with a 3-pixel
    /// feather. A map's exit arrows leave plain parchment behind. Returns a new picture; alpha is kept as it was.
    /// </summary>
    public static MoonfallImage Erase(MoonfallImage source, IReadOnlyList<Vector4> boxes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(boxes);
        var out_ = source.Copy();
        int sw = source.Width, sh = source.Height;
        foreach (var box in boxes)
        {
            int x0 = Math.Max(0, (int)box.X), y0 = Math.Max(0, (int)box.Y);
            int x1 = Math.Min(sw, (int)(box.X + box.Z)), y1 = Math.Min(sh, (int)(box.Y + box.W));
            if (x1 <= x0 || y1 <= y0)
            {
                continue;
            }

            int px0 = Math.Max(0, x0 - ErasePad), py0 = Math.Max(0, y0 - ErasePad), px1 = Math.Min(sw, x1 + ErasePad), py1 = Math.Min(sh, y1 + ErasePad);
            int w = px1 - px0, h = py1 - py0;
            var inside = new MoonfallPlane(w, h);
            for (var y = y0 - py0; y < y1 - py0; y++)
            {
                for (var x = x0 - px0; x < x1 - px0; x++)
                {
                    inside.Data[(y * w) + x] = 1;
                }
            }

            var feather = MoonfallFilters.Blur(inside, 3f);
            foreach (var plane in new[] { out_.R, out_.G, out_.B })
            {
                var patch = new MoonfallPlane(w, h);
                for (var y = 0; y < h; y++)
                {
                    Array.Copy(plane.Data, ((py0 + y) * sw) + px0, patch.Data, y * w, w);
                }

                double sum = 0;
                var n = 0;
                for (var i = 0; i < patch.Data.Length; i++)
                {
                    if (inside.Data[i] == 0)
                    {
                        sum += patch.Data[i];
                        n++;
                    }
                }

                var mean = n > 0 ? (float)(sum / n) : 0f;
                var fill = patch.Copy();
                for (var i = 0; i < fill.Data.Length; i++)
                {
                    if (inside.Data[i] > 0)
                    {
                        fill.Data[i] = mean;
                    }
                }

                for (var round = 0; round < 40; round++)
                {
                    fill = MoonfallFilters.Blur(fill, 3f);
                    for (var i = 0; i < fill.Data.Length; i++)
                    {
                        if (inside.Data[i] == 0)
                        {
                            fill.Data[i] = patch.Data[i];
                        }
                    }
                }

                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var i = (y * w) + x;
                        var m = feather.Data[i];
                        plane.Data[((py0 + y) * sw) + px0 + x] = (patch.Data[i] * (1 - m)) + (fill.Data[i] * m);
                    }
                }
            }
        }

        return out_;
    }

    /// <summary>
    /// A map's land (the pipeline's <c>scene.derived_mask</c>, rim-fill), at <paramref name="s"/> pixels a unit, 0 or 1 before
    /// the term's own blur: the source cut at 1 px a unit (<paramref name="cut1"/>, 800 × 600, ungraded), its OKLab lightness
    /// blurred 1.5 px; under <paramref name="threshold"/> is a wall (a map's dark rim), grown <paramref name="grow"/> pixels (four
    /// neighbours a step, wrapping at the sides as the pipeline's does); what a flood from the board's four corners cannot reach
    /// is land. Doubled bilinearly at 2 px a unit.
    /// </summary>
    public static MoonfallPlane LandMask(MoonfallImage cut1, float threshold, int grow, int s)
    {
        ArgumentNullException.ThrowIfNull(cut1);
        const int W = 800, H = 600;
        if (cut1.Width != W || cut1.Height != H)
        {
            throw new ArgumentException("The land is worked out on the source cut at 1 px a unit (800 × 600).", nameof(cut1));
        }

        var light = MoonfallFilters.Blur(MoonfallGrade.Lightness(cut1), 1.5f);
        var wall = new bool[W * H];
        for (var i = 0; i < wall.Length; i++)
        {
            wall[i] = light.Data[i] < threshold;
        }

        for (var g = 0; g < grow; g++)
        {
            var next = (bool[])wall.Clone();
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    if (wall[(((y + 1) % H) * W) + x] || wall[(((y + H - 1) % H) * W) + x] || wall[(y * W) + ((x + 1) % W)] || wall[(y * W) + ((x + W - 1) % W)])
                    {
                        next[(y * W) + x] = true;
                    }
                }
            }

            wall = next;
        }

        var outside = new bool[W * H];
        var queue = new Queue<int>();
        foreach (var (sx, sy) in new[] { (5, 5), (795, 5), (5, 595), (795, 595) })
        {
            var i = (sy * W) + sx;
            if (!wall[i] && !outside[i])
            {
                outside[i] = true;
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            int x = i % W, y = i / W;
            foreach (var (nx, ny) in new[] { (x, y + 1), (x, y - 1), (x + 1, y), (x - 1, y) })
            {
                if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                {
                    continue;
                }

                var j = (ny * W) + nx;
                if (!outside[j] && !wall[j])
                {
                    outside[j] = true;
                    queue.Enqueue(j);
                }
            }
        }

        var land = new MoonfallPlane(W, H);
        for (var i = 0; i < land.Data.Length; i++)
        {
            land.Data[i] = outside[i] ? 0f : 1f;
        }

        if (s == 1)
        {
            return land;
        }

        // PIL's bilinear enlargement: each pixel samples the 1x mask at its own centre, held at the edges.
        var up = new MoonfallPlane(W * s, H * s);
        for (var y = 0; y < H * s; y++)
        {
            var v = Math.Clamp(((y + 0.5f) / s) - 0.5f, 0f, H - 1);
            var y0 = (int)v;
            var y1 = Math.Min(H - 1, y0 + 1);
            var ty = v - y0;
            for (var x = 0; x < W * s; x++)
            {
                var u = Math.Clamp(((x + 0.5f) / s) - 0.5f, 0f, W - 1);
                var x0 = (int)u;
                var x1 = Math.Min(W - 1, x0 + 1);
                var tx = u - x0;
                var top = (land.Data[(y0 * W) + x0] * (1 - tx)) + (land.Data[(y0 * W) + x1] * tx);
                var bottom = (land.Data[(y1 * W) + x0] * (1 - tx)) + (land.Data[(y1 * W) + x1] * tx);
                up.Data[(y * W * s) + x] = (top * (1 - ty)) + (bottom * ty);
            }
        }

        return up;
    }

    /// <summary>
    /// A closed polygon (board units) filled at <paramref name="s"/> pixels a unit (the pipeline's <c>dress.poly_mask</c>
    /// before its blur): drawn at twice the resolution, a sub-pixel inside when its centre is (PIL's convention, centres on the
    /// whole numbers), then averaged two by two.
    /// </summary>
    public static MoonfallPlane PolygonFill(IReadOnlyList<Vector2> points, int width, int height, float s)
    {
        ArgumentNullException.ThrowIfNull(points);
        var plane = new MoonfallPlane(width, height);
        if (points.Count < 3)
        {
            return plane;
        }

        var ss = 2 * s;
        // Each pixel row takes its own two rows of sub-pixels, so the rows run in parallel without sharing a pixel.
        MoonfallParallel.For(0, height, row =>
        {
            var crossings = new List<float>();
            for (var sy = row * 2; sy < (row * 2) + 2; sy++)
            {
                var yy = sy / ss;
                crossings.Clear();
                for (var k = 0; k < points.Count; k++)
                {
                    var a = points[k];
                    var b = points[(k + 1) % points.Count];
                    if ((a.Y <= yy && b.Y > yy) || (b.Y <= yy && a.Y > yy))
                    {
                        crossings.Add(a.X + ((yy - a.Y) / (b.Y - a.Y) * (b.X - a.X)));
                    }
                }

                crossings.Sort();
                for (var c = 0; c + 1 < crossings.Count; c += 2)
                {
                    // Sub-pixels whose centres (sx / ss units) lie within the span.
                    var from = Math.Max(0, (int)MathF.Ceiling(crossings[c] * ss));
                    var to = Math.Min((width * 2) - 1, (int)MathF.Floor(crossings[c + 1] * ss));
                    for (var sx = from; sx <= to; sx++)
                    {
                        plane.Data[(row * width) + (sx / 2)] += 0.25f;
                    }
                }
            }
        });

        return plane;
    }

    /// <summary>The distance (board units) from each pixel's centre to the polyline <paramref name="points"/>.</summary>
    public static MoonfallPlane PolylineDistance(IReadOnlyList<Vector2> points, int width, int height, float s)
    {
        ArgumentNullException.ThrowIfNull(points);
        var plane = new MoonfallPlane(width, height);
        MoonfallParallel.For(0, height, y =>
        {
            var Y = (y + 0.5f) / s;
            for (var x = 0; x < width; x++)
            {
                var p = new Vector2((x + 0.5f) / s, Y);
                var best = float.MaxValue;
                for (var k = 0; k + 1 < points.Count; k++)
                {
                    best = MathF.Min(best, MoonfallDress.Segment(p, points[k], points[k + 1]));
                }

                plane.Data[(y * width) + x] = best;
            }
        });
        return plane;
    }

    /// <summary>
    /// A mask: <paramref name="weight"/> times each term's value (scaled; one less it when inverted). <paramref name="lightness"/>
    /// gives the scene's OKLab lightness for <c>lum</c> terms (worked out once, on first use).
    /// </summary>
    private static MoonfallPlane Mask(MoonfallDressContext ctx, IReadOnlyList<MoonfallMaskTerm> terms, float weight, Func<MoonfallPlane> lightness)
    {
        var m = MoonfallPlane.Filled(ctx.W, ctx.H, weight);
        foreach (var t in terms)
        {
            MoonfallPlane? source = t.Kind switch
            {
                MoonfallMaskKind.Lum => t.Blur > 0 ? MoonfallFilters.Blur(lightness(), t.Blur * ctx.S) : lightness(),
                MoonfallMaskKind.Near => t.Blur > 0 ? MoonfallFilters.Blur(ctx.ClearanceAtS, t.Blur * ctx.S) : ctx.ClearanceAtS,
                MoonfallMaskKind.Poly => MoonfallFilters.Blur(PolygonFill(t.Points, ctx.W, ctx.H, ctx.S), t.Blur * ctx.S),
                MoonfallMaskKind.Line => PolylineDistance(t.Points, ctx.W, ctx.H, ctx.S),
                MoonfallMaskKind.Land => MoonfallFilters.Blur(ctx.Land(t.Args[0], (int)t.Args[1]), t.Blur * ctx.S),
                _ => null,
            };

            // Each pixel on its own, so the rows run in parallel (the same result, a few times sooner).
            MoonfallParallel.For(0, ctx.H, y =>
            {
                var Y = (y + 0.5f) / ctx.S;
                var a = t.Args;
                for (var x = 0; x < ctx.W; x++)
                {
                    var X = (x + 0.5f) / ctx.S;
                    var i = (y * ctx.W) + x;
                    var v = t.Kind switch
                    {
                        MoonfallMaskKind.Lum or MoonfallMaskKind.Near or MoonfallMaskKind.Line => MoonfallColor.Smooth(a[0], a[1], source!.Data[i]),
                        MoonfallMaskKind.Poly or MoonfallMaskKind.Land => source!.Data[i],
                        MoonfallMaskKind.Y => MoonfallColor.Smooth(a[0], a[1], Y),
                        MoonfallMaskKind.X => MoonfallColor.Smooth(a[0], a[1], X),
                        _ => MoonfallColor.Smooth(a[2] + a[3] + 1e-3f, a[2] - a[3], MathF.Sqrt(((X - a[0]) * (X - a[0])) + ((Y - a[1]) * (Y - a[1])))),
                    };
                    v *= t.Scale;
                    m.Data[i] *= t.Invert ? 1 - v : v;
                }
            });
        }

        return m;
    }

    /// <summary>Whether any of the recipe's masks reads the painting's land.</summary>
    private static bool UsesLand(MoonfallSceneRecipe recipe)
    {
        var terms = recipe.Paint.OfType<MoonfallTone>().SelectMany(static t => t.Where);
        if (recipe.Palette is { } p)
        {
            terms = terms.Concat(p.Where).Concat(p.Regions.SelectMany(static r => r.Where)).Concat(p.Spare.SelectMany(static s => s));
        }

        return terms.Any(static t => t.Kind == MoonfallMaskKind.Land);
    }

    /// <summary>A tone: the painting made lighter or darker where its mask is.</summary>
    private static void Tone(MoonfallDressContext ctx, MoonfallImage px, MoonfallTone tone)
    {
        var m = Mask(ctx, tone.Where, 1f, () => MoonfallGrade.Lightness(px));
        var k = 1 - tone.Mul;
        for (var i = 0; i < m.Data.Length; i++)
        {
            var f = 1 - (k * m.Data[i]);
            px.R.Data[i] *= f;
            px.G.Data[i] *= f;
            px.B.Data[i] *= f;
        }
    }

    /// <summary>A plate laid over the scene; a missing one is left out (and counted in <see cref="MoonfallSceneLayers.Dropped"/>).</summary>
    private static void Plate(MoonfallDressContext ctx, MoonfallImage px, MoonfallPlate plate)
    {
        if (ctx.Plates is null || !ctx.Plates.TryGetValue(plate.Picture, out var picture))
        {
            ctx.Dropped++;
            return;
        }

        var p = picture.Width == ctx.W && picture.Height == ctx.H ? picture : MoonfallFilters.Resize(picture, ctx.W, ctx.H);
        var w = ctx.W;
        MoonfallParallel.For(0, ctx.H, y =>
        {
            for (var i = y * w; i < (y + 1) * w; i++)
            {
                float r = p.R.Data[i], g = p.G.Data[i], b = p.B.Data[i];
                switch (plate.Blend)
                {
                    case MoonfallPlateBlend.Screen:
                        px.R.Data[i] = MoonfallColor.Screen(px.R.Data[i], r);
                        px.G.Data[i] = MoonfallColor.Screen(px.G.Data[i], g);
                        px.B.Data[i] = MoonfallColor.Screen(px.B.Data[i], b);
                        break;
                    case MoonfallPlateBlend.Multiply:
                        px.R.Data[i] *= r;
                        px.G.Data[i] *= g;
                        px.B.Data[i] *= b;
                        if (plate.Cover)
                        {
                            ctx.Cover.Data[i] = MathF.Max(ctx.Cover.Data[i], 1 - ((r + g + b) / 3f));
                        }

                        break;
                    default:
                        px.R.Data[i] = MathF.Min(1f, px.R.Data[i] + r);
                        px.G.Data[i] = MathF.Min(1f, px.G.Data[i] + g);
                        px.B.Data[i] = MathF.Min(1f, px.B.Data[i] + b);
                        break;
                }
            }
        });
    }
}
