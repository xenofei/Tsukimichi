using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

public class ReversePrereqIndexTests
{
    private static QuestCatalog Chain() => Catalog(
        Quest(A) with { Level = 5 },
        Quest(B) with { Level = 10, PreviousQuests = new Prereq([A], JoinKind.All) },
        Quest(C) with { Level = 10, PreviousQuests = new Prereq([A, B], JoinKind.Any) },
        Quest(D) with { Level = 15, QuestLocks = [A] },
        Quest(E) with { Level = 5, Festival = 9 });

    [Fact]
    public void Dependents_include_previous_quest_and_lock_users()
    {
        var index = ReversePrereqIndex.Build(Chain());

        Assert.Equal([B, C, D], index.Dependents(A).Order());
        Assert.Equal([C], index.Dependents(B));
        Assert.Empty(index.Dependents(E));
        Assert.Empty(index.Dependents(12345));
    }

    [Fact]
    public void ByLevel_and_ByFestival_group_rows()
    {
        var index = ReversePrereqIndex.Build(Chain());

        Assert.Equal([B, C], index.ByLevel(10).Order());
        Assert.Empty(index.ByLevel(99));
        Assert.Equal([E], index.ByFestival(9));
        Assert.Empty(index.ByFestival(1));
    }

    [Fact]
    public void ResolveDependents_only_re_resolves_affected_rows()
    {
        var catalog = Chain();
        var index = ReversePrereqIndex.Build(catalog);
        var before = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        Assert.Equal(QuestState.Blocked, before[B].State);
        Assert.Equal(QuestState.Ready, before[D].State);

        var after = StateResolver.ResolveDependents(before, [A], index, catalog, Snapshot(A), EvalContext.Default);

        Assert.Equal(QuestState.Completed, after[A].State);
        Assert.Equal(QuestState.Ready, after[B].State);
        Assert.Equal(QuestState.Ready, after[C].State);
        Assert.Equal(QuestState.Foreclosed, after[D].State);
        Assert.Same(before[E], after[E]);
        Assert.NotSame(before[B], after[B]);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public void ResolveDependents_re_resolves_level_and_festival_rows_when_asked()
    {
        var catalog = Chain();
        var index = ReversePrereqIndex.Build(catalog);
        var low = Snapshot() with { JobLevels = Levels((Gladiator, 9)) };
        var before = StateResolver.ResolveAll(catalog, low, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, before[C].State);
        Assert.Equal(QuestState.Blocked, before[E].State);

        var leveled = low with { JobLevels = Levels((Gladiator, 10)), ActiveFestivals = [9] };
        var after = StateResolver.ResolveDependents(before, [], index, catalog, leveled, EvalContext.Default, changedLevels: [10], changedFestivals: [9]);

        Assert.Equal(QuestState.Blocked, after[C].State); // prerequisites still missing
        Assert.NotSame(before[C], after[C]);
        Assert.Equal(QuestState.Ready, after[E].State);
        Assert.Same(before[D], after[D]);
    }

    [Fact]
    public void ResolveDependents_ignores_unknown_ids()
    {
        var catalog = Chain();
        var index = ReversePrereqIndex.Build(catalog);
        var before = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);

        var after = StateResolver.ResolveDependents(before, [12345], index, catalog, Snapshot(), EvalContext.Default);

        Assert.Equal(before.Count, after.Count);
        Assert.All(before, kv => Assert.Same(kv.Value, after[kv.Key]));
    }
}
