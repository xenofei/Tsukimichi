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

    /// <summary>
    /// Appended to the hero's caption line for a completed quest (decision 9): " · Done 12 Sep 2026" when the plugin saw
    /// it completed, " · Done by …" when it was found completed at a login, " · Done before …" when it was already
    /// complete when dates started being recorded for the character.
    /// </summary>
    public static string DetailDoneSuffix(Core.Runtime.QuestCompletionDate date)
    {
        var format = date.Kind switch
        {
            Core.Runtime.CompletionDateKind.By => Loc.Get("DetailDoneByFormat"),
            Core.Runtime.CompletionDateKind.Before => Loc.Get("DetailDoneBeforeFormat"),
            _ => Loc.Get("DetailDoneFormat"),
        };

        var day = date.Utc.ToLocalTime().ToString(DateFormat, System.Globalization.CultureInfo.CurrentCulture);
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, format, day);
    }

    /// <summary>The header badge's tooltip for <c>QuestRecord.IconSpecial</c>: a seasonal event quest, or another special one (a promotion).</summary>
    public static string DetailSeasonalBadgeTooltip => Loc.Get("DetailSeasonalBadgeTooltip");
    public static string DetailSpecialBadgeTooltip => Loc.Get("DetailSpecialBadgeTooltip");

    // ---- Hero banner source (V4), the banner's tooltip ----
    public static string DetailBannerOwn => Loc.Get("DetailBannerOwn");
    public static string DetailBannerSibling => Loc.Get("DetailBannerSibling");
    public static string DetailBannerDuty => Loc.Get("DetailBannerDuty");
    public static string DetailBannerZone => Loc.Get("DetailBannerZone");
    public static string DetailBannerCategory => Loc.Get("DetailBannerCategory");

    // ---- Links on the Path card (the chain line's words are Core's: ChainLine) ----
    /// <summary>The tooltip of a quest link on the Path card ("Next:" and the chain line).</summary>
    public static string DetailChainNextTooltip => Loc.Get("DetailChainNextTooltip");

    /// <summary>Before the earlier quest to do first, under the Path header: "Next: quest · Ready".</summary>
    public static string PathNextLabel => Loc.Get("PathNextLabel");

    // ---- Requirements you do not meet (L8) ----
    /// <summary>A jump button's tooltip: {0} = the quest that clears the requirement.</summary>
    public static string DetailJumpTooltipFormat => Loc.Get("DetailJumpTooltipFormat");

    // ---- Path (star chart; the header caption is Core's: PathHeading) ----
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

    // ---- Action bar: travel and automation pills (1.10) ----
    public static string ActionGoToShort => Loc.Get("ActionGoToShort");
    public static string ActionQuestionableStart => Loc.Get("ActionQuestionableStart");
    public static string ActionQuestionableShort => Loc.Get("ActionQuestionableShort");
    public static string ActionQuestionableStop => Loc.Get("ActionQuestionableStop");
    public static string ActionQuestionableStartTooltip => Loc.Get("ActionQuestionableStartTooltip");
    public static string ActionAutoDutyShort => Loc.Get("ActionAutoDutyShort");
    public static string ActionStopShort => Loc.Get("ActionStopShort");

    /// <summary>{0} = the step under way ("Teleporting…").</summary>
    public static string ActionTravelStatusFormat => Loc.Get("ActionTravelStatusFormat");
    public static string ActionTravelStepTeleporting => Loc.Get("ActionTravelStepTeleporting");
    public static string ActionTravelStepHopping => Loc.Get("ActionTravelStepHopping");
    public static string ActionTravelStepPreparing => Loc.Get("ActionTravelStepPreparing");
    public static string ActionTravelStepWalking => Loc.Get("ActionTravelStepWalking");
}
