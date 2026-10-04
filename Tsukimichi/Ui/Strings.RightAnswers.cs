using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.19.0 "Right answers" (feature plan v7): the game's own offers (C1, <c>GameOffer</c> prefixes),
/// which job gets a quest's EXP and Switch gearset (C8, <c>ExpAdvice</c> and <c>GearsetSwitch</c>), and the journal's
/// room with Make room (C9, <c>Journal</c>).
/// </summary>
static partial class Strings
{
    // ---- C1: the game confirms it ----
    /// <summary>{0} = the date the game first offered it.</summary>
    public static string GameOfferedFormat => Loc.Get("GameOfferedFormat");
    public static string GameOfferedTooltip => Loc.Get("GameOfferedTooltip");
    public static string GameOfferedOverrideTooltip => Loc.Get("GameOfferedOverrideTooltip");
    public static string SeenInGameChip => Loc.Get("SeenInGameChip");
    /// <summary>{0} = date.</summary>
    public static string SeenInGameTooltipFormat => Loc.Get("SeenInGameTooltipFormat");
    /// <summary>{0} = date.</summary>
    public static string SeenInGameOverrideTooltipFormat => Loc.Get("SeenInGameOverrideTooltipFormat");
    public static string GameDisagreesChip => Loc.Get("GameDisagreesChip");
    /// <summary>{0} = the quest Tsukimichi expects first.</summary>
    public static string GameDisagreesTooltipFormat => Loc.Get("GameDisagreesTooltipFormat");
    /// <summary>{0} = Tsukimichi's state, {1} = its reason.</summary>
    public static string GameDisagreesReasonTooltipFormat => Loc.Get("GameDisagreesReasonTooltipFormat");
    public static string GameCardTitle => Loc.Get("GameCardTitle");
    /// <summary>{0} = the quest, {1} = date, {2} = Tsukimichi's state, {3} = the quest it expects first.</summary>
    public static string GameCardWhyFormat => Loc.Get("GameCardWhyFormat");
    /// <summary>{0} = the quest, {1} = date, {2} = Tsukimichi's state, {3} = its reason.</summary>
    public static string GameCardWhyReasonFormat => Loc.Get("GameCardWhyReasonFormat");
    public static string GameCardContext => Loc.Get("GameCardContext");
    public static string GoWithGame => Loc.Get("GoWithGame");
    public static string GoWithGameTooltip => Loc.Get("GoWithGameTooltip");
    public static string GameCardHint => Loc.Get("GameCardHint");
    public static string GameCardCopyReport => Loc.Get("GameCardCopyReport");
    public static string GameCardCopied => Loc.Get("GameCardCopied");
    public static string GameCardCopyReportTooltip => Loc.Get("GameCardCopyReportTooltip");
    /// <summary>{0} = the quest.</summary>
    public static string GoWithGameToastFormat => Loc.Get("GoWithGameToastFormat");
    public static string UseOwnAnswer => Loc.Get("UseOwnAnswer");
    public static string UseOwnAnswerTooltip => Loc.Get("UseOwnAnswerTooltip");
    /// <summary>{0} = the quest.</summary>
    public static string UseOwnAnswerToastFormat => Loc.Get("UseOwnAnswerToastFormat");
    public static string GameOffersNotLive => Loc.Get("GameOffersNotLive");
    public static string GameOffersNone => Loc.Get("GameOffersNone");
    /// <summary>{0} = disagreements, {1} = confirmed, {2} = Ready quests the game did not show.</summary>
    public static string GameOffersCountFormat => Loc.Get("GameOffersCountFormat");
    public static string GameOffersTooltip => Loc.Get("GameOffersTooltip");
    public static string GameOffersCopy => Loc.Get("GameOffersCopy");
    public static string GameOffersCopyTooltip => Loc.Get("GameOffersCopyTooltip");

