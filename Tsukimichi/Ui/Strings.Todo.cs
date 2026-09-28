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
    public const string TodoLockedTooltip = "Locked: the overlay stays where it is. Unlock it from this menu or in Settings.";
    public const string TodoEmpty = "Nothing to do here";
    public const string TodoNoSections = "No sections enabled";

    public const string TodoSectionPinned = "Pinned";
    public const string TodoSectionNearby = "Feature quests here";
    public const string TodoSectionMsq = "Main scenario";
    public const string TodoSectionJobQuests = "Job quests";

    /// <summary>{0} = section name, {1} = row count.</summary>
    public const string TodoSectionFormat = "{0} ({1})";

    public const string TodoRowClickHint = "Click: flag the giver on the map · right-click: more";
    public const string TodoRevealInTsukimichi = "Reveal in Tsukimichi";

    // Header context menu (right-click on the title).
    public const string TodoMenuLock = "Lock position";
    public const string TodoMenuUnlock = "Unlock position";
    public const string TodoMenuResetPosition = "Reset position";
    public const string TodoMenuHide = "Hide overlay";

    public static string TodoSectionName(TodoSection section) => section switch
    {
        TodoSection.Pinned => TodoSectionPinned,
        TodoSection.NearbyFeature => TodoSectionNearby,
        TodoSection.Msq => TodoSectionMsq,
        TodoSection.JobQuests => TodoSectionJobQuests,
        _ => section.ToString(),
    };

    // ---- Settings › Todo overlay ----
    public const string TodoConfigSection = "Todo overlay";
    public const string TodoConfigEnabled = "Show the todo overlay";
    public const string TodoConfigEnabledHint = "A small always-visible panel: your pins, feature quests you can start here, the next main scenario quest and your job quests. /tsuki todo toggles it.";
    public const string TodoConfigLocked = "Lock position";
    public const string TodoConfigLockedHint = "The overlay cannot be dragged; rows stay clickable.";
    public const string TodoConfigOpacity = "Background opacity";
    public const string TodoConfigSectionsLabel = "Sections";
    public const string TodoConfigShowPins = "Pinned quests";
    public const string TodoConfigShowNearby = "Feature quests you can start in this zone";
    public const string TodoConfigShowMsq = "Next main scenario quest";
    public const string TodoConfigShowJobQuests = "Job and role quests for the current job";
    public const string TodoConfigResetPosition = "Reset position";
    public const string TodoConfigResetPositionHint = "Move the overlay back to the top left of the screen";
}
