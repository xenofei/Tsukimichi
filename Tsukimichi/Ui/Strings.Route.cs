using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the unlock route (feature plan v3 P6): the route window, its entry points in the detail pane, the
/// Moonlit row menu and the Characters job rows. Constant names carry the <c>Route</c> prefix so this part of the
/// partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Window ----
    public static string RouteWindowTitle => Loc.Get("RouteWindowTitle");

    /// <summary>{0} = the target ("Blue Mage", "Retainers", a quest's name as the spoiler shield prints it).</summary>
    public static string RouteTitleFormat => Loc.Get("RouteTitleFormat");

    /// <summary>{0} = the viewed character's name.</summary>
    public static string RouteForLiveFormat => Loc.Get("RouteForLiveFormat");

    /// <summary>{0} = the viewed character's name, {1} = when its snapshot was saved.</summary>
    public static string RouteForStoredFormat => Loc.Get("RouteForStoredFormat");

    public static string RouteForNobody => Loc.Get("RouteForNobody");
    public static string RouteLoading => Loc.Get("RouteLoading");

    /// <summary>{0} = the other quests, "Name (N quests)" joined by commas.</summary>
    public static string RouteAlsoUnlockedByFormat => Loc.Get("RouteAlsoUnlockedByFormat");

    /// <summary>{0} = quest name, {1} = quests left on its way.</summary>
    public const string RouteAlternativeFormat = "{0} ({1})";

    public static string RouteAlreadyDoneHeading => Loc.Get("RouteAlreadyDoneHeading");
    public static string RouteAlreadyDoneBody => Loc.Get("RouteAlreadyDoneBody");
    public static string RouteNoQuestHeading => Loc.Get("RouteNoQuestHeading");
    public static string RouteNoQuestBody => Loc.Get("RouteNoQuestBody");
    public static string RouteLockedOut => Loc.Get("RouteLockedOut");

    // ---- Lines ----
    /// <summary>{0} = the main scenario category ("Heavensward").</summary>
    public static string RouteMilestoneFormat => Loc.Get("RouteMilestoneFormat");

    public static string RouteAfterMainScenario => Loc.Get("RouteAfterMainScenario");

    /// <summary>{0} = level to reach.</summary>
    public static string RouteGateFormat => Loc.Get("RouteGateFormat");

    /// <summary>{0} = level to reach, {1} = the character's best level on a job the quest admits.</summary>
    public static string RouteGateWithLevelFormat => Loc.Get("RouteGateWithLevelFormat");

    public static string RouteGateTooltip => Loc.Get("RouteGateTooltip");

    /// <summary>{0} = level.</summary>
    public static string RouteLevelFormat => Loc.Get("RouteLevelFormat");

    public static string RouteMsqMark => Loc.Get("RouteMsqMark");
    public static string RouteTargetMark => Loc.Get("RouteTargetMark");

    /// <summary>{0} = quest name, {1} = quests left on that way.</summary>
    public static string RouteOrInsteadFormat => Loc.Get("RouteOrInsteadFormat");

    /// <summary>{0} = quest name.</summary>
    public static string RouteOrInsteadOneFormat => Loc.Get("RouteOrInsteadOneFormat");

    public static string RouteStepTooltipHint => Loc.Get("RouteStepTooltipHint");
    public static string RouteAlternativeTooltip => Loc.Get("RouteAlternativeTooltip");

    // ---- Actions ----
    public static string RouteCopy => Loc.Get("RouteCopy");
    public static string RouteCopyTooltip => Loc.Get("RouteCopyTooltip");
    public static string RouteCopied => Loc.Get("RouteCopied");

    /// <summary>{0} = number of quests; the label ends with <see cref="Chrome.HoldIdSuffix"/> where it is used.</summary>
    public static string RoutePinAllFormat => Loc.Get("RoutePinAllFormat");

    public static string RoutePinAllTooltip => Loc.Get("RoutePinAllTooltip");
    public static string RoutePinAllUnavailable => Loc.Get("RoutePinAllUnavailable");

    /// <summary>{0} = number of quests pinned.</summary>
    public static string RoutePinnedFormat => Loc.Get("RoutePinnedFormat");

    public static string RoutePinnedOne => Loc.Get("RoutePinnedOne");
    public static string RouteAllPinnedAlready => Loc.Get("RouteAllPinnedAlready");
    public const string RouteUndoSeparator = " · ";
    public static string RouteUndo => Loc.Get("RouteUndo");
    public static string RouteUndoTooltip => Loc.Get("RouteUndoTooltip");

    // ---- Entry points ----
    public static string RouteToThisTooltip => Loc.Get("RouteToThisTooltip");
    public static string RouteToThisReward => Loc.Get("RouteToThisReward");

    /// <summary>{0} = job name.</summary>
    public static string RouteToUnlockJobFormat => Loc.Get("RouteToUnlockJobFormat");

    public static string RouteToUnlockMenu => Loc.Get("RouteToUnlockMenu");
    public static string RouteToUnlockMenuTooltip => Loc.Get("RouteToUnlockMenuTooltip");
    public static string RouteToUnlockNone => Loc.Get("RouteToUnlockNone");
}
