using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Full's banner night grade (docs/design/flair-v13/spec.md §1.2, supervisor fix 1): every quest banner in the fallback
/// chain is graded as it is drawn, so a daylight zone sits in the moonlit window. In order: a multiply by
/// <see cref="IndigoHex"/> at a strength chosen per texture (<see cref="Strength(Vector3, float)"/>, 0 to 0.75), a
/// desaturation that grows with that strength (<see cref="DesaturationAt"/>, 35 % at 0.70), a scrim to Night (0 at the
/// top, 0.25 at 45 %, 0.85 at the bottom edge) and a faint MoonHigh wash from the upper left; a faint moon road of six
/// dashes lies along the bottom-right edge. The art itself is never shipped graded. Targets, on the graded art before the
/// scrim: its mean L (0–255 greyscale) at most <see cref="TargetMeanL"/>, and its 99.5th-percentile L at most
/// <see cref="TargetPeakL"/>, under the hero medal's moon. The strength is the least that meets both, so art that is
/// already a night scene (the plugin's own banners) is only lightly touched and keeps its warm accents, while daylight
/// art is pulled down into the moonlit window (implementation review, fix 1). Pure, so the parameters are tested.
/// </summary>
public static class BannerGrade
{
    /// <summary>The multiply colour: the Moon Road's indigo.</summary>
    public const uint IndigoHex = 0x2A3768;

    /// <summary>The weakest multiply, none: a banner that already meets both targets is left as painted.</summary>
    public const float MinStrength = 0f;

    /// <summary>The strongest multiply, for the brightest daylight art.</summary>
    public const float MaxStrength = 0.75f;

    /// <summary>The strength used before a texture has been measured (the mock's, which met the target on the Dawntrail banner).</summary>
    public const float DefaultStrength = 0.70f;

    /// <summary>How far each pixel moves toward its own grey after the multiply, at <see cref="DefaultStrength"/>; it scales with the strength (<see cref="DesaturationAt"/>).</summary>
    public const float Desaturation = 0.35f;

    /// <summary>The graded art's mean lightness may be at most this (0–255 greyscale).</summary>
    public const float TargetMeanL = 55f;

    /// <summary>The graded art's bright end (<see cref="PeakFraction"/> of its pixels at or under it) stays at most this, below the hero moon's ≈ 230.</summary>
    public const float TargetPeakL = 170f;

    /// <summary>
    /// The percentile that stands for a banner's peak: 99.5 %, so a painted moon or a lantern of a few pixels is not what
    /// decides the grade.
    /// </summary>
    public const float PeakFraction = 0.995f;

    /// <summary>How many bins a luma histogram has (<see cref="MeanRgb"/>, <see cref="LumaPercentile"/>): one per level.</summary>
    public const int HistogramBins = 256;

    /// <summary>The MoonHigh wash's alpha at the upper-left corner, fading out across the banner.</summary>
    public const float WashAlpha = 0.06f;

    /// <summary>The scrim to Night, as (fraction of the height, alpha) stops from the top.</summary>
    public static readonly (float At, float Alpha)[] ScrimStops = [(0f, 0f), (0.45f, 0.25f), (1f, 0.85f)];

    /// <summary>
    /// The moon road on the water along the banner's bottom edge (spec §1, "Banners"): six MoonHigh dashes, each as
    /// (its top from the bottom edge as a fraction of <see cref="RoadHeightFraction"/>, its width as a fraction of the
    /// banner's width, its alpha, its sideways nudge as a fraction of the width), widening toward the viewer.
    /// </summary>
    public static readonly (float Y, float Width, float Alpha, float Nudge)[] RoadDashes =
    [
        (4f / 34f, 4.5f / 380f, 0.07f, -2f / 380f),
        (9f / 34f, 7.5f / 380f, 0.09f, 2f / 380f),
        (14f / 34f, 12f / 380f, 0.11f, -2f / 380f),
        (19f / 34f, 18f / 380f, 0.13f, 2f / 380f),
        (24f / 34f, 25.5f / 380f, 0.15f, -2f / 380f),
        (29f / 34f, 34.5f / 380f, 0.17f, 2f / 380f),
    ];

    /// <summary>The road's strip: the bottom 22 % of the banner (34 of 156 px).</summary>
    public const float RoadHeightFraction = 34f / 156f;

    /// <summary>The road's centre across the banner: under the lit side, toward the right edge (356 of 380 px).</summary>
    public const float RoadCenterFraction = 356f / 380f;