    // ---- C8: EXP to the right job ----
    public static string ExpAdviceTooltip => Loc.Get("ExpAdviceTooltip");
    /// <summary>{0} = job abbreviation, {1} = its level, {2} = the EXP it gets.</summary>
    public static string ExpLineFormat => Loc.Get("ExpLineFormat");
    /// <summary>The job's level unknown. {0} = job abbreviation, {1} = the EXP it gets.</summary>
    public static string ExpLineNoLevelFormat => Loc.Get("ExpLineNoLevelFormat");
    /// <summary>{0} = whole percent of the EXP the level needs.</summary>
    public static string ExpShareFormat => Loc.Get("ExpShareFormat");
    public static string ExpShareUnderOne => Loc.Get("ExpShareUnderOne");
    /// <summary>{0} = a job at the level cap (abbreviation).</summary>
    public static string ExpCappedClauseFormat => Loc.Get("ExpCappedClauseFormat");
    /// <summary>{0} = the current job, {1} = its level, {2} = the EXP it gets.</summary>
    public static string ExpLessClauseFormat => Loc.Get("ExpLessClauseFormat");
    /// <summary>{0} = the current job.</summary>
    public static string ExpAdviceAllCappedFormat => Loc.Get("ExpAdviceAllCappedFormat");
    public static string ExpAdviceNeedsJobNone => Loc.Get("ExpAdviceNeedsJobNone");
    /// <summary>{0} = the job's full name (Culinarian).</summary>
    public static string GearsetSwitchJobFormat => Loc.Get("GearsetSwitchJobFormat");
    /// <summary>{0} = the job's full name (Culinarian).</summary>
    public static string GearsetNoneFormat => Loc.Get("GearsetNoneFormat");
    /// <summary>The unmet Job requirement. {0} = the job required, {1} = the job you are on (full names).</summary>
    public static string JobRequirementDetailFormat => Loc.Get("JobRequirementDetailFormat");
    /// <summary>{0} = gearset number, {1} = its name, {2} = its item level.</summary>
    public static string GearsetSwitchTooltipFormat => Loc.Get("GearsetSwitchTooltipFormat");
    /// <summary>{0} = why not now ("you are in combat").</summary>
    public static string GearsetSwitchBlockedFormat => Loc.Get("GearsetSwitchBlockedFormat");
    public static string GearsetSwitchPaused => Loc.Get("GearsetSwitchPaused");
    public static string GearsetSwitchLoggedOut => Loc.Get("GearsetSwitchLoggedOut");

    // ---- C9: journal slots and Make room ----
    public static string JournalRoomTooltip => Loc.Get("JournalRoomTooltip");
    /// <summary>{0} = slots used, {1} = slots the journal has.</summary>
    public static string JournalBarFormat => Loc.Get("JournalBarFormat");
    /// <summary>{0} = slots used, {1} = slots the journal has.</summary>
    public static string JournalBarFullFormat => Loc.Get("JournalBarFullFormat");
    public static string JournalBarLeftOne => Loc.Get("JournalBarLeftOne");
    /// <summary>{0} = free slots.</summary>
    public static string JournalBarLeftFormat => Loc.Get("JournalBarLeftFormat");
    public static string JournalBarFullTooltip => Loc.Get("JournalBarFullTooltip");
    public static string JournalFullWords => Loc.Get("JournalFullWords");
    public static string JournalFullRowTooltip => Loc.Get("JournalFullRowTooltip");
    public static string MakeRoom => Loc.Get("MakeRoom");
    public static string MakeRoomTooltip => Loc.Get("MakeRoomTooltip");
    public static string MakeRoomToAccept => Loc.Get("MakeRoomToAccept");
    /// <summary>{0} = slots used, {1} = slots the journal has.</summary>
    public static string MakeRoomSubtitleFormat => Loc.Get("MakeRoomSubtitleFormat");
    public static string MakeRoomFinishNow => Loc.Get("MakeRoomFinishNow");
    public static string MakeRoomFinishNowSub => Loc.Get("MakeRoomFinishNowSub");
    public static string MakeRoomNeedsDuty => Loc.Get("MakeRoomNeedsDuty");
    public static string MakeRoomNeedsDutySub => Loc.Get("MakeRoomNeedsDutySub");
    public static string MakeRoomNeedsItem => Loc.Get("MakeRoomNeedsItem");
    public static string MakeRoomNeedsItemSub => Loc.Get("MakeRoomNeedsItemSub");
    public static string MakeRoomNeedsGroup => Loc.Get("MakeRoomNeedsGroup");
    public static string MakeRoomNeedsGroupSub => Loc.Get("MakeRoomNeedsGroupSub");
    public static string MakeRoomSafeToDrop => Loc.Get("MakeRoomSafeToDrop");
    public static string MakeRoomSafeToDropSub => Loc.Get("MakeRoomSafeToDropSub");
    public static string MakeRoomTalk => Loc.Get("MakeRoomTalk");
    public static string MakeRoomDelivery => Loc.Get("MakeRoomDelivery");
    /// <summary>{0} = the duty.</summary>
    public static string MakeRoomSoloDutyFormat => Loc.Get("MakeRoomSoloDutyFormat");
    /// <summary>{0} = the duty.</summary>
    public static string MakeRoomNpcDutyFormat => Loc.Get("MakeRoomNpcDutyFormat");
    /// <summary>{0} = the duty, {1} = players it seats.</summary>
    public static string MakeRoomGroupFormat => Loc.Get("MakeRoomGroupFormat");
    /// <summary>{0} = the duty.</summary>
    public static string MakeRoomGroupUnknownFormat => Loc.Get("MakeRoomGroupUnknownFormat");
    /// <summary>{0} = the item, {1} = how many count.</summary>
    public static string MakeRoomItemFormat => Loc.Get("MakeRoomItemFormat");
    /// <summary>{0} = where the giver stands.</summary>
    public static string MakeRoomStepOneFormat => Loc.Get("MakeRoomStepOneFormat");
    public static string MakeRoomStepOne => Loc.Get("MakeRoomStepOne");
    public static string MakeRoomSafeChip => Loc.Get("MakeRoomSafeChip");
    /// <summary>{0} = the giver.</summary>
    public static string MakeRoomSafeChipTooltipFormat => Loc.Get("MakeRoomSafeChipTooltipFormat");
    public static string MakeRoomOpenInJournal => Loc.Get("MakeRoomOpenInJournal");
    public static string MakeRoomOpenInJournalTooltip => Loc.Get("MakeRoomOpenInJournalTooltip");
    public static string MakeRoomOpenInJournalStored => Loc.Get("MakeRoomOpenInJournalStored");
    public static string MakeRoomFooter => Loc.Get("MakeRoomFooter");
    public static string MakeRoomNothing => Loc.Get("MakeRoomNothing");
    public static string TodoJournalCount => Loc.Get("TodoJournalCount");
    public static string TodoJournalCountTooltip => Loc.Get("TodoJournalCountTooltip");

