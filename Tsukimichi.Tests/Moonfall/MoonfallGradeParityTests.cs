using System.Buffers.Binary;
using Tsukimichi.Core.Moonfall.Art;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The runtime grades against the approved Python (docs/design/v9/runtime/parity/make_parity.py): one small synthetic
/// picture of our own, graded by rich_lib.night_lab, dress2.jewel and r2lib.gild there and by <see cref="MoonfallGrade"/>
/// here. Every case must agree within <see cref="Tolerance"/> on 99.9% of channels (the sRGB table and float order
/// differ in the last bits), and on average within <see cref="MeanTolerance"/>.
/// </summary>
public sealed class MoonfallGradeParityTests(ITestOutputHelper output)
{
    /// <summary>Most a channel may differ: two 8-bit steps.</summary>
    private const float Tolerance = 2.5f / 255f;

    private const float MeanTolerance = 0.5f / 255f;

    private const float S = 0.12f;

    internal static string FixtureDir() => Path.Combine(Ui.OrnamentLayoutTests.RepoRoot(), "Tsukimichi.Tests", "Fixtures", "moonfall-grade");

    internal static MoonfallImage LoadPng(string name, bool alpha = false)
    {
        var bytes = File.ReadAllBytes(Path.Combine(FixtureDir(), name));
        var decoded = MoonfallPng.Decode(bytes, out var error);
        Assert.True(decoded is not null, error);
        var (rgba, w, h) = decoded.Value;
        return MoonfallImage.FromBytes(rgba, w, h, w * 4, bgra: false, keepAlpha: alpha);
    }

