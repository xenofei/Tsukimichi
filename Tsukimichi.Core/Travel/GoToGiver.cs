namespace Tsukimichi.Core.Travel;

/// <summary>One travel leg: the Aetheryte sheet row to go to and the territory it lands in.</summary>
public readonly record struct TravelLeg(uint Id, uint TerritoryId);

/// <summary>
/// What one "Go to giver" (or a lone Walk) does, in order: an optional teleport, an optional aethernet hop, then an
/// optional walk to the goal's raw world position (<paramref name="GoalX"/>, <paramref name="GoalY"/> the height,
/// <paramref name="GoalZ"/>) in <paramref name="GoalTerritory"/>. The goal is the giver, or the door of the interior
/// the giver stands in (<see cref="ToEntrance"/>). <see cref="Options"/> say whether the walk may mount, fly or sprint.
/// </summary>
public sealed record GoToGiverPlan(uint GoalTerritory, float GoalX, float GoalY, float GoalZ, TravelLeg? Teleport, TravelLeg? Hop, bool Walk)
{
    /// <summary>The hop id that stands for the Firmament (Lifestream's <c>AethernetTeleportToFirmament</c>), which has no shard row.</summary>
    public const uint FirmamentHop = uint.MaxValue;

    /// <summary>How the walk moves; on foot unless the plugin passes the settings.</summary>
    public TravelOptions Options { get; init; } = TravelOptions.OnFoot;

    /// <summary>True when the walk ends at the door of an interior the giver stands in, not at the giver.</summary>
    public bool ToEntrance { get; init; }

    /// <summary>A walk only: the player is already in the goal's territory.</summary>
    public static GoToGiverPlan WalkOnly(uint goalTerritory, float goalX, float goalY, float goalZ) => new(goalTerritory, goalX, goalY, goalZ, null, null, true);
}

/// <summary>
/// What <see cref="GoToGiver"/> reads and asks of the game and the travel plugins (Lifestream, vnavmesh). The plugin
/// implements it over Dalamud's services, the two IPC wrappers and the game's action manager; tests implement it with
/// plain fields.
/// </summary>
public interface ITravelPorts
{
    /// <summary>The territory the player stands in; 0 when not logged in.</summary>
    uint Territory { get; }

    /// <summary>The player's raw (x, z), or null when there is no player object.</summary>
    (float X, float Z)? Position { get; }

    /// <summary>True during a loading screen (the game's BetweenAreas conditions).</summary>
    bool BetweenAreas { get; }

    /// <summary>True while Lifestream runs a task.</summary>
    bool LifestreamBusy { get; }

    /// <summary>True when vnavmesh has a navmesh for the current territory.</summary>
    bool NavReady { get; }

    /// <summary>The player's height (raw y), or null when there is no player object.</summary>
    float? Height { get; }

    /// <summary>True while vnavmesh follows a path or is still finding one.</summary>
    bool Walking { get; }

    /// <summary>True while vnavmesh is still finding a path (its pathfind task is pending; nothing moves yet).</summary>
    bool Pathfinding { get; }

    /// <summary>How many waypoints vnavmesh's path has left, or null when vnavmesh cannot say.</summary>
    int? Waypoints { get; }

    /// <summary>
    /// How many waypoints vnavmesh's path has left, asked now rather than from a cache, and only while vnavmesh follows a
    /// path (asked now too); null while it follows none or cannot say. Read on each tick of a walk, so the last count
    /// before the walk ended tells a walk stopped by hand (waypoints left) from one that ran to its last waypoint, even
    /// when the end comes between two cached reads.
    /// </summary>
    int? WaypointsNow { get; }

    /// <summary>True while the character is on a mount.</summary>
    bool Mounted { get; }

    /// <summary>True while the mount is in the air.</summary>
    bool InFlight { get; }

    /// <summary>What the zone and the character allow for a walk about to start (read once per walk).</summary>
    TravelMoveContext MoveContext { get; }

    /// <summary>Asks Lifestream to teleport; false when it refused.</summary>
    bool StartTeleport(uint aetheryteId);

    /// <summary>Asks Lifestream for an aethernet hop (<see cref="GoToGiverPlan.FirmamentHop"/> for the Firmament); false when it refused.</summary>
    bool StartHop(uint shardId);

    /// <summary>
    /// Asks vnavmesh to walk (or, <paramref name="fly"/>, fly) close to the world position (<paramref name="x"/>,
    /// <paramref name="y"/> the height, <paramref name="z"/>): the plan's goal, a landing spot beside it, or an
    /// aetheryte before a hop. False when it refused.
    /// </summary>
    bool StartWalk(float x, float y, float z, bool fly);

    /// <summary>Asks the game to mount (the chosen mount or Mount Roulette); false when it refused.</summary>
    bool StartMount();

    /// <summary>Asks the game to land the flying mount; false when it refused.</summary>
    bool StartLanding();

    /// <summary>Asks the game for Sprint; false when it refused.</summary>
    bool StartSprint();

    /// <summary>Stops vnavmesh's movement.</summary>
    void StopWalk();

