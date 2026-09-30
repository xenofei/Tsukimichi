namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the main window's toolbar, chip row and tab rail (T14). Voice per spec §1: calm, precise, short;
/// labels are nouns; no exclamation marks.
/// </summary>
static partial class Strings
{
    // Quick views (the segmented control on the toolbar)
    public const string QuickViewAll = "All";
    public const string QuickViewAllTooltip = "No quick view: the table follows the tree and the filters alone";
    public const string QuickViewsPanelTooltip = "The quick views sit on the toolbar; the Stalled view reads the number of days set here";

    // Filters button and its badge (Core.Query.FilterBadge decides what counts)
    public const string FiltersBadgeOne = "1 filter narrows the table; its chip under the toolbar clears it";
    /// <summary>{0} = engaged filters.</summary>
    public const string FiltersBadgeFormat = "{0} filters narrow the table; each chip under the toolbar clears one";

    // Character chip
    public const string CharacterChipTooltip = "Whose progress is shown. A filled pip is the logged-in character, a hollow one a stored snapshot. Click to switch";

    // Chip row: the tree scope comes first
    /// <summary>{0} = the scope's name, such as "Sidequests › Gridania".</summary>
    public const string ScopeChipFormat = "Scope: {0}";
    /// <summary>{0} = parent node, {1} = the selected node.</summary>
    public const string FoldedScopeFormat = "{0} › {1}";
    public const string ScopeUnnamed = "this node";
    public const string ScopeChipTooltip = "The tree selection narrowing the table; clearing it shows All quests";

    // Tab rail
    public const string TabJournalTooltip = "Every quest in the game, filed as the journal files it";
    public const string TabMoonlitTooltip = "Quest rewards that exist nowhere else, and which you already own";
    public const string TabCharactersTooltip = "Every stored character, with a dashboard of its progress";
    public const string TabFlightTooltip = "The aether current quests of each flying zone";
}
