using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Model;

/// <summary>PatchVersion (P8): patch numbers compare as the game writes them, the fraction as a decimal.</summary>
public class PatchVersionTests
{
    [Theory]
    [InlineData("7.5", "7.51")]
    [InlineData("7.51", "7.55")]
    [InlineData("7.55", "7.56")]
    [InlineData("7.56", "7.6")]
    [InlineData("7.5", "7.6")]
    [InlineData("3.07", "3.1")]
    [InlineData("3.0", "3.01")]
    [InlineData("2.55", "3.0")]
    [InlineData("6.58", "7.0")]
    [InlineData("9.9", "10.0")]
    [InlineData("", "2.0")]
    [InlineData("", "not a patch")]
    [InlineData("not a patch", "2.0")]
    public void Older_patch_sorts_first(string older, string newer)
    {
        Assert.True(PatchVersion.Compare(older, newer) < 0, $"{older} should be older than {newer}");
        Assert.True(PatchVersion.Compare(newer, older) > 0, $"{newer} should be newer than {older}");
        Assert.True(PatchVersion.Comparer.Compare(older, newer) < 0);
        Assert.True(PatchVersion.NewestFirst.Compare(older, newer) > 0);
    }

    [Theory]
    [InlineData("7.5", "7.50")]
    [InlineData("2.0", "2.00")]
    [InlineData("", null)]
    public void Equal_spellings_compare_equal(string a, string? b)
    {
        Assert.Equal(0, PatchVersion.Compare(a, b));
    }

    [Fact]
    public void Sorting_follows_the_game_not_the_string_or_the_number()
    {
        string[] patches = ["7.6", "7.55", "7.5", "7.51", "3.1", "3.07", "", "2.0", "10.0"];

        var sorted = patches.Order(PatchVersion.Comparer).ToArray();

        Assert.Equal(["", "2.0", "3.07", "3.1", "7.5", "7.51", "7.55", "7.6", "10.0"], sorted);
    }

    [Theory]
    [InlineData("7.55", "7.5")]
    [InlineData("7.5", "7.5")]
    [InlineData("7.51", "7.5")]
    [InlineData("3.07", "3.0")]
    [InlineData("2.0", "2.0")]
    [InlineData("10.35", "10.3")]
    [InlineData("", "")]
    [InlineData("soon", "")]
    public void Series_is_the_major_and_first_fraction_digit(string patch, string series)
    {
        Assert.Equal(series, PatchVersion.Series(patch));
    }

    [Theory]
    [InlineData("7.55", "7.5", true)]
    [InlineData("7.5", "7.5", true)]
    [InlineData("7.51", "7.5", true)]
    [InlineData("7.4", "7.5", false)]
    [InlineData("17.5", "7.5", false)]
    [InlineData("7.6", "7.5", false)]
    [InlineData("", "7.5", false)]
    [InlineData("7.5", "", false)]
    public void In_series_matches_the_series_prefix(string patch, string series, bool expected)
    {
        Assert.Equal(expected, PatchVersion.InSeries(patch, series));
    }

    [Theory]
    [InlineData("2", "2.0")]
    [InlineData("2.0", "2.0")]
    [InlineData("7.50", "7.5")]
    [InlineData(" 7.55 ", "7.55")]
    [InlineData("6.00", "6.0")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("n/a", "n/a")]
    public void Normalize_gives_the_canonical_spelling(string? text, string expected)
    {
        Assert.Equal(expected, PatchVersion.Normalize(text));
    }

    [Theory]
    [InlineData("7.5", true)]
    [InlineData("10.01", true)]
    [InlineData("7", false)]
    [InlineData("7.", false)]
    [InlineData(".5", false)]
    [InlineData("7.5a", false)]
    [InlineData("", false)]
    public void Is_patch_accepts_only_major_dot_digits(string text, bool expected)
    {
        Assert.Equal(expected, PatchVersion.IsPatch(text));
    }

    [Fact]
    public void Newest_ignores_unknown_and_non_patches()
    {
        Assert.Equal("7.56", PatchVersion.Newest(["2.0", "", "7.56", "7.6x", "7.55", "7.5"]));
        Assert.Equal(string.Empty, PatchVersion.Newest(["", "soon"]));
    }
}
