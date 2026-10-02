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

    /// <summary>True while vnavmesh follows a path or is still finding one.</summary>
    bool Walking { get; }

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

    /// <summary>Asks vnavmesh to walk (or, <paramref name="fly"/>, fly) close to the plan's goal; false when it refused.</summary>
    bool StartWalk(GoToGiverPlan plan, bool fly);

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
/// failure: the walk goes on foot. A walk that stops making progress gets one new path, then fails as stuck. The
/// character is never dismounted.
/// </summary>
public sealed class GoToGiver
{
    /// <summary>A teleport cast is five seconds; with no loading screen by then it was interrupted.</summary>
    public const long TeleportDepartMs = 15_000;

    /// <summary>Lifestream may first walk a few steps to the aetheryte before an aethernet hop.</summary>
    public const long HopDepartMs = 30_000;

    /// <summary>After Lifestream went idle without a loading screen, the hop is taken as not started.</summary>
    public const long HopIdleGraceMs = 3_000;

    /// <summary>The longest a teleport or hop may take from the request to standing in the new zone.</summary>
    public const long ArriveTimeoutMs = 90_000;

    /// <summary>How long the player must stand in the new zone, idle, before the next step: the zone finishes loading.</summary>
    public const long SettleMs = 1_500;

    /// <summary>The longest vnavmesh may take to have a navmesh for the zone (a first build is slow).</summary>
    public const long PathReadyTimeoutMs = 120_000;

    /// <summary>vnavmesh reports pathfinding at once; without it by then the walk did not start.</summary>
    public const long WalkStartTimeoutMs = 5_000;

    /// <summary>The longest one walk may take.</summary>
    public const long WalkTimeoutMs = 300_000;

    /// <summary>Distance (raw units) within which a finished walk counts as arrived: the walk stops about 3 away.</summary>
    public const float ArriveRange = 8f;

    /// <summary>The mount cast is about a second; without the mount by then it is asked for once more.</summary>
    public const long MountRetryMs = 3_000;

    /// <summary>Without a mount by then, the walk goes on foot.</summary>
    public const long MountTimeoutMs = 8_000;

    /// <summary>A walk that comes no <see cref="StuckProgress"/> closer in this long is stuck.</summary>
    public const long StuckMs = 15_000;

    /// <summary>How much closer (raw units) the walk must come to count as progress.</summary>
    public const float StuckProgress = 2f;

    /// <summary>How often the landing is asked for again while the mount is still in the air.</summary>
    public const long LandRetryMs = 1_000;

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
    private float bestDistance;
    private long progressAt;
    private bool repathed;

    public GoToGiver(ITravelPorts ports)
    {
        this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
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

    /// <summary>True from <see cref="Start"/> until the run is done, cancelled or failed.</summary>
    public bool IsActive => Step is GoToGiverStep.Teleporting or GoToGiverStep.Hopping or GoToGiverStep.PreparingPath
        or GoToGiverStep.Mounting or GoToGiverStep.Walking or GoToGiverStep.Landing;

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
    /// a mount being summoned or landing finishes on its own. The ending, or null when nothing ran. A cancelled run
    /// never ticks again, so a teleport cast that was under way lands without a hop or a walk after it.
    /// </summary>
    public GoToGiverOutcome? Cancel()
    {
        if (!IsActive)
        {
            return null;
        }

        if (Step == GoToGiverStep.Walking)
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
            GoToGiverStep.Walking => TickWalking(plan, now),
            GoToGiverStep.Landing => TickLanding(plan, now),
            _ => null,
        };
    }

    private GoToGiverOutcome? BeginTeleport(TravelLeg leg, long now)
    {
        if (!ports.StartTeleport(leg.Id))
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
            if (!ports.StartHop(hop.Id))
            {
                return Fail(GoToGiverFailure.HopRefused);
            }

            Enter(GoToGiverStep.Hopping, now);
            return null;
        }

        return AfterHop(plan, now);
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
        var leg = plan.Teleport!.Value;
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

        if (Settled(leg.TerritoryId, now))
        {
            return AfterTeleport(plan, now);
        }

