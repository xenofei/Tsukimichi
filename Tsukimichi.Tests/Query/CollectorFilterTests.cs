using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The collector extras in the quest table and the counts (feature plan v5): the free-trial view folds what lies
/// beyond the trial into a last group that reads "Beyond your trial" (R9 F3), the tree counts leave it out of the
/// totals, and "Once-only story quests I haven't done" keeps the story quests New Game+ cannot replay (R9 F7).
/// </summary>
public sealed class CollectorFilterTests
{
    // Genre 100: a linear story of three quests (a derived chain), the middle one replayable in New Game+.
    // Genre 101: two loose quests; 70001 is an Endwalker quest and 70002 a level 90 one, both beyond the trial.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(65001, "Story one", genre: 100, sortKey: 1),
        Quest(65002, "Story two", genre: 100, sortKey: 2) with { PreviousQuests = new Prereq([65001], JoinKind.All) },
        Quest(65003, "Story three", genre: 100, sortKey: 3) with { PreviousQuests = new Prereq([65002], JoinKind.All) },
        Quest(70001, "Endwalker quest", genre: 101, sortKey: 4, expansion: 4, level: 80),
        Quest(70002, "Level 90 quest", genre: 101, sortKey: 5, expansion: 3, level: 90),
        Quest(65004, "Shadowbringers quest", genre: 101, sortKey: 6, expansion: 3, level: 80),
        Quest(65005, "Daily", genre: 101, sortKey: 7, repeatable: true),
    ]);

    private static readonly IReadOnlySet<uint> Replayable = new HashSet<uint> { 65002 };

    [Theory]
    [InlineData(3, 80, false)]
    [InlineData(4, 80, true)]
    [InlineData(3, 81, true)]
    [InlineData(0, 1, false)]
    public void The_trial_ends_after_Shadowbringers_and_level_80(byte expansion, byte level, bool beyond)
    {
        Assert.Equal(beyond, FreeTrial.IsBeyond(Quest(1, "q", expansion: expansion, level: level)));
    }

    [Fact]
    public void Under_the_trial_view_what_lies_beyond_is_listed_last_and_says_so()
    {
        var states = States(Catalog, QuestState.Blocked);
        var ctx = QueryContext.Empty with { FreeTrial = true };
        var result = Run(Catalog, Evaluations(states, new Dictionary<uint, string> { [70001] = "needs Endwalker" }), ctx: ctx);

        Assert.Equal(2, result.BeyondTrial);
        Assert.Equal([65001u, 65002u, 65003u, 65004u, 65005u, 70001u, 70002u], RowIds(result));
        Assert.All(result.Rows[^2..], row => Assert.Equal("Beyond your trial", row.Status));
        Assert.DoesNotContain(result.Rows[..^2], row => row.Status == "Beyond your trial");
    }

    [Fact]
    public void Without_the_trial_view_nothing_moves()
    {
        var result = Run(Catalog, Evaluations(States(Catalog, QuestState.Blocked)));

        Assert.Equal(0, result.BeyondTrial);
        Assert.Equal([65001u, 65002u, 65003u, 70001u, 70002u, 65004u, 65005u], RowIds(result));
    }

    [Fact]
    public void Tree_counts_leave_what_lies_beyond_the_trial_out_of_the_totals()
    {
        var states = States(Catalog, QuestState.Blocked);
        states[65001] = QuestState.Completed;
        var evaluations = Evaluations(states);

        var normal = TreeCounts.Compute(Catalog, evaluations, includeUnlisted: false);
        var trial = TreeCounts.Compute(Catalog, evaluations, includeUnlisted: false, FreeTrial.IsBeyond);

        // The daily is a repeatable outside the counts either way.
        Assert.Equal(new NodeCount(1, 6, 0), normal.Overall);
        Assert.Equal(1, trial.Overall.Done);
        Assert.Equal(4, trial.Overall.Total);
        Assert.Equal(2, trial.Overall.BeyondTrial);
        Assert.Equal(2, trial.Genre(101).BeyondTrial);
        Assert.Equal(0, trial.Genre(100).BeyondTrial);
    }

    [Fact]
    public void A_quest_beyond_the_trial_already_done_still_counts()
    {
        var states = States(Catalog, QuestState.Blocked);
        states[70001] = QuestState.Completed;
        var counts = TreeCounts.Compute(Catalog, Evaluations(states), includeUnlisted: false, FreeTrial.IsBeyond);

        Assert.Equal(1, counts.Overall.Done);
        Assert.Equal(1, counts.Overall.BeyondTrial);
        Assert.Equal(5, counts.Overall.Total);
    }

    [Fact]
    public void The_beyond_trial_group_holds_only_what_the_tree_counts_as_beyond()
    {
        // 70001 is done (it keeps its place and status), 70002 is locked out (it keeps "Locked out"): neither is left
        // to do beyond the trial, so the group and the tree's tally hold neither.
        var states = States(Catalog, QuestState.Blocked);
        states[70001] = QuestState.Completed;
        states[70002] = QuestState.Foreclosed;
        var evaluations = Evaluations(states, new Dictionary<uint, string> { [70002] = "Locked out" });
        var ctx = QueryContext.Empty with { FreeTrial = true };
        var result = Run(Catalog, evaluations, ctx: ctx);
        var counts = TreeCounts.Compute(Catalog, evaluations, includeUnlisted: false, FreeTrial.IsBeyond);

        Assert.Equal(0, result.BeyondTrial);
        Assert.Equal(0, counts.Overall.BeyondTrial);
        Assert.Equal([65001u, 65002u, 65003u, 70001u, 70002u, 65004u, 65005u], RowIds(result));
        Assert.DoesNotContain(result.Rows, row => row.Status == "Beyond your trial");
        Assert.StartsWith("Locked out", result.Rows.Single(row => row.Quest.RowId == 70002).Status);

        // One left to do: the group and the tally agree.
        states[70001] = QuestState.Ready;
        evaluations = Evaluations(states, new Dictionary<uint, string> { [70002] = "Locked out" });
        result = Run(Catalog, evaluations, ctx: ctx);
        counts = TreeCounts.Compute(Catalog, evaluations, includeUnlisted: false, FreeTrial.IsBeyond);
        Assert.Equal(1, result.BeyondTrial);
        Assert.Equal(1, counts.Overall.BeyondTrial);
        Assert.Equal(70001u, result.Rows[^1].Quest.RowId);
        Assert.Equal("Beyond your trial", result.Rows[^1].Status);
    }

    [Fact]
    public void Once_only_story_leaves_out_quests_out_of_the_totals()
    {
        var chains = ChainCatalog.Build(Catalog, CuratedData.Empty);
        var evaluations = Evaluations(States(Catalog, QuestState.Ready));

        // 65001 is a spare alternative of a choice not made yet: the choice's other option stands for it.
        evaluations[65001] = evaluations[65001] with { IsSpareAlternative = true };
        var ctx = QueryContext.Empty with { NewGamePlus = Replayable, Chains = chains };
        var result = Run(Catalog, evaluations, new FilterSet { OnceOnlyStory = true }, ctx: ctx);

        Assert.Equal([65003u], RowIds(result));
        Assert.False(NewGamePlus.IsOnceOnlyStoryLeft(Catalog.GetByRowId(65003)!, QuestState.Blocked, leavesTotals: true, Replayable, chains, null));
        Assert.True(NewGamePlus.IsOnceOnlyStoryLeft(Catalog.GetByRowId(65003)!, QuestState.Blocked, leavesTotals: false, Replayable, chains, null));
    }

    [Fact]
    public void Once_only_story_keeps_the_open_story_quests_New_Game_plus_cannot_replay()
    {
        var chains = ChainCatalog.Build(Catalog, CuratedData.Empty);
        var states = States(Catalog, QuestState.Ready);
        states[65003] = QuestState.Completed;
        var ctx = QueryContext.Empty with { NewGamePlus = Replayable, Chains = chains };
        var result = Run(Catalog, Evaluations(states), new FilterSet { OnceOnlyStory = true }, ctx: ctx);

        // 65002 is replayable, 65003 done, the genre-101 quests are on no story and the daily repeats.
        Assert.Equal([65001u], RowIds(result));
    }

    [Fact]
    public void Once_only_story_keeps_nothing_without_the_New_Game_plus_data()
    {
        var chains = ChainCatalog.Build(Catalog, CuratedData.Empty);
        var ctx = QueryContext.Empty with { Chains = chains };
        var result = Run(Catalog, Evaluations(States(Catalog, QuestState.Ready)), new FilterSet { OnceOnlyStory = true }, ctx: ctx);

        Assert.Empty(result.Rows);
        Assert.Contains(FilterNames.OnceOnlyStory, result.Empty!.Filters);
    }

    [Fact]
    public void The_replay_badge_reads_the_chapters_and_leaves_repeatables_alone()
    {
        Assert.Equal(ReplayKind.Replayable, NewGamePlus.Of(Catalog.GetByRowId(65002)!, Replayable));
        Assert.Equal(ReplayKind.OnceOnly, NewGamePlus.Of(Catalog.GetByRowId(65001)!, Replayable));
        Assert.Equal(ReplayKind.None, NewGamePlus.Of(Catalog.GetByRowId(65005)!, Replayable));
        Assert.Equal(ReplayKind.None, NewGamePlus.Of(Catalog.GetByRowId(65001)!, new HashSet<uint>()));
    }

    [Fact]
    public void The_filter_is_a_counted_narrowing_filter_that_round_trips()
    {
        var filters = new FilterSet { OnceOnlyStory = true };

        Assert.True(filters.IsActive());
        Assert.Equal(1, FilterBadge.Count(filters));
        Assert.Equal(filters, filters.Clone());
        Assert.NotEqual(new FilterSet(), filters);
        filters.Reset();
        Assert.False(filters.OnceOnlyStory);
    }
}
