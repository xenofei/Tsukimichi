namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// A game copied onto another of the same level (plan v9 G7): an opponent or the playability check tries a shot on
/// a copy and plays the real one on the game itself. Nothing is allocated: every array is copied into the target's own.
/// </summary>
public sealed partial class MoonfallGame
{
    /// <summary>
    /// Makes this game a copy of <paramref name="source"/>, which must be a game of the same level object, level number,
    /// rules and oranges (a copy made with the same constructor arguments, bar the seed and balls, which are copied):
    /// from here on both play the same calls the same way, bit for bit (<see cref="Fingerprint"/>). Allocates nothing.
    /// </summary>
    public void CopyFrom(MoonfallGame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(source, this))
        {
            return;
        }

        if (!ReferenceEquals(source.Level, Level) || source.LevelNumber != LevelNumber || source.RuleSet != RuleSet || source.orangeTarget != orangeTarget)
        {
            throw new ArgumentException("A game copies only a game of the same level, level number, rules and oranges.", nameof(source));
        }

        // The board, the balls and the event ring.
        Array.Copy(source.bodies, bodies, bodies.Length);
        Array.Copy(source.hitOrder, hitOrder, hitOrder.Length);
        Array.Copy(source.events, events, events.Length);
        Array.Copy(source.balls, balls, balls.Length);
        Array.Copy(source.contacts, contacts, contacts.Length);
        eventHead = source.eventHead;
        eventCount = source.eventCount;
        contactCount = source.contactCount;
        random = source.random;
        counter = source.counter;

        // The clocks.
        gameTick = source.gameTick;
        realTick = source.realTick;
        speedMilli = source.speedMilli;
        speedAccumulator = source.speedAccumulator;
        realAccumulator = source.realAccumulator;

        // The shot and the turn.
        ballCount = source.ballCount;
        toucher = source.toucher;
        hitCount = source.hitCount;
        purple = source.purple;
        shotValue = source.shotValue;
        shotPegs = source.shotPegs;
        freeBallsThisShot = source.freeBallsThisShot;
        levelScore = source.levelScore;
        clearWait = source.clearWait;
        clearCursor = source.clearCursor;
        clearedThisTurn = source.clearedThisTurn;
        nextBallWait = source.nextBallWait;
        turnScored = source.turnScored;
        lostThisTurn = source.lostThisTurn;
        Phase = source.Phase;
        BallsLeft = source.BallsLeft;
        OrangesLeft = source.OrangesLeft;
        Score = source.Score;
        Perfect = source.Perfect;
        Tally = source.Tally;
        LastBounce = source.LastBounce;

        // Fever and the camera.
        lastOrange = source.lastOrange;
        approachBall = source.approachBall;
        approach = source.approach;
        feverHit = source.feverHit;
        feverLanded = source.feverLanded;
        feverRealTicks = source.feverRealTicks;
        feverBucket = source.feverBucket;
        feverBonus = source.feverBonus;
        zoom = source.zoom;
        zoomAtHit = source.zoomAtHit;
        focusX = source.focusX;
        focusY = source.focusY;
        hitFocusX = source.hitFocusX;
        hitFocusY = source.hitFocusY;

        // The powers.
        Power = source.Power;
        Array.Copy(source.powerShots, powerShots, powerShots.Length);
        Array.Copy(source.powerActive, powerActive, powerActive.Length);
        Array.Copy(source.greenQueue, greenQueue, greenQueue.Length);
        Array.Copy(source.scratch, scratch, scratch.Length);
        Array.Copy(source.scratchDistance, scratchDistance, scratchDistance.Length);
        Array.Copy(source.stamp, stamp, stamp.Length);
        Array.Copy(source.boltPoints, boltPoints, boltPoints.Length);
        greenQueued = source.greenQueued;
        powerDepth = source.powerDepth;
        stampId = source.stampId;
        gateLeft = source.gateLeft;
        tripleShots = source.tripleShots;
        tripleThisShot = source.tripleThisShot;
        flipperInput = source.flipperInput;
        flipperLift = source.flipperLift;
        flipperLiftPrev = source.flipperLiftPrev;
        flipperRate = source.flipperRate;
        boltPointCount = source.boltPointCount;
        boltFired = source.boltFired;
        boltTick = source.boltTick;
        pathNudge = source.pathNudge;
        probe = source.probe;
        pathCandidate = source.pathCandidate;
        pathAim = source.pathAim;
        pathBest = source.pathBest;
        pathBestValue = source.pathBestValue;
        pathLaunchTick = source.pathLaunchTick;
        LastPathSubSteps = source.LastPathSubSteps;
        LastPathTickSubSteps = source.LastPathTickSubSteps;

        // The style shots.
        styleBonus = source.styleBonus;
        styleAwarded = source.styleAwarded;
        shotOranges = source.shotOranges;
        orangesAtShotStart = source.orangesAtShotStart;

        // The duel.
        Array.Copy(source.sidePowerShots, sidePowerShots, sidePowerShots.Length);
        sideTriple0 = source.sideTriple0;
        sideTriple1 = source.sideTriple1;
        duelGreensLeft = source.duelGreensLeft;
        Side = source.Side;
    }
}
