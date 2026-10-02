namespace Tsukimichi.Core.Companions;

/// <summary>
/// The queue AutoDuty uses for a run: the names of AutoDuty's own <c>DutyMode</c> values (erdelf/AutoDuty
/// <c>AutoDuty/Data/Enums.cs</c>), which its <c>Meta.DutyModeEnum</c> setting takes as text.
/// </summary>
public enum AutoDutyMode
{
    None,

    /// <summary>The Duty Support window: NPC party, solo.</summary>
    Support,

    /// <summary>Trust (Shadowbringers on): NPC party, solo.</summary>
    Trust,

    /// <summary>The regular Duty Finder, a dungeon: other players.</summary>
    Regular,

    /// <summary>The regular Duty Finder, a trial.</summary>
    Trial,

    /// <summary>The regular Duty Finder, a raid.</summary>
    Raid,
}

/// <summary>Why "Run with AutoDuty" is disabled, the first that applies; <see cref="None"/> when it can run.</summary>
public enum AutoDutyBlocker
{
    None,

    /// <summary>AutoDuty is missing, turned off or too old.</summary>
    AutoDutyUnavailable,

    /// <summary>vnavmesh, which AutoDuty walks with, is not loaded.</summary>
    NeedsVnavmesh,

    /// <summary>Neither Boss Mod nor Boss Mod Reborn is loaded.</summary>
    NeedsBossMod,

    /// <summary>The viewed character is a stored one: the run would be for whoever is logged in.</summary>
    NotLive,

    /// <summary>AutoDuty is already running something.</summary>
    Busy,

    /// <summary>AutoDuty has no path for the duty.</summary>
    NoPath,

    /// <summary>The character has not unlocked the duty.</summary>
    Locked,

    /// <summary>Neither Duty Support nor Trust offers it, and the regular Duty Finder is not allowed in Settings.</summary>
    NeedsDutyFinder,

    /// <summary>Neither Duty Support nor Trust offers it and it is no dungeon, trial or raid AutoDuty can queue.</summary>
    NoQueue,
}

/// <summary>What the detail pane knows when it draws "Run with AutoDuty" for one duty.</summary>
/// <param name="AutoDuty">AutoDuty's state in Dalamud's list.</param>
/// <param name="Vnavmesh">vnavmesh's state.</param>
/// <param name="BossMod">Boss Mod's (or Boss Mod Reborn's) state.</param>
/// <param name="Live">The viewed character is the one logged in.</param>
/// <param name="Busy">AutoDuty reports it is not stopped.</param>
/// <param name="HasPath">AutoDuty's <c>ContentHasPath</c> answered true; null when it could not be asked.</param>
/// <param name="Unlocked">The character has the duty unlocked; null when unknown (counted as locked).</param>
/// <param name="AllowDutyFinder">Settings › Integrations › "Allow AutoDuty to queue in the regular Duty Finder".</param>
public readonly record struct AutoDutyInputs(
    CompanionState AutoDuty,
    CompanionState Vnavmesh,
    CompanionState BossMod,
    bool Live,
    bool Busy,
    bool? HasPath,
    bool? Unlocked,
    bool AllowDutyFinder);

/// <summary>The mode AutoDuty would run in and, when the button is disabled, why.</summary>
public readonly record struct AutoDutyChoice(AutoDutyMode Mode, AutoDutyBlocker Blocker)
{
    public bool CanRun => Blocker == AutoDutyBlocker.None;
}

/// <summary>
/// Decision 1's AutoDuty rules, pure so they are tested without the game: Duty Support when the duty offers it, else
/// Trust, else the regular Duty Finder (dungeon, trial or raid by its category) only when Settings allows it.
/// <see cref="Choose"/> then checks, in order, AutoDuty and its own requirements (vnavmesh; Boss Mod or Boss Mod
/// Reborn), the live character, a run already going, a path, the unlock and the queue.
/// </summary>
public static class AutoDutyPlan
{
    /// <summary>AutoDuty's setting the mode goes in, as its <c>PushConfigOverrides</c> names it.</summary>
    public const string DutyModeSetting = "Meta.DutyModeEnum";

