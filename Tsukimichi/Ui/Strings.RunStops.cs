using Tsukimichi.Core.Companions;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for runs you can trust (plan v7, 1.18.0): the "Why it stopped" card (A2), the "Needs you" panel, chat lines
/// and settings (A5). Keys carry the <c>Stop</c>, <c>NeedsYou</c> or <c>ConfigNeedsYou</c> prefix.
/// </summary>
public static partial class Strings
{
    /// <summary>The card's title for <paramref name="reason"/> (<see cref="RunStopClassifier.TitleKey"/>); a few take the hand-off's name as {0}.</summary>
    public static string StopCardTitle(StopReason reason) => Loc.Get(RunStopClassifier.TitleKey(reason));

    /// <summary>The panel's title for <paramref name="kind"/> (<see cref="NeedsYouAlert.TitleKey"/>).</summary>
    public static string NeedsYouTitle(NeedsYouKind kind) => Loc.Get(NeedsYouAlert.TitleKey(kind));

    /// <summary>Why it stopped card title (1.18, A2): {0} = the hand-off ("Questionable").</summary>
    public static string StopCardTitleFinished => Loc.Get("StopCardTitleFinished");

    /// <summary>Card title: {0} = the hand-off.</summary>
    public static string StopCardTitlePlayer => Loc.Get("StopCardTitlePlayer");

    /// <summary>Card title of the duty guard's stop (A3).</summary>
    public static string StopCardTitleDutyGuard => Loc.Get("StopCardTitleDutyGuard");

    /// <summary>Card title.</summary>
    public static string StopCardTitleKnockedOut => Loc.Get("StopCardTitleKnockedOut");

    /// <summary>Card title.</summary>
    public static string StopCardTitleStuck => Loc.Get("StopCardTitleStuck");

    /// <summary>Card title.</summary>
    public static string StopCardTitleNoPath => Loc.Get("StopCardTitleNoPath");

    /// <summary>Card title: {0} = the plugin ("AutoDuty").</summary>
    public static string StopCardTitleMissingPlugin => Loc.Get("StopCardTitleMissingPlugin");

    /// <summary>Card title: {0} = the hand-off.</summary>
    public static string StopCardTitleError => Loc.Get("StopCardTitleError");

    /// <summary>The hand-off name of a Walk or Go to giver in the card's titles.</summary>
    public static string StopHandOffTravel => Loc.Get("StopHandOffTravel");

    /// <summary>Joins the parts of the card's context line.</summary>
    public static string StopCardSeparator => Loc.Get("StopCardSeparator");

    /// <summary>{0} = the quest.</summary>
    public static string StopWhyPlayerFormat => Loc.Get("StopWhyPlayerFormat");

    /// <summary>Card reason with no quest known.</summary>
    public static string StopWhyPlayer => Loc.Get("StopWhyPlayer");

    /// <summary>{0} = the duty.</summary>
    public static string StopWhyDutyGuardFormat => Loc.Get("StopWhyDutyGuardFormat");

    /// <summary>Card reason with no duty known.</summary>
    public static string StopWhyDutyGuard => Loc.Get("StopWhyDutyGuard");

    /// <summary>Context part of the duty guard card.</summary>
    public static string StopContextGuardSetting => Loc.Get("StopContextGuardSetting");

    /// <summary>Line on the duty guard card while Keep going after it waits: {0} = the duty.</summary>
    public static string StopWaitingDutyFormat => Loc.Get("StopWaitingDutyFormat");

    /// <summary>{0} = the quest, {1} = what Tsukimichi stopped (NeedsYouDid*).</summary>
    public static string StopWhyKnockedOutFormat => Loc.Get("StopWhyKnockedOutFormat");

    /// <summary>{0} = what Tsukimichi stopped (NeedsYouDid*).</summary>
    public static string StopWhyKnockedOut => Loc.Get("StopWhyKnockedOut");

    /// <summary>{0} = the zone.</summary>
    public static string StopWhyStuckFormat => Loc.Get("StopWhyStuckFormat");

    /// <summary>Card reason of a stall Tsukimichi saw (not its own walk).</summary>
    public static string StopWhyStuck => Loc.Get("StopWhyStuck");

