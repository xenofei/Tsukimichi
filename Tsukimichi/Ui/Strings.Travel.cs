using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for travel (feature plan v5, 1.6.0): Teleport through Lifestream with attunement, cost and "already here",
/// the aethernet hop, vnavmesh's Walk to giver with Stop, the Go to giver chain, their chat lines and their settings.
/// Constant names carry the <c>Travel</c> prefix (the two settings the <c>Config</c> one) so this part of the partial
/// class never collides with the others.
/// </summary>
static partial class Strings
{
    public static string TravelNeedsLifestream => Loc.Get("TravelNeedsLifestream");

    /// <summary>{0} = the giver's zone.</summary>
    public static string TravelNotAttunedFormat => Loc.Get("TravelNotAttunedFormat");

    /// <summary>{0} = gil cost.</summary>
    public static string TravelCostFormat => Loc.Get("TravelCostFormat");

    public static string TravelFavourite => Loc.Get("TravelFavourite");

    /// <summary>{0} = aetheryte name.</summary>
    public static string TravelAlreadyHereFormat => Loc.Get("TravelAlreadyHereFormat");

    public static string TravelAlreadyInZone => Loc.Get("TravelAlreadyInZone");

    public static string TravelFirmamentHint => Loc.Get("TravelFirmamentHint");

    /// <summary>{0} = the giver's zone.</summary>
    public static string TravelCosmicHintFormat => Loc.Get("TravelCosmicHintFormat");

    public static string TravelIslandTooltip => Loc.Get("TravelIslandTooltip");

    public static string TravelOccultTooltip => Loc.Get("TravelOccultTooltip");

    /// <summary>{0} = aetheryte, {1} = reason.</summary>
    public static string TravelTeleportFailedFormat => Loc.Get("TravelTeleportFailedFormat");

    public static string TravelReasonCombat => Loc.Get("TravelReasonCombat");

    public static string TravelReasonCasting => Loc.Get("TravelReasonCasting");

    public static string TravelReasonLoading => Loc.Get("TravelReasonLoading");

    public static string TravelReasonDuty => Loc.Get("TravelReasonDuty");

    public static string TravelReasonCutscene => Loc.Get("TravelReasonCutscene");

    public static string TravelReasonOccupied => Loc.Get("TravelReasonOccupied");

    public static string TravelReasonDeclined => Loc.Get("TravelReasonDeclined");

    /// <summary>{0} = aethernet shard.</summary>
    public static string TravelHopFormat => Loc.Get("TravelHopFormat");

    /// <summary>{0} = aethernet shard.</summary>
    public static string TravelHopTooltipFormat => Loc.Get("TravelHopTooltipFormat");

    /// <summary>{0} = the city's aetheryte.</summary>
    public static string TravelHopStandAtFormat => Loc.Get("TravelHopStandAtFormat");

    public static string TravelFirmament => Loc.Get("TravelFirmament");

    public static string TravelWalk => Loc.Get("TravelWalk");

    public static string TravelWalkShort => Loc.Get("TravelWalkShort");

    public static string TravelStop => Loc.Get("TravelStop");

    public static string TravelWalkTooltip => Loc.Get("TravelWalkTooltip");

    public static string TravelStopTooltip => Loc.Get("TravelStopTooltip");

    public static string TravelNeedsVnavmesh => Loc.Get("TravelNeedsVnavmesh");

    /// <summary>{0} = the giver's zone.</summary>
    public static string TravelWalkNotInZoneFormat => Loc.Get("TravelWalkNotInZoneFormat");

    public static string TravelWalkCombat => Loc.Get("TravelWalkCombat");

    public static string TravelWalkCutscene => Loc.Get("TravelWalkCutscene");

    public static string TravelWalkLoading => Loc.Get("TravelWalkLoading");

    /// <summary>{0} = percent done.</summary>
    public static string TravelPreparingFormat => Loc.Get("TravelPreparingFormat");

    public static string TravelPreparing => Loc.Get("TravelPreparing");

    public static string TravelNoGiverPlace => Loc.Get("TravelNoGiverPlace");

    public static string TravelGoTo => Loc.Get("TravelGoTo");

    public static string TravelGoToTooltip => Loc.Get("TravelGoToTooltip");

    /// <summary>{0} = aetheryte.</summary>
    public static string TravelGoToStepTeleportFormat => Loc.Get("TravelGoToStepTeleportFormat");

    /// <summary>{0} = shard.</summary>
    public static string TravelGoToStepHopFormat => Loc.Get("TravelGoToStepHopFormat");

    public static string TravelGoToStepWalk => Loc.Get("TravelGoToStepWalk");

    /// <summary>{0} = zone.</summary>
    public static string TravelGoToNoWalkFormat => Loc.Get("TravelGoToNoWalkFormat");

    /// <summary>{0} = zone.</summary>
    public static string TravelGoToConversationFormat => Loc.Get("TravelGoToConversationFormat");

    /// <summary>{0} = the step under way.</summary>
    public static string TravelGoToStopTooltipFormat => Loc.Get("TravelGoToStopTooltipFormat");

    public static string TravelStepTeleporting => Loc.Get("TravelStepTeleporting");

    public static string TravelStepHopping => Loc.Get("TravelStepHopping");

    public static string TravelStepPreparing => Loc.Get("TravelStepPreparing");

    public static string TravelStepWalking => Loc.Get("TravelStepWalking");

    /// <summary>{0} = reason.</summary>
    public static string TravelGoToStoppedFormat => Loc.Get("TravelGoToStoppedFormat");

    /// <summary>{0} = reason.</summary>
    public static string TravelWalkStoppedFormat => Loc.Get("TravelWalkStoppedFormat");

    public static string TravelFailNotInZone => Loc.Get("TravelFailNotInZone");

    public static string TravelFailTeleportDidNotStart => Loc.Get("TravelFailTeleportDidNotStart");

    public static string TravelFailTeleportTimedOut => Loc.Get("TravelFailTeleportTimedOut");

    public static string TravelFailHopRefused => Loc.Get("TravelFailHopRefused");

    public static string TravelFailHopDidNotStart => Loc.Get("TravelFailHopDidNotStart");

    public static string TravelFailHopTimedOut => Loc.Get("TravelFailHopTimedOut");

    public static string TravelFailPathNotReady => Loc.Get("TravelFailPathNotReady");

    public static string TravelFailWalkRefused => Loc.Get("TravelFailWalkRefused");

    public static string TravelFailWalkDidNotStart => Loc.Get("TravelFailWalkDidNotStart");

    public static string TravelFailWalkTimedOut => Loc.Get("TravelFailWalkTimedOut");

    public static string TravelFailWalkStoppedShort => Loc.Get("TravelFailWalkStoppedShort");

    public static string TravelFailLeftZone => Loc.Get("TravelFailLeftZone");

    public static string ConfigShowWalk => Loc.Get("ConfigShowWalk");

    public static string ConfigShowWalkHint => Loc.Get("ConfigShowWalkHint");

    public static string ConfigShowGoTo => Loc.Get("ConfigShowGoTo");

    public static string ConfigShowGoToHint => Loc.Get("ConfigShowGoToHint");
}
