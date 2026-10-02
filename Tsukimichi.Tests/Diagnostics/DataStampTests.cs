using Tsukimichi.Core.Diagnostics;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>The data version stamp in Settings › About and the status bar tooltip, and its game-version warning.</summary>
public class DataStampTests
{
    [Fact]
    public void The_line_reads_as_the_plan_example()
    {
        var line = DataStamp.Line("2026.09.15.0000.0000", 3464, new DateTime(2026, 9, 28, 22, 39, 14, DateTimeKind.Utc), "573d225");

        Assert.Equal("Data: game 2026.09.15 · unique rewards 3,464 (generated 2026-09-28) · curated 573d225", line);
    }

    [Fact]
    public void Missing_parts_read_unknown_and_a_missing_date_is_left_out()
    {
        Assert.Equal("Data: game unknown · unique rewards 0 · curated unknown", DataStamp.Line(string.Empty, 0, null, string.Empty));
        Assert.Equal("Data: game 2026.09.15 · unique rewards 12 · curated abc1234", DataStamp.Line("2026.09.15.0000.0000", 12, default(DateTime), "abc1234"));
    }

    [Theory]
    [InlineData("2026.09.15.0000.0000", "2026.09.15")]
    [InlineData("2026.09.15", "2026.09.15")]
    [InlineData("  2026.09.15.0001.0000 ", "2026.09.15")]
    [InlineData("", "unknown")]
    public void The_short_game_version_is_its_date_part(string version, string expected)
    {
        Assert.Equal(expected, DataStamp.ShortGameVersion(version));
    }

    [Fact]
    public void The_warning_appears_only_when_both_versions_are_known_and_differ()
    {
        Assert.Null(DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.09.15.0000.0000"));
        Assert.Null(DataStamp.MismatchWarning(string.Empty, "2026.09.15.0000.0000"));
        Assert.Null(DataStamp.MismatchWarning("2026.09.15.0000.0000", string.Empty));
        Assert.Equal(
            "Reward data was generated for game 2026.09.15; you are on 2026.10.20",
            DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.10.20.0000.0000"));
    }

    [Fact]
    public void The_warning_compares_game_versions_as_the_strip_does()
    {
        // A short or padded spelling of the same version is no mismatch.
        Assert.Null(DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.09.15\n"));
        // An older client keeps the About line (and gets no strip).
        Assert.Equal(
            "Reward data was generated for game 2026.09.15; you are on 2026.08.01",
            DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.08.01.0000.0000", newQuests: 0));
        // Unparseable versions still compare as text.
        Assert.Null(DataStamp.MismatchWarning("odd", "odd"));
        Assert.NotNull(DataStamp.MismatchWarning("odd", "other"));
    }

    [Fact]
    public void A_newer_client_with_new_quests_adds_the_strips_count()
    {
        Assert.Equal(
            "Reward data was generated for game 2026.09.15; you are on 2026.10.20, which has 1,234 quests newer than the data",
            DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.10.20.0000.0000", 1234));
        Assert.Equal(
            "Reward data was generated for game 2026.09.15; you are on 2026.10.20, which has 1 quest newer than the data",
            DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.10.20.0000.0000", 1));
        // An older client never carries a count.
        Assert.DoesNotContain("newer", DataStamp.MismatchWarning("2026.09.15.0000.0000", "2026.08.01.0000.0000", 5));
    }
}
