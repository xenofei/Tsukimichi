using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The banner on a light palette (docs/design/v7/ui/spec-1.16.md §A5): the daylight grade (no night multiply, 20 %
/// desaturation, brightness 1.04, the scrim to snow) and the location line's label ladder (the full path, then without
/// its prefix, then the level alone; never an ellipsis).
/// </summary>
public sealed class DaylightBannerTests
{
    [Fact]
    public void The_daylight_grade_desaturates_a_fifth_and_brightens_four_percent()
    {
        // A grey keeps its hue and gains 4 %.
        var grey = BannerGrade.Daylight(new Vector3(0.5f));
        Assert.Equal(0.52f, grey.X, 3);
        Assert.Equal(grey.X, grey.Z, 4);

        // Pure red moves a fifth of the way to its own grey, then brightens; white stays white (clamped).
        var red = BannerGrade.Daylight(Vector3.UnitX);
        var luma = BannerGrade.Luma(Vector3.UnitX) / 255f;
        Assert.Equal(MathF.Min(1f, (0.8f + (0.2f * luma)) * 1.04f), red.X, 3);
        Assert.Equal(0.2f * luma * 1.04f, red.Y, 3);
        Assert.Equal(Vector3.One, BannerGrade.Daylight(Vector3.One));
    }

    [Fact]
    public void Grading_in_place_keeps_alpha_and_reads_bgra()
    {
        byte[] pixels = [10, 20, 200, 128];
        BannerGrade.DaylightInPlace(pixels, 1, 1, 4, bgra: true);
        var expected = BannerGrade.Daylight(new Vector3(200 / 255f, 20 / 255f, 10 / 255f));
        Assert.Equal((byte)MathF.Round(expected.X * 255f), pixels[2]);
        Assert.Equal((byte)MathF.Round(expected.Z * 255f), pixels[0]);
        Assert.Equal(128, pixels[3]);
    }

    [Fact]
    public void The_daylight_scrim_falls_to_snow()
    {
        Assert.Equal([(0f, 0f), (0.40f, 0.22f), (1f, 0.92f)], BannerGrade.DaylightScrimStops);
    }

    [Fact]
    public void The_location_ladder_drops_the_prefix_then_the_path()
    {
        var rungs = LocationLine.Rungs(["Main Scenario (Heavensward)", "Heavensward"], " › ", "Lv 54");
        Assert.Equal(["Main Scenario (Heavensward) › Heavensward · Lv 54", "Heavensward · Lv 54", "Lv 54"], rungs);

        // One segment: no prefix to drop; no level: the last segment closes the ladder; nothing repeats.
        Assert.Equal(["Removed from the game · Lv 20", "Lv 20"], LocationLine.Rungs(["Removed from the game"], " › ", "Lv 20"));
        Assert.Equal(["A › B", "B"], LocationLine.Rungs(["A", " ", "B"], " › ", string.Empty));
        Assert.Equal(["Lv 1"], LocationLine.Rungs([], " › ", "Lv 1"));
        Assert.Empty(LocationLine.Rungs([], " › ", string.Empty));
    }

    [Fact]
    public void The_ladder_takes_the_longest_rung_that_fits_and_never_cuts()
    {
        float[] widths = [300f, 140f, 40f];
        Assert.Equal(0, LocationLine.Fit(widths, 320f));
        Assert.Equal(0, LocationLine.Fit(widths, 300.4f));
        Assert.Equal(1, LocationLine.Fit(widths, 299f));
        Assert.Equal(2, LocationLine.Fit(widths, 100f));

        // Even the shortest does not fit: it is still the one drawn (clipped by the banner, never ellipsised).
        Assert.Equal(2, LocationLine.Fit(widths, 10f));
        Assert.Equal(-1, LocationLine.Fit([], 100f));
    }
}