    /// <summary>The multiply colour as a vector.</summary>
    public static Vector4 Indigo => ColorMath.FromHex(IndigoHex);

    /// <summary>The Night the scrim falls to.</summary>
    public static Vector4 Night => GlyphTokens.Night;

    /// <summary>Greyscale lightness, 0–255, of sRGB channels 0..1 (Rec. 601 luma, as the design's measurements are taken).</summary>
    public static float Luma(Vector3 rgb) => 255f * ((0.299f * rgb.X) + (0.587f * rgb.Y) + (0.114f * rgb.Z));

    /// <summary>
    /// How much a grey's lightness falls per unit of strength: 1 less the luma of <see cref="IndigoHex"/> (≈ 0.777), so a
    /// grey of lightness L comes out of the multiply at L × (1 − <see cref="LumaDrop"/> × s).
    /// </summary>
    public static float LumaDrop => 1f - (Luma(new Vector3(Indigo.X, Indigo.Y, Indigo.Z)) / 255f);

    /// <summary>The tint that multiplies a banner by <see cref="IndigoHex"/> at <paramref name="strength"/>: white mixed toward indigo.</summary>
    public static Vector4 Tint(float strength)
    {
        var s = ClampStrength(strength);
        var indigo = Indigo;
        return new Vector4(1f - s + (s * indigo.X), 1f - s + (s * indigo.Y), 1f - s + (s * indigo.Z), 1f);
    }

    /// <summary>
    /// The desaturation at <paramref name="strength"/>: <see cref="Desaturation"/> scaled by the strength over
    /// <see cref="DefaultStrength"/> (35 % at 0.70, none at 0), so art left nearly as painted keeps its colour.
    /// </summary>
    public static float DesaturationAt(float strength) => Desaturation * ClampStrength(strength) / DefaultStrength;

    /// <summary>A strength within <see cref="MinStrength"/>..<see cref="MaxStrength"/>; a value that is not a number reads as <see cref="DefaultStrength"/>.</summary>
    public static float ClampStrength(float strength) =>
        float.IsFinite(strength) ? Math.Clamp(strength, MinStrength, MaxStrength) : DefaultStrength;

    /// <summary>The scrim's mean alpha down the whole banner (the area under <see cref="ScrimStops"/>).</summary>
    public static float ScrimMeanAlpha
    {
        get
        {
            var area = 0f;
            for (var i = 1; i < ScrimStops.Length; i++)
            {
                var (a0, v0) = ScrimStops[i - 1];
                var (a1, v1) = ScrimStops[i];
                area += (a1 - a0) * (v0 + v1) * 0.5f;
            }

            return area;
        }
    }

    /// <summary>The scrim's alpha at <paramref name="t"/> (0 at the top, 1 at the bottom edge).</summary>
    public static float ScrimAlpha(float t)
    {
        t = float.IsFinite(t) ? Math.Clamp(t, 0f, 1f) : 0f;
        for (var i = 1; i < ScrimStops.Length; i++)
        {
            var (a0, v0) = ScrimStops[i - 1];
            var (a1, v1) = ScrimStops[i];
            if (t <= a1)
            {
                return v0 + ((v1 - v0) * (t - a0) / MathF.Max(1e-6f, a1 - a0));
            }
        }

        return ScrimStops[^1].Alpha;
    }

    /// <summary>
    /// The mean lightness of a banner whose mean colour is <paramref name="meanRgb"/> (sRGB 0..1) after the colour passes
    /// at <paramref name="strength"/>, before the scrim: the multiply (the desaturation keeps each pixel's grey). This is
    /// what <see cref="TargetMeanL"/> bounds.
    /// </summary>
    public static float GradedArtMeanL(Vector3 meanRgb, float strength)
    {
        var tint = Tint(strength);
        return Luma(new Vector3(meanRgb.X * tint.X, meanRgb.Y * tint.Y, meanRgb.Z * tint.Z));
    }

    /// <summary>
    /// The graded mean lightness of the whole banner as drawn: <see cref="GradedArtMeanL"/>, then the scrim's mean toward
    /// Night. The wash is left out: at 0.06 and fading it adds under a level.
    /// </summary>
    public static float GradedMeanL(Vector3 meanRgb, float strength)
    {
        var night = Luma(new Vector3(Night.X, Night.Y, Night.Z));
        var a = ScrimMeanAlpha;
        return (GradedArtMeanL(meanRgb, strength) * (1f - a)) + (night * a);
    }

