using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Full's banner night grade (docs/design/flair-v13/spec.md §1.2, supervisor fix 1, implementation review fix 1): the
/// multiply strength chosen as the least within 0–0.75 that brings the graded art's mean L to 55 and its 99.5th
/// percentile to 170, the desaturation scaled with it, the colour passes in order, and the measurement pass.
/// </summary>
public class BannerGradeTests
{
    [Fact]
    public void The_strength_stays_within_its_bounds()
    {
        foreach (var meanL in new[] { 0f, 10f, 40f, 80f, 110f, 160f, 220f, 255f, -5f, 400f })
        {
            foreach (var peakL in new[] { float.NaN, 0f, 120f, 200f, 255f, 400f })
            {
                var s = BannerGrade.Strength(meanL, peakL);
                Assert.InRange(s, BannerGrade.MinStrength, BannerGrade.MaxStrength);
            }
        }
    }

    [Fact]
    public void A_dark_banner_takes_no_grade_and_a_daylight_one_the_strongest()
    {
        Assert.Equal(0f, BannerGrade.MinStrength);
        Assert.Equal(BannerGrade.MinStrength, BannerGrade.Strength(30f, 150f));
        Assert.Equal(BannerGrade.MaxStrength, BannerGrade.Strength(250f, 255f));
    }

    [Fact]
    public void The_strength_rises_with_the_banner_s_lightness_and_its_peak()
    {
        var previous = 0f;
        for (var meanL = 0f; meanL <= 255f; meanL += 5f)
        {
            var s = BannerGrade.Strength(meanL);
            Assert.True(s >= previous - 1e-5f, $"{meanL}: {s} < {previous}");
            previous = s;
        }

        previous = 0f;
        for (var peakL = 100f; peakL <= 255f; peakL += 5f)
        {
            var s = BannerGrade.Strength(40f, peakL);
            Assert.True(s >= previous - 1e-5f, $"peak {peakL}: {s} < {previous}");
            previous = s;
        }
    }

    [Fact]
    public void The_chosen_strength_brings_the_banner_to_the_target_where_it_can()
    {
        for (var meanL = 0f; meanL <= 255f; meanL += 5f)
        {
            var rgb = new Vector3(meanL / 255f);
            var s = BannerGrade.Strength(rgb);
            var graded = BannerGrade.GradedArtMeanL(rgb, s);
            if (s < BannerGrade.MaxStrength)
            {
                Assert.True(graded <= BannerGrade.TargetMeanL + 0.05f, $"{meanL}: graded {graded} at {s}");
                Assert.True(BannerGrade.GradedMeanL(rgb, s) <= BannerGrade.TargetMeanL + 0.05f);
            }

            // Never darker than needed: a weaker grade would have met the target too, so it is the weakest one.
            if (s > BannerGrade.MinStrength + 1e-4f && s < BannerGrade.MaxStrength - 1e-4f)
            {
                Assert.True(BannerGrade.GradedArtMeanL(rgb, s - 0.01f) > BannerGrade.TargetMeanL);
            }
        }
    }

    [Fact]
    public void A_night_banner_is_graded_lightly_by_its_peak()
    {
        // The plugin's own banners are moonlit already: msq-dt measures mean L 45, peak 221 (implementation review).
        var s = BannerGrade.Strength(45f, 221f);
        Assert.InRange(s, 0.2f, 0.4f);
        Assert.True(BannerGrade.GradedArtMeanL(new Vector3(45f / 255f), s) <= BannerGrade.TargetMeanL);
        Assert.True(BannerGrade.GradedPeakL(221f, s) <= BannerGrade.TargetPeakL + 0.01f);

        // Just the least: any weaker and the peak would sit over the target.
        Assert.True(BannerGrade.GradedPeakL(221f, s - 0.01f) > BannerGrade.TargetPeakL);

        // Its colour mostly kept: under half the default desaturation.
        Assert.True(BannerGrade.DesaturationAt(s) < BannerGrade.Desaturation * 0.6f);
    }

