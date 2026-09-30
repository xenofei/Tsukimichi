namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the unlock route (feature plan v3 P6): the route window, its entry points in the detail pane, the
/// Moonlit row menu and the Characters job rows. Constant names carry the <c>Route</c> prefix so this part of the
/// partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Window ----
    public const string RouteWindowTitle = "Unlock route###tsukimichiRoute";

    /// <summary>{0} = the target ("Blue Mage", "Retainers", a quest's name as the spoiler shield prints it).</summary>
    public const string RouteTitleFormat = "Route to {0}";

    /// <summary>{0} = the viewed character's name.</summary>
    public const string RouteForLiveFormat = "For {0}, live";

    /// <summary>{0} = the viewed character's name, {1} = when its snapshot was saved.</summary>
    public const string RouteForStoredFormat = "For {0}, from the snapshot saved {1}";

    public const string RouteForNobody = "No character selected: every quest on the way is listed, none marked done.";
    public const string RouteLoading = "The quest catalog is still loading.";

    /// <summary>{0} = the other quests, "Name (N quests)" joined by commas.</summary>
    public const string RouteAlsoUnlockedByFormat = "Also unlocked by: {0}";

    /// <summary>{0} = quest name, {1} = quests left on its way.</summary>
    public const string RouteAlternativeFormat = "{0} ({1})";

    public const string RouteAlreadyDoneHeading = "Already unlocked";
    public const string RouteAlreadyDoneBody = "The quest that unlocks this is completed on this character. Nothing left to do.";
    public const string RouteNoQuestHeading = "No quest known to unlock this";
    public const string RouteNoQuestBody = "Nothing in the quest data or the curated unlock lists names a quest for it.";
    public const string RouteLockedOut = "A quest on this route is locked out or removed from the game, so the route cannot be finished as it stands.";

    // ---- Lines ----
    /// <summary>{0} = the main scenario category ("Heavensward").</summary>
    public const string RouteMilestoneFormat = "Main scenario · {0}";

    public const string RouteAfterMainScenario = "After the main scenario";

    /// <summary>{0} = level to reach.</summary>
    public const string RouteGateFormat = "Level gate · reach Lv {0}";

    /// <summary>{0} = level to reach, {1} = the character's best level on a job the quest admits.</summary>
    public const string RouteGateWithLevelFormat = "Level gate · reach Lv {0} (your best job for it is Lv {1})";

    public const string RouteGateTooltip = "The next quest needs a higher level than anything before it on the route. Level a job the quest accepts first.";

    /// <summary>{0} = level.</summary>
    public const string RouteLevelFormat = "Lv {0}";

    public const string RouteMsqMark = "MSQ";
    public const string RouteTargetMark = "target";

    /// <summary>{0} = quest name, {1} = quests left on that way.</summary>
    public const string RouteOrInsteadFormat = "or instead: {0} · {1} quests";

    /// <summary>{0} = quest name.</summary>
    public const string RouteOrInsteadOneFormat = "or instead: {0} · 1 quest";

    public const string RouteStepTooltipHint = "Click to show it in the Journal.";
    public const string RouteAlternativeTooltip = "Another way into the next quest, with how many quests it still needs. Click to show it in the Journal.";

    // ---- Actions ----
    public const string RouteCopy = "Copy route";
    public const string RouteCopyTooltip = "Copy the route as a Markdown list: levels, names as the spoiler shield shows them, MSQ milestones. Your character's name is not included.";
    public const string RouteCopied = "Copied the route to the clipboard.";

    /// <summary>{0} = number of quests; the label ends with <see cref="Chrome.HoldIdSuffix"/> where it is used.</summary>
    public const string RoutePinAllFormat = "Pin all ({0})";

    public const string RoutePinAllTooltip = "Hold to pin every quest on the route, in order (Shift+click pins at once). They join this character's pins and the Todo overlay.";
    public const string RoutePinAllUnavailable = "Pins belong to a character; pick one to pin for.";

    /// <summary>{0} = number of quests pinned.</summary>
    public const string RoutePinnedFormat = "Pinned {0} quests";

    public const string RoutePinnedOne = "Pinned 1 quest";
    public const string RouteAllPinnedAlready = "Every quest on the route is already pinned.";
    public const string RouteUndoSeparator = " · ";
    public const string RouteUndo = "Undo";
    public const string RouteUndoTooltip = "Unpin the quests Pin all just added";

    // ---- Entry points ----
    public const string RouteToThisTooltip = "Route to this: every quest still to do before it, in order, with level gates";
    public const string RouteToThisReward = "Route to this reward";

    /// <summary>{0} = job name.</summary>
    public const string RouteToUnlockJobFormat = "Route to unlock {0}";

    public const string RouteToUnlockMenu = "Route to unlock…";
    public const string RouteToUnlockMenuTooltip = "Pick a job this character has not unlocked yet and see the quests still to do for it";
    public const string RouteToUnlockNone = "Every job is unlocked.";
}