    /// <summary>A banner's peak (its <see cref="PeakFraction"/> percentile lightness, 0–255) after the multiply at <paramref name="strength"/>, taken as grey.</summary>
    public static float GradedPeakL(float peakL, float strength) => peakL * (1f - (LumaDrop * ClampStrength(strength)));

    /// <summary>
    /// The multiply strength for a banner whose mean colour is <paramref name="meanRgb"/> and whose
    /// <see cref="PeakFraction"/> percentile lightness is <paramref name="peakL"/> (0–255): the least strength that brings
    /// both its graded mean to <see cref="TargetMeanL"/> or under and its graded peak to <see cref="TargetPeakL"/> or
    /// under, never past <see cref="MaxStrength"/> (the brightest art then sits a little over a target rather than
    /// turning to ink). Art that meets both as painted takes none. A peak that is not a number is left out (the mean
    /// alone decides); a mean that is not a number reads as unmeasured: <see cref="DefaultStrength"/>.
    /// </summary>
    public static float Strength(Vector3 meanRgb, float peakL)
    {
        if (!float.IsFinite(meanRgb.X) || !float.IsFinite(meanRgb.Y) || !float.IsFinite(meanRgb.Z))
        {
            return DefaultStrength;
        }

        var rgb = Vector3.Clamp(meanRgb, Vector3.Zero, Vector3.One);

        // The graded mean falls linearly with the strength: solve for the target.
        var mean = 0f;
        var weak = GradedArtMeanL(rgb, 0f);
        if (weak > TargetMeanL)
        {
            var fall = weak - GradedArtMeanL(rgb, MaxStrength);
            mean = fall > 1e-4f ? MaxStrength * (weak - TargetMeanL) / fall : MaxStrength;
        }

        // The peak falls by 1 − LumaDrop × s, so it asks for s ≥ (1 − TargetPeakL / peak) / LumaDrop.
        var peak = 0f;
        if (float.IsFinite(peakL) && peakL > TargetPeakL)
        {
            peak = (1f - (TargetPeakL / MathF.Min(peakL, 255f))) / LumaDrop;
        }

        return Math.Clamp(MathF.Max(mean, peak), MinStrength, MaxStrength);
    }

    /// <summary><see cref="Strength(Vector3, float)"/> with the peak unknown: the mean target alone.</summary>
    public static float Strength(Vector3 meanRgb) => Strength(meanRgb, float.NaN);

    /// <summary><see cref="Strength(Vector3, float)"/> for a grey banner of mean lightness <paramref name="meanL"/> and peak <paramref name="peakL"/> (0–255).</summary>
    public static float Strength(float meanL, float peakL = float.NaN) =>
        float.IsFinite(meanL) ? Strength(new Vector3(Math.Clamp(meanL, 0f, 255f) / 255f), peakL) : DefaultStrength;

    /// <summary>One pixel through the colour passes, in order: the multiply at <paramref name="strength"/>, then the desaturation (<see cref="DesaturationAt"/>).</summary>
    public static Vector3 Grade(Vector3 rgb, float strength)
    {
        var tint = Tint(strength);
        var m = new Vector3(rgb.X * tint.X, rgb.Y * tint.Y, rgb.Z * tint.Z);
        var grey = Luma(m) / 255f;
        return Vector3.Lerp(m, new Vector3(grey), DesaturationAt(strength));
    }

    /// <summary>
    /// The mean colour of a 32-bit image (sRGB 0..1), every <paramref name="step"/>-th pixel each way: B8G8R8A8 when
    /// <paramref name="bgra"/>, else R8G8B8A8. Fully transparent pixels are skipped. An empty image is not a number.
    /// In the same pass, when <paramref name="lumaHistogram"/> has <see cref="HistogramBins"/> bins, each pixel counted
    /// adds one to the bin of its lightness (for <see cref="LumaPercentile"/>); the caller clears it first.
    /// </summary>
    public static Vector3 MeanRgb(ReadOnlySpan<byte> pixels, int width, int height, int pitch, bool bgra, int step = 1, Span<int> lumaHistogram = default)
    {
        step = Math.Max(1, step);
        var histogram = lumaHistogram.Length >= HistogramBins;
        double r = 0, g = 0, b = 0;
        long n = 0;
        for (var y = 0; y < height; y += step)
        {
            var row = y * pitch;
            for (var x = 0; x < width; x += step)
            {
                var o = row + (x * 4);
                if (o + 3 >= pixels.Length || pixels[o + 3] == 0)
                {
                    continue;
                }

                var (ri, bi) = bgra ? (o + 2, o) : (o, o + 2);
                r += pixels[ri];
                g += pixels[o + 1];
                b += pixels[bi];
                n++;
                if (histogram)
                {
                    // Rec. 601 in thousandths, rounded to the nearest level.
                    var luma = (299 * pixels[ri]) + (587 * pixels[o + 1]) + (114 * pixels[bi]);
                    lumaHistogram[Math.Min(HistogramBins - 1, (luma + 500) / 1000)]++;
                }
            }
        }

        return n == 0 ? new Vector3(float.NaN) : new Vector3((float)(r / n / 255.0), (float)(g / n / 255.0), (float)(b / n / 255.0));
    }

