using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// Decision 1's AutoDuty rules (<see cref="AutoDutyPlan"/>): Duty Support, else Trust, else the regular Duty Finder
/// only when Settings allows it; the order in which "Run with AutoDuty" says why it is disabled; and the duties a quest
/// lists (<see cref="QuestDuties"/>).
/// </summary>
public class AutoDutyPlanTests
{
    private static readonly DutyRunInfo Sastasha = new(4, 4, 1036, DutyRunInfo.Dungeons, "Sastasha", OffersDutySupport: true, OffersTrust: false);
    private static readonly DutyRunInfo TrustOnly = new(900, 95, 1200, DutyRunInfo.Dungeons, "Trust only", OffersDutySupport: false, OffersTrust: true);
    private static readonly DutyRunInfo BrayfloxHard = new(20, 20, 362, DutyRunInfo.Dungeons, "Brayflox's Longstop (Hard)", false, false);
    private static readonly DutyRunInfo Trial = new(57, 20002, 1046, DutyRunInfo.Trials, "Trial", false, false);
    private static readonly DutyRunInfo Raid = new(93, 30001, 241, DutyRunInfo.Raids, "Raid", false, false);
    private static readonly DutyRunInfo Guildhest = new(30, 10001, 300, 3, "Guildhest", false, false);

    private static readonly AutoDutyInputs Ready = new(
        CompanionState.Loaded, CompanionState.Loaded, CompanionState.Loaded, Live: true, Busy: false, HasPath: true, Unlocked: true, AllowDutyFinder: false);

    [Fact]
    public void Duty_Support_comes_first_then_Trust()
    {
        Assert.Equal(AutoDutyMode.Support, AutoDutyPlan.ModeFor(Sastasha, allowDutyFinder: true));
        Assert.Equal(AutoDutyMode.Trust, AutoDutyPlan.ModeFor(TrustOnly, allowDutyFinder: false));
        Assert.Equal(AutoDutyMode.Support, AutoDutyPlan.ModeFor(Sastasha with { OffersTrust = true }, allowDutyFinder: false));
    }

    [Fact]
    public void The_regular_Duty_Finder_only_when_allowed_and_by_category()
    {
        Assert.Equal(AutoDutyMode.None, AutoDutyPlan.ModeFor(BrayfloxHard, allowDutyFinder: false));
        Assert.Equal(AutoDutyMode.Regular, AutoDutyPlan.ModeFor(BrayfloxHard, allowDutyFinder: true));
        Assert.Equal(AutoDutyMode.Trial, AutoDutyPlan.ModeFor(Trial, allowDutyFinder: true));
        Assert.Equal(AutoDutyMode.Raid, AutoDutyPlan.ModeFor(Raid, allowDutyFinder: true));
        Assert.Equal(AutoDutyMode.None, AutoDutyPlan.ModeFor(Guildhest, allowDutyFinder: true));
    }

    [Fact]
    public void AutoDuty_takes_the_mode_as_its_own_enum_name()
    {
        Assert.Equal("Meta.DutyModeEnum", AutoDutyPlan.DutyModeSetting);
        Assert.Equal("Support", AutoDutyPlan.SettingValue(AutoDutyMode.Support));
        Assert.Equal("Trust", AutoDutyPlan.SettingValue(AutoDutyMode.Trust));
        Assert.Equal("Regular", AutoDutyPlan.SettingValue(AutoDutyMode.Regular));
        Assert.Equal("Trial", AutoDutyPlan.SettingValue(AutoDutyMode.Trial));
        Assert.Equal("Raid", AutoDutyPlan.SettingValue(AutoDutyMode.Raid));
        Assert.Null(AutoDutyPlan.SettingValue(AutoDutyMode.None));
    }

    [Fact]
    public void A_run_overrides_run_mode_queue_and_loop_count_in_that_order()
    {
        Assert.Equal(
            [
                new KeyValuePair<string, string>("Meta.AutoDutyModeEnum", "Looping"),
                new KeyValuePair<string, string>("Meta.DutyModeEnum", "Trust"),
                new KeyValuePair<string, string>("Meta.LoopTimes", "1"),
            ],
            AutoDutyPlan.Overrides(AutoDutyMode.Trust));
        Assert.Empty(AutoDutyPlan.Overrides(AutoDutyMode.None));
    }

    [Fact]
    public void Everything_in_place_runs_in_Duty_Support()
    {
        var choice = AutoDutyPlan.Choose(Sastasha, Ready);
        Assert.True(choice.CanRun);
        Assert.Equal(AutoDutyMode.Support, choice.Mode);
    }

