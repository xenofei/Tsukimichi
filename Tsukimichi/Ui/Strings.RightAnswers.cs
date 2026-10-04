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
    public static string GameOfferTooltip => Loc.Get("GameOfferTooltip");
    public static string GameOfferDisagreesTooltip => Loc.Get("GameOfferDisagreesTooltip");
    public static string GameOffersNotLive => Loc.Get("GameOffersNotLive");
    public static string GameOffersNone => Loc.Get("GameOffersNone");
    /// <summary>{0} = disagreements, {1} = confirmed, {2} = Ready quests the game did not show.</summary>
    public static string GameOffersCountFormat => Loc.Get("GameOffersCountFormat");
    public static string GameOffersTooltip => Loc.Get("GameOffersTooltip");
    public static string GameOffersCopy => Loc.Get("GameOffersCopy");
    public static string GameOffersCopyTooltip => Loc.Get("GameOffersCopyTooltip");

    // ---- C8: EXP to the right job ----
    public static string ExpAdviceTooltip => Loc.Get("ExpAdviceTooltip");
    /// <summary>{0} = job, {1} = its level.</summary>
    public static string ExpAdviceGoesToFormat => Loc.Get("ExpAdviceGoesToFormat");
    /// <summary>{0} = job, {1} = its level, {2} = EXP.</summary>
    public static string ExpAdviceGoesToAmountFormat => Loc.Get("ExpAdviceGoesToAmountFormat");
    /// <summary>{0} = the current job, {1} = the better job, {2} = its level, {3} = its EXP.</summary>
    public static string ExpAdviceCappedFormat => Loc.Get("ExpAdviceCappedFormat");
    /// <summary>{0} = the current job.</summary>
    public static string ExpAdviceAllCappedFormat => Loc.Get("ExpAdviceAllCappedFormat");
    /// <summary>{0} = the current job, {1} = its level, {2} = its EXP, {3} = the better job, {4} = its level, {5} = its EXP.</summary>
    public static string ExpAdviceLessFormat => Loc.Get("ExpAdviceLessFormat");
    /// <summary>{0} = the job that takes the quest, {1} = its level, {2} = its EXP.</summary>
    public static string ExpAdviceNeedsJobFormat => Loc.Get("ExpAdviceNeedsJobFormat");
    public static string ExpAdviceNeedsJobNone => Loc.Get("ExpAdviceNeedsJobNone");
    /// <summary>{0} = job abbreviation (or the gearset's name).</summary>
    public static string GearsetSwitchFormat => Loc.Get("GearsetSwitchFormat");
    /// <summary>{0} = gearset number, {1} = its name, {2} = its item level.</summary>
    public static string GearsetSwitchTooltipFormat => Loc.Get("GearsetSwitchTooltipFormat");
    /// <summary>{0} = why not now ("you are in combat").</summary>
    public static string GearsetSwitchBlockedFormat => Loc.Get("GearsetSwitchBlockedFormat");
    public static string GearsetSwitchPaused => Loc.Get("GearsetSwitchPaused");
    public static string GearsetSwitchLoggedOut => Loc.Get("GearsetSwitchLoggedOut");

    // ---- C9: journal slots and Make room ----
    public static string JournalFullReadyLine => Loc.Get("JournalFullReadyLine");
    public static string JournalFullReadyTooltip => Loc.Get("JournalFullReadyTooltip");
    public static string JournalRoomTooltip => Loc.Get("JournalRoomTooltip");
    public static string JournalRoomEmpty => Loc.Get("JournalRoomEmpty");
    public static string JournalRoomNote => Loc.Get("JournalRoomNote");
    public static string JournalRoomColumnAdvice => Loc.Get("JournalRoomColumnAdvice");
    public static string JournalRoomHandIn => Loc.Get("JournalRoomHandIn");
    public static string JournalRoomSafeToDrop => Loc.Get("JournalRoomSafeToDrop");
    public static string JournalRoomKeep => Loc.Get("JournalRoomKeep");

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