    /// <summary>Stops Lifestream's running task.</summary>
    void AbortLifestream();

    /// <summary>
    /// The aetheryte or aethernet shard (Aetheryte sheet row) the player stands at as Lifestream sees it, 0 at none;
    /// null when Lifestream cannot say (absent, or a build without <c>GetActiveAetheryte</c>).
    /// </summary>
    uint? ActiveAetheryte { get; }

    /// <summary>
    /// Where an aethernet hop to <paramref name="hopId"/> (<see cref="GoToGiverPlan.FirmamentHop"/> for the Firmament)
    /// leaves from: the nearest attuned aetheryte or shard of that network in the player's zone, as a world position
    /// (y the height); null when none is known.
    /// </summary>
    (float X, float Y, float Z)? HopStart(uint hopId);

    /// <summary>
    /// A floor point near the world position (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) where
    /// a flying mount can come down and the character stand: on vnavmesh's mesh, reachable on foot. Null when vnavmesh
    /// cannot say.
    /// </summary>
    (float X, float Y, float Z)? LandingSpot(float x, float y, float z);

    /// <summary>
    /// Asks vnavmesh to reload the zone's navmesh, the fix its developers prescribe for a walk gone bad (a stale mesh
    /// after a long session, a new seed point). False when it refused or is absent.
    /// </summary>
    bool StartNavReload();
}

/// <summary>Where a <see cref="GoToGiver"/> run stands.</summary>
public enum GoToGiverStep
{
    Idle,
    Teleporting,
    Hopping,
    PreparingPath,
    Walking,
    Done,
    Cancelled,
    Failed,

    /// <summary>The mount is being summoned before a long walk.</summary>
    Mounting,

    /// <summary>The flying mount is coming down near the goal.</summary>
    Landing,

    /// <summary>A walk went bad: vnavmesh reloads the zone's navmesh before the walk is tried once more.</summary>
    ReloadingNav,

    /// <summary>Walking to the nearest aetheryte or shard first: Lifestream hops only from within one's range.</summary>
    ToAetheryte,
}

/// <summary>Why a <see cref="GoToGiver"/> run failed.</summary>
public enum GoToGiverFailure
{
    None,

    /// <summary>A walk was asked for outside the goal's territory, with no teleport to get there.</summary>
    NotInZone,
    TeleportRefused,

    /// <summary>No loading screen followed the teleport request (an interrupted cast).</summary>
    TeleportDidNotStart,
    TeleportTimedOut,
    HopRefused,

    /// <summary>Lifestream took the hop but no loading screen followed (not at an aetheryte, or no such destination).</summary>
    HopDidNotStart,
    HopTimedOut,

    /// <summary>vnavmesh had no navmesh for the zone in time.</summary>
    PathNotReady,
    WalkRefused,
    WalkDidNotStart,
    WalkTimedOut,

    /// <summary>The walk ended farther from the goal than <see cref="GoToGiver.ArriveRange"/> (no path, or stopped by hand).</summary>
    WalkStoppedShort,

    /// <summary>The player left the goal's territory during the walk.</summary>
    LeftZone,

    /// <summary>The walk made no progress for <see cref="GoToGiver.StuckMs"/>, twice (one new path in between).</summary>
    Stuck,

    /// <summary>The flying mount did not come down within <see cref="GoToGiver.LandTimeoutMs"/>.</summary>
    LandingFailed,
}

/// <summary>How a run ended: <see cref="GoToGiverStep.Done"/>, <see cref="GoToGiverStep.Cancelled"/> or <see cref="GoToGiverStep.Failed"/> with its reason.</summary>
public readonly record struct GoToGiverOutcome(GoToGiverStep Step, GoToGiverFailure Failure);

/// <summary>
/// The "Go to giver" chain (feature plan v5, 1.6.0, decision 1): teleport, wait for the arrival, an optional aethernet
/// hop, then a vnavmesh walk to the goal, mounting first for a long walk and flying where the zone allows it
/// (<see cref="TravelMovement"/>), landing at the end. Pure: the plugin calls <see cref="Tick"/> from the framework
/// update with a millisecond clock and the machine reads and acts through <see cref="ITravelPorts"/>. Every step can
/// be cancelled (<see cref="Cancel"/>, one Stop) and every wait has a deadline, so a run always ends; the ending is
/// returned once from the call that reached it, for the plugin's chat line. A mount that does not come is not a
/// failure: the walk goes on foot. A walk that stops making progress gets one new path, then fails as stuck: progress
/// is the character's own movement or vnavmesh's path getting shorter, not the straight line to the goal (a path
/// around a wall moves away from it for a while), and a pathfind still running is progress too (no new path is asked
/// for while one is pending). The character is never dismounted: Dismount is pressed once in the air and again only
/// when the mount has not come down at all.
/// <para>
/// Recovery (feature plan v7 A8), each at most once per run so a run never loops: a walk that got stuck, ended short
/// of the goal on its own, never started or waited too long for a navmesh has vnavmesh reload the zone's navmesh
/// (<see cref="GoToGiverStep.ReloadingNav"/>) and is tried once more from where the character stands; a walk the
/// player stopped by hand (vnavmesh's path still had waypoints left) is not retried. Before an aethernet hop, a player
/// Lifestream says stands at no aetheryte walks to the nearest one of the network first
/// (<see cref="GoToGiverStep.ToAetheryte"/>), and a hop that never started is asked for once more. A flight aims for a
/// landing spot on the floor beside the goal (<see cref="ITravelPorts.LandingSpot"/>), and a landing farther than
/// <see cref="TalkRange"/> from the goal closes the gap on foot, still mounted.
/// </para>
/// </summary>
public sealed class GoToGiver
{
    /// <summary>A teleport cast is five seconds; with no loading screen by then it was interrupted.</summary>
    public const long TeleportDepartMs = 15_000;

