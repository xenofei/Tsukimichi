using Tsukimichi.Localization;

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
    public static string DetailRequirementsUnmetFormat => Loc.Get("DetailRequirementsUnmetFormat");
    public static string DetailRequirementsAllMet => Loc.Get("DetailRequirementsAllMet");

    /// <summary>{0} = rewards that exist nowhere else.</summary>
    public static string DetailRewardsUniqueFormat => Loc.Get("DetailRewardsUniqueFormat");
    public static string DetailUniqueRewardTooltip => Loc.Get("DetailUniqueRewardTooltip");
    public static string DetailNoGiverPlace => Loc.Get("DetailNoGiverPlace");

    /// <summary>Appended to the hero's caption line (P8): {0} = the patch the quest was added in, as the game writes it.</summary>
    public static string DetailAddedInFormat => Loc.Get("DetailAddedInFormat");

    /// <summary>The header badge's tooltip for <c>QuestRecord.IconSpecial</c>: a seasonal event quest, or another special one (a promotion).</summary>
    public static string DetailSeasonalBadgeTooltip => Loc.Get("DetailSeasonalBadgeTooltip");
    public static string DetailSpecialBadgeTooltip => Loc.Get("DetailSpecialBadgeTooltip");

    // ---- Chain line at the top of the Path card ----
    /// <summary>{0} = chain name, {1} = quests done, {2} = quests in the chain.</summary>
    public static string DetailChainFormat => Loc.Get("DetailChainFormat");

    /// <summary>A side story: {0} = the story's name ("Story: …"), {1} = quests done, {2} = quests in it.</summary>
    public static string DetailStoryFormat => Loc.Get("DetailStoryFormat");
    public static string DetailChainNext => Loc.Get("DetailChainNext");
    public static string DetailChainComplete => Loc.Get("DetailChainComplete");
    public static string DetailChainNextTooltip => Loc.Get("DetailChainNextTooltip");

    /// <summary>The chain halo's tooltip: {0} = quests done, {1} = quests in the chain.</summary>
    public static string DetailChainHaloTooltipFormat => Loc.Get("DetailChainHaloTooltipFormat");

    // ---- Requirements you do not meet (L8) ----
    /// <summary>A jump button's tooltip: {0} = the quest that clears the requirement.</summary>
    public static string DetailJumpTooltipFormat => Loc.Get("DetailJumpTooltipFormat");

    // ---- Path (star chart) ----
    /// <summary>{0} = steps on the path, {1} = steps done.</summary>
    public static string PathCaptionFormat => Loc.Get("PathCaptionFormat");
    public static string PathCaptionOne => Loc.Get("PathCaptionOne");

    /// <summary>{0} = completed steps folded into one bead.</summary>
    public static string PathMoonsWalkedFormat => Loc.Get("PathMoonsWalkedFormat");
    /// <summary>{0} = the other quest that leads here.</summary>
    public static string PathAlternativeFormat => Loc.Get("PathAlternativeFormat");

    /// <summary>{0} = quests still to do on the alternative's own path.</summary>
    public static string PathAlternativeStepsFormat => Loc.Get("PathAlternativeStepsFormat");
    public static string PathAlternativeOneStep => Loc.Get("PathAlternativeOneStep");
    public static string PathAlternativeDone => Loc.Get("PathAlternativeDone");

    /// <summary>{0} = quests still to do on the alternative's own path.</summary>
    public static string PathAlternativeTooltipFormat => Loc.Get("PathAlternativeTooltipFormat");
    public static string PathAlternativeTooltipDone => Loc.Get("PathAlternativeTooltipDone");

    /// <summary>{0} = level, {1} = expansion.</summary>
    public static string PathStepDetailFormat => Loc.Get("PathStepDetailFormat");
    public static string PathSingleCaption => Loc.Get("PathSingleCaption");
    public static string PathAloneCaption => Loc.Get("PathAloneCaption");
    public static string PathNotCheckedCaption => Loc.Get("PathNotCheckedCaption");

    /// <summary>{0} = how many quests this one opens.</summary>
    public static string PathUnlocksHeaderFormat => Loc.Get("PathUnlocksHeaderFormat");
    public static string PathUnlocksHeaderNone => Loc.Get("PathUnlocksHeaderNone");
    public static string PathJumpToTarget => Loc.Get("PathJumpToTarget");
    public static string PathJumpToTargetTooltip => Loc.Get("PathJumpToTargetTooltip");
    public static string PathMinimapTooltip => Loc.Get("PathMinimapTooltip");

    // ---- Action bar ----
    public static string ActionTeleport => Loc.Get("ActionTeleport");

    /// <summary>{0} = the aetheryte.</summary>
    public static string ActionTeleportTooltipFormat => Loc.Get("ActionTeleportTooltipFormat");
    public static string ActionFlagUnavailable => Loc.Get("ActionFlagUnavailable");
    public static string ActionPinTooltip => Loc.Get("ActionPinTooltip");
    public static string ActionUnpinTooltip => Loc.Get("ActionUnpinTooltip");
    public static string ActionPinUnavailable => Loc.Get("ActionPinUnavailable");
    public static string ActionShowPathTooltip => Loc.Get("ActionShowPathTooltip");
    public static string ActionCopyCoordinatesUnavailable => Loc.Get("ActionCopyCoordinatesUnavailable");

    // ---- Provenance ----
    /// <summary>{0} = how long ago ("just now", "5 min ago").</summary>
    public static string ProvenanceLiveFormat => Loc.Get("ProvenanceLiveFormat");

    /// <summary>{0} = the character's first name, {1} = how long ago.</summary>
    public static string ProvenanceSnapshotFormat => Loc.Get("ProvenanceSnapshotFormat");
    public static string ProvenanceLogIn => Loc.Get("ProvenanceLogIn");

    // ---- Empty states ----
    public static string EmptyNothingMatchesHeading => Loc.Get("EmptyNothingMatchesHeading");

    public static string EmptyFiltersHiding => Loc.Get("EmptyFiltersHiding");
    public static string EmptyChipTooltip => Loc.Get("EmptyChipTooltip");
    public static string EmptyNotInCatalogHeading => Loc.Get("EmptyNotInCatalogHeading");
    public static string EmptyNotInCatalogBody => Loc.Get("EmptyNotInCatalogBody");
    public static string EmptyClearSelection => Loc.Get("EmptyClearSelection");

    // ---- Tonight ----
    public static string TonightTitle => Loc.Get("TonightTitle");

    /// <summary>{0} = quests Ready now.</summary>
    public static string TonightReadyFormat => Loc.Get("TonightReadyFormat");
    public static string TonightReadyOne => Loc.Get("TonightReadyOne");
    public static string TonightReadyNone => Loc.Get("TonightReadyNone");
    public static string TonightShowReady => Loc.Get("TonightShowReady");
    public static string TonightShowReadyTooltip => Loc.Get("TonightShowReadyTooltip");
    public static string TonightMsqLabel => Loc.Get("TonightMsqLabel");
    public static string TonightMsqDone => Loc.Get("TonightMsqDone");

    /// <summary>{0} = the running events, joined.</summary>
    public static string TonightEventsFormat => Loc.Get("TonightEventsFormat");

    /// <summary>{0} = the event's name, {1} = its quests Ready now.</summary>
    public static string TonightEventReadyFormat => Loc.Get("TonightEventReadyFormat");
    public static string TonightEventFallback => Loc.Get("TonightEventFallback");
    public static string TonightPinnedTitle => Loc.Get("TonightPinnedTitle");
    public static string TonightPickHint => Loc.Get("TonightPickHint");
    public static string TonightLogIn => Loc.Get("TonightLogIn");
    public static string TonightRowTooltip => Loc.Get("TonightRowTooltip");
}
