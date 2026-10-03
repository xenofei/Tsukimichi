using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// Accept conditions that name a quest (QuestAcceptAdditionCondition) are judged as previous quests under the quest's
/// own join (<see cref="QuestCatalog.PrerequisitesOf"/>); any other value stays listed and not checked.
/// </summary>
public class AcceptConditionTests
{
    /// <summary>A value of another sheet (Operation Archon's 17), never a quest row.</summary>
    private const uint NotAQuest = 17;

    private static QuestRecord Msq(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(0, "Main Scenario", 1, "Heavensward", 1, "Heavensward", (int)rowId) };

    private static QuestRecord Side(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Side Quests", 60, "Sidequests", 1, "Genre", (int)rowId) };

    /// <summary>Target follows A and also needs B through an accept condition, plus one value that is no quest.</summary>
    private static QuestCatalog KillingArt(out QuestRecord target)
    {
        target = Side(Target, "The Killing Art") with { PreviousQuests = new Prereq([A], JoinKind.All), AcceptConditions = [B, NotAQuest] };
        return Catalog(Side(A, "Thicker than Blood"), Msq(B, "Over the Wall"), target);
    }

    [Fact]
    public void A_quest_valued_condition_extends_the_previous_quests_and_the_rest_stay_unchecked()
    {
        var catalog = KillingArt(out var target);

        var prereq = catalog.PrerequisitesOf(target);
        Assert.Equal([A, B], prereq.QuestIds);
        Assert.Equal(JoinKind.All, prereq.Join);
        Assert.Equal([NotAQuest], catalog.UncheckedAcceptConditions(target));

        // A quest without accept conditions keeps its own record; so does one whose values name no quest.
        var plain = catalog.GetByRowId(A)!;
        Assert.Same(plain.PreviousQuests, catalog.PrerequisitesOf(plain));
        var other = target with { AcceptConditions = [NotAQuest] };
        Assert.Same(other.PreviousQuests, catalog.PrerequisitesOf(other));

        // An edited copy is worked out from its own values, not the held record's.
        Assert.Equal([A, B], catalog.PrerequisitesOf(target with { AcceptConditions = [C, B, B] }).QuestIds);
    }

    [Fact]
    public void The_evaluator_blocks_until_the_accept_quest_is_done_and_names_it()
    {
        var catalog = KillingArt(out var target);

        var results = RequirementEvaluator.Evaluate(target, Snapshot(A), catalog, EvalContext.Default);
        var previous = Only(results, RequirementKind.PreviousQuests);
        Assert.False(previous.Met);
        Assert.Equal("1 prerequisite left", previous.Detail);
        var req = Assert.IsType<PreviousQuestsRequirement>(previous.Req);
        Assert.Equal([A, B], req.QuestIds);
        Assert.Equal([A], req.DoneIds!);

        // Only the value that is no quest is left as an accept condition, met and not checked.
        var accept = Only(results, RequirementKind.AcceptCondition);
        Assert.True(accept.Met);
        Assert.Equal("1 accept condition not checked", accept.Detail);
        Assert.Equal([NotAQuest], Assert.IsType<AcceptConditionRequirement>(accept.Req).ConditionIds);

        var blocked = StateResolver.Resolve(target, Snapshot(A), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, blocked.State);
        var names = BlockerNames.Default with { Catalog = catalog };
        Assert.Equal("after MSQ: Over the Wall", BlockerText.For(blocked, target, names));

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(A, B), catalog, EvalContext.Default).State);
    }

    [Fact]
    public void A_lone_accept_quest_reads_like_a_single_previous_quest()
    {
        var target = Side(Target, "You Otter Be There") with { AcceptConditions = [B] };
        var catalog = Catalog(Side(B, "It Could Happen to You"), target);

        var results = RequirementEvaluator.Evaluate(target, Snapshot(), catalog, EvalContext.Default);
        Assert.Equal("needs It Could Happen to You", Only(results, RequirementKind.PreviousQuests).Detail);
        Assert.DoesNotContain(results, r => r.Req.Kind == RequirementKind.AcceptCondition);

        var done = RequirementEvaluator.Evaluate(target, Snapshot(B), catalog, EvalContext.Default);
        Assert.Equal("It Could Happen to You done", Only(done, RequirementKind.PreviousQuests).Detail);
    }

    [Fact]
    public void An_any_join_keeps_its_join_when_the_conditions_repeat_its_previous_quests()
    {
        // Royal Rumblings: any one of the three envoy quests, listed again as accept conditions.
        var target = Side(Target, "Royal Rumblings") with { PreviousQuests = new Prereq([A, B, C], JoinKind.Any), AcceptConditions = [A, B, C] };
        var catalog = Catalog(Side(A, "The Gridanian Envoy"), Side(B, "The Ul'dahn Envoy"), Side(C, "The Lominsan Envoy"), target);

        Assert.Same(target.PreviousQuests, catalog.PrerequisitesOf(target));
        var r = Only(RequirementEvaluator.Evaluate(target, Snapshot(B), catalog, EvalContext.Default), RequirementKind.PreviousQuests);
        Assert.True(r.Met);
        Assert.Equal("one of 3 prerequisites done", r.Detail);
    }

    [Fact]
    public void An_accept_quest_outside_an_any_join_is_needed_beside_one_of_its_previous_quests()
    {
        // Any one of two city quests, and an accept condition naming a third quest: the condition is no alternative.
        var target = Side(Target, "Royal Rumblings") with { PreviousQuests = new Prereq([A, B], JoinKind.Any), AcceptConditions = [C] };
        var catalog = Catalog(Side(A, "The Gridanian Envoy"), Side(B, "The Ul'dahn Envoy"), Side(C, "Over the Wall"), target);

        var prereq = catalog.PrerequisitesOf(target);
        Assert.Equal([A, B, C], prereq.QuestIds);
        Assert.Equal(JoinKind.Any, prereq.Join);
        Assert.Equal([C], prereq.Required);

        // One envoy quest alone is not enough: the accept quest is a second, All requirement.
        var results = RequirementEvaluator.Evaluate(target, Snapshot(A), catalog, EvalContext.Default);
        var previous = results.Where(r => r.Req.Kind == RequirementKind.PreviousQuests).ToArray();
        Assert.Equal(2, previous.Length);
        var alternatives = Assert.IsType<PreviousQuestsRequirement>(previous[0].Req);
        Assert.Equal([A, B], alternatives.QuestIds);
        Assert.Equal(JoinKind.Any, alternatives.Join);
        Assert.True(previous[0].Met);
        var required = Assert.IsType<PreviousQuestsRequirement>(previous[1].Req);
        Assert.Equal([C], required.QuestIds);
        Assert.Equal(JoinKind.All, required.Join);
        Assert.False(previous[1].Met);
        Assert.Equal("needs Over the Wall", previous[1].Detail);

        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(target, Snapshot(A), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(target, Snapshot(C), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(target, Snapshot(B, C), catalog, EvalContext.Default).State);

        // The path and the route take one branch and the accept quest; only the other branch is an alternative.
        var states = catalog.All.ToDictionary(q => q.RowId, q => new QuestEvaluation(q.RowId == A ? QuestState.Completed : QuestState.Blocked, [], null, null, null));
        var path = PathFinder.PathTo(Target, catalog, states);
        Assert.Equal([A, C, Target], path.Select(s => s.RowId));
        var join = Assert.Single(PathFinder.Alternatives(path, catalog, states));
        Assert.Equal([B], join.Alternatives.Select(a => a.RowId));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, states);
        Assert.Equal([C, Target], route.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void Completing_the_accept_quest_re_evaluates_its_dependent()
    {
        var catalog = KillingArt(out _);

        Assert.Contains(Target, ReversePrereqIndex.Build(catalog).Dependents(B));
    }

    [Fact]
    public void The_path_and_the_unlock_route_walk_through_the_accept_quest()
    {
        // B (the accept quest) has its own prerequisite D.
        var target = Side(Target, "The Killing Art") with { Level = 80, PreviousQuests = new Prereq([A], JoinKind.All), AcceptConditions = [B] };
        var catalog = Catalog(
            Side(A, "Thicker than Blood") with { Level = 80 },
            Msq(B, "Over the Wall") with { Level = 60, PreviousQuests = new Prereq([D], JoinKind.All) },
            Msq(D, "Before the Wall") with { Level = 60 },
            target);
        var states = catalog.All.ToDictionary(q => q.RowId, q => new QuestEvaluation(q.RowId == A ? QuestState.Completed : QuestState.Blocked, [], null, null, null));

        Assert.Equal([A, D, B, Target], PathFinder.PathTo(Target, catalog, states).Select(s => s.RowId));

        var route = UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, states);
        Assert.Equal([D, B, Target], route.Steps.Select(s => s.RowId));
    }
}

