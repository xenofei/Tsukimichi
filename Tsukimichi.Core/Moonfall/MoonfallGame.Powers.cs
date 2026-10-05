namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The eleven powers (plan v9 G5). A green peg lit by any means triggers the level's <see cref="Power"/>
/// (<see cref="MoonfallEventKind.GreenHit"/>); each power keeps a count of the shots it still acts in, and acts at the
/// hit, from the hit, or from the next shot (<see cref="MoonfallPowers.Timing"/>). Every rule cites its line in the
/// research or is marked [J] in <see cref="MoonfallRules"/>. Nothing here allocates: every buffer is made with the game.
/// </summary>
public sealed partial class MoonfallGame
{
    private readonly int[] powerShots = new int[MoonfallPowers.Count + 1];
    private readonly bool[] powerActive = new bool[MoonfallPowers.Count + 1];

    /// <summary>Greens lit while a power is lighting pegs wait here, so one burst never runs inside another.</summary>
    private readonly int[] greenQueue = new int[8];
    private int greenQueued;
    private int powerDepth;

    /// <summary>Pegs and their distances for a burst or a bolt, sorted; and the path search's per-flight stamps.</summary>
    private readonly int[] scratch;
    private readonly double[] scratchDistance;
    private readonly int[] stamp;
    private int stampId;

    private int gateLeft;
    private int tripleShots;
    private bool tripleThisShot;

    private bool flipperInput;
    private double flipperLift;
    private double flipperLiftPrev;
    private double flipperRate;

    private readonly (double X, double Y)[] boltPoints = new (double X, double Y)[MoonfallRules.BoltMaxPoints];
    private int boltPointCount;
    private bool boltFired;
    private long boltTick = -1;

    private double pathNudge;

    /// <summary>What a flight off the live ball touched in one <see cref="ResolveContacts"/> call.</summary>
    private struct Probe
    {
        public bool Any;
        public int Peg;
        public bool Recording;
        public long Value;
        public int Pegs;
        public int Oranges;
    }

    private Probe probe;

    // ---- What the window reads ----

    /// <summary>The power a green peg triggers on this level (the level's character's), or <see cref="MoonfallPower.None"/>.</summary>
    public MoonfallPower Power { get; private set; }

    /// <summary>
    /// The shots <paramref name="power"/> still acts in: counting this one while it acts in it, or the coming shot's
    /// while a ball waits in the launcher. 0 when it is not charged.
    /// </summary>
    public int PowerShotsLeft(MoonfallPower power) => power is > MoonfallPower.None and <= MoonfallPower.Bolt ? powerShots[(int)power] : 0;

    /// <summary>Whether <paramref name="power"/> acts in the shot in play.</summary>
    public bool PowerActive(MoonfallPower power) => power is > MoonfallPower.None and <= MoonfallPower.Bolt && powerActive[(int)power];

    /// <summary>Super Guide is charged: the guide runs on through its first bounce (<see cref="GuideBeyond"/>).</summary>
    public bool GuideExtended => powerShots[(int)MoonfallPower.SuperGuide] > 0;

    /// <summary>Brass Wings are open: the bucket's mouth is <see cref="MoonfallRules.WingsMouth"/> wide.</summary>
    public bool WingsOpen => powerShots[(int)MoonfallPower.Wings] > 0 && !feverHit;

    /// <summary>The bucket's mouth now (px).</summary>
    public double BucketMouth => WingsOpen ? MoonfallRules.WingsMouth : MoonfallRules.BucketMouth;

    private double MouthHalf => BucketMouth * 0.5;

    /// <summary>The flippers are out (they stay out between the shots they act in; never in the Full Moon, as the wings).</summary>
    public bool FlippersOut => powerShots[(int)MoonfallPower.Flippers] > 0 && !feverHit;

    /// <summary>Moon Gate's re-entries left this shot.</summary>
    public int GateLeft => gateLeft;

    /// <summary>A triple score is on for this shot (<see cref="MoonfallDrawOutcome.TripleScore"/>).</summary>
    public bool TripleScore => tripleThisShot;

