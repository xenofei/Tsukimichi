using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.19.0 "Right answers" duty features (feature plan v7; spec-1.19): How you'll clear it and the
/// item-level wall (C7), the side quests the story needs in the catch-up and the story meter (N3), and the Duties
/// board (N4).
/// </summary>
static partial class Strings
{
    public static string DutyClearHeading => Loc.Get("DutyClearHeading");

    public static string DutyClearCaptionOne => Loc.Get("DutyClearCaptionOne");

    /// <summary>{0} = how many duties the quest involves.</summary>
    public static string DutyClearCaptionFormat => Loc.Get("DutyClearCaptionFormat");

    public static string DutyBadgeSoloWithNpcs => Loc.Get("DutyBadgeSoloWithNpcs");

    public static string DutyBadgeSoloWithNpcsTip => Loc.Get("DutyBadgeSoloWithNpcsTip");

    public static string DutyBadgeSolo => Loc.Get("DutyBadgeSolo");

    public static string DutyBadgeSoloTip => Loc.Get("DutyBadgeSoloTip");

    /// <summary>{0} = the players it seats (4, 8, 24).</summary>
    public static string DutyBadgeGroupFormat => Loc.Get("DutyBadgeGroupFormat");

    public static string DutyBadgeGroupTip => Loc.Get("DutyBadgeGroupTip");

    /// <summary>{0} = the roulettes' short names.</summary>
    public static string DutyBadgeGroupRoulettesFormat => Loc.Get("DutyBadgeGroupRoulettesFormat");

    public static string DutyBadgeGroupPartyTip => Loc.Get("DutyBadgeGroupPartyTip");

    public static string DutyBadgeHighEnd => Loc.Get("DutyBadgeHighEnd");

    public static string DutyBadgeHighEndTip => Loc.Get("DutyBadgeHighEndTip");

    public static string DutyBadgeStoryRequired => Loc.Get("DutyBadgeStoryRequired");

    public static string DutyBadgeStoryRequiredTip => Loc.Get("DutyBadgeStoryRequiredTip");

    public static string DutyBadgeOptional => Loc.Get("DutyBadgeOptional");

    public static string DutyBadgeOptionalTip => Loc.Get("DutyBadgeOptionalTip");

    /// <summary>The item-level wall before Patch 8.0. {0} = the duty's item level, {1} = the current job's, {2} = its abbreviation (DRG).</summary>
    public static string DutyWallJobFormat => Loc.Get("DutyWallJobFormat");

    /// <summary>After the wall, the best gearset that qualifies. {0} = the job's abbreviation (WAR), {1} = its item level.</summary>
    public static string DutyWallGearsetFormat => Loc.Get("DutyWallGearsetFormat");

    /// <summary>The item-level wall from Patch 8.0 (your highest item level counts for every job). {0} = the duty's, {1} = yours.</summary>
    public static string DutyWallSharedFormat => Loc.Get("DutyWallSharedFormat");

    /// <summary>{0} = a job's abbreviation, {1} = its item level.</summary>
    public static string DutyWallJobLevelFormat => Loc.Get("DutyWallJobLevelFormat");

    /// <summary>{0} = "SGE (i705)".</summary>
    public static string DutyWallAlsoOneFormat => Loc.Get("DutyWallAlsoOneFormat");

    /// <summary>{0} = "SGE (i705) and WHM (i700)".</summary>
    public static string DutyWallAlsoManyFormat => Loc.Get("DutyWallAlsoManyFormat");

    /// <summary>Joins the last two jobs of the also-qualifies line.</summary>
    public static string DutyWallAnd => Loc.Get("DutyWallAnd");

    public static string DutyWallNpcNote => Loc.Get("DutyWallNpcNote");

    /// <summary>{0} = every quest left ("151 quests"), {1} = how many of them are side quests the story needs.</summary>
    public static string PlanningCatchUpSideFormat => Loc.Get("PlanningCatchUpSideFormat");

    public static string PlanningSideQuestsOne => Loc.Get("PlanningSideQuestsOne");

    /// <summary>{0} = the count.</summary>
    public static string PlanningSideQuestsFormat => Loc.Get("PlanningSideQuestsFormat");

    /// <summary>{0} = "8 side quests", {1} = the main scenario quest that needs them (spoiler-masked).</summary>
    public static string PlanningCatchUpSideBeforeFormat => Loc.Get("PlanningCatchUpSideBeforeFormat");

    /// <summary>An expansion's catch-up line with its side quests. {0} = the line, {1} = "8 side quests".</summary>
    public static string PlanningCatchUpExpansionSideFormat => Loc.Get("PlanningCatchUpExpansionSideFormat");

    public static string PlanningCatchUpOthersOne => Loc.Get("PlanningCatchUpOthersOne");

    /// <summary>{0} = how many duties left on the story path have no Duty Support or Trust.</summary>
    public static string PlanningCatchUpOthersFormat => Loc.Get("PlanningCatchUpOthersFormat");

    /// <summary>{0} = the highest item level a story duty left asks, {1} = the character's.</summary>
    public static string PlanningCatchUpWallFormat => Loc.Get("PlanningCatchUpWallFormat");

    /// <summary>The MSQ pill's hover title. {0} = done, {1} = in all (the side quests the story requires included), {2} = whole percent.</summary>
    public static string MsqMeterTitleFormat => Loc.Get("MsqMeterTitleFormat");

