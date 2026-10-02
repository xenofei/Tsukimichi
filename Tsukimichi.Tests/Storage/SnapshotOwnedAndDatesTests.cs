using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// The fields 1.5 added at schema v1 (decision 9): the owned collectibles and the quest completion dates survive a save
/// and load, are not written while empty, read as "not captured" from an older file, and a kind name this build does
/// not know (a newer client's) is skipped instead of failing the file.
/// </summary>
public sealed class SnapshotOwnedAndDatesTests
{
    private static readonly DateTime Since = new(2026, 9, 12, 19, 2, 11, DateTimeKind.Utc);
    private static readonly DateTime Seen = new(2026, 9, 20, 21, 14, 5, DateTimeKind.Utc);

    private static CharacterSnapshot WithEverything() => Fixture.Snapshot(Fixture.A, Fixture.B) with
    {
        TakenUtc = Seen,
        Collectibles = new Dictionary<string, CollectibleSet>
        {
            ["Mount"] = new() { Owned = [15, 71], Missing = [3] },
            ["Barding"] = new() { Owned = [], Missing = [24, 30] },
        },
        CompletionDatesSinceUtc = Since,
        CompletedUtc = new Dictionary<ushort, DateTime> { [QuestRecord.ToQuestId(Fixture.B)] = Seen },
        CompletedAfterUtc = new Dictionary<ushort, DateTime> { [QuestRecord.ToQuestId(Fixture.B)] = Since },
    };

    [Fact]
    public void Save_then_Load_round_trips_owned_collectibles_and_completion_dates()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(WithEverything());
        var loaded = new JsonSnapshotStore(root).Load(1);

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Equal([15u, 71u], loaded.Collectibles["Mount"].Owned);
        Assert.Equal([3u], loaded.Collectibles["Mount"].Missing);
        Assert.Empty(loaded.Collectibles["Barding"].Owned);
        Assert.Equal(Since, loaded.CompletionDatesSinceUtc);
        Assert.Equal(Seen, loaded.CompletedUtc[QuestRecord.ToQuestId(Fixture.B)]);
        Assert.Equal(Since, loaded.CompletedAfterUtc[QuestRecord.ToQuestId(Fixture.B)]);

        var lookup = CollectibleLookup.For(loaded)!;
        Assert.True(lookup.Owns(RewardKind.Mount, 71));
        Assert.False(lookup.Owns(RewardKind.Mount, 3));
        Assert.False(lookup.Owns(RewardKind.Barding, 30));
        Assert.Null(lookup.Owns(RewardKind.Mount, 999));
        Assert.Null(lookup.Owns(RewardKind.Minion, 15));

        // The kinds are written by name, the ids as plain arrays; the dates go to their own file, by runtime quest id.
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters", "1.json")))!.AsObject();
        Assert.Equal("[15,71]", json["collectibles"]!["Mount"]!["owned"]!.ToJsonString());
        Assert.Null(json["completedUtc"]);
        Assert.Null(json["completionDatesSinceUtc"]);
        var dates = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters", "1.dates.json")))!.AsObject();
        Assert.NotNull(dates["completedUtc"]![QuestRecord.ToQuestId(Fixture.B).ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        Assert.NotNull(dates["sinceUtc"]);
    }

    [Fact]
    public void Empty_fields_are_not_written_and_an_older_file_reads_as_not_captured()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(Fixture.Snapshot(Fixture.A));

        var text = File.ReadAllText(Path.Combine(root, "characters", "1.json"));
        Assert.DoesNotContain("collectibles", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("completedUtc", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("completedAfterUtc", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("completionDatesSinceUtc", text, StringComparison.OrdinalIgnoreCase);

        var loaded = new JsonSnapshotStore(root).Load(1);
        Assert.NotNull(loaded);
        Assert.Empty(loaded.Collectibles);
        Assert.Null(CollectibleLookup.For(loaded));
        Assert.Null(loaded.CompletionDatesSinceUtc);
        Assert.Empty(loaded.CompletedUtc);
    }

    [Fact]
    public void The_frozen_pre_1_5_fixture_loads_without_owned_or_dates()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        Directory.CreateDirectory(Path.Combine(root, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(root, "characters", "1.json"));

        var store = new JsonSnapshotStore(root);
        var loaded = store.Load(1);

        Assert.NotNull(loaded);
        Assert.Empty(store.Warnings);
        Assert.Empty(loaded.Collectibles);
        Assert.Null(loaded.CompletionDatesSinceUtc);
        Assert.Null(Core.Runtime.CompletionDates.For(loaded, QuestRecord.ToQuestId(65621u)));
    }

    [Fact]
    public void A_kind_this_build_does_not_know_is_skipped_and_the_rest_still_load()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(WithEverything());
        var path = Path.Combine(root, "characters", "1.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["collectibles"]!["Glasses"] = JsonNode.Parse("""{ "owned": [1, 2], "missing": [] }""");
        json["collectibles"]!["7"] = JsonNode.Parse("""{ "owned": [1] }""");
        json["someFieldFromTheFuture"] = 42;
        File.WriteAllText(path, json.ToJsonString());

        var store = new JsonSnapshotStore(root);
        var loaded = store.Load(1);

        Assert.NotNull(loaded);
        Assert.Empty(store.Warnings);
        var lookup = CollectibleLookup.For(loaded)!;
        Assert.True(lookup.Owns(RewardKind.Mount, 15));
        Assert.Null(lookup.Owns((RewardKind)7, 1));
    }
}
