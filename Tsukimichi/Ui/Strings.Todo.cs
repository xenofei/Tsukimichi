using Tsukimichi.Core.Todo;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the todo overlay (V2-13) and its settings section. Every constant is prefixed <c>Todo</c> so the
/// partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Overlay window ----
    public const string TodoWindowTitle = "Tsukimichi Todo###TsukimichiTodo";
    public const string TodoHeader = "☾ Tsukimichi";
    public const string TodoEmpty = "Nothing to do here";
    public const string TodoNoSections = "No sections enabled";

    public const string TodoSectionPinned = "Pinned";
    public const string TodoSectionSeasonal = "Event quests running now";
    public const string TodoSectionNearby = "Unlocks you can start here";
    public const string TodoSectionMsq = "Main scenario";
    public const string TodoSectionJobQuests = "Job quests";

    /// <summary>{0} = section name, {1} = row count.</summary>
    public const string TodoSectionFormat = "{0} ({1})";

    public const string TodoRowClickHint = "Click: show in Tsukimichi · double-click: flag the giver on the map · right-click or …: more";
    public const string TodoRowMoreTooltip = "More: reveal, flag, teleport, link in chat";
    public const string TodoHeaderMoreTooltip = "Overlay options: lock, compact, reset position, hide";

    /// <summary>{0} = section name. Tooltip on a section header.</summary>
    public const string TodoSectionToggleFormat = "Click to fold or unfold {0}";
    public const string TodoRevealInTsukimichi = "Reveal in Tsukimichi";

    // Header context menu (right-click on the title).
    public const string TodoMenuLock = "Lock (click-through)";
    public const string TodoMenuLockTooltip = "The overlay stops taking clicks so the game behind it gets them. Unlock it again in Settings › Todo overlay.";
    public const string TodoMenuCompact = "Compact";
    public const string TodoMenuResetPosition = "Reset position";
    public const string TodoMenuHide = "Hide overlay";

    public static string TodoSectionName(TodoSection section) => section switch
    {
        TodoSection.Pinned => TodoSectionPinned,
        TodoSection.Seasonal => TodoSectionSeasonal,
        TodoSection.NearbyFeature => TodoSectionNearby,
        TodoSection.Msq => TodoSectionMsq,
        TodoSection.JobQuests => TodoSectionJobQuests,
        TodoSection.Plan => PlanTodoSection,
        _ => section.ToString(),
    };

    // ---- Settings › Todo overlay ----
    public const string TodoConfigSection = "Todo overlay";
    public const string TodoConfigEnabled = "Show the todo overlay";
    public const string TodoConfigEnabledHint = "A small always-visible panel: your pins, event quests running now, unlock quests you can start here, the next main scenario quest and your job quests. /tsuki todo toggles it.";
    public const string TodoConfigLocked = "Locked (click-through)";
    public const string TodoConfigLockedHint = "The overlay cannot be moved and ignores the mouse: clicks go to the game behind it. Untick this to use its rows and menu again.";
    /// <summary>Settings › Todo overlay, under Locked, for a player who upgraded with the overlay locked (shown until they unlock it).</summary>
    public const string TodoLockUpgradeNotice = "Since 0.8.0, Locked means click-through: your locked overlay lets every click through to the game, so its rows and menu do not respond. Untick Locked above to use them again.";
    /// <summary>The one chat line at login for a player who upgraded with the overlay locked.</summary>
    public const string TodoLockUpgradeChat = "Your todo overlay is locked, and since 0.8.0 Locked means click-through: clicks go to the game behind it. To use its rows and menu again, untick Settings › Todo overlay › Locked (click-through).";
    public const string TodoConfigCompact = "Compact";
    public const string TodoConfigCompactHint = "Moon and name only, one line per row, at a fixed width.";
    public const string TodoConfigOpacity = "Background opacity";
    public const string TodoConfigSectionsLabel = "Sections";
    public const string TodoConfigShowPins = "Pinned quests";
    public const string TodoConfigShowSeasonal = "Event quests running now";
    public const string TodoConfigShowSeasonalHint = "Quests of the seasonal events running now that you can start or have in your journal, with their giver. An end date shows only when the Lodestone announced it (\"Ends Aug 28 (Lodestone)\").";
    public const string TodoConfigShowNearby = "Unlock quests you can start in this zone";
    public const string TodoConfigShowMsq = "Next main scenario quest";
    public const string TodoConfigShowJobQuests = "Job and role quests for the current job";
    public const string TodoConfigResetPosition = "Reset position";
    public const string TodoConfigResetPositionHint = "Move the overlay back to the top left of the screen";
}
