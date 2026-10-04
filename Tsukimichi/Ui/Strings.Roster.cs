using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for every character (plan v7, 1.21.0 P1, P3, N11): Up next at the top of Tonight and the way back to it,
/// the All characters roster with "your other characters" under the detail hero, the linked launcher folders, and
/// alt goals. Every constant is prefixed <c>UpNext</c>, <c>Roster</c>, <c>Others</c>, <c>Goal</c> or <c>LinkedFolders</c>.
/// </summary>
static partial class Strings
{
    /// <summary>Between the parts of a roster or goal line.</summary>
    public const string GoalSeparator = " · ";

    /// <summary>Between the parts of Up next's place line.</summary>
    public const string UpNextSeparator = " · ";

    /// <summary>Tonight's Up next block's eyebrow (1.21 P1).</summary>
    public static string UpNextEyebrow => Loc.Get("UpNextEyebrow");
    /// <summary>Up next's level chip. {0} = the quest's level.</summary>
    public static string UpNextLevelFormat => Loc.Get("UpNextLevelFormat");
    /// <summary>Up next reason (rule 1). {0} = the followed route's target, through the spoiler shield; {1} = stops left.</summary>
    public static string UpNextRouteFormat => Loc.Get("UpNextRouteFormat");
    /// <summary>Up next reason (rule 1), one stop left. {0} = the route's target.</summary>
    public static string UpNextRouteOneFormat => Loc.Get("UpNextRouteOneFormat");
    /// <summary>A route to flying in a zone. {0} = the zone, or the shield's words for one ahead.</summary>
    public static string UpNextFlyingInFormat => Loc.Get("UpNextFlyingInFormat");
    /// <summary>Up next reason (rule 2). {0} = the goal as a phrase ("match Michiru's unlocks"); {1} = what is left.</summary>
    public static string UpNextGoalFormat => Loc.Get("UpNextGoalFormat");
    /// <summary>Up next reason (rule 3).</summary>
    public static string UpNextMsq => Loc.Get("UpNextMsq");
    /// <summary>Up next reason (rule 3), the quest is in the journal.</summary>
    public static string UpNextMsqInJournal => Loc.Get("UpNextMsqInJournal");
    /// <summary>Up next reason (rule 6). {0} = level needed; {1} = job abbreviation; {2} = its level.</summary>
    public static string UpNextGateFormat => Loc.Get("UpNextGateFormat");
    /// <summary>Up next's place line for the level gate.</summary>
    public static string UpNextGateHint => Loc.Get("UpNextGateHint");
    /// <summary>Up next's level gate pill: the Journal on the Ready quests.</summary>
    public static string UpNextGateShow => Loc.Get("UpNextGateShow");
    /// <summary>Up next's order, rule 1 (the reason line's hover).</summary>
    public static string UpNextRuleRoute => Loc.Get("UpNextRuleRoute");
    /// <summary>Up next's order, rule 2.</summary>
    public static string UpNextRuleGoal => Loc.Get("UpNextRuleGoal");
    /// <summary>Up next's order, rule 3.</summary>
    public static string UpNextRuleMsq => Loc.Get("UpNextRuleMsq");
    /// <summary>Up next's order, rule 4, and its reason line.</summary>
    public static string UpNextRulePinned => Loc.Get("UpNextRulePinned");
    /// <summary>Up next's order, rule 5, and its reason line.</summary>
    public static string UpNextRuleClosest => Loc.Get("UpNextRuleClosest");
    /// <summary>Up next's order, rule 6.</summary>
    public static string UpNextRuleGate => Loc.Get("UpNextRuleGate");
    /// <summary>The reason line's hover title.</summary>
    public static string UpNextWhyTitle => Loc.Get("UpNextWhyTitle");
    /// <summary>The reason line's hover, above the order.</summary>
    public static string UpNextWhyLead => Loc.Get("UpNextWhyLead");
    /// <summary>Up next's place line for a quest in the journal. {0} = step number; {1} = the game's objective text.</summary>
    public static string UpNextStepFormat => Loc.Get("UpNextStepFormat");
    /// <summary>Up next's place line. {0} = the giver, through the spoiler shield.</summary>
    public static string UpNextTalkFormat => Loc.Get("UpNextTalkFormat");
    /// <summary>Up next's quiet button: selects the quest.</summary>
    public static string UpNextDetails => Loc.Get("UpNextDetails");
    public static string UpNextDetailsTooltip => Loc.Get("UpNextDetailsTooltip");
    /// <summary>Up next with nothing to pick. {0} = job abbreviation.</summary>
    public static string UpNextNothingFormat => Loc.Get("UpNextNothingFormat");
    /// <summary>{0} = job abbreviation.</summary>
    public static string UpNextNothingOtherOneFormat => Loc.Get("UpNextNothingOtherOneFormat");
    /// <summary>{0} = job abbreviation; {1} = how many quests.</summary>
    public static string UpNextNothingOtherFormat => Loc.Get("UpNextNothingOtherFormat");
    public static string UpNextNothingTooltip => Loc.Get("UpNextNothingTooltip");
    /// <summary>Tonight's main scenario line when Up next already names the quest. {0} = quests left; {1}–{2} = level span.</summary>
    public static string UpNextMsqCatchUpFormat => Loc.Get("UpNextMsqCatchUpFormat");
    /// <summary>{0} = its level.</summary>
    public static string UpNextMsqCatchUpOneFormat => Loc.Get("UpNextMsqCatchUpOneFormat");
    /// <summary>The detail pane's way back to Tonight (1.21 P1).</summary>
    public static string UpNextBackToTonight => Loc.Get("UpNextBackToTonight");
    public static string UpNextBackToTonightTooltip => Loc.Get("UpNextBackToTonightTooltip");
    /// <summary>The Characters view switch's third cell (1.21 P3).</summary>
    public static string RosterView => Loc.Get("RosterView");
    /// <summary>The roster's caption.</summary>
    public static string RosterCountOne => Loc.Get("RosterCountOne");
    /// <summary>The roster's caption. {0} = how many.</summary>
    public static string RosterCountFormat => Loc.Get("RosterCountFormat");
    /// <summary>The roster's caption, after the count.</summary>
    public static string RosterLiveElsewhereOne => Loc.Get("RosterLiveElsewhereOne");
    /// <summary>{0} = how many.</summary>
    public static string RosterLiveElsewhereFormat => Loc.Get("RosterLiveElsewhereFormat");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnCharacter => Loc.Get("RosterColumnCharacter");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnJob => Loc.Get("RosterColumnJob");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnStory => Loc.Get("RosterColumnStory");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnGoal => Loc.Get("RosterColumnGoal");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnReady => Loc.Get("RosterColumnReady");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnToday => Loc.Get("RosterColumnToday");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnMoonlit => Loc.Get("RosterColumnMoonlit");
    /// <summary>Roster column header.</summary>
    public static string RosterColumnLastSeen => Loc.Get("RosterColumnLastSeen");
    /// <summary>The roster header's right-click menu.</summary>
    public static string RosterNarrowColumns => Loc.Get("RosterNarrowColumns");
    /// <summary>A roster name with a nickname. {0} = nickname; {1} = the character's name.</summary>
    public static string RosterNicknameFormat => Loc.Get("RosterNicknameFormat");
    /// <summary>The roster's Job cell. {0} = job abbreviation; {1} = level.</summary>
    public static string RosterJobFormat => Loc.Get("RosterJobFormat");
    /// <summary>The roster's Story cell. {0} = journal part; {1} = that character's next story quest.</summary>
    public static string RosterStoryNextFormat => Loc.Get("RosterStoryNextFormat");
    /// <summary>{0} = main scenario quests left.</summary>
    public static string RosterToLatestFormat => Loc.Get("RosterToLatestFormat");
    /// <summary>{0} = the level the next story quest needs.</summary>
    public static string RosterLevelNeededFormat => Loc.Get("RosterLevelNeededFormat");
    /// <summary>The roster's Story cell, nothing left.</summary>
    public static string RosterCaughtUp => Loc.Get("RosterCaughtUp");
    /// <summary>A character whose save is still being evaluated.</summary>
    public static string RosterBeingRead => Loc.Get("RosterBeingRead");
    /// <summary>A character whose save cannot be read.</summary>
    public static string RosterUnreadable => Loc.Get("RosterUnreadable");
    /// <summary>Allied society allowances left today.</summary>
    public static string RosterAllowanceOne => Loc.Get("RosterAllowanceOne");
    /// <summary>{0} = allied society allowances left today.</summary>
    public static string RosterAllowancesFormat => Loc.Get("RosterAllowancesFormat");
    /// <summary>{0} = leve allowances banked.</summary>
    public static string RosterLevesFormat => Loc.Get("RosterLevesFormat");
    /// <summary>A count the save does not hold.</summary>
    public static string RosterNotRead => Loc.Get("RosterNotRead");
    /// <summary>The roster's Last seen cell: logged in now.</summary>
    public static string RosterLive => Loc.Get("RosterLive");
    public static string RosterThisClient => Loc.Get("RosterThisClient");
    public static string RosterOtherClient => Loc.Get("RosterOtherClient");
    /// <summary>A character read from another launcher folder.</summary>
    public static string RosterOtherFolder => Loc.Get("RosterOtherFolder");
    public static string RosterReadOnlyClientTooltip => Loc.Get("RosterReadOnlyClientTooltip");
    /// <summary>{0} = the folder.</summary>
    public static string RosterReadOnlyFolderTooltipFormat => Loc.Get("RosterReadOnlyFolderTooltipFormat");
    /// <summary>{0} = the character.</summary>
    public static string RosterOtherFolderToastFormat => Loc.Get("RosterOtherFolderToastFormat");
    /// <summary>Roster row menu.</summary>
    public static string RosterMenuStar => Loc.Get("RosterMenuStar");
    /// <summary>Roster row menu.</summary>
    public static string RosterMenuUnstar => Loc.Get("RosterMenuUnstar");
    /// <summary>Roster row menu.</summary>
    public static string RosterMenuRole => Loc.Get("RosterMenuRole");
    /// <summary>Roster row menu.</summary>
    public static string RosterMenuNickname => Loc.Get("RosterMenuNickname");
    public static string RosterRoleTitle => Loc.Get("RosterRoleTitle");
    public static string RosterRoleHint => Loc.Get("RosterRoleHint");
    public static string RosterNicknameTitle => Loc.Get("RosterNicknameTitle");
    public static string RosterNicknameHint => Loc.Get("RosterNicknameHint");
    public static string RosterLabelSave => Loc.Get("RosterLabelSave");
    /// <summary>The line under the detail hero (1.21 P3).</summary>
    public static string OthersLabel => Loc.Get("OthersLabel");
    /// <summary>{0} = how many characters completed it.</summary>
    public static string OthersDoneFormat => Loc.Get("OthersDoneFormat");
    public static string OthersCannotOne => Loc.Get("OthersCannotOne");
    /// <summary>{0} = how many characters.</summary>
    public static string OthersCannotFormat => Loc.Get("OthersCannotFormat");
    /// <summary>{0} = how many characters.</summary>
    public static string OthersReadingFormat => Loc.Get("OthersReadingFormat");
    /// <summary>The line's hover title. {0} = the quest, through the shield.</summary>
    public static string OthersTableTitleFormat => Loc.Get("OthersTableTitleFormat");
    /// <summary>The roster's Goal cell without a goal (1.21 N11).</summary>
    public static string GoalSetLink => Loc.Get("GoalSetLink");
    public static string GoalSetTooltip => Loc.Get("GoalSetTooltip");
    public static string GoalEditTooltip => Loc.Get("GoalEditTooltip");
    /// <summary>Roster row menu.</summary>
    public static string GoalChange => Loc.Get("GoalChange");
    public static string GoalClear => Loc.Get("GoalClear");
    /// <summary>After Goal reached.</summary>
    public static string GoalSetNew => Loc.Get("GoalSetNew");
    public static string GoalReached => Loc.Get("GoalReached");
    public static string GoalBeingRead => Loc.Get("GoalBeingRead");
    /// <summary>A goal this character's save cannot answer (no duty records).</summary>
    public static string GoalUnknown => Loc.Get("GoalUnknown");
    public static string GoalOtherUnread => Loc.Get("GoalOtherUnread");
    public static string GoalAnotherCharacter => Loc.Get("GoalAnotherCharacter");
    /// <summary>{0} = a patch ("7.0").</summary>
    public static string GoalStoryTitleFormat => Loc.Get("GoalStoryTitleFormat");
    /// <summary>{0} = a patch.</summary>
    public static string GoalStoryPhraseFormat => Loc.Get("GoalStoryPhraseFormat");
    /// <summary>{0} = the other character's first name.</summary>
    public static string GoalMatchTitleFormat => Loc.Get("GoalMatchTitleFormat");
    /// <summary>{0} = the other character's first name.</summary>
    public static string GoalMatchPhraseFormat => Loc.Get("GoalMatchPhraseFormat");
    /// <summary>{0} = an expansion.</summary>
    public static string GoalFlyingTitleFormat => Loc.Get("GoalFlyingTitleFormat");
    /// <summary>{0} = an expansion.</summary>
    public static string GoalFlyingPhraseFormat => Loc.Get("GoalFlyingPhraseFormat");
    public static string GoalRoulettesTitle => Loc.Get("GoalRoulettesTitle");
    public static string GoalRoulettesPhrase => Loc.Get("GoalRoulettesPhrase");
    /// <summary>{0} = what is left.</summary>
    public static string GoalLeftFormat => Loc.Get("GoalLeftFormat");
    public static string GoalZonesLeftOne => Loc.Get("GoalZonesLeftOne");
    public static string GoalZonesLeftFormat => Loc.Get("GoalZonesLeftFormat");
    public static string GoalDutiesLeftOne => Loc.Get("GoalDutiesLeftOne");
    public static string GoalDutiesLeftFormat => Loc.Get("GoalDutiesLeftFormat");
    /// <summary>{0} = how many can be done now.</summary>
    public static string GoalReadyFormat => Loc.Get("GoalReadyFormat");
    /// <summary>{0} = the character's first name.</summary>
    public static string GoalPopoverTitleFormat => Loc.Get("GoalPopoverTitleFormat");
    public static string GoalPopoverSubtitle => Loc.Get("GoalPopoverSubtitle");
    public static string GoalKindStory => Loc.Get("GoalKindStory");
    public static string GoalKindMatch => Loc.Get("GoalKindMatch");
    public static string GoalKindFlying => Loc.Get("GoalKindFlying");
    public static string GoalKindRoulettes => Loc.Get("GoalKindRoulettes");
    public static string GoalNoPatches => Loc.Get("GoalNoPatches");
    public static string GoalNoOthers => Loc.Get("GoalNoOthers");
    public static string GoalNoFlying => Loc.Get("GoalNoFlying");
    public static string GoalPreviewNone => Loc.Get("GoalPreviewNone");
    /// <summary>{0} = quests left; {1} = the patch.</summary>
    public static string GoalPreviewStoryFormat => Loc.Get("GoalPreviewStoryFormat");
    /// <summary>{0} = how many; {1} = the other character; {2} = this one.</summary>
    public static string GoalPreviewMatchFormat => Loc.Get("GoalPreviewMatchFormat");
    /// <summary>{0} = zones; {1} = the expansion.</summary>
    public static string GoalPreviewFlyingFormat => Loc.Get("GoalPreviewFlyingFormat");
    /// <summary>{0} = duties.</summary>
    public static string GoalPreviewRoulettesFormat => Loc.Get("GoalPreviewRoulettesFormat");
    /// <summary>{0} = how many quests. Follows the preview's first part.</summary>
    public static string GoalPreviewNowFormat => Loc.Get("GoalPreviewNowFormat");
    /// <summary>{0} = how many; {1} = the character.</summary>
    public static string GoalPreviewWaitFormat => Loc.Get("GoalPreviewWaitFormat");
    /// <summary>{0} = the character.</summary>
    public static string GoalPreviewReachedFormat => Loc.Get("GoalPreviewReachedFormat");
    /// <summary>{0} = the character.</summary>
    public static string GoalPreviewNoDutiesFormat => Loc.Get("GoalPreviewNoDutiesFormat");
    /// <summary>{0} = the other character.</summary>
    public static string GoalPreviewOtherUnreadFormat => Loc.Get("GoalPreviewOtherUnreadFormat");
    public static string GoalSetButton => Loc.Get("GoalSetButton");
    /// <summary>Undo toast. {0} = the character.</summary>
    public static string GoalSetToastFormat => Loc.Get("GoalSetToastFormat");
    /// <summary>Undo toast. {0} = the character.</summary>
    public static string GoalClearedToastFormat => Loc.Get("GoalClearedToastFormat");
    /// <summary>The goal card's title. {0} = the goal as a phrase.</summary>
    public static string GoalCardTitleFormat => Loc.Get("GoalCardTitleFormat");
    /// <summary>{0} = the character.</summary>
    public static string GoalWaitsForStoryFormat => Loc.Get("GoalWaitsForStoryFormat");
    /// <summary>{0} = rows not shown.</summary>
    public static string GoalMoreFormat => Loc.Get("GoalMoreFormat");
    public static string GoalFewer => Loc.Get("GoalFewer");
    /// <summary>{0} = quests that can be done now.</summary>
    public static string GoalSendFormat => Loc.Get("GoalSendFormat");
    public static string GoalSendNeedsFull => Loc.Get("GoalSendNeedsFull");
    /// <summary>{0} = the character.</summary>
    public static string GoalReadOnlyClientFormat => Loc.Get("GoalReadOnlyClientFormat");
    /// <summary>{0} = the character.</summary>
    public static string GoalReadOnlyStoredFormat => Loc.Get("GoalReadOnlyStoredFormat");
    public static string GoalRoute => Loc.Get("GoalRoute");
    /// <summary>Settings › Data › Characters (1.21 P3).</summary>
    public static string LinkedFoldersLabel => Loc.Get("LinkedFoldersLabel");
    public static string LinkedFoldersHint => Loc.Get("LinkedFoldersHint");
    public static string LinkedFoldersPathHint => Loc.Get("LinkedFoldersPathHint");
    public static string LinkedFoldersAdd => Loc.Get("LinkedFoldersAdd");
    public static string LinkedFoldersRemove => Loc.Get("LinkedFoldersRemove");
    public static string LinkedFoldersDetect => Loc.Get("LinkedFoldersDetect");
    public static string LinkedFoldersNotFound => Loc.Get("LinkedFoldersNotFound");
    public static string LinkedFoldersNoneFound => Loc.Get("LinkedFoldersNoneFound");
    public static string LinkedFoldersReadOne => Loc.Get("LinkedFoldersReadOne");
    /// <summary>{0} = how many.</summary>
    public static string LinkedFoldersReadFormat => Loc.Get("LinkedFoldersReadFormat");
}
