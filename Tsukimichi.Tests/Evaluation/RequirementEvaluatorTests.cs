using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

public class RequirementEvaluatorTests
{
    private static IReadOnlyList<RequirementResult> Eval(QuestRecord q, CharacterSnapshot s, QuestCatalog? c = null, EvalContext? ctx = null) =>
        RequirementEvaluator.Evaluate(q, s, c ?? Catalog(q), ctx ?? EvalContext.Default);

    [Fact]
    public void Results_follow_display_priority_order()
    {
        var quest = Quest(Target) with
        {
            Level = 10,
            ClassJobCategory = 5,
            PreviousQuests = new Prereq([A], JoinKind.All),
            QuestLocks = [B],
            GrandCompany = 1,
            GrandCompanyRank = 2,
            BeastTribe = 3,
            BeastRank = 4,
            BeastValue = 100,
            IsRepeatable = true,
            InstanceContentRequired = [7],
            Festival = 9,
            AcceptConditions = [11],
            MountRequired = true,
            HouseRequired = true,
        };
        var ctx = new EvalContext
        {
            IsAchievementGated = _ => true,
            TodaysDailyOffer = new HashSet<ushort> { quest.QuestId },
        };

        // A member of the quest's own company, so the locks count as foreclosure rather than a switchable company.
        var kinds = Eval(quest, Snapshot() with { GrandCompany = 1 }, ctx: ctx).Select(r => r.Req.Kind).ToArray();

        Assert.Equal(
        [
            RequirementKind.Foreclosure, RequirementKind.ClassJob, RequirementKind.Level, RequirementKind.PreviousQuests,
            RequirementKind.GrandCompany, RequirementKind.GrandCompanyRank,
            RequirementKind.TribeRank, RequirementKind.TribeReputation, RequirementKind.TribeAllowance, RequirementKind.TribeDailyOffer,
            RequirementKind.DutyCompletion, RequirementKind.Seasonal, RequirementKind.AcceptCondition,
            RequirementKind.Mount, RequirementKind.House, RequirementKind.Achievement,
        ], kinds);
    }

    [Fact]
    public void Ungated_quest_has_only_a_level_requirement()
    {
        var results = Eval(Quest(Target), Snapshot());

        var level = Assert.Single(results);
        Assert.Equal(RequirementKind.Level, level.Req.Kind);
        Assert.True(level.Met);
    }

    [Fact]
    public void Foreclosure_lists_completed_locks_by_name()
    {
        var lockQuest = Quest(B, "Joining the Maelstrom");
        var quest = Quest(Target) with { QuestLocks = [B, C] };

        var results = Eval(quest, Snapshot(B), Catalog(quest, lockQuest));

        var r = Only(results, RequirementKind.Foreclosure);
        Assert.False(r.Met);
        Assert.Equal("foreclosed by Joining the Maelstrom", r.Detail);
        var req = Assert.IsType<ForeclosureRequirement>(r.Req);
        Assert.Equal(new uint[] { B }, req.CompletedLockIds);
    }

    [Fact]
    public void Foreclosure_is_met_when_no_lock_is_completed()
    {
        var quest = Quest(Target) with { QuestLocks = [B] };

        var r = Only(Eval(quest, Snapshot()), RequirementKind.Foreclosure);

        Assert.True(r.Met);
    }

    [Fact]
    public void Expansion_cap_appears_only_when_exceeded()
    {
        var quest = Quest(Target) with { Expansion = 3 };

        Assert.DoesNotContain(Eval(quest, Snapshot()), r => r.Req.Kind == RequirementKind.ExpansionCap);
        Assert.DoesNotContain(Eval(quest, Snapshot() with { MaxExpansion = 3 }), r => r.Req.Kind == RequirementKind.ExpansionCap);

        var r = Only(Eval(quest, Snapshot() with { MaxExpansion = 2 }), RequirementKind.ExpansionCap);
        Assert.False(r.Met);
        Assert.Equal("requires Shadowbringers", r.Detail);
    }

