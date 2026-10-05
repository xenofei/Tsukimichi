namespace Tsukimichi.Core.Moonfall;

/// <summary>A bounce as the tests read it: the peg (−1 for a wall or rim), where the ball's centre was, the normal, and the velocity in and out.</summary>
internal readonly record struct MoonfallBounce(int Peg, double X, double Y, double Nx, double Ny, double InX, double InY, double OutX, double OutY);

/// <summary>A peg as the window draws it this frame.</summary>
/// <param name="Shape">Round, straight or curved.</param>
/// <param name="X">A round peg's or curved brick's centre, a straight brick's first end (where it is now, for a mover).</param>
/// <param name="Y">See <paramref name="X"/>.</param>
/// <param name="X2">A straight brick's second end.</param>
/// <param name="Y2">See <paramref name="X2"/>.</param>
/// <param name="Radius">A round peg's radius, or a curved brick's middle radius.</param>
/// <param name="Thickness">A brick's thickness.</param>
/// <param name="StartRadians">A curved brick's first angle.</param>
/// <param name="SweepRadians">A curved brick's sweep.</param>
/// <param name="Colour">Its colour this turn.</param>
/// <param name="Lit">Hit this turn and waiting to clear.</param>
/// <param name="Cleared">Gone.</param>
public readonly record struct MoonfallPegView(
    PegShape Shape,
    double X,
    double Y,
    double X2,
    double Y2,
    double Radius,
    double Thickness,
    double StartRadians,
    double SweepRadians,
    PegColour Colour,
    bool Lit,
    bool Cleared);

/// <summary>
/// One level of Moonfall (feature plan v9 G1–G3): the board, the ball, the bucket, scoring and Fever, as pure code with
/// no ImGui. It runs on a fixed 100 Hz clock (<see cref="MoonfallRules.TicksPerSecond"/>): <see cref="Advance"/> takes
/// wall-clock seconds and runs whole 10 ms <em>real</em> ticks (<see cref="Tick"/>), and each real tick moves game time
/// by the current speed (1 normally, 1/10 in the approach to the last orange, rising to 1/2 after it), running a 10 ms
/// <em>game</em> tick of physics whenever a whole one has built up. The speed is counted in thousandths, so the same
/// calls give the same game, bit for bit, on every run: the oranges, greens and purples come from the seed
/// (<see cref="MoonfallRandom"/>), and nothing reads a clock.
/// <para>
/// Steady state allocates nothing: every array is made when the level starts, events go into a fixed ring
/// (<see cref="TryReadEvent"/>), and the guide writes into the caller's span.
/// </para>
/// <para>
/// Extension points: powers (G5) attach to <see cref="MoonfallEventKind.GreenHit"/>; style shots (G4) read the event
/// stream (wall and bucket bounces, hits with their positions); modes (G7) set the balls, the level number and the seed.
/// </para>
/// </summary>
public sealed class MoonfallGame
{
    private const int EventCapacity = 256;
    private const int MaxIterations = 4;
    private const int MaxSubSteps = 16;
    private const int SpeedOne = 1000;

    private readonly Body[] bodies;
    private readonly int[] hitOrder;
    private readonly MoonfallEvent[] events = new MoonfallEvent[EventCapacity];
    private int eventHead;
    private int eventCount;

    private MoonfallRandom random;
    private ScoreCounter counter;

    private long gameTick;
    private long realTick;
    private int speedMilli = SpeedOne;
    private int speedAccumulator;
    private double realAccumulator;

    private double ballX;
    private double ballY;
    private double ballVx;
    private double ballVy;

    private int hitCount;
    private int purple = -1;
    private long shotValue;
    private int shotPegs;
    private int freeBallsThisShot;
    private long levelScore;

    private int clearWait;
    private int clearCursor;
    private int clearedThisTurn;
    private int nextBallWait;
    private bool turnScored;
    private bool lostThisTurn;

    private double stuckX;
    private double stuckY;
    private int stuckTicks;
    private double lowestY;
    private int sinkTicks;

    private int lastOrange = -1;
    private bool approach;
    private bool feverHit;
    private bool feverLanded;
    private int feverRealTicks;
    private int feverBucket = -1;
    private long feverBonus;
    private double zoom = 1;
    private double zoomAtHit = 1;
    private double focusX = MoonfallRules.Width * 0.5;
    private double focusY = MoonfallRules.Height * 0.5;
    private double hitFocusX;
    private double hitFocusY;

    /// <param name="level">The level to play.</param>
    /// <param name="levelNumber">Its number in its campaign, from 1: green pegs appear from <see cref="MoonfallRules.FirstGreenLevel"/>.</param>
    /// <param name="seed">Picks the oranges, greens and purples; the same seed gives the same board.</param>
    /// <param name="balls">Balls to start with (<see cref="MoonfallRules.BallsPerLevel"/>; a challenge may give fewer).</param>
    public MoonfallGame(MoonfallLevel level, int levelNumber, ulong seed, int balls = MoonfallRules.BallsPerLevel)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfLessThan(levelNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(balls, 1);
        Level = level;
        LevelNumber = levelNumber;
        BallsLeft = balls;
        random = new MoonfallRandom(seed);
        bodies = new Body[level.Pegs.Count];
        hitOrder = new int[level.Pegs.Count];
        for (var i = 0; i < bodies.Length; i++)
        {
            bodies[i] = Body.From(level.Pegs[i]);
        }

        PickColours();
        UpdateMovers();
        StartTurn();
        counter.Reset(0);
    }

    // ---- What the window reads ----

    public MoonfallLevel Level { get; }

    public int LevelNumber { get; }

    public MoonfallPhase Phase { get; private set; } = MoonfallPhase.Aiming;

    /// <summary>Balls left in the launcher, not counting one in play.</summary>
    public int BallsLeft { get; private set; }

    /// <summary>Orange pegs not yet hit.</summary>
    public int OrangesLeft { get; private set; }

    /// <summary>The Fever meter's multiplier now (<see cref="MoonfallRules.Multiplier"/>).</summary>
    public int Multiplier => MoonfallRules.Multiplier(OrangesLeft);

