using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class StarFieldTests
{
    [Fact]
    public void The_seed_is_expansion_times_7919_plus_the_row_count()
    {
        Assert.Equal(7919 * 3 + 12, StarField.Seed(3, 12));
        Assert.Equal(5, StarField.Seed(0, 5));
    }

    [Fact]
    public void The_same_seed_gives_the_same_sky()
    {
        var first = StarField.Generate(StarField.Seed(2, 9), 30);
        var second = StarField.Generate(StarField.Seed(2, 9), 30);
        var other = StarField.Generate(StarField.Seed(2, 10), 30);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Stars_stay_inside_the_unit_square()
    {
        foreach (var star in StarField.Generate(12345, 2000))
        {
            Assert.InRange(star.U, 0.03f, 0.97f);
            Assert.InRange(star.V, 0.03f, 0.97f);
        }
    }

    [Fact]
    public void Magnitudes_are_mostly_faint_with_a_few_sparkles()
    {
        var stars = StarField.Generate(777, 10_000);

        var faint = stars.Count(s => s.Magnitude == StarMagnitude.Faint) / 10_000f;
        var small = stars.Count(s => s.Magnitude == StarMagnitude.Small) / 10_000f;
        var bright = stars.Count(s => s.Magnitude == StarMagnitude.Bright) / 10_000f;

        Assert.InRange(faint, 0.66f, 0.74f);
        Assert.InRange(small, 0.21f, 0.29f);
        Assert.InRange(bright, 0.03f, 0.07f);
    }

    [Theory]
    [InlineData(324f, 92f, 21)]
    [InlineData(324f, 20f, 6)]
    [InlineData(324f, 1000f, 48)]
    [InlineData(0f, 100f, 6)]
    [InlineData(float.NaN, 100f, 6)]
    public void Density_is_one_star_per_1400_px_squared_between_6_and_48(float width, float height, int expected)
    {
        Assert.Equal(expected, StarField.CountFor(width, height));
    }

    [Fact]
    public void A_path_gets_one_band_per_expansion_header_seeded_by_its_row_count()
    {
        var rows = new[]
        {
            new PathRow(PathRowKind.Band) { Expansion = 0 },
            new PathRow(PathRowKind.FoldedRun) { Expansion = 0, Count = 5 },
            new PathRow(PathRowKind.Step) { Expansion = 0, RowId = 1 },
            new PathRow(PathRowKind.Band) { Expansion = 2 },
            new PathRow(PathRowKind.Step) { Expansion = 2, RowId = 2, IsTarget = true },
        };

        var bands = StarField.ForPath(rows);

        Assert.Equal(2, bands.Count);
        Assert.Equal((0, 0, 3), (bands[0].Expansion, bands[0].FirstRow, bands[0].RowCount));
        Assert.Equal((2, 3, 2), (bands[1].Expansion, bands[1].FirstRow, bands[1].RowCount));
        Assert.Equal(StarField.Seed(0, 3), bands[0].Seed);
        Assert.Equal(StarField.Seed(2, 2), bands[1].Seed);

        // 20 + 24 + 24 = 68 logical px at 324 wide: 16 stars; the second band, 20 + 32 = 52 px: 12.
        Assert.Equal(16, bands[0].Stars.Count);
        Assert.Equal(12, bands[1].Stars.Count);
        Assert.Equal(StarField.Generate(bands[0].Seed, 16), bands[0].Stars);
    }

    [Fact]
    public void A_path_of_one_still_gets_a_sky_and_no_rows_get_none()
    {
        var single = StarField.ForPath([new PathRow(PathRowKind.Step) { Expansion = 4, RowId = 9, IsTarget = true, State = QuestState.Ready }]);

        var band = Assert.Single(single);
        Assert.Equal(4, band.Expansion);
        // One 32 px target row at 324 wide: 7 stars.
        Assert.Equal(7, band.Stars.Count);
        Assert.Empty(StarField.ForPath([]));
    }
}
