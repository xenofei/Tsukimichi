using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// Curated extra prerequisites (<c>curated/extra_prerequisites.json</c>, 1.5.0 Gates) join the previous quests through
/// <see cref="QuestCatalog.PrerequisitesOf"/> as an "all" list, so the evaluator, the blocker text, the reverse index,
/// the path and the unlock route all see them.
/// </summary>
public class ExtraPrerequisiteTests
{
    private static QuestRecord Msq(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(0, "Main Scenario", 1, "Shadowbringers", 1, "Shadowbringers", (int)rowId) };

    private static QuestRecord Side(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Side Quests", 60, "Role Quests", 1, "Tank Role Quests", (int)rowId) };

    /// <summary>Target follows A in the sheet; the curated file adds the main scenario quest B.</summary>
    private static QuestCatalog Princess(out QuestRecord target, params uint[] extras)
    {
        target = Side(Target, "The Princess and Her Knight") with { PreviousQuests = new Prereq([A], JoinKind.All) };
        return QuestCatalog.Build(
            [Side(A, "Defined by Loss"), Msq(B, "A Fitting Payment"), Msq(C, "Acht-la Ormh Inn"), target],
            new Dictionary<uint, uint[]> { [Target] = extras.Length == 0 ? [B] : extras });
    }

    [Fact]
    public void An_extra_extends_the_previous_quests_as_an_all_list()
    {
        var catalog = Princess(out var target);

        var prereq = catalog.PrerequisitesOf(target);
        Assert.Equal([A, B], prereq.QuestIds);
        Assert.Equal(JoinKind.All, prereq.Join);
        Assert.Empty(prereq.Required);
        Assert.Equal([B], catalog.ExtraPrerequisitesOf(Target));

        // A quest without extras keeps its own record; an edited copy is worked out with the held extras.
        var plain = catalog.GetByRowId(A)!;
        Assert.Same(plain.PreviousQuests, catalog.PrerequisitesOf(plain));
        Assert.Empty(catalog.ExtraPrerequisitesOf(A));
        Assert.Equal([A, B], catalog.PrerequisitesOf(target with { Level = 76 }).QuestIds);
    }

    [Fact]
    public void The_evaluator_blocks_until_the_extra_is_done_and_names_it()
    {
        var catalog = Princess(out var target);

        var previous = Only(RequirementEvaluator.Evaluate(target, Snapshot(A), catalog, EvalContext.Default), RequirementKind.PreviousQuests);
        Assert.False(previous.Met);
        Assert.Equal("1 prerequisite left", previous.Detail);
        Assert.Equal([A], Assert.IsType<PreviousQuestsRequirement>(previous.Req).DoneIds!);

        var blocked = StateResolver.Resolve(target, Snapshot(A), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal("after MSQ: A Fitting Payment", BlockerText.For(blocked, target, BlockerNames.Default with { Catalog = catalog }));

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(A, B), catalog, EvalContext.Default).State);
    }