    [Fact]
    public void Blockers_come_in_order_AutoDuty_first()
    {
        var nothing = new AutoDutyInputs(CompanionState.Missing, CompanionState.Missing, CompanionState.Missing, false, true, false, false, false);
        Assert.Equal(AutoDutyBlocker.AutoDutyUnavailable, AutoDutyPlan.Choose(BrayfloxHard, nothing).Blocker);
        Assert.Equal(AutoDutyBlocker.AutoDutyUnavailable, AutoDutyPlan.Choose(Sastasha, Ready with { AutoDuty = CompanionState.Outdated }).Blocker);
        Assert.Equal(AutoDutyBlocker.NeedsVnavmesh, AutoDutyPlan.Choose(Sastasha, Ready with { Vnavmesh = CompanionState.Disabled }).Blocker);
        Assert.Equal(AutoDutyBlocker.NeedsBossMod, AutoDutyPlan.Choose(Sastasha, Ready with { BossMod = CompanionState.Missing }).Blocker);
        Assert.Equal(AutoDutyBlocker.NotLive, AutoDutyPlan.Choose(Sastasha, Ready with { Live = false }).Blocker);
        Assert.Equal(AutoDutyBlocker.Busy, AutoDutyPlan.Choose(Sastasha, Ready with { Busy = true }).Blocker);
        Assert.Equal(AutoDutyBlocker.NoPath, AutoDutyPlan.Choose(Sastasha, Ready with { HasPath = false }).Blocker);
        Assert.Equal(AutoDutyBlocker.NoPath, AutoDutyPlan.Choose(Sastasha, Ready with { HasPath = null }).Blocker);
        Assert.Equal(AutoDutyBlocker.Locked, AutoDutyPlan.Choose(Sastasha, Ready with { Unlocked = false }).Blocker);
        Assert.Equal(AutoDutyBlocker.Locked, AutoDutyPlan.Choose(Sastasha, Ready with { Unlocked = null }).Blocker);
    }

    [Fact]
    public void A_duty_without_Duty_Support_or_Trust_needs_the_setting()
    {
        var off = AutoDutyPlan.Choose(BrayfloxHard, Ready);
        Assert.Equal(AutoDutyBlocker.NeedsDutyFinder, off.Blocker);
        Assert.Equal(AutoDutyMode.None, off.Mode);

        var on = AutoDutyPlan.Choose(BrayfloxHard, Ready with { AllowDutyFinder = true });
        Assert.True(on.CanRun);
        Assert.Equal(AutoDutyMode.Regular, on.Mode);

        Assert.Equal(AutoDutyBlocker.NoQueue, AutoDutyPlan.Choose(Guildhest, Ready with { AllowDutyFinder = true }).Blocker);
    }

    // ------------------------------------------------------------------ QuestDuties

    private static readonly DutyRunIndex Index = DutyRunIndex.From([Sastasha, BrayfloxHard, Trial, TrustOnly, Raid]);

    private static UniqueRewardEntry DutyUnlock(uint quest, uint condition) =>
        new(quest, RewardKind.DutyUnlock, condition, 0, "duty", Confidence.Static, "test");

    [Fact]
    public void Required_duties_come_first_by_instance_then_unlocks()
    {
        var quest = new QuestRecord { RowId = 66000, InstanceContentRequired = [20] };
        var duties = QuestDuties.For(quest, Index, CuratedData.Empty, [DutyUnlock(66000, 57), DutyUnlock(66000, 4)]);
        Assert.Equal([20u, 57u, 4u], duties.Select(static d => d.Duty.ContentFinderConditionId));
        Assert.Equal([QuestDutyRelation.Required, QuestDutyRelation.Unlocks, QuestDutyRelation.Unlocks], duties.Select(static d => d.Relation));
    }

    [Fact]
    public void Unknown_duties_other_quests_and_repeats_are_left_out()
    {
        var quest = new QuestRecord { RowId = 66000, InstanceContentRequired = [4, 777] };
        var entries = new[]
        {
            DutyUnlock(66000, 4),
            DutyUnlock(66001, 57),
            DutyUnlock(66000, 9999),
            new UniqueRewardEntry(66000, RewardKind.Mount, 57, 0, "mount", Confidence.Static, "test"),
        };
        var duties = QuestDuties.For(quest, Index, null, entries);
        var only = Assert.Single(duties);
        Assert.Equal(4u, only.Duty.ContentFinderConditionId);
        Assert.Equal(QuestDutyRelation.Required, only.Relation);
    }

    [Fact]
    public void At_most_four_duties_and_none_without_an_index()
    {
        var quest = new QuestRecord { RowId = 66000, InstanceContentRequired = [4, 20, 20002, 95, 30001] };
        Assert.Equal(QuestDuties.Max, QuestDuties.For(quest, Index, null, null).Count);
        Assert.Empty(QuestDuties.For(quest, DutyRunIndex.Empty, null, null));
    }
}
