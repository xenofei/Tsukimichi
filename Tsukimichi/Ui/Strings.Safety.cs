using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>The safety table and the floating Undo (feature plan v6 S1/S2).</summary>
public static partial class Strings
{
    // ---- How a guarded action is confirmed: the last line of its tooltip (Safety.Suffix) ----
    public static string SafetyArmedSuffix => Loc.Get("SafetyArmedSuffix");
    public static string SafetyArmedTwoClickSuffix => Loc.Get("SafetyArmedTwoClickSuffix");
    public static string SafetyHoldSuffix => Loc.Get("SafetyHoldSuffix");
    public static string SafetyHoldTwoClickSuffix => Loc.Get("SafetyHoldTwoClickSuffix");
    public static string SafetyHoldUndoSuffix => Loc.Get("SafetyHoldUndoSuffix");
    public static string SafetyHoldTwoClickUndoSuffix => Loc.Get("SafetyHoldTwoClickUndoSuffix");
    public static string SafetyNoneSuffix => Loc.Get("SafetyNoneSuffix");
    /// <summary>Two-click mode: the label of a guarded item after its first click.</summary>
    public static string SafetyClickAgain => Loc.Get("SafetyClickAgain");
    /// <summary>The tooltip's first line after a plain click on an armed item.</summary>
    public static string SafetyKeyHint => Loc.Get("SafetyKeyHint");

    // ---- The floating Undo ----
    public const string UndoToastSeparator = " · ";
    public static string UndoToastUndo => Loc.Get("UndoToastUndo");
    public static string UndoToastUndoTooltip => Loc.Get("UndoToastUndoTooltip");
    public static string UndoToastAddNote => Loc.Get("UndoToastAddNote");
    public static string UndoToastVerdictRestored => Loc.Get("UndoToastVerdictRestored");
    /// <summary>{0} = how many verdicts Restore all took back.</summary>
    public static string UndoToastVerdictsRestoredFormat => Loc.Get("UndoToastVerdictsRestoredFormat");
    public static string UndoToastNoteSaved => Loc.Get("UndoToastNoteSaved");
    /// <summary>{0} = quest name.</summary>
    public static string UndoToastUnpinnedFormat => Loc.Get("UndoToastUnpinnedFormat");
    /// <summary>{0} = character name.</summary>
    public static string UndoToastHiddenFormat => Loc.Get("UndoToastHiddenFormat");
    /// <summary>{0} = character name.</summary>
    public static string UndoToastShownFormat => Loc.Get("UndoToastShownFormat");
    /// <summary>{0} = character name.</summary>
    public static string UndoToastNotTrackedFormat => Loc.Get("UndoToastNotTrackedFormat");
    /// <summary>{0} = character name.</summary>
    public static string UndoToastTrackedFormat => Loc.Get("UndoToastTrackedFormat");

    // ---- Verdict note ("Add note") ----
    /// <summary>{0} = quest name.</summary>
    public static string VerdictNoteQuestionFormat => Loc.Get("VerdictNoteQuestionFormat");
    public static string VerdictNoteSave => Loc.Get("VerdictNoteSave");
    public static string MoonlitMarkNotUniqueTooltip => Loc.Get("MoonlitMarkNotUniqueTooltip");

    // ---- What the press-and-hold confirms do (the first line of their tooltips) ----
    public static string QuestionableReplaceConfirmTooltip => Loc.Get("QuestionableReplaceConfirmTooltip");
    public static string CompanionSetupApplyConfirmTooltip => Loc.Get("CompanionSetupApplyConfirmTooltip");
    public static string CharactersForgetConfirmTooltip => Loc.Get("CharactersForgetConfirmTooltip");
    public static string AltsForgetBulkConfirmTooltip => Loc.Get("AltsForgetBulkConfirmTooltip");
    public static string ConfigDeleteConfirmTooltip => Loc.Get("ConfigDeleteConfirmTooltip");

    // ---- Settings › Keyboard › Safety ----
    public static string ConfigSafetyHeading => Loc.Get("ConfigSafetyHeading");
    public static string ConfigSafetyRules => Loc.Get("ConfigSafetyRules");
    public static string ConfigSafetyRulesHint => Loc.Get("ConfigSafetyRulesHint");
    public static string ConfigSafetyHold => Loc.Get("ConfigSafetyHold");
    public static string ConfigSafetyHoldHint => Loc.Get("ConfigSafetyHoldHint");
    public static string ConfigSafetyHoldOffHint => Loc.Get("ConfigSafetyHoldOffHint");
    public static string ConfigSafetyTwoClick => Loc.Get("ConfigSafetyTwoClick");
    public static string ConfigSafetyTwoClickHint => Loc.Get("ConfigSafetyTwoClickHint");
}
