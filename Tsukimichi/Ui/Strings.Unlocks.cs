using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for what a quest opens (feature plan v6 K1, K2, K4): the detail pane's Unlocks section, its clicks and
/// menu, the table's Opens column and its setting, the chat line and the Moonlit tooltip. Prefixed <c>Unlocks</c> (and
/// <c>ColumnOpens</c>, <c>PlanningConfigOpens</c>) so this half of the partial class never collides with the others.
/// The kind words and group captions are Core's (<c>Core.UnlockTarget.*</c>, <c>Core.UnlockGroup.*</c>).
/// </summary>
static partial class Strings
{
    // ---- Detail pane section ----
    public static string UnlocksSection => Loc.Get("UnlocksSection");

    /// <summary>The section's only line while the spoiler shield masks the quest's name.</summary>
    public static string UnlocksMasked => Loc.Get("UnlocksMasked");

    /// <summary>{0} = rows past a group's cap, listed in a popup.</summary>
    public static string UnlocksMoreFormat => Loc.Get("UnlocksMoreFormat");

    /// <summary>The tooltip line of a row the first-visit rule inferred.</summary>
    public static string UnlocksLikely => Loc.Get("UnlocksLikely");

    public static string UnlocksAttuned => Loc.Get("UnlocksAttuned");

    public static string UnlocksDutyUnlocked => Loc.Get("UnlocksDutyUnlocked");

    public static string UnlocksOwned => Loc.Get("UnlocksOwned");

    // ---- Clicks ----
    public static string UnlocksClickTeleport => Loc.Get("UnlocksClickTeleport");

    public static string UnlocksClickFlag => Loc.Get("UnlocksClickFlag");

    public static string UnlocksClickMap => Loc.Get("UnlocksClickMap");

    public static string UnlocksRightClickDuty => Loc.Get("UnlocksRightClickDuty");

    // ---- Row menu ----
    /// <summary>{0} = the aetheryte's name.</summary>
    public static string UnlocksMenuTeleportFormat => Loc.Get("UnlocksMenuTeleportFormat");

    public static string UnlocksMenuCopyCoordinates => Loc.Get("UnlocksMenuCopyCoordinates");

    public static string UnlocksMenuOpenMap => Loc.Get("UnlocksMenuOpenMap");

    public static string UnlocksMenuDutyFinder => Loc.Get("UnlocksMenuDutyFinder");

    /// <summary>Under "Open in Duty Finder": it selects the duty and never queues (decision 1).</summary>
    public static string UnlocksMenuDutyFinderHint => Loc.Get("UnlocksMenuDutyFinderHint");

    public static string UnlocksMenuCopyName => Loc.Get("UnlocksMenuCopyName");

    // ---- Why Teleport cannot start ----
    public static string UnlocksTeleportNoLifestream => Loc.Get("UnlocksTeleportNoLifestream");

    public static string UnlocksTeleportNotAttuned => Loc.Get("UnlocksTeleportNotAttuned");

    public static string UnlocksTeleportBusy => Loc.Get("UnlocksTeleportBusy");

    // ---- Journal table: the Opens column (off by default) ----
    public static string ColumnOpens => Loc.Get("ColumnOpens");

    public static string ColumnOpensTooltip => Loc.Get("ColumnOpensTooltip");

    public static string PlanningConfigOpensColumn => Loc.Get("PlanningConfigOpensColumn");

    public static string PlanningConfigOpensColumnHint => Loc.Get("PlanningConfigOpensColumnHint");

    // ---- Elsewhere ----

    /// <summary>The chat line after a turn-in: {0} = what the completed quests opened ("Kugane · The Sirensong Sea").</summary>
    public static string UnlocksChatFormat => Loc.Get("UnlocksChatFormat");

    /// <summary>Moonlit's quest tooltip: {0} = what else the quest opens.</summary>
    public static string MoonlitAlsoOpensFormat => Loc.Get("MoonlitAlsoOpensFormat");

    // ---- Find by unlock (plan v7, 1.19.0 K3) ----

    /// <summary>The head of the Unlocks group under the toolbar search.</summary>
    public static string FindUnlocksHeading => Loc.Get("FindUnlocksHeading");

    /// <summary>{0} = the first quest that opens it, through the spoiler shield.</summary>
    public static string FindUnlockViaFormat => Loc.Get("FindUnlockViaFormat");

    /// <summary>The search row's quiet button and the Unlocks row menu's item.</summary>
    public static string RouteToUnlockAction => Loc.Get("RouteToUnlockAction");

    public static string RouteToUnlockTooltip => Loc.Get("RouteToUnlockTooltip");

    /// <summary>The filter drawer's Unlocks group.</summary>
    public static string FilterDrawerUnlocks => Loc.Get("FilterDrawerUnlocks");

    public static string UnlockKindsCountOne => Loc.Get("UnlockKindsCountOne");

    /// <summary>{0} = how many kinds are on.</summary>
    public static string UnlockKindsCountFormat => Loc.Get("UnlockKindsCountFormat");

    /// <summary>Below the Unlocks chips, and their tooltip.</summary>
    public static string UnlockKindsHint => Loc.Get("UnlockKindsHint");

    /// <summary>The chip lane's chip: {0} = the kinds on ("Mount · Flying").</summary>
    public static string UnlockKindsChipFormat => Loc.Get("UnlockKindsChipFormat");

    // ---- Route to unlock ----
    public static string RouteStopsOne => Loc.Get("RouteStopsOne");

    /// <summary>{0} = the quests and field currents left.</summary>
    public static string RouteStopsFormat => Loc.Get("RouteStopsFormat");

    /// <summary>{0} = the aetheryte nearest the field current.</summary>
    public static string RouteFieldCurrentFormat => Loc.Get("RouteFieldCurrentFormat");

    /// <summary>A field current the game's layouts do not place.</summary>
    public static string RouteFieldCurrentUnplaced => Loc.Get("RouteFieldCurrentUnplaced");

    /// <summary>A field current while the game's layouts are still being read for where it stands.</summary>
    public static string RouteFieldCurrentReading => Loc.Get("RouteFieldCurrentReading");

    /// <summary>The mark at a field current's line end.</summary>
    public static string RouteFieldMark => Loc.Get("RouteFieldMark");

    public static string RouteFieldTooltip => Loc.Get("RouteFieldTooltip");

    public static string RouteFieldFlagTooltip => Loc.Get("RouteFieldFlagTooltip");
}