    public static string MsqMeterCounts => Loc.Get("MsqMeterCounts");

    public static string MsqMeterMilestoneOne => Loc.Get("MsqMeterMilestoneOne");

    /// <summary>{0} = main scenario quests left to the end of the current patch's story.</summary>
    public static string MsqMeterMilestoneFormat => Loc.Get("MsqMeterMilestoneFormat");

    /// <summary>{0} = the side quests skipped, joined ("Crystal Tower Quests (8 quests at Lv 50)").</summary>
    public static string MsqMeterEarlierFormat => Loc.Get("MsqMeterEarlierFormat");

    /// <summary>{0} = the line's name (its journal genre, or its one quest), {1} = "8 quests", {2} = the highest level among them.</summary>
    public static string MsqMeterEarlierItemFormat => Loc.Get("MsqMeterEarlierItemFormat");

    public static string DutyBoardHeading => Loc.Get("DutyBoardHeading");

    public static string DutyBoardTooltip => Loc.Get("DutyBoardTooltip");

    public static string DutyBoardLockedOne => Loc.Get("DutyBoardLockedOne");

    /// <summary>{0} = how many.</summary>
    public static string DutyBoardLockedFormat => Loc.Get("DutyBoardLockedFormat");

    public static string DutyBoardNotRead => Loc.Get("DutyBoardNotRead");

    /// <summary>A roulette a stored capture of an older duty list cannot judge.</summary>
    public static string DutyBoardUnknownState => Loc.Get("DutyBoardUnknownState");

    /// <summary>{0} = the roulette's short name ("Level Cap Dungeons").</summary>
    public static string DutyBoardRouletteFormat => Loc.Get("DutyBoardRouletteFormat");

    /// <summary>{0} = the roulette's level, {1} = the best job's abbreviation, {2} = its level.</summary>
    public static string DutyBoardLockedLevelFormat => Loc.Get("DutyBoardLockedLevelFormat");

    /// <summary>{0} = the expansion's name.</summary>
    public static string DutyBoardLockedExpansionFormat => Loc.Get("DutyBoardLockedExpansionFormat");

    /// <summary>{0} = "2 dungeons not unlocked".</summary>
    public static string DutyBoardLockedEveryFormat => Loc.Get("DutyBoardLockedEveryFormat");

    /// <summary>{0} = how many more duties.</summary>
    public static string DutyBoardLockedSomeFormat => Loc.Get("DutyBoardLockedSomeFormat");

    /// <summary>{0} = "1 raid not unlocked".</summary>
    public static string DutyBoardOpenLeftFormat => Loc.Get("DutyBoardOpenLeftFormat");

    public static string DutyBoardDungeonsOne => Loc.Get("DutyBoardDungeonsOne");

    public static string DutyBoardDungeonsFormat => Loc.Get("DutyBoardDungeonsFormat");

    public static string DutyBoardTrialsOne => Loc.Get("DutyBoardTrialsOne");

    public static string DutyBoardTrialsFormat => Loc.Get("DutyBoardTrialsFormat");

    public static string DutyBoardRaidsOne => Loc.Get("DutyBoardRaidsOne");

    public static string DutyBoardRaidsFormat => Loc.Get("DutyBoardRaidsFormat");

    public static string DutyBoardGuildhestsOne => Loc.Get("DutyBoardGuildhestsOne");

    public static string DutyBoardGuildhestsFormat => Loc.Get("DutyBoardGuildhestsFormat");

    public static string DutyBoardDutiesOne => Loc.Get("DutyBoardDutiesOne");

    public static string DutyBoardDutiesFormat => Loc.Get("DutyBoardDutiesFormat");

    /// <summary>Before the unlock quest's name, which follows in Text.</summary>
    public static string DutyBoardWith => Loc.Get("DutyBoardWith");

    public static string DutyBoardNoQuest => Loc.Get("DutyBoardNoQuest");

    /// <summary>{0} = the duty's level. Shown when every quest that unlocks it is hidden by the spoiler shield.</summary>
    public static string DutyBoardHiddenDutyFormat => Loc.Get("DutyBoardHiddenDutyFormat");

    public static string DutyBoardQuestTip => Loc.Get("DutyBoardQuestTip");

    public static string DutyBoardRoute => Loc.Get("DutyBoardRoute");

    public static string DutyBoardRouteTip => Loc.Get("DutyBoardRouteTip");

    public static string DutyBoardPinOne => Loc.Get("DutyBoardPinOne");

    public static string DutyBoardPinBoth => Loc.Get("DutyBoardPinBoth");

    public static string DutyBoardPinAll => Loc.Get("DutyBoardPinAll");

    public static string DutyBoardPinTip => Loc.Get("DutyBoardPinTip");

    public static string DutyBoardPinnedTip => Loc.Get("DutyBoardPinnedTip");

    /// <summary>{0} = how many rows are folded; opens them in the same card.</summary>
    public static string DutyBoardMoreFormat => Loc.Get("DutyBoardMoreFormat");

    public static string DutyBoardFewer => Loc.Get("DutyBoardFewer");

    public static string DutyBoardNeverHeading => Loc.Get("DutyBoardNeverHeading");
}
