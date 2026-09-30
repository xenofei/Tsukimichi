using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The requirement kinds 0.6.2 added on synthetic quests: custom delivery satisfaction rank, Delivery Moogle carrier
/// level, and the festival phase window on the seasonal check (evaluator, resolver rule 3 and the blocker phrases).
/// The fixture-backed cases (the 16 satisfaction quests, the 17 postmoogle quests, Hatching-tide 2014) are in
/// <see cref="DeliveryAndPhaseFixtureTests"/>.
/// </summary>
public class DeliveryAndPhaseRequirementTests
{
    private const byte Mnaago = 2;
    private const ushort HatchingTide = 10;

    private static readonly BlockerNames Names = new()
    {
        SatisfactionNpc = id => id == Mnaago ? "M'naago" : string.Empty,
    };

    private static readonly EvalContext Context = new() { SatisfactionNpcName = id => id == Mnaago ? "M'naago" : string.Empty };

    private static QuestRecord Delivery() => Quest(Target) with { SatisfactionNpc = Mnaago, SatisfactionLevel = 4 };

    private static QuestRecord Postmoogle() => Quest(Target) with { CarrierLevel = 7 };

    private static QuestRecord Chapter2() => Quest(Target) with { Festival = HatchingTide, FestivalBegin = 2, FestivalEnd = 5 };

    /// <summary>
    /// Chapter 1 of the synthetic Hatching-tide. Its window differs from every other quest's here, so a catalog that
    /// holds it makes the festival phased (<see cref="QuestCatalog.PhasedFestivals"/>) and the window is judged.
    /// </summary>
    private static QuestRecord Chapter1() => Quest(E) with { Festival = HatchingTide, FestivalBegin = 1, FestivalEnd = 1 };

    /// <summary>The quest's catalog; a Hatching-tide quest gets <see cref="Chapter1"/> beside it.</summary>
    private static QuestCatalog Phased(QuestRecord q) => q.Festival == HatchingTide && q.RowId != E ? Catalog(q, Chapter1()) : Catalog(q);

    private static CharacterSnapshot Ranked(byte rank) => Snapshot() with { SatisfactionRanks = new Dictionary<byte, byte> { [Mnaago] = rank } };

    private static CharacterSnapshot Running(params ushort[] phases) => Snapshot() with { ActiveFestivals = [HatchingTide], ActiveFestivalPhases = phases };

    private static QuestEvaluation Resolve(QuestRecord q, CharacterSnapshot s) => StateResolver.Resolve(q, s, Phased(q), Context);

    private static string For(QuestRecord q, CharacterSnapshot s) => BlockerText.For(Resolve(q, s), q, Names with { Catalog = Phased(q) });

    [Fact]
    public void Custom_delivery_rank_blocks_below_the_rank_and_passes_at_it()
    {
        var quest = Delivery();

        var below = Only(RequirementEvaluator.Evaluate(quest, Ranked(3), Catalog(quest), Context), RequirementKind.CustomDeliveryRank);
        Assert.False(below.Met);
        Assert.Equal("needs satisfaction rank 4 with M'naago, you are rank 3", below.Detail);
        var req = Assert.IsType<CustomDeliveryRankRequirement>(below.Req);
        Assert.Equal((Mnaago, (byte)4, (byte?)3), (req.Npc, req.RequiredRank, req.ActualRank));
        Assert.Equal(QuestState.Blocked, Resolve(quest, Ranked(3)).State);
        Assert.Equal("Custom delivery: rank 4 with M'naago", For(quest, Ranked(3)));
        Assert.Equal("Blocked · Custom delivery: rank 4 with M'naago", BlockerText.StatusText(Resolve(quest, Ranked(3)), quest, Names));

        var at = Only(RequirementEvaluator.Evaluate(quest, Ranked(4), Catalog(quest), Context), RequirementKind.CustomDeliveryRank);
        Assert.True(at.Met);
        Assert.Equal("satisfaction rank 4 with M'naago", at.Detail);
        Assert.Equal(QuestState.Ready, Resolve(quest, Ranked(4)).State);
        Assert.Equal(QuestState.Ready, Resolve(quest, Ranked(5)).State);

        // A client not yet unlocked reads rank 0 from the client, which is below any rank a quest asks for.
        Assert.Equal(QuestState.Blocked, Resolve(quest, Ranked(0)).State);
    }