    /// <summary>{0} = the target ("Ser Aymeric").</summary>
    public static string StopWhyNoPathFormat => Loc.Get("StopWhyNoPathFormat");

    /// <summary>Card reason with no target known.</summary>
    public static string StopWhyNoPath => Loc.Get("StopWhyNoPath");

    /// <summary>{0} = why (installed but off, or not installed).</summary>
    public static string StopWhyMissingDutyFormat => Loc.Get("StopWhyMissingDutyFormat");

    /// <summary>Card reason of a missing plugin when the registry says nothing more.</summary>
    public static string StopWhyMissing => Loc.Get("StopWhyMissing");

    /// <summary>{0} = the quest.</summary>
    public static string StopWhyErrorFormat => Loc.Get("StopWhyErrorFormat");

    /// <summary>Card reason with no quest known.</summary>
    public static string StopWhyError => Loc.Get("StopWhyError");

    /// <summary>{0} = the travel failure (TravelFail*).</summary>
    public static string StopWhyTravelErrorFormat => Loc.Get("StopWhyTravelErrorFormat");

    /// <summary>{0} = the quest, {1} = Questionable's step, from 1.</summary>
    public static string StopContextStepFormat => Loc.Get("StopContextStepFormat");

    /// <summary>{0} = the target.</summary>
    public static string StopContextTravelFormat => Loc.Get("StopContextTravelFormat");

    /// <summary>The Finished card's receipt line: {0} = how long ("1 h 12 min"), {1} = the quests done.</summary>
    public static string StopReceiptFormat => Loc.Get("StopReceiptFormat");

    /// <summary>The quests done in StopReceiptFormat.</summary>
    public static string StopReceiptOneQuest => Loc.Get("StopReceiptOneQuest");

    /// <summary>When the card was raised.</summary>
    public static string StopAgoNow => Loc.Get("StopAgoNow");

    /// <summary>{0} = minutes.</summary>
    public static string StopAgoMinutesFormat => Loc.Get("StopAgoMinutesFormat");

    /// <summary>{0} = hours.</summary>
    public static string StopAgoHoursFormat => Loc.Get("StopAgoHoursFormat");

    /// <summary>Fix on the Finished card.</summary>
    public static string StopFixStartNext => Loc.Get("StopFixStartNext");

    /// <summary>{0} = the quest.</summary>
    public static string StopFixStartNextTooltipFormat => Loc.Get("StopFixStartNextTooltipFormat");

    /// <summary>Fix on the You stopped it card.</summary>
    public static string StopFixStartAgain => Loc.Get("StopFixStartAgain");

    /// <summary>{0} = the quest.</summary>
    public static string StopFixStartAgainTooltipFormat => Loc.Get("StopFixStartAgainTooltipFormat");

    /// <summary>Fix on the duty guard card.</summary>
    public static string StopFixShowDuty => Loc.Get("StopFixShowDuty");

    /// <summary>Tooltip.</summary>
    public static string StopFixShowDutyTooltip => Loc.Get("StopFixShowDutyTooltip");

    /// <summary>Fix on the duty guard card.</summary>
    public static string StopFixKeepGoing => Loc.Get("StopFixKeepGoing");

    /// <summary>Tooltip.</summary>
    public static string StopFixKeepGoingTooltip => Loc.Get("StopFixKeepGoingTooltip");

    /// <summary>The Keep going after it pill while it waits.</summary>
    public static string StopFixKeepGoingCancel => Loc.Get("StopFixKeepGoingCancel");

    /// <summary>Fix on the knocked out card.</summary>
    public static string StopFixTryAgain => Loc.Get("StopFixTryAgain");

    /// <summary>Tooltip.</summary>
    public static string StopFixTryAgainTooltip => Loc.Get("StopFixTryAgainTooltip");

    /// <summary>Under the disabled Try again.</summary>
    public static string StopTryAgainWaitUp => Loc.Get("StopTryAgainWaitUp");

    /// <summary>Under the disabled Try again.</summary>
    public static string StopTryAgainWaitCombat => Loc.Get("StopTryAgainWaitCombat");