    /// <summary>The last bolt's path: where it began, each peg it lit in order, the bucket (Storm Post).</summary>
    public ReadOnlySpan<(double X, double Y)> BoltPoints => boltPoints.AsSpan(0, boltPointCount);

    /// <summary>The game tick the last bolt struck, or −1.</summary>
    public long BoltTick => boltTick;

    /// <summary>How far (degrees) Sage's Path turned this shot from where it was aimed.</summary>
    public double PathNudge => pathNudge;

    // ---- Input ----

    /// <summary>
    /// Holds the flippers up (true) or lets them fall (false). They answer only while the Flippers power acts in the shot
    /// in play; the window passes the button's state every frame.
    /// </summary>
    public void SetFlippers(bool up) => flipperInput = up;

    /// <summary>
    /// A flipper as drawn at <paramref name="alpha"/>: its pivot and its tip's centre (<paramref name="right"/> for the
    /// right one). Its thickness is <see cref="MoonfallRules.FlipperThickness"/>.
    /// </summary>
    public (double PivotX, double PivotY, double TipX, double TipY) Flipper(bool right, double alpha)
    {
        var lift = flipperLiftPrev + ((flipperLift - flipperLiftPrev) * alpha);
        var (px, py, angle) = FlipperPose(right, lift);
        return (px, py, px + (MoonfallRules.FlipperLength * Math.Cos(angle)), py + (MoonfallRules.FlipperLength * Math.Sin(angle)));
    }

    // ---- The shot's powers ----

    private void StartShotPowers()
    {
        for (var p = 1; p < powerShots.Length; p++)
        {
            powerActive[p] = powerShots[p] > 0;
        }

        tripleThisShot = tripleShots > 0;
        gateLeft = 0;
        boltFired = false;
        pathNudge = 0;
        pathCandidate = -1;
    }

    /// <summary>The shot is over: each power that acted in it has one shot fewer; a power that runs out says so.</summary>
    private void EndShotPowers()
    {
        for (var p = 1; p < powerShots.Length; p++)
        {
            if (!powerActive[p])
            {
                continue;
            }

            powerActive[p] = false;
            powerShots[p] = Math.Max(0, powerShots[p] - 1);
            if (powerShots[p] == 0)
            {
                Post(MoonfallEventKind.PowerEnded, -1, p, 0, 0, 0);
            }
        }

        // The triple stays on this shot until it is scored, as its pegs clear; the next shot looks again.
        if (tripleThisShot)
        {
            tripleShots = Math.Max(0, tripleShots - 1);
        }

        gateLeft = 0;
    }

    /// <summary>A green was lit: its power acts now, or once the power lighting pegs at the moment has finished.</summary>
    private void OnGreenLit(int peg)
    {
        if (greenQueued < greenQueue.Length)
        {
            greenQueue[greenQueued++] = peg;
        }

        DrainGreens();
    }

    /// <summary>Triggers the power for each green waiting, unless a power is still lighting pegs (it drains them when done).</summary>
    private void DrainGreens()
    {
        if (powerDepth > 0)
        {
            return;
        }

        // A green lit by one of these powers joins the queue and is reached by this same loop.
        for (var k = 0; k < greenQueued; k++)
        {
            TriggerPower(Power, greenQueue[k]);
        }

        greenQueued = 0;
    }

    /// <summary>
    /// <paramref name="power"/> is triggered at peg <paramref name="peg"/> (a green, or the green the drum turned on):
    /// its count of shots goes up (<see cref="MoonfallPowers.Shots"/>, added to what is left), and a power that acts at
    /// the hit acts.
    /// </summary>
    private void TriggerPower(MoonfallPower power, int peg)
    {
        if (power == MoonfallPower.None)
        {
            return;
        }

        var p = (int)power;
        switch (MoonfallPowers.Timing(power))
        {
            case MoonfallPowerTiming.Instant:
                powerShots[p] = Math.Max(powerShots[p], 1);
                powerActive[p] = true;
                break;

            case MoonfallPowerTiming.AtOnce:
                powerShots[p] += MoonfallPowers.Shots(power);
                powerActive[p] = true;
                break;

            default:
                powerShots[p] += MoonfallPowers.Shots(power);
                break;
        }

        ref readonly var green = ref bodies[peg];
        Post(MoonfallEventKind.PowerTriggered, peg, p, powerShots[p], green.BoundX, green.BoundY);
        powerDepth++;
        try
        {
            switch (power)
            {
                case MoonfallPower.Multiball:
                    SpawnTwin(peg);
                    break;

                case MoonfallPower.Burst:
                    BurstFrom(peg);
                    break;

                case MoonfallPower.Gate:
                    gateLeft += MoonfallRules.GateReentries;
                    break;

                case MoonfallPower.Bloom:
                    BloomFrom(peg);
                    break;

                case MoonfallPower.Draw:
                    DrawAt(peg);
                    break;
            }
        }
        finally
        {
            powerDepth--;
        }
    }