    /// <summary>Lifestream may first walk a few steps to the aetheryte before an aethernet hop.</summary>
    public const long HopDepartMs = 30_000;

    /// <summary>After Lifestream went idle without a loading screen, the hop is taken as not started.</summary>
    public const long HopIdleGraceMs = 3_000;

    /// <summary>
    /// The pause before a hop that never started is asked for again: Lifestream checks whether it can teleport once,
    /// and right after a zone change or a dismount it cannot yet (Lifestream#163).
    /// </summary>
    public const long HopRetryMs = 1_000;

    /// <summary>The longest a teleport or hop may take from the request to standing in the new zone.</summary>
    public const long ArriveTimeoutMs = 90_000;

    /// <summary>How long the player must stand in the new zone, idle, before the next step: the zone finishes loading.</summary>
    public const long SettleMs = 1_500;

    /// <summary>The longest vnavmesh may take to have a navmesh for the zone (a first build is slow).</summary>
    public const long PathReadyTimeoutMs = 120_000;

    /// <summary>
    /// After a reload is asked for, how long the old "ready" answer is not trusted: vnavmesh drops the mesh at once and
    /// loads it again from its cache, while its answer is cached for a moment on this side.
    /// </summary>
    public const long ReloadSettleMs = 2_000;

    /// <summary>vnavmesh reports pathfinding at once; without it by then the walk did not start.</summary>
    public const long WalkStartTimeoutMs = 5_000;

    /// <summary>The longest one walk may take.</summary>
    public const long WalkTimeoutMs = 300_000;

    /// <summary>Distance (raw units) within which a finished walk counts as arrived: the walk stops about 3 away.</summary>
    public const float ArriveRange = 8f;

    /// <summary>
    /// Distance (raw units, about yalms) from the goal within which the character can talk to the giver; a landing
    /// farther away closes the gap on foot.
    /// </summary>
    public const float TalkRange = 4.5f;

    /// <summary>Distance (raw units) from the hop's aetheryte or shard within which no walk to it is asked for.</summary>
    public const float AetheryteReach = 4f;

    /// <summary>The mount cast is about a second; without the mount by then it is asked for once more.</summary>
    public const long MountRetryMs = 3_000;

    /// <summary>Without a mount by then, the walk goes on foot.</summary>
    public const long MountTimeoutMs = 8_000;

    /// <summary>A walk that makes no progress (<see cref="StuckMove"/>, fewer waypoints, a pathfind) in this long is stuck.</summary>
    public const long StuckMs = 15_000;

    /// <summary>How far (raw units) the character must move from where it last made progress to count as progress.</summary>
    public const float StuckMove = 5f;

    /// <summary>How long after a Dismount press the height is checked: dropped, the mount is coming down; else pressed again.</summary>
    public const long LandRetryMs = 2_000;

    /// <summary>How much lower (raw units) the character must be to count as coming down.</summary>
    public const float LandDrop = 1f;

    /// <summary>The longest the mount may take to come down.</summary>
    public const long LandTimeoutMs = 15_000;

    private readonly ITravelPorts ports;
    private long stepStarted;
    private long? arrivedAt;
    private long? idleSince;
    private bool sawLoading;
    private bool sawBusy;
    private bool sawWalking;
    private long lastPress;
    private bool pressedAgain;
    private (float X, float Y, float Z)? progressFrom;
    private int? progressWaypoints;
    private long progressAt;
    private float? landHeight;
    private bool repathed;

    /// <summary>Which walk is under way: to the goal, to the aetheryte before a hop, or the last yalms after a landing.</summary>
    private WalkLeg leg;
    private (float X, float Y, float Z) walkTarget;
    private uint hopFromTerritory;
    private long? hopAt;

    /// <summary>The earliest a hop asked again after one that never started may go (Lifestream#163).</summary>
    private long? hopRetryAt;
    private int? lastWaypoints;

    public GoToGiver(ITravelPorts ports)
    {
        this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
    }

    private enum WalkLeg
    {
        Goal,
        Aetheryte,
        Approach,
    }

    public GoToGiverStep Step { get; private set; } = GoToGiverStep.Idle;

    public GoToGiverFailure Failure { get; private set; } = GoToGiverFailure.None;

