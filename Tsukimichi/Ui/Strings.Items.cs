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

    /// <summary>Second line under a quest whose reward the FFXIV Online Store also sells; the hint takes no input, so the reason is spelled out.</summary>
    public const string ItemsStoreOnly = "Store only: also sold on the FFXIV Online Store";

    // ---- Context menu ----
    /// <summary>{0} = quest name.</summary>
    public const string ItemsMenuSingleFormat = "Tsukimichi: quest reward ({0})";

    /// <summary>{0} = number of quests; the entry opens a submenu with one line per quest.</summary>
    public const string ItemsMenuManyFormat = "Tsukimichi: quest rewards ({0})";

    // ---- NPC context menu (P2) ----
    /// <summary>{0} = number of quests the targeted NPC hands out (removed quests excluded); clicking opens the Journal on them.</summary>
    public const string NpcMenuFormat = "Tsukimichi: quests here ({0})";

    /// <summary>{0} = NPC name; the scope chip while the Journal shows one NPC's quests.</summary>
    public const string ChipIssuerFormat = "Quests from {0}";

    /// <summary>The chip when the catalog has no name for the NPC (it issues nothing); the chip still clears the scope.</summary>
    public const string ChipIssuerUnknown = "Quests from this NPC";

    /// <summary>Above the "click to clear" line on the issuer chip.</summary>
    public const string ChipIssuerTooltip = "The quests this NPC hands out, each with what blocks it; clearing shows the whole journal again";
}
