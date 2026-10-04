using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for going to the current step (plan v7, 1.21.0 P2: the detail pane's Where to go, the row menus) and the
/// plain chat lines of <c>/tsuki msq</c>, <c>/tsuki next</c> and <c>/tsuki go</c> with Say what's next in chat (P8). The
/// sentences themselves are Core's (<c>GuidanceText</c>). Every constant is prefixed <c>Step</c> or <c>Guidance</c>.
/// </summary>
static partial class Strings
{
    // ---- Detail pane: Where to go ----
    public static string StepWhereToGo => Loc.Get("StepWhereToGo");
    public static string StepAimAt => Loc.Get("StepAimAt");
    public static string StepAimCurrent => Loc.Get("StepAimCurrent");
    public static string StepAimCurrentShort => Loc.Get("StepAimCurrentShort");
    public static string StepAimGiver => Loc.Get("StepAimGiver");
    public static string StepAimCurrentTooltip => Loc.Get("StepAimCurrentTooltip");
    public static string StepAimGiverTooltip => Loc.Get("StepAimGiverTooltip");

    /// <summary>{0} = the step's number.</summary>
    public static string StepNumberFormat => Loc.Get("StepNumberFormat");

    /// <summary>{0} = how many more objectives the step has besides the one shown.</summary>
    public static string StepMoreFormat => Loc.Get("StepMoreFormat");
    public static string StepGiverLabel => Loc.Get("StepGiverLabel");
    public static string StepNoObjective => Loc.Get("StepNoObjective");

    /// <summary>What travel calls a marked area it walks to (the status line's "Walking to …").</summary>
    public static string StepAreaTarget => Loc.Get("StepAreaTarget");
    public static string StepAreaPlace => Loc.Get("StepAreaPlace");

    /// <summary>{0} = how many separate places the step has.</summary>
    public static string StepNearestFormat => Loc.Get("StepNearestFormat");

    /// <summary>{0}, {1} = map coordinates, one decimal.</summary>
    public static string StepCoordinatesFormat => Loc.Get("StepCoordinatesFormat");

    /// <summary>{0} = the zone the player stands in.</summary>
    public static string StepYoureInFormat => Loc.Get("StepYoureInFormat");

    /// <summary>{0} = the giver, {1} = the giver's zone.</summary>
    public static string StepNoPlaceFormat => Loc.Get("StepNoPlaceFormat");

    /// <summary>{0} = the duty, {1} = the giver, {2} = the giver's zone.</summary>
    public static string StepDutyFormat => Loc.Get("StepDutyFormat");
    public static string StepDutyFinder => Loc.Get("StepDutyFinder");
    public static string StepDutyFinderTooltip => Loc.Get("StepDutyFinderTooltip");

    /// <summary>{0} = the giver, {1} = the giver's zone.</summary>
    public static string StepNoteFormat => Loc.Get("StepNoteFormat");
    public static string StepGiverNote => Loc.Get("StepGiverNote");
    public static string StepFlag => Loc.Get("StepFlag");
    public static string StepGoTo => Loc.Get("StepGoTo");
    public static string StepWalkTo => Loc.Get("StepWalkTo");

    // ---- Row menus (Todo overlay, Nearby) ----
    public static string StepMenuFlag => Loc.Get("StepMenuFlag");
    public static string StepMenuTeleport => Loc.Get("StepMenuTeleport");
    public static string StepMenuWalk => Loc.Get("StepMenuWalk");
    public static string StepMenuFlagGiver => Loc.Get("StepMenuFlagGiver");

    // ---- Chat: /tsuki go, Say what's next ----
    public static string GuidanceNothingToGo => Loc.Get("GuidanceNothingToGo");

    /// <summary>{0} = the quest's name.</summary>
    public static string GuidanceNoPlaceFormat => Loc.Get("GuidanceNoPlaceFormat");
    public static string GuidanceChatHeader => Loc.Get("GuidanceChatHeader");
    public static string GuidanceSayNext => Loc.Get("GuidanceSayNext");
    public static string GuidanceSayNextHint => Loc.Get("GuidanceSayNextHint");

    /// <summary>{0} = a sample line.</summary>
    public static string GuidanceSayNextExampleFormat => Loc.Get("GuidanceSayNextExampleFormat");
}