    /// <summary>The level's score so far (every scored shot, and the end bonuses once won).</summary>
    public long Score { get; private set; }

    /// <summary>The score counter's value now: it counts up to <see cref="Score"/> by the measured rule.</summary>
    public long ShownScore => counter.Shown;

    /// <summary>This shot's peg values so far, each with the multiplier it was hit at.</summary>
    public long ShotValue => shotValue;

    /// <summary>This shot's pegs hit so far.</summary>
    public int ShotPegs => shotPegs;

    /// <summary>This shot's score: <see cref="ShotValue"/> × <see cref="ShotPegs"/> [M correction].</summary>
    public long ShotScore => shotValue * shotPegs;

    /// <summary>Free balls this shot's score has earned (0–3).</summary>
    public int FreeBallsThisShot => freeBallsThisShot;

    /// <summary>How many of this turn's lit pegs have cleared (the tally counts "× N pegs" up with them).</summary>
    public int ClearedThisTurn => clearedThisTurn;

    /// <summary>Whether a ball is in play.</summary>
    public bool BallInPlay => Phase == MoonfallPhase.Flying;

    public double BallX => ballX;

    public double BallY => ballY;

    public double BallVelocityX => ballVx;

    public double BallVelocityY => ballVy;

    /// <summary>Game ticks since the level began (the bucket's and the movers' clock).</summary>
    public long GameTick => gameTick;

    /// <summary>Real ticks since the level began.</summary>
    public long RealTick => realTick;

    /// <summary>The bucket's centre now.</summary>
    public double BucketX => MoonfallBucket.CentreAt(gameTick);

    /// <summary>Game speed now: 1 normally, 0.1 in the approach, rising to 0.5 after the last orange.</summary>
    public double Speed => speedMilli / (double)SpeedOne;

    /// <summary>The slow-motion approach to the last orange is on.</summary>
    public bool Approaching => approach && !feverHit;

    /// <summary>The last orange has been hit: Fever is on and the five Fever buckets replace the moving one.</summary>
    public bool Fever => feverHit;

    /// <summary>Every peg was lit or gone when the last orange was hit: all five Fever buckets pay 100,000.</summary>
    public bool Perfect { get; private set; }

    /// <summary>How far the Fever buckets have come up (0–1), for drawing; they catch the ball from the hit on.</summary>
    public double FeverBucketsShown => feverHit ? Math.Min(1.0, feverRealTicks / (double)MoonfallRules.FeverBucketsShowTicks) : 0.0;

    /// <summary>The Fever banner shows (0.08 s after the hit, for 2.95 s [M 8]).</summary>
    public bool BannerVisible => feverHit && feverRealTicks >= MoonfallRules.BannerDelayTicks && feverRealTicks < MoonfallRules.BannerDelayTicks + MoonfallRules.BannerTicks;

    /// <summary>The Fever bucket the ball landed in (0–4), or −1.</summary>
    public int FeverBucket => feverBucket;

    /// <summary>The camera's zoom (1–2): the window ignores it under Reduce motion.</summary>
    public double Zoom => zoom;

    /// <summary>The board point the zoom centres on.</summary>
    public double FocusX => focusX;

    /// <summary>See <see cref="FocusX"/>.</summary>
    public double FocusY => focusY;

    /// <summary>The end-of-level tally once <see cref="Phase"/> is <see cref="MoonfallPhase.Won"/>.</summary>
    public MoonfallTally? Tally { get; private set; }

    /// <summary>The pegs and bricks, in the level's order.</summary>
    public int PegCount => bodies.Length;

    /// <summary>Peg <paramref name="index"/> as it is now.</summary>
    public MoonfallPegView Peg(int index)
    {
        ref readonly var b = ref bodies[index];
        return new MoonfallPegView(b.Shape, b.X, b.Y, b.X + b.Dx, b.Y + b.Dy, b.Radius, b.Half * 2, b.Start, b.Sweep, b.Colour, b.Lit, b.Cleared);
    }

    /// <summary>The peg that is purple this turn, or −1.</summary>
    public int PurplePeg => purple;

    /// <summary>Takes the oldest event not read yet; false when there is none. The ring keeps the latest 256.</summary>
    public bool TryReadEvent(out MoonfallEvent e)
    {
        if (eventCount == 0)
        {
            e = default;
            return false;
        }

        e = events[eventHead];
        eventHead = (eventHead + 1) % EventCapacity;
        eventCount--;
        return true;
    }

    // ---- Input ----

    /// <summary>The aim angle (degrees from straight down, positive to the right, within the limit) towards a board point.</summary>
    public static double AimAt(double x, double y)
    {
        var dx = x - MoonfallRules.LauncherX;
        var dy = y - MoonfallRules.LauncherY;
        var degrees = (dx == 0 && dy == 0) ? 0 : Math.Atan2(dx, dy) * (180 / Math.PI);
        return Math.Clamp(degrees, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees);
    }

    /// <summary>Fires the ball at <paramref name="angleDegrees"/> (clamped to the limit); false unless a ball is waiting.</summary>
    public bool Shoot(double angleDegrees)
    {
        if (Phase != MoonfallPhase.Aiming || BallsLeft <= 0 || !double.IsFinite(angleDegrees))
        {
            return false;
        }

        var (dirX, dirY) = Direction(angleDegrees);
        BallsLeft--;
        ballX = MoonfallRules.LauncherX + (dirX * MoonfallRules.BarrelLength);
        ballY = MoonfallRules.LauncherY + (dirY * MoonfallRules.BarrelLength);
        ballVx = dirX * MoonfallRules.LaunchSpeed;
        ballVy = dirY * MoonfallRules.LaunchSpeed;
        shotValue = 0;
        shotPegs = 0;
        freeBallsThisShot = 0;
        hitCount = 0;
        clearedThisTurn = 0;
        stuckX = ballX;
        stuckY = ballY;
        stuckTicks = 0;
        lowestY = ballY;
        sinkTicks = 0;
        Phase = MoonfallPhase.Flying;
        return true;
    }

