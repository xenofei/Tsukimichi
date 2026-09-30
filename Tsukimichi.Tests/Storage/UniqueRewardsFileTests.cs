using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class UniqueRewardsFileTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static UniqueRewardsData Sample() => new(
        "2026.09.20.0000.0000",
        new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc),
        [
            new UniqueRewardEntry(66038, RewardKind.Emote, 114, 0, "Most Gentlemanly", Confidence.Static, "Quest.EmoteReward"),
            new UniqueRewardEntry(65576, RewardKind.Mount, 71, 15939, "Company Chocobo", Confidence.Curated, "curated/mounts.json")
            {
                OtherSources = [OtherSource.OnlineStore, OtherSource.Tradable],
            },
        ]);

    [Fact]
    public void Entries_compare_by_value_including_other_sources()
    {
        var plain = new UniqueRewardEntry(1, RewardKind.Mount, 2, 3, "n", Confidence.Static, "s");
        var store = plain with { OtherSources = [OtherSource.OnlineStore] };

        Assert.Equal(plain, new UniqueRewardEntry(1, RewardKind.Mount, 2, 3, "n", Confidence.Static, "s"));
        Assert.Equal(store, plain with { OtherSources = new List<string> { OtherSource.OnlineStore } });
        Assert.NotEqual(plain, store);
        Assert.Equal(plain.GetHashCode(), new UniqueRewardEntry(1, RewardKind.Mount, 2, 3, "n", Confidence.Static, "s").GetHashCode());
        Assert.Empty(plain.OtherSources);
        Assert.True(store.SoldOnOnlineStore);
        Assert.False(plain.SoldOnOnlineStore);
        Assert.Same(store, store.WithOtherSource(OtherSource.OnlineStore));
        Assert.Equal([OtherSource.OnlineStore, OtherSource.Tradable], store.WithOtherSource(OtherSource.Tradable).OtherSources);
    }

    [Fact]
    public void Load_reads_other_sources_and_treats_a_missing_or_null_field_as_empty()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path,
            """
            { "gameVersion": "x", "generatedUtc": "2026-09-27T08:00:00Z", "entries": [
              { "questRowId": 68546, "kind": "Mount", "rewardId": 99, "itemId": 22437, "rewardName": "Starlight bear", "confidence": "Static", "source": "s", "otherSources": ["OnlineStore"] },
              { "questRowId": 66038, "kind": "Emote", "rewardId": 114, "itemId": 0, "rewardName": "A", "confidence": "Static", "source": "s" },
              { "questRowId": 66039, "kind": "Emote", "rewardId": 115, "itemId": 0, "rewardName": "B", "confidence": "Static", "source": "s", "otherSources": null },
              { "questRowId": 66040, "kind": "Emote", "rewardId": 116, "itemId": 0, "rewardName": "C", "confidence": "Static", "source": "s", "otherSources": ["SpecialShop", null, " "] }
            ] }
            """);

        var loaded = UniqueRewardsFile.Load(path);

        Assert.Empty(loaded.Warnings);
        Assert.Equal(4, loaded.Entries.Count);
        Assert.Equal(["OnlineStore"], loaded.Entries[0].OtherSources);
        Assert.True(loaded.Entries[0].SoldOnOnlineStore);
        Assert.Empty(loaded.Entries[1].OtherSources);
        Assert.Empty(loaded.Entries[2].OtherSources);
        Assert.Equal(["SpecialShop"], loaded.Entries[3].OtherSources);
    }

    [Fact]
    public void Write_then_Load_round_trips()
    {
        var path = tmp.File("unique_quests.json");
        var data = Sample();

        UniqueRewardsFile.Write(path, data);
        var loaded = UniqueRewardsFile.Load(path);

        Assert.Equal(data.GameVersion, loaded.GameVersion);
        Assert.Equal(data.GeneratedUtc, loaded.GeneratedUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.GeneratedUtc.Kind);
        Assert.Equal(data.Entries, loaded.Entries);
        Assert.Empty(loaded.Warnings);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Load_title_cases_mount_minion_ornament_and_job_names()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path, """
            {
              "gameVersion": "x", "generatedUtc": "2026-09-27T08:00:00Z",
              "entries": [
                { "questRowId": 70058, "kind": "Mount", "rewardId": 6, "itemId": 6008, "rewardName": "magitek armor", "confidence": "Static", "source": "s" },
                { "questRowId": 66038, "kind": "Minion", "rewardId": 1, "itemId": 0, "rewardName": "wind-up cid", "confidence": "Static", "source": "s" },
                { "questRowId": 66039, "kind": "Ornament", "rewardId": 2, "itemId": 0, "rewardName": "chair of the eminent", "confidence": "Static", "source": "s" },
                { "questRowId": 65557, "kind": "ClassJob", "rewardId": 5, "itemId": 0, "rewardName": "archer", "confidence": "Static", "source": "s" },
                { "questRowId": 65558, "kind": "Mount", "rewardId": 7, "itemId": 0, "rewardName": "CHL P-0005", "confidence": "Static", "source": "s" },
                { "questRowId": 65559, "kind": "Item", "rewardId": 8, "itemId": 8, "rewardName": "lowercase item", "confidence": "Static", "source": "s" }
              ]
            }
            """);

        var loaded = UniqueRewardsFile.Load(path);

        Assert.Empty(loaded.Warnings);
        Assert.Equal(
            ["Magitek Armor", "Wind-up Cid", "Chair of the Eminent", "Archer", "CHL P-0005", "lowercase item"],
            loaded.Entries.Select(e => e.RewardName));
    }

    [Fact]
    public void Write_serializes_enums_as_strings_with_spec_field_names()
    {
        var path = tmp.File("unique_quests.json");
        UniqueRewardsFile.Write(path, Sample());

        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        Assert.Equal("2026.09.20.0000.0000", (string?)json["gameVersion"]);
        Assert.Equal("2026-09-27T08:00:00Z", (string?)json["generatedUtc"]);
        Assert.Equal(3, json.Count);
        var entry = json["entries"]![0]!.AsObject();
        Assert.Equal(66038, (int)entry["questRowId"]!);
        Assert.Equal("Emote", (string?)entry["kind"]);
        Assert.Equal(114, (int)entry["rewardId"]!);
        Assert.Equal(0, (int)entry["itemId"]!);
        Assert.Equal("Most Gentlemanly", (string?)entry["rewardName"]);
        Assert.Equal("Static", (string?)entry["confidence"]);
        Assert.Equal("Quest.EmoteReward", (string?)entry["source"]);
        Assert.Equal("[]", entry["otherSources"]!.ToJsonString());
        Assert.Equal(8, entry.Count);
        var store = json["entries"]![1]!.AsObject();
        Assert.Equal("""["OnlineStore","Tradable"]""", store["otherSources"]!.ToJsonString());
        Assert.Null(store["soldOnOnlineStore"]);
    }

    [Fact]
    public void Load_parses_the_spec_example_verbatim()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path,
            """
            { "gameVersion": "2026.09.20", "generatedUtc": "2026-09-27T00:00:00Z", "entries": [
              { "questRowId": 66038, "kind": "Emote", "rewardId": 114, "itemId": 0, "rewardName": "Most Gentlemanly", "confidence": "Static", "source": "Quest.EmoteReward" }
            ] }
            """);

        var loaded = UniqueRewardsFile.Load(path);

        Assert.Single(loaded.Entries);
        Assert.Equal(new UniqueRewardEntry(66038, RewardKind.Emote, 114, 0, "Most Gentlemanly", Confidence.Static, "Quest.EmoteReward"), loaded.Entries[0]);
    }

    [Fact]
    public void Missing_file_returns_empty_with_warning()
    {
        var loaded = UniqueRewardsFile.Load(tmp.File("nope.json"));

        Assert.Empty(loaded.Entries);
        Assert.Equal(string.Empty, loaded.GameVersion);
        Assert.Single(loaded.Warnings);
    }

    [Fact]
    public void Malformed_entries_are_skipped_with_warning_and_good_ones_kept()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path,
            """
            { "gameVersion": "x", "generatedUtc": "2026-01-01T00:00:00Z", "entries": [
              { "questRowId": 66038, "kind": "Emote", "rewardId": 114, "itemId": 0, "rewardName": "A", "confidence": "Static", "source": "s" },
              { "questRowId": 66039, "kind": "NotAKind", "rewardId": 1, "itemId": 0, "rewardName": "B", "confidence": "Static", "source": "s" },
              "not an object",
              { "questRowId": "abc" },
              { "questRowId": 66040, "kind": "Mount", "rewardId": 2, "itemId": 3, "rewardName": "C", "confidence": "Curated", "source": "s" }
            ] }
            """);

        var loaded = UniqueRewardsFile.Load(path);

        Assert.Equal(2, loaded.Entries.Count);
        Assert.Equal(66038u, loaded.Entries[0].QuestRowId);
        Assert.Equal(66040u, loaded.Entries[1].QuestRowId);
        Assert.Equal(3, loaded.Warnings.Count);
    }

    [Fact]
    public void Locked_file_returns_empty_with_warning_and_is_not_moved()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path, """{ "gameVersion": "1", "generatedUtc": "2026-09-27T08:00:00Z", "entries": [] }""");

        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var loaded = UniqueRewardsFile.Load(path);

            Assert.Empty(loaded.Entries);
            Assert.Single(loaded.Warnings);
            Assert.Contains("unique_quests.json", loaded.Warnings[0]);
        }

        Assert.True(File.Exists(path));
        Assert.Empty(UniqueRewardsFile.Load(path).Warnings);
    }

    [Fact]
    public void Corrupt_file_returns_empty_with_warning_and_is_not_moved()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path, "nope");

        var loaded = UniqueRewardsFile.Load(path);

        Assert.Empty(loaded.Entries);
        Assert.Single(loaded.Warnings);
        Assert.True(File.Exists(path), "shipped files are read-only and must never be renamed");
    }

    [Fact]
    public void Missing_header_fields_default_without_dropping_entries()
    {
        var path = tmp.File("unique_quests.json");
        File.WriteAllText(path,
            """{ "entries": [ { "questRowId": 1, "kind": "Item", "rewardId": 2, "itemId": 2, "rewardName": "n", "confidence": "Community", "source": "s" } ] }""");

        var loaded = UniqueRewardsFile.Load(path);

        Assert.Single(loaded.Entries);
        Assert.Equal(string.Empty, loaded.GameVersion);
        Assert.Equal(default, loaded.GeneratedUtc);
    }
}
