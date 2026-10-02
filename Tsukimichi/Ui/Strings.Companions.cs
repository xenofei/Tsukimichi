using Tsukimichi.Core.Companions;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the companion plugins (feature plan v5, decision 1): the registry's disabled-button reasons, Settings ›
/// Integrations › Companion plugins, the detail pane's Duties section ("Run with AutoDuty") and "Open in Quest Map",
/// the Help topic and the tour step. Constant names carry a <c>Companion</c>, <c>AutoDuty</c> or <c>QuestMap</c> prefix
/// so this part of the partial class never collides with the others. English only until localization reopens.
/// </summary>
static partial class Strings
{
    // ---- Registry: why a button is disabled ----

    /// <summary>{0} = the plugin's name ("Lifestream", "GatherBuddy or GatherBuddy Reborn").</summary>
    public static string CompanionMissingReasonFormat => Loc.Get("CompanionMissingReasonFormat");

    /// <summary>{0} = the plugin's name.</summary>
    public static string CompanionDisabledReasonFormat => Loc.Get("CompanionDisabledReasonFormat");

    /// <summary>{0} = the plugin's name.</summary>
    public static string CompanionOutdatedReasonFormat => Loc.Get("CompanionOutdatedReasonFormat");

    /// <summary>{0} = the plugin's name, {1} = the oldest version Tsukimichi accepts.</summary>
    public static string CompanionOutdatedMinimumReasonFormat => Loc.Get("CompanionOutdatedMinimumReasonFormat");

    /// <summary>{0} and {1} = two builds of one plugin ("GatherBuddy or GatherBuddy Reborn").</summary>
    public static string CompanionEitherFormat => Loc.Get("CompanionEitherFormat");

    // ---- Settings › Integrations › Companion plugins ----
    public static string CompanionsHeading => Loc.Get("CompanionsHeading");
    public static string CompanionsIntro => Loc.Get("CompanionsIntro");
    public static string CompanionsColumnPlugin => Loc.Get("CompanionsColumnPlugin");
    public static string CompanionsColumnUnlocks => Loc.Get("CompanionsColumnUnlocks");
    public static string CompanionsColumnRepository => Loc.Get("CompanionsColumnRepository");
    public static string CompanionStateLoaded => Loc.Get("CompanionStateLoaded");
    public static string CompanionStateDisabled => Loc.Get("CompanionStateDisabled");
    public static string CompanionStateOutdated => Loc.Get("CompanionStateOutdated");
    public static string CompanionStateMissing => Loc.Get("CompanionStateMissing");

    /// <summary>{0} = the installed version.</summary>
    public static string CompanionInstalledVersionFormat => Loc.Get("CompanionInstalledVersionFormat");

    /// <summary>{0} = the oldest version Tsukimichi accepts.</summary>
    public static string CompanionMinimumVersionFormat => Loc.Get("CompanionMinimumVersionFormat");

    public static string CompanionCopyRepo => Loc.Get("CompanionCopyRepo");

    /// <summary>{0} = the repository URL.</summary>
    public static string CompanionCopyRepoTooltipFormat => Loc.Get("CompanionCopyRepoTooltipFormat");

    /// <summary>{0} = the plugin's name.</summary>
    public static string CompanionCopiedFormat => Loc.Get("CompanionCopiedFormat");

    public static string CompanionOfficial => Loc.Get("CompanionOfficial");
    public static string CompanionOfficialTooltip => Loc.Get("CompanionOfficialTooltip");
    public static string CompanionAddRepoHowTo => Loc.Get("CompanionAddRepoHowTo");
    public static string CompanionAutoDutyAllowDutyFinder => Loc.Get("CompanionAutoDutyAllowDutyFinder");
    public static string CompanionAutoDutyAllowDutyFinderHint => Loc.Get("CompanionAutoDutyAllowDutyFinderHint");

    /// <summary>The state's word for the table's glyph tooltip.</summary>
    public static string CompanionStateName(CompanionState state) => state switch
    {
        CompanionState.Loaded => CompanionStateLoaded,
        CompanionState.Disabled => CompanionStateDisabled,
        CompanionState.Outdated => CompanionStateOutdated,
        _ => CompanionStateMissing,
    };

