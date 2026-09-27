using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class JsonSnapshotStoreTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static CharacterSnapshot FullSnapshot() => new()
    {
        ContentId = 0x0040_0000_0123_4567UL,
        Name = "Michiru Tsukikage",
        World = 74,
        TakenUtc = new DateTime(2026, 9, 27, 12, 30, 45, DateTimeKind.Utc),
        CompletedBits = [0b0000_0100, 0xFF, 0x00, 0x81],
        Accepted = [new AcceptedQuest(40, 3), new AcceptedQuest(60000, 255)],
        DailyDone = new Dictionary<ushort, byte> { [1234] = 1, [60000] = 2 },
        JobLevels = new Dictionary<byte, short> { [1] = 90, [19] = 100, [41] = 7 },
        GrandCompany = 2,
        GcRanks = [0, 5, 12, 3],
        Tribes = new Dictionary<byte, TribeStanding> { [3] = new TribeStanding(4, 250), [9] = new TribeStanding(8, 0) },
        TribeAllowance = 12,
        LeveAllowance = 99,
        UnlockedInstances = [1, 2, 3, 900],
        ActiveFestivals = [7, 42],
        MaxExpansion = 5,
        LevelCap = 100,
        AchievementsLoaded = true,
        CompletedAchievements = [10, 20, 3000],
        CurrentJob = 41,
    };

    private string CharactersDir()
    {
        var dir = tmp.File("characters");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Save_then_Load_round_trips_every_field()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var original = FullSnapshot();

        store.Save(original);
        var loaded = store.Load(original.ContentId);

        Assert.NotNull(loaded);
        Assert.Equal(original.ContentId, loaded.ContentId);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.World, loaded.World);
        Assert.Equal(original.TakenUtc, loaded.TakenUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.TakenUtc.Kind);
        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(original.CompletedBits, loaded.CompletedBits);
        Assert.Equal(original.Accepted, loaded.Accepted);
        Assert.Equal(original.DailyDone, loaded.DailyDone);
        Assert.Equal(original.JobLevels, loaded.JobLevels);
        Assert.Equal(original.GrandCompany, loaded.GrandCompany);
        Assert.Equal(original.GcRanks, loaded.GcRanks);
        Assert.Equal(original.Tribes, loaded.Tribes);
        Assert.Equal(original.TribeAllowance, loaded.TribeAllowance);
        Assert.Equal(original.LeveAllowance, loaded.LeveAllowance);
        Assert.Equal(original.UnlockedInstances, loaded.UnlockedInstances);
        Assert.Equal(original.ActiveFestivals, loaded.ActiveFestivals);
        Assert.Equal(original.MaxExpansion, loaded.MaxExpansion);
        Assert.Equal(original.LevelCap, loaded.LevelCap);
        Assert.Equal(original.AchievementsLoaded, loaded.AchievementsLoaded);
        Assert.Equal(original.CompletedAchievements, loaded.CompletedAchievements);
        Assert.Equal(original.CurrentJob, loaded.CurrentJob);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void Save_writes_characters_contentid_json_with_no_tmp_left()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var snapshot = FullSnapshot();

        store.Save(snapshot);

        var expected = tmp.File(Path.Combine("characters", snapshot.ContentId + ".json"));
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(expected + ".tmp"));
        var json = JsonNode.Parse(File.ReadAllText(expected))!.AsObject();
        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, (int)json["schemaVersion"]!);
        Assert.Equal(snapshot.ContentId, (ulong)json["contentId"]!);
    }

    [Fact]
    public void Load_returns_null_for_unknown_character_without_warning()
    {
        var store = new JsonSnapshotStore(tmp.Path);

        Assert.Null(store.Load(12345));
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void Corrupt_file_is_quarantined_and_warning_recorded()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var dir = CharactersDir();
        var path = Path.Combine(dir, "77.json");
        File.WriteAllText(path, "{ this is not json");

        var loaded = store.Load(77);

        Assert.Null(loaded);
        Assert.False(File.Exists(path));
        var quarantined = Directory.GetFiles(dir, "77.json.corrupt-*");
        Assert.Single(quarantined);
        Assert.Matches(@"77\.json\.corrupt-\d{14}$", quarantined[0]);
        Assert.Single(store.Warnings);
        Assert.Contains("77.json", store.Warnings[0]);
    }

    [Fact]
    public void Wrong_json_shape_is_treated_as_corrupt()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var dir = CharactersDir();
        File.WriteAllText(Path.Combine(dir, "5.json"), "[1,2,3]");

        Assert.Null(store.Load(5));
        Assert.Single(Directory.GetFiles(dir, "5.json.corrupt-*"));
        Assert.Single(store.Warnings);
    }

    [Fact]
    public void Migrator_is_invoked_for_old_schema_version()
    {
        var migrator = new SnapshotMigrator();
        var invoked = 0;
        migrator.Register(0, node =>
        {
            invoked++;
            var obj = node.AsObject();
            // Pretend a v0 file stored the character name under "characterName".
            obj["name"] = (string?)obj["characterName"];
            obj.Remove("characterName");
            return obj;
        });
        var store = new JsonSnapshotStore(tmp.Path, migrator);
        var dir = CharactersDir();
        File.WriteAllText(Path.Combine(dir, "9.json"),
            """{ "schemaVersion": 0, "contentId": 9, "characterName": "Old Name", "world": 1, "completedBits": "AQ==" }""");

        var loaded = store.Load(9);

        Assert.Equal(1, invoked);
        Assert.NotNull(loaded);
        Assert.Equal("Old Name", loaded.Name);
        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(new byte[] { 1 }, loaded.CompletedBits);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void Migrator_is_not_invoked_for_current_version()
    {
        var migrator = new SnapshotMigrator();
        var invoked = false;
        migrator.Register(0, node => { invoked = true; return node; });
        var store = new JsonSnapshotStore(tmp.Path, migrator);
        store.Save(FullSnapshot());

        store.Load(FullSnapshot().ContentId);

        Assert.False(invoked);
    }

    [Fact]
    public void Missing_migration_step_quarantines_file()
    {
        var store = new JsonSnapshotStore(tmp.Path, new SnapshotMigrator());
        var dir = CharactersDir();
        File.WriteAllText(Path.Combine(dir, "3.json"), """{ "schemaVersion": 0, "contentId": 3, "name": "x" }""");

        Assert.Null(store.Load(3));
        Assert.Single(Directory.GetFiles(dir, "3.json.corrupt-*"));
        Assert.Single(store.Warnings);
    }

    [Fact]
    public void Unknown_properties_are_ignored_on_read()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var dir = CharactersDir();
        File.WriteAllText(Path.Combine(dir, "11.json"),
            """
            {
              "schemaVersion": 1,
              "contentId": 11,
              "name": "Future Proof",
              "world": 55,
              "somethingNew": { "nested": [1, 2, 3] },
              "completedBits": "gA==",
              "dailyDone": { "65535": 1 }
            }
            """);

        var loaded = store.Load(11);

        Assert.NotNull(loaded);
        Assert.Equal("Future Proof", loaded.Name);
        Assert.Equal(55u, loaded.World);
        Assert.Equal(new byte[] { 0x80 }, loaded.CompletedBits);
        Assert.Equal((byte)1, loaded.DailyDone[65535]);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void Unknown_quest_ids_inside_known_collections_are_preserved()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var snapshot = new CharacterSnapshot
        {
            ContentId = 20,
            DailyDone = new Dictionary<ushort, byte> { [65000] = 3 },
            Accepted = [new AcceptedQuest(64999, 1)],
            UnlockedInstances = [4_000_000_000],
        };

        store.Save(snapshot);
        var loaded = store.Load(20)!;

        Assert.Equal((byte)3, loaded.DailyDone[65000]);
        Assert.Equal(new AcceptedQuest(64999, 1), loaded.Accepted[0]);
        Assert.Equal(4_000_000_000u, loaded.UnlockedInstances[0]);
    }

    [Fact]
    public void List_returns_summaries_with_completed_count_from_bitmask()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(FullSnapshot());
        store.Save(new CharacterSnapshot { ContentId = 2, Name = "Second", World = 3, CompletedBits = [] });

        var list = store.List();

        Assert.Equal(2, list.Count);
        var first = list.Single(s => s.ContentId == FullSnapshot().ContentId);
        Assert.Equal("Michiru Tsukikage", first.Name);
        Assert.Equal(74u, first.World);
        Assert.Equal(FullSnapshot().TakenUtc, first.TakenUtc);
        Assert.Equal(1 + 8 + 0 + 2, first.CompletedCount);
        var second = list.Single(s => s.ContentId == 2);
        Assert.Equal(0, second.CompletedCount);
    }

    [Fact]
    public void List_is_empty_when_directory_does_not_exist()
    {
        var store = new JsonSnapshotStore(tmp.File("nope"));

        Assert.Empty(store.List());
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void List_skips_and_quarantines_corrupt_files()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(new CharacterSnapshot { ContentId = 1, Name = "Good" });
        var dir = tmp.File("characters");
        File.WriteAllText(Path.Combine(dir, "2.json"), "garbage");

        var list = store.List();

        Assert.Single(list);
        Assert.Equal("Good", list[0].Name);
        Assert.Single(store.Warnings);
        Assert.Single(Directory.GetFiles(dir, "2.json.corrupt-*"));
    }

    [Fact]
    public void List_skips_a_locked_file_with_a_warning_and_leaves_it_in_place()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(new CharacterSnapshot { ContentId = 1, Name = "Good" });
        store.Save(new CharacterSnapshot { ContentId = 2, Name = "Locked" });
        var lockedPath = tmp.File(Path.Combine("characters", "2.json"));

        using (File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var list = store.List();

            Assert.Single(list);
            Assert.Equal("Good", list[0].Name);
            Assert.Single(store.Warnings);
            Assert.Contains("2.json", store.Warnings[0]);
        }

        Assert.True(File.Exists(lockedPath), "a locked file is not corrupt and must not be quarantined");
        Assert.Empty(Directory.GetFiles(tmp.File("characters"), "2.json.corrupt-*"));
    }

    [Fact]
    public void Load_returns_null_for_a_locked_file_with_a_warning()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(new CharacterSnapshot { ContentId = 3, Name = "Locked" });
        var lockedPath = tmp.File(Path.Combine("characters", "3.json"));

        using (File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(store.Load(3));
            Assert.Single(store.Warnings);
        }

        store.ClearWarnings();
        Assert.Equal("Locked", store.Load(3)!.Name);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void List_ignores_files_that_are_not_named_by_content_id()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(new CharacterSnapshot { ContentId = 1, Name = "Good" });
        var dir = tmp.File("characters");
        File.WriteAllText(Path.Combine(dir, "notes.json"), "{}");
        File.WriteAllText(Path.Combine(dir, "1.json.corrupt-20260101000000"), "garbage");

        var list = store.List();

        Assert.Single(list);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void Delete_removes_file_and_tolerates_missing()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(new CharacterSnapshot { ContentId = 8 });

        store.Delete(8);
        store.Delete(8);

        Assert.Null(store.Load(8));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Save_overwrites_existing_snapshot()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(new CharacterSnapshot { ContentId = 4, Name = "Before" });

        store.Save(new CharacterSnapshot { ContentId = 4, Name = "After" });

        Assert.Equal("After", store.Load(4)!.Name);
        Assert.Single(store.List());
    }

    [Fact]
    public void ClearWarnings_empties_the_list()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        var dir = CharactersDir();
        File.WriteAllText(Path.Combine(dir, "1.json"), "x");
        store.Load(1);
        Assert.Single(store.Warnings);

        store.ClearWarnings();

        Assert.Empty(store.Warnings);
    }
}