    /// <summary>The plan of the current or last run; null before the first.</summary>
    public GoToGiverPlan? Plan { get; private set; }

    /// <summary>How the current or last walk moves (decided when the path is ready); <see cref="TravelMove.Walk"/> before.</summary>
    public TravelMove Move { get; private set; }

    /// <summary>True while the current walk flies.</summary>
    public bool Flying { get; private set; }

    /// <summary>True when the run asked for a mount that did not come, so the walk went on foot.</summary>
    public bool MountGaveUp { get; private set; }

    /// <summary>True once the walk was given a new path after it stopped making progress.</summary>
    public bool Repathed => repathed;

    /// <summary>
    /// True once vnavmesh took the run's navmesh reload and the walk was tried again (once per run): only then may a
    /// failure say "even after reloading". A reload vnavmesh declined leaves it false.
    /// </summary>
    public bool Reloaded { get; private set; }

    /// <summary>True once the run used its one recovery, taken or declined, so a run never tries a second.</summary>
    public bool RecoverySpent { get; private set; }

    /// <summary>What the reload under way (or the last one) recovers from; <see cref="GoToGiverFailure.None"/> before one.</summary>
    public GoToGiverFailure RecoveringFrom { get; private set; }

    /// <summary>True once the run walked to an aetheryte before its hop (once per run).</summary>
    public bool WalkedToAetheryte { get; private set; }

    /// <summary>True once the run asked for its hop a second time.</summary>
    public bool HopRetried { get; private set; }

    /// <summary>True once the run closed the gap to the goal on foot after landing.</summary>
    public bool Approached { get; private set; }

    /// <summary>True from <see cref="Start"/> until the run is done, cancelled or failed.</summary>
    public bool IsActive => Step is GoToGiverStep.Teleporting or GoToGiverStep.Hopping or GoToGiverStep.PreparingPath
        or GoToGiverStep.Mounting or GoToGiverStep.Walking or GoToGiverStep.Landing or GoToGiverStep.ReloadingNav
        or GoToGiverStep.ToAetheryte;

    /// <summary>True while a walk this run asked vnavmesh for may be moving the character.</summary>
    private bool WalkUnderWay => Step is GoToGiverStep.Walking or GoToGiverStep.ToAetheryte;

    /// <summary>
    /// Starts a run (cancelling one in progress) and takes its first step now. Returns the ending when the run ends
    /// at once (a refused request, nothing to do), else null.
    /// </summary>
    public GoToGiverOutcome? Start(GoToGiverPlan plan, long now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (IsActive)
        {
            Cancel();
        }

        Plan = plan;
        Failure = GoToGiverFailure.None;
        Move = TravelMove.Walk;
        Flying = false;
        MountGaveUp = false;
        repathed = false;
        Reloaded = false;
        RecoverySpent = false;
        RecoveringFrom = GoToGiverFailure.None;
        WalkedToAetheryte = false;
        HopRetried = false;
        Approached = false;
        leg = WalkLeg.Goal;
        walkTarget = (plan.GoalX, plan.GoalY, plan.GoalZ);
        hopAt = null;
        hopRetryAt = null;
        lastWaypoints = null;
        if (plan.Teleport is { } teleport)
        {
            return BeginTeleport(teleport, now);
        }

        return AfterTeleport(plan, now);
    }

    /// <summary>
    /// True while the teleport is asked for and no loading screen has followed yet: the cast is under way. A Stop then
    /// cannot cut the cast short (Lifestream's teleport is no task it could abort), so the teleport lands and the run
    /// does nothing more.
    /// </summary>
    public bool TeleportCastPending => Step == GoToGiverStep.Teleporting && !sawLoading;

    /// <summary>
    /// Stops the run: the walk this run started is stopped and Lifestream's task aborted. Nothing else is touched: while
    /// the path is still being prepared vnavmesh has no walk of this run's, so a walk another plugin started goes on;
    /// a mount being summoned or landing finishes on its own, and so does a navmesh reload. The ending, or null when
    /// nothing ran. A cancelled run never ticks again, so a teleport cast that was under way lands without a hop or a
    /// walk after it.
    /// </summary>
    public GoToGiverOutcome? Cancel()
    {
        if (!IsActive)
        {
            return null;
        }

        if (WalkUnderWay)
        {
            ports.StopWalk();
        }
        else if (Step is GoToGiverStep.Teleporting or GoToGiverStep.Hopping && ports.LifestreamBusy)
        {
            ports.AbortLifestream();
        }

        Step = GoToGiverStep.Cancelled;
        return new GoToGiverOutcome(Step, GoToGiverFailure.None);
    }

    /// <summary>
    /// Ends the run without touching vnavmesh or Lifestream: another plugin (Questionable, AutoDuty) took the character
    /// over and now moves it through vnavmesh itself, so stopping "our" walk would stop theirs. The ending, or null when
    /// nothing ran.
    /// </summary>
    public GoToGiverOutcome? Abandon()
    {
        if (!IsActive)
        {
            return null;
        }

        Step = GoToGiverStep.Cancelled;
        return new GoToGiverOutcome(Step, GoToGiverFailure.None);
    }