    private static float[] LoadBin(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(FixtureDir(), name));
        var values = new float[bytes.Length / 4];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4, 4));
        }

        return values;
    }

    private void AssertMatches(string reference, MoonfallImage actual)
    {
        var expected = LoadBin(reference);
        var n = actual.Width * actual.Height;
        Assert.Equal(n * 3, expected.Length);
        var worst = 0f;
        var sum = 0.0;
        var over = 0;
        for (var i = 0; i < n; i++)
        {
            ReadOnlySpan<float> got = [actual.R.Data[i], actual.G.Data[i], actual.B.Data[i]];
            for (var c = 0; c < 3; c++)
            {
                var d = MathF.Abs(got[c] - expected[(i * 3) + c]);
                worst = MathF.Max(worst, d);
                sum += d;
                if (d > Tolerance)
                {
                    over++;
                }
            }
        }

        var mean = sum / (n * 3);
        output.WriteLine($"{reference}: worst {worst * 255:0.00}/255, mean {mean * 255:0.000}/255, {over} of {n * 3} channels over {Tolerance * 255:0.0}/255");
        Assert.True(over <= n * 3 / 1000, $"{reference}: {over} channels differ by more than {Tolerance * 255:0.0}/255 (worst {worst * 255:0.00})");
        Assert.True(mean <= MeanTolerance, $"{reference}: mean difference {mean * 255:0.000}/255");
    }

    [Fact]
    public void The_night_grade_matches_the_reference()
    {
        AssertMatches("nightlab.bin", MoonfallGrade.NightLab(LoadPng("fixture.png"), new MoonfallNightGrade(), S));
    }

    [Fact]
    public void The_chart_grade_matches_the_reference()
    {
        var grade = new MoonfallNightGrade { Gamma = 1.35f, Exposure = 0.72f, WarmKeep = 0.25f, ChromaMid = 0.45f };
        AssertMatches("nightlab-chart.bin", MoonfallGrade.NightLab(LoadPng("fixture.png"), grade, S));
    }

    [Fact]
    public void The_night_grade_with_its_sky_matches_the_reference()
    {
        var grade = new MoonfallNightGrade { SkyDrop = 0.3f, SkyTop = 0.35f, SkyBottom = 0.75f };
        AssertMatches("nightlab-sky.bin", MoonfallGrade.NightLab(LoadPng("fixture.png"), grade, S));
    }

    [Fact]
    public void The_far_shore_violet_grade_matches_the_reference()
    {
        var grade = MoonfallNightGrade.Violet with { Gamma = 1.8f, Exposure = 0.70f };
        AssertMatches("nightlab-violet.bin", MoonfallGrade.NightLab(LoadPng("fixture.png"), grade, S));
    }

    [Fact]
    public void The_jewel_palette_with_a_second_jewel_matches_the_reference()
    {
        var graded = MoonfallGrade.NightLab(LoadPng("fixture.png"), new MoonfallNightGrade { Gamma = 1.35f, Exposure = 0.72f, WarmKeep = 0.25f, ChromaMid = 0.45f }, S);
        var sea = MoonfallFilters.Blur(MoonfallGrade.Lightness(graded), 3 * S);
        for (var i = 0; i < sea.Data.Length; i++)
        {
            sea.Data[i] = MoonfallColor.Smooth(0.30f, 0.42f, sea.Data[i]) * 0.85f;
        }

        var jewel = new MoonfallJewelGrade
        {
            Bands = [(0f, MoonfallColor.Hex("#2B5FD0")), (600f, MoonfallColor.Hex("#1D4DB8"))],
            Chroma = 0.9f,
            Floor = 0.03f,
            Regions = [new MoonfallJewelRegion(sea, MoonfallColor.Hex("#169A9A"), 0.085f)],
        };
        AssertMatches("jewel.bin", MoonfallGrade.Jewel(graded, jewel, S));
    }

    [Fact]
    public void The_jewel_palette_with_value_hues_matches_the_reference()
    {
        var graded = MoonfallGrade.NightLab(LoadPng("fixture.png"), new MoonfallNightGrade { Gamma = 1.35f, Exposure = 0.72f, WarmKeep = 0.25f, ChromaMid = 0.45f }, S);
        var jewel = new MoonfallJewelGrade
        {
            Bands = [(0f, MoonfallColor.Hex("#3A6FD8")), (600f, MoonfallColor.Hex("#2B4FB0"))],
            ValueHues = [(0.08f, MoonfallColor.Hex("#1E3A9A")), (0.45f, MoonfallColor.Hex("#C9D6FF"))],
            Chroma = 0.95f,
            Floor = 0.03f,
        };
        AssertMatches("jewel-values.bin", MoonfallGrade.Jewel(graded, jewel, S));
    }

    [Fact]
    public void Gild_matches_the_reference()
    {
        AssertMatches("gild.bin", MoonfallGrade.Gild(LoadPng("fixture-rgba.png", alpha: true), 0.55f, 0.1f));
    }

    [Fact]
    public void The_jewel_palette_keeps_every_pixels_lightness()
    {
        // F1: colour never moves value. The grade keeps OKLab L; only a colour pushed out of sRGB's gamut and clipped
        // back (as the Python's oklab_to_srgb clips) moves it, a little, on a few pixels.
        var graded = MoonfallGrade.NightLab(LoadPng("fixture.png"), new MoonfallNightGrade(), S);
        var jewel = new MoonfallJewelGrade { Bands = [(0f, MoonfallColor.Hex("#6B3FA8")), (600f, MoonfallColor.Hex("#1F6E78"))], Chroma = 0.9f, Floor = 0.03f };
        var before = MoonfallGrade.Lightness(graded);
        var after = MoonfallGrade.Lightness(MoonfallGrade.Jewel(graded, jewel, S));
        var moved = before.Data.Zip(after.Data, static (a, b) => MathF.Abs(a - b)).Order().ToArray();
        var p99 = moved[(int)(moved.Length * 0.99)];
        output.WriteLine($"lightness moved: 99th percentile {p99:0.0000}, worst {moved[^1]:0.0000}");
        Assert.True(p99 < 0.002f, $"99th percentile {p99}");
        Assert.True(moved[^1] < 0.02f, $"worst {moved[^1]}");
    }
}
