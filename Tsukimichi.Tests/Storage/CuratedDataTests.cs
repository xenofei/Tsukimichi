using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class CuratedDataTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private void WriteCurated(string name, string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), json);
    }

    [Fact]
    public void Missing_directory_yields_empty_data_without_warnings()
    {
        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.SystemUnlocks);
        Assert.Empty(data.DutyUnlocks);
        Assert.Empty(data.FeatureQuests);
        Assert.Empty(data.Festivals);
        Assert.Empty(data.Chains);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void Chains_parse_number_and_string_genre_ids_in_order()
    {
        WriteCurated("chains.json",
            """
            {
              "$schema_note": "ignored",
              "chains": [
                { "name": "Hildibrand", "genreIds": [82, "83", 83, 84], "note": "across expansions" },
                { "name": " Omega ", "genreIds": ["22"] }
              ]
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Warnings);
        Assert.Equal(2, data.Chains.Count);
        Assert.Equal("Hildibrand", data.Chains[0].Name);
        Assert.Equal([82u, 83u, 84u], data.Chains[0].GenreIds);
        Assert.Equal("across expansions", data.Chains[0].Note);
        Assert.Equal("Omega", data.Chains[1].Name);
        Assert.Equal([22u], data.Chains[1].GenreIds);
        Assert.Null(data.Chains[1].Note);
    }

    [Fact]
    public void Chains_accept_a_bare_array_root()
    {
        WriteCurated("chains.json", """[ { "name": "Eden", "genreIds": [26] } ]""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Warnings);
        Assert.Equal("Eden", Assert.Single(data.Chains).Name);
    }

    [Fact]
    public void Chains_skip_bad_entries_with_a_warning_each()
    {
        WriteCurated("chains.json",
            """
            {
              "chains": [
                { "name": "Good", "genreIds": [1] },
                { "genreIds": [2] },
                { "name": "No ids", "genreIds": [] },
                { "name": "Not an array", "genreIds": 5 },
                { "name": "Zero", "genreIds": [0] },
                { "name": "Text", "genreIds": ["abc"] },
                42
              ]
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal("Good", Assert.Single(data.Chains).Name);
        Assert.Equal(6, data.Warnings.Count);
        Assert.All(data.Warnings, w => Assert.StartsWith("chains.json: chains[", w));
    }

    [Fact]
    public void Chains_with_a_wrong_root_are_ignored_with_one_warning()
    {
        WriteCurated("chains.json", """{ "name": "Eden" }""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Chains);
        var warning = Assert.Single(data.Warnings);
        Assert.Contains("chains", warning);
    }

    [Fact]
    public void Missing_individual_files_yield_empty_collections()
    {
        WriteCurated("feature_quests.json", "[66038]");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Single(data.FeatureQuests);
        Assert.Empty(data.SystemUnlocks);
        Assert.Empty(data.DutyUnlocks);
        Assert.Empty(data.Festivals);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void System_unlocks_parse_string_keys()
    {
        WriteCurated("system_unlocks.json",
            """
            {
              "66038": { "label": "Glamour Dresser", "kind": "system", "note": "Prism cabinet" },
              "65576": { "label": "Aesthetician" }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(2, data.SystemUnlocks.Count);
        Assert.Equal(new SystemUnlock("Glamour Dresser", "system", "Prism cabinet"), data.SystemUnlocks[66038]);
        Assert.Equal(new SystemUnlock("Aesthetician", "system", null), data.SystemUnlocks[65576]);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void System_unlocks_malformed_entries_are_skipped_with_warning()
    {
        WriteCurated("system_unlocks.json",
            """
            {
              "66038": { "label": "Good" },
              "notanumber": { "label": "Bad key" },
              "66039": { "kind": "system" },
              "66040": "not an object",
              "66041": { "label": "Also good", "kind": "feature" }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(2, data.SystemUnlocks.Count);
        Assert.Equal("feature", data.SystemUnlocks[66041].Kind);
        Assert.Equal(3, data.Warnings.Count);
        Assert.All(data.Warnings, w => Assert.Contains("system_unlocks.json", w));
    }

    [Fact]
    public void Duty_unlocks_accept_bare_array_and_object_forms()
    {
        WriteCurated("duty_unlocks.json",
            """
            {
              "66038": [ 4, 5 ],
              "66039": { "contentFinderConditionIds": [ 6 ], "note": "Hard mode" }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(2, data.DutyUnlocks.Count);
        Assert.Equal(new uint[] { 4, 5 }, data.DutyUnlocks[66038].ContentFinderConditionIds);
        Assert.Null(data.DutyUnlocks[66038].Note);
        Assert.Equal(new uint[] { 6 }, data.DutyUnlocks[66039].ContentFinderConditionIds);
        Assert.Equal("Hard mode", data.DutyUnlocks[66039].Note);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void Duty_unlocks_malformed_entries_are_skipped_with_warning()
    {
        WriteCurated("duty_unlocks.json",
            """
            {
              "66038": [ 4, "five" ],
              "66039": 7,
              "-1": [ 1 ],
              "66040": []
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Single(data.DutyUnlocks);
        Assert.Empty(data.DutyUnlocks[66040].ContentFinderConditionIds);
        Assert.Equal(3, data.Warnings.Count);
    }

    [Fact]
    public void Feature_quests_parse_numbers_and_skip_junk()
    {
        WriteCurated("feature_quests.json", """[ 66038, 66039, "66040", "junk", -5, 66038 ]""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(new HashSet<uint> { 66038, 66039, 66040 }, data.FeatureQuests);
        Assert.Equal(2, data.Warnings.Count);
    }

    [Fact]
    public void Feature_quests_accept_a_bare_array()
    {
        WriteCurated("feature_quests.json", "[ 66038, 66039 ]");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(new HashSet<uint> { 66038, 66039 }, data.FeatureQuests);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void Feature_quests_accept_an_object_with_questRowIds_and_ignore_its_note()
    {
        WriteCurated("feature_quests.json", """{ "questRowIds": [ 66038, "66039" ], "note": "union of the unlock files" }""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(new HashSet<uint> { 66038, 66039 }, data.FeatureQuests);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void Feature_quests_wrong_shape_is_warned_and_empty()
    {
        WriteCurated("feature_quests.json", """{ "a": 1 }""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.FeatureQuests);
        var warning = Assert.Single(data.Warnings);
        Assert.Contains("questRowIds", warning);
    }

    [Fact]
    public void Festivals_accept_an_entries_wrapper_and_ignore_dollar_keys()
    {
        WriteCurated("festivals.json",
            """
            {
              "$schema_note": "festivalId -> { name, start, end, mogStation }",
              "entries": { "1": { "name": "Starlight Celebration" }, "$comment": "ignored" }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Warnings);
        Assert.Equal("Starlight Celebration", Assert.Single(data.Festivals).Value.Name);
    }

    [Fact]
    public void Online_store_entries_parse_with_kind_reward_id_and_evidence()
    {
        WriteCurated("online_store.json",
            """
            {
              "schema": 1,
              "note": "store re-sells",
              "entries": {
                "22437": { "name": "Starlight bear", "kind": "Mount", "rewardId": 99, "evidence": "https://ffxivcollect.com/api/mounts/99", "note": "Starlight 2017" },
                "12040": { "name": "Bomb Dance", "kind": "Emote", "rewardId": "109", "evidence": "https://ffxivcollect.com/api/emotes/109" }
              }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Warnings);
        Assert.Equal(2, data.OnlineStore.Count);
        Assert.Equal(new OnlineStoreItem("Starlight bear", RewardKind.Mount, 99, "https://ffxivcollect.com/api/mounts/99", "Starlight 2017"), data.OnlineStore[22437]);
        Assert.Equal(new OnlineStoreItem("Bomb Dance", RewardKind.Emote, 109, "https://ffxivcollect.com/api/emotes/109", null), data.OnlineStore[12040]);
    }

    [Fact]
    public void Online_store_malformed_entries_are_skipped_with_warning()
    {
        WriteCurated("online_store.json",
            """
            {
              "schema": 1,
              "entries": {
                "1": { "name": "Good", "kind": "Minion", "rewardId": 5, "evidence": "https://x" },
                "abc": { "name": "Bad key", "kind": "Minion", "rewardId": 5, "evidence": "https://x" },
                "2": { "kind": "Minion", "rewardId": 5, "evidence": "https://x" },
                "3": { "name": "Bad kind", "kind": "Pet", "rewardId": 5, "evidence": "https://x" },
                "4": { "name": "No reward", "kind": "Minion", "rewardId": 0, "evidence": "https://x" },
                "5": { "name": "No evidence", "kind": "Minion", "rewardId": 5 },
                "6": "not an object"
              }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal("Good", Assert.Single(data.OnlineStore).Value.Name);
        Assert.Equal(6, data.Warnings.Count);
        Assert.All(data.Warnings, w => Assert.StartsWith("online_store.json", w));
    }

    [Fact]
    public void Other_sources_parse_source_where_note_and_evidence_by_item_id()
    {
        WriteCurated("other_sources.json",
            """
            {
              "schema": 1,
              "note": "dungeon drops",
              "entries": {
                "4520": { "name": "Darklight Band of Striking", "source": "DungeonDrop", "where": "The Lost City of Amdapor", "evidence": "https://na.finalfantasyxiv.com/lodestone/playguide/db/item/1a08ceb6404/", "note": "quest 66711" },
                "$comment": { "note": "skipped silently" }
              }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Warnings);
        var (itemId, item) = Assert.Single(data.OtherSources);
        Assert.Equal(4520u, itemId);
        Assert.Equal(new OtherSourceItem("Darklight Band of Striking", OtherSource.DungeonDrop, "The Lost City of Amdapor", "https://na.finalfantasyxiv.com/lodestone/playguide/db/item/1a08ceb6404/", "quest 66711"), item);
    }

    [Fact]
    public void Other_sources_require_a_known_source_where_note_and_evidence()
    {
        WriteCurated("other_sources.json",
            """
            {
              "schema": 1,
              "entries": {
                "1": { "name": "Good", "source": "DungeonDrop", "where": "Snowcloak", "evidence": "https://x", "note": "n" },
                "abc": { "name": "Bad key", "source": "DungeonDrop", "where": "Snowcloak", "evidence": "https://x", "note": "n" },
                "0": { "name": "Zero", "source": "DungeonDrop", "where": "Snowcloak", "evidence": "https://x", "note": "n" },
                "2": { "source": "DungeonDrop", "where": "Snowcloak", "evidence": "https://x", "note": "n" },
                "3": { "name": "Store", "source": "OnlineStore", "where": "Mog Station", "evidence": "https://x", "note": "n" },
                "4": { "name": "No where", "source": "DungeonDrop", "evidence": "https://x", "note": "n" },
                "5": { "name": "No evidence", "source": "DungeonDrop", "where": "Snowcloak", "note": "n" },
                "6": { "name": "No note", "source": "DungeonDrop", "where": "Snowcloak", "evidence": "https://x" },
                "7": "not an object"
              }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal([1u], data.OtherSources.Keys);
        Assert.Equal(8, data.Warnings.Count);
        Assert.All(data.Warnings, w => Assert.StartsWith("other_sources.json", w));
    }

    [Fact]
    public void Curated_files_are_parsed_strictly_so_comments_and_trailing_commas_are_errors()
    {
        WriteCurated("feature_quests.json", "[ 66038, 66039, ]");
        WriteCurated("system_unlocks.json",
            """
            {
              // not allowed by the README
              "66038": { "label": "Glamour Dresser" }
            }
            """);
        WriteCurated("duty_unlocks.json", """{ "66038": [ 4 ] }""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.FeatureQuests);
        Assert.Empty(data.SystemUnlocks);
        Assert.Single(data.DutyUnlocks);
        Assert.Equal(2, data.Warnings.Count);
        Assert.Contains(data.Warnings, w => w.StartsWith("feature_quests.json could not be parsed", StringComparison.Ordinal));
        Assert.Contains(data.Warnings, w => w.StartsWith("system_unlocks.json could not be parsed", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Curated")]
    public void Shipped_curated_files_load_without_warnings()
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tsukimichi", "Data", "curated"));
        Assert.True(Directory.Exists(dir), dir);

        var data = CuratedData.Load(dir);

        Assert.Empty(data.Warnings);
        Assert.NotEmpty(data.FeatureQuests);
        Assert.NotEmpty(data.SystemUnlocks);
        Assert.NotEmpty(data.DutyUnlocks);
        Assert.NotEmpty(data.Chains);
        Assert.NotEmpty(data.OnlineStore);
        Assert.NotEmpty(data.OtherSources);
        Assert.Empty(data.Festivals);
    }

    [Fact]
    public void Quirks_parse_note_and_evidence_by_row_id()
    {
        WriteCurated("quirks.json",
            """
            {
              "schema": 1,
              "note": "file note, ignored",
              "entries": {
                "66971": { "note": "Optional once the Zenith is in hand.", "evidence": "https://forum.square-enix.com/ffxiv/threads/525622" },
                "$comment": { "note": "skipped silently" }
              }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        var (rowId, quirk) = Assert.Single(data.Quirks);
        Assert.Equal(66971u, rowId);
        Assert.Equal(new QuestQuirk("Optional once the Zenith is in hand.", "https://forum.square-enix.com/ffxiv/threads/525622"), quirk);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void Quirks_require_note_and_evidence_and_a_row_id_key()
    {
        WriteCurated("quirks.json",
            """
            {
              "schema": 1,
              "entries": {
                "66971": { "note": "good", "evidence": "https://example.test/" },
                "66972": { "note": "no evidence" },
                "66973": { "evidence": "https://example.test/no-note" },
                "66974": "not an object",
                "0": { "note": "zero", "evidence": "https://example.test/" },
                "Up in Arms": { "note": "keyed by name", "evidence": "https://example.test/" }
              }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal([66971u], data.Quirks.Keys);
        Assert.Equal(5, data.Warnings.Count);
        Assert.All(data.Warnings, w => Assert.Contains("quirks.json", w));
    }

    [Fact]
    public void Quirks_file_with_a_trailing_comma_is_rejected_whole()
    {
        WriteCurated("quirks.json", """{ "entries": { "66971": { "note": "n", "evidence": "https://e/" }, } }""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.Quirks);
        Assert.Contains(data.Warnings, w => w.Contains("quirks.json could not be parsed", StringComparison.Ordinal));
    }

    [Fact]
    public void Festivals_parse_optional_dates_and_mog_station()
    {
        WriteCurated("festivals.json",
            """
            {
              "1": { "name": "Starlight Celebration", "start": "2025-12-15T08:00:00Z", "end": "2025-12-31T14:59:59Z", "mogStation": false },
              "2": { "name": "Heavensturn" },
              "3": { "name": "Moogle Treasure Trove", "mogStation": true, "start": null }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Equal(3, data.Festivals.Count);
        var starlight = data.Festivals[1];
        Assert.Equal("Starlight Celebration", starlight.Name);
        Assert.Equal(new DateTime(2025, 12, 15, 8, 0, 0, DateTimeKind.Utc), starlight.Start);
        Assert.Equal(DateTimeKind.Utc, starlight.Start!.Value.Kind);
        Assert.Equal(new DateTime(2025, 12, 31, 14, 59, 59, DateTimeKind.Utc), starlight.End);
        Assert.False(starlight.MogStation);
        Assert.Equal(new FestivalInfo("Heavensturn", null, null, false), data.Festivals[2]);
        Assert.True(data.Festivals[3].MogStation);
        Assert.Null(data.Festivals[3].Start);
        Assert.Empty(data.Warnings);
    }

    [Fact]
    public void Festivals_malformed_entries_are_skipped_with_warning()
    {
        WriteCurated("festivals.json",
            """
            {
              "1": { "name": "Good" },
              "2": { "start": "2025-01-01T00:00:00Z" },
              "3": { "name": "Bad date", "start": "yesterday" },
              "70000": { "name": "Key too large for ushort" }
            }
            """);

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Single(data.Festivals);
        Assert.Equal(3, data.Warnings.Count);
    }

    [Fact]
    public void Locked_file_is_warned_and_others_still_load()
    {
        WriteCurated("system_unlocks.json", """{ "66038": { "label": "Glamour Dresser" } }""");
        WriteCurated("feature_quests.json", "[1]");
        var lockedPath = tmp.File(Path.Combine("curated", "system_unlocks.json"));

        using (File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var data = CuratedData.Load(tmp.File("curated"));

            Assert.Empty(data.SystemUnlocks);
            Assert.Single(data.FeatureQuests);
            Assert.Single(data.Warnings);
            Assert.Contains("system_unlocks.json", data.Warnings[0]);
        }

        Assert.Single(CuratedData.Load(tmp.File("curated")).SystemUnlocks);
    }

    [Fact]
    public void Unparseable_file_is_warned_and_others_still_load()
    {
        WriteCurated("system_unlocks.json", "{ broken");
        WriteCurated("feature_quests.json", "[1]");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.SystemUnlocks);
        Assert.Single(data.FeatureQuests);
        Assert.Single(data.Warnings);
        Assert.Contains("system_unlocks.json", data.Warnings[0]);
        Assert.True(File.Exists(tmp.File(Path.Combine("curated", "system_unlocks.json"))));
    }

    [Fact]
    public void Version_file_gives_the_curated_revision_and_is_optional()
    {
        WriteCurated("feature_quests.json", "[1]");
        Assert.Equal(string.Empty, CuratedData.Load(tmp.File("curated")).CuratedRevision);
        Assert.Empty(CuratedData.Load(tmp.File("curated")).Warnings);

        WriteCurated(CuratedData.VersionFileName, """{ "$schema_note": "written by regen", "curatedRevision": " 573d225-dirty " }""");
        var data = CuratedData.Load(tmp.File("curated"));
        Assert.Equal("573d225-dirty", data.CuratedRevision);
        Assert.Empty(data.Warnings);
        Assert.Equal("573d225-dirty", data.WithoutFeatureQuests().CuratedRevision);
    }

    [Fact]
    public void Version_file_without_a_revision_is_warned_and_reads_empty()
    {
        WriteCurated(CuratedData.VersionFileName, """{ "note": "no revision" }""");
        var missing = CuratedData.Load(tmp.File("curated"));
        Assert.Equal(string.Empty, missing.CuratedRevision);
        Assert.Single(missing.Warnings);
        Assert.Contains(CuratedData.CuratedRevisionKey, missing.Warnings[0]);

        WriteCurated(CuratedData.VersionFileName, "[1]");
        var array = CuratedData.Load(tmp.File("curated"));
        Assert.Equal(string.Empty, array.CuratedRevision);
        Assert.Single(array.Warnings);
        Assert.Contains(CuratedData.VersionFileName, array.Warnings[0]);
    }
}
