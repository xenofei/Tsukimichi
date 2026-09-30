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
}