    /// <summary>One framework tick: reads the ports and moves on. Returns the ending on the tick that reached it.</summary>
    public GoToGiverOutcome? Tick(long now)
    {
        if (!IsActive || Plan is not { } plan)
        {
            return null;
        }

        return Step switch
        {
            GoToGiverStep.Teleporting => TickTeleport(plan, now),
            GoToGiverStep.Hopping => TickHop(plan, now),
            GoToGiverStep.PreparingPath => TickPreparing(plan, now),
            GoToGiverStep.Mounting => TickMounting(plan, now),
            GoToGiverStep.Walking or GoToGiverStep.ToAetheryte => TickWalking(plan, now),
            GoToGiverStep.Landing => TickLanding(plan, now),
            GoToGiverStep.ReloadingNav => TickReloading(plan, now),
            _ => null,
        };
    }

    private GoToGiverOutcome? BeginTeleport(TravelLeg teleport, long now)
    {
        if (!ports.StartTeleport(teleport.Id))
        {
            return Fail(GoToGiverFailure.TeleportRefused);
        }

        Enter(GoToGiverStep.Teleporting, now);
        return null;
    }

    private GoToGiverOutcome? AfterTeleport(GoToGiverPlan plan, long now)
    {
        if (plan.Hop is { } hop)
        {
            return BeginHop(plan, hop, now);
        }

        return AfterHop(plan, now);
    }

    /// <summary>
    /// The hop, or first the walk to the network's nearest aetheryte or shard when Lifestream says the player stands at
    /// none (a teleport that lands outside the aetheryte's range, a player elsewhere in the city). The walk is taken once
    /// per run; a Lifestream that cannot say leaves the hop to it, as before.
    /// </summary>
    private GoToGiverOutcome? BeginHop(GoToGiverPlan plan, TravelLeg hop, long now)
    {
        if (!WalkedToAetheryte && ports.ActiveAetheryte == 0 && ports.HopStart(hop.Id) is { } start
            && ports.Position is { } at && TravelPlanner.Distance(at.X, at.Z, start.X, start.Z) > AetheryteReach)
        {
            WalkedToAetheryte = true;
            leg = WalkLeg.Aetheryte;
            walkTarget = start;
            hopFromTerritory = ports.Territory;
            Enter(GoToGiverStep.PreparingPath, now);
            return TickPreparing(plan, now);
        }

        return AskHop(hop, now);
    }

    private GoToGiverOutcome? AskHop(TravelLeg hop, long now)
    {
        if (!ports.StartHop(hop.Id))
        {
            return Fail(GoToGiverFailure.HopRefused);
        }

        Enter(GoToGiverStep.Hopping, now);
        return null;
    }

    /// <summary>
    /// The walk to the aetheryte is over, however it went: the hop is asked for now, and Lifestream decides. A hop asked
    /// again after one that never started still waits out <see cref="HopRetryMs"/> from then (Lifestream#163), however
    /// short the walk was (a refused one takes no time at all).
    /// </summary>
    private GoToGiverOutcome? AfterAetheryteWalk(GoToGiverPlan plan, long now)
    {
        if (Step == GoToGiverStep.ToAetheryte && ports.Walking)
        {
            ports.StopWalk();
        }

        leg = WalkLeg.Goal;
        walkTarget = (plan.GoalX, plan.GoalY, plan.GoalZ);
        repathed = false;
        if (hopRetryAt is { } at && now < at)
        {
            Enter(GoToGiverStep.Hopping, now);
            hopAt = at;
            return null;
        }

        return AskHop(plan.Hop!.Value, now);
    }

    private GoToGiverOutcome? AfterHop(GoToGiverPlan plan, long now)
    {
        if (!plan.Walk)
        {
            return Finish();
        }

        if (ports.Territory != plan.GoalTerritory)
        {
            return Fail(GoToGiverFailure.NotInZone);
        }

        Enter(GoToGiverStep.PreparingPath, now);
        return TickPreparing(plan, now);
    }

    private GoToGiverOutcome? TickTeleport(GoToGiverPlan plan, long now)
    {
        var teleport = plan.Teleport!.Value;
        if (ports.BetweenAreas)
        {
            sawLoading = true;
            arrivedAt = null;
            return null;
        }

        if (!sawLoading)
        {
            return now - stepStarted > TeleportDepartMs ? Fail(GoToGiverFailure.TeleportDidNotStart) : null;
        }

        if (Settled(teleport.TerritoryId, now))
        {
            return AfterTeleport(plan, now);
        }

        return now - stepStarted > ArriveTimeoutMs ? Fail(GoToGiverFailure.TeleportTimedOut) : null;
    }

