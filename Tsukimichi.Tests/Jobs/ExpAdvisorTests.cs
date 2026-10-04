using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.JobLadders;

/// <summary>
/// EXP to the right job (feature plan v7, C8): the game gives a quest's EXP to the job the character hands it in on, so
/// the advisor says which job that is, warns when the EXP would be lost or another job gets far more, and names the job
/// a class or job quest needs. A synthetic table keeps the sums plain: EXP = ExpFactor × level.
/// </summary>
public class ExpAdvisorTests
{
    private const byte Marauder = 3;
    private const byte Warrior = 21;
    private const byte WhiteMage = 24;
    private const byte BlueMage = 36;
    private const byte Culinarian = 15;
    private const uint Tanks = 156;
    private const uint CulinarianOnly = 24;

    /// <summary>Every level's row reads modifier = level and scale = 100, so the formula gives ExpFactor × level.</summary>
    private static readonly QuestExpTable Table = QuestExpTable.From(Enumerable.Range(1, 100).Select(l => (l, (uint)l, 100u)));

    private static readonly EvalContext Context = new()
    {
        ParentJob = static job => job switch { Paladin => Gladiator, Warrior => Marauder, WhiteMage => Conjurer, _ => job },
        ClassJobs = new Jobs((Tanks, [Gladiator, Paladin, Marauder, Warrior])),
    };

    private static QuestRecord Side(byte level = 50, uint factor = 10) => Quest(Target) with { Level = level, ExpFactor = factor };

    private static CharacterSnapshot On(byte current, params (byte Job, short Level)[] levels) => Snapshot() with
    {
        CurrentJob = current,
        LevelCap = 100,
        JobLevels = Levels(levels),
    };

    [Fact]
    public void A_quest_any_job_takes_goes_to_the_current_job_and_a_tie_keeps_it()
    {
        var advice = ExpAdvisor.Advise(Side(), On(Warrior, (Warrior, 92), (WhiteMage, 80)), Table, Context);

        Assert.NotNull(advice);
        Assert.Equal(new JobExp(Warrior, 92, 500), advice.Current);
        Assert.True(advice.CurrentTakes);
        Assert.Equal(Warrior, advice.Best?.Job);
        Assert.Equal(ExpWarning.None, advice.Warning);
        Assert.False(advice.NamesOther);
    }

    [Fact]
    public void A_job_at_the_cap_loses_the_exp_and_the_highest_other_job_is_named()
    {
        var advice = ExpAdvisor.Advise(Side(), On(Paladin, (Gladiator, 100), (Paladin, 100), (Warrior, 92), (WhiteMage, 80)), Table, Context);

        Assert.NotNull(advice);
        Assert.Equal(0UL, advice.Current.Exp);
        Assert.Equal(ExpWarning.Capped, advice.Warning);
        Assert.Equal(new JobExp(Warrior, 92, 500), advice.Best);
        Assert.True(advice.NamesOther);
    }

    [Fact]
    public void When_every_job_is_capped_there_is_nothing_better_to_name()
    {
        var advice = ExpAdvisor.Advise(Side(), On(Paladin, (Paladin, 100), (Warrior, 100)), Table, Context);

        Assert.NotNull(advice);
        Assert.Equal(ExpWarning.None, advice.Warning);
        Assert.Equal(0UL, advice.Current.Exp);
    }

    [Fact]
    public void Under_quest_sync_the_amount_follows_the_job_s_level_and_far_more_elsewhere_warns()
    {
        var synced = Side(level: 10) with { LevelMax = 50 };

        var close = ExpAdvisor.Advise(synced, On(WhiteMage, (WhiteMage, 40), (Warrior, 60)), Table, Context);
        Assert.NotNull(close);
        Assert.Equal(ExpKind.Range, close.Reward.Kind);
        Assert.Equal(400UL, close.Current.Exp);
        Assert.Equal(new JobExp(Warrior, 60, 500), close.Best);
        Assert.Equal(ExpWarning.None, close.Warning);

        var far = ExpAdvisor.Advise(synced, On(WhiteMage, (WhiteMage, 12), (Warrior, 60)), Table, Context);
        Assert.NotNull(far);
        Assert.Equal(120UL, far.Current.Exp);
        Assert.Equal(ExpWarning.LessThanBest, far.Warning);
        Assert.Equal(Warrior, far.Best?.Job);
    }

    [Fact]
    public void A_job_quest_names_the_job_it_needs_never_its_base_class_or_a_job_under_its_level()
    {
        var paladinQuest = Side() with { ClassJobRequired = Paladin };
        var advice = ExpAdvisor.Advise(paladinQuest, On(Warrior, (Warrior, 90), (Gladiator, 60), (Paladin, 60)), Table, Context);

        Assert.NotNull(advice);
        Assert.False(advice.CurrentTakes);
        Assert.Equal(ExpWarning.NeedsJob, advice.Warning);
        Assert.Equal(new JobExp(Paladin, 60, 500), advice.Best);

        var tooLow = ExpAdvisor.Advise(paladinQuest, On(Warrior, (Warrior, 90), (Paladin, 40)), Table, Context);
        Assert.NotNull(tooLow);
        Assert.Equal(ExpWarning.NeedsJob, tooLow.Warning);
        Assert.Null(tooLow.Best);
    }

