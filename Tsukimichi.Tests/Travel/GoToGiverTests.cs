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

        public float? Height { get; set; } = 0f;

        public bool Walking { get; set; }

        public bool Pathfinding { get; set; }

        public int? Waypoints { get; set; }

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

        public bool Mounted { get; set; }

        public bool InFlight { get; set; }

        public TravelMoveContext MoveContext { get; set; }

        public bool AcceptMount { get; set; } = true;

        public bool AcceptLanding { get; set; } = true;

        public bool StartWalk(GoToGiverPlan plan, bool fly)
        {
            Calls.Add($"{(fly ? "fly" : "walk")} {plan.GoalX},{plan.GoalY},{plan.GoalZ}");
            return AcceptWalk;
        }

        public bool StartMount()
        {
            Calls.Add("mount");
            return AcceptMount;
        }

        public bool StartLanding()
        {
            Calls.Add("land");
            return AcceptLanding;
        }

        public bool StartSprint()
        {
            Calls.Add("sprint");
            return true;
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
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 100_000f, 2f, 3f), 0);

        // Slow but steady progress (a yalm a second), so it is never stuck: only the walk's own deadline ends it.
        GoToGiverOutcome? outcome = null;
        for (var now = 0L; now <= GoToGiver.WalkTimeoutMs + 1_000 && outcome is null; now += 100)
        {
            ports.Position = (now / 1_000f, 0f);
            outcome = machine.Tick(now);
        }

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.WalkTimedOut), outcome);
        Assert.False(machine.Repathed);
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

    // ------------------------------------------------------------------ mounting, flying, landing, stuck

    /// <summary>A walk of 200 yalms in a zone with mounts and flying; the settings' defaults.</summary>
    private static GoToGiverPlan LongWalk(TravelOptions? options = null) =>
        GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f) with { Options = options ?? TravelOptions.Default };

    private static readonly TravelMoveContext Field = new(Mounted: false, MountAllowed: true, FlightUnlocked: true, NoMountZone: false, SprintReady: true);

    [Fact]
    public void A_long_walk_mounts_first_then_flies_and_lands()
    {
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field };
        var machine = new GoToGiver(ports);

        Assert.Null(machine.Start(LongWalk(), 0));
        Assert.Equal(GoToGiverStep.Mounting, machine.Step);
        Assert.True(machine.Move.Mount);
        Assert.True(machine.Move.Fly);
        Assert.Equal(["mount"], ports.Calls);

        // The mount comes: the flight starts.
        Assert.Null(machine.Tick(500));
        ports.Mounted = true;
        Assert.Null(machine.Tick(1_500));
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        Assert.True(machine.Flying);
        Assert.Equal(["mount", "fly 200,5,0"], ports.Calls);

        // vnavmesh flies there and stops in the air beside the giver: land, never dismount.
        ports.Walking = true;
        ports.InFlight = true;
        ports.Height = 40f;
        ports.Position = (100f, 0f);
        Assert.Null(machine.Tick(5_000));
        ports.Position = (198f, 0f);
        ports.Walking = false;
        Assert.Null(machine.Tick(9_000));
        Assert.Equal(GoToGiverStep.Landing, machine.Step);
        Assert.Equal("land", ports.Calls[^1]);

        // Still up and no lower after the retry time: asked again; down: arrived.
        Assert.Null(Run(machine, 9_100, 9_000 + GoToGiver.LandRetryMs - 100));
        Assert.Single(ports.Calls, c => c == "land");
        Assert.Null(machine.Tick(9_000 + GoToGiver.LandRetryMs));
        Assert.Equal(2, ports.Calls.Count(c => c == "land"));
        ports.InFlight = false;
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Done, GoToGiverFailure.None), machine.Tick(12_000));
        Assert.DoesNotContain("stop walk", ports.Calls);
    }

    [Fact]
    public void A_mount_coming_down_is_not_pressed_again()
    {
        // Dismount pressed on the ground would dismount the character: while the mount sinks, it is left be.
        var ports = new FakePorts { Territory = SubZone, Mounted = true, MoveContext = Field with { Mounted = true }, Height = 60f };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);
        ports.Walking = true;
        machine.Tick(100);
        ports.Walking = false;
        ports.InFlight = true;
        ports.Position = (199f, 0f);
        machine.Tick(200);
        Assert.Equal(GoToGiverStep.Landing, machine.Step);
        Assert.Single(ports.Calls, c => c == "land");

        // Sinking a yalm a second for eight seconds: one press is enough, however long the descent takes.
        for (var now = 300L; now <= 8_300; now += 100)
        {
            ports.Height = 60f - ((now - 200) / 1_000f);
            Assert.Null(machine.Tick(now));
        }

        Assert.Single(ports.Calls, c => c == "land");

        // Then hovering at one height: pressed again at the next check, not every frame.
        Assert.Null(Run(machine, 8_400, 10_400));
        Assert.Equal(2, ports.Calls.Count(c => c == "land"));
        ports.InFlight = false;
        Assert.Equal(GoToGiverStep.Done, machine.Tick(10_500)?.Step);
    }

    [Fact]
    public void A_mount_hovering_in_place_is_pressed_again_every_retry()
    {
        var ports = new FakePorts { Territory = SubZone, Mounted = true, MoveContext = Field with { Mounted = true }, Height = 60f };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);
        ports.Walking = true;
        machine.Tick(100);
        ports.Walking = false;
        ports.InFlight = true;
        ports.Position = (199f, 0f);
        machine.Tick(200);

        // Four retries in eight seconds, no more: the press waits for the height check.
        Assert.Null(Run(machine, 300, 200 + (4 * GoToGiver.LandRetryMs)));
        Assert.Equal(5, ports.Calls.Count(c => c == "land"));
    }

    [Fact]
    public void A_mount_that_does_not_come_walks_on_foot()
    {
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);

        // Asked once more after the retry time, then given up at the deadline.
        Assert.Null(Run(machine, 0, GoToGiver.MountRetryMs + 100));
        Assert.Equal(2, ports.Calls.Count(c => c == "mount"));
        Assert.Null(Run(machine, GoToGiver.MountRetryMs + 200, GoToGiver.MountTimeoutMs + 100));
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        Assert.True(machine.MountGaveUp);
        Assert.False(machine.Flying);
        Assert.Equal("walk 200,5,0", ports.Calls[^1]);
    }

    [Fact]
    public void A_refused_mount_walks_on_foot_at_once()
    {
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field, AcceptMount = false };
        var machine = new GoToGiver(ports);

        Assert.Null(machine.Start(LongWalk(), 0));
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        Assert.True(machine.MountGaveUp);
        Assert.Equal(["mount", "walk 200,5,0"], ports.Calls);
    }

    [Fact]
    public void A_short_walk_does_not_mount_and_already_mounted_flies_only_when_long()
    {
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 20f, 0f, 0f) with { Options = TravelOptions.Default }, 0);
        Assert.Equal(["walk 20,0,0"], ports.Calls);
        machine.Cancel();

        // Already on a mount for a long walk: no summons, straight into the air.
        var mounted = new FakePorts { Territory = SubZone, Mounted = true, MoveContext = Field with { Mounted = true } };
        var flying = new GoToGiver(mounted);
        flying.Start(LongWalk(), 0);
        Assert.Equal(["fly 200,5,0"], mounted.Calls);
    }

    [Fact]
    public void Flying_needs_the_mount_under_the_character()
    {
        // The decision said fly, but the mount reads as absent when the walk starts (dismounted meanwhile): on foot.
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);
        Assert.Equal(GoToGiverStep.Mounting, machine.Step);

        Assert.Null(Run(machine, 0, GoToGiver.MountTimeoutMs + 200));
        Assert.Equal(GoToGiverStep.Walking, machine.Step);
        Assert.False(machine.Flying);
        Assert.DoesNotContain(ports.Calls, c => c.StartsWith("fly", StringComparison.Ordinal));
    }

    [Fact]
    public void A_town_walk_sprints_and_the_options_off_walk_plainly()
    {
        var town = new TravelMoveContext(Mounted: false, MountAllowed: false, FlightUnlocked: false, NoMountZone: true, SprintReady: true);
        var ports = new FakePorts { Territory = SubZone, MoveContext = town };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);
        Assert.Equal(["sprint", "walk 200,5,0"], ports.Calls);

        // A plan without options (on foot) never mounts, flies or sprints.
        var plain = new FakePorts { Territory = SubZone, MoveContext = Field };
        new GoToGiver(plain).Start(GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f), 0);
        Assert.Equal(["walk 200,5,0"], plain.Calls);
    }

    [Fact]
    public void A_stuck_walk_gets_one_new_path_then_fails()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f), 0);

        // Some progress, then none.
        ports.Position = (50f, 0f);
        Assert.Null(machine.Tick(1_000));
        Assert.Null(Run(machine, 1_100, 1_000 + GoToGiver.StuckMs));
        Assert.Null(machine.Tick(1_100 + GoToGiver.StuckMs));
        Assert.True(machine.Repathed);
        Assert.Equal(["walk 200,5,0", "stop walk", "walk 200,5,0"], ports.Calls);

        // Progress on the new path keeps it going; stuck again fails and stops the walk.
        ports.Position = (60f, 0f);
        Assert.Null(machine.Tick(2_000 + GoToGiver.StuckMs));
        Assert.Equal(
            new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.Stuck),
            Run(machine, 2_100 + GoToGiver.StuckMs, 4_000 + (2 * GoToGiver.StuckMs)));
        Assert.Equal("stop walk", ports.Calls[^1]);
    }

    [Fact]
    public void A_detour_away_from_the_goal_is_progress()
    {
        // The path runs around a wall: for longer than the stuck time it leads away from the giver.
        var ports = new FakePorts { Territory = SubZone, Walking = true, Position = (100f, 0f) };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f), 0);

        GoToGiverOutcome? outcome = null;
        for (var now = 100L; now <= 3 * GoToGiver.StuckMs && outcome is null; now += 100)
        {
            ports.Position = (100f - (now / 1_000f), now / 500f);
            outcome = machine.Tick(now);
        }

        Assert.Null(outcome);
        Assert.False(machine.Repathed);
        Assert.Equal(["walk 200,5,0"], ports.Calls);
    }

    [Fact]
    public void Fewer_waypoints_are_progress_and_shuffling_in_place_is_not()
    {
        // Shuffling within a yalm (pushed against a corner) while the path shortens: progress.
        var ports = new FakePorts { Territory = SubZone, Walking = true, Waypoints = 40 };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f), 0);
        for (var now = 100L; now <= 2 * GoToGiver.StuckMs; now += 100)
        {
            ports.Position = ((now / 100) % 2 == 0 ? 0.5f : 0f, 0f);
            ports.Waypoints = 40 - (int)(now / 1_000);
            Assert.Null(machine.Tick(now));
        }

        Assert.False(machine.Repathed);

        // The same shuffling with the path as long as before: stuck, a new path.
        var start = (2 * GoToGiver.StuckMs) + 100;
        for (var now = start; now <= start + GoToGiver.StuckMs; now += 100)
        {
            ports.Position = ((now / 100) % 2 == 0 ? 0.5f : 0f, 0f);
            Assert.Null(machine.Tick(now));
        }

        Assert.True(machine.Repathed);
        Assert.Equal(["walk 200,5,0", "stop walk", "walk 200,5,0"], ports.Calls);
    }

    [Fact]
    public void A_longer_path_from_vnavmesh_is_not_progress_but_the_new_count_is_the_baseline()
    {
        var ports = new FakePorts { Territory = SubZone, Walking = true, Waypoints = 10 };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f), 0);

        // vnavmesh replans with more waypoints, standing still: no progress for that.
        ports.Waypoints = 30;
        Assert.Null(Run(machine, 100, GoToGiver.StuckMs - 100));
        Assert.False(machine.Repathed);

        // Then one fewer than the new count: progress.
        ports.Waypoints = 29;
        Assert.Null(machine.Tick(GoToGiver.StuckMs - 50));
        Assert.Null(Run(machine, GoToGiver.StuckMs, (2 * GoToGiver.StuckMs) - 100));
        Assert.False(machine.Repathed);
    }

    [Fact]
    public void A_pathfind_still_running_is_progress_and_never_gets_a_new_path()
    {
        // A long pathfind (a big zone, a slow machine): Walking, nothing moves yet.
        var ports = new FakePorts { Territory = SubZone, Walking = true, Pathfinding = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 200f, 5f, 0f), 0);

        Assert.Null(Run(machine, 100, 3 * GoToGiver.StuckMs));
        Assert.False(machine.Repathed);
        Assert.Equal(["walk 200,5,0"], ports.Calls);

        // The path is found and followed: the stuck time counts from there.
        ports.Pathfinding = false;
        var found = (3 * GoToGiver.StuckMs) + 100;
        Assert.Null(Run(machine, found, found + GoToGiver.StuckMs - 200));
        Assert.False(machine.Repathed);
        Assert.Null(Run(machine, found + GoToGiver.StuckMs - 100, found + GoToGiver.StuckMs + 100));
        Assert.True(machine.Repathed);
    }

    [Fact]
    public void Abandon_ends_the_run_without_touching_vnavmesh_or_lifestream()
    {
        // Questionable started from its own window: the walk is its now.
        var ports = new FakePorts { Territory = SubZone, Walking = true };
        var machine = new GoToGiver(ports);
        machine.Start(GoToGiverPlan.WalkOnly(SubZone, 1f, 2f, 3f), 0);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Cancelled, GoToGiverFailure.None), machine.Abandon());
        Assert.False(machine.IsActive);
        Assert.Equal(["walk 1,2,3"], ports.Calls);
        Assert.Null(machine.Abandon());

        var teleporting = new FakePorts { LifestreamBusy = true };
        var chain = new GoToGiver(teleporting);
        chain.Start(Full, 0);
        chain.Abandon();
        Assert.Equal(["teleport 2"], teleporting.Calls);
    }

    [Fact]
    public void A_mount_that_never_lands_fails()
    {
        var ports = new FakePorts { Territory = SubZone, Mounted = true, MoveContext = Field with { Mounted = true } };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);
        ports.Walking = true;
        machine.Tick(100);
        ports.Walking = false;
        ports.InFlight = true;
        ports.Position = (199f, 0f);
        machine.Tick(200);
        Assert.Equal(GoToGiverStep.Landing, machine.Step);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.LandingFailed), Run(machine, 300, 300 + GoToGiver.LandTimeoutMs + 200));
    }

    [Fact]
    public void Cancel_while_mounting_or_landing_stops_nothing_and_ends_the_run()
    {
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);
        Assert.Equal(GoToGiverStep.Mounting, machine.Step);

        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Cancelled, GoToGiverFailure.None), machine.Cancel());
        Assert.Equal(["mount"], ports.Calls);
        Assert.Null(machine.Tick(1_000));
        Assert.False(machine.IsActive);
    }

    [Fact]
    public void Leaving_the_zone_while_mounting_fails()
    {
        var ports = new FakePorts { Territory = SubZone, MoveContext = Field };
        var machine = new GoToGiver(ports);
        machine.Start(LongWalk(), 0);

        ports.Load(City);
        Assert.Equal(new GoToGiverOutcome(GoToGiverStep.Failed, GoToGiverFailure.LeftZone), machine.Tick(500));
    }
}
