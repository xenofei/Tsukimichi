using System.Globalization;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the alt diff (V2-12): the dashboard's "Compare with" section. Every constant is prefixed <c>Diff</c>
/// so the partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard: Compare with ----
    public static string DiffSection => Loc.Get("DiffSection");
    public static string DiffNeedsTwo => Loc.Get("DiffNeedsTwo");
    public static string DiffNeedsCatalog => Loc.Get("DiffNeedsCatalog");
    public static string DiffOtherUnreadable => Loc.Get("DiffOtherUnreadable");
    public static string DiffComboTooltip => Loc.Get("DiffComboTooltip");
    public static string DiffColumnQuest => Loc.Get("DiffColumnQuest");
    public static string DiffColumnValue => Loc.Get("DiffColumnValue");
    public static string DiffColumnWhy => Loc.Get("DiffColumnWhy");
    public static string DiffColumnSection => Loc.Get("DiffColumnSection");
    /// <summary>{0} = character name; header of a per-section count column.</summary>
    public static string DiffOnlyColumnFormat => Loc.Get("DiffOnlyColumnFormat");
    public static string DiffValueTooltip => Loc.Get("DiffValueTooltip");
    /// <summary>{0} = the character that has the quests, {1} = the one that lacks them.</summary>
    public static string DiffOnlyFormat => Loc.Get("DiffOnlyFormat");
    public static string DiffNone => Loc.Get("DiffNone");
    /// <summary>{0} = rows beyond the shown cap.</summary>
    public static string DiffMoreFormat => Loc.Get("DiffMoreFormat");
    /// <summary>{0} = viewed character, {1} = "N quests", {2} = other character.</summary>
    public static string DiffAheadFormat => Loc.Get("DiffAheadFormat");
    /// <summary>{0} = viewed character, {1} = "N quests", {2} = other character.</summary>
    public static string DiffBehindFormat => Loc.Get("DiffBehindFormat");
    /// <summary>{0} = viewed character, {1} = other character.</summary>
    public static string DiffLevelFormat => Loc.Get("DiffLevelFormat");
    /// <summary>{0} = done on both, {1} = done on neither.</summary>
    public static string DiffCountsFormat => Loc.Get("DiffCountsFormat");
    public static string DiffCopyList => Loc.Get("DiffCopyList");
    public static string DiffCopyListTooltip => Loc.Get("DiffCopyListTooltip");
    /// <summary>{0} = quests copied.</summary>
    public static string DiffCopiedFormat => Loc.Get("DiffCopiedFormat");
    /// <summary>{0} = quest name, {1} = value; one line of the copied list.</summary>
    public const string DiffClipboardLineFormat = "{0} ({1})";

    /// <summary>"1 quest" or "N quests".</summary>
    public static string DiffQuestCount(int count) =>
        count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " quest" : " quests");
}
