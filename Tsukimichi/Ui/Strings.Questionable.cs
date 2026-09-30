namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Questionable cross-check and hand-off (V2-17): the line under the detail pane's status, the
/// "…" menu's hand-off and its setting in Settings › Integrations. Constant names carry the <c>Questionable</c> prefix
/// so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>Both say the quest can be picked up, or both say it cannot.</summary>
    public const string QuestionableAgrees = "Questionable agrees";

    /// <summary>Agreement on a quest Tsukimichi holds back only for level or job, which Questionable's lock does not check.</summary>
    public const string QuestionableAgreesLevelAside = "Questionable agrees (it does not check level or job)";

    /// <summary>Questionable's answer differs; {0} is its reason ("Prev quest (1), Aetheryte locked: Ul'dah") or "not locked".</summary>
    public const string QuestionableSaysFormat = "Questionable says: {0}";

    /// <summary>What Questionable says when it would start a quest Tsukimichi has blocked.</summary>
    public const string QuestionableNotLocked = "not locked";

    public const string QuestionableLineTooltip = "Questionable's own lock check for this quest, beside Tsukimichi's. When they differ, Report this quest records both, which helps find which one is wrong.";

    /// <summary>The round "…" button at the end of the action bar and its tooltip.</summary>
    public const string QuestionableMoreTooltip = "More actions";

    public const string QuestionableAddToPriority = "Add to Questionable priority";

    public const string QuestionableAddToPriorityTooltip = "Put this quest on Questionable's priority list. Nothing starts: Questionable picks it up the next time you run it yourself.";

    public const string QuestionableAddToPriorityNoPath = "Questionable has no path for this quest.";

    /// <summary>Shown for a few seconds in place of the Questionable line after the hand-off.</summary>
    public const string QuestionableAdded = "Added to Questionable's priority list";

    public const string QuestionableAddFailed = "Questionable did not take the quest; see /xllog";

    public const string ConfigQuestionableHandoff = "Show Questionable hand-off";

    public const string ConfigQuestionableHandoffHint = "When Questionable is loaded, the detail pane's \"…\" button offers \"Add to Questionable priority\". It only calls Questionable's own priority list; Tsukimichi never starts Questionable or moves your character. The \"Questionable agrees\" line under a quest shows whether this is on or not.";
}