    [Fact]
    public void A_class_quest_on_its_job_names_the_class()
    {
        // Gladiator's own quest while on Paladin (not accepted on it): only Gladiator takes it.
        var classQuest = Side() with { ClassJobRequired = Gladiator };
        var advice = ExpAdvisor.Advise(classQuest, On(Paladin, (Gladiator, 60), (Paladin, 60)), Table, Context);

        Assert.NotNull(advice);
        Assert.Equal(ExpWarning.NeedsJob, advice.Warning);
        Assert.Equal(Gladiator, advice.Best?.Job);
    }

    [Fact]
    public void A_role_quest_suggests_only_jobs_of_its_category_and_never_a_limited_job()
    {
        var tankQuest = Side() with { ClassJobCategory = Tanks };
        var advice = ExpAdvisor.Advise(tankQuest, On(WhiteMage, (WhiteMage, 90), (Warrior, 70), (Paladin, 80)), Table, Context);
        Assert.NotNull(advice);
        Assert.Equal(ExpWarning.NeedsJob, advice.Warning);
        Assert.Equal(Paladin, advice.Best?.Job);

        // A limited job is never the job to hand a quest in on.
        var any = ExpAdvisor.Advise(Side(), On(Paladin, (Paladin, 100), (BlueMage, 70)), Table, Context, isLimited: job => job == BlueMage);
        Assert.NotNull(any);
        Assert.Equal(ExpWarning.None, any.Warning);
        Assert.Equal(Paladin, any.Best?.Job);
    }

    [Fact]
    public void No_known_amount_or_no_job_gives_no_advice()
    {
        Assert.Null(ExpAdvisor.Advise(Side(factor: 0), On(Warrior, (Warrior, 90)), Table, Context));
        Assert.Null(ExpAdvisor.Advise(Side() with { BeastTribe = 2 }, On(Warrior, (Warrior, 90)), Table, Context));
        Assert.Null(ExpAdvisor.Advise(Side(), On(Warrior, (Warrior, 90)), QuestExpTable.Empty, Context));
        Assert.Null(ExpAdvisor.Advise(Side(), On(0), Table, Context));
    }

    [Fact]
    public void For_level_is_zero_at_the_cap_the_fixed_amount_or_the_clamped_sync_amount()
    {
        var fixedQuest = Side();
        Assert.Equal(500UL, QuestExp.ForLevel(fixedQuest, Table, 60, 100));
        Assert.Equal(0UL, QuestExp.ForLevel(fixedQuest, Table, 100, 100));
        Assert.Equal(500UL, QuestExp.ForLevel(fixedQuest, Table, 100, 0));

        var synced = Side(level: 10) with { LevelMax = 50 };
        Assert.Equal(100UL, QuestExp.ForLevel(synced, Table, 3, 100));
        Assert.Equal(300UL, QuestExp.ForLevel(synced, Table, 30, 100));
        Assert.Equal(500UL, QuestExp.ForLevel(synced, Table, 90, 100));
        Assert.Null(QuestExp.ForLevel(Side(factor: 0), Table, 30, 100));
    }

    [Fact]
    public void Switch_gearset_is_offered_only_for_a_quest_that_needs_a_job_the_current_one_is_not()
    {
        var snapshot = On(Warrior, (Warrior, 90), (Paladin, 60));
        Assert.False(GearsetChoice.Needed(Side(), snapshot, Context, QuestState.Ready));
        Assert.True(GearsetChoice.Needed(Side() with { ClassJobRequired = Paladin }, snapshot, Context, QuestState.ReadyOnOtherJob));
        Assert.False(GearsetChoice.Needed(Side() with { ClassJobRequired = Warrior }, snapshot, Context, QuestState.Ready));
        Assert.False(GearsetChoice.Needed(Side() with { ClassJobRequired = Paladin }, snapshot, Context, QuestState.Completed));
        Assert.False(GearsetChoice.Needed(Side() with { ClassJobCategory = Tanks }, snapshot, Context, QuestState.Ready));
    }

    [Fact]
    public void The_gearset_picked_is_the_ready_job_s_then_the_highest_item_level_then_the_first()
    {
        var snapshot = On(WhiteMage, (WhiteMage, 90), (Warrior, 90), (Paladin, 90));
        var tankQuest = Side() with { ClassJobCategory = Tanks };
        GearsetInfo[] sets =
        [
            new(0, WhiteMage, 700, "Healer"),
            new(1, Warrior, 690, "War"),
            new(2, Paladin, 680, "Pld"),
            new(3, Warrior, 690, "War alt"),
        ];

        Assert.Equal(1, GearsetChoice.Pick(sets, tankQuest, snapshot, Context)?.Id);
        Assert.Equal(2, GearsetChoice.Pick(sets, tankQuest, snapshot, Context, preferredJob: Paladin)?.Id);
        Assert.Null(GearsetChoice.Pick([sets[0]], tankQuest, snapshot, Context));
    }

