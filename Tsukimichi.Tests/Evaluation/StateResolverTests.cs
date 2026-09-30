using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

public class StateResolverTests
{
    private static QuestEvaluation Resolve(QuestRecord q, CharacterSnapshot s, QuestCatalog? c = null, EvalContext? ctx = null) =>
        StateResolver.Resolve(q, s, c ?? Catalog(q), ctx ?? EvalContext.Default);

    // Rule 1

    [Fact]
    public void Completed_bit_on_non_repeatable_is_Completed()
    {
        var quest = Quest(Target);

        var result = Resolve(quest, Snapshot(Target));

        Assert.Equal(QuestState.Completed, result.State);
        Assert.Null(result.NextStep);
    }

    [Fact]
    public void Completed_wins_over_QuestLock_foreclosure()
    {
        var quest = Quest(Target) with { QuestLocks = [B] };

        Assert.Equal(QuestState.Completed, Resolve(quest, Snapshot(Target, B)).State);
    }

    [Fact]
    public void Completed_bit_on_repeatable_without_interval_is_Completed()
    {
        // RepeatInterval 0 means the flag never resets, so the completion bit is final (rule 1, not rule 5).
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 0 };

        Assert.Equal(QuestState.Completed, Resolve(quest, Snapshot(Target)).State);
    }

    [Fact]
    public void Completed_seasonal_quest_stays_Completed_after_the_event_ends()
    {
        // Rule 1 beats rule 3: the festival is inactive and even reads as past, yet the quest is done.
        var quest = Quest(Target) with { Festival = 9 };
        var ctx = new EvalContext { FestivalIsPast = _ => true };

        var result = Resolve(quest, Snapshot(Target), ctx: ctx);

        Assert.Equal(QuestState.Completed, result.State);
        Assert.Null(result.NextStep);
    }

    // Rule 2

    [Fact]
    public void Completed_QuestLock_is_Foreclosed()
    {
        var quest = Quest(Target) with { QuestLocks = [B, C] };

        var result = Resolve(quest, Snapshot(C));

        Assert.Equal(QuestState.Foreclosed, result.State);
        Assert.Equal(RequirementKind.Foreclosure, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void GC_specific_quest_in_another_GC_is_Blocked_not_Foreclosed()
    {
        var quest = Quest(Target) with { GrandCompany = 1, QuestLocks = [B] };

        var result = Resolve(quest, Snapshot(B) with { GrandCompany = 2 });

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.GrandCompany, result.NextStep!.Req.Kind);
        Assert.Equal("requires Maelstrom", result.NextStep.Detail);
        Assert.DoesNotContain(result.Requirements, r => r.Req.Kind == RequirementKind.Foreclosure);
    }

    [Fact]
    public void GC_specific_quest_in_the_same_GC_with_a_completed_lock_is_Foreclosed()
    {
        var quest = Quest(Target) with { GrandCompany = 1, QuestLocks = [B] };

        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(B) with { GrandCompany = 1 }).State);
    }

    [Fact]
    public void Foreclosed_wins_over_Accepted()
    {
        var quest = Quest(Target) with { QuestLocks = [B] };
        var snapshot = Snapshot(B) with { Accepted = [Accepted(Target)] };

        Assert.Equal(QuestState.Foreclosed, Resolve(quest, snapshot).State);
    }

    [Fact]
    public void Completed_lock_wins_over_inactive_festival()
    {
        // Rule 2 beats rule 3: the reason shown is the lock, not the season, even though both would block.
        var quest = Quest(Target) with { QuestLocks = [B], Festival = 9 };

        var result = Resolve(quest, Snapshot(B));

        Assert.Equal(QuestState.Foreclosed, result.State);
        Assert.Equal(RequirementKind.Foreclosure, result.NextStep!.Req.Kind);
    }

    // Rule 3

    [Fact]
    public void Inactive_festival_never_seen_is_Blocked_seasonal()
    {
        var quest = Quest(Target) with { Festival = 9 };

        var result = Resolve(quest, Snapshot());

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.Seasonal, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Inactive_festival_with_a_completed_quest_of_that_festival_is_Foreclosed()
    {
        var earlier = Quest(A) with { Festival = 9 };
        var quest = Quest(Target) with { Festival = 9 };

        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(A), Catalog(earlier, quest)).State);
    }

    [Fact]
    public void FestivalIsPast_hook_composes_with_the_default_heuristic()
    {
        var earlier = Quest(A) with { Festival = 9 };
        var quest = Quest(Target) with { Festival = 9 };
        var catalog = Catalog(earlier, quest);

        // Hook says past, nothing completed: past.
        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(), catalog, new EvalContext { FestivalIsPast = id => id == 9 }).State);

        // Hook says not past, but a quest of that festival is done: still past (OR, not override).
        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(A), catalog, new EvalContext { FestivalIsPast = _ => false }).State);

        // Neither: blocked as seasonal.
        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot(), catalog, new EvalContext { FestivalIsPast = _ => false }).State);
    }

    [Fact]
    public void WithFestivalEnds_marks_a_festival_past_once_its_end_has_passed()
    {
        var quest = Quest(Target) with { Festival = 9 };
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var ends = new Dictionary<ushort, DateTime?>
        {
            [9] = now.AddDays(-1),
            [10] = now.AddDays(1),
            [11] = null,
        };
        var ctx = EvalContext.Default.WithFestivalEnds(ends, () => now);

        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(), ctx: ctx).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest with { Festival = 10 }, Snapshot(), ctx: ctx).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest with { Festival = 11 }, Snapshot(), ctx: ctx).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest with { Festival = 12 }, Snapshot(), ctx: ctx).State);
    }

    [Fact]
    public void WithFestivalEnds_keeps_the_default_heuristic_and_any_earlier_hook()
    {
        var earlier = Quest(A) with { Festival = 10 };
        var quest = Quest(Target) with { Festival = 10 };
        var catalog = Catalog(earlier, quest);
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var ends = new Dictionary<ushort, DateTime?> { [10] = now.AddDays(1) };

        // The end is in the future, but the character already completed a quest of that festival.
        var ctx = EvalContext.Default.WithFestivalEnds(ends, () => now);
        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(A), catalog, ctx).State);

        // An earlier hook is not replaced.
        var chained = new EvalContext { FestivalIsPast = id => id == 10 }.WithFestivalEnds(ends, () => now);
        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(), catalog, chained).State);
    }

    [Fact]
    public void Curated_rerun_entry_is_never_past_even_after_part_of_it_was_done()
    {
        var earlier = Quest(A) with { Festival = 84 };
        var quest = Quest(Target) with { Festival = 84 };
        var catalog = Catalog(earlier, quest);
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        var curated = new Dictionary<ushort, FestivalInfo>
        {
            [84] = new("A Nocturne for Heroes", null, null, false, "https://example.com/ffxv"),
        };
        var ctx = EvalContext.Default.WithCuratedFestivals(curated, () => now);

        // A quest of it done, not running: the heuristic alone would read Locked out; the rerun entry keeps it Blocked.
        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(A), catalog).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot(A), catalog, ctx).State);

        // The curated verdict is final over an ad-hoc hook too.
        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot(A), catalog, ctx with { FestivalIsPast = _ => true }).State);
    }

    [Fact]
    public void Curated_entries_leave_the_heuristic_only_undated_editions_and_unlisted_festivals()
    {
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        var curated = new Dictionary<ushort, FestivalInfo>
        {
            [10] = new("Moonfire Faire (2014)", now.AddDays(-30), now.AddDays(-1), false),
            [11] = new("Moonfire Faire (2027)", now.AddDays(1), now.AddDays(30), false),
            [12] = new("The Rising (2024)", null, null, false),
        };
        var ctx = EvalContext.Default.WithCuratedFestivals(curated, () => now);

        foreach (var (festival, doneOne, expected) in new (ushort, bool, QuestState)[]
        {
            (10, false, QuestState.Foreclosed), // curated end passed
            (11, true, QuestState.Blocked),     // curated end still ahead: the heuristic may not foreclose
            (12, true, QuestState.Foreclosed),  // undated edition (name with its year): heuristic
            (12, false, QuestState.Blocked),
            (13, true, QuestState.Foreclosed),  // no entry: heuristic
            (13, false, QuestState.Blocked),
        })
        {
            var earlier = Quest(A) with { Festival = festival };
            var quest = Quest(Target) with { Festival = festival };
            var snapshot = doneOne ? Snapshot(A) : Snapshot();
            Assert.Equal(expected, Resolve(quest, snapshot, Catalog(earlier, quest), ctx).State);
        }

        Assert.False(curated[12].IsRerun);
        Assert.True(new FestivalInfo("Blunderville", null, null, false).IsRerun);
    }

    [Fact]
    public void Active_festival_falls_through_to_Ready()
    {
        var quest = Quest(Target) with { Festival = 9 };

        Assert.Equal(QuestState.Ready, Resolve(quest, Snapshot() with { ActiveFestivals = [9] }).State);
    }

    [Fact]
    public void Inactive_festival_wins_over_Accepted()
    {
        var quest = Quest(Target) with { Festival = 9 };
        var snapshot = Snapshot() with { Accepted = [Accepted(Target)] };

        Assert.Equal(QuestState.Blocked, Resolve(quest, snapshot).State);
    }

    // Rule 4

    [Fact]
    public void Accepted_quest_carries_its_sequence()
    {
        var quest = Quest(Target) with { Level = 90 };
        var snapshot = Snapshot() with { Accepted = [Accepted(Target, 3)] };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.Accepted, result.State);
        Assert.Equal((byte)3, result.Sequence);
    }

    [Fact]
    public void Accepted_wins_over_DoneThisCycle()
    {
        var quest = Quest(Target) with { IsRepeatable = true };
        var snapshot = Snapshot() with
        {
            Accepted = [Accepted(Target)],
            DailyDone = new Dictionary<ushort, byte> { [quest.QuestId] = 1 },
        };

        Assert.Equal(QuestState.Accepted, Resolve(quest, snapshot).State);
    }

    // Rule 4 with allied-society dailies (bug-hunt C1/C2)

    [Fact]
    public void Accepting_one_tribe_daily_leaves_the_other_dailies_Ready_and_resolves_it_Accepted()
    {
        // The client's 12-slot daily array holds the dailies accepted today, not the day's offer. 0.5.0 fed it to the
        // context as the offer, so after accepting one Vanu Vanu daily every other daily read Blocked "not offered
        // today". The reader now lists an in-progress daily in Accepted (step 0) and the context carries no offer.
        var vanuA = Quest(A) with { BeastTribe = 7, BeastRank = 1, IsRepeatable = true, RepeatInterval = 1 };
        var vanuB = Quest(B) with { BeastTribe = 7, BeastRank = 1, IsRepeatable = true, RepeatInterval = 1 };
        var ixal = Quest(C) with { BeastTribe = 3, BeastRank = 1, IsRepeatable = true, RepeatInterval = 1 };
        var catalog = Catalog(vanuA, vanuB, ixal);
        var snapshot = Snapshot() with
        {
            Tribes = new Dictionary<byte, TribeStanding> { [7] = new(3, 0), [3] = new(3, 0) },
            TribeAllowance = 11,
            Accepted = [Accepted(A, 0)],
        };

        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

        var accepted = states[A];
        Assert.Equal(QuestState.Accepted, accepted.State);
        Assert.Equal((byte)0, accepted.Sequence);
        Assert.Equal(QuestState.Ready, states[B].State);
        Assert.Equal(QuestState.Ready, states[C].State);
        Assert.DoesNotContain(states[B].Requirements, r => r.Req.Kind == RequirementKind.TribeDailyOffer);
    }

    [Fact]
    public void Turned_in_tribe_daily_is_DoneThisCycle_while_an_in_progress_one_is_Accepted()
    {
        // A turned-in daily keeps its slot with the completed flag: it lands in DailyDone, not in Accepted.
        var done = Quest(A) with { BeastTribe = 7, BeastRank = 1, IsRepeatable = true, RepeatInterval = 1 };
        var inProgress = Quest(B) with { BeastTribe = 7, BeastRank = 1, IsRepeatable = true, RepeatInterval = 1 };
        var catalog = Catalog(done, inProgress);
        var snapshot = Snapshot() with
        {
            Tribes = new Dictionary<byte, TribeStanding> { [7] = new(3, 0) },
            TribeAllowance = 10,
            Accepted = [Accepted(B, 0)],
            DailyDone = new Dictionary<ushort, byte> { [QuestRecord.ToQuestId(A)] = 1 },
        };

        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

        Assert.Equal(QuestState.DoneThisCycle, states[A].State);
        Assert.Equal(QuestState.Accepted, states[B].State);
    }

    // Rule 5

    [Fact]
    public void Repeatable_in_DailyDone_is_DoneThisCycle()
    {
        var quest = Quest(Target) with { IsRepeatable = true };
        var snapshot = Snapshot() with { DailyDone = new Dictionary<ushort, byte> { [quest.QuestId] = 1 } };

        Assert.Equal(QuestState.DoneThisCycle, Resolve(quest, snapshot).State);
    }

    [Fact]
    public void Repeatable_with_interval_and_completed_bit_is_done_before_not_done_this_cycle()
    {
        // The completion bit of a daily stays set after the first turn-in; "done today" lives only in DailyDone
        // (docs/data/v4/tagging-audit.md finding 1: 39 allied society dailies read Done this cycle for good).
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 1 };

        var result = Resolve(quest, Snapshot(Target));

        Assert.Equal(QuestState.Ready, result.State);
        Assert.True(result.RepeatableDoneBefore);
        Assert.True(result.CountsAsDone);
    }

    [Fact]
    public void Daily_in_DailyDone_is_DoneThisCycle_and_done_before()
    {
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 1 };
        var snapshot = Snapshot(Target) with { DailyDone = new Dictionary<ushort, byte> { [quest.QuestId] = 1 } };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.DoneThisCycle, result.State);
        Assert.True(result.RepeatableDoneBefore);
    }

    [Fact]
    public void Daily_never_done_is_not_done_before()
    {
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 1 };

        var result = Resolve(quest, Snapshot());

        Assert.Equal(QuestState.Ready, result.State);
        Assert.False(result.RepeatableDoneBefore);
        Assert.False(result.CountsAsDone);
    }

    [Fact]
    public void Completed_quests_are_done_but_not_repeatables_done_before()
    {
        var once = Resolve(Quest(Target), Snapshot(Target));
        Assert.Equal(QuestState.Completed, once.State);
        Assert.False(once.RepeatableDoneBefore);
        Assert.True(once.CountsAsDone);

        // A repeatable whose flag never resets reads Completed under rule 1, done like any other quest.
        var final = Resolve(Quest(Target) with { IsRepeatable = true, RepeatInterval = 0 }, Snapshot(Target));
        Assert.Equal(QuestState.Completed, final.State);
        Assert.False(final.RepeatableDoneBefore);
    }

    [Fact]
    public void Repeatable_without_interval_in_DailyDone_is_DoneThisCycle()
    {
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 0 };
        var snapshot = Snapshot() with { DailyDone = new Dictionary<ushort, byte> { [quest.QuestId] = 1 } };

        Assert.Equal(QuestState.DoneThisCycle, Resolve(quest, snapshot).State);
    }

    [Fact]
    public void Repeatable_not_done_this_cycle_is_Ready()
    {
        var quest = Quest(Target) with { IsRepeatable = true };

        Assert.Equal(QuestState.Ready, Resolve(quest, Snapshot()).State);
    }

    [Fact]
    public void DoneThisCycle_wins_over_Unknown()
    {
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 1 };
        var ctx = new EvalContext { IsAchievementGated = _ => true };
        var snapshot = Snapshot(Target) with { AchievementsLoaded = false, DailyDone = new Dictionary<ushort, byte> { [quest.QuestId] = 1 } };

        Assert.Equal(QuestState.DoneThisCycle, Resolve(quest, snapshot, ctx: ctx).State);
    }

    // Rule 6

    [Fact]
    public void Achievement_gated_without_loaded_achievements_is_Unknown()
    {
        var quest = Quest(Target);
        var ctx = new EvalContext { IsAchievementGated = id => id == Target };

        var result = Resolve(quest, Snapshot() with { AchievementsLoaded = false }, ctx: ctx);

        Assert.Equal(QuestState.Unknown, result.State);
        Assert.Equal(RequirementKind.Achievement, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Unknown_wins_over_unmet_requirements()
    {
        var quest = Quest(Target) with { Level = 90 };
        var ctx = new EvalContext { IsAchievementGated = _ => true };

        Assert.Equal(QuestState.Unknown, Resolve(quest, Snapshot() with { AchievementsLoaded = false }, ctx: ctx).State);
    }

    [Fact]
    public void Achievement_gated_with_loaded_achievements_is_evaluated_normally()
    {
        var ctx = new EvalContext { IsAchievementGated = _ => true };

        Assert.Equal(QuestState.Ready, Resolve(Quest(Target), Snapshot(), ctx: ctx).State);
        Assert.Equal(QuestState.Blocked, Resolve(Quest(Target) with { Level = 90 }, Snapshot(), ctx: ctx).State);
    }

    // Rule 7

    [Fact]
    public void All_requirements_met_on_current_job_is_Ready()
    {
        var quest = Quest(Target) with { Level = 20, PreviousQuests = new Prereq([A], JoinKind.All) };

        var result = Resolve(quest, Snapshot(A));

        Assert.Equal(QuestState.Ready, result.State);
        Assert.Null(result.NextStep);
        Assert.Null(result.ReadyOnJob);
        Assert.All(result.Requirements, r => Assert.True(r.Met));
    }

    [Fact]
    public void Unsynced_JobLevels_decide_readiness_not_the_visible_level()
    {
        var quest = Quest(Target) with { Level = 50 };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50)) };

        Assert.Equal(QuestState.Ready, Resolve(quest, snapshot).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest, snapshot with { JobLevels = Levels((Gladiator, 49)) }).State);
    }

    [Fact]
    public void Blocked_reports_the_first_unmet_requirement_as_next_step()
    {
        var quest = Quest(Target) with { Level = 60, PreviousQuests = new Prereq([A, B], JoinKind.All) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 10)) };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.Level, result.NextStep!.Req.Kind);
        Assert.Equal("needs level 60, you are 10", result.NextStep.Detail);
        Assert.Equal(2, result.Requirements.Count(r => !r.Met));
    }

    [Fact]
    public void ReadyOnOtherJob_picks_the_highest_qualifying_job()
    {
        var quest = Quest(Target) with { Level = 30, ClassJobCategory = 5 };
        var ctx = new EvalContext { ClassJobs = new Jobs((5, [Conjurer, Paladin])) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 35), (Paladin, 45)) };

        var result = Resolve(quest, snapshot, ctx: ctx);

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Paladin, result.ReadyOnJob);
        Assert.Null(result.NextStep);
    }

    [Fact]
    public void ReadyOnOtherJob_ignores_jobs_below_the_level()
    {
        var quest = Quest(Target) with { Level = 30, ClassJobCategory = 5 };
        var ctx = new EvalContext { ClassJobs = new Jobs((5, [Conjurer, Paladin])) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 35), (Paladin, 20)) };

        var result = Resolve(quest, snapshot, ctx: ctx);

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Conjurer, result.ReadyOnJob);
    }

    [Fact]
    public void Blocked_on_every_job_reports_current_job_requirements()
    {
        var quest = Quest(Target) with { Level = 30, ClassJobCategory = 5 };
        var ctx = new EvalContext { ClassJobs = new Jobs((5, [Conjurer])) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 10)) };

        var result = Resolve(quest, snapshot, ctx: ctx);

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.ClassJob, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Without_a_lookup_other_jobs_come_from_JobLevels()
    {
        var quest = Quest(Target) with { Level = 60 };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Conjurer, result.ReadyOnJob);
    }

    [Fact]
    public void Non_job_gates_block_on_every_job()
    {
        var quest = Quest(Target) with { Level = 1, PreviousQuests = new Prereq([A], JoinKind.All) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) };

        Assert.Equal(QuestState.Blocked, Resolve(quest, snapshot).State);
    }

    [Fact]
    public void Pinned_job_quest_is_ReadyOnOtherJob_only_on_that_job()
    {
        var quest = Quest(Target) with { Level = 30, ClassJobRequired = Conjurer };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 35), (Paladin, 90)) };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Conjurer, result.ReadyOnJob);
        Assert.Equal(QuestState.Blocked, Resolve(quest, snapshot with { JobLevels = Levels((Gladiator, 50), (Conjurer, 29), (Paladin, 90)) }).State);
    }

    [Fact]
    public void Other_job_check_ignores_jobs_the_category_does_not_admit()
    {
        var quest = Quest(Target) with { Level = 30, ClassJobCategory = 5 };
        var ctx = new EvalContext { ClassJobs = new Jobs((5, [Conjurer])) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 35), (Paladin, 90)) };

        var result = Resolve(quest, snapshot, ctx: ctx);

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Conjurer, result.ReadyOnJob);
    }

    // Entitlement caps (PlayerState.MaxExpansion / MaxLevel; 0 = not checked)

    [Fact]
    public void Caps_of_zero_are_not_checked()
    {
        // A snapshot from an older build, or a client that has not said, carries 0 for both caps; that must never read
        // as "level 0" or "no expansion", so a Dawntrail level-100 quest is gated by its other requirements only.
        var quest = Quest(Target) with { Expansion = 5, Level = 100 };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 100)), MaxExpansion = 0, LevelCap = 0 };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.Ready, result.State);
        Assert.DoesNotContain(result.Requirements, r => r.Req.Kind is RequirementKind.ExpansionCap or RequirementKind.LevelCap);
    }

    [Fact]
    public void Expansion_cap_below_the_quest_blocks_it()
    {
        var quest = Quest(Target) with { Expansion = 5, Level = 100 };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 100)), MaxExpansion = 4, LevelCap = 100 };

        var result = Resolve(quest, snapshot);

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.ExpansionCap, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Level_cap_below_the_quest_blocks_it_and_caps_at_or_above_pass()
    {
        var quest = Quest(Target) with { Expansion = 4, Level = 90 };
        var levelled = Snapshot() with { JobLevels = Levels((Gladiator, 90)) };

        var capped = Resolve(quest, levelled with { MaxExpansion = 4, LevelCap = 80 });
        Assert.Equal(QuestState.Blocked, capped.State);
        Assert.Equal(RequirementKind.LevelCap, capped.NextStep!.Req.Kind);

        Assert.Equal(QuestState.Ready, Resolve(quest, levelled with { MaxExpansion = 4, LevelCap = 90 }).State);
        Assert.Equal(QuestState.Ready, Resolve(quest, levelled with { MaxExpansion = 5, LevelCap = 100 }).State);
    }

    // Batch

    [Fact]
    public void ResolveAll_covers_every_catalog_row()
    {
        var a = Quest(A);
        var b = Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) };
        var c = Quest(C) with { Festival = 9 };
        var catalog = Catalog(a, b, c);

        var all = StateResolver.ResolveAll(catalog, Snapshot(A), EvalContext.Default);

        Assert.Equal(3, all.Count);
        Assert.Equal(QuestState.Completed, all[A].State);
        Assert.Equal(QuestState.Ready, all[B].State);
        Assert.Equal(QuestState.Blocked, all[C].State);
    }
}
