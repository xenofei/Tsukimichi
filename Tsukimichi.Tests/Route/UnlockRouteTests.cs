using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Tests.Evaluation;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Route;

/// <summary><see cref="UnlockRoute"/>'s ordering rule, joins and pruning on small hand-built catalogs, plus Copy route and Pin all.</summary>
public class UnlockRouteTests
{
    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static Dictionary<uint, QuestEvaluation> States(QuestCatalog catalog, params (uint RowId, QuestState State)[] set)
    {
        var states = catalog.All.ToDictionary(q => q.RowId, _ => Eval(QuestState.Blocked));
        foreach (var (rowId, state) in set)
        {
            states[rowId] = Eval(state);
        }

        return states;
    }

    /// <summary>A side quest (journal section 3, so not main scenario) at <paramref name="level"/> with the given previous quests.</summary>
    private static QuestRecord Needs(uint rowId, byte level, JoinKind join, params uint[] prereqs) =>
        Quest(rowId) with
        {
            Level = level,
            PreviousQuests = new Prereq(prereqs, join),
            Journal = new JournalRef(3, "Side", 60, "Sidequests", 1, "Genre", (int)rowId),
        };

    [Fact]
    public void Ready_choices_go_by_level_then_journal_order_and_the_target_is_last()
    {
        // Target needs A (Lv 10), B (Lv 5) and C (Lv 5, later in the journal).
        var catalog = Catalog(
            Needs(A, 10, JoinKind.All),
            Needs(B, 5, JoinKind.All),
            Needs(C, 5, JoinKind.All),
            Needs(Target, 10, JoinKind.All, A, B, C));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog));

        Assert.Equal([B, C, A, Target], route.Steps.Select(s => s.RowId));
        Assert.Equal([5, 0, 10, 0], route.Steps.Select(s => (int)s.LevelGate));
        Assert.True(route.Steps[^1].IsTarget);
        Assert.Equal("4 quests · Lv 5–10", route.Summary.Text);
    }

    [Fact]
    public void A_quest_never_comes_before_what_it_requires_even_at_a_lower_level()
    {
        // B (Lv 1) needs A (Lv 20): A goes first however low B is.
        var catalog = Catalog(Needs(A, 20, JoinKind.All), Needs(B, 1, JoinKind.All, A), Needs(Target, 20, JoinKind.All, B));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog));

        Assert.Equal([A, B, Target], route.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void An_Any_join_takes_the_branch_with_fewest_quests_left_and_lists_the_other()
    {
        // Target takes A or B; A needs C and D (three quests), B stands alone (one).
        var catalog = Catalog(
            Needs(A, 1, JoinKind.All, C, D),
            Needs(B, 1, JoinKind.All),
            Needs(C, 1, JoinKind.All),
            Needs(D, 1, JoinKind.All),
            Needs(Target, 1, JoinKind.Any, A, B));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog));

        Assert.Equal([B, Target], route.Steps.Select(s => s.RowId));
        var alternative = Assert.Single(route.Steps[^1].Alternatives);
        Assert.Equal(A, alternative.RowId);
        Assert.Equal(3, alternative.RemainingCount);

        // One previous quest completed meets the join: nothing before the target.
        var met = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog, (C, QuestState.Completed), (D, QuestState.Completed), (A, QuestState.Completed)));
        Assert.Equal([Target], met.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void A_branch_through_a_locked_out_quest_is_taken_only_when_every_branch_has_one()
    {
        var catalog = Catalog(Needs(A, 1, JoinKind.All), Needs(B, 1, JoinKind.All, C), Needs(C, 1, JoinKind.All), Needs(Target, 1, JoinKind.Any, A, B));

        var avoided = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog, (A, QuestState.Foreclosed)));
        Assert.Equal([C, B, Target], avoided.Steps.Select(s => s.RowId));
        Assert.Equal(RouteOutcome.Route, avoided.Outcome);

        var stuck = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog, (A, QuestState.Foreclosed), (C, QuestState.Foreclosed)));
        Assert.Equal(RouteOutcome.LockedOut, stuck.Outcome);
    }

    [Fact]
    public void A_completed_quest_ends_the_walk_back()
    {
        // A is done although its own prerequisite B is not (a class intro the game never flags, say): B is not on the route.
        var catalog = Catalog(Needs(A, 1, JoinKind.All, B), Needs(B, 1, JoinKind.All), Needs(Target, 1, JoinKind.All, A));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog, (A, QuestState.Completed)));

        Assert.Equal([Target], route.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void A_target_no_quest_unlocks_has_no_route()
    {
        var catalog = Catalog(Quest(A));
        var route = UnlockRoute.Build(RouteTarget.ForJob("Gladiator", 0), catalog, States(catalog));

        Assert.Equal(RouteOutcome.NoQuest, route.Outcome);
        Assert.Empty(route.Steps);
    }

    [Fact]
    public void Level_gates_are_met_by_a_job_the_quest_admits()
    {
        var catalog = Catalog(Needs(A, 30, JoinKind.All), Needs(Target, 50, JoinKind.All, A));
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 40)) };

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog), levelOf: RouteLevels.For(snapshot, EvalContext.Default));

        Assert.True(route.Steps[0].LevelMet);
        Assert.Equal(50, route.Steps[1].LevelGate);
        Assert.False(route.Steps[1].LevelMet);
    }

    [Fact]
    public void Copy_route_is_a_Markdown_list_with_masked_names_and_no_character()
    {
        var catalog = Catalog(
            Needs(A, 49, JoinKind.All) with { Name = "The *Secret* Finale", Journal = new JournalRef(0, "Main Scenario", 2, "Seventh Umbral Era Main Scenario Quests", 1, "Genre", 1) },
            Needs(Target, 50, JoinKind.All, A) with { Name = "Out of the Blue" });

        var route = UnlockRoute.Build(RouteTarget.ForJob("Blue Mage", Target), catalog, States(catalog));
        var text = RouteMarkdown.Write(route, catalog, q => q.RowId == A ? "Main scenario quest (Lv 49)" : q.Name);

        Assert.Equal(
            "**Route to Blue Mage** · 2 quests · Lv 49–50 · MSQ: Seventh Umbral Era\n"
            + "\n*Main scenario: Seventh Umbral Era*\n"
            + "1. Lv 49 · Main scenario quest (Lv 49) (MSQ)\n"
            + "\n*After the main scenario*\n"
            + "2. Lv 50 · Out of the Blue — target",
            text);
        Assert.DoesNotContain("Secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Snapshot().Name, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pin_all_pins_the_unpinned_steps_in_route_order_and_Undo_takes_back_only_those()
    {
        var catalog = Catalog(Needs(A, 1, JoinKind.All), Needs(B, 2, JoinKind.All, A), Needs(Target, 3, JoinKind.All, B));
        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, States(catalog));
        var store = new FakePins { Pinned = [B] };

        var added = RoutePins.PinAll(route, store);

        Assert.Equal([A, Target], added);
        Assert.Equal([B, A, Target], store.Pinned);

        // The player unpins A meanwhile: Undo leaves it unpinned and removes only the target.
        store.TogglePin(A);
        Assert.Equal(1, RoutePins.Undo(added, store));
        Assert.Equal([B], store.Pinned);

        // Nobody to pin for (browse mode): nothing happens.
        Assert.Empty(RoutePins.PinAll(route, new FakePins { CanPin = false }));
    }

    [Fact]
    public void Targets_gather_every_quest_that_gives_the_same_thing()
    {
        var entries = new[]
        {
            new UniqueRewardEntry(A, RewardKind.SystemUnlock, 0, 0, "Retainers", Confidence.Curated, "curated"),
            new UniqueRewardEntry(B, RewardKind.SystemUnlock, 0, 0, "Retainers", Confidence.Curated, "curated"),
            new UniqueRewardEntry(C, RewardKind.SystemUnlock, 0, 0, "Gold Saucer", Confidence.Curated, "curated"),
            new UniqueRewardEntry(D, RewardKind.Minion, 7, 70, "Wind-up Airship", Confidence.Static, "sheet"),
            new UniqueRewardEntry(E, RewardKind.Minion, 7, 70, "Wind-up Airship", Confidence.Static, "sheet"),
        };

        Assert.Equal([A, B], RouteTarget.ForSystem(entries, "Retainers").QuestRowIds);
        Assert.Equal([A, B], RouteTarget.ForReward(entries[0], entries).QuestRowIds);
        Assert.Equal(RouteTargetKind.System, RouteTarget.ForReward(entries[0], entries).Kind);
        Assert.Equal([D, E], RouteTarget.ForReward(entries[3], entries).QuestRowIds);
        Assert.Equal(RouteTargetKind.Reward, RouteTarget.ForReward(entries[3]).Kind);
        Assert.Equal("Airship", RouteTarget.ForReward(entries[3], entries, "Airship").Label);

        // Quests you marked unique yourself share one placeholder name but are not one reward.
        var marked = new[]
        {
            new UniqueRewardEntry(A, RewardKind.Other, 0, 0, "Marked unique by you", Confidence.UserOverride, "user"),
            new UniqueRewardEntry(B, RewardKind.Other, 0, 0, "Marked unique by you", Confidence.UserOverride, "user"),
        };
        Assert.Equal([A], RouteTarget.ForReward(marked[0], marked).QuestRowIds);

        var catalog = Catalog(Quest(A) with { Rewards = [new RewardRef(RewardKind.Instance, 20040, 0, 1, "Nidhogg's Rage", 0)] }, Quest(B));
        var duty = RouteTarget.ForDuty(catalog, RewardKind.Instance, 20040, "Nidhogg's Rage", [new UniqueRewardEntry(B, RewardKind.Instance, 20040, 0, string.Empty, Confidence.Curated, "curated")]);
        Assert.Equal([A, B], duty.QuestRowIds);
        Assert.Equal(RouteTargetKind.Duty, duty.Kind);
    }

    private sealed class FakePins : IRoutePinStore
    {
        public List<uint> Pinned { get; init; } = [];

        public bool CanPin { get; init; } = true;

        public bool IsPinned(uint rowId) => Pinned.Contains(rowId);

        public bool TogglePin(uint rowId)
        {
            if (!CanPin)
            {
                return false;
            }

            if (!Pinned.Remove(rowId))
            {
                Pinned.Add(rowId);
            }

            return true;
        }
    }
}