    [Fact]
    public void Level_cap_appears_only_when_exceeded()
    {
        var quest = Quest(Target) with { Level = 70 };

        Assert.DoesNotContain(Eval(quest, Snapshot()), r => r.Req.Kind == RequirementKind.LevelCap);

        var r = Only(Eval(quest, Snapshot() with { LevelCap = 60 }), RequirementKind.LevelCap);
        Assert.False(r.Met);
        Assert.Equal("level 70 is above your cap of 60", r.Detail);
    }

    [Fact]
    public void ClassJob_uses_lookup_and_null_lookup_admits_all()
    {
        var quest = Quest(Target) with { ClassJobCategory = 5 };

        Assert.True(Only(Eval(quest, Snapshot()), RequirementKind.ClassJob).Met);

        var ctx = new EvalContext { ClassJobs = new Jobs((5, [Conjurer])) };
        var r = Only(Eval(quest, Snapshot(), ctx: ctx), RequirementKind.ClassJob);
        Assert.False(r.Met);
        Assert.Equal("not available on the current job", r.Detail);
    }

    [Fact]
    public void ClassJobRequired_pins_a_single_job()
    {
        var quest = Quest(Target) with { ClassJobRequired = Conjurer };

        Assert.False(Only(Eval(quest, Snapshot()), RequirementKind.ClassJob).Met);
        Assert.True(Only(Eval(quest, Snapshot() with { CurrentJob = Conjurer, JobLevels = Levels((Conjurer, 1)) }), RequirementKind.ClassJob).Met);
    }

    [Fact]
    public void Level_uses_unsynced_job_level_and_reports_gap()
    {
        var quest = Quest(Target) with { Level = 50 };

        var met = Only(Eval(quest, Snapshot()), RequirementKind.Level);
        Assert.True(met.Met);
        Assert.Equal("level 50", met.Detail);

        var unmet = Only(Eval(quest, Snapshot() with { JobLevels = Levels((Gladiator, 30)) }), RequirementKind.Level);
        Assert.False(unmet.Met);
        Assert.Equal("needs level 50, you are 30", unmet.Detail);
        var req = Assert.IsType<LevelRequirement>(unmet.Req);
        Assert.Equal((50, 30), (req.Level, req.ActualLevel));
    }

    [Fact]
    public void Level_for_job_without_a_recorded_level_is_zero()
    {
        var quest = Quest(Target) with { Level = 5 };

        var r = Only(Eval(quest, Snapshot() with { CurrentJob = Conjurer }), RequirementKind.Level);

        Assert.False(r.Met);
        Assert.Equal("needs level 5, you are 0", r.Detail);
    }