    [Fact]
    public void A_gearset_whose_job_is_under_the_quest_s_level_is_never_picked()
    {
        // Review fix: the Warrior set has the higher item level, but Warrior is 30 and the quest is level 50.
        var snapshot = On(WhiteMage, (WhiteMage, 90), (Warrior, 30), (Paladin, 60));
        var tankQuest = Side() with { ClassJobCategory = Tanks };
        GearsetInfo[] sets = [new(0, Warrior, 700, "War"), new(1, Paladin, 600, "Pld")];
        Assert.Equal(1, GearsetChoice.Pick(sets, tankQuest, snapshot, Context)?.Id);
        Assert.Null(GearsetChoice.Pick([sets[0]], tankQuest, snapshot, Context));

        // A job whose level the capture does not know is not ruled out.
        Assert.Equal(0, GearsetChoice.Pick([sets[0]], tankQuest, On(WhiteMage, (WhiteMage, 90)), Context)?.Id);
    }

    [Fact]
    public void Switch_gearset_is_only_for_a_quest_pinned_to_one_class_or_job()
    {
        // spec-1.19 C8 and decision 6: ClassJobRequired, or a category of one job; a role quest several jobs take is not.
        var crafting = new EvalContext { ClassJobs = new Jobs((Tanks, [Gladiator, Paladin, Marauder, Warrior]), (CulinarianOnly, [Culinarian])) };
        Assert.Equal(Paladin, GearsetChoice.PinnedJob(Side() with { ClassJobRequired = Paladin }, crafting));
        Assert.Equal(Culinarian, GearsetChoice.PinnedJob(Side() with { ClassJobCategory = CulinarianOnly }, crafting));
        Assert.Null(GearsetChoice.PinnedJob(Side() with { ClassJobCategory = Tanks }, crafting));
        Assert.Null(GearsetChoice.PinnedJob(Side(), crafting));

        var onHealer = On(WhiteMage, (WhiteMage, 90), (Warrior, 90), (Culinarian, 60));
        Assert.False(GearsetChoice.Needed(Side() with { ClassJobCategory = Tanks }, onHealer, crafting, QuestState.ReadyOnOtherJob));
        Assert.True(GearsetChoice.Needed(Side() with { ClassJobCategory = CulinarianOnly }, onHealer, crafting, QuestState.ReadyOnOtherJob));
    }

    [Fact]
    public void Switch_gearset_equips_the_first_gearset_of_the_job()
    {
        var snapshot = On(Warrior, (Warrior, 90), (Gladiator, 60), (Paladin, 30));
        var paladinQuest = Side() with { ClassJobRequired = Paladin };
        GearsetInfo[] sets = [new(4, Paladin, 690, "Pld best"), new(2, Paladin, 600, "Pld old"), new(0, Warrior, 700, "War")];

        // The lowest id of that job, even under the quest's level: the Level requirement says what is missing.
        Assert.Equal(2, GearsetChoice.First(sets, Paladin, paladinQuest, snapshot, Context)?.Id);

        // No gearset of the job: "No Paladin gearset saved".
        Assert.Null(GearsetChoice.First([sets[2]], Paladin, paladinQuest, snapshot, Context));
    }

    [Fact]
    public void Another_capped_job_that_could_hand_it_in_is_named_best_geared_first()
    {
        // spec-1.19 C8: "Hand in on DRG Lv 56: … · your SGE is capped, 0".
        var snapshot = On(Warrior, (Warrior, 56), (WhiteMage, 100), (Paladin, 100)) with
        {
            JobItemLevels = new Dictionary<byte, ushort> { [WhiteMage] = 700, [Paladin] = 710 },
        };
        var advice = ExpAdvisor.Advise(Side(), snapshot, Table, Context);
        Assert.NotNull(advice);
        Assert.Equal(ExpWarning.None, advice.Warning);
        Assert.Equal(new JobExp(Paladin, 100, 0), advice.Capped);

        // A capped job that cannot take the quest is never named; nor is any without a known cap.
        Assert.Null(ExpAdvisor.Advise(Side() with { ClassJobCategory = Tanks }, On(Warrior, (Warrior, 56), (WhiteMage, 100)), Table, Context)?.Capped);
        Assert.Null(ExpAdvisor.Advise(Side(), snapshot with { LevelCap = 0 }, Table, Context)?.Capped);

        // No capped job: nothing to say.
        Assert.Null(ExpAdvisor.Advise(Side(), On(Warrior, (Warrior, 56), (WhiteMage, 80)), Table, Context)?.Capped);
    }
}