    /// <summary>Duty Support, else Trust, else the regular Duty Finder when allowed; <see cref="AutoDutyMode.None"/> when none fits.</summary>
    public static AutoDutyMode ModeFor(DutyRunInfo duty, bool allowDutyFinder)
    {
        ArgumentNullException.ThrowIfNull(duty);
        if (duty.OffersDutySupport)
        {
            return AutoDutyMode.Support;
        }

        if (duty.OffersTrust)
        {
            return AutoDutyMode.Trust;
        }

        return allowDutyFinder ? DutyFinderMode(duty) : AutoDutyMode.None;
    }

    /// <summary>The regular Duty Finder's mode for the duty's category; <see cref="AutoDutyMode.None"/> outside dungeons, trials and raids.</summary>
    public static AutoDutyMode DutyFinderMode(DutyRunInfo duty) => duty.ContentTypeId switch
    {
        DutyRunInfo.Dungeons => AutoDutyMode.Regular,
        DutyRunInfo.Trials => AutoDutyMode.Trial,
        DutyRunInfo.Raids => AutoDutyMode.Raid,
        _ => AutoDutyMode.None,
    };

    /// <summary>The run's mode and the first reason it cannot start.</summary>
    public static AutoDutyChoice Choose(DutyRunInfo duty, in AutoDutyInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(duty);
        var mode = ModeFor(duty, inputs.AllowDutyFinder);
        var blocker = inputs switch
        {
            { AutoDuty: not CompanionState.Loaded } => AutoDutyBlocker.AutoDutyUnavailable,
            { Vnavmesh: not CompanionState.Loaded } => AutoDutyBlocker.NeedsVnavmesh,
            { BossMod: not CompanionState.Loaded } => AutoDutyBlocker.NeedsBossMod,
            { Live: false } => AutoDutyBlocker.NotLive,
            { Busy: true } => AutoDutyBlocker.Busy,
            { HasPath: not true } => AutoDutyBlocker.NoPath,
            { Unlocked: not true } => AutoDutyBlocker.Locked,
            _ when mode != AutoDutyMode.None => AutoDutyBlocker.None,
            _ when DutyFinderMode(duty) != AutoDutyMode.None => AutoDutyBlocker.NeedsDutyFinder,
            _ => AutoDutyBlocker.NoQueue,
        };
        return new AutoDutyChoice(mode, blocker);
    }

    /// <summary>The value AutoDuty's <see cref="DutyModeSetting"/> takes for a mode (its enum name); null for none.</summary>
    public static string? SettingValue(AutoDutyMode mode) => mode == AutoDutyMode.None ? null : mode.ToString();

    /// <summary>AutoDuty's run mode setting; its <c>Run</c> gate sets it to Looping for good, so the override puts the player's back.</summary>
    public const string RunModeSetting = "Meta.AutoDutyModeEnum";

    /// <summary>AutoDuty's loop count; its <c>Run</c> gate would write a loop count for good, so the run passes 0 and overrides it.</summary>
    public const string LoopTimesSetting = "Meta.LoopTimes";

    /// <summary>
    /// The temporary settings a run pushes through <c>AutoDuty.PushConfigOverrides</c>, in the order AutoDuty applies
    /// them: Looping (what <c>Run</c> sets anyway), the queue, one loop. AutoDuty restores every one when it stops, so
    /// the player's own mode, queue and loop count come back after the run. Empty for <see cref="AutoDutyMode.None"/>.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Overrides(AutoDutyMode mode) =>
        SettingValue(mode) is { } value
            ?
            [
                new(RunModeSetting, "Looping"),
                new(DutyModeSetting, value),
                new(LoopTimesSetting, "1"),
            ]
            : [];
}
