using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

public sealed class PendingWalkStopTests
{
    [Fact]
    public void Unarmed_it_never_stops_anything()
    {
        var stop = new PendingWalkStop();

        Assert.False(stop.Armed);
        Assert.False(stop.Tick(0, pathfinding: true, running: true));
    }

    [Fact]
    public void A_path_handed_over_after_the_stop_is_stopped_the_frame_it_runs()
    {
        // Stop pressed while vnavmesh still finds the path: nothing to stop yet.
        var stop = new PendingWalkStop();
        stop.Arm(1_000);
        Assert.False(stop.Tick(1_100, pathfinding: true, running: false));
        Assert.False(stop.Tick(4_000, pathfinding: true, running: false));
        Assert.True(stop.Armed);

        // The pathfind ends and its path starts to run: stop it, once, and stop watching.
        Assert.True(stop.Tick(4_100, pathfinding: false, running: true));
        Assert.False(stop.Armed);
        Assert.False(stop.Tick(4_200, pathfinding: false, running: true));
    }

    [Fact]
    public void A_path_running_while_the_pathfind_still_reads_pending_is_stopped_and_watched_on()
    {
        var stop = new PendingWalkStop();
        stop.Arm(0);

        Assert.True(stop.Tick(100, pathfinding: true, running: true));
        Assert.True(stop.Armed);
        Assert.False(stop.Tick(200, pathfinding: false, running: false));
        Assert.False(stop.Armed);
    }

    [Fact]
    public void Nothing_pending_disarms_at_once()
    {
        // A walk that was following its path: vnavmesh's own stop was enough.
        var stop = new PendingWalkStop();
        stop.Arm(0);

        Assert.False(stop.Tick(16, pathfinding: false, running: false));
        Assert.False(stop.Armed);
    }

    [Fact]
    public void A_pathfind_that_never_ends_is_given_up_at_the_deadline()
    {
        var stop = new PendingWalkStop();
        stop.Arm(0);

        Assert.False(stop.Tick(PendingWalkStop.DeadlineMs, pathfinding: true, running: false));
        Assert.True(stop.Armed);
        Assert.False(stop.Tick(PendingWalkStop.DeadlineMs + 1, pathfinding: true, running: true));
        Assert.False(stop.Armed);
    }

    [Fact]
    public void Disarm_leaves_a_new_walk_alone()
    {
        // A new walk vnavmesh took, or Questionable taking the character over.
        var stop = new PendingWalkStop();
        stop.Arm(0);
        stop.Disarm();

        Assert.False(stop.Tick(100, pathfinding: false, running: true));
    }
}
