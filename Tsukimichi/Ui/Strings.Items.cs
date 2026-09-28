namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the item hover hint and the item context-menu entry (V2-14). Constants here are prefixed
/// <c>Items</c> so this half of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Hover hint ----
    public const string ItemsHintWindowId = "##TsukimichiItemHint";
    public const string ItemsHintTitle = "Tsukimichi";

    /// <summary>{0} = quest name.</summary>
    public const string ItemsQuestRewardFormat = "Quest reward: {0}";

    /// <summary>Status shown in Moon after a completed quest's name.</summary>
    public const string ItemsDone = "done";

    public const string ItemsOwned = "owned";
    public const string ItemsNotOwned = "not owned";
    public const string ItemsVeiled = "veiled";

    /// <summary>{0} = number of further quests the hint does not list.</summary>
    public const string ItemsMoreFormat = "and {0} more";

    // ---- Context menu ----
    /// <summary>{0} = quest name.</summary>
    public const string ItemsMenuSingleFormat = "Tsukimichi: quest reward ({0})";

    /// <summary>{0} = number of quests; the entry opens a submenu with one line per quest.</summary>
    public const string ItemsMenuManyFormat = "Tsukimichi: quest rewards ({0})";
}