    /// <summary>Fix on the stuck and no-path cards.</summary>
    public static string StopFixReloadRetry => Loc.Get("StopFixReloadRetry");

    /// <summary>Tooltip.</summary>
    public static string StopFixReloadRetryTooltip => Loc.Get("StopFixReloadRetryTooltip");

    /// <summary>Label of the reload pill while it waits.</summary>
    public static string StopFixReloading => Loc.Get("StopFixReloading");

    /// <summary>Why Reload navmesh and retry is unavailable.</summary>
    public static string StopReloadNoVnav => Loc.Get("StopReloadNoVnav");

    /// <summary>Chat line when the retry gave up waiting.</summary>
    public static string StopReloadFailed => Loc.Get("StopReloadFailed");

    /// <summary>Fix on the stuck card.</summary>
    public static string StopFixFlagSpot => Loc.Get("StopFixFlagSpot");

    /// <summary>Tooltip.</summary>
    public static string StopFixFlagSpotTooltip => Loc.Get("StopFixFlagSpotTooltip");

    /// <summary>Fix on the no-path card.</summary>
    public static string StopFixTeleport => Loc.Get("StopFixTeleport");

    /// <summary>Fix on the missing-plugin card.</summary>
    public static string StopFixOpenSetup => Loc.Get("StopFixOpenSetup");

    /// <summary>Tooltip.</summary>
    public static string StopFixOpenSetupTooltip => Loc.Get("StopFixOpenSetupTooltip");

    /// <summary>Fix on the error card: the quest's objective and map are in the game's journal.</summary>
    public static string StopFixOpenJournal => Loc.Get("StopFixOpenJournal");

    /// <summary>Quiet action on every card.</summary>
    public static string StopCopyReport => Loc.Get("StopCopyReport");

    /// <summary>Tooltip.</summary>
    public static string StopCopyReportTooltip => Loc.Get("StopCopyReportTooltip");

    /// <summary>Status-bar note after Copy report.</summary>
    public static string StopReportCopied => Loc.Get("StopReportCopied");

    /// <summary>The card's close button.</summary>
    public static string StopDismissTooltip => Loc.Get("StopDismissTooltip");

    /// <summary>Tooltip of a folded card's status-bar note.</summary>
    public static string StopFoldedTooltip => Loc.Get("StopFoldedTooltip");

    /// <summary>The Needs you panel's eyebrow (1.18, A5); upper-cased at Full.</summary>
    public static string NeedsYouEyebrow => Loc.Get("NeedsYouEyebrow");

    /// <summary>Bold start of every Needs you chat line.</summary>
    public static string NeedsYouLead => Loc.Get("NeedsYouLead");

    /// <summary>Needs you panel title.</summary>
    public static string NeedsYouTitleDeath => Loc.Get("NeedsYouTitleDeath");

    /// <summary>Needs you panel title.</summary>
    public static string NeedsYouTitleStuck => Loc.Get("NeedsYouTitleStuck");

    /// <summary>Needs you panel title.</summary>
    public static string NeedsYouTitleDutyPop => Loc.Get("NeedsYouTitleDutyPop");

    /// <summary>Needs you panel title.</summary>
    public static string NeedsYouTitleTell => Loc.Get("NeedsYouTitleTell");

    /// <summary>What Tsukimichi did: {0} = the hand-offs stopped ("Go to giver, AutoDuty").</summary>
    public static string NeedsYouDidStoppedFormat => Loc.Get("NeedsYouDidStoppedFormat");

    /// <summary>What Tsukimichi did when nothing was stopped.</summary>
    public static string NeedsYouDidNothing => Loc.Get("NeedsYouDidNothing");

    /// <summary>Added when Questionable was left running (stopping it is opt-in).</summary>
    public static string NeedsYouDidQuestionableRuns => Loc.Get("NeedsYouDidQuestionableRuns");

    /// <summary>Chat after "Needs you:": {0} = the quest, {1} = what Tsukimichi did.</summary>
    public static string NeedsYouDeathDuringChatFormat => Loc.Get("NeedsYouDeathDuringChatFormat");

