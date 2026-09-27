using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
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
    public void FestivalIsPast_hook_overrides_the_default_heuristic()
    {
        var quest = Quest(Target) with { Festival = 9 };

        Assert.Equal(QuestState.Foreclosed, Resolve(quest, Snapshot(), ctx: new EvalContext { FestivalIsPast = id => id == 9 }).State);
        Assert.Equal(QuestState.Blocked, Resolve(quest, Snapshot(), ctx: new EvalContext { FestivalIsPast = _ => false }).State);
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

    // Rule 5

    [Fact]
    public void Repeatable_in_DailyDone_is_DoneThisCycle()
    {
        var quest = Quest(Target) with { IsRepeatable = true };
        var snapshot = Snapshot() with { DailyDone = new Dictionary<ushort, byte> { [quest.QuestId] = 1 } };

        Assert.Equal(QuestState.DoneThisCycle, Resolve(quest, snapshot).State);
    }

    [Fact]
    public void Repeatable_with_completed_bit_is_DoneThisCycle_not_Completed()
    {
        var quest = Quest(Target) with { IsRepeatable = true, RepeatInterval = 1 };

        Assert.Equal(QuestState.DoneThisCycle, Resolve(quest, Snapshot(Target)).State);
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
        var quest = Quest(Target) with { IsRepeatable = true };
        var ctx = new EvalContext { IsAchievementGated = _ => true };

        Assert.Equal(QuestState.DoneThisCycle, Resolve(quest, Snapshot(Target) with { AchievementsLoaded = false }, ctx: ctx).State);
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
