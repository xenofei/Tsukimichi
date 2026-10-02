namespace Tsukimichi.Core.Travel;

/// <summary>
/// How Walk to giver and Go to giver move the character (Settings › Integrations › Travel): mount for walks longer
/// than <paramref name="MountDistance"/> yalms (0 never mounts), fly where the zone's flying is unlocked
/// (<paramref name="Fly"/>), and Sprint where mounts are not allowed (<paramref name="SprintInTowns"/>).
/// </summary>
public sealed record TravelOptions(float MountDistance, bool Fly, bool SprintInTowns)
{
    /// <summary>The setting's default: a walk longer than this mounts.</summary>
    public const float DefaultMountDistance = 40f;

    /// <summary>The settings' defaults: mount past <see cref="DefaultMountDistance"/>, fly, sprint in towns.</summary>
    public static readonly TravelOptions Default = new(DefaultMountDistance, true, true);

    /// <summary>On foot only: never mount, fly or sprint (what a plan without options does).</summary>
    public static readonly TravelOptions OnFoot = new(0f, false, false);
}

/// <summary>
/// What the zone and the character allow when a walk is about to start, read once by the plugin.
/// <paramref name="Mounted"/>: on a mount already. <paramref name="MountAllowed"/>: the zone allows mounts and the
/// game would take the mount action now (not in combat, a duty, a sanctuary or water, the action unlocked, the game
/// calls allowed). <paramref name="FlightUnlocked"/>: every aether current of the zone is attuned (the game's own
/// flag) and Tsukimichi can land the mount. <paramref name="NoMountZone"/>: the zone forbids mounts (a town).
/// <paramref name="SprintReady"/>: the game would take Sprint now.
/// </summary>
public readonly record struct TravelMoveContext(bool Mounted, bool MountAllowed, bool FlightUnlocked, bool NoMountZone, bool SprintReady);

/// <summary>What one walk does: mount first, fly (on the mount), or sprint on foot.</summary>
public readonly record struct TravelMove(bool Mount, bool Fly, bool Sprint)
{
    /// <summary>On foot, nothing else.</summary>
    public static readonly TravelMove Walk = default;
}

/// <summary>
/// The GeneralAction sheet rows the travel steps use through the game's action manager (checked against the sheet in
/// tests): Sprint, Mount Roulette, and Dismount, which first brings a flying mount down to land.
/// </summary>
public static class TravelActions
{
    public const uint Sprint = 4;

    public const uint MountRoulette = 9;

    public const uint Dismount = 23;
}

/// <summary>
/// The mount, fly and sprint rules for one walk (feature plan v5, 1.6.0 travel, decision 1). Pure, so tests decide
/// every case without the game.
/// </summary>
public static class TravelMovement
{
    /// <summary>Sprint is not worth it for fewer yalms than this.</summary>
    public const float MinSprintDistance = 15f;

    /// <summary>
    /// Decides how to cover <paramref name="distance"/> yalms. A walk longer than the mount distance mounts when the
    /// zone and the character allow it; one on a mount (just mounted or already) flies when the setting is on, the
    /// zone's flying is unlocked and the walk is long enough to mount for (the default distance when mounting is
    /// off). On foot in a zone without mounts, a walk of <see cref="MinSprintDistance"/> or more sprints when the
    /// setting is on and Sprint is ready.
    /// </summary>
    public static TravelMove Decide(float distance, TravelOptions options, TravelMoveContext context)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (float.IsNaN(distance) || distance < 0f)
        {
            return TravelMove.Walk;
        }

        var longWalk = options.MountDistance > 0f && distance > options.MountDistance;
        var mount = longWalk && !context.Mounted && context.MountAllowed;
        var riding = context.Mounted || mount;
        var flyDistance = options.MountDistance > 0f ? options.MountDistance : TravelOptions.DefaultMountDistance;
        var fly = riding && options.Fly && context.FlightUnlocked && distance > flyDistance;
        var sprint = !riding && options.SprintInTowns && context.NoMountZone && context.SprintReady && distance >= MinSprintDistance;
        return new TravelMove(mount, fly, sprint);
    }
}
