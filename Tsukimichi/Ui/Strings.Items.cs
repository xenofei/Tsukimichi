using Tsukimichi.Localization;

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
    public static string ItemsQuestRewardFormat => Loc.Get("ItemsQuestRewardFormat");

    /// <summary>Status shown in Moon after a completed quest's name.</summary>
    public static string ItemsDone => Loc.Get("ItemsDone");

    public static string ItemsOwned => Loc.Get("ItemsOwned");
    public static string ItemsNotOwned => Loc.Get("ItemsNotOwned");
    public static string ItemsVeiled => Loc.Get("ItemsVeiled");

    /// <summary>{0} = number of further quests the hint does not list.</summary>
    public static string ItemsMoreFormat => Loc.Get("ItemsMoreFormat");

    /// <summary>Second line under a quest whose reward the FFXIV Online Store also sells; the hint takes no input, so the reason is spelled out.</summary>
    public static string ItemsStoreOnly => Loc.Get("ItemsStoreOnly");

    // ---- Context menu ----
    /// <summary>{0} = quest name.</summary>
    public static string ItemsMenuSingleFormat => Loc.Get("ItemsMenuSingleFormat");

    /// <summary>{0} = number of quests; the entry opens a submenu with one line per quest.</summary>
    public static string ItemsMenuManyFormat => Loc.Get("ItemsMenuManyFormat");

    // ---- NPC context menu (P2) ----
    /// <summary>{0} = number of quests the targeted NPC hands out (removed quests excluded); clicking opens the Journal on them.</summary>
    public static string NpcMenuFormat => Loc.Get("NpcMenuFormat");

    /// <summary>{0} = NPC name; the scope chip while the Journal shows one NPC's quests.</summary>
    public static string ChipIssuerFormat => Loc.Get("ChipIssuerFormat");

    /// <summary>The chip when the catalog has no name for the NPC (it issues nothing); the chip still clears the scope.</summary>
    public static string ChipIssuerUnknown => Loc.Get("ChipIssuerUnknown");

    /// <summary>Above the "click to clear" line on the issuer chip.</summary>
    public static string ChipIssuerTooltip => Loc.Get("ChipIssuerTooltip");
}
