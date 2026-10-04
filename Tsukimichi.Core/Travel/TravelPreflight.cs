namespace Tsukimichi.Core.Travel;

/// <summary>One check of the travel preflight (feature plan v7 A9), in the order Setup lists them.</summary>
public enum PreflightItem
{
    /// <summary>The game's movement type (Character Configuration › Control Settings › Movement Type): Standard, not Legacy.</summary>
    MovementType,

    /// <summary>The camera: third person, not first person.</summary>
    Camera,

    /// <summary>vnavmesh's own "movement allowed" switch, which another plugin can turn off.</summary>
    VnavmeshMovement,

    /// <summary>Plugins known to break travel, loaded.</summary>
    Conflicts,
}

/// <summary>Where one preflight check stands.</summary>
public enum PreflightState
{
    /// <summary>Fine for a walk.</summary>
    Ok,

    /// <summary>Can make a walk run the wrong way, or not move at all.</summary>
    Warn,

    /// <summary>Could not be read (logged out, vnavmesh not loaded, game reads off).</summary>
    Unread,
}

/// <summary>The one-click fix a check offers; each runs only from an explicit button.</summary>
public enum PreflightFix
{
    None,

    /// <summary>Set the game's movement type to Standard (with Undo).</summary>
    StandardMovement,

    /// <summary>Turn vnavmesh's "movement allowed" back on (a runtime switch of vnavmesh's, not saved by it; with Undo).</summary>
    AllowVnavmeshMovement,

    /// <summary>Open Dalamud's plugin installer at the installed plugins: Tsukimichi never turns another plugin off itself.</summary>
    OpenPluginInstaller,
}

/// <summary>A plugin known to break travel while loaded: its internal name and the strings key of what it breaks.</summary>
/// <param name="InternalName">The manifest's <c>InternalName</c>, compared ignoring case.</param>
/// <param name="Key">Names the strings that say what it breaks ("wrongwarpfinder").</param>
public sealed record KnownConflict(string InternalName, string Key);

/// <summary>What the preflight read: each value null when it could not be read.</summary>
/// <param name="MoveMode">The game's <c>MoveMode</c> option (UiControl): 0 Standard, 1 Legacy.</param>
/// <param name="FirstPerson">True while the game camera is in first-person view.</param>
/// <param name="VnavmeshMovementAllowed">vnavmesh's <c>Path.GetMovementAllowed</c>.</param>
/// <param name="Conflicts">The known conflicts loaded now (empty when none; never null).</param>
public sealed record TravelPreflightReading(uint? MoveMode, bool? FirstPerson, bool? VnavmeshMovementAllowed, IReadOnlyList<KnownConflict> Conflicts)
{
    /// <summary>Nothing could be read.</summary>
    public static TravelPreflightReading Unread { get; } = new(null, null, null, []);
}

/// <summary>One check and where it stands; <see cref="Conflicts"/> names the loaded conflicts for that row.</summary>
public sealed record PreflightResult(PreflightItem Item, PreflightState State, PreflightFix Fix, IReadOnlyList<KnownConflict> Conflicts);

/// <summary>
/// A game setting the preflight changed, kept for Undo: <see cref="Before"/> is what it was, <see cref="After"/> what
/// the fix set. Undo puts <see cref="Before"/> back only while the setting still reads <see cref="After"/>, so a change
/// the player made since in the game's own window is never overwritten.
/// </summary>
public readonly record struct PreflightChange(PreflightItem Item, uint Before, uint After)
{
    /// <summary>True while Undo would restore <see cref="Before"/>: the setting still reads what the fix set.</summary>
    public bool CanUndo(uint? current) => current == After && Before != After;
}

/// <summary>
/// The travel preflight in Settings › Automation › Travel (feature plan v7 A9): the game settings and plugins that make
/// vnavmesh and Lifestream run the wrong way or not move at all, each with a plain status and, where it is safe, a
/// one-click fix. Pure: the plugin reads the game config, the camera, vnavmesh's gate and Dalamud's plugin list; this
/// decides. Evidence: Legacy movement (awgil/ffxiv_navmesh#87), first-person camera (NightmareXIV/Lifestream#167),
/// WrongWarpFinder (Lifestream#150, "bricks LI until you restart the game").
/// </summary>
public static class TravelPreflight
{
    /// <summary>The game's <c>MoveMode</c> value for Standard movement.</summary>
    public const uint StandardMoveMode = 0;

    /// <summary>The game's <c>MoveMode</c> value for Legacy movement.</summary>
    public const uint LegacyMoveMode = 1;

    /// <summary>Plugins known to break travel while loaded. Add one only with a public report that shows it.</summary>
    public static readonly IReadOnlyList<KnownConflict> KnownConflicts =
    [
        // Lifestream#150 (2026-05-14): "When their plugin is enabled it just bricks LI until you restart the game."
        new("WrongWarpFinder", "wrongwarpfinder"),
    ];

    /// <summary>The known conflicts among the loaded plugins' internal names, in <see cref="KnownConflicts"/> order.</summary>
    public static IReadOnlyList<KnownConflict> ConflictsAmong(IEnumerable<string> loadedInternalNames)
    {
        ArgumentNullException.ThrowIfNull(loadedInternalNames);
        var loaded = new HashSet<string>(loadedInternalNames, StringComparer.OrdinalIgnoreCase);
        return KnownConflicts.Where(c => loaded.Contains(c.InternalName)).ToList();
    }

    /// <summary>Every check, in <see cref="PreflightItem"/> order.</summary>
    public static IReadOnlyList<PreflightResult> Evaluate(TravelPreflightReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return
        [
            new(PreflightItem.MovementType, reading.MoveMode switch
            {
                null => PreflightState.Unread,
                StandardMoveMode => PreflightState.Ok,
                _ => PreflightState.Warn,
            }, reading.MoveMode is { } mode && mode != StandardMoveMode ? PreflightFix.StandardMovement : PreflightFix.None, []),
            new(PreflightItem.Camera, reading.FirstPerson switch
            {
                null => PreflightState.Unread,
                true => PreflightState.Warn,
                false => PreflightState.Ok,
            }, PreflightFix.None, []),
            new(PreflightItem.VnavmeshMovement, reading.VnavmeshMovementAllowed switch
            {
                null => PreflightState.Unread,
                true => PreflightState.Ok,
                false => PreflightState.Warn,
            }, reading.VnavmeshMovementAllowed == false ? PreflightFix.AllowVnavmeshMovement : PreflightFix.None, []),
            new(PreflightItem.Conflicts, reading.Conflicts.Count > 0 ? PreflightState.Warn : PreflightState.Ok, reading.Conflicts.Count > 0 ? PreflightFix.OpenPluginInstaller : PreflightFix.None, reading.Conflicts),
        ];
    }

    /// <summary>How many checks warn.</summary>
    public static int Warnings(IReadOnlyList<PreflightResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Count(static r => r.State == PreflightState.Warn);
    }

    /// <summary>
    /// The checks worth one chat line when Walk or Go to giver starts, in order: those that make the walk itself run
    /// the wrong way or not move (movement type, camera, vnavmesh's switch). A conflict is left to Setup: it breaks
    /// Lifestream, not the walk. Empty when nothing warns.
    /// </summary>
    public static IReadOnlyList<PreflightItem> WalkWarnings(IReadOnlyList<PreflightResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results
            .Where(static r => r.State == PreflightState.Warn && r.Item != PreflightItem.Conflicts)
            .Select(static r => r.Item)
            .ToList();
    }
}