    // ---- Multiball [R §5 l.108] ----

    /// <summary>A twin ball springs from the upper face of the green with the hitting ball's velocity mirrored left to right.</summary>
    private void SpawnTwin(int peg)
    {
        ref readonly var green = ref bodies[peg];
        double vx, vy, away;
        if (toucher >= 0)
        {
            ref readonly var hitter = ref balls[toucher];
            vx = -hitter.Vx;
            vy = hitter.Vy;
            away = hitter.X > green.BoundX ? -1.0 : 1.0;
        }
        else
        {
            vx = 0;
            vy = -MoonfallRules.TwinMinSideSpeed;
            away = 1.0;
        }

        if (Math.Abs(vx) < MoonfallRules.TwinMinSideSpeed)
        {
            vx = away * MoonfallRules.TwinMinSideSpeed;
        }

        // Clear of the green along its upper face's normal: straight up off a round peg, square off a sloped brick (a
        // brick's centre line is further from its face straight up than across, so straight up would start inside it).
        var top = (green.Shape == PegShape.Round ? green.Radius : green.Half) + MoonfallRules.BallRadius + 0.5;
        var (nx, ny) = green.UpperNormal(away);
        var x = green.BoundX + (nx * top);
        var y = green.BoundY + (ny * top);
        if (Phase == MoonfallPhase.Flying && AddBall(x, y, vx, vy))
        {
            Post(MoonfallEventKind.BallAdded, peg, 0, ballCount, x, y);
        }
    }

    // ---- Lunar Burst [R §5 l.110] ----

    /// <summary>Lights every peg whose surface is within <see cref="MoonfallRules.BurstRadius"/> of the green's centre, nearest first.</summary>
    private void BurstFrom(int peg)
    {
        var cx = bodies[peg].BoundX;
        var cy = bodies[peg].BoundY;
        var n = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (i == peg || b.Lit || b.Cleared)
            {
                continue;
            }

            var surface = b.Gap(cx, cy) + MoonfallRules.BallRadius;
            if (surface <= MoonfallRules.BurstRadius)
            {
                n = InsertSorted(n, i, surface);
            }
        }