    /// <summary>Chat after "Needs you:": {0} = what Tsukimichi did.</summary>
    public static string NeedsYouDeathChatFormat => Loc.Get("NeedsYouDeathChatFormat");

    /// <summary>Chat after "Needs you:": {0} = seconds, {1} = what Tsukimichi did.</summary>
    public static string NeedsYouStuckChatFormat => Loc.Get("NeedsYouStuckChatFormat");

    /// <summary>Panel line: {0} = seconds, {1} = what Tsukimichi did.</summary>
    public static string NeedsYouStuckLineFormat => Loc.Get("NeedsYouStuckLineFormat");

    /// <summary>Chat after "Needs you:": {0} = the duty.</summary>
    public static string NeedsYouDutyPopChatFormat => Loc.Get("NeedsYouDutyPopChatFormat");

    /// <summary>Chat after "Needs you:" with no duty name.</summary>
    public static string NeedsYouDutyPopChat => Loc.Get("NeedsYouDutyPopChat");

    /// <summary>Panel line: {0} = the duty.</summary>
    public static string NeedsYouDutyPopLineFormat => Loc.Get("NeedsYouDutyPopLineFormat");

    /// <summary>Panel line with no duty name.</summary>
    public static string NeedsYouDutyPopLine => Loc.Get("NeedsYouDutyPopLine");

    /// <summary>Chat after "Needs you:". Never names the sender or repeats the text.</summary>
    public static string NeedsYouTellChat => Loc.Get("NeedsYouTellChat");

    /// <summary>Panel line.</summary>
    public static string NeedsYouTellLine => Loc.Get("NeedsYouTellLine");

    /// <summary>Panel line after a walk's recovery gave up.</summary>
    public static string NeedsYouTravelGaveUpLine => Loc.Get("NeedsYouTravelGaveUpLine");

    /// <summary>Panel button: /tsuki stop.</summary>
    public static string NeedsYouStopAll => Loc.Get("NeedsYouStopAll");

    /// <summary>Tooltip.</summary>
    public static string NeedsYouStopAllTooltip => Loc.Get("NeedsYouStopAllTooltip");

    /// <summary>Panel button.</summary>
    public static string NeedsYouDismiss => Loc.Get("NeedsYouDismiss");

    /// <summary>{0} = alerts waiting behind this one.</summary>
    public static string NeedsYouMoreFormat => Loc.Get("NeedsYouMoreFormat");

    /// <summary>Sub-row under each Needs you kind: the chat sound effect.</summary>
    public static string ConfigNeedsYouSoundRow => Loc.Get("ConfigNeedsYouSoundRow");

    /// <summary>Hint.</summary>
    public static string ConfigNeedsYouSoundRowHint => Loc.Get("ConfigNeedsYouSoundRowHint");

    /// <summary>Sound choice: no sound.</summary>
    public static string ConfigNeedsYouSoundNone => Loc.Get("ConfigNeedsYouSoundNone");

    /// <summary>Plays the chosen sound.</summary>
    public static string ConfigNeedsYouSoundTest => Loc.Get("ConfigNeedsYouSoundTest");

    /// <summary>Toggle.</summary>
    public static string ConfigNeedsYouFlash => Loc.Get("ConfigNeedsYouFlash");

    /// <summary>Hint.</summary>
    public static string ConfigNeedsYouFlashHint => Loc.Get("ConfigNeedsYouFlashHint");

    /// <summary>Toggle.</summary>
    public static string ConfigNeedsYouStopHandOffs => Loc.Get("ConfigNeedsYouStopHandOffs");

    /// <summary>Hint.</summary>
    public static string ConfigNeedsYouStopHandOffsHint => Loc.Get("ConfigNeedsYouStopHandOffsHint");

    /// <summary>Sub-toggle.</summary>
    public static string ConfigNeedsYouStopQuestionable => Loc.Get("ConfigNeedsYouStopQuestionable");

    /// <summary>Hint.</summary>
    public static string ConfigNeedsYouStopQuestionableHint => Loc.Get("ConfigNeedsYouStopQuestionableHint");
}
