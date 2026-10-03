using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the 1.9.0 planning extras (feature plan v5 "Planning extras", R6 C, E, F, G): EXP and gil per quest
/// (the detail pane and the Journal's EXP column), the level advisor, the main scenario catch-up summary and the allied
/// society board, and their settings. Constant names carry the <c>Planning</c> prefix so this part of the partial
/// class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>Between the parts of a list inside one line ("2 unlock quests, MSQ").</summary>
    public const string PlanningListSeparator = ", ";

    // ---- EXP and gil ----
    /// <summary>{0} = the EXP, formatted ("12,345").</summary>
    public static string PlanningExpFormat => Loc.Get("PlanningExpFormat");

    /// <summary>{0} = the EXP at the quest's level, {1} = at its Quest Sync cap.</summary>
    public static string PlanningExpRangeFormat => Loc.Get("PlanningExpRangeFormat");

    /// <summary>{0} = the gil, formatted ("1,200").</summary>
    public static string PlanningGilFormat => Loc.Get("PlanningGilFormat");

    public static string PlanningExpTooltip => Loc.Get("PlanningExpTooltip");

    /// <summary>{0} = the quest's level, {1} = its Quest Sync cap.</summary>
    public static string PlanningExpRangeTooltipFormat => Loc.Get("PlanningExpRangeTooltipFormat");

    public static string PlanningExpVariesTooltip => Loc.Get("PlanningExpVariesTooltip");

    /// <summary>{0} = the first value, {1} = the second ("{0}–{1}"): the Journal column's range.</summary>
    public static string PlanningExpRangeShortFormat => Loc.Get("PlanningExpRangeShortFormat");

    public static string ColumnExp => Loc.Get("ColumnExp");
    public static string ColumnExpTooltip => Loc.Get("ColumnExpTooltip");

    // ---- Counts ----
    public static string PlanningQuestsOne => Loc.Get("PlanningQuestsOne");

    /// <summary>{0} = how many quests.</summary>
    public static string PlanningQuestsFormat => Loc.Get("PlanningQuestsFormat");

    public static string PlanningUnlocksOne => Loc.Get("PlanningUnlocksOne");

    /// <summary>{0} = how many unlock quests.</summary>
    public static string PlanningUnlocksFormat => Loc.Get("PlanningUnlocksFormat");

    /// <summary>The main scenario, as the counts name it.</summary>
    public static string PlanningMsq => Loc.Get("PlanningMsq");

    /// <summary>{0} = what was counted, {1} = what is among it ("7 quests (2 unlock quests, MSQ)").</summary>
    public static string PlanningCountDetailFormat => Loc.Get("PlanningCountDetailFormat");

    public static string PlanningDutiesNone => Loc.Get("PlanningDutiesNone");
    public static string PlanningDutiesOne => Loc.Get("PlanningDutiesOne");

    /// <summary>{0} = how many duties.</summary>
    public static string PlanningDutiesFormat => Loc.Get("PlanningDutiesFormat");

    // ---- Level advisor ----
    public static string PlanningLevelHeading => Loc.Get("PlanningLevelHeading");

    /// <summary>{0} = the level now, {1} = the level needed, {2} = what it opens ("7 quests (2 unlock quests, MSQ)").</summary>
    public static string PlanningLevelRowFormat => Loc.Get("PlanningLevelRowFormat");

    /// <summary>{0} = the job's abbreviation, {1} = its level now, {2} = the level needed, {3} = what it opens.</summary>
    public static string PlanningLevelOpensFormat => Loc.Get("PlanningLevelOpensFormat");

    /// <summary>{0} = the level, {1} = what it opens: one line of the tooltip.</summary>
    public static string PlanningLevelStepFormat => Loc.Get("PlanningLevelStepFormat");

    /// <summary>{0} = how many more quest names the tooltip leaves out.</summary>
    public static string PlanningLevelMoreFormat => Loc.Get("PlanningLevelMoreFormat");

    public static string PlanningLevelRowTooltip => Loc.Get("PlanningLevelRowTooltip");

    /// <summary>{0} = the level the next main scenario quest needs, {1} = the job's abbreviation, {2} = its level now, {3} = what levelling opens.</summary>
    public static string PlanningTonightLevelFormat => Loc.Get("PlanningTonightLevelFormat");

    // ---- Main scenario catch-up ----
    public static string PlanningCatchUpHeading => Loc.Get("PlanningCatchUpHeading");

    /// <summary>{0} = the quests ("143 quests"), {1} = the lowest level, {2} = the highest, {3} = the duties ("6 duties").</summary>
    public static string PlanningCatchUpFormat => Loc.Get("PlanningCatchUpFormat");

    /// <summary>{0} = the quests, {1} = their one level, {2} = the duties.</summary>
    public static string PlanningCatchUpOneLevelFormat => Loc.Get("PlanningCatchUpOneLevelFormat");

    /// <summary>{0} = the expansion, {1} = the quests, {2} = the lowest level, {3} = the highest, {4} = the duties.</summary>
    public static string PlanningCatchUpExpansionFormat => Loc.Get("PlanningCatchUpExpansionFormat");

    /// <summary>{0} = the expansion, {1} = the quests, {2} = their one level, {3} = the duties.</summary>
    public static string PlanningCatchUpExpansionOneLevelFormat => Loc.Get("PlanningCatchUpExpansionOneLevelFormat");

    public static string PlanningCatchUpDone => Loc.Get("PlanningCatchUpDone");
    public static string PlanningCatchUpTooltip => Loc.Get("PlanningCatchUpTooltip");

    // ---- Allied society board ----
    public static string PlanningBoardHeading => Loc.Get("PlanningBoardHeading");

    /// <summary>{0} = allowances left today, {1} = "resets in 3 h".</summary>
    public static string PlanningBoardAllowancesFormat => Loc.Get("PlanningBoardAllowancesFormat");

    public static string PlanningBoardColumnSociety => Loc.Get("PlanningBoardColumnSociety");
    public static string PlanningBoardColumnRank => Loc.Get("PlanningBoardColumnRank");
    public static string PlanningBoardColumnToday => Loc.Get("PlanningBoardColumnToday");
    public static string PlanningBoardColumnWhere => Loc.Get("PlanningBoardColumnWhere");

    /// <summary>{0} = the rank's name, {1} = the reputation, {2} = the rank's maximum.</summary>
    public static string PlanningBoardRankFormat => Loc.Get("PlanningBoardRankFormat");

    /// <summary>{0} = the rank's name.</summary>
    public static string PlanningBoardRankFullFormat => Loc.Get("PlanningBoardRankFullFormat");

    public static string PlanningBoardRankedUpToday => Loc.Get("PlanningBoardRankedUpToday");

    /// <summary>{0} = dailies done today.</summary>
    public static string PlanningBoardTodayUnknownFormat => Loc.Get("PlanningBoardTodayUnknownFormat");

    public static string PlanningBoardTodayUnknownTooltip => Loc.Get("PlanningBoardTodayUnknownTooltip");
    public static string PlanningBoardTeleport => Loc.Get("PlanningBoardTeleport");
    public static string PlanningBoardNone => Loc.Get("PlanningBoardNone");

    // ---- Settings ----
    public static string PlanningConfigHeading => Loc.Get("PlanningConfigHeading");
    public static string PlanningConfigExpColumn => Loc.Get("PlanningConfigExpColumn");
    public static string PlanningConfigExpColumnHint => Loc.Get("PlanningConfigExpColumnHint");
    public static string PlanningConfigBoard => Loc.Get("PlanningConfigBoard");
    public static string PlanningConfigBoardHint => Loc.Get("PlanningConfigBoardHint");
}
