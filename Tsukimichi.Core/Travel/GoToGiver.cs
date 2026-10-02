namespace Tsukimichi.Core.Travel;

/// <summary>One travel leg: the Aetheryte sheet row to go to and the territory it lands in.</summary>
public readonly record struct TravelLeg(uint Id, uint TerritoryId);

/// <summary>
/// What one "Go to giver" (or a lone Walk) does, in order: an optional teleport, an optional aethernet hop, then an
/// optional walk to the giver's raw world position (<paramref name="GoalX"/>, <paramref name="GoalY"/> the height,
/// <paramref name="GoalZ"/>) in <paramref name="GiverTerritory"/>.
/// </summary>
public sealed record GoToGiverPlan(uint GiverTerritory, float GoalX, float GoalY, float GoalZ, TravelLeg? Teleport, TravelLeg? Hop, bool Walk)
{
    /// <summary>The hop id that stands for the Firmament (Lifestream's <c>AethernetTeleportToFirmament</c>), which has no shard row.</summary>
    public const uint FirmamentHop = uint.MaxValue;

    /// <summary>A walk only: the player is already in the giver's territory.</summary>
    public static GoToGiverPlan WalkOnly(uint giverTerritory, float goalX, float goalY, float goalZ) => new(giverTerritory, goalX, goalY, goalZ, null, null, true);
}

/// <summary>
/// What <see cref="GoToGiver"/> reads and asks of the game and the travel plugins (Lifestream, vnavmesh). The plugin
/// implements it over Dalamud's services and the two IPC wrappers; tests implement it with plain fields.
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

    /// <summary>Asks Lifestream to teleport; false when it refused.</summary>
    bool StartTeleport(uint aetheryteId);

    /// <summary>Asks Lifestream for an aethernet hop (<see cref="GoToGiverPlan.FirmamentHop"/> for the Firmament); false when it refused.</summary>
    bool StartHop(uint shardId);

    /// <summary>Asks vnavmesh to walk close to the plan's goal; false when it refused.</summary>
    bool StartWalk(GoToGiverPlan plan);

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
}

/// <summary>Why a <see cref="GoToGiver"/> run failed.</summary>
public enum GoToGiverFailure
{
    None,

    /// <summary>A walk was asked for outside the giver's territory, with no teleport to get there.</summary>
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

    /// <summary>The walk ended farther from the giver than <see cref="GoToGiver.ArriveRange"/> (no path, or stopped by hand).</summary>
    WalkStoppedShort,

    /// <summary>The player left the giver's territory during the walk.</summary>
    LeftZone,
}

/// <summary>How a run ended: <see cref="GoToGiverStep.Done"/>, <see cref="GoToGiverStep.Cancelled"/> or <see cref="GoToGiverStep.Failed"/> with its reason.</summary>
public readonly record struct GoToGiverOutcome(GoToGiverStep Step, GoToGiverFailure Failure);

/// <summary>
/// The "Go to giver" chain (feature plan v5, 1.6.0, decision 1): teleport, wait for the arrival, an optional aethernet
/// hop, then a vnavmesh walk to the giver. Pure: the plugin calls <see cref="Tick"/> from the framework update with a
/// millisecond clock and the machine reads and acts through <see cref="ITravelPorts"/>. Every step can be cancelled
/// (<see cref="Cancel"/>, one Stop) and every wait has a deadline, so a run always ends; the ending is returned once
/// from the call that reached it, for the plugin's chat line.
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

    private readonly ITravelPorts ports;
    private long stepStarted;
    private long? arrivedAt;
    private long? idleSince;
    private bool sawLoading;
    private bool sawBusy;
    private bool sawWalking;

    public GoToGiver(ITravelPorts ports)
    {
        this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
    }

    public GoToGiverStep Step { get; private set; } = GoToGiverStep.Idle;

    public GoToGiverFailure Failure { get; private set; } = GoToGiverFailure.None;

    /// <summary>The plan of the current or last run; null before the first.</summary>
    public GoToGiverPlan? Plan { get; private set; }

    /// <summary>True from <see cref="Start"/> until the run is done, cancelled or failed.</summary>
    public bool IsActive => Step is GoToGiverStep.Teleporting or GoToGiverStep.Hopping or GoToGiverStep.PreparingPath or GoToGiverStep.Walking;

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
        if (plan.Teleport is { } teleport)
        {
            return BeginTeleport(teleport, now);
        }

        return AfterTeleport(plan, now);
    }

    /// <summary>Stops the run: the walk is stopped and Lifestream's task aborted. The ending, or null when nothing ran.</summary>
    public GoToGiverOutcome? Cancel()
    {
        if (!IsActive)
        {
            return null;
        }

        if (Step is GoToGiverStep.PreparingPath or GoToGiverStep.Walking)
        {
            ports.StopWalk();
        }
        else if (ports.LifestreamBusy)
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
            GoToGiverStep.Walking => TickWalking(plan, now),
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

        if (ports.Territory != plan.GiverTerritory)
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

    private GoToGiverOutcome? TickPreparing(GoToGiverPlan plan, long now)
    {
        if (ports.Territory != plan.GiverTerritory || ports.BetweenAreas)
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (ports.NavReady)
        {
            if (!ports.StartWalk(plan))
            {
                return Fail(GoToGiverFailure.WalkRefused);
            }

            Enter(GoToGiverStep.Walking, now);
            return null;
        }

        return now - stepStarted > PathReadyTimeoutMs ? Fail(GoToGiverFailure.PathNotReady) : null;
    }

    private GoToGiverOutcome? TickWalking(GoToGiverPlan plan, long now)
    {
        if (ports.Territory != plan.GiverTerritory || ports.BetweenAreas)
        {
            return Fail(GoToGiverFailure.LeftZone);
        }

        if (ports.Walking)
        {
            sawWalking = true;
            return now - stepStarted > WalkTimeoutMs ? Fail(GoToGiverFailure.WalkTimedOut) : null;
        }

        if (!sawWalking)
        {
            return now - stepStarted > WalkStartTimeoutMs ? Fail(GoToGiverFailure.WalkDidNotStart) : null;
        }

        // The walk ended: arrived, or stopped short (no path found, stopped from vnavmesh's own window).
        if (ports.Position is { } at && TravelPlanner.Distance(at.X, at.Z, plan.GoalX, plan.GoalZ) > ArriveRange)
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
    }

    private GoToGiverOutcome Finish()
    {
        Step = GoToGiverStep.Done;
        return new GoToGiverOutcome(Step, GoToGiverFailure.None);
    }

    private GoToGiverOutcome Fail(GoToGiverFailure failure)
    {
        // A failure while vnavmesh may be moving (left the zone, timed out) stops it; nothing keeps walking unattended.
        if (Step is GoToGiverStep.PreparingPath or GoToGiverStep.Walking)
        {
            ports.StopWalk();
        }

        Step = GoToGiverStep.Failed;
        Failure = failure;
        return new GoToGiverOutcome(Step, failure);
    }
}