    /// <summary>What the plugin unlocks in Tsukimichi, one line.</summary>
    public static string CompanionUnlocks(CompanionPlugin plugin) => plugin switch
    {
        CompanionPlugin.Questionable => Loc.Get("CompanionUnlocks.Questionable"),
        CompanionPlugin.AutoDuty => Loc.Get("CompanionUnlocks.AutoDuty"),
        CompanionPlugin.Artisan => Loc.Get("CompanionUnlocks.Artisan"),
        CompanionPlugin.GatherBuddy => Loc.Get("CompanionUnlocks.GatherBuddy"),
        CompanionPlugin.Vnavmesh => Loc.Get("CompanionUnlocks.Vnavmesh"),
        CompanionPlugin.Lifestream => Loc.Get("CompanionUnlocks.Lifestream"),
        CompanionPlugin.AllaganTools => Loc.Get("CompanionUnlocks.AllaganTools"),
        CompanionPlugin.QuestMap => Loc.Get("CompanionUnlocks.QuestMap"),
        CompanionPlugin.ChatTwo => Loc.Get("CompanionUnlocks.ChatTwo"),
        CompanionPlugin.BossMod => Loc.Get("CompanionUnlocks.BossMod"),
        CompanionPlugin.RotationPlugin => Loc.Get("CompanionUnlocks.RotationPlugin"),
        _ => string.Empty,
    };

    // ---- Help and the tour ----
    public static string HelpCompanionsTip => Loc.Get("Help.CompanionsTip");
    public static string HelpCompanionsRepoTitle => Loc.Get("Help.CompanionsRepoTitle");

    // ---- Detail pane: Duties and "Run with AutoDuty" ----
    public static string DutiesSection => Loc.Get("DutiesSection");

    /// <summary>Between a duty's relation and its AutoDuty path line.</summary>
    public const string AutoDutyCaptionSeparator = " · ";
    public static string AutoDutyRelationRequired => Loc.Get("AutoDutyRelationRequired");
    public static string AutoDutyRelationUnlocks => Loc.Get("AutoDutyRelationUnlocks");
    public static string AutoDutyHasPath => Loc.Get("AutoDutyHasPath");
    public static string AutoDutyHasPathTooltip => Loc.Get("AutoDutyHasPathTooltip");
    public static string AutoDutyRun => Loc.Get("AutoDutyRun");

    /// <summary>{0} = the queue ("Duty Support", "Trust", "the Duty Finder").</summary>
    public static string AutoDutyRunTooltipFormat => Loc.Get("AutoDutyRunTooltipFormat");

    public static string AutoDutyStop => Loc.Get("AutoDutyStop");
    public static string AutoDutyStopTooltip => Loc.Get("AutoDutyStopTooltip");
    public static string AutoDutyRunning => Loc.Get("AutoDutyRunning");
    public static string AutoDutyModeSupport => Loc.Get("AutoDutyModeSupport");
    public static string AutoDutyModeTrust => Loc.Get("AutoDutyModeTrust");
    public static string AutoDutyModeDutyFinder => Loc.Get("AutoDutyModeDutyFinder");

    /// <summary>{0} = a plugin AutoDuty itself needs ("vnavmesh", "Boss Mod or Boss Mod Reborn").</summary>
    public static string AutoDutyNeedsFormat => Loc.Get("AutoDutyNeedsFormat");

    public static string AutoDutyNotLive => Loc.Get("AutoDutyNotLive");
    public static string AutoDutyBusy => Loc.Get("AutoDutyBusy");
    public static string AutoDutyNoPath => Loc.Get("AutoDutyNoPath");
    public static string AutoDutyLocked => Loc.Get("AutoDutyLocked");
    public static string AutoDutyNeedsDutyFinder => Loc.Get("AutoDutyNeedsDutyFinder");
    public static string AutoDutyNoQueue => Loc.Get("AutoDutyNoQueue");
    public static string AutoDutyRotationNote => Loc.Get("AutoDutyRotationNote");

    /// <summary>{0} = the duty's name.</summary>
    public static string AutoDutyStartedFormat => Loc.Get("AutoDutyStartedFormat");

    public static string AutoDutyModeRefused => Loc.Get("AutoDutyModeRefused");
    public static string AutoDutyNotStarted => Loc.Get("AutoDutyNotStarted");
    public static string AutoDutyUnreachable => Loc.Get("AutoDutyUnreachable");

    /// <summary>The queue's name in the Run tooltip.</summary>
    public static string AutoDutyModeName(AutoDutyMode mode) => mode switch
    {
        AutoDutyMode.Support => AutoDutyModeSupport,
        AutoDutyMode.Trust => AutoDutyModeTrust,
        _ => AutoDutyModeDutyFinder,
    };

    // ---- Quest Map ----
    public static string QuestMapOpen => Loc.Get("QuestMapOpen");
    public static string QuestMapOpenTooltip => Loc.Get("QuestMapOpenTooltip");
    public static string QuestMapNotCharted => Loc.Get("QuestMapNotCharted");
    public static string QuestMapWhyLine => Loc.Get("QuestMapWhyLine");

    /// <summary>{0} = why Quest Map cannot be used ("Needs Quest Map — see Settings › Integrations").</summary>
    public static string QuestMapWhyUnavailableFormat => Loc.Get("QuestMapWhyUnavailableFormat");
}
