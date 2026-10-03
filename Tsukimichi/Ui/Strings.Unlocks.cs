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

    /// <summary>{0} = next quests past the three shown; the button scrolls to the Path card.</summary>
    public static string UnlocksMoreInPathFormat => Loc.Get("UnlocksMoreInPathFormat");

    /// <summary>The tooltip line of a row the first-visit rule inferred.</summary>
    public static string UnlocksLikely => Loc.Get("UnlocksLikely");

    public static string UnlocksAttuned => Loc.Get("UnlocksAttuned");

    public static string UnlocksDutyUnlocked => Loc.Get("UnlocksDutyUnlocked");

    public static string UnlocksOwned => Loc.Get("UnlocksOwned");

    // ---- Clicks ----
    public static string UnlocksClickShowQuest => Loc.Get("UnlocksClickShowQuest");

    public static string UnlocksClickTeleport => Loc.Get("UnlocksClickTeleport");

    public static string UnlocksClickFlag => Loc.Get("UnlocksClickFlag");

    public static string UnlocksClickMap => Loc.Get("UnlocksClickMap");

    public static string UnlocksRightClickDuty => Loc.Get("UnlocksRightClickDuty");

    // ---- Row menu ----
    public static string UnlocksMenuShowQuest => Loc.Get("UnlocksMenuShowQuest");

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
}
