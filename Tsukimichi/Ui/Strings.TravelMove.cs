using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for getting there faster (travel review, 1.10): interiors and their ways in, the attunement substitute,
/// mounting, flying, landing and Sprint, the status line under the detail pane's pills and their settings. Keys carry
/// the <c>Travel</c>, <c>ActionTravel</c> or <c>ConfigTravel</c> prefix.
/// </summary>
static partial class Strings
{
    /// <summary>{0} = the nearest aetheryte, {1} = the attuned one used instead.</summary>
    public static string TravelSubstitutedFormat => Loc.Get("TravelSubstitutedFormat");

    /// <summary>{0} = the nearest aetheryte, {1} = the attuned one in another region.</summary>
    public static string TravelTooFarFormat => Loc.Get("TravelTooFarFormat");

    /// <summary>{0} = the giver, {1} = the zone the giver stands in, {2} = aetheryte.</summary>
    public static string TravelInsideFormat => Loc.Get("TravelInsideFormat");

    /// <summary>{0} = the giver, {1} = the zone the giver stands in, {2} = aetheryte.</summary>
    public static string TravelInsideUnplacedFormat => Loc.Get("TravelInsideUnplacedFormat");

    /// <summary>Go to giver tooltip step. {0} = the zone the giver stands in.</summary>
    public static string TravelGoToStepEntranceFormat => Loc.Get("TravelGoToStepEntranceFormat");

    /// <summary>Go to giver tooltip. {0} = the zone the giver stands in.</summary>
    public static string TravelGoToNoEntranceFormat => Loc.Get("TravelGoToNoEntranceFormat");

    /// <summary>Chat line when Go to giver or Walk ends at an interior's door. {0} = zone, {1} = giver.</summary>
    public static string TravelArrivedEntranceFormat => Loc.Get("TravelArrivedEntranceFormat");

    /// <summary>{0} = the zone the giver stands in, {1} = the giver.</summary>
    public static string TravelWalkEntranceTooltipFormat => Loc.Get("TravelWalkEntranceTooltipFormat");

    /// <summary>{0} = the giver, {1} = the zone the giver stands in.</summary>
    public static string TravelWalkNoEntranceFormat => Loc.Get("TravelWalkNoEntranceFormat");

    /// <summary>Status line target. {0} = the zone the giver stands in.</summary>
    public static string TravelTargetEntranceFormat => Loc.Get("TravelTargetEntranceFormat");

    /// <summary>Walk and Go to giver tooltip. {0} = yalms.</summary>
    public static string TravelMoveMountFormat => Loc.Get("TravelMoveMountFormat");

    public static string TravelMoveFly => Loc.Get("TravelMoveFly");

    public static string TravelMoveSprint => Loc.Get("TravelMoveSprint");

    public static string TravelStepMounting => Loc.Get("TravelStepMounting");

    public static string TravelStepFlying => Loc.Get("TravelStepFlying");

    public static string TravelStepLanding => Loc.Get("TravelStepLanding");

    public static string TravelFailStuck => Loc.Get("TravelFailStuck");

    public static string TravelFailLanding => Loc.Get("TravelFailLanding");

    /// <summary>{0} = aetheryte.</summary>
    public static string ActionTravelStepTeleportingToFormat => Loc.Get("ActionTravelStepTeleportingToFormat");

    /// <summary>{0} = shard.</summary>
    public static string ActionTravelStepHoppingToFormat => Loc.Get("ActionTravelStepHoppingToFormat");

    public static string ActionTravelStepMounting => Loc.Get("ActionTravelStepMounting");

    /// <summary>{0} = the giver, or the way into a zone.</summary>
    public static string ActionTravelStepWalkingToFormat => Loc.Get("ActionTravelStepWalkingToFormat");

    /// <summary>{0} = the giver, or the way into a zone.</summary>
    public static string ActionTravelStepRidingToFormat => Loc.Get("ActionTravelStepRidingToFormat");

    /// <summary>{0} = the giver, or the way into a zone.</summary>
    public static string ActionTravelStepFlyingToFormat => Loc.Get("ActionTravelStepFlyingToFormat");

    public static string ActionTravelStepLanding => Loc.Get("ActionTravelStepLanding");

    public static string ConfigTravelMountDistance => Loc.Get("ConfigTravelMountDistance");

    public static string ConfigTravelMountDistanceHint => Loc.Get("ConfigTravelMountDistanceHint");

    /// <summary>Slider format; %d = yalms.</summary>
    public static string ConfigTravelMountDistanceFormat => Loc.Get("ConfigTravelMountDistanceFormat");

    public static string ConfigTravelMount => Loc.Get("ConfigTravelMount");

    public static string ConfigTravelMountHint => Loc.Get("ConfigTravelMountHint");

    public static string ConfigTravelMountRoulette => Loc.Get("ConfigTravelMountRoulette");

    public static string ConfigTravelFly => Loc.Get("ConfigTravelFly");

    public static string ConfigTravelFlyHint => Loc.Get("ConfigTravelFlyHint");

    public static string ConfigTravelSprint => Loc.Get("ConfigTravelSprint");

    public static string ConfigTravelSprintHint => Loc.Get("ConfigTravelSprintHint");

    /// <summary>Stands for a quest giver whose name is unknown, inside a sentence.</summary>
    public static string TravelTheGiver => Loc.Get("TravelTheGiver");
}
