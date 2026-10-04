using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.19.0 "Right answers" duty features (feature plan v7): how a duty can be cleared and its item-level
/// wall (C7), the side quests the story needs in the catch-up and the story meter (N3), and the Duties board (N4).
/// </summary>
static partial class Strings
{
    public static string DutyClearSolo => Loc.Get("DutyClearSolo");

    public static string DutyClearSupport => Loc.Get("DutyClearSupport");

    public static string DutyClearTrust => Loc.Get("DutyClearTrust");

    public static string DutyClearFinder => Loc.Get("DutyClearFinder");

    public static string DutyClearParty => Loc.Get("DutyClearParty");

    public static string DutyClearSoloTip => Loc.Get("DutyClearSoloTip");

    public static string DutyClearSupportTip => Loc.Get("DutyClearSupportTip");

    public static string DutyClearTrustTip => Loc.Get("DutyClearTrustTip");

    public static string DutyClearFinderTip => Loc.Get("DutyClearFinderTip");

    public static string DutyClearPartyTip => Loc.Get("DutyClearPartyTip");

    /// <summary>The item-level wall, not met. {0} = the duty's item level, {1} = the character's.</summary>
    public static string DutyWallNeedsFormat => Loc.Get("DutyWallNeedsFormat");

    /// <summary>The item-level wall, met. {0} = the duty's item level, {1} = the character's.</summary>
    public static string DutyWallMetFormat => Loc.Get("DutyWallMetFormat");

    /// <summary>{0} = the job's abbreviation (WAR), {1} = its gearset's item level.</summary>
    public static string DutyWallGearsetFormat => Loc.Get("DutyWallGearsetFormat");

    /// <summary>{0} = the current job's abbreviation.</summary>
    public static string DutyWallCurrentTipFormat => Loc.Get("DutyWallCurrentTipFormat");

    /// <summary>{0} = the best job's abbreviation, {1} = its item level.</summary>
    public static string DutyWallBestTipFormat => Loc.Get("DutyWallBestTipFormat");

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

    /// <summary>{0} = whole percent, {1} = done, {2} = in all, {3} = the side quests the story needs.</summary>
    public static string MsqStoryMeterFormat => Loc.Get("MsqStoryMeterFormat");

    public static string DutyBoardHeading => Loc.Get("DutyBoardHeading");

    public static string DutyBoardNotRead => Loc.Get("DutyBoardNotRead");

    /// <summary>{0} = the open roulettes' short names.</summary>
    public static string DutyBoardOpenFormat => Loc.Get("DutyBoardOpenFormat");

    /// <summary>{0} = the roulette's level.</summary>
    public static string DutyBoardNeedsLevelFormat => Loc.Get("DutyBoardNeedsLevelFormat");

    public static string DutyBoardNeedsExpansion => Loc.Get("DutyBoardNeedsExpansion");

    /// <summary>{0} = how many.</summary>
    public static string DutyBoardNeedsEveryFormat => Loc.Get("DutyBoardNeedsEveryFormat");

    /// <summary>{0} = how many open it, {1} = how many are still needed.</summary>
    public static string DutyBoardNeedsSomeFormat => Loc.Get("DutyBoardNeedsSomeFormat");

    /// <summary>{0} = the unlock quest (spoiler-masked), {1} = its state.</summary>
    public static string DutyBoardFromFormat => Loc.Get("DutyBoardFromFormat");

    public static string DutyBoardNoQuest => Loc.Get("DutyBoardNoQuest");

    /// <summary>{0} = the duty's level. Shown when every quest that unlocks it is hidden by the spoiler shield.</summary>
    public static string DutyBoardHiddenDutyFormat => Loc.Get("DutyBoardHiddenDutyFormat");

    public static string DutyBoardQuestTip => Loc.Get("DutyBoardQuestTip");

    public static string DutyBoardNeverHeading => Loc.Get("DutyBoardNeverHeading");

    public static string DutyBoardNeverNone => Loc.Get("DutyBoardNeverNone");

    public static string DutyBoardKindDungeons => Loc.Get("DutyBoardKindDungeons");

    public static string DutyBoardKindTrials => Loc.Get("DutyBoardKindTrials");

    public static string DutyBoardKindRaids => Loc.Get("DutyBoardKindRaids");

    public static string DutyBoardKindGuildhests => Loc.Get("DutyBoardKindGuildhests");

    public static string DutyBoardTooltip => Loc.Get("DutyBoardTooltip");
}
