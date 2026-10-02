namespace Tsukimichi.Core.Travel;

/// <summary>
/// A Stop that waits for vnavmesh's pathfind (travel review, 1.10). vnavmesh's <c>Path.Stop</c> only clears the
/// waypoints it follows; a <c>SimpleMove.PathfindAndMoveCloseTo</c> still finding its path is a task of its own, which
/// later hands its path to the follower and moves the character after all. So once a walk of Tsukimichi's is stopped
/// (Stop, a failure, logout), this stays armed while that pathfind runs and asks for <c>Path.Stop</c> again as soon as
/// the path starts to run, until the pathfind is over or <see cref="DeadlineMs"/> passes. It is armed only for a walk
/// Tsukimichi asked for, disarmed by a new walk vnavmesh took, and never cancels other plugins' pathfinds
/// (<c>Nav.PathfindCancelAll</c> would). Pure: the plugin ticks it each frame with what vnavmesh reports.
/// </summary>
public sealed class PendingWalkStop
{
    /// <summary>The longest the stop waits for a pathfind to finish.</summary>
    public const long DeadlineMs = 30_000;

    private long? until;

    /// <summary>True while a stopped walk's pathfind may still start moving the character.</summary>
    public bool Armed => until is not null;

    /// <summary>A walk of Tsukimichi's was just stopped: watch its pathfind from <paramref name="now"/>.</summary>
    public void Arm(long now) => until = now + DeadlineMs;

    /// <summary>Nothing more to watch (a new walk was taken, or another plugin now drives the character).</summary>
    public void Disarm() => until = null;

    /// <summary>
    /// One frame: true when <c>Path.Stop</c> must be asked for now (the stopped pathfind's path has started to run).
    /// Disarms once vnavmesh neither finds nor follows a path, after that last stop, or at the deadline.
    /// </summary>
    /// <param name="now">The millisecond clock.</param>
    /// <param name="pathfinding">vnavmesh's <c>SimpleMove.PathfindInProgress</c>.</param>
    /// <param name="running">vnavmesh's <c>Path.IsRunning</c> (waypoints left to follow).</param>
    public bool Tick(long now, bool pathfinding, bool running)
    {
        if (until is not { } deadline)
        {
            return false;
        }

        if (now > deadline)
        {
            until = null;
            return false;
        }

        if (!pathfinding)
        {
            // The pathfind is over: its path runs now (stop it, the last time) or it found none.
            until = null;
            return running;
        }

        // Still finding: a path running meanwhile is the one handed over this frame; stop it and keep watching.
        return running;
    }
}