    [Fact]
    public void A_daylight_banner_is_graded_strongly_by_its_mean()
    {
        // The mock's scene-day.jpg, cover-cropped to the hero: mean (0.283, 0.470, 0.537), L 107.6; 99.5th percentile 232.
        foreach (var day in new[] { new Vector3(0.2832f, 0.4704f, 0.5374f), new Vector3(107.6f / 255f) })
        {
            var s = BannerGrade.Strength(day, 232.3f);
            Assert.InRange(s, 0.5f, BannerGrade.MaxStrength);
            Assert.True(BannerGrade.GradedArtMeanL(day, s) <= BannerGrade.TargetMeanL + 0.05f, $"{day}: {BannerGrade.GradedArtMeanL(day, s)}");
            Assert.True(BannerGrade.GradedPeakL(232.3f, s) <= BannerGrade.TargetPeakL);
        }

        Assert.InRange(BannerGrade.Strength(107.6f, 246f), 0.5f, BannerGrade.MaxStrength);
    }

    [Fact]
    public void A_daylight_banner_like_dawntrail_s_meets_the_mean_target()
    {
        // A sunlit zone: blue sky, white cloud, warm stone. The mock measured Dawntrail's banner at mean L 65 graded at
        // 0.55 and 52.5 at 0.70, which puts its own mean near L 155, cool.
        var dawntrail = new Vector3(0.55f, 0.62f, 0.72f);
        var s = BannerGrade.Strength(dawntrail);
        Assert.InRange(s, 0.6f, BannerGrade.MaxStrength);
        Assert.True(BannerGrade.GradedMeanL(dawntrail, s) <= BannerGrade.TargetMeanL + 0.05f);
    }

    [Fact]
    public void An_unmeasured_banner_takes_the_default_strength()
    {
        Assert.Equal(BannerGrade.DefaultStrength, BannerGrade.Strength(new Vector3(float.NaN)));
        Assert.Equal(BannerGrade.DefaultStrength, BannerGrade.Strength(new Vector3(float.NaN), 240f));
        Assert.Equal(BannerGrade.DefaultStrength, BannerGrade.Strength(float.NaN));
        Assert.Equal(BannerGrade.DefaultStrength, BannerGrade.ClampStrength(float.PositiveInfinity));
        Assert.Equal(BannerGrade.MinStrength, BannerGrade.ClampStrength(-0.1f));
        Assert.Equal(BannerGrade.MaxStrength, BannerGrade.ClampStrength(0.9f));
    }

    [Fact]
    public void The_tint_runs_from_white_toward_indigo()
    {
        var indigo = BannerGrade.Indigo;
        Assert.Equal(Vector4.One, BannerGrade.Tint(BannerGrade.MinStrength));
        var weak = BannerGrade.Tint(0.3f);
        var strong = BannerGrade.Tint(BannerGrade.MaxStrength);
        Assert.True(strong.X < weak.X && strong.Y < weak.Y && strong.Z < weak.Z);
        Assert.True(strong.Z > strong.X, "the multiply cools the art toward indigo");
        Assert.Equal(1f - 0.75f + (0.75f * indigo.X), strong.X, 4);
        Assert.InRange(BannerGrade.LumaDrop, 0.77f, 0.78f);
    }

    [Fact]
    public void The_desaturation_scales_with_the_strength()
    {
        Assert.Equal(0f, BannerGrade.DesaturationAt(0f));
        Assert.Equal(BannerGrade.Desaturation, BannerGrade.DesaturationAt(BannerGrade.DefaultStrength), 5);
        Assert.Equal(BannerGrade.Desaturation * 0.5f, BannerGrade.DesaturationAt(0.35f), 5);

        // Ungraded art keeps its colour exactly.
        var warm = new Vector3(0.9f, 0.6f, 0.2f);
        Assert.Equal(warm, BannerGrade.Grade(warm, 0f));
    }

