using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

public sealed class SnapshotDiffTests
{
    [Fact]
    public void Identical_snapshots_are_empty_even_with_different_timestamps_and_names()
    {
        var a = Fixture.Snapshot(Fixture.A) with { TakenUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Name = "One" };
        var b = a with { TakenUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), Name = "Two", World = 5 };

        var diff = SnapshotDiff.Compute(a, b);

        Assert.True(diff.IsEmpty);
        Assert.Same(SnapshotDiff.Empty, diff);
    }

    [Fact]
    public void Completion_bit_changes_list_quest_ids_in_ascending_order()
    {
        var a = Fixture.Snapshot(Fixture.B);
        var b = Fixture.Snapshot(Fixture.A, Fixture.C);

        var diff = SnapshotDiff.Compute(a, b);

        Assert.Equal(
            new[] { QuestRecord.ToQuestId(Fixture.A), QuestRecord.ToQuestId(Fixture.B), QuestRecord.ToQuestId(Fixture.C) },
            diff.ChangedQuestIds);
        Assert.Empty(diff.ChangedJobs);
        Assert.False(diff.OtherChanged);
    }

    [Fact]
    public void Shorter_bitmask_reads_missing_bytes_as_zero()
    {
        var a = Fixture.Snapshot() with { CompletedBits = [] };
        var b = Fixture.Snapshot() with { CompletedBits = [0, 0, 0b1000_0000] };

        var diff = SnapshotDiff.Compute(a, b);

        Assert.Equal(new ushort[] { 23 }, diff.ChangedQuestIds);
        Assert.True(SnapshotDiff.Compute(b, a).ChangedQuestIds.SequenceEqual(diff.ChangedQuestIds));
    }

    [Fact]
    public void Accepted_added_removed_or_sequence_changed_counts_as_a_quest_change()
    {
        var a = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A, 1), Fixture.Accepted(Fixture.B, 2), Fixture.Accepted(Fixture.C, 1)] };
        var b = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A, 1), Fixture.Accepted(Fixture.B, 3), Fixture.Accepted(Fixture.D, 1)] };

        var diff = SnapshotDiff.Compute(a, b);

        Assert.Equal(
            new[] { QuestRecord.ToQuestId(Fixture.B), QuestRecord.ToQuestId(Fixture.C), QuestRecord.ToQuestId(Fixture.D) },
            diff.ChangedQuestIds);
    }

    [Fact]
    public void Daily_done_changes_count_as_quest_changes()
    {
        var a = Fixture.Snapshot() with { DailyDone = new Dictionary<ushort, byte> { [100] = 1 } };
        var b = Fixture.Snapshot() with { DailyDone = new Dictionary<ushort, byte> { [100] = 1, [101] = 1 } };

        Assert.Equal(new ushort[] { 101 }, SnapshotDiff.Compute(a, b).ChangedQuestIds);
        Assert.Equal(new ushort[] { 101 }, SnapshotDiff.Compute(b, a).ChangedQuestIds);
    }

    [Fact]
    public void Job_level_changes_list_jobs_and_nothing_else()
    {
        var a = Fixture.Snapshot() with { JobLevels = Fixture.Levels((Fixture.Gladiator, 50), (Fixture.Conjurer, 20)) };
        var b = Fixture.Snapshot() with { JobLevels = Fixture.Levels((Fixture.Gladiator, 51), (Fixture.Paladin, 1)) };

        var diff = SnapshotDiff.Compute(a, b);

        Assert.Equal(new[] { Fixture.Gladiator, Fixture.Conjurer, Fixture.Paladin }, diff.ChangedJobs);
        Assert.Empty(diff.ChangedQuestIds);
        Assert.False(diff.OtherChanged);
    }

    [Fact]
    public void Festival_changes_are_the_symmetric_difference()
    {
        var a = Fixture.Snapshot() with { ActiveFestivals = [1, 2] };
        var b = Fixture.Snapshot() with { ActiveFestivals = [2, 3] };

        Assert.Equal(new ushort[] { 1, 3 }, SnapshotDiff.Compute(a, b).ChangedFestivals);
    }

    public static TheoryData<Func<CharacterSnapshot, CharacterSnapshot>> OtherMutations => new()
    {
        s => s with { GrandCompany = 2 },
        s => s with { GcRanks = [0, 3, 0, 0] },
        s => s with { Tribes = new Dictionary<byte, TribeStanding> { [1] = new(2, 10) } },
        s => s with { TribeAllowance = 3 },
        s => s with { LeveAllowance = 7 },
        s => s with { UnlockedInstances = [5] },
        s => s with { CurrentJob = Fixture.Conjurer },
        s => s with { AchievementsLoaded = false },
        s => s with { CompletedAchievements = [12] },
        s => s with { MaxExpansion = 2 },
        s => s with { LevelCap = 60 },
        s => s with { ContentId = 99 },
    };

    [Theory]
    [MemberData(nameof(OtherMutations))]
    public void Other_evaluation_inputs_flag_a_full_resolve(Func<CharacterSnapshot, CharacterSnapshot> mutate)
    {
        var a = Fixture.Snapshot(Fixture.A);
        var b = mutate(a);

        var diff = SnapshotDiff.Compute(a, b);

        Assert.True(diff.OtherChanged);
        Assert.False(diff.IsEmpty);
        Assert.Empty(diff.ChangedQuestIds);
    }

    [Fact]
    public void Reordered_lists_with_the_same_members_are_not_a_change()
    {
        var a = Fixture.Snapshot() with { UnlockedInstances = [1, 2], CompletedAchievements = [7, 8], ActiveFestivals = [4, 5] };
        var b = Fixture.Snapshot() with { UnlockedInstances = [2, 1], CompletedAchievements = [8, 7], ActiveFestivals = [5, 4] };

        Assert.True(SnapshotDiff.Compute(a, b).IsEmpty);
    }
}
