using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for 1.6.0's routes and Next stops (R6, C3): following a route, Flag next stop, the steps' place, Flag and
/// Teleport, routes to several targets and their entry points, and Next stops in the Tonight card and the todo
/// overlay. Names keep their surface's prefix (Route, Todo, Tonight, Plan, DutyHint) so the partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Route window: following ----
    public static string RouteFollow => Loc.Get("RouteFollow");
    public static string RouteFollowTooltip => Loc.Get("RouteFollowTooltip");
    public static string RouteFollowUnavailable => Loc.Get("RouteFollowUnavailable");
    public static string RouteStopFollowing => Loc.Get("RouteStopFollowing");
    public static string RouteStopFollowingTooltip => Loc.Get("RouteStopFollowingTooltip");
    public static string RouteFlagNextStop => Loc.Get("RouteFlagNextStop");
    public static string RouteFlagNextStopTooltip => Loc.Get("RouteFlagNextStopTooltip");
    public static string RouteFlagNextStopUnavailable => Loc.Get("RouteFlagNextStopUnavailable");

    // ---- Route window: where each step is ----
    public static string RouteStepFlag => Loc.Get("RouteStepFlag");
    public static string RouteStepFlagTooltip => Loc.Get("RouteStepFlagTooltip");
    public static string RouteStepFlagUnavailable => Loc.Get("RouteStepFlagUnavailable");
    public static string RouteStepTeleport => Loc.Get("RouteStepTeleport");
    public static string RouteTeleportNeedsLifestream => Loc.Get("RouteTeleportNeedsLifestream");

    /// <summary>{0} = steps in the stop, {1} = the aetheryte's place name.</summary>
    public static string RouteStopFormat => Loc.Get("RouteStopFormat");
    public static string RouteStopTooltip => Loc.Get("RouteStopTooltip");

    /// <summary>{0} = the aetheryte's place name.</summary>
    public static string RouteNearFormat => Loc.Get("RouteNearFormat");

    /// <summary>{0} = the milestones a step reaches on a route to several targets ("Dragoon quests").</summary>
    public static string RouteReachesFormat => Loc.Get("RouteReachesFormat");
    public const string RouteDetailSeparator = " · ";

    // ---- Routes to several targets ----
    /// <summary>{0} = job name.</summary>
    public static string RouteEverythingJobFormat => Loc.Get("RouteEverythingJobFormat");
    public static string RouteEverythingTooltip => Loc.Get("RouteEverythingTooltip");
    public static string PlanRouteBlues => Loc.Get("PlanRouteBlues");
    public static string PlanRouteBluesTooltip => Loc.Get("PlanRouteBluesTooltip");
    public static string PlanRouteToThis => Loc.Get("PlanRouteToThis");
    public static string PlanRouteToThisTooltip => Loc.Get("PlanRouteToThisTooltip");
    public static string PlanFlagNextStopTooltip => Loc.Get("PlanFlagNextStopTooltip");
    public static string PlanFlagNextStopUnavailable => Loc.Get("PlanFlagNextStopUnavailable");
    public static string DutyHintRoute => Loc.Get("DutyHintRoute");
    public static string DutyHintRouteTooltip => Loc.Get("DutyHintRouteTooltip");

    // ---- Todo overlay ----
    public static string TodoSectionNextStops => Loc.Get("TodoSectionNextStops");
    public static string TodoRouteStop => Loc.Get("TodoRouteStop");
    public static string TodoRouteMoreTooltip => Loc.Get("TodoRouteMoreTooltip");
    public static string TodoPinsMenuHint => Loc.Get("TodoPinsMenuHint");
    public static string TodoRoutePins => Loc.Get("TodoRoutePins");
    public static string TodoRoutePinsTooltip => Loc.Get("TodoRoutePinsTooltip");
    public static string TodoStopClickHint => Loc.Get("TodoStopClickHint");
    public static string TodoConfigShowRoute => Loc.Get("TodoConfigShowRoute");
    public static string TodoConfigShowRouteHint => Loc.Get("TodoConfigShowRouteHint");
    public static string TodoConfigRouteFlagAdvance => Loc.Get("TodoConfigRouteFlagAdvance");
    public static string TodoConfigRouteFlagAdvanceHint => Loc.Get("TodoConfigRouteFlagAdvanceHint");
    public static string ConfigSectionRoutes => Loc.Get("ConfigSectionRoutes");
    public static string TodoConfigShowNextStops => Loc.Get("TodoConfigShowNextStops");
    public static string TodoConfigShowNextStopsHint => Loc.Get("TodoConfigShowNextStopsHint");

    // ---- Tonight card ----
    public static string TonightStopsTitle => Loc.Get("TonightStopsTitle");

    /// <summary>{0} = the aetheryte's place name, {1} = "3 quests · 1 unlock".</summary>
    public static string TonightStopFormat => Loc.Get("TonightStopFormat");
    public static string TonightStopHereSuffix => Loc.Get("TonightStopHereSuffix");
    public static string TonightStopTeleport => Loc.Get("TonightStopTeleport");
    public static string TonightStopTooltip => Loc.Get("TonightStopTooltip");
}
