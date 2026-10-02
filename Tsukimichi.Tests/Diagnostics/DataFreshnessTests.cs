using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>The "Game updated" strip's verdict and the quests new since the shipped data (1.5.0 "Trust").</summary>
public class DataFreshnessTests
{
    private const string Data = "2026.09.15.0000.0000";

    /// <summary>quest_patches.json as written against <see cref="Data"/>: ids 1 to 4, one with an unknown patch.</summary>
    private static readonly QuestPatches Patches = new(Data, new Dictionary<uint, string> { [1] = "2.0", [2] = "7.4", [3] = "7.5", [4] = "" }, []);

    [Fact]
    public void Same_game_version_is_current_with_nothing_new()
    {
        var report = DataFreshness.Evaluate([1, 2, 3, 4], Patches, Data, Data);

        Assert.Equal(FreshnessVerdict.Current, report.Verdict);
        Assert.Equal(0, report.NewQuests);
        Assert.False(report.ShowStrip);
    }

    [Fact]
    public void A_newer_client_with_unlisted_quests_shows_the_strip()
    {
        var report = DataFreshness.Evaluate([1, 2, 3, 4, 70001, 70002], Patches, Data, "2026.10.28.0000.0000");

        Assert.Equal(FreshnessVerdict.NewerClient, report.Verdict);
        Assert.Equal(2, report.NewQuests);
        Assert.True(report.IsNew(70001));
        Assert.False(report.IsNew(4)); // listed with an unknown patch: not new
        Assert.True(report.ShowStrip);
        Assert.Equal("2026.10.28.0000.0000", report.ClientVersion);
        Assert.Equal(Data, report.DataVersion);
    }

    [Fact]
    public void A_hotfix_without_new_quests_is_newer_but_shows_no_strip()
    {
        var report = DataFreshness.Evaluate([1, 2, 3, 4], Patches, Data, "2026.09.15.0001.0000");

        Assert.Equal(FreshnessVerdict.NewerClient, report.Verdict);
        Assert.False(report.ShowStrip);
    }

    [Fact]
    public void An_older_client_never_shows_the_strip()
    {
        var report = DataFreshness.Evaluate([1, 2, 3, 70001], Patches, Data, "2026.08.01.0000.0000");

        Assert.Equal(FreshnessVerdict.OlderClient, report.Verdict);
        Assert.False(report.ShowStrip);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unknown")]
    public void An_unreadable_client_version_is_newer_only_on_the_evidence_of_new_quests(string? client)
    {
        Assert.Equal(FreshnessVerdict.NewerClient, DataFreshness.Evaluate([1, 70001], Patches, Data, client).Verdict);
        Assert.Equal(FreshnessVerdict.Current, DataFreshness.Evaluate([1, 2], Patches, Data, client).Verdict);
    }

    [Fact]
    public void Without_quest_patches_nothing_is_new()
    {
        var report = DataFreshness.Evaluate([1, 2, 70001], QuestPatches.Empty, Data, "2026.10.28.0000.0000");

        Assert.Equal(FreshnessVerdict.NewerClient, report.Verdict);
        Assert.Equal(0, report.NewQuests);
        Assert.False(report.ShowStrip);
    }

    [Fact]
    public void Versions_compare_as_game_versions_not_text()
    {
        Assert.Equal(FreshnessVerdict.Current, DataFreshness.Compare(Data, " 2026.09.15\n"));
        Assert.Equal(FreshnessVerdict.NewerClient, DataFreshness.Compare("2026.12.31", "2027.1.2"));
        Assert.Null(DataFreshness.Compare(Data, "garbage"));
    }

    [Fact]
    public void A_dismissal_holds_for_its_client_version_only()
    {
        var report = DataFreshness.Evaluate([1, 70001], Patches, Data, "2026.10.28.0000.0000");

        Assert.True(DataFreshness.StripVisible(report, dismissedFor: null));
        Assert.True(DataFreshness.StripVisible(report, string.Empty));

        var key = DataFreshness.DismissKey(report);
        Assert.Equal("2026.10.28.0000.0000", key);
        Assert.False(DataFreshness.StripVisible(report, key));
        Assert.False(DataFreshness.StripVisible(report, "2026.10.28.0000.0000\r\n"));

        // The next update brings it back.
        var next = DataFreshness.Evaluate([1, 70001], Patches, Data, "2026.12.02.0000.0000");
        Assert.True(DataFreshness.StripVisible(next, key));
    }

    [Fact]
    public void A_dismissal_without_a_readable_client_version_is_stored_as_unknown()
    {
        var report = DataFreshness.Evaluate([1, 70001], Patches, Data, string.Empty);

        Assert.True(report.ShowStrip);
        Assert.Equal("unknown", DataFreshness.DismissKey(report));
        Assert.False(DataFreshness.StripVisible(report, DataFreshness.DismissKey(report)));
    }

    [Fact]
    public void Nothing_known_is_current_and_shows_nothing()
    {
        Assert.Equal(FreshnessVerdict.Current, DataFreshnessReport.None.Verdict);
        Assert.False(DataFreshness.StripVisible(DataFreshnessReport.None, null));
    }
}