    /// <summary>The unit direction of an aim angle (0° straight down).</summary>
    public static (double X, double Y) Direction(double angleDegrees)
    {
        var radians = MoonfallGeometry.Radians(Math.Clamp(angleDegrees, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees));
        return (Math.Sin(radians), Math.Cos(radians));
    }

    // ---- Time ----

    /// <summary>
    /// Runs whole real ticks for <paramref name="realSeconds"/> of wall-clock time (at most 0.25 s at once, so a hitch
    /// never fast-forwards the board); the remainder carries to the next call.
    /// </summary>
    public void Advance(double realSeconds)
    {
        if (!double.IsFinite(realSeconds) || realSeconds <= 0)
        {
            return;
        }

        // A whole tick is taken with a hair to spare, so 1/60 s steps add up to exactly 5 ticks every 3 frames.
        realAccumulator += Math.Min(realSeconds, 0.25);
        while (realAccumulator >= MoonfallRules.TickSeconds - 1e-9)
        {
            realAccumulator -= MoonfallRules.TickSeconds;
            Tick();
        }
    }

    /// <summary>One 10 ms real tick: the counter, the zoom and the speed, then game ticks for the time built up.</summary>
    public void Tick()
    {
        realTick++;
        counter.Tick();
        TickPresentation();
        speedAccumulator += speedMilli;
        while (speedAccumulator >= SpeedOne)
        {
            speedAccumulator -= SpeedOne;
            StepGame();
        }
    }

    private void TickPresentation()
    {
        if (feverHit)
        {
            feverRealTicks++;
            zoom = Math.Max(1.0, zoom - (MoonfallRules.ZoomOutPerSecond * MoonfallRules.TickSeconds));
            var back = zoomAtHit > 1.0 ? 1.0 - ((zoom - 1.0) / (zoomAtHit - 1.0)) : 1.0;
            focusX = hitFocusX + ((MoonfallRules.Width * 0.5 - hitFocusX) * back);
            focusY = hitFocusY + ((MoonfallRules.Height * 0.5 - hitFocusY) * back);
            if (!feverLanded)
            {
                var ramp = Math.Min(feverRealTicks, MoonfallRules.FeverRampTicks);
                speedMilli = MoonfallRules.ApproachSpeedMilli + ((MoonfallRules.FeverSpeedMilli - MoonfallRules.ApproachSpeedMilli) * ramp / MoonfallRules.FeverRampTicks);
            }

            return;
        }

        if (approach)
        {
            zoom = Math.Min(MoonfallRules.ZoomMax, zoom + ((MoonfallRules.ZoomMax - 1.0) / MoonfallRules.ZoomInTicks));
            if (lastOrange >= 0)
            {
                ref readonly var target = ref bodies[lastOrange];
                focusX = (ballX + target.BoundX) * 0.5;
                focusY = (ballY + target.BoundY) * 0.5;
            }

            return;
        }

        zoom = Math.Max(1.0, zoom - ((MoonfallRules.ZoomMax - 1.0) / MoonfallRules.ZoomCancelTicks));
    }

    /// <summary>One 10 ms game tick: the bucket and movers advance, then the ball or the clearing.</summary>
    private void StepGame()
    {
        gameTick++;
        UpdateMovers();
        switch (Phase)
        {
            case MoonfallPhase.Flying:
                StepBall();
                if (Phase == MoonfallPhase.Flying)
                {
                    WatchStuck();
                    WatchApproach();
                }

                break;

            case MoonfallPhase.Clearing:
                StepClearing();
                break;
        }
    }

    // ---- The ball ----

    private void StepBall()
    {
        var speed = MoonfallGeometry.Hypot(ballVx, ballVy) + (MoonfallRules.Gravity * MoonfallRules.TickSeconds);
        var steps = Math.Clamp((int)Math.Ceiling(speed * MoonfallRules.TickSeconds / MoonfallRules.MaxSubStep), 1, MaxSubSteps);
        var h = MoonfallRules.TickSeconds / steps;
        var bucketX = BucketX;
        var bucketV = feverHit ? 0.0 : MoonfallBucket.VelocityAt(gameTick);
        for (var s = 0; s < steps; s++)
        {
            var previousY = ballY;
            ballX += ballVx * h;
            ballY += (ballVy * h) + (0.5 * MoonfallRules.Gravity * h * h);
            ballVy += MoonfallRules.Gravity * h;
            ResolveContacts(bucketX, bucketV);
            if (previousY < MoonfallRules.CatchLine && ballY >= MoonfallRules.CatchLine)
            {
                if (feverHit)
                {
                    LandInFeverBucket();
                    return;
                }

                if (Math.Abs(ballX - bucketX) <= MoonfallRules.BucketMouth * 0.5)
                {
                    CatchInBucket();
                    return;
                }
            }

            if (ballY > MoonfallRules.FloorExit)
            {
                LoseBall();
                return;
            }
        }
    }

    private enum ContactKind : byte
    {
        None,
        Peg,
        Wall,
        Rim,
    }

    /// <summary>One surface the ball overlaps in a sub-step.</summary>
    private struct Contact
    {
        public ContactKind Kind;
        public int Index;
        public double Depth;
        public double Nx;
        public double Ny;
        public double Vx;
        public double Vy;
    }

    private const int MaxContacts = 8;
    private readonly Contact[] contacts = new Contact[MaxContacts];
    private int contactCount;