    [Fact]
    public void Even_white_stays_under_the_hero_moon_and_loses_its_colour()
    {
        // White as a banner's peak: the strength its peak asks for brings it to the target.
        var forWhite = BannerGrade.Strength(40f, 255f);
        var white = BannerGrade.Grade(Vector3.One, forWhite);
        Assert.True(BannerGrade.Luma(white) <= BannerGrade.TargetPeakL + 0.01f, $"white at {forWhite}: {BannerGrade.Luma(white)}");
        foreach (var s in new[] { BannerGrade.DefaultStrength, BannerGrade.MaxStrength })
        {
            Assert.True(BannerGrade.Luma(BannerGrade.Grade(Vector3.One, s)) <= BannerGrade.TargetPeakL, $"white at {s}");
        }

        // Saturated sky blue keeps its lightness through the desaturation but loses its share of chroma.
        var sky = new Vector3(0.25f, 0.55f, 0.95f);
        var multiplied = Vector3.Multiply(sky, new Vector3(BannerGrade.Tint(0.6f).X, BannerGrade.Tint(0.6f).Y, BannerGrade.Tint(0.6f).Z));
        var graded = BannerGrade.Grade(sky, 0.6f);
        Assert.Equal(BannerGrade.Luma(multiplied), BannerGrade.Luma(graded), 2);
        var chromaBefore = MathF.Max(multiplied.X, MathF.Max(multiplied.Y, multiplied.Z)) - MathF.Min(multiplied.X, MathF.Min(multiplied.Y, multiplied.Z));
        var chromaAfter = MathF.Max(graded.X, MathF.Max(graded.Y, graded.Z)) - MathF.Min(graded.X, MathF.Min(graded.Y, graded.Z));
        Assert.Equal(chromaBefore * (1f - BannerGrade.DesaturationAt(0.6f)), chromaAfter, 3);
    }

    [Fact]
    public void Grading_in_place_matches_the_pixel_grade_and_keeps_alpha()
    {
        // Two BGRA pixels and two RGBA ones.
        byte[] bgra = [200, 120, 40, 255, 10, 20, 30, 128];
        byte[] rgba = [40, 120, 200, 255, 30, 20, 10, 128];
        BannerGrade.GradeInPlace(bgra, 2, 1, 8, bgra: true, 0.4f);
        BannerGrade.GradeInPlace(rgba, 2, 1, 8, bgra: false, 0.4f);
        var expected = BannerGrade.Grade(new Vector3(40f, 120f, 200f) / 255f, 0.4f);
        Assert.Equal(expected.X * 255f, bgra[2], 0);
        Assert.Equal(expected.Y * 255f, bgra[1], 0);
        Assert.Equal(expected.Z * 255f, bgra[0], 0);
        Assert.Equal(bgra[2], rgba[0]);
        Assert.Equal(bgra[1], rgba[1]);
        Assert.Equal(bgra[0], rgba[2]);
        Assert.Equal(255, bgra[3]);
        Assert.Equal(128, bgra[7]);
    }

    [Fact]
    public void The_mean_colour_skips_transparent_pixels()
    {
        byte[] rgba = [255, 0, 0, 255, 0, 0, 255, 255, 9, 9, 9, 0];
        var mean = BannerGrade.MeanRgb(rgba, 3, 1, 12, bgra: false);
        Assert.Equal(0.5f, mean.X, 3);
        Assert.Equal(0f, mean.Y, 3);
        Assert.Equal(0.5f, mean.Z, 3);
        Assert.True(float.IsNaN(BannerGrade.MeanRgb([0, 0, 0, 0], 1, 1, 4, bgra: false).X));
    }

    [Fact]
    public void The_same_pass_counts_a_luma_histogram_for_the_peak()
    {
        // 200 grey pixels: 199 at L 40 and one lantern at L 250, then a transparent one that is not counted.
        var pixels = new byte[201 * 4];
        for (var i = 0; i < 201; i++)
        {
            var v = (byte)(i == 0 ? 250 : 40);
            pixels[(i * 4) + 0] = v;
            pixels[(i * 4) + 1] = v;
            pixels[(i * 4) + 2] = v;
            pixels[(i * 4) + 3] = (byte)(i == 200 ? 0 : 255);
        }

        var histogram = new int[BannerGrade.HistogramBins];
        BannerGrade.MeanRgb(pixels, 201, 1, 201 * 4, bgra: true, lumaHistogram: histogram);
        Assert.Equal(200, histogram.Sum());
        Assert.Equal(199, histogram[40]);
        Assert.Equal(1, histogram[250]);

        // One lantern pixel in two hundred is past the 99.5th percentile: it does not decide the grade.
        Assert.Equal(40f, BannerGrade.LumaPercentile(histogram, BannerGrade.PeakFraction));
        Assert.Equal(250f, BannerGrade.LumaPercentile(histogram, 1f));
        Assert.True(float.IsNaN(BannerGrade.LumaPercentile(new int[BannerGrade.HistogramBins], BannerGrade.PeakFraction)));
    }

