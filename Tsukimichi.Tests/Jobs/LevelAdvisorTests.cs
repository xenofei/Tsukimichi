using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.JobLadders;

/// <summary>
/// The level advisor (1.9.0, R6 C): Blocked quests held back by the level and the class or job alone, grouped per job by
/// the level they need, main scenario quests first, then unlock quests; and the next main scenario quest's level gate.
/// </summary>
public sealed class LevelAdvisorTests
{
    private const byte Lancer = 4;
    private const byte Conjurer = 6;
    private const byte Dragoon = 22;
    private const byte WhiteMage = 24;
    private const uint Combat = 200;

    private static readonly JournalRef Side = new(3, "Side", 30, "Side", 300, "Side", 0);
    private static readonly JournalRef Msq = new(0, "Main Scenario", 1, "Seventh Umbral Era", 1, "Seventh Umbral Era", 0);

    private static QuestRecord SideQuest(uint rowId, byte level, int sort) =>
        Fixture.Quest(rowId) with { Level = level, Journal = Side with { SortKey = sort }, ClassJobCategory = Combat };

    // A Dragoon job quest at 53, a Lancer class quest at 56 (Dragoon's line by its base class), the main scenario at 56,
    // an unlock quest at 56, a White Mage job quest at 54; and what never counts: a quest that also needs a previous
    // quest, a repeatable, a quest above the level cap and a quest the character already can take.
    private static readonly QuestRecord DragoonQuest = SideQuest(66001, 53, 1) with { ClassJobCategory = 0, ClassJobRequired = Dragoon };
    private static readonly QuestRecord LancerQuest = SideQuest(66002, 56, 2) with { ClassJobCategory = 0, ClassJobRequired = Lancer };
    private static readonly QuestRecord StoryQuest = Fixture.Quest(66003) with { Level = 56, Journal = Msq with { SortKey = 1 }, ClassJobCategory = Combat };
    private static readonly QuestRecord UnlockQuest = SideQuest(66004, 56, 4);
    private static readonly QuestRecord WhiteMageQuest = SideQuest(66005, 54, 5) with { ClassJobCategory = 0, ClassJobRequired = WhiteMage };
    private static readonly QuestRecord NeedsPrevious = SideQuest(66006, 56, 6) with { PreviousQuests = new Prereq([66099u], JoinKind.All) };
    private static readonly QuestRecord Repeatable = SideQuest(66007, 56, 7) with { IsRepeatable = true, RepeatInterval = 1 };
    private static readonly QuestRecord AboveCap = SideQuest(66008, 101, 8);
    private static readonly QuestRecord AlreadyOpen = SideQuest(66009, 30, 9);
    private static readonly QuestRecord Missing = SideQuest(66099, 1, 99) with { Journal = Side with { SortKey = 99 } };

    private static readonly QuestCatalog Catalog = Fixture.Catalog(
        DragoonQuest, LancerQuest, StoryQuest, UnlockQuest, WhiteMageQuest, NeedsPrevious, Repeatable, AboveCap, AlreadyOpen, Missing);

    private static readonly IReadOnlySet<uint> Unlocks = new HashSet<uint> { UnlockQuest.RowId };

    private static readonly EvalContext Context = EvalContext.Default with
    {
        ClassJobs = new Fixture.Jobs((Combat, [Lancer, Conjurer, Dragoon, WhiteMage])),
        ParentJob = job => job switch { Dragoon => Lancer, WhiteMage => Conjurer, _ => job },
    };

    private static CharacterSnapshot Character(byte current = Dragoon) => Fixture.Snapshot() with
    {
        CurrentJob = current,
        LevelCap = 100,
        JobLevels = Fixture.Levels((Lancer, 52), (Dragoon, 52), (Conjurer, 30), (WhiteMage, 30)),
        // Missing (the previous quest of NeedsPrevious) stays undone on purpose.
        CompletedBits = Fixture.Bits(),
    };

    private static IReadOnlyList<JobLevelAdvice> Advise(CharacterSnapshot snapshot, params byte[] jobs) =>
        LevelAdvisor.Compute(Catalog, StateResolver.ResolveAll(Catalog, snapshot, Context), snapshot, Context, jobs, Unlocks);