    [Fact]
    public void PreviousQuests_all_join_counts_done()
    {
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A, B, C], JoinKind.All) };

        var r = Only(Eval(quest, Snapshot(A, C)), RequirementKind.PreviousQuests);

        Assert.False(r.Met);
        Assert.Equal("2 of 3 prerequisites done", r.Detail);
        var req = Assert.IsType<PreviousQuestsRequirement>(r.Req);
        Assert.Equal((JoinKind.All, 2), (req.Join, req.DoneCount));

        Assert.True(Only(Eval(quest, Snapshot(A, B, C)), RequirementKind.PreviousQuests).Met);
    }

    [Fact]
    public void PreviousQuests_any_join_is_met_by_one()
    {
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A, B, C], JoinKind.Any) };

        var unmet = Only(Eval(quest, Snapshot()), RequirementKind.PreviousQuests);
        Assert.False(unmet.Met);
        Assert.Equal("0 of 3 prerequisites done, one needed", unmet.Detail);

        var met = Only(Eval(quest, Snapshot(B)), RequirementKind.PreviousQuests);
        Assert.True(met.Met);
        Assert.Equal("1 of 3 prerequisites done, one needed", met.Detail);
    }

    [Fact]
    public void Single_prerequisite_is_named()
    {
        var prereq = Quest(A, "Coming to Gridania");
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = Catalog(prereq, quest);

        Assert.Equal("needs Coming to Gridania", Only(Eval(quest, Snapshot(), catalog), RequirementKind.PreviousQuests).Detail);
        Assert.Equal("Coming to Gridania done", Only(Eval(quest, Snapshot(A), catalog), RequirementKind.PreviousQuests).Detail);
    }

    [Fact]
    public void GrandCompany_requires_membership_and_rank()
    {
        var quest = Quest(Target) with { GrandCompany = 1, GrandCompanyRank = 5 };

        var other = Eval(quest, Snapshot() with { GrandCompany = 2, GcRanks = [0, 0, 7] });
        var gc = Only(other, RequirementKind.GrandCompany);
        Assert.False(gc.Met);
        Assert.Equal("requires Maelstrom", gc.Detail);
        var rank = Only(other, RequirementKind.GrandCompanyRank);
        Assert.False(rank.Met);
        Assert.Equal("needs Maelstrom rank 5, you are rank 0", rank.Detail);

        var member = Eval(quest, Snapshot() with { GrandCompany = 1, GcRanks = [0, 5] });
        Assert.True(Only(member, RequirementKind.GrandCompany).Met);
        Assert.True(Only(member, RequirementKind.GrandCompanyRank).Met);
    }

    [Fact]
    public void GrandCompanyRank_without_a_specific_company_uses_the_characters_own()
    {
        var quest = Quest(Target) with { GrandCompanyRank = 3 };

        var results = Eval(quest, Snapshot() with { GrandCompany = 3, GcRanks = [0, 0, 0, 2] });

        Assert.DoesNotContain(results, r => r.Req.Kind == RequirementKind.GrandCompany);
        var rank = Only(results, RequirementKind.GrandCompanyRank);
        Assert.False(rank.Met);
        Assert.Equal("needs Immortal Flames rank 3, you are rank 2", rank.Detail);
    }

    [Fact]
    public void Tribe_rank_fails_with_named_ranks()
    {
        var quest = Quest(Target) with { BeastTribe = 2, BeastRank = 7 };
        var snapshot = Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [2] = new(4, 0) } };

        var r = Only(Eval(quest, snapshot), RequirementKind.TribeRank);

        Assert.False(r.Met);
        Assert.Equal("needs Sworn, you are Trusted", r.Detail);
        var req = Assert.IsType<TribeRankRequirement>(r.Req);
        Assert.Equal((2, 7, 4), (req.Tribe, req.RequiredRank, req.ActualRank));
    }

    [Fact]
    public void Tribe_rank_for_unknown_tribe_is_None()
    {
        var quest = Quest(Target) with { BeastTribe = 2, BeastRank = 1 };

        var r = Only(Eval(quest, Snapshot()), RequirementKind.TribeRank);

        Assert.False(r.Met);
        Assert.Equal("needs Neutral, you are None", r.Detail);
    }

    [Fact]
    public void Tribe_reputation_fails_separately_from_rank()
    {
        var quest = Quest(Target) with { BeastTribe = 2, BeastRank = 4, BeastValue = 500 };
        var snapshot = Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [2] = new(4, 320) } };

        var results = Eval(quest, snapshot);

        Assert.True(Only(results, RequirementKind.TribeRank).Met);
        var rep = Only(results, RequirementKind.TribeReputation);
        Assert.False(rep.Met);
        Assert.Equal("needs 500 reputation, you have 320", rep.Detail);
        Assert.DoesNotContain(results, r => r.Req.Kind == RequirementKind.TribeAllowance);
        Assert.DoesNotContain(results, r => r.Req.Kind == RequirementKind.TribeDailyOffer);
    }

    [Fact]
    public void Tribe_allowance_fails_for_repeatable_tribe_quest_when_none_left()
    {
        var quest = Quest(Target) with { BeastTribe = 2, BeastRank = 1, IsRepeatable = true };
        var snapshot = Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [2] = new(3, 0) }, TribeAllowance = 0 };

        var results = Eval(quest, snapshot);

        Assert.True(Only(results, RequirementKind.TribeRank).Met);
        var allowance = Only(results, RequirementKind.TribeAllowance);
        Assert.False(allowance.Met);
        Assert.Equal("no allowances left today", allowance.Detail);

        var withAllowance = Only(Eval(quest, snapshot with { TribeAllowance = 6 }), RequirementKind.TribeAllowance);
        Assert.True(withAllowance.Met);
        Assert.Equal("6 allowances left", withAllowance.Detail);
    }

    [Fact]
    public void Tribe_daily_offer_fails_when_not_offered_today_and_is_skipped_when_unknown()
    {
        var quest = Quest(Target) with { BeastTribe = 2, BeastRank = 1, IsRepeatable = true };
        var snapshot = Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [2] = new(3, 0) }, TribeAllowance = 6 };

        Assert.DoesNotContain(Eval(quest, snapshot), r => r.Req.Kind == RequirementKind.TribeDailyOffer);

        var notOffered = new EvalContext { TodaysDailyOffer = new HashSet<ushort> { 1 } };
        var r = Only(Eval(quest, snapshot, ctx: notOffered), RequirementKind.TribeDailyOffer);
        Assert.False(r.Met);
        Assert.Equal("not offered today", r.Detail);

        var offered = new EvalContext { TodaysDailyOffer = new HashSet<ushort> { quest.QuestId } };
        Assert.True(Only(Eval(quest, snapshot, ctx: offered), RequirementKind.TribeDailyOffer).Met);
    }

    [Fact]
    public void Duty_completion_counts_unlocked_instances_with_join()
    {
        var quest = Quest(Target) with { InstanceContentRequired = [7, 8], InstanceJoin = JoinKind.All };

        var r = Only(Eval(quest, Snapshot() with { UnlockedInstances = [7] }), RequirementKind.DutyCompletion);
        Assert.False(r.Met);
        Assert.Equal("1 of 2 duties completed", r.Detail);

        var any = quest with { InstanceJoin = JoinKind.Any };
        Assert.True(Only(Eval(any, Snapshot() with { UnlockedInstances = [7] }), RequirementKind.DutyCompletion).Met);
    }

    [Fact]
    public void Seasonal_requires_active_festival()
    {
        var quest = Quest(Target) with { Festival = 9 };

        var inactive = Only(Eval(quest, Snapshot()), RequirementKind.Seasonal);
        Assert.False(inactive.Met);
        Assert.Equal("seasonal event not active", inactive.Detail);

        Assert.True(Only(Eval(quest, Snapshot() with { ActiveFestivals = [9] }), RequirementKind.Seasonal).Met);
    }

    [Fact]
    public void Accept_conditions_are_listed_but_not_checked()
    {
        var quest = Quest(Target) with { AcceptConditions = [11, 12] };

        var r = Only(Eval(quest, Snapshot()), RequirementKind.AcceptCondition);

        Assert.True(r.Met);
        Assert.Equal("2 accept conditions not checked", r.Detail);
    }

    [Fact]
    public void Mount_and_house_use_context_hooks_and_pass_when_unknown()
    {
        var quest = Quest(Target) with { MountRequired = true, HouseRequired = true };

        var unknown = Eval(quest, Snapshot());
        Assert.True(Only(unknown, RequirementKind.Mount).Met);
        Assert.True(Only(unknown, RequirementKind.House).Met);

        var ctx = new EvalContext { HasMount = false, HasHouse = false };
        var known = Eval(quest, Snapshot(), ctx: ctx);
        var mount = Only(known, RequirementKind.Mount);
        Assert.False(mount.Met);
        Assert.Equal("requires a mount", mount.Detail);
        var house = Only(known, RequirementKind.House);
        Assert.False(house.Met);
        Assert.Equal("requires a house", house.Detail);
    }

    [Fact]
    public void Achievement_gate_is_unmet_until_achievements_load()
    {
        var quest = Quest(Target);
        var ctx = new EvalContext { IsAchievementGated = id => id == Target };

        var notLoaded = Only(Eval(quest, Snapshot() with { AchievementsLoaded = false }, ctx: ctx), RequirementKind.Achievement);
        Assert.False(notLoaded.Met);
        Assert.Equal("achievements not loaded", notLoaded.Detail);

        var loaded = Only(Eval(quest, Snapshot(), ctx: ctx), RequirementKind.Achievement);
        Assert.True(loaded.Met);

        Assert.DoesNotContain(Eval(quest, Snapshot()), r => r.Req.Kind == RequirementKind.Achievement);
    }

    [Fact]
    public void EvaluateForJob_checks_the_given_job()
    {
        var quest = Quest(Target) with { Level = 30, ClassJobCategory = 5 };
        var ctx = new EvalContext { ClassJobs = new Jobs((5, [Conjurer])) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 35)) };

        var results = RequirementEvaluator.EvaluateForJob(quest, snapshot, Catalog(quest), ctx, Conjurer);

        Assert.All(results, r => Assert.True(r.Met));
        Assert.Equal(Conjurer, Assert.IsType<ClassJobRequirement>(Only(results, RequirementKind.ClassJob).Req).Job);
    }

    [Fact]
    public void Rank_and_company_names_cover_the_known_ranges()
    {
        Assert.Equal("None", TribeRanks.Name(0));
        Assert.Equal("Allied", TribeRanks.Name(8));
        Assert.Equal("rank 9", TribeRanks.Name(9));
        Assert.Equal("Order of the Twin Adder", GrandCompanies.Name(2));
        Assert.Equal("Grand Company 4", GrandCompanies.Name(4));
        Assert.Equal("Shadowbringers", Expansions.Name(3));
    }

    [Fact]
    public void Context_name_hooks_replace_the_static_tables_in_details()
    {
        var ctx = new EvalContext
        {
            TribeRankName = rank => $"Rang {rank}",
            GrandCompanyName = gc => $"Compagnie {gc}",
            ExpansionName = ex => $"Extension {ex}",
        };
        var quest = Quest(Target) with
        {
            Expansion = 3,
            GrandCompany = 1,
            GrandCompanyRank = 5,
            BeastTribe = 3,
            BeastRank = 7,
        };
        var snapshot = Snapshot() with
        {
            MaxExpansion = 2,
            GrandCompany = 2,
            GcRanks = [0, 0, 0, 0],
            Tribes = new Dictionary<byte, TribeStanding> { [3] = new TribeStanding(4, 0) },
        };

        var results = Eval(quest, snapshot, ctx: ctx);

        Assert.Equal("requires Extension 3", Only(results, RequirementKind.ExpansionCap).Detail);
        Assert.Equal("requires Compagnie 1", Only(results, RequirementKind.GrandCompany).Detail);
        Assert.Equal("needs Compagnie 1 rank 5, you are rank 0", Only(results, RequirementKind.GrandCompanyRank).Detail);
        Assert.Equal("needs Rang 7, you are Rang 4", Only(results, RequirementKind.TribeRank).Detail);
    }

    [Fact]
    public void Default_context_names_come_from_the_static_tables()
    {
        Assert.Equal(TribeRanks.Name(7), EvalContext.Default.TribeRankName(7));
        Assert.Equal(GrandCompanies.Name(1), EvalContext.Default.GrandCompanyName(1));
        Assert.Equal(Expansions.Name(3), EvalContext.Default.ExpansionName(3));
        Assert.Null(EvalContext.Default.TodaysDailyOffer);
    }
}