    private GoToGiverOutcome? TickHop(GoToGiverPlan plan, long now)
    {
        var hop = plan.Hop!.Value;
        if (hopAt is { } askAt)
        {
            // The pause before the hop is asked for again.
            if (now < askAt)
            {
                return null;
            }

            hopAt = null;
            if (!ports.StartHop(hop.Id))
            {
                return Fail(GoToGiverFailure.HopRefused);
            }

            stepStarted = now;
            return null;
        }

        var busy = ports.LifestreamBusy;
        if (busy)
        {
            sawBusy = true;
            idleSince = null;
        }

        if (ports.BetweenAreas)
        {
            sawLoading = true;
            arrivedAt = null;
            return null;
        }

        if (!sawLoading)
        {
            if (sawBusy && !busy)
            {
                idleSince ??= now;
                if (now - idleSince.Value > HopIdleGraceMs)
                {
                    return HopNotStarted(plan, hop, now);
                }
            }

            return now - stepStarted > HopDepartMs ? HopNotStarted(plan, hop, now) : null;
        }

        if (Settled(hop.TerritoryId, now))
        {
            return AfterHop(plan, now);
        }

        return now - stepStarted > ArriveTimeoutMs ? Fail(GoToGiverFailure.HopTimedOut) : null;
    }

    /// <summary>
    /// A hop that never started gets one more try: after the walk to the aetheryte when the player stands at none and
    /// has not walked there yet, else after <see cref="HopRetryMs"/>. A second one that never starts fails.
    /// </summary>
    private GoToGiverOutcome? HopNotStarted(GoToGiverPlan plan, TravelLeg hop, long now)
    {
        if (HopRetried)
        {
            return Fail(GoToGiverFailure.HopDidNotStart);
        }

        HopRetried = true;
        hopRetryAt = now + HopRetryMs;
        if (!WalkedToAetheryte && ports.ActiveAetheryte == 0 && ports.HopStart(hop.Id) is not null)
        {
            return BeginHop(plan, hop, now);
        }

        Enter(GoToGiverStep.Hopping, now);
        hopAt = hopRetryAt;
        return null;
    }

    /// <summary>True once the player has stood in <paramref name="territory"/>, out of loading and with Lifestream idle, for <see cref="SettleMs"/>.</summary>
    private bool Settled(uint territory, long now)
    {
        if (ports.Territory != territory || ports.LifestreamBusy)
        {
            arrivedAt = null;
            return false;
        }

        arrivedAt ??= now;
        return now - arrivedAt.Value >= SettleMs;
    }

    /// <summary>True while the player is still in the zone of the walk under way (the goal's, or the hop's start) and out of loading.</summary>
    private bool InWalkZone(GoToGiverPlan plan) =>
        ports.Territory == (leg == WalkLeg.Aetheryte ? hopFromTerritory : plan.GoalTerritory) && !ports.BetweenAreas;

    /// <summary>Distance on the ground plane from the player to the goal; NaN without a player position.</summary>
    private float GoalDistance(GoToGiverPlan plan) =>
        ports.Position is { } at ? TravelPlanner.Distance(at.X, at.Z, plan.GoalX, plan.GoalZ) : float.NaN;