    [Fact]
    public void Levelling_a_job_opens_its_quests_grouped_by_level_main_scenario_and_unlocks_first()
    {
        var dragoon = Assert.Single(Advise(Character(), Dragoon));
        Assert.Equal(Dragoon, dragoon.Job);
        Assert.Equal(52, dragoon.Level);
        Assert.Equal([53, 56], dragoon.Steps.Select(s => (int)s.Level));

        var first = dragoon.Steps[0];
        Assert.Equal([DragoonQuest.RowId], first.Quests.Select(q => q.RowId));
        Assert.Equal((0, 0), (first.MainScenario, first.Unlocks));
        Assert.Same(first, dragoon.Next);

        // The Lancer class quest counts for Dragoon (they share a level); the order is MSQ, unlock, the rest.
        var second = dragoon.Steps[1];
        Assert.Equal([StoryQuest.RowId, UnlockQuest.RowId, LancerQuest.RowId], second.Quests.Select(q => q.RowId));
        Assert.Equal((1, 1), (second.MainScenario, second.Unlocks));
        Assert.Equal(4, dragoon.Total);
    }

    [Fact]
    public void Each_job_counts_only_what_it_can_take()
    {
        var advice = Advise(Character(), WhiteMage, Dragoon);
        Assert.Equal([WhiteMage, Dragoon], advice.Select(a => a.Job));

        // White Mage at 30: its own job quest at 54 and the two quests any combat job takes at 56; never the Dragoon
        // or Lancer quests, the repeatable, the quest that also needs a previous quest or the one above the cap.
        var whiteMage = advice[0];
        Assert.Equal([54, 56], whiteMage.Steps.Select(s => (int)s.Level));
        Assert.Equal([WhiteMageQuest.RowId], whiteMage.Steps[0].Quests.Select(q => q.RowId));
        Assert.Equal([StoryQuest.RowId, UnlockQuest.RowId], whiteMage.Steps[1].Quests.Select(q => q.RowId));
    }

    [Fact]
    public void Only_quests_held_back_by_the_level_and_the_job_alone_are_level_gated()
    {
        var snapshot = Character();
        var gated = LevelAdvisor.LevelGated(Catalog, StateResolver.ResolveAll(Catalog, snapshot, Context)).Select(q => q.RowId).ToHashSet();
        Assert.Equal(new HashSet<uint> { DragoonQuest.RowId, LancerQuest.RowId, StoryQuest.RowId, UnlockQuest.RowId, WhiteMageQuest.RowId }, gated);
    }

    [Fact]
    public void A_job_not_levelled_gets_no_advice_and_one_at_the_cap_gets_no_steps()
    {
        Assert.Empty(Advise(Character(), 19));

        var capped = Character() with { JobLevels = Fixture.Levels((Lancer, 100), (Dragoon, 100), (Conjurer, 30), (WhiteMage, 30)) };
        var dragoon = Assert.Single(Advise(capped, Dragoon));
        Assert.Empty(dragoon.Steps);
        Assert.Null(dragoon.Next);
    }

    [Fact]
    public void The_next_main_scenario_quest_waits_for_a_level_on_the_current_job()
    {
        var snapshot = Character();
        var gate = LevelAdvisor.MsqGate(Catalog, StateResolver.ResolveAll(Catalog, snapshot, Context), snapshot, Context);
        Assert.NotNull(gate);
        Assert.Same(StoryQuest, gate.Quest);
        Assert.Equal((56, Dragoon, 52), (gate.Level, gate.Job, gate.JobLevel));
    }

    [Fact]
    public void A_main_scenario_quest_open_or_held_by_something_else_has_no_level_gate()
    {
        // Open on a job at 60.
        var high = Character() with { JobLevels = Fixture.Levels((Lancer, 60), (Dragoon, 60), (Conjurer, 30), (WhiteMage, 30)) };
        Assert.Null(LevelAdvisor.MsqGate(Catalog, StateResolver.ResolveAll(Catalog, high, Context), high, Context));

        // Held by a previous quest too.
        var held = StoryQuest with { PreviousQuests = new Prereq([Missing.RowId], JoinKind.All) };
        var catalog = Fixture.Catalog(held, Missing);
        var snapshot = Character();
        Assert.Null(LevelAdvisor.MsqGate(catalog, StateResolver.ResolveAll(catalog, snapshot, Context), snapshot, Context));
    }

    [Fact]
    public void A_current_job_that_cannot_take_the_quest_measures_the_best_one_that_can()
    {
        var conjurerOnly = StoryQuest with { ClassJobCategory = 0, ClassJobRequired = Conjurer };
        var catalog = Fixture.Catalog(conjurerOnly);
        var snapshot = Character();
        var gate = LevelAdvisor.MsqGate(catalog, StateResolver.ResolveAll(catalog, snapshot, Context), snapshot, Context);
        Assert.NotNull(gate);
        Assert.Equal((56, Conjurer, 30), (gate.Level, gate.Job, gate.JobLevel));
    }
}
