namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The filters the grades use, ported exactly from the design kit where the approved renders depend on them:
/// <see cref="Blur"/> is artlib's Gaussian by three box passes per axis with the edge held (v8/art/src/artlib.py),
/// <see cref="Gradient"/> is numpy's <c>gradient</c> and <see cref="Percentile"/> numpy's linear <c>percentile</c>.
/// <see cref="Resample"/> is a separable Lanczos-3, as PIL's <c>LANCZOS</c> resize the design crops with.
/// Row work runs in parallel: these run off the framework thread on whole scenes.
/// </summary>
public static class MoonfallFilters
{
    /// <summary>artlib's <c>blur</c>: three passes of a centred box (edge-clamped) down then across; none at sigma 0.3 or less.</summary>
    public static MoonfallPlane Blur(MoonfallPlane source, float sigma)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sigma <= 0.3f)
        {
            return source.Copy();
        }

        var w = Math.Sqrt((12.0 * sigma * sigma / 3.0) + 1.0);
        var r = Math.Max(1, (int)Math.Floor(w / 2.0));
        var a = source.Copy();
        var b = new MoonfallPlane(source.Width, source.Height);
        for (var pass = 0; pass < 3; pass++)
        {
            BoxColumns(a, b, r);
            BoxRows(b, a, r);
        }

        return a;
    }

    /// <summary>Blurs each colour plane of <paramref name="image"/> (alpha untouched).</summary>
    public static MoonfallImage Blur(MoonfallImage image, float sigma)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new MoonfallImage(Blur(image.R, sigma), Blur(image.G, sigma), Blur(image.B, sigma), image.A?.Copy());
    }

    private static void BoxColumns(MoonfallPlane from, MoonfallPlane to, int r)
    {
        int w = from.Width, h = from.Height;
        var src = from.Data;
        var dst = to.Data;
        var n = (2 * r) + 1;
        const int Block = 64;
        var blocks = (w + Block - 1) / Block;
        MoonfallParallel.For(0, blocks, block =>
        {
            // A band of columns walked down together, so every read and write runs along a row (the running sum over
            // rows y - r .. y + r, each held at the edge: numpy's pad mode "edge").
            var x0 = block * Block;
            var x1 = Math.Min(w, x0 + Block);
            Span<double> sums = stackalloc double[Block];
            for (var k = -r; k <= r; k++)
            {
                var o = Math.Clamp(k, 0, h - 1) * w;
                for (var x = x0; x < x1; x++)
                {
                    sums[x - x0] += src[o + x];
                }
            }

            for (var y = 0; y < h; y++)
            {
                var o = y * w;
                var add = Math.Min(y + r + 1, h - 1) * w;
                var sub = Math.Max(y - r, 0) * w;
                for (var x = x0; x < x1; x++)
                {
                    dst[o + x] = (float)(sums[x - x0] / n);
                    sums[x - x0] += src[add + x] - src[sub + x];
                }
            }
        });
    }

    private static void BoxRows(MoonfallPlane from, MoonfallPlane to, int r)
    {
        int w = from.Width, h = from.Height;
        var src = from.Data;
        var dst = to.Data;
        var n = (2 * r) + 1;
        MoonfallParallel.For(0, h, y =>
        {
            var o = y * w;
            double sum = 0;
            for (var k = -r; k <= r; k++)
            {
                sum += src[o + Math.Clamp(k, 0, w - 1)];
            }

            for (var x = 0; x < w; x++)
            {
                dst[o + x] = (float)(sum / n);
                sum += src[o + Math.Min(x + r + 1, w - 1)] - src[o + Math.Max(x - r, 0)];
            }
        });
    }

    /// <summary>Runs <paramref name="body"/> over 0..<paramref name="count"/> in parallel chunks (lo inclusive, hi exclusive).</summary>
    public static void Chunks(int count, Action<int, int> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        const int Size = 16384;
        MoonfallParallel.For(0, (count + Size - 1) / Size, c => body(c * Size, Math.Min(count, (c + 1) * Size)));
    }

    /// <summary>numpy's <c>gradient</c> with unit spacing: (d/dy, d/dx), central inside and one-sided at the edges.</summary>
    public static (MoonfallPlane Dy, MoonfallPlane Dx) Gradient(MoonfallPlane p)
    {
        ArgumentNullException.ThrowIfNull(p);
        int w = p.Width, h = p.Height;
        var dy = new MoonfallPlane(w, h);
        var dx = new MoonfallPlane(w, h);
        var d = p.Data;
        MoonfallParallel.For(0, h, y =>
        {
            var o = y * w;
            for (var x = 0; x < w; x++)
            {
                dx.Data[o + x] = w == 1 ? 0f
                    : x == 0 ? d[o + 1] - d[o]
                    : x == w - 1 ? d[o + x] - d[o + x - 1]
                    : (d[o + x + 1] - d[o + x - 1]) * 0.5f;
                dy.Data[o + x] = h == 1 ? 0f
                    : y == 0 ? d[w + x] - d[x]
                    : y == h - 1 ? d[o + x] - d[o - w + x]
                    : (d[o + w + x] - d[o - w + x]) * 0.5f;
            }
        });
        return (dy, dx);
    }

    /// <summary>numpy's linear <c>percentile</c> (q 0..100) of every value (exact, by selection rather than a full sort).</summary>
    public static float Percentile(MoonfallPlane p, float q)
    {
        ArgumentNullException.ThrowIfNull(p);
        var values = (float[])p.Data.Clone();
        var at = Math.Clamp(q, 0f, 100f) / 100.0 * (values.Length - 1);
        var i = (int)Math.Floor(at);
        var f = (float)(at - i);
        var low = Select(values, 0, values.Length - 1, i);
        if (i + 1 >= values.Length || f == 0)
        {
            return low;
        }

        // The next value up is the least of those the selection left above position i.
        var high = float.MaxValue;
        for (var k = i + 1; k < values.Length; k++)
        {
            high = MathF.Min(high, values[k]);
        }

        return low + ((high - low) * f);
    }

    /// <summary>The k-th smallest of values[lo..hi] (Hoare's selection, median-of-three), leaving smaller before and larger after.</summary>
    private static float Select(float[] v, int lo, int hi, int k)
    {
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (v[mid] < v[lo])
            {
                (v[mid], v[lo]) = (v[lo], v[mid]);
            }

            if (v[hi] < v[lo])
            {
                (v[hi], v[lo]) = (v[lo], v[hi]);
            }

            if (v[hi] < v[mid])
            {
                (v[hi], v[mid]) = (v[mid], v[hi]);
            }

            var pivot = v[mid];
            int i = lo, j = hi;
            while (i <= j)
            {
                while (v[i] < pivot)
                {
                    i++;
                }

                while (v[j] > pivot)
                {
                    j--;
                }

                if (i <= j)
                {
                    (v[i], v[j]) = (v[j], v[i]);
                    i++;
                    j--;
                }
            }

            if (k <= j)
            {
                hi = j;
            }
            else if (k >= i)
            {
                lo = i;
            }
            else
            {
                return v[k];
            }
        }

        return v[k];
    }

    /// <summary>
    /// The source rectangle (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>, <paramref name="h"/>, in
    /// source pixels, fractional allowed, parts outside held at the edge) resampled to <paramref name="width"/> ×
    /// <paramref name="height"/> with a separable Lanczos-3 (widened when shrinking, as PIL does).
    /// </summary>
    public static MoonfallPlane Resample(MoonfallPlane source, double x, double y, double w, double h, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        var across = Weights(x, w, width);
        var down = Weights(y, h, height);
        var mid = new float[source.Height * width];
        var src = source.Data;
        var sw = source.Width;
        MoonfallParallel.For(0, source.Height, row =>
        {
            var o = row * sw;
            for (var c = 0; c < width; c++)
            {
                var (start, kw) = across[c];
                double sum = 0;
                for (var k = 0; k < kw.Length; k++)
                {
                    sum += src[o + Math.Clamp(start + k, 0, sw - 1)] * kw[k];
                }

                mid[(row * width) + c] = (float)sum;
            }
        });
        var out_ = new MoonfallPlane(width, height);
        MoonfallParallel.For(0, height, r =>
        {
            var (start, kw) = down[r];
            for (var c = 0; c < width; c++)
            {
                double sum = 0;
                for (var k = 0; k < kw.Length; k++)
                {
                    sum += mid[(Math.Clamp(start + k, 0, source.Height - 1) * width) + c] * kw[k];
                }

                out_.Data[(r * width) + c] = (float)sum;
            }
        });
        return out_;
    }

    /// <summary>Every plane of <paramref name="image"/> resampled (see the plane overload).</summary>
    public static MoonfallImage Resample(MoonfallImage image, double x, double y, double w, double h, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new MoonfallImage(
            Clip(Resample(image.R, x, y, w, h, width, height)),
            Clip(Resample(image.G, x, y, w, h, width, height)),
            Clip(Resample(image.B, x, y, w, h, width, height)),
            image.A is { } a ? Clip(Resample(a, x, y, w, h, width, height)) : null);
    }

    /// <summary>The whole picture resized (see <see cref="Resample(MoonfallImage, double, double, double, double, int, int)"/>).</summary>
    public static MoonfallImage Resize(MoonfallImage image, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Resample(image, 0, 0, image.Width, image.Height, width, height);
    }

    /// <summary>Premultiplied resize of a picture with alpha (no dark fringes), as the design's <c>resample</c>.</summary>
    public static MoonfallImage ResizePremultiplied(MoonfallImage image, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.A is not { } a)
        {
            return Resize(image, width, height);
        }

        var pm = image.Copy();
        for (var i = 0; i < a.Data.Length; i++)
        {
            pm.R.Data[i] *= a.Data[i];
            pm.G.Data[i] *= a.Data[i];
            pm.B.Data[i] *= a.Data[i];
        }

        var out_ = Resize(pm, width, height);
        var oa = out_.A!;
        for (var i = 0; i < oa.Data.Length; i++)
        {
            var al = oa.Data[i];
            if (al > 1e-4f)
            {
                out_.R.Data[i] = Math.Clamp(out_.R.Data[i] / al, 0f, 1f);
                out_.G.Data[i] = Math.Clamp(out_.G.Data[i] / al, 0f, 1f);
                out_.B.Data[i] = Math.Clamp(out_.B.Data[i] / al, 0f, 1f);
            }
            else
            {
                out_.R.Data[i] = out_.G.Data[i] = out_.B.Data[i] = 0f;
            }
        }

        return out_;
    }

    private static MoonfallPlane Clip(MoonfallPlane p)
    {
        for (var i = 0; i < p.Data.Length; i++)
        {
            p.Data[i] = Math.Clamp(p.Data[i], 0f, 1f);
        }

        return p;
    }

    private static (int Start, double[] Weights)[] Weights(double from, double span, int size)
    {
        var scale = span / size;
        var support = 3.0 * Math.Max(1.0, scale);
        var filterScale = Math.Max(1.0, scale);
        var table = new (int, double[])[size];
        for (var i = 0; i < size; i++)
        {
            var centre = from + ((i + 0.5) * scale);
            var start = (int)Math.Floor(centre - support);
            var end = (int)Math.Ceiling(centre + support);
            var weights = new double[end - start];
            double total = 0;
            for (var k = 0; k < weights.Length; k++)
            {
                var t = (start + k + 0.5 - centre) / filterScale;
                weights[k] = Lanczos(t);
                total += weights[k];
            }

            if (total != 0)
            {
                for (var k = 0; k < weights.Length; k++)
                {
                    weights[k] /= total;
                }
            }

            table[i] = (start, weights);
        }

        return table;
    }

    private static double Lanczos(double t)
    {
        t = Math.Abs(t);
        if (t < 1e-9)
        {
            return 1.0;
        }

        if (t >= 3.0)
        {
            return 0.0;
        }

        var pt = Math.PI * t;
        return 3.0 * Math.Sin(pt) * Math.Sin(pt / 3.0) / (pt * pt);
    }
}
