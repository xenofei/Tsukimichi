using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>The Medallion night grade's settings (<see cref="MoonfallGrade.NightLab"/>); the defaults are the design's.</summary>
public sealed record MoonfallNightGrade
{
    public float Exposure { get; init; } = 0.80f;

    public float Gamma { get; init; } = 1.7f;

    /// <summary>The OKLab lightness the scene is held under (0.39 is about luma 0.42 for a moonstone grey).</summary>
    public float Ceiling { get; init; } = 0.39f;

    public float Knee { get; init; } = 0.23f;

    public float Detail { get; init; } = 1.15f;

    public float ChromaMid { get; init; } = 0.55f;

    public float ChromaHigh { get; init; } = 0.75f;

    public string Tint { get; init; } = "#C3CEE4";

    public float TintK { get; init; } = 0.55f;

    public string BaseHue { get; init; } = "#22356E";

    /// <summary>How much further the sky (a mask from its blue cast, high in the frame) is taken down; 0 for none.</summary>
    public float SkyDrop { get; init; }

    /// <summary>The sky mask's vertical fade: full above <see cref="SkyTop"/>, none below <see cref="SkyBottom"/> (fractions of the height).</summary>
    public float SkyTop { get; init; } = 0.35f;

    /// <inheritdoc cref="SkyTop"/>
    public float SkyBottom { get; init; } = 0.75f;

    public float WarmKeep { get; init; } = 0.35f;

    /// <summary>The form light on small pale shapes (0 for none).</summary>
    public float Form { get; init; } = 0.10f;

    public float FormRadius { get; init; } = 20f;

    /// <summary>How much of the 2–12 unit band the roll-off took out is restored.</summary>
    public float BandK { get; init; } = 1.0f;

    /// <summary>The Far Shore's later hour: the same grade pulled toward violet-moonstone (rich_lib.VIOLET).</summary>
    public static MoonfallNightGrade Violet { get; } = new() { Tint = "#CBC4EA", BaseHue = "#2A2358" };
}

/// <summary>One region a second jewel is pushed into (<see cref="MoonfallJewelGrade"/>).</summary>
/// <param name="Mask">Where, 0..1 per pixel.</param>
/// <param name="Hue">The jewel's colour (only its OKLab hue is used).</param>
/// <param name="Chroma">The chroma it is pushed to.</param>
public sealed record MoonfallJewelRegion(MoonfallPlane Mask, Vector3 Hue, float Chroma);

/// <summary>The level's palette (the design's <c>dress2.jewel</c>): lightness kept, hue and chroma from bands down the board.</summary>
public sealed record MoonfallJewelGrade
{
    /// <summary>(board y, colour): the jewel's hue down the board, interpolated.</summary>
    public required IReadOnlyList<(float Y, Vector3 Colour)> Bands { get; init; }

    /// <summary>(OKLab L, colour): hues by value, mixed in at <see cref="Mix"/>; empty for none.</summary>
    public IReadOnlyList<(float L, Vector3 Colour)> ValueHues { get; init; } = [];

    public float Mix { get; init; } = 0.5f;

    public float Chroma { get; init; } = 1f;

    public float KeepHigh { get; init; } = 0.75f;

    /// <summary>How much of the source's own a, b is kept.</summary>
    public float Keep { get; init; } = 0.30f;

    public float Floor { get; init; } = 0.024f;

    public IReadOnlyList<MoonfallJewelRegion> Regions { get; init; } = [];

    /// <summary>Where the grade applies (null: everywhere).</summary>
    public MoonfallPlane? Mask { get; init; }
}

/// <summary>
/// The grades of rich pass 2, ported from the design's Python so the runtime matches the approved renders (the parity
/// test runs both on one fixture): the Medallion night grade for the official paintings (<see cref="NightLab"/>,
/// rich_lib.night_lab), the level's jewel palette (<see cref="Jewel"/>, dress2.jewel, lightness kept exactly), "gild"
/// for the game's UI gold (<see cref="Gild"/>, r2lib.gild) and the light grade for the companions' cards
/// (<see cref="LightGrade"/>, r2cast.light_grade). Every grade is pure, allocates its result and runs off the framework
/// thread. Units: <c>S</c> is pixels per board unit (1 for the 800 × 600 tier, 2 for 1600 × 1200).
/// </summary>
public static class MoonfallGrade
{
    /// <summary>rich_lib.night_lab: day for night in OKLab (see spec-rich.md, "The Medallion night grade, version 2").</summary>
    public static MoonfallImage NightLab(MoonfallImage source, MoonfallNightGrade grade, float s)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(grade);
        int w = source.Width, h = source.Height;
        var n = w * h;
        var L = new MoonfallPlane(w, h);
        var A = new MoonfallPlane(w, h);
        var B = new MoonfallPlane(w, h);
        ToOklab(source, L, A, B);