    [Fact]
    public void Graded_images_meet_both_targets()
    {
        // A night scene (dark sky, mean L about 46, with a moon's worth of bright pixels) and a daylight one (bright sky
        // over darker ground, mean L about 109), 380 × 156, measured, graded and measured again as the plugin does it.
        foreach (var day in new[] { false, true })
        {
            const int W = 380, H = 156;
            var pixels = new byte[W * H * 4];
            var random = new Random(day ? 7 : 3);
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    var o = ((y * W) + x) * 4;
                    var (r, g, b) = day
                        ? (y < H / 2 ? (100 + random.Next(40), 160 + random.Next(40), 200 + random.Next(40)) : (20 + random.Next(40), 40 + random.Next(40), 20 + random.Next(30)))
                        : (20 + random.Next(30), 30 + random.Next(30), 60 + random.Next(40));
                    if (!day && random.Next(40) == 0)
                    {
                        (r, g, b) = (230, 220, 190);
                    }

                    pixels[o] = (byte)b;
                    pixels[o + 1] = (byte)g;
                    pixels[o + 2] = (byte)r;
                    pixels[o + 3] = 255;
                }
            }

            var histogram = new int[BannerGrade.HistogramBins];
            var mean = BannerGrade.MeanRgb(pixels, W, H, W * 4, bgra: true, lumaHistogram: histogram);
            var peak = BannerGrade.LumaPercentile(histogram, BannerGrade.PeakFraction);
            var s = BannerGrade.Strength(mean, peak);
            Assert.InRange(s, day ? 0.5f : 0.2f, day ? BannerGrade.MaxStrength : 0.4f);

            BannerGrade.GradeInPlace(pixels, W, H, W * 4, bgra: true, s);
            Array.Clear(histogram);
            var after = BannerGrade.MeanRgb(pixels, W, H, W * 4, bgra: true, lumaHistogram: histogram);
            Assert.True(BannerGrade.Luma(after) <= BannerGrade.TargetMeanL + 0.5f, $"{(day ? "day" : "night")} at {s}: mean {BannerGrade.Luma(after)}");
            Assert.True(BannerGrade.LumaPercentile(histogram, BannerGrade.PeakFraction) <= BannerGrade.TargetPeakL + 1f, $"{(day ? "day" : "night")} at {s}: peak {BannerGrade.LumaPercentile(histogram, BannerGrade.PeakFraction)}");
        }
    }

    [Fact]
    public void A_copy_is_graded_again_when_the_banner_is_drawn_much_wider()
    {
        Assert.Equal(400, BannerGrade.CopyWidth(400, 752));
        Assert.Equal(BannerGrade.MinCopyWidth, BannerGrade.CopyWidth(10, 752));
        Assert.Equal(752, BannerGrade.CopyWidth(1600, 752));
        Assert.Equal(40, BannerGrade.CopyWidth(400, 40));

        Assert.False(BannerGrade.Regrade(400, 480, 752));
        Assert.False(BannerGrade.Regrade(400, 300, 752));
        Assert.True(BannerGrade.Regrade(400, 520, 752));

        // Capped at the art's own width: a copy as wide as the art is never graded again.
        Assert.True(BannerGrade.Regrade(64, 2000, 752));
        Assert.False(BannerGrade.Regrade(752, 2000, 752));
        Assert.False(BannerGrade.Regrade(700, 2000, 752));
    }

    [Fact]
    public void The_scrim_runs_from_clear_at_the_top_to_night_at_the_foot()
    {
        Assert.Equal(0f, BannerGrade.ScrimAlpha(0f));
        Assert.Equal(0.25f, BannerGrade.ScrimAlpha(0.45f), 4);
        Assert.Equal(0.85f, BannerGrade.ScrimAlpha(1f), 4);
        Assert.InRange(BannerGrade.ScrimMeanAlpha, 0.3f, 0.4f);
        var previous = -1f;
        foreach (var (y, width, alpha, _) in BannerGrade.RoadDashes)
        {
            Assert.True(y > previous && width > 0f && alpha is > 0f and <= 0.2f);
            previous = y;
        }
    }
}