/// <summary>Accept conditions over the frozen catalog: The Killing Art (Reaper, Lv 80) also needs the main scenario's Over the Wall.</summary>
public class AcceptConditionFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Reaper = 39;
    private const uint ThickerThanBlood = 69613;
    private const uint TheKillingArt = 69614;
    private const uint OverTheWall = 67119;
    private const uint RoyalRumblings = 70858;
    private const uint TheGridanianEnvoy = 66043;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context => new() { ClassJobs = fixture.Bundle.Jobs };

    private static CharacterSnapshot Reaper80(params uint[] completedRowIds) => Snapshot(completedRowIds) with
    {
        CurrentJob = Reaper,
        JobLevels = Levels((Reaper, 80)),
    };

    [Fact]
    public void The_Killing_Art_is_blocked_until_Over_the_Wall_is_done()
    {
        var quest = Catalog.GetByRowId(TheKillingArt)!;
        Assert.Equal("The Killing Art", quest.Name);
        Assert.Equal([ThickerThanBlood], quest.PreviousQuests.QuestIds);
        Assert.Equal([OverTheWall], quest.AcceptConditions);
        Assert.Equal("Over the Wall", Catalog.GetByRowId(OverTheWall)!.Name);

        var blocked = StateResolver.Resolve(quest, Reaper80(ThickerThanBlood), Catalog, Context);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal("after MSQ: Over the Wall", BlockerText.For(blocked, quest, fixture.Bundle.BlockerNames()));
        Assert.DoesNotContain(blocked.Requirements, r => r.Req.Kind == RequirementKind.AcceptCondition);

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Reaper80(ThickerThanBlood, OverTheWall), Catalog, Context).State);
    }

    [Fact]
    public void Royal_Rumblings_still_needs_only_one_envoy_quest()
    {
        var quest = Catalog.GetByRowId(RoyalRumblings)!;
        Assert.Equal(JoinKind.Any, Catalog.PrerequisitesOf(quest).Join);
        Assert.Empty(Catalog.PrerequisitesOf(quest).Required);

        var r = Only(RequirementEvaluator.Evaluate(quest, Snapshot(TheGridanianEnvoy), Catalog, Context), RequirementKind.PreviousQuests);
        Assert.True(r.Met);
    }

    [Fact]
    public void Forty_seven_quests_gain_a_prerequisite_and_twelve_values_stay_unchecked()
    {
        // 57 quests carry accept conditions. 47 hold quest ids only: all but Royal Rumblings (which repeats its own
        // previous quests) gain them, and so does In the Name of the Light (a quest and a value of another sheet). The
        // twelve values that are no quest sit on ten quests. (The curated extras extend others; ExtraPrerequisiteTests.)
        Assert.Equal(57, Catalog.All.Count(q => q.AcceptConditions.Length > 0));
        var extended = Catalog.All.Where(q => q.AcceptConditions.Length > 0 && !ReferenceEquals(Catalog.PrerequisitesOf(q), q.PreviousQuests)).ToArray();
        Assert.Equal(47, extended.Length);
        Assert.All(extended, q => Assert.Equal(q.PreviousQuests.Join, Catalog.PrerequisitesOf(q).Join));
        Assert.Equal(12, Catalog.All.Sum(q => Catalog.UncheckedAcceptConditions(q).Length));
        Assert.Equal(10, Catalog.All.Count(q => Catalog.UncheckedAcceptConditions(q).Length > 0));
    }
}