    [Fact]
    public void Custom_delivery_rank_is_listed_not_judged_when_the_capture_has_no_ranks()
    {
        var quest = Delivery();

        var result = Only(RequirementEvaluator.Evaluate(quest, Snapshot(), Catalog(quest), Context), RequirementKind.CustomDeliveryRank);
        Assert.True(result.Met);
        Assert.Equal("needs satisfaction rank 4 with M'naago, not checked", result.Detail);
        Assert.Null(Assert.IsType<CustomDeliveryRankRequirement>(result.Req).ActualRank);
        Assert.Equal(QuestState.Ready, Resolve(quest, Snapshot()).State);

        // Without a client name the phrase drops the "with" clause rather than printing a row id.
        var plain = Only(RequirementEvaluator.Evaluate(quest, Ranked(1), Catalog(quest), EvalContext.Default), RequirementKind.CustomDeliveryRank);
        Assert.Equal("needs satisfaction rank 4, you are rank 1", plain.Detail);
        Assert.Equal("Custom delivery: rank 4", BlockerText.For(StateResolver.Resolve(quest, Ranked(1), Catalog(quest), EvalContext.Default), quest, BlockerNames.Default));
    }

    [Fact]
    public void Carrier_level_blocks_below_the_level_and_passes_at_it()
    {
        var quest = Postmoogle();

        var below = Only(RequirementEvaluator.Evaluate(quest, Snapshot() with { CarrierLevel = 6 }, Catalog(quest), Context), RequirementKind.CarrierLevel);
        Assert.False(below.Met);
        Assert.Equal("needs carrier level 7, you are level 6", below.Detail);
        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot() with { CarrierLevel = 6 }).State);
        Assert.Equal("Delivery Moogle: carrier level 7", For(quest, Snapshot() with { CarrierLevel = 6 }));

        var at = Only(RequirementEvaluator.Evaluate(quest, Snapshot() with { CarrierLevel = 7 }, Catalog(quest), Context), RequirementKind.CarrierLevel);
        Assert.True(at.Met);
        Assert.Equal("carrier level 7", at.Detail);
        Assert.Equal(QuestState.Ready, Resolve(quest, Snapshot() with { CarrierLevel = 7 }).State);
        Assert.Equal(QuestState.Ready, Resolve(quest, Snapshot() with { CarrierLevel = 24 }).State);
    }

    [Fact]
    public void A_captured_carrier_level_of_0_is_a_real_level_and_blocks()
    {
        var quest = Postmoogle();
        var never = Snapshot() with { CarrierLevel = 0 };

        var result = Only(RequirementEvaluator.Evaluate(quest, never, Catalog(quest), Context), RequirementKind.CarrierLevel);
        Assert.False(result.Met);
        Assert.Equal("needs carrier level 7, you are level 0", result.Detail);
        Assert.Equal((byte?)0, Assert.IsType<CarrierLevelRequirement>(result.Req).ActualLevel);
        Assert.Equal(QuestState.Blocked, Resolve(quest, never).State);
        Assert.Equal("Delivery Moogle: carrier level 7", For(quest, never));
    }

    [Fact]
    public void Carrier_level_is_listed_not_judged_when_the_capture_has_none()
    {
        var quest = Postmoogle();

        var result = Only(RequirementEvaluator.Evaluate(quest, Snapshot(), Catalog(quest), Context), RequirementKind.CarrierLevel);
        Assert.True(result.Met);
        Assert.Equal("needs carrier level 7, not checked", result.Detail);
        Assert.Null(Assert.IsType<CarrierLevelRequirement>(result.Req).ActualLevel);
        Assert.Equal(QuestState.Ready, Resolve(quest, Snapshot()).State);
    }

    [Fact]
    public void Quests_without_delivery_gates_list_neither_kind()
    {
        var results = RequirementEvaluator.Evaluate(Quest(Target), Ranked(0) with { CarrierLevel = 3 }, Catalog(Quest(Target)), Context);

        Assert.DoesNotContain(results, r => r.Req.Kind is RequirementKind.CustomDeliveryRank or RequirementKind.CarrierLevel);
    }

    [Fact]
    public void A_chapter_is_blocked_before_its_phase_and_ready_from_it()
    {
        var quest = Chapter2();

        var early = Resolve(quest, Running(1));
        Assert.Equal(QuestState.Blocked, early.State);
        Assert.Equal(RequirementKind.Seasonal, early.NextStep!.Req.Kind);
        Assert.True(early.IsOutOfSeason);
        var req = Assert.IsType<SeasonalRequirement>(early.NextStep.Req);
        Assert.True(req.Active);
        Assert.True(req.ChapterNotOpen);
        Assert.False(req.ChapterOver);
        Assert.Equal((ushort?)1, req.Phase);
        Assert.Equal("chapter opens at phase 2, the event is at phase 1", early.NextStep.Detail);
        Assert.Equal("Seasonal: chapter not open yet", For(quest, Running(1)));
        Assert.Equal("Blocked · Seasonal: chapter not open yet", BlockerText.StatusText(early, quest, Names));

        var open = Resolve(quest, Running(2));
        Assert.Equal(QuestState.Ready, open.State);
        Assert.Equal("seasonal event active, phase 2", Only(open.Requirements, RequirementKind.Seasonal).Detail);
        Assert.Equal(QuestState.Ready, Resolve(quest, Running(5)).State);
    }

    [Fact]
    public void A_chapter_is_blocked_once_its_phase_has_passed()
    {
        var quest = Chapter2();

        var over = Resolve(quest, Running(6));
        Assert.Equal(QuestState.Blocked, over.State);
        Assert.Equal(RequirementKind.Seasonal, over.NextStep!.Req.Kind);
        Assert.True(Assert.IsType<SeasonalRequirement>(over.NextStep.Req).ChapterOver);
        Assert.Equal("chapter ended after phase 5, the event is at phase 6", over.NextStep.Detail);
        Assert.Equal("Seasonal: chapter over", For(quest, Running(6)));

        // The event still runs, so the run is not "past" for this character: Blocked, never Foreclosed, even with an
        // earlier quest of the festival done.
        var earlier = Quest(A) with { Festival = HatchingTide, FestivalBegin = 1, FestivalEnd = 5 };
        var snapshot = Running(6) with { CompletedBits = Bits(A) };
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, snapshot, Catalog(earlier, quest), Context).State);
    }

    [Fact]
    public void A_chapter_quest_in_the_journal_stays_in_journal_and_counted_after_its_phase()
    {
        var quest = Chapter2();

        // Picked up while its chapter ran; the event has moved on but still runs, and the game keeps the quest.
        var held = Running(6) with { Accepted = [Accepted(Target, sequence: 2)] };
        var result = Resolve(quest, held);
        Assert.Equal(QuestState.Accepted, result.State);
        Assert.Equal((byte?)2, result.Sequence);
        Assert.False(result.IsOutOfSeason);
        Assert.False(result.LeavesTotals);

        // Before its chapter opens (a stale journal entry) it reads the same.
        Assert.Equal(QuestState.Accepted, Resolve(quest, Running(1) with { Accepted = [Accepted(Target)] }).State);

        // With the event over, the seasonal check still comes first, as before.
        var ended = Snapshot() with { Accepted = [Accepted(Target)] };
        var blocked = Resolve(quest, ended);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.True(blocked.LeavesTotals);
    }

    [Fact]
    public void An_unknown_phase_never_blocks_a_windowed_quest()
    {
        var quest = Chapter2();

        // No phases captured at all (a file written before 0.6.2), or fewer phases than festivals.
        Assert.Equal(QuestState.Ready, Resolve(quest, Running()).State);
        Assert.Equal("seasonal event active", Only(Resolve(quest, Running()).Requirements, RequirementKind.Seasonal).Detail);
        var shortList = Snapshot() with { ActiveFestivals = [39, HatchingTide], ActiveFestivalPhases = [1] };
        Assert.Equal(QuestState.Ready, Resolve(quest, shortList).State);
        Assert.Null(Assert.IsType<SeasonalRequirement>(Only(Resolve(quest, shortList).Requirements, RequirementKind.Seasonal).Req).Phase);
    }

    [Fact]
    public void A_quest_without_a_window_keeps_the_id_only_check_whatever_the_phase()
    {
        var quest = Quest(Target) with { Festival = HatchingTide };

        Assert.Equal(QuestState.Ready, Resolve(quest, Running(0)).State);
        Assert.Equal(QuestState.Ready, Resolve(quest, Running(9)).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot()).State);
        Assert.Equal("Seasonal: not running", For(quest, Snapshot()));
        var req = Assert.IsType<SeasonalRequirement>(Only(Resolve(quest, Running(9)).Requirements, RequirementKind.Seasonal).Req);
        Assert.False(req.HasWindow);
        Assert.Equal((ushort?)9, req.Phase);
    }

    [Fact]
    public void An_open_ended_window_bounds_one_side_only()
    {
        var fromThree = Quest(Target) with { Festival = HatchingTide, FestivalBegin = 3 };
        Assert.Equal(QuestState.Blocked, Resolve(fromThree, Running(2)).State);
        Assert.Equal(QuestState.Ready, Resolve(fromThree, Running(3)).State);
        Assert.Equal(QuestState.Ready, Resolve(fromThree, Running(40)).State);

        var untilThree = Quest(Target) with { Festival = HatchingTide, FestivalEnd = 3 };
        Assert.Equal(QuestState.Ready, Resolve(untilThree, Running(0)).State);
        Assert.Equal(QuestState.Ready, Resolve(untilThree, Running(3)).State);
        Assert.Equal(QuestState.Blocked, Resolve(untilThree, Running(4)).State);
    }

    [Fact]
    public void An_inactive_festival_still_reads_not_running_or_ended()
    {
        var quest = Chapter2();

        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot()).State);
        Assert.Equal("Seasonal: not running", For(quest, Snapshot()));
        var past = StateResolver.Resolve(quest, Snapshot(), Phased(quest), Context with { FestivalIsPast = _ => true });
        Assert.Equal(QuestState.Foreclosed, past.State);
        Assert.Equal("Seasonal: ended", BlockerText.For(past, quest, Names));
    }

    [Fact]
    public void A_single_window_festival_quest_is_ready_at_any_phase_while_the_festival_runs()
    {
        // Every quest of this festival carries the same window (0, 1), as 250 seasonal rows do: the window cannot
        // separate chapters, so it is not judged and the reported phase never blocks.
        const ushort Moonfire = 39;
        var quest = Quest(Target) with { Festival = Moonfire, FestivalEnd = 1 };
        var sibling = Quest(A) with { Festival = Moonfire, FestivalEnd = 1 };
        var catalog = Catalog(quest, sibling);
        Assert.DoesNotContain(Moonfire, catalog.PhasedFestivals);

        foreach (ushort phase in new ushort[] { 0, 1, 2, 9 })
        {
            var running = Snapshot() with { ActiveFestivals = [Moonfire], ActiveFestivalPhases = [phase] };
            var result = StateResolver.Resolve(quest, running, catalog, Context);
            Assert.True(result.State == QuestState.Ready, $"phase {phase}: {result.State} {result.NextStep?.Detail}");
            var req = Assert.IsType<SeasonalRequirement>(Only(result.Requirements, RequirementKind.Seasonal).Req);
            Assert.False(req.HasWindow);
            Assert.Equal((ushort?)phase, req.Phase);
        }

        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, Snapshot(), catalog, Context).State);

        // The same window in a festival whose quests carry two windows is judged.
        var phased = Catalog(quest, sibling with { FestivalBegin = 2, FestivalEnd = 3 });
        Assert.Contains(Moonfire, phased.PhasedFestivals);
        var late = Snapshot() with { ActiveFestivals = [Moonfire], ActiveFestivalPhases = [2] };
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, late, phased, Context).State);
    }
}
