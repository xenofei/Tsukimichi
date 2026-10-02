using Tsukimichi.Core.Todo;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the todo overlay (V2-13) and its settings section. Every constant is prefixed <c>Todo</c> so the
/// partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Overlay window ----
    public static string TodoWindowTitle => Loc.Get("TodoWindowTitle");
    public static string TodoHeader => Loc.Get("TodoHeader");
    public static string TodoEmpty => Loc.Get("TodoEmpty");
    public static string TodoNoSections => Loc.Get("TodoNoSections");

    public static string TodoSectionPinned => Loc.Get("TodoSectionPinned");
    public static string TodoSectionSeasonal => Loc.Get("TodoSectionSeasonal");
    public static string TodoSectionNearby => Loc.Get("TodoSectionNearby");
    public static string TodoSectionMsq => Loc.Get("TodoSectionMsq");
    public static string TodoSectionJobQuests => Loc.Get("TodoSectionJobQuests");

    /// <summary>{0} = section name, {1} = row count.</summary>
    public const string TodoSectionFormat = "{0} ({1})";

    /// <summary>{0} = rows a capped section leaves out ("+52 more").</summary>
    public static string TodoMoreFormat => Loc.Get("TodoMoreFormat");
    public static string TodoMoreTooltip => Loc.Get("TodoMoreTooltip");

    public static string TodoRowClickHint => Loc.Get("TodoRowClickHint");
    public static string TodoRowMoreTooltip => Loc.Get("TodoRowMoreTooltip");
    public static string TodoHeaderMoreTooltip => Loc.Get("TodoHeaderMoreTooltip");

    /// <summary>{0} = section name. Tooltip on a section header.</summary>
    public static string TodoSectionToggleFormat => Loc.Get("TodoSectionToggleFormat");
    public static string TodoRevealInTsukimichi => Loc.Get("TodoRevealInTsukimichi");

    // Header context menu (right-click on the title).
    public static string TodoMenuLock => Loc.Get("TodoMenuLock");
    public static string TodoMenuLockTooltip => Loc.Get("TodoMenuLockTooltip");
    public static string TodoMenuCompact => Loc.Get("TodoMenuCompact");
    public static string TodoMenuResetPosition => Loc.Get("TodoMenuResetPosition");
    public static string TodoMenuHide => Loc.Get("TodoMenuHide");

    public static string TodoSectionName(TodoSection section) => section switch
    {
        TodoSection.Pinned => TodoSectionPinned,
        TodoSection.Seasonal => TodoSectionSeasonal,
        TodoSection.NearbyFeature => TodoSectionNearby,
        TodoSection.Msq => TodoSectionMsq,
        TodoSection.JobQuests => TodoSectionJobQuests,
        TodoSection.Plan => PlanTodoSection,
        TodoSection.Route => RouteWindowTitle,
        TodoSection.NextStops => TodoSectionNextStops,
        _ => section.ToString(),
    };

    // ---- Settings › Todo overlay ----
    public static string TodoConfigSection => Loc.Get("TodoConfigSection");
    public static string TodoConfigEnabled => Loc.Get("TodoConfigEnabled");
    public static string TodoConfigEnabledHint => Loc.Get("TodoConfigEnabledHint");
    public static string TodoConfigLocked => Loc.Get("TodoConfigLocked");
    public static string TodoConfigLockedHint => Loc.Get("TodoConfigLockedHint");
    /// <summary>Settings › Todo overlay, under Locked, for a player who upgraded with the overlay locked (shown until they unlock it).</summary>
    public static string TodoLockUpgradeNotice => Loc.Get("TodoLockUpgradeNotice");
    /// <summary>The one chat line at login for a player who upgraded with the overlay locked.</summary>
    public static string TodoLockUpgradeChat => Loc.Get("TodoLockUpgradeChat");
    public static string TodoConfigCompact => Loc.Get("TodoConfigCompact");
    public static string TodoConfigCompactHint => Loc.Get("TodoConfigCompactHint");
    public static string TodoConfigOpacity => Loc.Get("TodoConfigOpacity");
    public static string TodoConfigSectionsLabel => Loc.Get("TodoConfigSectionsLabel");
    public static string TodoConfigShowPins => Loc.Get("TodoConfigShowPins");
    public static string TodoConfigShowSeasonal => Loc.Get("TodoConfigShowSeasonal");
    public static string TodoConfigShowSeasonalHint => Loc.Get("TodoConfigShowSeasonalHint");
    public static string TodoConfigShowNearby => Loc.Get("TodoConfigShowNearby");
    public static string TodoConfigShowMsq => Loc.Get("TodoConfigShowMsq");
    public static string TodoConfigShowJobQuests => Loc.Get("TodoConfigShowJobQuests");
    public static string TodoConfigResetPosition => Loc.Get("TodoConfigResetPosition");
    public static string TodoConfigResetPositionHint => Loc.Get("TodoConfigResetPositionHint");
}
