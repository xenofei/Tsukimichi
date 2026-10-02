using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

public sealed class GoToGiverTests
{
    private const uint Home = 130;
    private const uint City = 132;
    private const uint SubZone = 133;
    private const uint Aetheryte = 2;
    private const uint Shard = 28;

    /// <summary>A giver in Old Gridania: teleport to New Gridania, hop to the Conjurers' Guild, walk.</summary>
    private static readonly GoToGiverPlan Full = new(SubZone, -150f, 5f, -15f, new TravelLeg(Aetheryte, City), new TravelLeg(Shard, SubZone), true);

    /// <summary>The game and the plugins as plain fields, with a log of what the machine asked for.</summary>
    private sealed class FakePorts : ITravelPorts
    {
        public uint Territory { get; set; } = Home;

        public (float X, float Z)? Position { get; set; } = (0f, 0f);

        public bool BetweenAreas { get; set; }

        public bool LifestreamBusy { get; set; }

        public bool NavReady { get; set; } = true;

        public bool Walking { get; set; }

        public bool AcceptTeleport { get; set; } = true;

        public bool AcceptHop { get; set; } = true;

        public bool AcceptWalk { get; set; } = true;

        public List<string> Calls { get; } = [];

        public bool StartTeleport(uint aetheryteId)
        {
            Calls.Add($"teleport {aetheryteId}");
            return AcceptTeleport;
        }

        public bool StartHop(uint shardId)
        {
            Calls.Add($"hop {shardId}");
            return AcceptHop;
        }

        public bool StartWalk(GoToGiverPlan plan)
        {
            Calls.Add($"walk {plan.GoalX},{plan.GoalY},{plan.GoalZ}");
            return AcceptWalk;
        }

        public void StopWalk() => Calls.Add("stop walk");

        public void AbortLifestream() => Calls.Add("abort lifestream");

        /// <summary>A teleport or hop: the loading screen, then the new zone with Lifestream idle.</summary>
        public void Load(uint territory)
        {
            BetweenAreas = true;
            Territory = territory;
        }

        public void Arrive()
        {
            BetweenAreas = false;
            LifestreamBusy = false;
        }
    }

    /// <summary>Ticks the machine from <paramref name="from"/> in 100 ms steps until <paramref name="to"/>; returns the first ending.</summary>
    private static GoToGiverOutcome? Run(GoToGiver machine, long from, long to)
    {
        for (var now = from; now <= to; now += 100)
        {
            if (machine.Tick(now) is { } outcome)
            {
                return outcome;
            }
        }

        return null;
    }

