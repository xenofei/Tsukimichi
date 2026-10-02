using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Questionable cross-check and hand-off (V2-17): the line under the detail pane's status, the
/// "…" menu's hand-off and its setting in Settings › Integrations. Constant names carry the <c>Questionable</c> prefix
/// so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>Both say the quest can be picked up, or both say it cannot.</summary>
    public static string QuestionableAgrees => Loc.Get("QuestionableAgrees");

    /// <summary>Agreement on a quest Tsukimichi holds back only for level or job, which Questionable's lock does not check.</summary>
    public static string QuestionableAgreesLevelAside => Loc.Get("QuestionableAgreesLevelAside");

    /// <summary>Questionable's answer differs; {0} is its reason ("Prev quest (1), Aetheryte locked: Ul'dah") or "not locked".</summary>
    public static string QuestionableSaysFormat => Loc.Get("QuestionableSaysFormat");

    /// <summary>What Questionable says when it would start a quest Tsukimichi has blocked.</summary>
    public static string QuestionableNotLocked => Loc.Get("QuestionableNotLocked");

    public static string QuestionableLineTooltip => Loc.Get("QuestionableLineTooltip");

    /// <summary>The round "…" button at the end of the action bar and its tooltip.</summary>
    public static string QuestionableMoreTooltip => Loc.Get("QuestionableMoreTooltip");

    public static string QuestionableAddToPriority => Loc.Get("QuestionableAddToPriority");

    public static string QuestionableAddToPriorityTooltip => Loc.Get("QuestionableAddToPriorityTooltip");

    public static string QuestionableAddToPriorityNoPath => Loc.Get("QuestionableAddToPriorityNoPath");

    /// <summary>Shown for a few seconds in place of the Questionable line after the hand-off.</summary>
    public static string QuestionableAdded => Loc.Get("QuestionableAdded");

    public static string QuestionableAddFailed => Loc.Get("QuestionableAddFailed");

    public static string ConfigQuestionableHandoff => Loc.Get("ConfigQuestionableHandoff");

    public static string ConfigQuestionableHandoffHint => Loc.Get("ConfigQuestionableHandoffHint");

    // ---- 1.6.0: Send to Questionable, start and stop, badges, live status (feature plan v5, decision 1) ----

    public static string QuestionableSendButton => Loc.Get("QuestionableSendButton");

    public static string QuestionableSendTooltip => Loc.Get("QuestionableSendTooltip");

    public static string QuestionableSendExpansionTooltip => Loc.Get("QuestionableSendExpansionTooltip");

    public static string QuestionableNeedsPlugin => Loc.Get("QuestionableNeedsPlugin");

    public static string QuestionableSendAppendFormat => Loc.Get("QuestionableSendAppendFormat");

    public static string QuestionableSendAppendTooltip => Loc.Get("QuestionableSendAppendTooltip");

    public static string QuestionableSkippedFormat => Loc.Get("QuestionableSkippedFormat");

    public static string QuestionableNothingToSend => Loc.Get("QuestionableNothingToSend");

    public static string QuestionableSendStartFormat => Loc.Get("QuestionableSendStartFormat");

    public static string QuestionableSendStartTooltip => Loc.Get("QuestionableSendStartTooltip");

    public static string QuestionableStartOffSetting => Loc.Get("QuestionableStartOffSetting");

    public static string QuestionableStartNoGate => Loc.Get("QuestionableStartNoGate");

    public static string QuestionableStartMissingFormat => Loc.Get("QuestionableStartMissingFormat");

    public static string QuestionableAlreadyRunning => Loc.Get("QuestionableAlreadyRunning");

    public static string QuestionableReplace => Loc.Get("QuestionableReplace");

    public static string QuestionableReplaceTooltip => Loc.Get("QuestionableReplaceTooltip");

    public static string QuestionableReplaceNoGate => Loc.Get("QuestionableReplaceNoGate");

    public static string QuestionableReplacePopup => Loc.Get("QuestionableReplacePopup");

    public static string QuestionableReplaceQuestionFormat => Loc.Get("QuestionableReplaceQuestionFormat");

    public static string QuestionableReplaceQuestionUnknownFormat => Loc.Get("QuestionableReplaceQuestionUnknownFormat");

    public static string QuestionableReplaceConfirm => Loc.Get("QuestionableReplaceConfirm");

    public static string QuestionableCancel => Loc.Get("QuestionableCancel");

    public static string QuestionableStartPopup => Loc.Get("QuestionableStartPopup");

    public static string QuestionableStartQuestionFormat => Loc.Get("QuestionableStartQuestionFormat");

    public static string QuestionableStartConfirm => Loc.Get("QuestionableStartConfirm");

    public static string QuestionableStartDontAsk => Loc.Get("QuestionableStartDontAsk");

    public static string QuestionableStop => Loc.Get("QuestionableStop");

    public static string QuestionableStopShort => Loc.Get("QuestionableStopShort");

    public static string QuestionableStopTooltip => Loc.Get("QuestionableStopTooltip");

    public static string QuestionableStopNoGate => Loc.Get("QuestionableStopNoGate");

    public static string QuestionableSentFormat => Loc.Get("QuestionableSentFormat");

    public static string QuestionableSentAllFormat => Loc.Get("QuestionableSentAllFormat");

    public static string QuestionableSentNoneFormat => Loc.Get("QuestionableSentNoneFormat");

    public static string QuestionableSentUnverifiedFormat => Loc.Get("QuestionableSentUnverifiedFormat");

    public static string QuestionableAlreadyOnFormat => Loc.Get("QuestionableAlreadyOnFormat");

    public static string QuestionableSendFailed => Loc.Get("QuestionableSendFailed");

    public static string QuestionableStartedFormat => Loc.Get("QuestionableStartedFormat");

    public static string QuestionableStartFailed => Loc.Get("QuestionableStartFailed");

    public static string QuestionableStopped => Loc.Get("QuestionableStopped");

    public static string QuestionableStopFailed => Loc.Get("QuestionableStopFailed");

    public static string QuestionableStatusRunning => Loc.Get("QuestionableStatusRunning");

    public static string QuestionableStatusQuestFormat => Loc.Get("QuestionableStatusQuestFormat");

    public static string QuestionableStatusStepFormat => Loc.Get("QuestionableStatusStepFormat");

    public static string QuestionableStatusPickUp => Loc.Get("QuestionableStatusPickUp");

    public static string QuestionableStatusTooltip => Loc.Get("QuestionableStatusTooltip");

    public static string QuestionableOnListFormat => Loc.Get("QuestionableOnListFormat");

    public static string QuestionableHasPath => Loc.Get("QuestionableHasPath");

    public static string QuestionableNoPath => Loc.Get("QuestionableNoPath");

    public static string QuestionableBadgesTooltip => Loc.Get("QuestionableBadgesTooltip");

    public static string QuestionableMarkListFormat => Loc.Get("QuestionableMarkListFormat");

    public static string QuestionableMarkNoPath => Loc.Get("QuestionableMarkNoPath");

    public static string QuestionableMarkListTooltipFormat => Loc.Get("QuestionableMarkListTooltipFormat");

    public static string QuestionableMarkNoPathTooltip => Loc.Get("QuestionableMarkNoPathTooltip");

    public static string QuestionableSaysUnobtainable => Loc.Get("QuestionableSaysUnobtainable");

    public static string QuestionableSaysObtainable => Loc.Get("QuestionableSaysObtainable");

    public static string QuestionableSaysEventRunning => Loc.Get("QuestionableSaysEventRunning");

    public static string QuestionableSendPins => Loc.Get("QuestionableSendPins");

    public static string ConfigQuestionableSection => Loc.Get("ConfigQuestionableSection");

    public static string ConfigQuestionableAllowStart => Loc.Get("ConfigQuestionableAllowStart");

    public static string ConfigQuestionableAllowStartHint => Loc.Get("ConfigQuestionableAllowStartHint");

    public static string ConfigQuestionableConfirmStart => Loc.Get("ConfigQuestionableConfirmStart");

    public static string ConfigQuestionableConfirmStartHint => Loc.Get("ConfigQuestionableConfirmStartHint");

    public static string ConfigQuestionableStatusFormat => Loc.Get("ConfigQuestionableStatusFormat");

    public static string ConfigQuestionableStatusReadyFormat => Loc.Get("ConfigQuestionableStatusReadyFormat");

    public static string ConfigQuestionableStatusAbsent => Loc.Get("ConfigQuestionableStatusAbsent");
}