    // ---- C3: gates Tsukimichi can't check ----
    public static string GateCantCheck => Loc.Get("GateCantCheck");
    public static string GateCantCheckTooltip => Loc.Get("GateCantCheckTooltip");
    public static string GateYouSaidSo => Loc.Get("GateYouSaidSo");
    /// <summary>{0} = where the gate is stated ("the wiki"), {1} = how many sources confirm it.</summary>
    public static string GateSourceFormat => Loc.Get("GateSourceFormat");
    public static string GateSourceWiki => Loc.Get("GateSourceWiki");
    public static string GateSourceGameText => Loc.Get("GateSourceGameText");
    public static string GateSourceSheets => Loc.Get("GateSourceSheets");
    public static string GateMarkDone => Loc.Get("GateMarkDone");
    public static string GateMarkDoneTooltip => Loc.Get("GateMarkDoneTooltip");
    /// <summary>{0} = the gate.</summary>
    public static string GateMarkedToastFormat => Loc.Get("GateMarkedToastFormat");
    public static string GateTakeBack => Loc.Get("GateTakeBack");
    public static string GateTakeBackTooltip => Loc.Get("GateTakeBackTooltip");
    public static string GateTakeBackMenu => Loc.Get("GateTakeBackMenu");
    /// <summary>{0} = the gate.</summary>
    public static string GateTakenBackToastFormat => Loc.Get("GateTakenBackToastFormat");
    public static string GateWhereToStart => Loc.Get("GateWhereToStart");
    /// <summary>{0} = the quest to start with (through the spoiler shield).</summary>
    public static string GateWhereToStartTooltipFormat => Loc.Get("GateWhereToStartTooltipFormat");
    /// <summary>{0} = gates that can't be checked.</summary>
    public static string DetailRequirementsCantCheckFormat => Loc.Get("DetailRequirementsCantCheckFormat");
    /// <summary>{0} = unmet requirements, {1} = gates that can't be checked.</summary>
    public static string DetailRequirementsUnmetCantCheckFormat => Loc.Get("DetailRequirementsUnmetCantCheckFormat");
    public static string HeroCantCheckOne => Loc.Get("HeroCantCheckOne");
    /// <summary>{0} = gates that can't be checked (two or more).</summary>
    public static string HeroCantCheckFormat => Loc.Get("HeroCantCheckFormat");
}