    [Fact]
    public void A_quest_with_no_previous_quest_no_longer_reads_Ready_at_its_level()
    {
        // The Hero's Journey and Shadow Walk with Me had no previous quest at all.
        var target = Side(Target, "Shadow Walk with Me");
        var catalog = QuestCatalog.Build([Side(A, "To Have Loved and Lost"), Side(B, "The Soul of Temperance"), target], new Dictionary<uint, uint[]> { [Target] = [A, B] });

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(), QuestCatalog.Build([Side(A, "x"), Side(B, "y"), target]), EvalContext.Default).State);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(target, Snapshot(), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(target, Snapshot(A), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(A, B), catalog, EvalContext.Default).State);
    }

    [Fact]
    public void Under_an_any_join_an_extra_is_needed_beside_one_alternative()
    {
        var target = Side(Target, "Royal Rumblings") with { PreviousQuests = new Prereq([A, B], JoinKind.Any) };
        var catalog = QuestCatalog.Build([Side(A, "The Gridanian Envoy"), Side(B, "The Ul'dahn Envoy"), Msq(C, "Over the Wall"), target], new Dictionary<uint, uint[]> { [Target] = [C] });

        var prereq = catalog.PrerequisitesOf(target);
        Assert.Equal([A, B, C], prereq.QuestIds);
        Assert.Equal(JoinKind.Any, prereq.Join);
        Assert.Equal([C], prereq.Required);

        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(target, Snapshot(A), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(B, C), catalog, EvalContext.Default).State);
    }

    [Fact]
    public void Extras_and_accept_conditions_merge_without_repeats()
    {
        var target = Side(Target, "Big Body, Big Beauty") with { PreviousQuests = new Prereq([A], JoinKind.All), AcceptConditions = [B] };
        var catalog = QuestCatalog.Build([Side(A, "Scaling Up Demand"), Msq(B, "The Skyruin"), Msq(C, "The Feat of the Brotherhood"), target], new Dictionary<uint, uint[]> { [Target] = [B, C, A] });

        Assert.Equal([A, B, C], catalog.PrerequisitesOf(target).QuestIds);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(target, Snapshot(A, B), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(A, B, C), catalog, EvalContext.Default).State);
    }

    [Fact]
    public void Ids_that_are_no_quest_of_the_catalog_are_ignored()
    {
        var target = Side(Target, "Target") with { PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = QuestCatalog.Build([Side(A, "A"), target], new Dictionary<uint, uint[]> { [Target] = [99999, Target], [70000] = [A] });

        Assert.Same(target.PreviousQuests, catalog.PrerequisitesOf(target));
        Assert.Empty(catalog.ExtraPrerequisitesOf(Target));
        Assert.Empty(catalog.ExtraPrerequisitesOf(70000));
    }

    [Fact]
    public void Completing_the_extra_re_evaluates_its_dependent()
    {
        var catalog = Princess(out _);

        Assert.Contains(Target, ReversePrereqIndex.Build(catalog).Dependents(B));
    }

    [Fact]
    public void The_path_and_the_unlock_route_walk_through_the_extra()
    {
        // B (the extra) has its own prerequisite D.
        var target = Side(Target, "The Princess and Her Knight") with { Level = 76, PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = QuestCatalog.Build(
            [
                Side(A, "Defined by Loss") with { Level = 76 },
                Msq(B, "A Fitting Payment") with { Level = 73, PreviousQuests = new Prereq([D], JoinKind.All) },
                Msq(D, "The Key to Paradise") with { Level = 73 },
                target,
            ],
            new Dictionary<uint, uint[]> { [Target] = [B] });
        var states = catalog.All.ToDictionary(q => q.RowId, q => new QuestEvaluation(q.RowId == A ? QuestState.Completed : QuestState.Blocked, [], null, null, null));

        Assert.Equal([A, D, B, Target], PathFinder.PathTo(Target, catalog, states).Select(s => s.RowId));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, states);
        Assert.Equal([D, B, Target], route.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void Coverage_follows_all_joins_and_only_what_every_alternative_needs()
    {
        // Target needs A (all) and one of B or C; B and C both need D, only B needs E.
        var target = Side(Target, "Target") with { PreviousQuests = new Prereq([A], JoinKind.All) };
        var join = Side(65605, "Join") with { PreviousQuests = new Prereq([B, C], JoinKind.Any) };
        var catalog = QuestCatalog.Build(
            [
                Side(A, "A") with { PreviousQuests = new Prereq([65605], JoinKind.All) },
                join,
                Side(B, "B") with { PreviousQuests = new Prereq([D, E], JoinKind.All) },
                Side(C, "C") with { PreviousQuests = new Prereq([D], JoinKind.All) },
                Side(D, "D"),
                Side(E, "E"),
                Side(65606, "Extra"),
                target,
            ],
            new Dictionary<uint, uint[]> { [Target] = [65606] });

        Assert.True(PrerequisiteCoverage.Requires(catalog, Target, A));
        Assert.True(PrerequisiteCoverage.Requires(catalog, Target, 65606));
        Assert.True(PrerequisiteCoverage.Requires(catalog, Target, D));
        Assert.False(PrerequisiteCoverage.Requires(catalog, Target, B));
        Assert.False(PrerequisiteCoverage.Requires(catalog, Target, E));
        Assert.False(PrerequisiteCoverage.Requires(catalog, D, Target));

        var gaps = PrerequisiteCoverage.Check(catalog, [(Target, 65606), (Target, E), (99999, A), (Target, 99999)]);
        Assert.Equal(
            [(Target, E, PrerequisiteCoverage.Gap.NotRequired), (99999u, A, PrerequisiteCoverage.Gap.UnknownQuest), (Target, 99999u, PrerequisiteCoverage.Gap.UnknownRequired)],
            gaps.Select(g => (g.QuestRowId, g.RequiredRowId, g.Gap)));
    }
}

/// <summary>The shipped extras over the frozen catalog: the gates the research found reading Ready too early.</summary>
public class ExtraPrerequisiteFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Paladin = 19;
    private const uint DefinedByLoss = 68781;
    private const uint ThePrincessAndHerKnight = 68782;
    private const uint AFittingPayment = 68850;
    private const uint TheHerosJourney = 69522;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context => new() { ClassJobs = fixture.Bundle.Jobs };

    private static CharacterSnapshot Paladin80(params uint[] completedRowIds) => Snapshot(completedRowIds) with
    {
        CurrentJob = Paladin,
        JobLevels = Levels((Paladin, 80)),
    };

    [Fact]
    public void The_Princess_and_Her_Knight_waits_for_A_Fitting_Payment()
    {
        var quest = Catalog.GetByRowId(ThePrincessAndHerKnight)!;
        Assert.Equal("The Princess and Her Knight", quest.Name);
        Assert.Equal([DefinedByLoss], quest.PreviousQuests.QuestIds);
        Assert.Empty(quest.AcceptConditions);
        Assert.Equal("A Fitting Payment", Catalog.GetByRowId(AFittingPayment)!.Name);
        Assert.Equal([AFittingPayment], Catalog.ExtraPrerequisitesOf(ThePrincessAndHerKnight));
        Assert.Equal([DefinedByLoss, AFittingPayment], Catalog.PrerequisitesOf(quest).QuestIds);

        var blocked = StateResolver.Resolve(quest, Paladin80(DefinedByLoss), Catalog, Context);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal("after MSQ: A Fitting Payment", BlockerText.For(blocked, quest, fixture.Bundle.BlockerNames()));

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Paladin80(DefinedByLoss, AFittingPayment), Catalog, Context).State);
        Assert.Contains(ThePrincessAndHerKnight, ReversePrereqIndex.Build(Catalog).Dependents(AFittingPayment));
    }

    [Fact]
    public void The_route_to_The_Princess_and_Her_Knight_takes_A_Fitting_Payment()
    {
        // Everything done but the extra and the quest itself.
        var states = Catalog.All.ToDictionary(
            q => q.RowId,
            q => new QuestEvaluation(q.RowId is AFittingPayment or ThePrincessAndHerKnight ? QuestState.Blocked : QuestState.Completed, [], null, null, null));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(ThePrincessAndHerKnight, "target"), Catalog, states);
        Assert.Equal([AFittingPayment, ThePrincessAndHerKnight], route.Steps.Select(s => s.RowId));
        Assert.Equal([AFittingPayment, ThePrincessAndHerKnight], PathFinder.PathTo(ThePrincessAndHerKnight, Catalog, states).Select(s => s.RowId).TakeLast(2));
    }

    [Fact]
    public void The_Heros_Journey_no_longer_reads_Ready_at_its_level()
    {
        var quest = Catalog.GetByRowId(TheHerosJourney)!;
        Assert.True(quest.PreviousQuests.IsEmpty);
        Assert.Equal(6, Catalog.PrerequisitesOf(quest).QuestIds.Length);

        var state = StateResolver.Resolve(quest, Paladin80(), Catalog, Context);
        Assert.Equal(QuestState.Blocked, state.State);
        Assert.False(Only(state.Requirements, RequirementKind.PreviousQuests).Met);
    }

    [Fact]
    public void Every_shipped_extra_reaches_the_catalog()
    {
        // The curated file loads into the fixture's catalog whole: every key and id is a quest, none dropped.
        var curated = fixture.Curated.ExtraPrerequisites;
        Assert.NotEmpty(curated);
        Assert.All(curated, kv => Assert.Equal(kv.Value.Requires, Catalog.ExtraPrerequisitesOf(kv.Key)));
        Assert.All(curated, kv => Assert.All(kv.Value.Requires, id => Assert.Contains(id, Catalog.PrerequisitesOf(Catalog.GetByRowId(kv.Key)!).QuestIds)));
    }
}
