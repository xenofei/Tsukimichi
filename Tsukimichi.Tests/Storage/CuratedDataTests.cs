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
    public void Feature_quests_wrong_shape_is_warned_and_empty()
    {
        WriteCurated("feature_quests.json", """{ "a": 1 }""");

        var data = CuratedData.Load(tmp.File("curated"));

        Assert.Empty(data.FeatureQuests);
        Assert.Single(data.Warnings);
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
}