        MoonfallPlane? sky = null;
        if (grade.SkyDrop > 0)
        {
            var mask = new MoonfallPlane(w, h);
            MoonfallParallel.For(0, h, y =>
            {
                var fade = MoonfallColor.Smooth(grade.SkyBottom, grade.SkyTop, y / (float)h);
                for (var x = 0; x < w; x++)
                {
                    var i = (y * w) + x;
                    mask.Data[i] = MoonfallColor.Smooth(0.02f, 0.15f, source.B.Data[i] - source.R.Data[i]) * fade;
                }
            });
            sky = mask;

            sky = MoonfallFilters.Blur(sky, 3 * s);
        }

        var ln = new MoonfallPlane(w, h);
        MoonfallFilters.Chunks(n, (lo0, hi0) =>
        {
            for (var i = lo0; i < hi0; i++)
            {
                var v = MathF.Pow(Math.Clamp(L.Data[i], 0f, 1f), grade.Gamma) * grade.Exposure;
                if (sky is not null)
                {
                    v *= 1 - (sky.Data[i] * grade.SkyDrop);
                }

                ln.Data[i] = v;
            }
        });

        var baseLayer = MoonfallFilters.Blur(ln, 48 * s);
        var k0 = grade.Knee;
        var span = grade.Ceiling - k0;
        var r0 = grade.Ceiling - 0.02f;
        var pre = new MoonfallPlane(w, h);
        var lo = new MoonfallPlane(w, h);
        MoonfallFilters.Chunks(n, (lo0, hi0) =>
        {
            for (var i = lo0; i < hi0; i++)
            {
                var b = baseLayer.Data[i];
                var det = (ln.Data[i] - b) * grade.Detail;
                var over = MathF.Max(b - k0, 0f);
                var bc = b > k0 ? k0 + (0.35f * over) + (0.65f * span * (1 - MathF.Exp(-over / span))) : b;
                var p = bc + det;
                pre.Data[i] = p;
                lo.Data[i] = p > r0 ? r0 + (0.06f * (1 - MathF.Exp(-(p - r0) / 0.06f))) : p;
            }
        });

        // The 2-12 px band the roll-off took out, restored (zero mean, so the ceiling holds on average).
        var preBlur = MoonfallFilters.Blur(pre, 12 * s);
        var loBlur = MoonfallFilters.Blur(lo, 12 * s);
        MoonfallFilters.Chunks(n, (lo0, hi0) =>
        {
            for (var i = lo0; i < hi0; i++)
            {
                var bandPre = pre.Data[i] - preBlur.Data[i];
                var bandLo = lo.Data[i] - loBlur.Data[i];
                lo.Data[i] += (bandPre - bandLo) * grade.BandK;
            }
        });

        if (grade.Form > 0)
        {
            FormLight(lo, grade, s);
        }

        var tint = MoonfallColor.ToOklab(MoonfallColor.Hex(grade.Tint));
        var hue = MoonfallColor.ToOklab(MoonfallColor.Hex(grade.BaseHue));
        var out_ = new MoonfallImage(w, h);
        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var l = MathF.Max(lo.Data[i], 0f);
                var a = A.Data[i];
                var b = B.Data[i];
                var t = MoonfallColor.Smooth(0.25f, grade.Ceiling, l);
                var warm = MoonfallColor.Smooth(0f, 0.04f, b) * MoonfallColor.Smooth(-0.02f, 0.03f, a + (b * 0.3f));
                var keep = (grade.ChromaMid + ((grade.ChromaHigh - grade.ChromaMid) * t)) * (1 - (warm * (1 - grade.WarmKeep)));
                if (sky is not null)
                {
                    keep *= 1 - (0.5f * sky.Data[i]);
                }

                var deg = MathF.Atan2(b, a) * (180f / MathF.PI);
                if (deg < 0)
                {
                    deg += 360f;
                }

