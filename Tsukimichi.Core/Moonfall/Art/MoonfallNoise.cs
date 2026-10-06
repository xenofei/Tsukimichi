namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The design kit's value noise and fbm (artlib <c>value_noise</c> and <c>fbm</c>: random lattice values, smoothstep
/// between them, octaves halving the cell), with a hashed lattice in place of numpy's generator, so the same seed gives
/// the same noise on every machine and every run. <see cref="FbmPeriodic"/> wraps across its width, so a layer scrolled
/// by its own width joins without a seam (the title mist's loop seam, spec-rich2.md §8).
/// </summary>
public static class MoonfallNoise
{
    /// <summary>A lattice value in [0, 1) for (x, y) under <paramref name="seed"/>.</summary>
    public static float Lattice(int x, int y, int seed)
    {
        var h = (uint)x * 0x8DA6B343u;
        h ^= (uint)y * 0xD8163841u;
        h ^= (uint)seed * 0xCB1AB31Fu;
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        h *= 0x297A2D39u;
        h ^= h >> 15;
        return (h >> 8) / (float)(1 << 24);
    }

    /// <summary>A small deterministic generator (SplitMix64) for placements: leaves, rocks, fireflies.</summary>
    public struct Random
    {
        private ulong state;

        public Random(int seed) => state = (ulong)(uint)seed * 0x9E3779B97F4A7C15ul + 0x632BE59BD9B4E019ul;

        /// <summary>A value in [0, 1).</summary>
        public float Next()
        {
            state += 0x9E3779B97F4A7C15ul;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ul;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBul;
            z ^= z >> 31;
            return (z >> 40) / (float)(1 << 24);
        }

        /// <summary>A value in [lo, hi).</summary>
        public float Range(float lo, float hi) => lo + ((hi - lo) * Next());

        /// <summary>A whole number in [lo, hi).</summary>
        public int Int(int lo, int hi) => Math.Min(hi - 1, lo + (int)((hi - lo) * Next()));

        /// <summary>A normal deviate (Box–Muller).</summary>
        public float Normal(float mean, float sd)
        {
            var u = MathF.Max(Next(), 1e-7f);
            var v = Next();
            return mean + (sd * MathF.Sqrt(-2f * MathF.Log(u)) * MathF.Cos(2f * MathF.PI * v));
        }
    }

    /// <summary>
    /// artlib's <c>value_noise</c>: one octave over <paramref name="width"/> × <paramref name="height"/> pixels whose
    /// top-left pixel is (<paramref name="ox"/>, <paramref name="oy"/>) of the whole field (so a window of a picture
    /// gets the same noise as the picture would).
    /// </summary>
    public static MoonfallPlane Value(int width, int height, float cell, int seed, int periodX = 0, int ox = 0, int oy = 0)
    {
        var plane = new MoonfallPlane(width, height);
        cell = MathF.Max(1f, cell);
        MoonfallParallel.For(0, height, y =>
        {
            var fy = (y + oy) / cell;
            var y0 = (int)MathF.Floor(fy);
            var ty = fy - y0;
            ty = ty * ty * (3 - (2 * ty));
            for (var x = 0; x < width; x++)
            {
                var fx = (x + ox) / cell;
                var x0 = (int)MathF.Floor(fx);
                var tx = fx - x0;
                tx = tx * tx * (3 - (2 * tx));
                var x1 = x0 + 1;
                if (periodX > 0)
                {
                    x0 %= periodX;
                    x1 %= periodX;
                }

                var a = Lattice(x0, y0, seed);
                var b = Lattice(x1, y0, seed);
                var c = Lattice(x0, y0 + 1, seed);
                var d = Lattice(x1, y0 + 1, seed);
                var top = a + ((b - a) * tx);
                var bot = c + ((d - c) * tx);
                plane.Data[(y * width) + x] = top + ((bot - top) * ty);
            }
        });
        return plane;
    }

    /// <summary>artlib's <c>fbm</c>: <paramref name="octaves"/> octaves, each half the cell and <paramref name="gain"/> the weight, normalised to 0..1.</summary>
    public static MoonfallPlane Fbm(int width, int height, float cell, int octaves, int seed, float gain = 0.5f) =>
        Octaves(width, height, cell, octaves, seed, gain, periodic: false, 0, 0);

    /// <summary><see cref="Fbm"/> over a window of the whole field whose top-left pixel is (<paramref name="ox"/>, <paramref name="oy"/>).</summary>
    public static MoonfallPlane FbmAt(int ox, int oy, int width, int height, float cell, int octaves, int seed, float gain = 0.5f) =>
        Octaves(width, height, cell, octaves, seed, gain, periodic: false, ox, oy);

    /// <summary>
    /// <see cref="Fbm"/> for a field whose finest cell is large: computed on a grid a few pixels apart and interpolated, the
    /// same smooth field at a fraction of the cost (shaft noise, nebulae).
    /// </summary>
    public static MoonfallPlane FbmSmooth(int width, int height, float cell, int octaves, int seed, float gain = 0.5f)
    {
        var finest = cell / (1 << Math.Max(0, octaves - 1));
        var step = Math.Max(1, (int)(finest / 4));
        if (step == 1)
        {
            return Fbm(width, height, cell, octaves, seed, gain);
        }

        var cw = (width / step) + 2;
        var ch = (height / step) + 2;
        var coarse = Fbm(cw, ch, cell / step, octaves, seed, gain);
        var out_ = new MoonfallPlane(width, height);
        MoonfallParallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                out_.Data[(y * width) + x] = coarse.Sample((x / (float)step) + 0.5f, (y / (float)step) + 0.5f);
            }
        });
        return out_;
    }

    /// <summary>
    /// <see cref="Fbm"/> that repeats every <paramref name="width"/> pixels across: each octave's lattice is a whole
    /// number of cells round the width, so column 0 continues column <c>width - 1</c> (a tile that scrolls without a seam).
    /// </summary>
    public static MoonfallPlane FbmPeriodic(int width, int height, float cell, int octaves, int seed, float gain = 0.5f) =>
        Octaves(width, height, cell, octaves, seed, gain, periodic: true, 0, 0);

    private static MoonfallPlane Octaves(int width, int height, float cell, int octaves, int seed, float gain, bool periodic, int ox, int oy)
    {
        var out_ = new MoonfallPlane(width, height);
        var amp = 1f;
        var total = 0f;
        for (var o = 0; o < Math.Max(1, octaves); o++)
        {
            var c = MathF.Max(1f, cell / (1 << o));
            var period = 0;
            if (periodic)
            {
                period = Math.Max(1, (int)MathF.Round(width / c));
                c = width / (float)period;
            }

            var layer = Value(width, height, c, seed + (101 * o), period, ox, oy);
            for (var i = 0; i < out_.Data.Length; i++)
            {
                out_.Data[i] += layer.Data[i] * amp;
            }

            total += amp;
            amp *= gain;
        }

        for (var i = 0; i < out_.Data.Length; i++)
        {
            out_.Data[i] /= total;
        }

        return out_;
    }
}
