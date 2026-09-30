using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the main window's toolbar, chip row and tab rail (T14). Voice per spec §1: calm, precise, short;
/// labels are nouns; no exclamation marks.
/// </summary>
static partial class Strings
{
    // Quick views (the segmented control on the toolbar)
    public static string QuickViewAll => Loc.Get("QuickViewAll");
    public static string QuickViewAllTooltip => Loc.Get("QuickViewAllTooltip");
    public static string QuickViewsPanelTooltip => Loc.Get("QuickViewsPanelTooltip");

    // Filters button and its badge (Core.Query.FilterBadge decides what counts)
    public static string FiltersBadgeOne => Loc.Get("FiltersBadgeOne");
    /// <summary>{0} = engaged filters.</summary>
    public static string FiltersBadgeFormat => Loc.Get("FiltersBadgeFormat");

    // Character chip
    public static string CharacterChipTooltip => Loc.Get("CharacterChipTooltip");

    // Chip row: the tree scope comes first
    /// <summary>{0} = the scope's name, such as "Sidequests › Gridania".</summary>
    public static string ScopeChipFormat => Loc.Get("ScopeChipFormat");
    /// <summary>{0} = parent node, {1} = the selected node.</summary>
    public const string FoldedScopeFormat = "{0} › {1}";
    public static string ScopeUnnamed => Loc.Get("ScopeUnnamed");
    public static string ScopeChipTooltip => Loc.Get("ScopeChipTooltip");

    // Tab rail
    public static string TabJournalTooltip => Loc.Get("TabJournalTooltip");
    public static string TabMoonlitTooltip => Loc.Get("TabMoonlitTooltip");
    public static string TabCharactersTooltip => Loc.Get("TabCharactersTooltip");
    public static string TabFlightTooltip => Loc.Get("TabFlightTooltip");
}
