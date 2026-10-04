using Tsukimichi.Core.Travel;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for runs you can trust (feature plan v7, 1.18.0): travel recovery (A8: the navmesh reload, the walk to the
/// aetheryte before a hop) and the travel preflight in Setup (A9). Keys carry the
/// <c>Travel</c>, <c>ActionTravel</c> or <c>TravelPreflight</c> prefix.
/// </summary>
static partial class Strings
{
    /// <summary>Chat line when the one recovery starts. {0} = "Walk to giver" or "Go to giver", {1} = reason (TravelFail*).</summary>
    public static string TravelReloadingFormat => Loc.Get("TravelReloadingFormat");

    /// <summary>Reason in TravelGoToStoppedFormat after the reload did not help. {0} = reason (TravelFail*).</summary>
    public static string TravelFailAfterReloadFormat => Loc.Get("TravelFailAfterReloadFormat");

    public static string TravelStepReloading => Loc.Get("TravelStepReloading");

    public static string TravelStepToAetheryte => Loc.Get("TravelStepToAetheryte");

    public static string ActionTravelStepReloading => Loc.Get("ActionTravelStepReloading");

    public static string ActionTravelStepToAetheryte => Loc.Get("ActionTravelStepToAetheryte");

    /// <summary>Go to giver tooltip step before the hop.</summary>
    public static string TravelGoToStepToAetheryte => Loc.Get("TravelGoToStepToAetheryte");

    /// <summary>Chat line when a walk starts with preflight findings. {0} = the findings, joined.</summary>
    public static string TravelPreflightWalkWarningFormat => Loc.Get("TravelPreflightWalkWarningFormat");

    /// <summary>Joins the findings in TravelPreflightWalkWarningFormat.</summary>
    public static string TravelPreflightClauseSeparator => Loc.Get("TravelPreflightClauseSeparator");

    /// <summary>One finding in TravelPreflightWalkWarningFormat.</summary>
    public static string TravelPreflightWalkClause(PreflightItem item) => Loc.Get("TravelPreflightWalk." + item);

    public static string ConfigTravelPreflight => Loc.Get("ConfigTravelPreflight");

    public static string ConfigTravelPreflightHint => Loc.Get("ConfigTravelPreflightHint");

    /// <summary>The check's name in Setup ("Movement type").</summary>
    public static string TravelPreflightLabel(PreflightItem item) => Loc.Get("TravelPreflightLabel." + item);

    /// <summary>What the check found, in a short plain sentence.</summary>
    public static string TravelPreflightStatus(PreflightItem item, PreflightState state) => Loc.Get("TravelPreflightStatus." + item + "." + state);

    /// <summary>The word for a check's state in its tooltip.</summary>
    public static string TravelPreflightWord(PreflightState state) => Loc.Get("TravelPreflightWord." + state);

    /// <summary>Why the check matters, for its tooltip.</summary>
    public static string TravelPreflightWhy(PreflightItem item) => Loc.Get("TravelPreflightWhy." + item);

    /// <summary>{0} = the plugin's name, {1} = what it breaks (TravelPreflightConflict.*).</summary>
    public static string TravelPreflightConflictFormat => Loc.Get("TravelPreflightConflictFormat");

    /// <summary>What a known conflict breaks; <paramref name="key"/> is <see cref="KnownConflict.Key"/>.</summary>
    public static string TravelPreflightConflict(string key) => Loc.Get("TravelPreflightConflict." + key);

    /// <summary>The fix button's label.</summary>
    public static string TravelPreflightFixLabel(PreflightFix fix) => Loc.Get("TravelPreflightFix." + fix);

    /// <summary>The fix button's tooltip.</summary>
    public static string TravelPreflightFixTooltip(PreflightFix fix) => Loc.Get("TravelPreflightFixTooltip." + fix);

    /// <summary>The note after a fix went through.</summary>
    public static string TravelPreflightFixed(PreflightItem item) => Loc.Get("TravelPreflightFixed." + item);

    /// <summary>The Undo button's label after a fix ("Restore Legacy").</summary>
    public static string TravelPreflightUndoLabel(PreflightItem item) => Loc.Get("TravelPreflightUndo." + item);

    public static string TravelPreflightUndoTooltip => Loc.Get("TravelPreflightUndoTooltip");

    public static string TravelPreflightUndone => Loc.Get("TravelPreflightUndone");

    public static string TravelPreflightFixFailed => Loc.Get("TravelPreflightFixFailed");
}