    [Fact]
    public void Full_chain_teleports_hops_and_walks_to_the_giver()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);

        Assert.Null(machine.Start(Full, 0));
        Assert.Equal(GoToGiverStep.Teleporting, machine.Step);
        Assert.True(machine.IsActive);

        // The cast, then the loading screen; the arrival waits for the zone to settle.
        Assert.Null(Run(machine, 0, 4_000));
        ports.Load(City);
        Assert.Null(machine.Tick(5_000));
        ports.Arrive();
        Assert.Null(machine.Tick(6_000));
        Assert.Equal(GoToGiverStep.Teleporting, machine.Step);
        Assert.Null(machine.Tick(6_000 + GoToGiver.SettleMs - 100));
        Assert.Null(machine.Tick(6_000 + GoToGiver.SettleMs));
        Assert.Equal(GoToGiverStep.Hopping, machine.Step);

        // The hop: Lifestream busy, the loading screen, the sub-zone.
        ports.LifestreamBusy = true;
        Assert.Null(machine.Tick(8_000));
        ports.Load(SubZone);
        Assert.Null(machine.Tick(9_000));
        ports.Arrive();
        Assert.Null(machine.Tick(10_000));
        Assert.Null(machine.Tick(10_000 + GoToGiver.SettleMs));

        // The navmesh is ready: the walk starts at once.
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        ports.Walking = true;
        Assert.Null(machine.Tick(12_000));
        ports.Walking = false;
        ports.Position = (-148f, -14f);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Done, GoToGiverFailure.None), machine.Tick(30_000));
        Assert.False(machine.IsActive);

        Assert.Equal(["teleport 2", "hop 28", "walk -150,5,-15"], ports.Calls);
    }

    [Fact]
    public void Arrival_waits_for_lifestream_to_go_idle()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full with { Hop = null, Walk = false }, 0);

        ports.Load(City);
        machine.Tick(1_000);
        ports.BetweenAreas = false;
        ports.LifestreamBusy = true;
        Assert.Null(Run(machine, 1_100, 10_000));
        Assert.Equal(GoToGiverStep.Teleporting, machine.Step);

        ports.LifestreamBusy = false;
        Assert.Null(machine.Tick(10_100));
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Done, GoToGiverFailure.None), machine.Tick(10_100 + GoToGiver.SettleMs));
    }

    [Fact]
    public void A_plan_without_a_walk_ends_on_arrival()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(new GoToGiverPlan(SubZone, 0f, 0f, 0f, new TravelLeg(Aetheryte, City), null, false), 0);

        ports.Load(City);
        machine.Tick(1_000);
        ports.Arrive();
        machine.Tick(2_000);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Done, GoToGiverFailure.None), machine.Tick(2_000 + GoToGiver.SettleMs));
        Assert.Equal(["teleport 2"], ports.Calls);
    }

    [Fact]
    public void A_refused_teleport_fails_at_once()
    {
        var ports = new FakePorts { AcceptTeleport = false };
        var machine = new GoToGiver(ports);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.TeleportRefused), machine.Start(Full, 0));
        Assert.False(machine.IsActive);
        Assert.Null(machine.Tick(100));
    }

    [Fact]
    public void A_teleport_without_a_loading_screen_did_not_start()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);

        var outcome = Run(machine, 0, GoToGiver.TeleportDepartMs + 1_000);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.TeleportDidNotStart), outcome);
        Assert.Equal(GoToGiverFailure.TeleportDidNotStart, machine.Failure);
    }

    [Fact]
    public void A_teleport_that_never_lands_in_the_zone_times_out()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);

        // Loaded somewhere else entirely.
        ports.Load(999);
        machine.Tick(1_000);
        ports.Arrive();
        var outcome = Run(machine, 1_100, GoToGiver.ArriveTimeoutMs + 1_000);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.TeleportTimedOut), outcome);
    }

    [Fact]
    public void A_refused_hop_fails_after_the_teleport()
    {
        var ports = new FakePorts { AcceptHop = false };
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);

        ports.Load(City);
        machine.Tick(1_000);
        ports.Arrive();
        machine.Tick(2_000);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.HopRefused), machine.Tick(2_000 + GoToGiver.SettleMs));
    }

    [Fact]
    public void A_hop_that_goes_idle_without_a_loading_screen_did_not_start()
    {
        var ports = new FakePorts { Territory = City };
        var machine = new GoToGiver(ports);
        machine.Start(Full with { Teleport = null }, 0);
        Assert.Equal(GoToGiverStep.Hopping, machine.Step);

        // Lifestream took it, then gave up (no destination, not at the aetheryte).
        ports.LifestreamBusy = true;
        machine.Tick(500);
        ports.LifestreamBusy = false;
        Assert.Null(machine.Tick(1_000));
        Assert.Null(machine.Tick(1_000 + GoToGiver.HopIdleGraceMs));
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.HopDidNotStart), machine.Tick(1_100 + GoToGiver.HopIdleGraceMs));
    }

    [Fact]
    public void A_hop_lifestream_never_starts_did_not_start()
    {
        var ports = new FakePorts { Territory = City };
        var machine = new GoToGiver(ports);
        machine.Start(Full with { Teleport = null }, 0);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.HopDidNotStart), Run(machine, 0, GoToGiver.HopDepartMs + 1_000));
    }

    [Fact]
    public void A_hop_that_never_lands_times_out()
    {
        var ports = new FakePorts { Territory = City };
        var machine = new GoToGiver(ports);
        machine.Start(Full with { Teleport = null }, 0);

        ports.LifestreamBusy = true;
        ports.Load(City);
        machine.Tick(1_000);
        ports.BetweenAreas = false;
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.HopTimedOut), Run(machine, 1_100, GoToGiver.ArriveTimeoutMs + 1_000));
    }

    [Fact]
    public void A_walk_only_plan_outside_the_zone_fails_at_once()
    {
        var ports = new FakePorts { Territory = City };
        var machine = new GoToGiver(ports);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.NotInZone), machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0));
        Assert.Empty(ports.Calls);
    }

    [Fact]
    public void A_walk_waits_for_the_navmesh_and_shows_the_wait()
    {
        var ports = new FakePorts { Territory = SubZone, NavReady = false };
        var machine = new GoToGiver(ports);

        Assert.Null(machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0));
        Assert.Equal(GoToGiverStep.PreparingPath, machine.Step);
        Assert.Null(Run(machine, 0, 10_000));
        Assert.Empty(ports.Calls);

        ports.NavReady = true;
        Assert.Null(machine.Tick(10_100));
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        Assert.Equal(["walk 1,2,3"], ports.Calls);
    }

    [Fact]
    public void A_navmesh_that_never_gets_ready_fails()
    {
        var ports = new FakePorts { Territory = SubZone, NavReady = false };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.PathNotReady), Run(machine, 0, GoToGiver.PathReadyTimeoutMs + 1_000));

        // No walk of this run was asked for: whatever vnavmesh does is another plugin's.
        Assert.DoesNotContain("stop walk", ports.Calls);
    }

    [Fact]
    public void Cancel_while_the_path_is_prepared_leaves_another_plugins_walk_alone()
    {
        var ports = new FakePorts { Territory = SubZone, NavReady = false, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        Assert.Equal(GoToGiverStep.Cancelled, machine.Cancel()?.Step);
        Assert.Empty(ports.Calls);
    }

    [Fact]
    public void Stop_during_the_teleport_cast_lets_it_land_and_does_nothing_after()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);
        Assert.True(machine.TeleportCastPending);

        Assert.Equal(GoToGiverStep.Cancelled, machine.Cancel()?.Step);
        Assert.False(machine.TeleportCastPending);

        // The cast finishes and the player lands where the teleport went; no hop and no walk follow.
        ports.Load(City);
        Assert.Null(machine.Tick(5_000));
        ports.Arrive();
        Assert.Null(Run(machine, 5_100, 20_000));
        Assert.Equal(["teleport 2"], ports.Calls);
    }

    [Fact]
    public void The_cast_is_pending_only_until_the_loading_screen()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);

        ports.Load(City);
        machine.Tick(1_000);
        Assert.Equal(GoToGiverStep.Teleporting, machine.Step);
        Assert.False(machine.TeleportCastPending);
    }

    [Fact]
    public void A_refused_walk_fails_at_once()
    {
        var ports = new FakePorts { Territory = SubZone, AcceptWalk = false };
        var machine = new GoToGiver(ports);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.WalkRefused), machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0));
    }

    [Fact]
    public void A_walk_vnavmesh_never_reports_did_not_start()
    {
        var ports = new FakePorts { Territory = SubZone };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.WalkDidNotStart), Run(machine, 0, GoToGiver.WalkStartTimeoutMs + 1_000));
    }

    [Fact]
    public void A_walk_that_never_ends_times_out_and_stops()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.WalkTimedOut), Run(machine, 0, GoToGiver.WalkTimeoutMs + 1_000));
        Assert.Equal("stop walk", ports.Calls[^1]);
    }

    [Fact]
    public void A_walk_that_ends_far_from_the_giver_stopped_short()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true, Position = (500f, 500f) };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        machine.Tick(1_000);
        ports.Walking = false;
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.WalkStoppedShort), machine.Tick(2_000));
    }

    [Fact]
    public void A_walk_that_ends_by_the_giver_is_done()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true, Position = (4f, 5f) };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        machine.Tick(1_000);
        ports.Walking = false;
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Done, GoToGiverFailure.None), machine.Tick(2_000));
        Assert.DoesNotContain("stop walk", ports.Calls);
    }

    [Fact]
    public void Leaving_the_zone_during_the_walk_stops_it()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);
        machine.Tick(1_000);

        ports.Load(City);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.LeftZone), machine.Tick(2_000));
        Assert.Equal("stop walk", ports.Calls[^1]);
    }

    [Fact]
    public void Leaving_the_zone_while_the_path_is_prepared_fails()
    {
        var ports = new FakePorts { Territory = SubZone, NavReady = false };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        ports.Territory = City;
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.LeftZone), machine.Tick(500));
    }

    [Fact]
    public void Cancel_while_teleporting_aborts_lifestream_when_it_is_busy()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);

        ports.LifestreamBusy = true;
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Cancelled, GoToGiverFailure.None), machine.Cancel());
        Assert.Equal(GoToGiverStep.Cancelled, machine.Step);
        Assert.Equal("abort lifestream", ports.Calls[^1]);

        // A cancelled run no longer ticks.
        ports.Load(City);
        Assert.Null(Run(machine, 100, 5_000));
    }

    [Fact]
    public void Cancel_while_teleporting_leaves_an_idle_lifestream_alone()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);
        machine.Start(Full, 0);

        machine.Cancel();
        Assert.Equal(["teleport 2"], ports.Calls);
    }

    [Fact]
    public void Cancel_while_walking_stops_vnavmesh()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        Assert.Equal(GoToGiverStep.Cancelled, machine.Cancel()?.Step);
        Assert.Equal("stop walk", ports.Calls[^1]);
    }

    [Fact]
    public void Cancel_with_nothing_running_does_nothing()
    {
        var ports = new FakePorts();
        var machine = new GoToGiver(ports);

        Assert.Null(machine.Cancel());
        Assert.Equal(GoToGiverStep.Idle, machine.Step);
        Assert.Null(machine.Tick(0));
        Assert.Empty(ports.Calls);
    }

    [Fact]
    public void Starting_again_cancels_the_run_under_way()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 7f, 8f, 9f), 1_000);
        Assert.Equal(["walk 1,2,3", "stop walk", "walk 7,8,9"], ports.Calls);
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        Assert.Equal(7f, machine.Plan!.GoalX);
    }

    [Fact]
    public void Firmament_hop_uses_its_own_id()
    {
        var ports = new FakePorts { Territory = 418 };
        var machine = new GoToGiver(ports);
        machine.Start(new GoToGiverPlan(886, 0f, 0f, 0f, null, new TravelLeg(GoToGiverPlan.FirmamentHop, 886), false), 0);

        Assert.Equal([$"hop {uint.MaxValue}"], ports.Calls);
    }
}