    private GoToGiverOutcome? TickPreparing(GoToGiverPlan plan, long now)
    {
        if (!InWalkZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (!ports.NavReady)
        {
            if (now - stepStarted <= PathReadyTimeoutMs)
            {
                return null;
            }

            return leg == WalkLeg.Aetheryte ? AfterAetheryteWalk(plan, now) : Recover(GoToGiverFailure.PathNotReady, now);
        }

        if (leg != WalkLeg.Goal)
        {
            // The few steps to the aetheryte, or the last yalms after a landing: plainly on foot (or on the landed mount).
            Move = TravelMove.Walk;
            return BeginWalk(plan, now);
        }

        // The path is ready: decide how to cover the distance, then mount first when the walk is long.
        var context = ports.MoveContext;
        Move = TravelMovement.Decide(GoalDistance(plan), plan.Options, context);
        if (Move.Mount)
        {
            if (ports.StartMount())
            {
                Enter(GoToGiverStep.Mounting, now);
                lastPress = now;
                return null;
            }

            GiveUpMount(context);
        }

        return BeginWalk(plan, now);
    }

    /// <summary>The mount did not come: the walk goes on foot (a flight needs the mount; Sprint may still help).</summary>
    private void GiveUpMount(TravelMoveContext context)
    {
        MountGaveUp = true;
        var onFoot = TravelMovement.Decide(GoalDistance(Plan!), Plan!.Options with { MountDistance = 0f }, context with { Mounted = false, MountAllowed = false });
        Move = onFoot with { Mount = false, Fly = false };
    }

    private GoToGiverOutcome? TickMounting(GoToGiverPlan plan, long now)
    {
        if (!InWalkZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (ports.Mounted)
        {
            return BeginWalk(plan, now);
        }

        if (now - stepStarted > MountTimeoutMs)
        {
            GiveUpMount(ports.MoveContext);
            return BeginWalk(plan, now);
        }

        if (!pressedAgain && now - lastPress >= MountRetryMs)
        {
            pressedAgain = true;
            lastPress = now;
            ports.StartMount();
        }

        return null;
    }

    private GoToGiverOutcome? BeginWalk(GoToGiverPlan plan, long now)
    {
        // A flight needs the mount under the character: vnavmesh takes off from a mount, never on foot.
        Flying = leg == WalkLeg.Goal && Move.Fly && ports.Mounted;
        if (!Flying && !ports.Mounted && Move.Sprint)
        {
            ports.StartSprint();
        }

        if (leg == WalkLeg.Goal)
        {
            // A flight aims for a spot on the floor beside the goal, where the mount can come down and the character
            // stand and talk, rather than for the air over the giver's head.
            walkTarget = Flying && ports.LandingSpot(plan.GoalX, plan.GoalY, plan.GoalZ) is { } spot ? spot : (plan.GoalX, plan.GoalY, plan.GoalZ);
        }

        if (!ports.StartWalk(walkTarget.X, walkTarget.Y, walkTarget.Z, Flying))
        {
            return leg == WalkLeg.Aetheryte ? AfterAetheryteWalk(plan, now) : Fail(GoToGiverFailure.WalkRefused);
        }

        Enter(leg == WalkLeg.Aetheryte ? GoToGiverStep.ToAetheryte : GoToGiverStep.Walking, now);
        lastWaypoints = null;
        MarkProgress(now);
        return null;
    }

    /// <summary>Where the character stands now, height included; null without a player.</summary>
    private (float X, float Y, float Z)? Here() =>
        ports.Position is { } at ? (at.X, ports.Height ?? 0f, at.Z) : null;

    /// <summary>The walk made progress now: the next is measured from here and from this many waypoints.</summary>
    private void MarkProgress(long now)
    {
        progressFrom = Here();
        progressWaypoints = ports.Waypoints;
        progressAt = now;
    }

    private GoToGiverOutcome? TickWalking(GoToGiverPlan plan, long now)
    {
        if (!InWalkZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (ports.Walking)
        {
            sawWalking = true;

            // Asked now, not cached: the cached count can be a quarter second old when the walk ends, and two waypoints
            // left then would read a walk that ran to its end as one stopped by hand.
            if (ports.WaypointsNow is { } left)
            {
                lastWaypoints = left;
            }

            if (now - stepStarted > WalkTimeoutMs)
            {
                return leg == WalkLeg.Aetheryte ? AfterAetheryteWalk(plan, now) : Fail(GoToGiverFailure.WalkTimedOut);
            }

            return CheckProgress(plan, now);
        }

        if (!sawWalking)
        {
            if (now - stepStarted <= WalkStartTimeoutMs)
            {
                return null;
            }

            return leg == WalkLeg.Aetheryte ? AfterAetheryteWalk(plan, now) : Recover(GoToGiverFailure.WalkDidNotStart, now);
        }

        if (leg == WalkLeg.Aetheryte)
        {
            return AfterAetheryteWalk(plan, now);
        }

        // The walk ended: still in the air, come down first; then arrived, or stopped short (no path, stopped by hand).
        if (ports.InFlight)
        {
            ports.StartLanding();
            Enter(GoToGiverStep.Landing, now);
            lastPress = now;
            landHeight = ports.Height;
            return null;
        }

        return Flying ? AfterLanding(plan, now) : Arrive(plan, now);
    }

    /// <summary>
    /// Stuck detection while vnavmesh walks. Progress is the character moving <see cref="StuckMove"/> from where it last
    /// made progress (height included: a take-off counts), vnavmesh's path having fewer waypoints left, or a pathfind
    /// still running; the straight line to the goal plays no part, as a path around an obstacle moves away from it. No
    /// progress for <see cref="StuckMs"/> gets one new path (never while a pathfind is pending: vnavmesh would refuse
    /// it, and the pending one would still move the character); stuck again reloads the navmesh once
    /// (<see cref="Recover"/>), and stuck after that fails. A walk to the aetheryte that sticks goes on to the hop.
    /// </summary>
    private GoToGiverOutcome? CheckProgress(GoToGiverPlan plan, long now)
    {
        if (ports.Pathfinding || Here() is not { } here)
        {
            MarkProgress(now);
            return null;
        }

        var waypoints = ports.Waypoints;
        var moved = progressFrom is not { } from || Moved(from, here) >= StuckMove;
        var shorter = waypoints is { } left && progressWaypoints is { } before && left < before;
        if (moved || shorter)
        {
            MarkProgress(now);
            return null;
        }

        if (waypoints is { } count && (progressWaypoints is not { } known || count > known))
        {
            // vnavmesh planned anew (more waypoints than before): measure from the new path, without counting it as progress.
            progressWaypoints = count;
        }

        if (now - progressAt < StuckMs)
        {
            return null;
        }

        if (repathed)
        {
            return leg == WalkLeg.Aetheryte ? AfterAetheryteWalk(plan, now) : Recover(GoToGiverFailure.Stuck, now);
        }

        repathed = true;
        ports.StopWalk();
        if (!ports.StartWalk(walkTarget.X, walkTarget.Y, walkTarget.Z, Flying))
        {
            return leg == WalkLeg.Aetheryte ? AfterAetheryteWalk(plan, now) : Fail(GoToGiverFailure.WalkRefused);
        }

        Enter(Step, now);
        MarkProgress(now);
        return null;
    }

    private static float Moved((float X, float Y, float Z) from, (float X, float Y, float Z) to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private GoToGiverOutcome? TickLanding(GoToGiverPlan plan, long now)
    {
        if (!InWalkZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (!ports.InFlight)
        {
            return AfterLanding(plan, now);
        }

        if (now - stepStarted > LandTimeoutMs)
        {
            return Fail(GoToGiverFailure.LandingFailed);
        }

        if (now - lastPress < LandRetryMs)
        {
            return null;
        }

        // Lower than at the last check: the mount is coming down, leave it be. Pressed again only when it has not come
        // down at all, since a press that lands on the ground would dismount the character.
        var height = ports.Height;
        lastPress = now;
        if (height is { } y && landHeight is { } before && y < before - LandDrop)
        {
            landHeight = y;
            return null;
        }

        landHeight = height;
        ports.StartLanding();
        return null;
    }

    /// <summary>
    /// Down from a flight: farther than <see cref="TalkRange"/> from the goal, walk the last yalms (on the landed
    /// mount, never dismounted), once; then arrived or stopped short.
    /// </summary>
    private GoToGiverOutcome? AfterLanding(GoToGiverPlan plan, long now)
    {
        Flying = false;
        var distance = GoalDistance(plan);
        if (!Approached && !float.IsNaN(distance) && distance > TalkRange)
        {
            Approached = true;
            leg = WalkLeg.Approach;
            walkTarget = (plan.GoalX, plan.GoalY, plan.GoalZ);
            if (ports.StartWalk(walkTarget.X, walkTarget.Y, walkTarget.Z, false))
            {
                Enter(GoToGiverStep.Walking, now);
                lastWaypoints = null;
                MarkProgress(now);
                return null;
            }
        }

        return Arrive(plan, now);
    }

    /// <summary>
    /// The walk ended: within <see cref="ArriveRange"/> of the goal it arrived. Farther away it stopped short: stopped by
    /// hand (vnavmesh's path still had waypoints to follow) it fails, else (no path, a path that ends short, a mesh gone
    /// bad) it gets the one reload.
    /// </summary>
    private GoToGiverOutcome? Arrive(GoToGiverPlan plan, long now)
    {
        var distance = GoalDistance(plan);
        if (!float.IsNaN(distance) && distance > ArriveRange)
        {
            var stoppedByHand = lastWaypoints is > 1;
            return stoppedByHand ? Fail(GoToGiverFailure.WalkStoppedShort) : Recover(GoToGiverFailure.WalkStoppedShort, now);
        }

        return Finish();
    }

    /// <summary>
    /// The one recovery of a run (feature plan v7 A8): the walk under way is stopped, vnavmesh reloads the zone's
    /// navmesh, and the walk is tried once more from where the character stands. A second failure, or a vnavmesh that
    /// declines the reload, fails with <paramref name="failure"/>; <see cref="Reloaded"/> is set only once vnavmesh took
    /// the reload, and <see cref="RecoverySpent"/> either way.
    /// </summary>
    private GoToGiverOutcome? Recover(GoToGiverFailure failure, long now)
    {
        if (RecoverySpent)
        {
            return Fail(failure);
        }

        RecoverySpent = true;
        if (WalkUnderWay)
        {
            ports.StopWalk();
        }

        if (!ports.StartNavReload())
        {
            return Fail(failure, stopWalk: false);
        }

        Reloaded = true;
        RecoveringFrom = failure;
        Enter(GoToGiverStep.ReloadingNav, now);
        return null;
    }

    private GoToGiverOutcome? TickReloading(GoToGiverPlan plan, long now)
    {
        if (!InWalkZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        var waited = now - stepStarted;
        if (waited < ReloadSettleMs)
        {
            return null;
        }

        if (!ports.NavReady)
        {
            return waited > PathReadyTimeoutMs ? Fail(RecoveringFrom) : null;
        }

        // The navmesh is back: the same walk (to the goal, or the last yalms) from where the character stands now.
        Enter(GoToGiverStep.PreparingPath, now);
        return TickPreparing(plan, now);
    }

    private void Enter(GoToGiverStep step, long now)
    {
        Step = step;
        stepStarted = now;
        arrivedAt = null;
        idleSince = null;
        sawLoading = false;
        sawBusy = false;
        sawWalking = false;
        pressedAgain = false;
        hopAt = null;
    }

    private GoToGiverOutcome Finish()
    {
        Step = GoToGiverStep.Done;
        return new GoToGiverOutcome(Step, GoToGiverFailure.None);
    }

    private GoToGiverOutcome Fail(GoToGiverFailure failure, bool stopWalk = true)
    {
        // A failure while this run's walk may be moving (left the zone, timed out, stuck) stops it; nothing keeps
        // walking unattended. Before the walk was asked for, vnavmesh's movement is not this run's to stop.
        if (stopWalk && WalkUnderWay)
        {
            ports.StopWalk();
        }

        Step = GoToGiverStep.Failed;
        Failure = failure;
        return new GoToGiverOutcome(Step, failure);
    }
}