                var yg = MoonfallColor.Smooth(45f, 70f, deg) * MoonfallColor.Smooth(195f, 170f, deg);
                keep *= 1 - (0.7f * yg);
                var pull = Math.Clamp(((1 - t) * 0.45f) + (t * grade.TintK) + (yg * 0.3f), 0f, 1f);
                var ta = (hue.Y * (1 - t)) + (tint.Y * t);
                var tb = (hue.Z * (1 - t)) + (tint.Z * t);
                var a2 = (a * keep * (1 - pull)) + (ta * pull);
                var b2 = (b * keep * (1 - pull)) + (tb * pull);
                var rgb = MoonfallColor.ToSrgb(l, a2, b2);
                out_.R.Data[i] = rgb.X;
                out_.G.Data[i] = rgb.Y;
                out_.B.Data[i] = rgb.Z;
            }
        });
        return out_;
    }

    /// <summary>The night grade's form light: pale shapes under about 40 units given relief from the upper left.</summary>
    private static void FormLight(MoonfallPlane lo, MoonfallNightGrade grade, float s)
    {
        int w = lo.Width, h = lo.Height;
        var n = w * h;
        var hi = MoonfallFilters.Percentile(lo, 92);
        var lb = MoonfallFilters.Blur(lo, 2 * s);
        // Each step works pixel by pixel, so each runs in chunks in parallel (the same result).
        var shape = new MoonfallPlane(w, h);
        MoonfallFilters.Chunks(n, (a, b) =>
        {
            for (var i = a; i < b; i++)
            {
                shape.Data[i] = MoonfallColor.Smooth(hi - 0.06f, hi - 0.01f, lb.Data[i]);
            }
        });

        var large = MoonfallFilters.Blur(shape, 40 * s);
        MoonfallFilters.Chunks(n, (a, b) =>
        {
            for (var i = a; i < b; i++)
            {
                shape.Data[i] *= 1 - MoonfallColor.Smooth(0.70f, 0.92f, large.Data[i]);
            }
        });

        var height = MoonfallFilters.Blur(shape, grade.FormRadius * s);
        MoonfallFilters.Chunks(n, (a, b) =>
        {
            for (var i = a; i < b; i++)
            {
                height.Data[i] = MathF.Sqrt(Math.Clamp(height.Data[i], 0f, 1f));
            }
        });

        var (gy, gx) = MoonfallFilters.Gradient(height);
        MoonfallFilters.Chunks(n, (a, b) =>
        {
            for (var i = a; i < b; i++)
            {
                var lam = Math.Clamp((gx.Data[i] + gy.Data[i]) * 0.7071f * s * grade.FormRadius * 2.5f, -1f, 1f);
                lo.Data[i] += (lam > 0 ? lam * 0.25f : lam) * grade.Form * shape.Data[i];
            }
        });
    }

    /// <summary>
    /// dress2.jewel: bolder colour with OKLab lightness kept exactly (F1). Hue from <see cref="MoonfallJewelGrade.Bands"/>
    /// down the board (board y = (row + 0.5) / <paramref name="s"/>), chroma raised, highlights kept cool, a second
    /// jewel pushed into each region.
    /// </summary>
    public static MoonfallImage Jewel(MoonfallImage source, MoonfallJewelGrade grade, float s)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(grade);
        int w = source.Width, h = source.Height;
        var bandY = grade.Bands.Select(static b => b.Y).ToArray();
        var bandDirs = grade.Bands.Select(static b => MoonfallColor.HueDirection(b.Colour)).ToArray();
        var bandA = bandDirs.Select(static d => d.X).ToArray();
        var bandB = bandDirs.Select(static d => d.Y).ToArray();
        var valueL = grade.ValueHues.Select(static v => v.L).ToArray();
        var valueDirs = grade.ValueHues.Select(static v => MoonfallColor.HueDirection(v.Colour)).ToArray();
        var valueA = valueDirs.Select(static d => d.X).ToArray();
        var valueB = valueDirs.Select(static d => d.Y).ToArray();
        var regions = grade.Regions.Select(static r => (r.Mask, Dir: MoonfallColor.HueDirection(r.Hue), r.Chroma)).ToArray();
        var out_ = new MoonfallImage(w, h, source.A is not null);
        if (source.A is { } alpha)
        {
            Array.Copy(alpha.Data, out_.A!.Data, alpha.Data.Length);
        }

        MoonfallParallel.For(0, h, y =>
        {
            var by = (y + 0.5f) / s;
            var ta0 = MoonfallColor.Interp(by, bandY, bandA);
            var tb0 = MoonfallColor.Interp(by, bandY, bandB);
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var lab = MoonfallColor.ToOklab(source.R.Data[i], source.G.Data[i], source.B.Data[i]);
                var l = lab.X;
                float ta = ta0, tb = tb0;
                if (valueL.Length > 0)
                {
                    var va = MoonfallColor.Interp(l, valueL, valueA);
                    var vb = MoonfallColor.Interp(l, valueL, valueB);
                    ta = (ta * (1 - grade.Mix)) + (va * grade.Mix);
                    tb = (tb * (1 - grade.Mix)) + (vb * grade.Mix);
                }

                var nrm = MathF.Sqrt((ta * ta) + (tb * tb)) + 1e-6f;
                ta /= nrm;
                tb /= nrm;
                var cc = MathF.Sqrt((lab.Y * lab.Y) + (lab.Z * lab.Z));
                var hi = MoonfallColor.Smooth(0.60f, 0.85f, l);
                var cn = (grade.Floor + (cc * 1.4f)) * grade.Chroma * (1 - (grade.KeepHigh * hi)) * MoonfallColor.Smooth(0.02f, 0.12f, l);
                var a2 = (lab.Y * grade.Keep) + (ta * cn * (1 - grade.Keep));
                var b2 = (lab.Z * grade.Keep) + (tb * cn * (1 - grade.Keep));
                foreach (var (mask, dir, chroma) in regions)
                {
                    var rm = mask.Data[i];
                    if (rm <= 0)
                    {
                        continue;
                    }

                    var c2 = chroma * MoonfallColor.Smooth(0.04f, 0.14f, l) * (1 - (0.6f * hi));
                    a2 = (a2 * (1 - rm)) + (dir.X * c2 * rm);
                    b2 = (b2 * (1 - rm)) + (dir.Y * c2 * rm);
                }

                if (grade.Mask is { } m)
                {
                    var k = m.Data[i];
                    a2 = (lab.Y * (1 - k)) + (a2 * k);
                    b2 = (lab.Z * (1 - k)) + (b2 * k);
                }

                var rgb = MoonfallColor.ToSrgbKeepingLightness(l, a2, b2);
                out_.R.Data[i] = rgb.X;
                out_.G.Data[i] = rgb.Y;
                out_.B.Data[i] = rgb.Z;
            }
        });
        return out_;
    }

    /// <summary>
    /// r2lib.gild: the game's gold, its lightness kept (lifted by <paramref name="lift"/>), its hue pulled toward the
    /// Medallion's gilt (OKLab 78°, amber with <paramref name="warm"/>) where it is golden, its chroma raised. Alpha kept.
    /// </summary>
    public static MoonfallImage Gild(MoonfallImage source, float k = 0.55f, float warm = 0f, float lift = 0f)
    {
        ArgumentNullException.ThrowIfNull(source);
        int w = source.Width, h = source.Height;
        var target = (78f - (18f * warm)) * (MathF.PI / 180f);
        var out_ = new MoonfallImage(w, h, source.A is not null);
        if (source.A is { } alpha)
        {
            Array.Copy(alpha.Data, out_.A!.Data, alpha.Data.Length);
        }

        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var lab = MoonfallColor.ToOklab(source.R.Data[i], source.G.Data[i], source.B.Data[i]);
                var c = MathF.Sqrt((lab.Y * lab.Y) + (lab.Z * lab.Z));
                var hue = MathF.Atan2(lab.Z, lab.Y);
                var weight = Math.Clamp(c / 0.05f, 0f, 1f) * k;
                var dh = target - hue;
                dh = MathF.Atan2(MathF.Sin(dh), MathF.Cos(dh));
                var hue2 = hue + (dh * weight);
                var c2 = c * (1f + (0.45f * k));
                var l2 = Math.Clamp(lab.X * (1 + lift), 0f, 1f);
                var rgb = MoonfallColor.ToSrgb(l2, c2 * MathF.Cos(hue2), c2 * MathF.Sin(hue2));
                out_.R.Data[i] = rgb.X;
                out_.G.Data[i] = rgb.Y;
                out_.B.Data[i] = rgb.Z;
            }
        });
        return out_;
    }

    /// <summary>r2cast.light_grade: a companion's card, its shadows cooled a little toward the night's blue; highlights and skin kept.</summary>
    public static MoonfallImage LightGrade(MoonfallImage source, float k = 0.25f, string tint = "#AFC4FF")
    {
        ArgumentNullException.ThrowIfNull(source);
        int w = source.Width, h = source.Height;
        var tl = MoonfallColor.ToOklab(MoonfallColor.Hex(tint));
        var out_ = new MoonfallImage(w, h, source.A is not null);
        if (source.A is { } alpha)
        {
            Array.Copy(alpha.Data, out_.A!.Data, alpha.Data.Length);
        }

        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var lab = MoonfallColor.ToOklab(source.R.Data[i], source.G.Data[i], source.B.Data[i]);
                var t = Math.Clamp(1 - (lab.X / 0.6f), 0f, 1f) * k;
                var a = (lab.Y * (1 - (0.5f * t))) + (tl.Y * 0.5f * t);
                var b = (lab.Z * (1 - (0.5f * t))) + (tl.Z * 0.5f * t);
                var rgb = MoonfallColor.ToSrgb(lab.X, a, b);
                out_.R.Data[i] = rgb.X;
                out_.G.Data[i] = rgb.Y;
                out_.B.Data[i] = rgb.Z;
            }
        });
        return out_;
    }

    /// <summary>The image's OKLab planes.</summary>
    public static void ToOklab(MoonfallImage source, MoonfallPlane l, MoonfallPlane a, MoonfallPlane b)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(l);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var w = source.Width;
        MoonfallParallel.For(0, source.Height, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var lab = MoonfallColor.ToOklab(source.R.Data[i], source.G.Data[i], source.B.Data[i]);
                l.Data[i] = lab.X;
                a.Data[i] = lab.Y;
                b.Data[i] = lab.Z;
            }
        });
    }

    /// <summary>The OKLab lightness of every pixel.</summary>
    public static MoonfallPlane Lightness(MoonfallImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var l = new MoonfallPlane(source.Width, source.Height);
        var w = source.Width;
        MoonfallParallel.For(0, source.Height, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                l.Data[i] = MoonfallColor.ToOklab(source.R.Data[i], source.G.Data[i], source.B.Data[i]).X;
            }
        });
        return l;
    }

    /// <summary>Screens <paramref name="colour"/> × <paramref name="amount"/> (per pixel) onto the image, in place.</summary>
    public static void ScreenIn(MoonfallImage image, Vector3 colour, MoonfallPlane amount)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(amount);
        var n = image.Width * image.Height;
        for (var i = 0; i < n; i++)
        {
            var k = amount.Data[i];
            if (k <= 0)
            {
                continue;
            }

            image.R.Data[i] = MoonfallColor.Screen(image.R.Data[i], colour.X * k);
            image.G.Data[i] = MoonfallColor.Screen(image.G.Data[i], colour.Y * k);
            image.B.Data[i] = MoonfallColor.Screen(image.B.Data[i], colour.Z * k);
        }
    }

    /// <summary>rich_lib.moon_glow: the moon's light in the air from (mx, my) device px (it may sit beyond the frame).</summary>
    public static void MoonGlow(MoonfallImage image, float mx, float my, float rCore, float rWide, float kCore, float kWide, Vector3 colour)
    {
        ArgumentNullException.ThrowIfNull(image);
        var amount = new MoonfallPlane(image.Width, image.Height);
        var w = image.Width;
        MoonfallParallel.For(0, image.Height, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var d = MathF.Sqrt(((x - mx) * (x - mx)) + ((y - my) * (y - my)));
                amount.Data[(y * w) + x] = (MathF.Exp(-(d / rCore) * (d / rCore)) * kCore) + (MathF.Exp(-(d / rWide) * (d / rWide)) * kWide);
            }
        });
        ScreenIn(image, colour, amount);
    }

    /// <summary>rich_lib.vignette, in place: the frame's edges darkened by up to <paramref name="k"/>.</summary>
    public static void Vignette(MoonfallImage image, float k, float cx = 0.5f, float cy = 0.45f)
    {
        ArgumentNullException.ThrowIfNull(image);
        int w = image.Width, h = image.Height;
        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var u = ((x / (float)w) - cx) / 0.75f;
                var v = ((y / (float)h) - cy) / 0.75f;
                var f = 1 - (k * MoonfallColor.Smooth(0.35f, 1f, MathF.Sqrt((u * u) + (v * v))));
                var i = (y * w) + x;
                image.R.Data[i] *= f;
                image.G.Data[i] *= f;
                image.B.Data[i] *= f;
            }
        });
    }

    /// <summary>rich_lib.grain, in place, from a hashed field (the same for the same seed).</summary>
    public static void Grain(MoonfallImage image, float amount, int seed)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (amount <= 0)
        {
            return;
        }

        int w = image.Width, h = image.Height;
        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                // A normal-ish deviate from four hashed uniforms (mean 0, variance 1).
                var g = (MoonfallNoise.Lattice(x, y, seed) + MoonfallNoise.Lattice(x, y, seed + 1)
                    + MoonfallNoise.Lattice(x, y, seed + 2) + MoonfallNoise.Lattice(x, y, seed + 3) - 2f) * 1.7320508f;
                var i = (y * w) + x;
                image.R.Data[i] = Math.Clamp(image.R.Data[i] + (g * amount), 0f, 1f);
                image.G.Data[i] = Math.Clamp(image.G.Data[i] + (g * amount), 0f, 1f);
                image.B.Data[i] = Math.Clamp(image.B.Data[i] + (g * amount), 0f, 1f);
            }
        });
    }
}
