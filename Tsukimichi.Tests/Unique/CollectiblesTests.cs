using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Unique;

/// <summary>
/// Owned collectibles in the snapshot (decision 9): which rewards a capture checks, how a read becomes the saved field,
/// how the diff sees a new one, and the one obtained rule every reader follows (live, then stored, then unknown).
/// </summary>
public sealed class CollectiblesTests
{
    private static UniqueRewardEntry Entry(RewardKind kind, uint rewardId, uint itemId = 0, uint quest = Fixture.A) =>
        new(quest, kind, rewardId, itemId, "reward", Confidence.Static, "test");

    [Fact]
    public void Targets_are_the_flag_backed_entries_and_the_curated_duties_once_each()
    {
        var data = new UniqueRewardsData("g", default,
        [
            Entry(RewardKind.Mount, 15, 1001),
            Entry(RewardKind.Mount, 15, 1002, Fixture.B),
            Entry(RewardKind.Barding, 24, 7550),
            Entry(RewardKind.Hairstyle, 239, 36618),
            Entry(RewardKind.Item, 5),
            Entry(RewardKind.Title, 9),
            Entry(RewardKind.ArtifactGear, 3),
            Entry(RewardKind.Emote, 0),
            Entry(RewardKind.DutyUnlock, 40),
        ]);
        using var tmp = new Storage.TempDir();
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.DutyUnlocksFileName), """{ "66038": [ 40, 41 ] }""");
        var curated = CuratedData.Load(dir);

        var targets = Collectibles.Targets(data, curated);

        // In kind order, the first entry's item kept for a reward two quests give; a curated duty the data lists once.
        Assert.Equal(
            [
                new CollectibleTarget(RewardKind.Mount, 15, 1001),
                new CollectibleTarget(RewardKind.Barding, 24, 7550),
                new CollectibleTarget(RewardKind.Hairstyle, 239, 36618),
                new CollectibleTarget(RewardKind.DutyUnlock, 40, 0),
                new CollectibleTarget(RewardKind.DutyUnlock, 41, 0),
            ],
            targets);
        Assert.All(targets, t => Assert.True(Collectibles.IsStored(t.Kind)));
        Assert.False(Collectibles.IsStored(RewardKind.Item));
        Assert.True(Collectibles.IsStored(RewardKind.Barding));
        Assert.True(Collectibles.IsStored(RewardKind.Hairstyle));
    }

    [Fact]
    public void A_read_keeps_owned_and_missing_apart_and_leaves_unanswered_ids_unknown()
    {
        CollectibleTarget[] targets =
        [
            new(RewardKind.Mount, 71, 0),
            new(RewardKind.Mount, 15, 0),
            new(RewardKind.Mount, 3, 0),
            new(RewardKind.Minion, 8, 0),
            new(RewardKind.Barding, 24, 0),
        ];

        var read = Collectibles.Read(targets, t => t.Kind switch
        {
            RewardKind.Mount => t.RewardId != 3,
            RewardKind.Barding => false,
            _ => null,
        });

        Assert.Equal(["Barding", "Mount"], read.Keys.Order());
        Assert.Equal([15u, 71u], read["Mount"].Owned);
        Assert.Equal([3u], read["Mount"].Missing);
        Assert.Equal([24u], read["Barding"].Missing);

        var lookup = CollectibleLookup.For(Fixture.Snapshot() with { Collectibles = read })!;
        Assert.True(lookup.Owns(RewardKind.Mount, 15));
        Assert.False(lookup.Owns(RewardKind.Mount, 3));
        Assert.Null(lookup.Owns(RewardKind.Minion, 8));
    }

    [Fact]
    public void A_newly_obtained_mount_makes_the_diff_save_without_a_full_resolve()
    {
        var a = Fixture.Snapshot(Fixture.A) with
        {
            Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = [15], Missing = [71] } },
        };
        var b = a with
        {
            Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = [15, 71], Missing = [] } },
        };

        var diff = SnapshotDiff.Compute(a, b);

        Assert.False(diff.IsEmpty);
        Assert.True(diff.CollectiblesChanged);
        Assert.False(diff.OtherChanged);
        Assert.Empty(diff.ChangedQuestIds);

        // The first capture that saves them (an older file had none) is a change too; the same ids in another order are not.
        Assert.True(SnapshotDiff.Compute(Fixture.Snapshot(Fixture.A), a).CollectiblesChanged);
        var reordered = a with
        {
            Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = [15], Missing = [71] } },
        };
        Assert.True(SnapshotDiff.Compute(a, reordered).IsEmpty);
        var shuffled = b with
        {
            Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = [71, 15], Missing = [] } },
        };
        Assert.True(SnapshotDiff.Compute(b, shuffled).IsEmpty);
    }

    [Fact]
    public void Obtained_reads_live_first_then_the_stored_capture_then_unknown()
    {
        var stored = CollectibleLookup.For(Fixture.Snapshot() with
        {
            TakenUtc = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc),
            Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = [15], Missing = [71] } },
        });
        Assert.NotNull(stored);
        Assert.Equal(new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), stored.AsOfUtc);

        // Live here: the client's flag wins over an older capture (the mount was learned since).
        Assert.True(Collectibles.Obtained(true, () => true, stored, RewardKind.Mount, 71));

        // Live here but the read failed: the capture answers.
        Assert.False(Collectibles.Obtained(true, () => null, stored, RewardKind.Mount, 71));

        // Stored, or live in another client: the capture answers and the client is never asked.
        Assert.True(Collectibles.Obtained(false, () => throw new InvalidOperationException("not live"), stored, RewardKind.Mount, 15));
        Assert.False(Collectibles.Obtained(false, () => null, stored, RewardKind.Mount, 71));

        // Not checked at that capture, or no capture saved any: unknown.
        Assert.Null(Collectibles.Obtained(false, () => null, stored, RewardKind.Mount, 999));
        Assert.Null(Collectibles.Obtained(false, () => null, null, RewardKind.Mount, 15));
    }
}
