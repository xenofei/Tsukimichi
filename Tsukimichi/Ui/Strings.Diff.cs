using System.Globalization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the alt diff (V2-12): the dashboard's "Compare with" section. Every constant is prefixed <c>Diff</c>
/// so the partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard: Compare with ----
    public const string DiffSection = "Compare with";
    public const string DiffNeedsTwo = "Log in on another character to compare.";
    public const string DiffNeedsCatalog = "The comparison appears once the catalog is built.";
    public const string DiffOtherUnreadable = "That character's snapshot could not be read.";
    public const string DiffComboTooltip = "Which stored character the viewed one is compared with; the most recently captured one is chosen at first";
    public const string DiffColumnQuest = "Quest";
    public const string DiffColumnValue = "Value";
    public const string DiffColumnWhy = "Why";
    public const string DiffColumnSection = "Section";
    /// <summary>{0} = character name; header of a per-section count column.</summary>
    public const string DiffOnlyColumnFormat = "Only {0}";
    public const string DiffValueTooltip = "Unlock value: 1 for any quest, +3 main scenario, +5 feature quest, +2 per unique reward. Higher first.";
    /// <summary>{0} = the character that has the quests, {1} = the one that lacks them.</summary>
    public const string DiffOnlyFormat = "Done on {0}, not on {1}";
    public const string DiffNone = "None.";
    /// <summary>{0} = rows beyond the shown cap.</summary>
    public const string DiffMoreFormat = "and {0} more";
    /// <summary>{0} = viewed character, {1} = "N quests", {2} = other character.</summary>
    public const string DiffAheadFormat = "{0} is {1} ahead of {2}";
    /// <summary>{0} = viewed character, {1} = "N quests", {2} = other character.</summary>
    public const string DiffBehindFormat = "{0} is {1} behind {2}";
    /// <summary>{0} = viewed character, {1} = other character.</summary>
    public const string DiffLevelFormat = "{0} and {1} are level";
    /// <summary>{0} = done on both, {1} = done on neither.</summary>
    public const string DiffCountsFormat = "{0} done on both · {1} done on neither";
    public const string DiffCopyList = "Copy list";
    public const string DiffCopyListTooltip = "Copies every quest of this list as \"name (value)\" lines, not only the rows shown";
    /// <summary>{0} = quests copied.</summary>
    public const string DiffCopiedFormat = "Copied {0} to the clipboard";
    /// <summary>{0} = quest name, {1} = value; one line of the copied list.</summary>
    public const string DiffClipboardLineFormat = "{0} ({1})";

    /// <summary>"1 quest" or "N quests".</summary>
    public static string DiffQuestCount(int count) =>
        count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " quest" : " quests");
}
