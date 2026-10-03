using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

public sealed class FullPassTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc);

    private static QuestCatalog Catalog() => Fixture.Catalog(
        Fixture.Quest(Fixture.A),
        Fixture.Quest(Fixture.B) with { Level = 60 },
        Fixture.Quest(Fixture.C) with { Level = 50 });

    [Fact]
    public void A_level_change_needs_a_full_pass()
    {
        var old = Fixture.Snapshot(Fixture.A);
        var levelled = old with { JobLevels = Fixture.Levels((Fixture.Gladiator, 60)) };

        Assert.True(FullPass.Needed(SnapshotDiff.Compute(old, levelled), offerChanged: false));
    }

    [Fact]
    public void A_job_change_or_a_new_offer_needs_a_full_pass()
    {
        var old = Fixture.Snapshot(Fixture.A);
        var swapped = old with { CurrentJob = Fixture.Conjurer };

        Assert.True(FullPass.Needed(SnapshotDiff.Compute(old, swapped), offerChanged: false));
        Assert.True(FullPass.Needed(SnapshotDiff.Empty, offerChanged: true));
    }

    [Fact]
    public void A_few_completed_quests_resolve_incrementally()
    {
        var old = Fixture.Snapshot();
        var done = Fixture.Snapshot(Fixture.A);

        Assert.False(FullPass.Needed(SnapshotDiff.Compute(old, done), offerChanged: false));
    }

    [Fact]
    public void The_last_mount_of_a_collection_re_resolves_the_gated_quest()
    {
        // 1.11.0, C2: owning the seventh Lanner opens the Firebird quest. Before, a capture where only the mounts moved
        // set only CollectiblesChanged, nothing re-resolved, and the quest kept reading Blocked.
        uint[] lanners = [75, 76, 77, 78, 90, 98, 104];
        var firebird = Fixture.Quest(67086);
        var catalog = QuestCatalog.Build([firebird], null, new Dictionary<uint, QuestGate> { [firebird.RowId] = new("all seven Heavensward Lanner mounts", [], null, lanners) });
        var old = Owning(Fixture.Snapshot(), "Mount", lanners[..6], [104]);
        var owned = Owning(Fixture.Snapshot(), "Mount", lanners, []);
        var previous = StateResolver.ResolveAll(catalog, old, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, previous[firebird.RowId].State);

        var diff = SnapshotDiff.Compute(old, owned);
        Assert.True(diff.MountsChanged);
        Assert.False(diff.OtherChanged);
        Assert.True(FullPass.Needed(diff, offerChanged: false));

        var result = FullPass.Run(catalog, old, owned, diff, EvalContext.Default, previous, Now);
        Assert.Equal(QuestState.Ready, result.States[firebird.RowId].State);
    }

    [Fact]
    public void A_new_minion_alone_needs_no_full_pass()
    {
        var old = Owning(Fixture.Snapshot(), "Minion", [1], [2]);
        var diff = SnapshotDiff.Compute(old, Owning(Fixture.Snapshot(), "Minion", [1, 2], []));

        Assert.True(diff.CollectiblesChanged);
        Assert.False(diff.MountsChanged);
        Assert.False(FullPass.Needed(diff, offerChanged: false));
    }

    private static CharacterSnapshot Owning(CharacterSnapshot snapshot, string kind, uint[] owned, uint[] missing) =>
        snapshot with { Collectibles = new Dictionary<string, CollectibleSet> { [kind] = new() { Owned = owned, Missing = missing } } };

    [Fact]
    public void Many_changed_quests_need_a_full_pass()
    {
        var diff = new SnapshotDiff([.. Enumerable.Range(1, 3).Select(i => (ushort)i)], [], [], false);

        Assert.False(FullPass.Needed(diff, offerChanged: false, threshold: 3));
        Assert.True(FullPass.Needed(diff, offerChanged: false, threshold: 2));
    }

    [Fact]
    public void Run_gives_the_states_and_events_the_framework_thread_would()
    {
        var catalog = Catalog();
        var old = Fixture.Snapshot(Fixture.A);
        var levelled = old with { JobLevels = Fixture.Levels((Fixture.Gladiator, 60)) };
        var diff = SnapshotDiff.Compute(old, levelled);
        var previous = StateResolver.ResolveAll(catalog, old, EvalContext.Default);

        var result = FullPass.Run(catalog, old, levelled, diff, EvalContext.Default, previous, Now);

        var expected = StateResolver.ResolveAll(catalog, levelled, EvalContext.Default);
        Assert.Equal(expected.Keys.Order(), result.States.Keys.Order());
        foreach (var (rowId, evaluation) in expected)
        {
            Assert.Equal(evaluation.State, result.States[rowId].State);
        }

        Assert.Equal(QuestEvents.Derive(diff, old, levelled, catalog, previous, expected, Now), result.Events);
        Assert.Equal([new QuestEvent(QuestEventKind.NewlyAvailable, Fixture.B, Now)], result.Events);
        Assert.True(result.ResolveMs >= 0);
    }

    [Fact]
    public void Run_leaves_the_committed_states_as_they_were()
    {
        var catalog = Catalog();
        var old = Fixture.Snapshot(Fixture.A);
        var levelled = old with { JobLevels = Fixture.Levels((Fixture.Gladiator, 60)) };
        var previous = StateResolver.ResolveAll(catalog, old, EvalContext.Default);
        var before = previous.ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = FullPass.Run(catalog, old, levelled, SnapshotDiff.Compute(old, levelled), EvalContext.Default, previous, Now);

        Assert.NotSame(previous, result.States);
        Assert.Equal(before.Count, previous.Count);
        foreach (var (rowId, evaluation) in before)
        {
            Assert.Same(evaluation, previous[rowId]);
        }
    }

    [Fact]
    public void A_pass_whose_inputs_are_unchanged_commits()
    {
        var catalog = Catalog();
        var base_ = Fixture.Snapshot(Fixture.A);

        Assert.Equal(FullPassVerdict.Commit, FullPass.Judge(catalog, catalog, 1, 1, base_, base_));
    }

    [Fact]
    public void A_replaced_catalog_drops_the_pass()
    {
        var base_ = Fixture.Snapshot(Fixture.A);

        Assert.Equal(FullPassVerdict.CatalogChanged, FullPass.Judge(Catalog(), Catalog(), 1, 1, base_, base_));
        Assert.Equal(FullPassVerdict.CatalogChanged, FullPass.Judge(Catalog(), null, 1, 1, base_, base_));
    }

    [Fact]
    public void Another_character_or_a_logout_drops_the_pass()
    {
        var catalog = Catalog();
        var base_ = Fixture.Snapshot(Fixture.A);

        Assert.Equal(FullPassVerdict.CharacterChanged, FullPass.Judge(catalog, catalog, 1, 2, base_, base_));
        Assert.Equal(FullPassVerdict.CharacterChanged, FullPass.Judge(catalog, catalog, 1, 0, base_, base_));
        Assert.Equal(FullPassVerdict.CharacterChanged, FullPass.Judge(catalog, catalog, 1, null, base_, base_));
    }

    [Fact]
    public void A_moved_base_capture_drops_the_pass_for_a_new_diff()
    {
        var catalog = Catalog();
        var base_ = Fixture.Snapshot(Fixture.A);
        var equalButNewer = base_ with { };

        Assert.Equal(FullPassVerdict.BaseChanged, FullPass.Judge(catalog, catalog, 1, 1, base_, equalButNewer));
        Assert.Equal(FullPassVerdict.BaseChanged, FullPass.Judge(catalog, catalog, 1, 1, base_, null));
    }

    [Fact]
    public void The_catalog_is_judged_before_the_character_and_the_base()
    {
        var base_ = Fixture.Snapshot(Fixture.A);

        Assert.Equal(FullPassVerdict.CatalogChanged, FullPass.Judge(Catalog(), Catalog(), 1, 2, base_, null));
        var catalog = Catalog();
        Assert.Equal(FullPassVerdict.CharacterChanged, FullPass.Judge(catalog, catalog, 1, 2, base_, null));
    }
}
