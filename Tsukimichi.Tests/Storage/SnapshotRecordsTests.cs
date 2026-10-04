using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// The records 1.19.0 adds at schema v1 (C7's item levels, N4's duty records): written only when captured, round-tripped
/// by the store, read as "not captured" from an older file, and a change of theirs saves the capture without resolving
/// any quest.
/// </summary>
public sealed class SnapshotRecordsTests
{
    private static CharacterSnapshot Captured() => Fixture.Snapshot(Fixture.A) with
    {
        ItemLevel = 677,
        JobItemLevels = new Dictionary<byte, ushort> { [22] = 677, [21] = 692 },
        DutyRecords = new DutyRecordCapture(5, [1004, 1005], [1004]),
    };

    [Fact]
    public void Save_then_Load_round_trips_item_levels_and_duty_records()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(Captured());
        var loaded = new JsonSnapshotStore(root).Load(1);

        Assert.NotNull(loaded);
        Assert.Equal(677, loaded.ItemLevel);
        Assert.Equal(692, loaded.JobItemLevels[21]);
        Assert.NotNull(loaded.DutyRecords);
        Assert.Equal(5u, loaded.DutyRecords.Watch);
        Assert.Equal([1004u, 1005u], loaded.DutyRecords.Unlocked);
        Assert.Equal([1004u], loaded.DutyRecords.Cleared);

        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters", "1.json")))!.AsObject();
        Assert.Equal(677, (int)json["itemLevel"]!);
        Assert.Equal(692, (int)json["jobItemLevels"]!["21"]!);
    }

    [Fact]
    public void Uncaptured_records_are_not_written_and_read_back_as_not_captured()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(Fixture.Snapshot(Fixture.A));

        var text = File.ReadAllText(Path.Combine(root, "characters", "1.json"));
        Assert.DoesNotContain("itemLevel", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dutyRecords", text, StringComparison.OrdinalIgnoreCase);

        var loaded = new JsonSnapshotStore(root).Load(1);
        Assert.NotNull(loaded);
        Assert.Equal(0, loaded.ItemLevel);
        Assert.Empty(loaded.JobItemLevels);
        Assert.Null(loaded.DutyRecords);
    }

    [Fact]
    public void A_record_change_is_saved_without_resolving_a_quest()
    {
        var a = Captured();

        // The next poll's equal capture in new collections is no change.
        Assert.True(SnapshotDiff.Compute(a, a with
        {
            JobItemLevels = new Dictionary<byte, ushort> { [21] = 692, [22] = 677 },
            DutyRecords = new DutyRecordCapture(5, [1004, 1005], [1004]),
        }).IsEmpty);

        foreach (var changed in new[]
        {
            a with { ItemLevel = 680 },
            a with { JobItemLevels = new Dictionary<byte, ushort> { [22] = 677, [21] = 700 } },
            a with { DutyRecords = new DutyRecordCapture(5, [1004, 1005], [1004, 1005]) },
            a with { DutyRecords = null },
        })
        {
            var diff = SnapshotDiff.Compute(a, changed);
            Assert.False(diff.IsEmpty);
            Assert.True(diff.RecordsChanged);
            Assert.False(diff.OtherChanged);
            Assert.Empty(diff.ChangedQuestIds);
            Assert.False(FullPass.Needed(diff, offerChanged: false));
        }
    }
}