        return now - stepStarted > ArriveTimeoutMs ? Fail(GoToGiverFailure.TeleportTimedOut) : null;
    }

    private GoToGiverOutcome? TickHop(GoToGiverPlan plan, long now)
    {
        var leg = plan.Hop!.Value;
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
                    return Fail(GoToGiverFailure.HopDidNotStart);
                }
            }

            return now - stepStarted > HopDepartMs ? Fail(GoToGiverFailure.HopDidNotStart) : null;
        }

        if (Settled(leg.TerritoryId, now))
        {
            return AfterHop(plan, now);
        }

        return now - stepStarted > ArriveTimeoutMs ? Fail(GoToGiverFailure.HopTimedOut) : null;
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

    /// <summary>True while the player is still in the goal's territory and out of loading.</summary>
    private bool InGoalZone(GoToGiverPlan plan) => ports.Territory == plan.GoalTerritory && !ports.BetweenAreas;

    /// <summary>Distance on the ground plane from the player to the goal; NaN without a player position.</summary>
    private float GoalDistance(GoToGiverPlan plan) =>
        ports.Position is { } at ? TravelPlanner.Distance(at.X, at.Z, plan.GoalX, plan.GoalZ) : float.NaN;

    private GoToGiverOutcome? TickPreparing(GoToGiverPlan plan, long now)
    {
        if (!InGoalZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (!ports.NavReady)
        {
            return now - stepStarted > PathReadyTimeoutMs ? Fail(GoToGiverFailure.PathNotReady) : null;
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
        if (!InGoalZone(plan))
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
        Flying = Move.Fly && ports.Mounted;
        if (!Flying && !ports.Mounted && Move.Sprint)
        {
            ports.StartSprint();
        }

        if (!ports.StartWalk(plan, Flying))
        {
            return Fail(GoToGiverFailure.WalkRefused);
        }

        Enter(GoToGiverStep.Walking, now);
        bestDistance = GoalDistance(plan);
        progressAt = now;
        return null;
    }

    private GoToGiverOutcome? TickWalking(GoToGiverPlan plan, long now)
    {
        if (!InGoalZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (ports.Walking)
        {
            sawWalking = true;
            if (now - stepStarted > WalkTimeoutMs)
            {
                return Fail(GoToGiverFailure.WalkTimedOut);
            }

            return CheckProgress(plan, now);
        }

        if (!sawWalking)
        {
            return now - stepStarted > WalkStartTimeoutMs ? Fail(GoToGiverFailure.WalkDidNotStart) : null;
        }

        // The walk ended: still in the air, come down first; then arrived, or stopped short (no path, stopped by hand).
        if (ports.InFlight)
        {
            ports.StartLanding();
            Enter(GoToGiverStep.Landing, now);
            lastPress = now;
            return null;
        }

        return Arrive(plan);
    }

    /// <summary>
    /// Stuck detection while vnavmesh walks: no <see cref="StuckProgress"/> closer for <see cref="StuckMs"/> gets one
    /// new path; stuck again fails.
    /// </summary>
    private GoToGiverOutcome? CheckProgress(GoToGiverPlan plan, long now)
    {
        var distance = GoalDistance(plan);
        if (float.IsNaN(distance))
        {
            progressAt = now;
            return null;
        }

        if (float.IsNaN(bestDistance) || distance < bestDistance - StuckProgress)
        {
            bestDistance = distance;
            progressAt = now;
            return null;
        }

        if (now - progressAt < StuckMs)
        {
            return null;
        }

        if (repathed)
        {
            return Fail(GoToGiverFailure.Stuck);
        }

        repathed = true;
        ports.StopWalk();
        if (!ports.StartWalk(plan, Flying))
        {
            return Fail(GoToGiverFailure.WalkRefused);
        }

        Enter(GoToGiverStep.Walking, now);
        bestDistance = distance;
        progressAt = now;
        return null;
    }

    private GoToGiverOutcome? TickLanding(GoToGiverPlan plan, long now)
    {
        if (!InGoalZone(plan))
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (!ports.InFlight)
        {
            return Arrive(plan);
        }

        if (now - stepStarted > LandTimeoutMs)
        {
            return Fail(GoToGiverFailure.LandingFailed);
        }

        if (now - lastPress >= LandRetryMs)
        {
            lastPress = now;
            ports.StartLanding();
        }

        return null;
    }

    private GoToGiverOutcome Arrive(GoToGiverPlan plan)
    {
        var distance = GoalDistance(plan);
        if (!float.IsNaN(distance) && distance > ArriveRange)
        {
            return Fail(GoToGiverFailure.WalkStoppedShort);
        }

        return Finish();
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
    }

    private GoToGiverOutcome Finish()
    {
        Step = GoToGiverStep.Done;
        return new GoToGiverOutcome(Step, GoToGiverFailure.None);
    }

    private GoToGiverOutcome Fail(GoToGiverFailure failure)
    {
        // A failure while this run's walk may be moving (left the zone, timed out, stuck) stops it; nothing keeps
        // walking unattended. Before the walk was asked for, vnavmesh's movement is not this run's to stop.
        if (Step == GoToGiverStep.Walking)
        {
            ports.StopWalk();
        }

        Step = GoToGiverStep.Failed;
        Failure = failure;
        return new GoToGiverOutcome(Step, failure);
    }
}
