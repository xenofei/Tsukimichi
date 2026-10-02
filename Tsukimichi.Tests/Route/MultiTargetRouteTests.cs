using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Route;
using Tsukimichi.GameData;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Route;

/// <summary>
/// Routes to several targets (1.6.0, R6 D): the union of the parts' closures in one topological and level order, a
/// milestone per part (parts sharing a label reached with the last of them), done and locked-out parts left out, and
/// the factories for "Everything for a job", "All my pins", "This expansion's blues" and "Route to this" in My blues.
/// </summary>
public class MultiTargetRouteTests
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

    private static QuestRecord Side(uint rowId, byte level, params uint[] prereqs) =>
        Quest(rowId) with
        {
            Level = level,
            PreviousQuests = new Prereq(prereqs, JoinKind.All),
            Journal = new JournalRef(3, "Side", 60, "Sidequests", 1, "Genre", (int)rowId),
        };

    private static RouteTarget Part(string label, params uint[] quests) => new(RouteTargetKind.Quest, label, quests);

    [Fact]
    public void The_union_of_two_targets_is_one_order_by_prerequisites_then_level()
    {
        // Target needs C (Lv 20) which needs A (Lv 1); E (Lv 10) needs B (Lv 5). Both targets share nothing.
        var catalog = Catalog(Side(A, 1), Side(B, 5), Side(C, 20, A), Side(E, 10, B), Side(Target, 25, C));
        var target = RouteTarget.Union(RouteTargetKind.Pins, "both", [Part("first", Target), Part("second", E)]);

        var route = UnlockRoute.Build(target, catalog, States(catalog));

        Assert.Equal(RouteOutcome.Route, route.Outcome);
        Assert.Equal([A, B, E, C, Target], route.Steps.Select(s => s.RowId));
        Assert.Equal([new RouteTargetMilestone("second", E), new RouteTargetMilestone("first", Target)], route.TargetMilestones);
        Assert.Equal("second", route.Steps[2].TargetLabel);
        Assert.Equal("first", route.Steps[4].TargetLabel);
        Assert.True(route.Steps[2].IsTarget);
        Assert.True(route.Steps[4].IsTarget);
        Assert.False(route.Steps[3].IsTarget);
        Assert.Equal(Target, route.TargetRowId);
    }

    [Fact]
    public void A_shared_prerequisite_is_done_once()
    {
        var catalog = Catalog(Side(A, 1), Side(B, 5, A), Side(C, 6, A));
        var route = UnlockRoute.Build(RouteTarget.Union(RouteTargetKind.Pins, "x", [Part(string.Empty, B), Part(string.Empty, C)]), catalog, States(catalog));

        Assert.Equal([A, B, C], route.Steps.Select(s => s.RowId));
        Assert.Equal([B, C], route.TargetMilestones.Select(m => m.RowId));
        Assert.All(route.Steps, s => Assert.Equal(string.Empty, s.TargetLabel));
    }

    [Fact]
    public void Parts_sharing_a_label_are_one_milestone_on_the_last_of_them()
    {
        var catalog = Catalog(Side(A, 1), Side(B, 5, A), Side(C, 9, B), Side(D, 3));
        var target = RouteTarget.Union(RouteTargetKind.JobQuests, "job", [Part("ladder", A), Part("ladder", B), Part("ladder", C), Part("role", D)]);

        var route = UnlockRoute.Build(target, catalog, States(catalog));

        Assert.Equal([A, D, B, C], route.Steps.Select(s => s.RowId));
        Assert.Equal([new RouteTargetMilestone("role", D), new RouteTargetMilestone("ladder", C)], route.TargetMilestones);
        Assert.Equal(string.Empty, route.Steps[0].TargetLabel);
        Assert.Equal("ladder", route.Steps[3].TargetLabel);
    }

    [Fact]
    public void Done_and_locked_out_parts_are_left_out_and_all_done_is_already_done()
    {
        var catalog = Catalog(Side(A, 1), Side(B, 5), Side(C, 9));
        var target = RouteTarget.Union(RouteTargetKind.Pins, "x", [Part(string.Empty, A), Part(string.Empty, B), Part(string.Empty, C)]);

        var route = UnlockRoute.Build(target, catalog, States(catalog, (A, QuestState.Completed), (B, QuestState.Foreclosed)));
        Assert.Equal([C], route.Steps.Select(s => s.RowId));
        Assert.Equal(RouteOutcome.Route, route.Outcome);

        var done = UnlockRoute.Build(target, catalog, States(catalog, (A, QuestState.Completed), (B, QuestState.Completed), (C, QuestState.Completed)));
        Assert.Equal(RouteOutcome.AlreadyDone, done.Outcome);
        Assert.Empty(done.Steps);

        var unknown = UnlockRoute.Build(RouteTarget.Union(RouteTargetKind.Pins, "x", [Part(string.Empty, 999_999)]), catalog, States(catalog));
        Assert.Equal(RouteOutcome.NoQuest, unknown.Outcome);
    }

    [Fact]
    public void All_my_pins_routes_every_pin_in_one_order()
    {
        var catalog = Catalog(Side(A, 1), Side(B, 30, A), Side(C, 10));
        var route = UnlockRoute.Build(RouteTarget.ForPins([B, C]), catalog, States(catalog));

        Assert.Equal(RouteTargetKind.Pins, route.Target.Kind);
        Assert.Equal("all your pins", route.Target.Label);
        Assert.Equal([A, C, B], route.Steps.Select(s => s.RowId));
        Assert.Equal([C, B], route.TargetMilestones.Select(m => m.RowId));
    }

    [Fact]
    public void Everything_for_a_job_is_its_unlock_its_ladder_and_its_role_quests_up_to_the_cap()
    {
        const byte gladiator = 1;
        const byte paladin = 19;
        const uint gladiatorOnly = 2;
        const uint paladinOnly = 20;
        const uint tanks = 156;
        const uint gladiatorOne = 65600;
        const uint gladiatorTwo = 65601;
        const uint pledge = 65603;
        const uint paladinOne = 65604;
        const uint tankRole = 65607;
        const uint tankRoleLater = 65608;

        static QuestRecord JobQuest(uint rowId, byte level, uint genre, string genreName, uint required, uint category, string categoryName, params uint[] prereqs) => new()
        {
            RowId = rowId,
            QuestId = QuestRecord.ToQuestId(rowId),
            InternalId = $"Test_{rowId}",
            Name = $"Quest {rowId}",
            Journal = new JournalRef(JobLadder.ClassJobSectionId, "Section", genre / 10, categoryName, genre, genreName, (int)(genre << 16) | (int)(rowId - 65600)),
            Level = level,
            ClassJobRequired = required,
            ClassJobCategory = category,
            PreviousQuests = new Prereq(prereqs, JoinKind.All),
        };

        var catalog = QuestCatalog.Build(
        [
            JobQuest(gladiatorOne, 1, 156, "Gladiator Quests", gladiator, gladiatorOnly, "Job Quests"),
            JobQuest(gladiatorTwo, 15, 156, "Gladiator Quests", 0, gladiatorOnly, "Job Quests", gladiatorOne),
            JobQuest(pledge, 30, 176, "Paladin Quests", gladiator, gladiatorOnly, "Job Quests", gladiatorTwo),
            JobQuest(paladinOne, 35, 176, "Paladin Quests", paladin, paladinOnly, "Job Quests", pledge),
            JobQuest(tankRole, 70, 217, "Tank Role Quests (Shadowbringers)", 0, tanks, "Role Quests"),
            JobQuest(tankRoleLater, 80, 217, "Tank Role Quests (Shadowbringers)", 0, tanks, "Role Quests", tankRole),
        ]);
        var ladder = JobLadder.Build(
            catalog,
            [new LadderJob(gladiator, "gladiator", gladiator, 0, 1, false, false), new LadderJob(paladin, "paladin", gladiator, pledge, 1, false, false)],
            ClassJobCategoryLookup.FromMembership(
            [
                new(gladiatorOnly, [gladiator]),
                new(paladinOnly, [paladin]),
                new(tanks, [paladin]),
            ]));

        var target = RouteTarget.ForJobQuests(ladder, catalog, paladin, "Paladin", pledge, levelCap: 70);
        Assert.Equal(RouteTargetKind.JobQuests, target.Kind);
        Assert.Equal("everything for Paladin", target.Label);
        Assert.DoesNotContain(tankRoleLater, target.QuestRowIds);

        var route = UnlockRoute.Build(target, catalog, States(catalog, (gladiatorOne, QuestState.Completed)));
        Assert.Equal([gladiatorTwo, pledge, paladinOne, tankRole], route.Steps.Select(s => s.RowId));
        Assert.Equal(
            [new RouteTargetMilestone("Paladin unlocked", pledge), new RouteTargetMilestone("Paladin quests", paladinOne), new RouteTargetMilestone("Role quests", tankRole)],
            route.TargetMilestones);

        // No cap: the later role quest joins the route.
        var uncapped = RouteTarget.ForJobQuests(ladder, catalog, paladin, "Paladin", pledge);
        Assert.Contains(tankRoleLater, uncapped.QuestRowIds);
    }

    [Fact]
    public void This_expansions_blues_routes_every_quest_of_the_block()
    {
        var catalog = Catalog(Side(A, 1), Side(B, 5, A), Side(C, 3));
        var names = BlockerNames.Default with { Catalog = catalog };
        var entries = new[] { B, C }.Select(id => new PlanEntry(catalog.ByRowId[id], $"Quest {id}", QuestState.Blocked, string.Empty, [new PlanUnlock(UnlockKind.Other, string.Empty)])).ToArray();
        var block = new PlanExpansion(0, "A Realm Reborn", [new PlanZone(0, 0, entries)]);

        var target = RouteTarget.ForBlues(block);
        Assert.Equal("A Realm Reborn blues", target.Label);
        var route = UnlockRoute.Build(target, catalog, States(catalog), names);
        Assert.Equal([A, C, B], route.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void Route_to_this_takes_the_duty_or_system_a_blue_opens_else_the_quest()
    {
        var dungeon = new RewardRef(RewardKind.Instance, 4, 0, 1, "Halatali", 0);
        var catalog = Catalog(
            Side(A, 1) with { Rewards = [dungeon] },
            Side(B, 5) with { Rewards = [dungeon] },
            Side(C, 9),
            Side(D, 9));
        var entries = new[]
        {
            new UniqueRewardEntry(C, RewardKind.SystemUnlock, 0, 0, "Retainers", Confidence.Curated, "curated"),
            new UniqueRewardEntry(E, RewardKind.SystemUnlock, 0, 0, "Retainers", Confidence.Curated, "curated"),
        };

        var duty = RouteTarget.ForUnlockQuest(catalog.ByRowId[A], catalog, entries, "Halatali");
        Assert.Equal(RouteTargetKind.Duty, duty.Kind);
        Assert.Equal([A, B], duty.QuestRowIds);

        var system = RouteTarget.ForUnlockQuest(catalog.ByRowId[C], catalog, entries, "Retainers");
        Assert.Equal(RouteTargetKind.System, system.Kind);
        Assert.Equal([C, E], system.QuestRowIds);

        var quest = RouteTarget.ForUnlockQuest(catalog.ByRowId[D], catalog, entries, "Quest D");
        Assert.Equal(RouteTargetKind.Quest, quest.Kind);
        Assert.Equal([D], quest.QuestRowIds);
    }
}
