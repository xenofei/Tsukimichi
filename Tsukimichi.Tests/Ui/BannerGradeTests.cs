using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Full's banner night grade (docs/design/flair-v13/spec.md §1.2, supervisor fix 1): the multiply strength chosen from a
/// banner's mean lightness within 0.55–0.75, the colour passes in order, and the targets (mean L at most 55, the
/// brightest pixel under the hero moon).
/// </summary>
public class BannerGradeTests
{
    [Fact]
    public void The_strength_stays_within_its_bounds()
    {
        foreach (var meanL in new[] { 0f, 10f, 40f, 80f, 110f, 160f, 220f, 255f, -5f, 400f })
        {
            var s = BannerGrade.Strength(meanL);
            Assert.InRange(s, BannerGrade.MinStrength, BannerGrade.MaxStrength);
        }
    }

    [Fact]
    public void A_dark_banner_takes_the_weakest_grade_and_a_daylight_one_the_strongest()
    {
        Assert.Equal(BannerGrade.MinStrength, BannerGrade.Strength(30f));
        Assert.Equal(BannerGrade.MaxStrength, BannerGrade.Strength(250f));
    }

    [Fact]
    public void The_strength_rises_with_the_banner_s_lightness()
    {
        var previous = 0f;
        for (var meanL = 0f; meanL <= 255f; meanL += 5f)
        {
            var s = BannerGrade.Strength(meanL);
            Assert.True(s >= previous - 1e-5f, $"{meanL}: {s} < {previous}");
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
            var graded = BannerGrade.GradedMeanL(rgb, s);
            if (s < BannerGrade.MaxStrength)
            {
                Assert.True(graded <= BannerGrade.TargetMeanL + 0.05f, $"{meanL}: graded {graded} at {s}");
            }

            // Never darker than needed: a weaker grade would have met the target too, so it is the weakest one.
            if (s > BannerGrade.MinStrength + 1e-4f && s < BannerGrade.MaxStrength - 1e-4f)
            {
                Assert.True(BannerGrade.GradedMeanL(rgb, s - 0.01f) > BannerGrade.TargetMeanL);
            }
        }
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
        Assert.Equal(BannerGrade.DefaultStrength, BannerGrade.Strength(float.NaN));
        Assert.Equal(BannerGrade.DefaultStrength, BannerGrade.ClampStrength(float.PositiveInfinity));
        Assert.Equal(BannerGrade.MinStrength, BannerGrade.ClampStrength(0.1f));
        Assert.Equal(BannerGrade.MaxStrength, BannerGrade.ClampStrength(0.9f));
    }

    [Fact]
    public void The_tint_runs_from_white_toward_indigo()
    {
        var indigo = BannerGrade.Indigo;
        var weak = BannerGrade.Tint(BannerGrade.MinStrength);
        var strong = BannerGrade.Tint(BannerGrade.MaxStrength);
        Assert.True(strong.X < weak.X && strong.Y < weak.Y && strong.Z < weak.Z);
        Assert.True(strong.Z > strong.X, "the multiply cools the art toward indigo");
        Assert.Equal(1f - 0.75f + (0.75f * indigo.X), strong.X, 4);
    }

    [Fact]
    public void Even_white_stays_under_the_hero_moon_and_loses_its_colour()
    {
        foreach (var s in new[] { BannerGrade.MinStrength, BannerGrade.DefaultStrength, BannerGrade.MaxStrength })
        {
            var white = BannerGrade.Grade(Vector3.One, s);
            Assert.True(BannerGrade.Luma(white) <= BannerGrade.TargetPeakL, $"white at {s}: {BannerGrade.Luma(white)}");
        }

        // Saturated sky blue keeps its lightness through the desaturation but loses a third of its chroma.
        var sky = new Vector3(0.25f, 0.55f, 0.95f);
        var multiplied = Vector3.Multiply(sky, new Vector3(BannerGrade.Tint(0.6f).X, BannerGrade.Tint(0.6f).Y, BannerGrade.Tint(0.6f).Z));
        var graded = BannerGrade.Grade(sky, 0.6f);
        Assert.Equal(BannerGrade.Luma(multiplied), BannerGrade.Luma(graded), 2);
        var chromaBefore = MathF.Max(multiplied.X, MathF.Max(multiplied.Y, multiplied.Z)) - MathF.Min(multiplied.X, MathF.Min(multiplied.Y, multiplied.Z));
        var chromaAfter = MathF.Max(graded.X, MathF.Max(graded.Y, graded.Z)) - MathF.Min(graded.X, MathF.Min(graded.Y, graded.Z));
        Assert.Equal(chromaBefore * (1f - BannerGrade.Desaturation), chromaAfter, 3);
    }

    [Fact]
    public void Grading_in_place_matches_the_pixel_grade_and_keeps_alpha()
    {
        // Two BGRA pixels and two RGBA ones.
        byte[] bgra = [200, 120, 40, 255, 10, 20, 30, 128];
        byte[] rgba = [40, 120, 200, 255, 30, 20, 10, 128];
        BannerGrade.GradeInPlace(bgra, 2, 1, 8, bgra: true, 0.7f);
        BannerGrade.GradeInPlace(rgba, 2, 1, 8, bgra: false, 0.7f);
        var expected = BannerGrade.Grade(new Vector3(40f, 120f, 200f) / 255f, 0.7f);
        Assert.Equal(expected.X * 255f, bgra[2], 0);
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