    /// <summary>
    /// The lightness (0–255) at or under which <paramref name="fraction"/> of a <see cref="MeanRgb"/> histogram's pixels
    /// lie: <see cref="PeakFraction"/> for the banner's peak. An empty histogram is not a number.
    /// </summary>
    public static float LumaPercentile(ReadOnlySpan<int> histogram, float fraction)
    {
        long total = 0;
        foreach (var count in histogram)
        {
            total += count;
        }

        if (total == 0)
        {
            return float.NaN;
        }

        // The pixel count the fraction asks for, less a hair so float noise (0.995f is a shade over 0.995) never asks for one more.
        var need = Math.Max(1L, (long)Math.Ceiling((Math.Clamp(fraction, 0f, 1f) * (double)total) - 1e-3));
        long seen = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            seen += histogram[i];
            if (seen >= need)
            {
                return i;
            }
        }

        return histogram.Length - 1;
    }

    /// <summary>
    /// Grades a 32-bit image in place through the colour passes (<see cref="Grade"/>) at <paramref name="strength"/>;
    /// alpha is kept. B8G8R8A8 when <paramref name="bgra"/>, else R8G8B8A8.
    /// </summary>
    public static void GradeInPlace(Span<byte> pixels, int width, int height, int pitch, bool bgra, float strength)
    {
        var tint = Tint(strength);
        var desaturation = DesaturationAt(strength);
        for (var y = 0; y < height; y++)
        {
            var row = y * pitch;
            for (var x = 0; x < width; x++)
            {
                var o = row + (x * 4);
                if (o + 3 >= pixels.Length)
                {
                    return;
                }

                var (ri, bi) = bgra ? (o + 2, o) : (o, o + 2);
                var mr = pixels[ri] / 255f * tint.X;
                var mg = pixels[o + 1] / 255f * tint.Y;
                var mb = pixels[bi] / 255f * tint.Z;
                var grey = (0.299f * mr) + (0.587f * mg) + (0.114f * mb);
                pixels[ri] = Byte(mr + ((grey - mr) * desaturation));
                pixels[o + 1] = Byte(mg + ((grey - mg) * desaturation));
                pixels[bi] = Byte(mb + ((grey - mb) * desaturation));
            }
        }
    }

    /// <summary>The narrowest graded copy, in pixels, so a banner first drawn in a sliver still measures fairly.</summary>
    public const int MinCopyWidth = 64;

    /// <summary>How far the drawn width may outgrow a graded copy before it is graded again at the new width: 25 %.</summary>
    public const float RegradeGrowth = 1.25f;

    /// <summary>
    /// How wide a graded copy of a banner of <paramref name="sourceWidth"/> pixels drawn <paramref name="drawWidth"/>
    /// wide is read back: about the drawn width (at least <see cref="MinCopyWidth"/>), never wider than the art itself.
    /// </summary>
    public static int CopyWidth(int drawWidth, int sourceWidth) => Math.Min(Math.Max(drawWidth, MinCopyWidth), Math.Max(1, sourceWidth));

    /// <summary>
    /// Whether a graded copy <paramref name="copyWidth"/> wide should be graded again because the banner is now drawn
    /// <paramref name="drawWidth"/> wide: when the copy it would take (<see cref="CopyWidth"/>) is more than
    /// <see cref="RegradeGrowth"/> times as wide. A narrower draw never regrades; the GPU scales the copy down.
    /// </summary>
    public static bool Regrade(int copyWidth, int drawWidth, int sourceWidth) => CopyWidth(drawWidth, sourceWidth) > copyWidth * RegradeGrowth;

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
