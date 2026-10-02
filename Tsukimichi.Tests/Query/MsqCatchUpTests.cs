using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Evaluation;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The main scenario catch-up summary (1.9.0, R6 F): the quests left per expansion, their level span and the duties
/// they ask to have cleared, over the frozen catalog for a fresh character and for the real schema-v1 character in
/// <c>Fixtures/snapshot-v1.json</c>, and on a small synthetic story.
/// </summary>
public sealed class MsqCatchUpTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private static readonly Dictionary<uint, UniqueOverride> NoOverrides = [];

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    /// <summary>The duty unlocks of the shipped curated files and <c>unique_quests.json</c>, as the plugin reads them (no sheet mapping).</summary>
    private CatchUpDutySource ShippedDuties()
    {
        var data = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        return CatchUpDutySource.From(fixture.Curated, UniqueRewardCatalog.Build(data, NoOverrides, fixture.Curated));
    }

    private static CharacterSnapshot LoadFixtureSnapshot()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.File("characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.File("characters"), "1.json"));
        return new JsonSnapshotStore(tmp.Path).Load(1) ?? throw new InvalidOperationException("fixture snapshot did not load");
    }

    private void Log(MsqCatchUpSummary summary)
    {
        output.WriteLine($"{summary.Quests} quests, Lv {summary.MinLevel}–{summary.MaxLevel}, {summary.Duties} duties");
        foreach (var part in summary.Expansions)
        {
            output.WriteLine($"  expansion {part.Expansion}: {part.Quests} quests, Lv {part.MinLevel}–{part.MaxLevel}, {part.Duties.Count} duties");
        }
    }

    /// <summary>Every count agrees with the main scenario position, and every expansion's span with its quests.</summary>
    private void AssertAgreesWithPosition(CharacterSnapshot snapshot, IReadOnlyDictionary<uint, QuestEvaluation> states, MsqCatchUpSummary summary)
    {
        var position = MsqProgress.Compute(Catalog, states);
        Assert.NotNull(position);
        Assert.Equal(position.Total - position.Done, summary.Quests);

        var left = MsqGraph.For(Catalog).Story
            .Where(q => states[q.RowId] is { LeavesTotals: false, State: not QuestState.Completed })
            .ToList();
        Assert.Equal(left.Count, summary.Quests);
        Assert.Equal(left.Select(q => q.Expansion).Distinct().Order(), summary.Expansions.Select(e => e.Expansion));
        foreach (var part in summary.Expansions)
        {
            var quests = left.Where(q => q.Expansion == part.Expansion).ToList();
            Assert.Equal(quests.Count, part.Quests);
            Assert.Equal(quests.Min(q => q.DisplayLevel), part.MinLevel);
            Assert.Equal(quests.Max(q => q.DisplayLevel), part.MaxLevel);
            var required = quests.SelectMany(q => q.InstanceContentRequired).Where(d => d != 0 && !snapshot.UnlockedInstances.Contains(d)).Distinct();
            Assert.All(required, id => Assert.Contains(part.Duties, d => d.InstanceContentId == id));
            Assert.Equal(part.Duties.Count, part.Duties.Distinct().Count());
        }
    }

    [Fact]
    public void A_fresh_character_has_the_whole_story_ahead()
    {
        var snapshot = Fixture.Snapshot() with { JobLevels = Fixture.Levels((1, 1)), LevelCap = 100, MaxExpansion = 5 };
        var states = StateResolver.ResolveAll(Catalog, snapshot, EvalContext.Default);
        var summary = MsqCatchUp.Compute(Catalog, states, snapshot, ShippedDuties());
        Assert.NotNull(summary);
        Log(summary);
        AssertAgreesWithPosition(snapshot, states, summary);

        Assert.False(summary.IsComplete);
        Assert.Equal([0, 1, 2, 3, 4, 5], summary.Expansions.Select(e => (int)e.Expansion));
        Assert.Equal(1, summary.MinLevel);
        Assert.Equal(100, summary.MaxLevel);
        Assert.True(summary.Quests > 800, $"a fresh character has the whole story ahead, found {summary.Quests}");
        Assert.True(summary.Duties > 50, $"the story's dungeons and trials, found {summary.Duties}");
        Assert.All(summary.Expansions, e => Assert.NotEmpty(e.Duties));

        // Without the unlock data only the duties the story asks to have cleared count.
        var required = MsqCatchUp.Compute(Catalog, states, snapshot);
        Assert.NotNull(required);
        Assert.Equal(summary.Quests, required.Quests);
        Assert.True(required.Duties < summary.Duties);
    }

    [Fact]
    public void The_fixture_character_has_what_its_position_says()
    {
        var snapshot = LoadFixtureSnapshot();
        var states = StateResolver.ResolveAll(Catalog, snapshot, EvalContext.Default);
        var summary = MsqCatchUp.Compute(Catalog, states, snapshot, ShippedDuties());
        Assert.NotNull(summary);
        Log(summary);
        AssertAgreesWithPosition(snapshot, states, summary);
    }

    [Fact]
    public void A_synthetic_story_counts_per_expansion_and_skips_cleared_duties()
    {
        var msq = new JournalRef(0, "Main Scenario", 1, "Story", 1, "Story", 0);
        var a = Fixture.Quest(65800) with { Journal = msq with { SortKey = 1 }, Level = 50, Expansion = 0 };
        var b = Fixture.Quest(65801) with { Journal = msq with { SortKey = 2 }, Level = 50, LevelOffset = 0, Expansion = 0, PreviousQuests = new Prereq([65800u], JoinKind.All), InstanceContentRequired = [10u] };
        var c = Fixture.Quest(65802) with { Journal = msq with { SortKey = 3 }, Level = 60, Expansion = 1, PreviousQuests = new Prereq([65801u], JoinKind.All), InstanceContentRequired = [11u, 12u] };
        var d = Fixture.Quest(65803) with { Journal = msq with { SortKey = 4 }, Level = 61, LevelOffset = 2, Expansion = 1, PreviousQuests = new Prereq([65802u], JoinKind.All), InstanceContentRequired = [12u] };
        var catalog = Fixture.Catalog(a, b, c, d);

        var snapshot = Fixture.Snapshot(a.RowId) with { UnlockedInstances = [11u] };
        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);
        var summary = MsqCatchUp.Compute(catalog, states, snapshot);
        Assert.NotNull(summary);
        Assert.Equal(3, summary.Quests);
        Assert.Equal(50, summary.MinLevel);
        Assert.Equal(63, summary.MaxLevel);
        Assert.Equal(2, summary.Duties);

        var arr = summary.Expansions[0];
        Assert.Equal((0, 1, 50, 50), (arr.Expansion, arr.Quests, arr.MinLevel, arr.MaxLevel));
        Assert.Equal([new CatchUpDuty(0, 10)], arr.Duties);
        var hw = summary.Expansions[1];
        Assert.Equal((1, 2, 60, 63), (hw.Expansion, hw.Quests, hw.MinLevel, hw.MaxLevel));
        Assert.Equal([new CatchUpDuty(0, 12)], hw.Duties);

        // b unlocks Duty Finder entry 112 (instance 12, which d needs) and c unlocks 113: with the mapping the two
        // sightings of instance 12 are one duty.
        var source = new CatchUpDutySource(
            rowId => rowId switch { 65801u => [112u], 65802u => [113u], _ => [] },
            instance => instance switch { 10u => 110u, 12u => 112u, _ => 0u });
        var mapped = MsqCatchUp.Compute(catalog, states, snapshot, source);
        Assert.NotNull(mapped);
        Assert.Equal([new CatchUpDuty(110, 10), new CatchUpDuty(112, 0)], mapped.Expansions[0].Duties);
        Assert.Equal([new CatchUpDuty(112, 12), new CatchUpDuty(113, 0)], mapped.Expansions[1].Duties);
        Assert.Equal(3, mapped.Duties);

        var done = Fixture.Snapshot(a.RowId, b.RowId, c.RowId, d.RowId);
        var complete = MsqCatchUp.Compute(catalog, StateResolver.ResolveAll(catalog, done, EvalContext.Default), done);
        Assert.NotNull(complete);
        Assert.True(complete.IsComplete);
        Assert.Equal((0, 0, 0, 0), (complete.Quests, complete.MinLevel, complete.MaxLevel, complete.Duties));
    }

    [Fact]
    public void Inside_a_routed_region_the_count_is_the_positions_quests_left()
    {
        // The Evercold fixture with an Any join: route C done opens the join, and the rest of routes A and B are
        // leftovers. The reconvergence quest stays Blocked (a level, say), so only the routes tell the join is met.
        var catalog = MsqBranchFixture.Build(JoinKind.Any);
        var states = MsqBranchFixture.Evaluate(catalog, MsqBranchFixture.L1, MsqBranchFixture.L2, MsqBranchFixture.A1, MsqBranchFixture.C1, MsqBranchFixture.C2);
        states[MsqBranchFixture.J] = states[MsqBranchFixture.J] with { State = QuestState.Blocked };

        var position = MsqProgress.Compute(catalog, states);
        var summary = MsqCatchUp.Compute(catalog, states, null);
        Assert.NotNull(position);
        Assert.NotNull(summary);

        // J, P1 and P2 are left; A2, A3 and route B are not.
        Assert.Equal(3, position.Total - position.Done);
        Assert.Equal(position.Total - position.Done, summary.Quests);

        // While the join is not met every route's quests are still ahead.
        var inside = MsqBranchFixture.Evaluate(catalog, MsqBranchFixture.L1, MsqBranchFixture.L2, MsqBranchFixture.A1);
        var insidePosition = MsqProgress.Compute(catalog, inside);
        Assert.NotNull(insidePosition);
        Assert.Equal(insidePosition.Total - insidePosition.Done, MsqCatchUp.Compute(catalog, inside, null)?.Quests);
    }

    [Fact]
    public void Duties_already_cleared_are_not_left()
    {
        // a opens instance 20 (cleared already); b needs 21 or 22 (Any) with 22 cleared; c needs 23 or 24, neither cleared.
        var msq = new JournalRef(0, "Main Scenario", 1, "Story", 1, "Story", 0);
        var a = Fixture.Quest(65810) with { Journal = msq with { SortKey = 1 }, Level = 50, Rewards = [new RewardRef(RewardKind.Instance, 20, 0, 1, "Opened", 0)] };
        var b = Fixture.Quest(65811) with { Journal = msq with { SortKey = 2 }, Level = 50, PreviousQuests = new Prereq([65810u], JoinKind.All), InstanceContentRequired = [21u, 22u], InstanceJoin = JoinKind.Any };
        var c = Fixture.Quest(65812) with { Journal = msq with { SortKey = 3 }, Level = 50, PreviousQuests = new Prereq([65811u], JoinKind.All), InstanceContentRequired = [23u, 24u], InstanceJoin = JoinKind.Any };
        var catalog = Fixture.Catalog(a, b, c);

        var snapshot = Fixture.Snapshot() with { UnlockedInstances = [20u, 22u] };
        var summary = MsqCatchUp.Compute(catalog, StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default), snapshot);
        Assert.NotNull(summary);
        Assert.Equal(3, summary.Quests);

        // Any one of 23 and 24 will do: one duty, not two.
        Assert.Equal([new CatchUpDuty(0, 23)], Assert.Single(summary.Expansions).Duties);
    }

    [Fact]
    public void A_catalog_without_a_main_scenario_has_no_summary()
    {
        var side = Fixture.Quest(65900) with { Journal = new JournalRef(3, "Side", 30, "Side", 300, "Side", 1) };
        var catalog = Fixture.Catalog(side);
        Assert.Null(MsqCatchUp.Compute(catalog, StateResolver.ResolveAll(catalog, Fixture.Snapshot(), EvalContext.Default), null));
    }
}
