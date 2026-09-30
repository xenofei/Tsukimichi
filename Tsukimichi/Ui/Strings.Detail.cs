namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the detail pane's card stack (T16): the hero, the cards' captions, the star-chart Path, the action
/// bar, the provenance line, the empty states and the Tonight card. Constant names carry the <c>Detail</c>,
/// <c>Path</c>, <c>Action</c>, <c>Provenance</c>, <c>Empty</c> or <c>Tonight</c> prefix so this part of the partial
/// class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Hero and cards ----
    /// <summary>{0} = unmet requirements, {1} = all requirements.</summary>
    public const string DetailRequirementsUnmetFormat = "{0} of {1} unmet";
    public const string DetailRequirementsAllMet = "All met";

    /// <summary>{0} = rewards that exist nowhere else.</summary>
    public const string DetailRewardsUniqueFormat = "{0} unique";
    public const string DetailUniqueRewardTooltip = "Unique: this quest is the only way to get it";
    public const string DetailNoGiverPlace = "No map position recorded";

    // ---- Path (star chart) ----
    /// <summary>{0} = steps on the path, {1} = steps done.</summary>
    public const string PathCaptionFormat = "{0} steps · {1} done";
    public const string PathCaptionOne = "1 step";

    /// <summary>{0} = completed steps folded into one bead.</summary>
    public const string PathMoonsWalkedFormat = "{0} moons walked";
    public const string PathAlternativePrefix = "or via ";

    /// <summary>{0} = quests still to do on the alternative's own path.</summary>
    public const string PathAlternativeStepsFormat = " · {0} steps";
    public const string PathAlternativeOneStep = " · 1 step";
    public const string PathAlternativeDone = " · done";

    /// <summary>{0} = quests still to do on the alternative's own path.</summary>
    public const string PathAlternativeTooltipFormat = "Another way in · {0} steps to go. Click to see its path.";
    public const string PathAlternativeTooltipDone = "Another way in, already walked. Click to see its path.";

    /// <summary>{0} = level, {1} = expansion.</summary>
    public const string PathStepDetailFormat = "Lv {0} · {1}";
    public const string PathSingleCaption = "Starts its own path: no previous quests.";
    public const string PathAloneCaption = "Stands alone: no previous quests, unlocks nothing.";
    public const string PathNotCheckedCaption = "Not checked yet: log in to light the path.";

    /// <summary>{0} = how many quests this one opens.</summary>
    public const string PathUnlocksHeaderFormat = "UNLOCKS NEXT · {0}";
    public const string PathUnlocksHeaderNone = "UNLOCKS NEXT · none";
    public const string PathJumpToTarget = "target";
    public const string PathJumpToTargetTooltip = "Scroll back to this quest";
    public const string PathMinimapTooltip = "The whole path: gold is walked, the dot is this quest. Click to scroll there.";

    // ---- Action bar ----
    public const string ActionTeleport = "Teleport";

    /// <summary>{0} = the aetheryte.</summary>
    public const string ActionTeleportTooltipFormat = "Teleport to {0} with Lifestream";
    public const string ActionFlagUnavailable = "No map position is recorded for the giver.";
    public const string ActionPinTooltip = "Pin: keep this quest on the Todo overlay and at the top of the table";
    public const string ActionUnpinTooltip = "Unpin this quest";
    public const string ActionPinUnavailable = "Pins belong to a character: log in or pick one first.";
    public const string ActionShowPathTooltip = "Show path: scroll to the Path card and light this quest";
    public const string ActionCopyCoordinatesUnavailable = "No map position is recorded for the giver.";

    // ---- Provenance ----
    /// <summary>{0} = how long ago ("just now", "5 min ago").</summary>
    public const string ProvenanceLiveFormat = "Checked {0} · live";

    /// <summary>{0} = the character's first name, {1} = how long ago.</summary>
    public const string ProvenanceSnapshotFormat = "From {0}'s snapshot, {1}";
    public const string ProvenanceLogIn = "Log in to check this quest";

    // ---- Empty states ----
    public const string EmptyNothingMatchesHeading = "Nothing matches";

    public const string EmptyFiltersHiding = "Each of these filters hides every quest here; click one to clear it.";
    public const string EmptyChipTooltip = "Clear this filter";
    public const string EmptyNotInCatalogHeading = "Quest not found";
    public const string EmptyNotInCatalogBody = "This quest is not in the catalog Tsukimichi loaded.";
    public const string EmptyClearSelection = "Clear selection";

    // ---- Tonight ----
    public const string TonightTitle = "Tonight";

    /// <summary>{0} = quests Ready now.</summary>
    public const string TonightReadyFormat = "{0} quests you can pick up now";
    public const string TonightReadyOne = "1 quest you can pick up now";
    public const string TonightReadyNone = "Nothing to pick up right now";
    public const string TonightShowReady = "Show them";
    public const string TonightShowReadyTooltip = "Open the Journal showing just these Ready quests (clears the search, the quick view and the other filters)";
    public const string TonightMsqLabel = "Main scenario";
    public const string TonightMsqDone = "Main scenario complete";

    /// <summary>{0} = the running events, joined.</summary>
    public const string TonightEventsFormat = "Events now: {0}";

    /// <summary>{0} = the event's name, {1} = its quests Ready now.</summary>
    public const string TonightEventReadyFormat = "{0} ({1} ready)";
    public const string TonightEventFallback = "Seasonal event";
    public const string TonightPinnedTitle = "Pinned and ready";
    public const string TonightPickHint = "Pick a quest to see what blocks it and what it gives.";
    public const string TonightLogIn = "Log in and Tsukimichi reads your journal. Nothing in the game changes.";
    public const string TonightRowTooltip = "Select this quest";
}