        for (var k = 0; k < n; k++)
        {
            Light(scratch[k]);
        }
    }

    /// <summary>Puts peg <paramref name="index"/> into the first <paramref name="n"/> of <see cref="scratch"/> by distance, then index; returns the new count.</summary>
    private int InsertSorted(int n, int index, double distance)
    {
        var at = n;
        while (at > 0 && scratchDistance[at - 1] > distance)
        {
            scratch[at] = scratch[at - 1];
            scratchDistance[at] = scratchDistance[at - 1];
            at--;
        }

        scratch[at] = index;
        scratchDistance[at] = distance;
        return n + 1;
    }

    // ---- Moonbloom [R §5 l.113] ----

    /// <summary>Lights the closest fifth (rounded up) of the oranges left, nearest the green first; ties go to the earlier peg.</summary>
    private void BloomFrom(int peg)
    {
        var cx = bodies[peg].BoundX;
        var cy = bodies[peg].BoundY;
        var count = (OrangesLeft + MoonfallRules.BloomShare - 1) / MoonfallRules.BloomShare;
        for (var k = 0; k < count; k++)
        {
            var pick = -1;
            var best = double.MaxValue;
            for (var i = 0; i < bodies.Length; i++)
            {
                ref readonly var b = ref bodies[i];
                if (b.Colour != PegColour.Orange || b.Lit || b.Cleared)
                {
                    continue;
                }

                var dx = b.BoundX - cx;
                var dy = b.BoundY - cy;
                var d2 = (dx * dx) + (dy * dy);
                if (d2 < best)
                {
                    best = d2;
                    pick = i;
                }
            }

            if (pick < 0)
            {
                return;
            }

            Light(pick);
        }
    }

    // ---- Moon-Viewing Draw [R §5 l.114] ----

    private void DrawAt(int peg)
    {
        ref readonly var green = ref bodies[peg];
        var outcome = MoonfallPowers.Draw(ref random, out var other);
        Post(MoonfallEventKind.Drawn, peg, (long)outcome, (int)other, green.BoundX, green.BoundY);
        switch (outcome)
        {
            case MoonfallDrawOutcome.FreeBall:
                BallsLeft++;
                Post(MoonfallEventKind.FreeBall, -1, 0, freeBallsThisShot, green.BoundX, green.BoundY);
                break;

            case MoonfallDrawOutcome.TripleScore:
                tripleShots += MoonfallRules.TripleScoreShots;
                tripleThisShot = true;
                AwardShotFreeBalls();
                break;

            default:
                TriggerPower(other, peg);
                break;
        }
    }

    // ---- Moon Gate [R §5 l.112] ----

    /// <summary>A ball falling out comes back in at the top while the gate has re-entries this shot (never in the Full Moon).</summary>
    private bool TryGate(ref Ball b)
    {
        if (gateLeft <= 0 || feverHit)
        {
            return false;
        }

        gateLeft--;
        b.Y = MoonfallRules.GateReentryY;
        b.PrevX = b.X;
        b.PrevY = b.Y;
        b.ResetWatch();
        Post(MoonfallEventKind.BallReentered, -1, gateLeft, 0, b.X, b.Y);
        return true;
    }

    // ---- Fireball [R §5 l.115] ----

    /// <summary>A Fireball shot meets peg <paramref name="index"/>: it is lit (once) and burns away at once, and the ball does not bounce.</summary>
    private void Burn(ref Ball b, int index)
    {
        Touch(ref b, index);
        ref var body = ref bodies[index];
        if (body.Cleared)
        {
            return;
        }

        body.Cleared = true;
        clearedThisTurn++;
        Post(MoonfallEventKind.PegCleared, index, 0, clearedThisTurn, body.BoundX, body.BoundY);
    }

    // ---- Flippers [R §5 l.111] ----

    /// <summary>
    /// The flippers move towards up while held and down while not, a game tick at a time. In the Full Moon they are gone
    /// (<see cref="FlippersOut"/>): they neither rise nor meet the ball, so nothing stands over the Fever buckets.
    /// </summary>
    private void StepFlippers()
    {
        flipperLiftPrev = flipperLift;
        var up = flipperInput && PowerActive(MoonfallPower.Flippers) && Phase == MoonfallPhase.Flying && !feverHit;
        flipperLift = up
            ? Math.Min(1.0, flipperLift + (MoonfallRules.TickSeconds / MoonfallRules.FlipperUpSeconds))
            : Math.Max(0.0, flipperLift - (MoonfallRules.TickSeconds / MoonfallRules.FlipperDownSeconds));
        flipperRate = (flipperLift - flipperLiftPrev) / MoonfallRules.TickSeconds;
    }

    /// <summary>A flipper's pivot and angle (radians, 0 along +x, positive towards +y) at <paramref name="lift"/> (0 down, 1 up).</summary>
    private static (double X, double Y, double Angle) FlipperPose(bool right, double lift)
    {
        var degrees = MoonfallRules.FlipperRestDegrees + (lift * (MoonfallRules.FlipperUpDegrees - MoonfallRules.FlipperRestDegrees));
        return right
            ? (MoonfallRules.Width - MoonfallRules.FlipperPivotX, MoonfallRules.FlipperPivotY, MoonfallGeometry.Radians(180 - degrees))
            : (MoonfallRules.FlipperPivotX, MoonfallRules.FlipperPivotY, MoonfallGeometry.Radians(degrees));
    }

    /// <summary>The flippers as surfaces while the power acts, never in the Full Moon: capsules that carry their swing's speed into the bounce.</summary>
    private void FlipperContacts(in Ball b)
    {
        if (feverHit || !PowerActive(MoonfallPower.Flippers) || b.Y < MoonfallRules.FlipperPivotY - MoonfallRules.FlipperLength - 20)
        {
            return;
        }

        var swing = flipperRate * MoonfallGeometry.Radians(MoonfallRules.FlipperUpDegrees - MoonfallRules.FlipperRestDegrees);
        for (var side = 0; side < 2; side++)
        {
            var right = side == 1;
            var (px, py, angle) = FlipperPose(right, flipperLift);
            var tx = px + (MoonfallRules.FlipperLength * Math.Cos(angle));
            var ty = py + (MoonfallRules.FlipperLength * Math.Sin(angle));
            var (qx, qy) = MoonfallGeometry.ClosestOnSegment(b.X, b.Y, px, py, tx, ty);
            var dx = b.X - qx;
            var dy = b.Y - qy;
            var reach = (MoonfallRules.FlipperThickness * 0.5) + MoonfallRules.BallRadius;
            var d2 = (dx * dx) + (dy * dy);
            if (d2 >= reach * reach)
            {
                continue;
            }

            var d = Math.Sqrt(d2);
            var (nx, ny) = d > 1e-9 ? (dx / d, dy / d) : (0.0, -1.0);

            // The surface's speed at the contact: ω × r, the right flipper turning the other way.
            var omega = right ? -swing : swing;
            AddContact(ContactKind.Flipper, side, reach - d, nx, ny, -omega * (qy - py), omega * (qx - px));
        }
    }

    // ---- Storm Post [R §5 l.117] ----

    /// <summary>
    /// The bolt: from the first peg the ball lights this shot to the bucket's centre, lighting every peg whose centre is
    /// within <see cref="MoonfallRules.BoltReach"/> of that line, in order along it.
    /// </summary>
    private void FireBolt(int start)
    {
        boltFired = true;
        boltTick = gameTick;
        var sx = bodies[start].BoundX;
        var sy = bodies[start].BoundY;
        var ex = BucketX;
        var ey = MoonfallRules.BucketTop;
        var dx = ex - sx;
        var dy = ey - sy;
        var length2 = Math.Max((dx * dx) + (dy * dy), 1e-9);
        var n = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (i == start || b.Lit || b.Cleared)
            {
                continue;
            }

            var (qx, qy) = MoonfallGeometry.ClosestOnSegment(b.BoundX, b.BoundY, sx, sy, ex, ey);
            if (MoonfallGeometry.Hypot(b.BoundX - qx, b.BoundY - qy) <= MoonfallRules.BoltReach)
            {
                n = InsertSorted(n, i, (((b.BoundX - sx) * dx) + ((b.BoundY - sy) * dy)) / length2);
            }
        }

        boltPointCount = 0;
        boltPoints[boltPointCount++] = (sx, sy);
        var lit = 0;
        powerDepth++;
        try
        {
            for (var k = 0; k < n; k++)
            {
                var index = scratch[k];
                if (!Light(index))
                {
                    continue;
                }

                lit++;
                if (boltPointCount < boltPoints.Length - 1)
                {
                    boltPoints[boltPointCount++] = (bodies[index].BoundX, bodies[index].BoundY);
                }
            }
        }
        finally
        {
            powerDepth--;
        }

        boltPoints[boltPointCount++] = (ex, ey);
        Post(MoonfallEventKind.BoltStruck, start, lit, boltPointCount, sx, sy);
        if (lit >= MoonfallRules.LiveWirePegs)
        {
            Award(MoonfallStyleShot.LiveWire, start, sx, sy);
        }

        // Greens the bolt lit act now, after it.
        DrainGreens();
    }

    // ---- Super Guide [R §1 l.41, §5 l.107] ----

    /// <summary>
    /// Super Guide's line past the plain guide (<see cref="Guide"/>): from the shot's first contact, through the bounce,
    /// to the next thing it touches, or <see cref="MoonfallRules.SuperGuideMaxLength"/> px. Flown with the real contact
    /// solver on a copy, nothing in the game changes. Writes points every <see cref="MoonfallRules.GuideDotSpacing"/>
    /// px (the first is the contact) into <paramref name="points"/> and returns how many; 0 unless
    /// <see cref="GuideExtended"/>.
    /// </summary>
    public int GuideBeyond(double angleDegrees, Span<(double X, double Y)> points)
    {
        if (!GuideExtended || points.IsEmpty)
        {
            return 0;
        }

        var (dirX, dirY) = Direction(angleDegrees);
        var b = default(Ball);
        b.X = MoonfallRules.LauncherX + (dirX * MoonfallRules.BarrelLength);
        b.Y = MoonfallRules.LauncherY + (dirY * MoonfallRules.BarrelLength);
        b.Vx = dirX * MoonfallRules.LaunchSpeed;
        b.Vy = dirY * MoonfallRules.LaunchSpeed;
        var bucketX = BucketX;
        var count = 0;
        var bounced = false;
        var separated = false;
        double travelled = 0, sinceDot = 0;
        const int MaxSteps = 4000;
        var h = MoonfallRules.TickSeconds / 8;
        for (var step = 0; step < MaxSteps && count < points.Length; step++)
        {
            var lastX = b.X;
            var lastY = b.Y;
            b.X += b.Vx * h;
            b.Y += (b.Vy * h) + (0.5 * MoonfallRules.Gravity * h * h);
            b.Vy += MoonfallRules.Gravity * h;
            probe.Any = false;
            probe.Peg = -1;
            ResolveContacts(ref b, bucketX, 0, live: false);
            var touching = probe.Any || probe.Peg >= 0;
            if (b.Y >= MoonfallRules.CatchLine)
            {
                if (bounced)
                {
                    points[count++] = (b.X, b.Y);
                }

                break;
            }

            if (!bounced)
            {
                if (touching)
                {
                    bounced = true;
                    points[count++] = (b.X, b.Y);
                }

                continue;
            }

            var moved = MoonfallGeometry.Hypot(b.X - lastX, b.Y - lastY);
            travelled += moved;
            sinceDot += moved;
            if (!touching)
            {
                separated = true;
            }
            else if (separated)
            {
                points[count++] = (b.X, b.Y);
                break;
            }

            if (travelled >= MoonfallRules.SuperGuideMaxLength)
            {
                points[count++] = (b.X, b.Y);
                break;
            }

            if (sinceDot >= MoonfallRules.GuideDotSpacing)
            {
                sinceDot -= MoonfallRules.GuideDotSpacing;
                points[count++] = (b.X, b.Y);
            }
        }

        return count;
    }

    // ---- Sage's Path [R §5 l.116] ----

    /// <summary>The lines the path search weighs: every <see cref="MoonfallRules.PathStepDegrees"/> either side of the aim, and the aim.</summary>
    internal const int PathCandidates = (2 * (int)((MoonfallRules.PathSpreadDegrees / MoonfallRules.PathStepDegrees) + 0.5)) + 1;

    /// <summary>Each line's share of <see cref="MoonfallRules.PathSubStepBudget"/>: the most sub-steps one game tick of the search takes.</summary>
    internal const int PathSubStepsPerLine = MoonfallRules.PathSubStepBudget / PathCandidates;

    /// <summary>The next line the path search flies while the ball waits in the barrel, or −1 when none waits.</summary>
    private int pathCandidate = -1;
    private double pathAim;
    private double pathBest;
    private long pathBestValue;
    private long pathLaunchTick;

    /// <summary>The physics sub-steps the last path search took (all its flights), for the cost test.</summary>
    internal int LastPathSubSteps { get; private set; }

    /// <summary>The most sub-steps one game tick of the last path search took (one line's flight), for the cost test.</summary>
    internal int LastPathTickSubSteps { get; private set; }

    /// <summary>
    /// Sage's Path is weighing its lines: the ball waits in the barrel, at the aim, for <see cref="PathCandidates"/>
    /// game ticks, then leaves on the line chosen (<see cref="MoonfallEventKind.PathChosen"/>).
    /// </summary>
    public bool ChoosingPath => pathCandidate >= 0;

    /// <summary>
    /// Sage's Path: tries every <see cref="MoonfallRules.PathStepDegrees"/> within <see cref="MoonfallRules.PathSpreadDegrees"/>
    /// of the aim, nearest the aim first, flies each on a copy (the bucket sweeping as it will once the ball leaves, the
    /// movers where they are) within a shared budget of sub-steps, and launches on the best: the most valuable flight,
    /// the nearest the aim on a tie. A search of a full board is a few tens of milliseconds, too long for one frame, so it
    /// is spread over game ticks, one line a tick (<see cref="StepPath"/>), while the ball waits in the barrel: every shot
    /// with the power waits the same <see cref="PathCandidates"/> ticks, so the launch tick, and with it the bucket's
    /// place in every flight, is known from the start. Deterministic: the same calls give the same lines, and the cost of
    /// a tick is capped by <see cref="PathSubStepsPerLine"/>, of the search by <see cref="MoonfallRules.PathSubStepBudget"/>.
    /// </summary>
    private void BeginPath(double angleDegrees)
    {
        pathAim = Math.Clamp(angleDegrees, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees);
        pathCandidate = 0;
        pathBest = pathAim;
        pathBestValue = long.MinValue;
        pathLaunchTick = gameTick + PathCandidates;
        LastPathSubSteps = 0;
        LastPathTickSubSteps = 0;

        // The ball waits at the barrel's mouth.
        ref var b = ref ball;
        b.Vx = 0;
        b.Vy = 0;
    }

    /// <summary>One game tick of the path search: the next line; after the last, the ball leaves on the best.</summary>
    private void StepPath()
    {
        // 0, −step, +step, −2·step, …: the order breaks ties towards the aim.
        var c = pathCandidate;
        var offset = ((c + 1) / 2) * MoonfallRules.PathStepDegrees * (c % 2 == 1 ? -1 : 1);
        var angle = Math.Clamp(pathAim + offset, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees);
        var before = LastPathSubSteps;
        var value = FlyCandidate(angle, PathSubStepsPerLine, pathLaunchTick);
        LastPathTickSubSteps = Math.Max(LastPathTickSubSteps, LastPathSubSteps - before);
        if (value > pathBestValue)
        {
            pathBestValue = value;
            pathBest = angle;
        }

        if (++pathCandidate < PathCandidates)
        {
            return;
        }

        pathCandidate = -1;
        pathNudge = pathBest - pathAim;
        Post(MoonfallEventKind.PathChosen, -1, (long)Math.Round(pathNudge * 100), PathCandidates, MoonfallRules.LauncherX, MoonfallRules.LauncherY);

        // Off along the chosen line, from the barrel's mouth there, on the next tick.
        var (dirX, dirY) = Direction(pathBest);
        ref var b = ref ball;
        b.X = MoonfallRules.LauncherX + (dirX * MoonfallRules.BarrelLength);
        b.Y = MoonfallRules.LauncherY + (dirY * MoonfallRules.BarrelLength);
        b.PrevX = b.X;
        b.PrevY = b.Y;
        b.Vx = dirX * MoonfallRules.LaunchSpeed;
        b.Vy = dirY * MoonfallRules.LaunchSpeed;
        b.ResetWatch();
    }

    /// <summary>
    /// One candidate flight of the path search, leaving the barrel on the tick after <paramref name="launchTick"/>: what
    /// it would be worth (<see cref="MoonfallRules.PathOrangeWeight"/>). It stops at <paramref name="budget"/> sub-steps,
    /// checked before each one, so it never takes more.
    /// </summary>
    private long FlyCandidate(double angleDegrees, int budget, long launchTick)
    {
        var used = FlyProbe(angleDegrees, budget, launchTick, out var caught);
        LastPathSubSteps += used;
        return (probe.Value * probe.Pegs) + (probe.Oranges * MoonfallRules.PathOrangeWeight) + (caught ? MoonfallRules.PathCatchWeight : 0);
    }

    /// <summary>
    /// One flight off the live ball, leaving the barrel on the tick after <paramref name="launchTick"/>, recorded in
    /// <see cref="probe"/> (what it touched still unlit: their value, count and oranges); <paramref name="caught"/> when
    /// it lands in the bucket. It stops at <paramref name="budget"/> sub-steps, checked before each one, so it never
    /// takes more, and returns the sub-steps it took. Changes nothing a player sees.
    /// </summary>
    private int FlyProbe(double angleDegrees, int budget, long launchTick, out bool caught)
    {
        if (++stampId == int.MaxValue)
        {
            Array.Clear(stamp);
            stampId = 1;
        }

        var (dirX, dirY) = Direction(angleDegrees);
        var b = default(Ball);
        b.X = MoonfallRules.LauncherX + (dirX * MoonfallRules.BarrelLength);
        b.Y = MoonfallRules.LauncherY + (dirY * MoonfallRules.BarrelLength);
        b.Vx = dirX * MoonfallRules.LaunchSpeed;
        b.Vy = dirY * MoonfallRules.LaunchSpeed;
        probe = new Probe { Recording = true, Peg = -1 };
        caught = false;
        var used = 0;
        try
        {
            for (var t = 1; used < budget; t++)
            {
                var bucketX = MoonfallBucket.CentreAt(launchTick + t);
                var bucketV = MoonfallBucket.VelocityAt(launchTick + t);
                var speed = MoonfallGeometry.Hypot(b.Vx, b.Vy) + (MoonfallRules.Gravity * MoonfallRules.TickSeconds);
                var steps = Math.Clamp((int)Math.Ceiling(speed * MoonfallRules.TickSeconds / MoonfallRules.MaxSubStep), 1, MaxSubSteps);
                var h = MoonfallRules.TickSeconds / steps;
                for (var s = 0; s < steps; s++)
                {
                    // The budget is a hard cap: a tick's sub-steps never carry the flight past it.
                    if (used == budget)
                    {
                        return used;
                    }

                    used++;
                    var previousY = b.Y;
                    b.X += b.Vx * h;
                    b.Y += (b.Vy * h) + (0.5 * MoonfallRules.Gravity * h * h);
                    b.Vy += MoonfallRules.Gravity * h;
                    ResolveContacts(ref b, bucketX, bucketV, live: false);
                    if (previousY < MoonfallRules.CatchLine && b.Y >= MoonfallRules.CatchLine && Math.Abs(b.X - bucketX) <= MouthHalf)
                    {
                        caught = true;
                        return used;
                    }

                    if (b.Y > MoonfallRules.FloorExit)
                    {
                        return used;
                    }
                }
            }

            return used;
        }
        finally
        {
            probe.Recording = false;
        }
    }

    /// <summary>A flight off the live ball overlaps peg <paramref name="index"/>: noted, and counted once a flight while the path search records.</summary>
    private void NoteProbe(int index)
    {
        if (probe.Peg < 0)
        {
            probe.Peg = index;
        }

        if (!probe.Recording || stamp[index] == stampId)
        {
            return;
        }

        stamp[index] = stampId;
        ref readonly var b = ref bodies[index];
        if (b.Lit)
        {
            return;
        }

        probe.Value += MoonfallRules.BaseValue(b.Colour);
        probe.Pegs++;
        if (b.Colour == PegColour.Orange)
        {
            probe.Oranges++;
        }
    }

    // ---- Determinism ----

    private void AddPowersTo(ref FingerprintHash hash)
    {
        for (var p = 1; p < powerShots.Length; p++)
        {
            hash.Add(powerShots[p]);
            hash.Add(powerActive[p] ? 1 : 0);
        }

        hash.Add((long)Power);
        hash.Add(gateLeft);
        hash.Add(tripleShots);
        hash.Add(tripleThisShot ? 1 : 0);
        hash.Add(flipperInput ? 1 : 0);
        hash.Add(flipperLift);
        hash.Add(flipperLiftPrev);
        hash.Add(flipperRate);
        hash.Add(boltPointCount);
        hash.Add(boltFired ? 1 : 0);
        hash.Add(boltTick);
        hash.Add(pathNudge);
        hash.Add(pathCandidate);
        hash.Add(pathAim);
        hash.Add(pathBest);
        hash.Add(pathBestValue);
        hash.Add(pathLaunchTick);
        hash.Add(greenQueued);
        AddStyleTo(ref hash);
        AddDuelTo(ref hash);
    }
}