    /// <summary>
    /// Pushes the ball out of whatever it overlaps and bounces it [M 4]. Surfaces touched in the same sub-step are taken
    /// together: the bounce is about their normals summed by depth, with the deepest one's material, so a ball dropped
    /// into the notch between two pegs bounces straight back up as it would off one surface, instead of being kicked
    /// sideways by whichever peg happened to be resolved first. With one contact this is the plain bounce.
    /// </summary>
    private void ResolveContacts(double bucketX, double bucketV)
    {
        const double r = MoonfallRules.BallRadius;
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            contactCount = 0;

            // Walls and ceiling.
            AddContact(ContactKind.Wall, -1, MoonfallRules.LeftWall + r - ballX, 1, 0, 0, 0);
            AddContact(ContactKind.Wall, -1, ballX - (MoonfallRules.RightWall - r), -1, 0, 0, 0);
            AddContact(ContactKind.Wall, -1, MoonfallRules.Ceiling + r - ballY, 0, 1, 0, 0);

            // The bucket's rims, or the Fever buckets' posts.
            if (ballY > MoonfallRules.BucketTop - (2 * r) - 20)
            {
                if (feverHit)
                {
                    for (var k = 1; k < 5; k++)
                    {
                        var postX = MoonfallRules.LeftWall + (k * MoonfallBucket.FeverBucketWidth);
                        PostContact(postX, MoonfallRules.BucketTop + MoonfallRules.FeverPostRadius, MoonfallRules.FeverPostRadius, 0);
                    }
                }
                else
                {
                    PostContact(bucketX - MoonfallBucket.RimOffset, MoonfallRules.BucketTop + MoonfallBucket.RimRadius, MoonfallBucket.RimRadius, bucketV);
                    PostContact(bucketX + MoonfallBucket.RimOffset, MoonfallRules.BucketTop + MoonfallBucket.RimRadius, MoonfallBucket.RimRadius, bucketV);
                }
            }

            // Pegs and bricks.
            for (var i = 0; i < bodies.Length; i++)
            {
                ref readonly var b = ref bodies[i];
                if (!b.Cleared && b.Near(ballX, ballY) && b.Overlap(ballX, ballY, out var pnx, out var pny, out var pdepth))
                {
                    AddContact(ContactKind.Peg, i, pdepth, pnx, pny, b.Vx, b.Vy);
                }
            }

            if (contactCount == 0)
            {
                return;
            }

            // The deepest contact gives the material and the surface's motion; the normals add up by depth.
            var deepest = 0;
            double sx = 0, sy = 0;
            for (var c = 0; c < contactCount; c++)
            {
                ref readonly var contact = ref contacts[c];
                sx += contact.Nx * contact.Depth;
                sy += contact.Ny * contact.Depth;
                if (contact.Depth > contacts[deepest].Depth)
                {
                    deepest = c;
                }
            }

            var main = contacts[deepest];
            var length = MoonfallGeometry.Hypot(sx, sy);
            double nx = main.Nx, ny = main.Ny;
            if (contactCount > 1 && length > 1e-9)
            {
                nx = sx / length;
                ny = sy / length;
            }

            // Out along that normal just far enough to clear every contact it faces (at most twice the deepest).
            var push = main.Depth;
            for (var c = 0; c < contactCount; c++)
            {
                ref readonly var contact = ref contacts[c];
                var facing = (contact.Nx * nx) + (contact.Ny * ny);
                if (facing > 1e-3)
                {
                    push = Math.Max(push, Math.Min(contact.Depth / facing, 2 * main.Depth));
                }
            }

            ballX += nx * push;
            ballY += ny * push;
            var peg = main.Kind == ContactKind.Peg;
            var inX = ballVx;
            var inY = ballVy;
            var bounced = Bounce(
                nx,
                ny,
                main.Vx,
                main.Vy,
                peg ? MoonfallRules.PegNormalRestitution : MoonfallRules.WallRestitution,
                peg ? MoonfallRules.PegTangentRestitution : MoonfallRules.WallRestitution);
            if (bounced)
            {
                LastBounce = new MoonfallBounce(peg ? main.Index : -1, ballX, ballY, nx, ny, inX, inY, ballVx, ballVy);
            }

            var wall = false;
            var rim = false;
            for (var c = 0; c < contactCount; c++)
            {
                switch (contacts[c].Kind)
                {
                    case ContactKind.Peg:
                        Light(contacts[c].Index);
                        break;
                    case ContactKind.Wall:
                        wall = true;
                        break;
                    case ContactKind.Rim:
                        rim = true;
                        break;
                }
            }

            if (bounced && wall)
            {
                Post(MoonfallEventKind.WallBounce, -1, 0, 0, ballX, ballY);
            }

            if (bounced && rim)
            {
                Post(MoonfallEventKind.BucketBounce, -1, 0, 0, ballX, ballY);
            }
        }
    }

    /// <summary>Notes a contact of <paramref name="depth"/> (none at 0 or less); past <see cref="MaxContacts"/> the shallowest gives way.</summary>
    private void AddContact(ContactKind kind, int index, double depth, double nx, double ny, double vx, double vy)
    {
        if (depth <= 0)
        {
            return;
        }

        var slot = contactCount;
        if (contactCount == MaxContacts)
        {
            slot = 0;
            for (var c = 1; c < MaxContacts; c++)
            {
                if (contacts[c].Depth < contacts[slot].Depth)
                {
                    slot = c;
                }
            }

            if (contacts[slot].Depth >= depth)
            {
                return;
            }
        }
        else
        {
            contactCount++;
        }

        contacts[slot] = new Contact { Kind = kind, Index = index, Depth = depth, Nx = nx, Ny = ny, Vx = vx, Vy = vy };
    }

    /// <summary>A rim or post: a vertical capsule from its top cap's centre down past the floor.</summary>
    private void PostContact(double x, double topY, double radius, double vx)
    {
        var qy = Math.Max(ballY, topY);
        var dx = ballX - x;
        var dy = ballY - qy;
        var reach = radius + MoonfallRules.BallRadius;
        var d2 = (dx * dx) + (dy * dy);
        if (d2 >= reach * reach)
        {
            return;
        }

        var d = Math.Sqrt(d2);
        var (cnx, cny) = d > 1e-9 ? (dx / d, dy / d) : (0.0, -1.0);
        AddContact(ContactKind.Rim, -1, reach - d, cnx, cny, vx, 0);
    }

    /// <summary>
    /// Reflects the ball's velocity, relative to the surface's own, about the normal: the normal part times
    /// <paramref name="en"/>, the rest times <paramref name="et"/>. Below <see cref="MoonfallRules.RestingSpeed"/> the
    /// approach is only cancelled (a resting contact). True for a real bounce.
    /// </summary>
    private bool Bounce(double nx, double ny, double ovx, double ovy, double en, double et)
    {
        var rvx = ballVx - ovx;
        var rvy = ballVy - ovy;
        var vn = (rvx * nx) + (rvy * ny);
        if (vn >= 0)
        {
            return false;
        }

        var tx = rvx - (vn * nx);
        var ty = rvy - (vn * ny);
        if (-vn < MoonfallRules.RestingSpeed)
        {
            ballVx = tx + ovx;
            ballVy = ty + ovy;
            return false;
        }

        ballVx = (tx * et) - (vn * en * nx) + ovx;
        ballVy = (ty * et) - (vn * en * ny) + ovy;
        return true;
    }

    // ---- Hits and scoring ----

    private void Light(int index)
    {
        ref var b = ref bodies[index];
        if (b.Lit || b.Cleared)
        {
            return;
        }

        // The multiplier in force when the peg is touched, before this touch counts (MoonfallRules.Multiplier).
        var value = (long)MoonfallRules.BaseValue(b.Colour) * MoonfallRules.Multiplier(OrangesLeft);
        b.Lit = true;
        b.HitIndex = hitCount;
        hitOrder[hitCount++] = index;
        shotValue += value;
        shotPegs++;
        Post(MoonfallEventKind.PegHit, index, value, shotPegs, b.BoundX, b.BoundY);
        if (b.Colour == PegColour.Green)
        {
            Post(MoonfallEventKind.GreenHit, index, value, shotPegs, b.BoundX, b.BoundY);
        }

        AwardShotFreeBalls();
        if (b.Colour == PegColour.Orange)
        {
            OrangesLeft--;
            if (OrangesLeft == 0)
            {
                StartFever(index);
            }
        }
    }

    private void AwardShotFreeBalls()
    {
        var thresholds = MoonfallRules.FreeBallThresholds;
        var score = ShotScore;
        while (freeBallsThisShot < thresholds.Length && score >= thresholds[freeBallsThisShot])
        {
            BallsLeft++;
            Post(MoonfallEventKind.FreeBall, -1, thresholds[freeBallsThisShot], freeBallsThisShot + 1, ballX, ballY);
            freeBallsThisShot++;
        }
    }

    // ---- The stuck ball ----

    private void WatchStuck()
    {
        // Sinking: a new low point (by StuckRadius) restarts the hollow watch.
        if (ballY > lowestY + MoonfallRules.StuckRadius)
        {
            lowestY = ballY;
            sinkTicks = 0;
        }
        else
        {
            sinkTicks++;
        }

        if (MoonfallGeometry.Hypot(ballX - stuckX, ballY - stuckY) > MoonfallRules.StuckRadius)
        {
            stuckX = ballX;
            stuckY = ballY;
            stuckTicks = 0;
        }
        else
        {
            stuckTicks++;
        }

        if ((stuckTicks >= MoonfallRules.StuckTicks || sinkTicks >= MoonfallRules.StuckSinkTicks) && ClearStuckPeg())
        {
            stuckTicks = 0;
            stuckX = ballX;
            stuckY = ballY;
            sinkTicks = 0;
            lowestY = ballY;
        }
    }

    /// <summary>Clears early the lit peg the ball touches that was hit last; false when it touches none (it is in the air).</summary>
    private bool ClearStuckPeg()
    {
        var pick = -1;
        var pickOrder = -1;
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (!b.Lit || b.Cleared || !b.Near(ballX, ballY, MoonfallRules.StuckTouchSlack))
            {
                continue;
            }

            if (b.Gap(ballX, ballY) <= MoonfallRules.StuckTouchSlack && b.HitIndex > pickOrder)
            {
                pick = i;
                pickOrder = b.HitIndex;
            }
        }

        if (pick < 0)
        {
            return false;
        }

        bodies[pick].Cleared = true;
        clearedThisTurn++;
        Post(MoonfallEventKind.StuckClear, pick, 0, clearedThisTurn, bodies[pick].BoundX, bodies[pick].BoundY);
        return true;
    }

    // ---- Fever ----

    private void WatchApproach()
    {
        if (feverHit || OrangesLeft != 1)
        {
            return;
        }

        if (lastOrange < 0 || bodies[lastOrange].Lit || bodies[lastOrange].Cleared)
        {
            lastOrange = FindLastOrange();
        }

        var predicted = lastOrange >= 0 && PredictsTouch(lastOrange, MoonfallRules.FeverLookaheadTicks);
        if (predicted && !approach)
        {
            approach = true;
            speedMilli = MoonfallRules.ApproachSpeedMilli;
            Post(MoonfallEventKind.FeverApproach, lastOrange, 0, 0, ballX, ballY);
        }
        else if (!predicted && approach)
        {
            approach = false;
            speedMilli = SpeedOne;
            Post(MoonfallEventKind.FeverApproachEnded, lastOrange, 0, 0, ballX, ballY);
        }
    }

    private int FindLastOrange()
    {
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (b.Colour == PegColour.Orange && !b.Lit && !b.Cleared)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Whether the ball's free flight touches peg <paramref name="target"/> first, within <paramref name="ticks"/> ticks.</summary>
    private bool PredictsTouch(int target, int ticks)
    {
        double x = ballX, y = ballY, vx = ballVx, vy = ballVy;
        const double r = MoonfallRules.BallRadius;
        for (var t = 0; t < ticks; t++)
        {
            var speed = MoonfallGeometry.Hypot(vx, vy) + (MoonfallRules.Gravity * MoonfallRules.TickSeconds);
            var steps = Math.Clamp((int)Math.Ceiling(speed * MoonfallRules.TickSeconds / MoonfallRules.MaxSubStep), 1, MaxSubSteps);
            var h = MoonfallRules.TickSeconds / steps;
            for (var s = 0; s < steps; s++)
            {
                x += vx * h;
                y += (vy * h) + (0.5 * MoonfallRules.Gravity * h * h);
                vy += MoonfallRules.Gravity * h;
                if (x < MoonfallRules.LeftWall + r || x > MoonfallRules.RightWall - r || y < MoonfallRules.Ceiling + r || y > MoonfallRules.CatchLine)
                {
                    return false;
                }

                for (var i = 0; i < bodies.Length; i++)
                {
                    ref readonly var b = ref bodies[i];
                    if (!b.Cleared && b.Near(x, y) && b.Overlap(x, y, out _, out _, out _))
                    {
                        return i == target;
                    }
                }
            }
        }

        return false;
    }

    private void StartFever(int index)
    {
        feverHit = true;
        approach = false;
        feverRealTicks = 0;
        zoomAtHit = zoom;
        hitFocusX = focusX;
        hitFocusY = focusY;
        speedMilli = MoonfallRules.ApproachSpeedMilli;
        var perfect = true;
        for (var i = 0; i < bodies.Length; i++)
        {
            if (!bodies[i].Lit && !bodies[i].Cleared)
            {
                perfect = false;
                break;
            }
        }

        Perfect = perfect;
        Post(MoonfallEventKind.FeverHit, index, perfect ? 1 : 0, shotPegs, bodies[index].BoundX, bodies[index].BoundY);
    }

    private void LandInFeverBucket()
    {
        feverBucket = MoonfallBucket.FeverBucketAt(ballX);
        feverBonus = Perfect ? MoonfallRules.PerfectFeverBucketValue : MoonfallRules.FeverBucketValues[feverBucket];
        feverLanded = true;
        speedMilli = SpeedOne;
        Post(MoonfallEventKind.FeverLanded, feverBucket, feverBonus, 0, ballX, ballY);
        EndTurn();
    }

    // ---- The end of a turn ----

    private void CatchInBucket()
    {
        BallsLeft++;
        Post(MoonfallEventKind.BucketCatch, -1, 0, 0, ballX, ballY);
        Post(MoonfallEventKind.FreeBall, -1, 0, freeBallsThisShot, ballX, ballY);
        EndTurn();
    }

    private void LoseBall()
    {
        Post(MoonfallEventKind.BallLost, -1, 0, 0, ballX, ballY);
        EndTurn();
    }

    private void EndTurn()
    {
        if (approach)
        {
            approach = false;
            speedMilli = SpeedOne;
        }

        Phase = MoonfallPhase.Clearing;
        clearWait = MoonfallRules.ClearDelayTicks;
        clearCursor = 0;
        nextBallWait = 0;
        turnScored = false;
        lostThisTurn = false;
    }

    private void StepClearing()
    {
        if (turnScored)
        {
            if (--nextBallWait <= 0)
            {
                StartTurn();
                Phase = MoonfallPhase.Aiming;
                Post(MoonfallEventKind.NextBall, -1, BallsLeft, 0, MoonfallRules.LauncherX, MoonfallRules.LauncherY);
            }

            return;
        }

        if (--clearWait > 0)
        {
            return;
        }

        // The next lit peg in hit order that a stuck ball has not already taken.
        while (clearCursor < hitCount && bodies[hitOrder[clearCursor]].Cleared)
        {
            clearCursor++;
        }

        if (clearCursor < hitCount)
        {
            var index = hitOrder[clearCursor++];
            bodies[index].Cleared = true;
            clearedThisTurn++;
            Post(MoonfallEventKind.PegCleared, index, 0, clearedThisTurn, bodies[index].BoundX, bodies[index].BoundY);
            clearWait = MoonfallRules.ClearIntervalTicks;

            // The turn is scored with its last pop, not a pop later.
            while (clearCursor < hitCount && bodies[hitOrder[clearCursor]].Cleared)
            {
                clearCursor++;
            }

            if (clearCursor < hitCount)
            {
                return;
            }
        }

        ScoreTurn();
    }

    private void ScoreTurn()
    {
        turnScored = true;
        var shot = ShotScore;
        levelScore += shot;
        Score = levelScore;
        Post(MoonfallEventKind.ShotScored, -1, shot, shotPegs, ballX, ballY);
        if (feverHit)
        {
            var ballBonus = (long)BallsLeft * MoonfallRules.UnusedBallBonus;
            var total = levelScore + feverBonus + ballBonus;
            Tally = new MoonfallTally(levelScore, feverBonus, Perfect, BallsLeft, ballBonus, total);
            Score = total;
            counter.SetTarget(total, MoonfallRules.CountUpHoldTicks);
            Phase = MoonfallPhase.Won;
            Post(MoonfallEventKind.LevelWon, -1, total, BallsLeft, 0, 0);
            return;
        }

        counter.SetTarget(Score, MoonfallRules.CountUpHoldTicks);
        if (BallsLeft <= 0 && OrangesLeft > 0)
        {
            lostThisTurn = true;
            Phase = MoonfallPhase.Lost;
            Post(MoonfallEventKind.LevelLost, -1, Score, OrangesLeft, 0, 0);
            return;
        }

        nextBallWait = MoonfallRules.NextBallTicks;
    }

    /// <summary>Whether the last turn ended the level without a win.</summary>
    public bool OutOfBalls => lostThisTurn;

    // ---- Colours ----

    private void PickColours()
    {
        Span<int> pool = bodies.Length <= 512 ? stackalloc int[bodies.Length] : new int[bodies.Length];
        var n = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].CanBeOrange)
            {
                pool[n++] = i;
            }
        }

        var oranges = Math.Min(MoonfallRules.OrangeCount, n);
        for (var k = 0; k < oranges; k++)
        {
            var pick = k + random.Next(n - k);
            (pool[k], pool[pick]) = (pool[pick], pool[k]);
            bodies[pool[k]].Colour = PegColour.Orange;
        }

        OrangesLeft = oranges;
        if (LevelNumber < MoonfallRules.FirstGreenLevel)
        {
            return;
        }

        n = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].Colour == PegColour.Blue)
            {
                pool[n++] = i;
            }
        }

        var greens = Math.Min(MoonfallRules.GreenCount, n);
        for (var k = 0; k < greens; k++)
        {
            var pick = k + random.Next(n - k);
            (pool[k], pool[pick]) = (pool[pick], pool[k]);
            bodies[pool[k]].Colour = PegColour.Green;
        }
    }

    /// <summary>A new turn: the purple peg moves to another blue one still standing [R §2: "location changes every shot"].</summary>
    private void StartTurn()
    {
        if (purple >= 0 && !bodies[purple].Lit && !bodies[purple].Cleared)
        {
            bodies[purple].Colour = PegColour.Blue;
        }

        var candidates = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (b.Colour == PegColour.Blue && !b.Lit && !b.Cleared && i != purple)
            {
                candidates++;
            }
        }

        if (candidates == 0)
        {
            purple = -1;
            return;
        }

        var pick = random.Next(candidates);
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (b.Colour == PegColour.Blue && !b.Lit && !b.Cleared && i != purple && pick-- == 0)
            {
                purple = i;
                bodies[i].Colour = PegColour.Purple;
                return;
            }
        }
    }

    private void UpdateMovers()
    {
        var seconds = gameTick * MoonfallRules.TickSeconds;
        for (var i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].Mover != MoverKind.None)
            {
                bodies[i].Move(seconds);
            }
        }
    }

    // ---- The aim guide [M 11] ----

    /// <summary>
    /// The aim guide for <paramref name="angleDegrees"/>: dots every <see cref="MoonfallRules.GuideDotSpacing"/> px
    /// along the ball's path from the barrel, ending where it would first touch a peg or after
    /// <see cref="MoonfallRules.GuideMaxLength"/> px. Writes into <paramref name="dots"/> (at most its length, and
    /// <see cref="MoonfallRules.GuideMaxDots"/> are enough) and returns how many it wrote.
    /// </summary>
    public int Guide(double angleDegrees, Span<(double X, double Y)> dots)
    {
        if (dots.IsEmpty)
        {
            return 0;
        }

        var (dirX, dirY) = Direction(angleDegrees);
        var x = MoonfallRules.LauncherX + (dirX * MoonfallRules.BarrelLength);
        var y = MoonfallRules.LauncherY + (dirY * MoonfallRules.BarrelLength);
        var vx = dirX * MoonfallRules.LaunchSpeed;
        var vy = dirY * MoonfallRules.LaunchSpeed;
        var count = 0;
        dots[count++] = (x, y);
        double travelled = 0, sinceDot = 0;
        const double r = MoonfallRules.BallRadius;
        while (travelled < MoonfallRules.GuideMaxLength && count < dots.Length)
        {
            var h = MoonfallRules.TickSeconds / 8;
            var nx = x + (vx * h);
            var ny = y + (vy * h) + (0.5 * MoonfallRules.Gravity * h * h);
            vy += MoonfallRules.Gravity * h;
            var step = MoonfallGeometry.Hypot(nx - x, ny - y);
            x = nx;
            y = ny;
            travelled += step;
            sinceDot += step;
            if (x < MoonfallRules.LeftWall + r || x > MoonfallRules.RightWall - r || y > MoonfallRules.CatchLine)
            {
                break;
            }

            for (var i = 0; i < bodies.Length; i++)
            {
                ref readonly var b = ref bodies[i];
                if (!b.Cleared && b.Near(x, y) && b.Overlap(x, y, out _, out _, out _))
                {
                    dots[count++] = (x, y);
                    return count;
                }
            }

            if (sinceDot >= MoonfallRules.GuideDotSpacing)
            {
                sinceDot -= MoonfallRules.GuideDotSpacing;
                dots[count++] = (x, y);
            }
        }

        return count;
    }

    // ---- Test seams ----

    /// <summary>The last real bounce (velocities at the contact, relative to the board), for the tests that replay the measured bounces.</summary>
    internal MoonfallBounce? LastBounce { get; private set; }

    /// <summary>Puts a ball in play at (x, y) with velocity (vx, vy), as a shot would, without using a ball.</summary>
    internal void PlaceBall(double x, double y, double vx, double vy)
    {
        ballX = x;
        ballY = y;
        ballVx = vx;
        ballVy = vy;
        shotValue = 0;
        shotPegs = 0;
        freeBallsThisShot = 0;
        hitCount = 0;
        clearedThisTurn = 0;
        stuckX = x;
        stuckY = y;
        stuckTicks = 0;
        lowestY = ballY;
        sinkTicks = 0;
        Phase = MoonfallPhase.Flying;
    }

    /// <summary>Lights peg <paramref name="index"/> as a touch would (scoring tests).</summary>
    internal void LightForTest(int index) => Light(index);

    // ---- Determinism ----

    /// <summary>A fingerprint of the whole game state: two games fed the same calls have the same one.</summary>
    public ulong Fingerprint()
    {
        var hash = 14695981039346656037UL;
        void Mix(ulong value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        Mix((ulong)BitConverter.DoubleToInt64Bits(ballX));
        Mix((ulong)BitConverter.DoubleToInt64Bits(ballY));
        Mix((ulong)BitConverter.DoubleToInt64Bits(ballVx));
        Mix((ulong)BitConverter.DoubleToInt64Bits(ballVy));
        Mix((ulong)gameTick);
        Mix((ulong)realTick);
        Mix((ulong)Score);
        Mix((ulong)BallsLeft);
        Mix((ulong)OrangesLeft);
        Mix((ulong)Phase);
        Mix((ulong)(purple + 1));
        foreach (ref readonly var b in bodies.AsSpan())
        {
            Mix(((ulong)b.Colour << 2) | (b.Lit ? 1UL : 0UL) | (b.Cleared ? 2UL : 0UL));
            Mix((ulong)BitConverter.DoubleToInt64Bits(b.X));
        }

        return hash;
    }

    private void Post(MoonfallEventKind kind, int peg, long value, int count, double x, double y)
    {
        var slot = (eventHead + eventCount) % EventCapacity;
        events[slot] = new MoonfallEvent(kind, peg, value, count, x, y);
        if (eventCount < EventCapacity)
        {
            eventCount++;
        }
        else
        {
            eventHead = (eventHead + 1) % EventCapacity;
        }
    }

    /// <summary>A peg's shape, place, colour and state, kept in one array so a tick walks it in order.</summary>
    private struct Body
    {
        public PegShape Shape;
        public double X;
        public double Y;
        public double Dx;
        public double Dy;
        public double Radius;
        public double Half;
        public double Start;
        public double Sweep;
        public double BoundX;
        public double BoundY;
        public double BoundRadius;
        public double Vx;
        public double Vy;
        public MoverKind Mover;
        public double HomeX;
        public double HomeY;
        public double MoverX;
        public double MoverY;
        public double Period;
        public double OrbitRadius;
        public double OrbitAngle;
        public bool Clockwise;
        public bool CanBeOrange;
        public PegColour Colour;
        public bool Lit;
        public bool Cleared;
        public int HitIndex;

        public static Body From(MoonfallPeg peg)
        {
            var b = new Body
            {
                Shape = peg.Shape,
                X = peg.X,
                Y = peg.Y,
                CanBeOrange = peg.CanBeOrange,
                Colour = PegColour.Blue,
                HitIndex = -1,
            };
            switch (peg.Shape)
            {
                case PegShape.Line:
                    b.Dx = peg.X2 - peg.X;
                    b.Dy = peg.Y2 - peg.Y;
                    b.Half = peg.Thickness * 0.5;
                    b.BoundX = peg.X + (b.Dx * 0.5);
                    b.BoundY = peg.Y + (b.Dy * 0.5);
                    b.BoundRadius = (MoonfallGeometry.Hypot(b.Dx, b.Dy) * 0.5) + b.Half;
                    break;

                case PegShape.Arc:
                    b.Radius = peg.Radius;
                    b.Half = peg.Thickness * 0.5;
                    b.Start = MoonfallGeometry.Radians(peg.StartDegrees);
                    b.Sweep = MoonfallGeometry.Radians(peg.SweepDegrees);
                    var mid = b.Start + (b.Sweep * 0.5);
                    b.BoundX = peg.X + (peg.Radius * Math.Cos(mid));
                    b.BoundY = peg.Y + (peg.Radius * Math.Sin(mid));
                    // Every point of the arc is within the chord of half its sweep of the arc's middle.
                    b.BoundRadius = (2 * peg.Radius * Math.Sin(b.Sweep * 0.25)) + b.Half;
                    break;

                default:
                    b.Radius = peg.Radius;
                    b.BoundX = peg.X;
                    b.BoundY = peg.Y;
                    b.BoundRadius = peg.Radius;
                    b.Mover = peg.Mover.Kind;
                    b.HomeX = peg.X;
                    b.HomeY = peg.Y;
                    b.MoverX = peg.Mover.X;
                    b.MoverY = peg.Mover.Y;
                    b.Period = Math.Max(peg.Mover.PeriodSeconds, MoonfallRules.TickSeconds);
                    b.Clockwise = peg.Mover.Clockwise;
                    b.OrbitRadius = MoonfallGeometry.Hypot(peg.X - peg.Mover.X, peg.Y - peg.Mover.Y);
                    b.OrbitAngle = Math.Atan2(peg.Y - peg.Mover.Y, peg.X - peg.Mover.X);
                    break;
            }

            return b;
        }

        /// <summary>Whether the ball at (x, y) is near enough to need the exact test (with <paramref name="slack"/> to spare).</summary>
        public readonly bool Near(double x, double y, double slack = 0)
        {
            var reach = BoundRadius + MoonfallRules.BallRadius + slack;
            var dx = x - BoundX;
            var dy = y - BoundY;
            return (dx * dx) + (dy * dy) < reach * reach;
        }

        /// <summary>The ball's centre's distance from this peg's surface, less the ball's radius: 0 at contact.</summary>
        public readonly double Gap(double x, double y)
        {
            var (qx, qy) = Closest(x, y);
            return MoonfallGeometry.Hypot(x - qx, y - qy) - Reach;
        }

        /// <summary>Whether a ball at (x, y) overlaps this peg; the normal points from the peg to the ball.</summary>
        public readonly bool Overlap(double x, double y, out double nx, out double ny, out double depth)
        {
            var (qx, qy) = Closest(x, y);
            var dx = x - qx;
            var dy = y - qy;
            var reach = Reach;
            var d2 = (dx * dx) + (dy * dy);
            if (d2 >= reach * reach)
            {
                nx = 0;
                ny = 0;
                depth = 0;
                return false;
            }

            var d = Math.Sqrt(d2);
            if (d > 1e-9)
            {
                nx = dx / d;
                ny = dy / d;
            }
            else
            {
                nx = 0;
                ny = -1;
            }

            depth = reach - d;
            return true;
        }

        private readonly double Reach => (Shape == PegShape.Round ? Radius : Half) + MoonfallRules.BallRadius;

        private readonly (double X, double Y) Closest(double x, double y) => Shape switch
        {
            PegShape.Line => MoonfallGeometry.ClosestOnSegment(x, y, X, Y, X + Dx, Y + Dy),
            PegShape.Arc => MoonfallGeometry.ClosestOnArc(x, y, X, Y, Radius, Start, Sweep),
            _ => (X, Y),
        };

        public void Move(double seconds)
        {
            var phase = 2 * Math.PI * (seconds % Period) / Period;
            var omega = 2 * Math.PI / Period;
            if (Mover == MoverKind.Orbit)
            {
                var sign = Clockwise ? 1.0 : -1.0;
                var angle = OrbitAngle + (sign * phase);
                X = MoverX + (OrbitRadius * Math.Cos(angle));
                Y = MoverY + (OrbitRadius * Math.Sin(angle));
                Vx = -OrbitRadius * Math.Sin(angle) * omega * sign;
                Vy = OrbitRadius * Math.Cos(angle) * omega * sign;
            }
            else
            {
                var s = (1 - Math.Cos(phase)) * 0.5;
                var ds = Math.Sin(phase) * 0.5 * omega;
                X = HomeX + ((MoverX - HomeX) * s);
                Y = HomeY + ((MoverY - HomeY) * s);
                Vx = (MoverX - HomeX) * ds;
                Vy = (MoverY - HomeY) * ds;
            }

            BoundX = X;
            BoundY = Y;
        }
    }
}
