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
/// calls give the same game, bit for bit, on every run on the same machine and build: the oranges, greens and purples
/// come from the seed (<see cref="MoonfallRandom"/>), and nothing reads a clock. Across machines a replay may drift:
/// <see cref="Math.Sin"/>, <see cref="Math.Cos"/> and <see cref="Math.Atan2"/> may differ in their last bit between
/// CPUs and runtimes, and nothing here needs more (there is no networked or recorded replay).
/// <para>
/// The window draws one game tick behind, blended by <see cref="Alpha"/> (<see cref="BallAt"/>,
/// <see cref="BucketXAt"/>, <see cref="Peg(int, double)"/>), so slow motion and any screen rate show smooth motion
/// while the physics keeps its fixed steps.
/// </para>
/// <para>
/// Steady state allocates nothing: every array is made when the level starts, events go into a fixed ring
/// (<see cref="TryReadEvent"/>), and the guide writes into the caller's span.
/// </para>
/// <para>
/// Extension points: powers (G5) attach to <see cref="MoonfallEventKind.GreenHit"/>; style shots (G4) read the event
/// stream (wall and bucket bounces, hits with their positions); modes (G7) set the balls, the level number and the seed.
/// </para>
/// </summary>
public sealed partial class MoonfallGame
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

    /// <summary>
    /// The balls in play (a twin ball makes more than one, <see cref="MoonfallRules.MaxBalls"/>), each with where it was
    /// one game tick ago (the window draws between the two). The first <see cref="ballCount"/> are in play, in the order
    /// they came in; slot 0 is always one of them while a ball flies, so the window's one-ball reads keep working.
    /// </summary>
    private readonly Ball[] balls = new Ball[MoonfallRules.MaxBalls];
    private int ballCount;

    /// <summary>The ball whose touch is being resolved (its index in <see cref="balls"/>), or −1 for a peg a power lights.</summary>
    private int toucher = -1;

    private ref Ball ball => ref balls[0];

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

    private int lastOrange = -1;
    private int approachBall;
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
    /// <param name="power">The power a green peg triggers (plan v9 G5): the level's character's (<see cref="MoonfallCharacters"/>); none by default.</param>
    public MoonfallGame(MoonfallLevel level, int levelNumber, ulong seed, int balls = MoonfallRules.BallsPerLevel, MoonfallPower power = MoonfallPower.None)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfLessThan(levelNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(balls, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)power, MoonfallPowers.Count, nameof(power));
        Level = level;
        LevelNumber = levelNumber;
        BallsLeft = balls;
        Power = power;
        random = new MoonfallRandom(seed);
        bodies = new Body[level.Pegs.Count];
        hitOrder = new int[level.Pegs.Count];
        scratch = new int[level.Pegs.Count];
        scratchDistance = new double[level.Pegs.Count];
        stamp = new int[level.Pegs.Count];
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

    /// <summary>
    /// This shot's score: <see cref="ShotValue"/> × <see cref="ShotPegs"/> [M correction], times 3 under a triple score
    /// (<see cref="MoonfallPower.Draw"/>), plus its style bonuses (<see cref="StyleBonus"/>, never multiplied).
    /// </summary>
    public long ShotScore => (shotValue * shotPegs * (tripleThisShot ? MoonfallRules.TripleScoreFactor : 1)) + styleBonus;

    /// <summary>Free balls this shot's score has earned (0–3).</summary>
    public int FreeBallsThisShot => freeBallsThisShot;

    /// <summary>How many of this turn's lit pegs have cleared (the tally counts "× N pegs" up with them).</summary>
    public int ClearedThisTurn => clearedThisTurn;

    /// <summary>Whether a ball is in play.</summary>
    public bool BallInPlay => Phase == MoonfallPhase.Flying;

    public double BallX => ball.X;

    public double BallY => ball.Y;

    public double BallVelocityX => ball.Vx;

    public double BallVelocityY => ball.Vy;

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

    /// <summary>
    /// The ball the camera follows (an index below <see cref="BallsInPlay"/>): in the approach, the one heading for the
    /// last orange (a twin may be); else the first.
    /// </summary>
    public int CameraBall => Approaching && approachBall < ballCount ? approachBall : 0;

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

        BallsLeft--;
        StartShot();
        var (dirX, dirY) = Direction(angleDegrees);
        BeginFlight(
            MoonfallRules.LauncherX + (dirX * MoonfallRules.BarrelLength),
            MoonfallRules.LauncherY + (dirY * MoonfallRules.BarrelLength),
            dirX * MoonfallRules.LaunchSpeed,
            dirY * MoonfallRules.LaunchSpeed);

        // Sage's Path holds the ball in the barrel while it weighs the lines within its spread, then nudges it onto the best.
        if (PowerActive(MoonfallPower.Path))
        {
            BeginPath(angleDegrees);
        }

        return true;
    }

    /// <summary>A new shot's state: its score, its hits, and the powers that act in it (<see cref="StartShotPowers"/>).</summary>
    private void StartShot()
    {
        shotValue = 0;
        shotPegs = 0;
        freeBallsThisShot = 0;
        hitCount = 0;
        clearedThisTurn = 0;
        StartShotPowers();
        StartShotStyle();
    }

    /// <summary>One ball in play at (x, y) with velocity (vx, vy): the shot's flight begins.</summary>
    private void BeginFlight(double x, double y, double vx, double vy)
    {
        ballCount = 0;
        AddBall(x, y, vx, vy);
        Phase = MoonfallPhase.Flying;
    }

    /// <summary>A ball comes into play (the shot's, or a twin); false when <see cref="MoonfallRules.MaxBalls"/> are in already.</summary>
    private bool AddBall(double x, double y, double vx, double vy)
    {
        if (ballCount >= balls.Length)
        {
            return false;
        }

        ref var b = ref balls[ballCount++];
        b = default;
        b.X = x;
        b.Y = y;
        b.Vx = vx;
        b.Vy = vy;
        b.PrevX = x;
        b.PrevY = y;
        b.ResetWatch();
        b.ResetStyle();
        return true;
    }

    /// <summary>How many balls are in play (0 while none flies).</summary>
    public int BallsInPlay => Phase == MoonfallPhase.Flying ? ballCount : 0;

    /// <summary>Ball <paramref name="index"/> (below <see cref="BallsInPlay"/>) as drawn at <paramref name="alpha"/>.</summary>
    public (double X, double Y) BallAt(int index, double alpha)
    {
        ref readonly var b = ref balls[Math.Clamp(index, 0, balls.Length - 1)];
        return (b.PrevX + ((b.X - b.PrevX) * alpha), b.PrevY + ((b.Y - b.PrevY) * alpha));
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

        // The approach's camera centres between the drawn ball and the last orange.
        if (approach && !feverHit && lastOrange >= 0)
        {
            var (x, y) = BallAt(approachBall, speedAccumulator / (double)SpeedOne);
            ref readonly var target = ref bodies[lastOrange];
            focusX = (x + target.BoundX) * 0.5;
            focusY = (y + target.BoundY) * 0.5;
        }
    }

    /// <summary>
    /// How far the drawn picture is from the last game tick towards the next one (0–1): the game time built up in the
    /// speed accumulator and in the wall-clock remainder of <see cref="Advance"/>. The window draws the ball, the bucket
    /// and the movers one game tick behind, blended by this, so slow motion and a 60 Hz screen show smooth motion while
    /// the simulation stays on its fixed 100 Hz steps. Reading it changes nothing.
    /// </summary>
    public double Alpha => Math.Clamp((speedAccumulator + (speedMilli * (realAccumulator / MoonfallRules.TickSeconds))) / SpeedOne, 0.0, 1.0);

    /// <summary>The ball as drawn at <paramref name="alpha"/> (<see cref="Alpha"/>): between its last two game ticks.</summary>
    public (double X, double Y) BallAt(double alpha) => BallAt(0, alpha);

    /// <summary>The bucket's centre as drawn at <paramref name="alpha"/>.</summary>
    public double BucketXAt(double alpha) => MoonfallBucket.CentreAt(gameTick - 1 + alpha);

    /// <summary>Peg <paramref name="index"/> as drawn at <paramref name="alpha"/>: a mover between its last two places.</summary>
    public MoonfallPegView Peg(int index, double alpha)
    {
        var view = Peg(index);
        ref readonly var b = ref bodies[index];
        return b.Mover == MoverKind.None
            ? view
            : view with { X = b.PrevX + ((b.X - b.PrevX) * alpha), Y = b.PrevY + ((b.Y - b.PrevY) * alpha) };
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
            return;
        }

        zoom = Math.Max(1.0, zoom - ((MoonfallRules.ZoomMax - 1.0) / MoonfallRules.ZoomCancelTicks));
    }

    /// <summary>One 10 ms game tick: the bucket and movers advance, then the ball or the clearing.</summary>
    private void StepGame()
    {
        for (var k = 0; k < ballCount; k++)
        {
            balls[k].PrevX = balls[k].X;
            balls[k].PrevY = balls[k].Y;
        }

        for (var i = 0; i < bodies.Length; i++)
        {
            bodies[i].PrevX = bodies[i].X;
            bodies[i].PrevY = bodies[i].Y;
        }

        gameTick++;
        UpdateMovers();
        StepFlippers();
        switch (Phase)
        {
            case MoonfallPhase.Flying:
                // Sage's Path weighs one line a game tick while the ball waits in the barrel.
                if (ChoosingPath)
                {
                    StepPath();
                    break;
                }

                StepBalls();
                if (Phase == MoonfallPhase.Flying)
                {
                    for (var k = 0; k < ballCount; k++)
                    {
                        WatchStuck(ref balls[k]);
                    }

                    WatchApproach();
                }

                break;

            case MoonfallPhase.Clearing:
                StepClearing();
                break;
        }
    }

    // ---- The ball ----

    /// <summary>
    /// One game tick for every ball in play. A ball that leaves (caught, lost) is dropped after the tick, keeping the
    /// others in order; a twin made this tick starts moving on the next. The turn ends when the last ball has gone, or
    /// at once when a ball lands in a Fever bucket.
    /// </summary>
    private void StepBalls()
    {
        var count = ballCount;
        for (var k = 0; k < count; k++)
        {
            StepBall(k);
            if (Phase != MoonfallPhase.Flying)
            {
                return;
            }
        }

        var kept = 0;
        for (var k = 0; k < ballCount; k++)
        {
            if (balls[k].Gone)
            {
                continue;
            }

            if (kept != k)
            {
                balls[kept] = balls[k];
            }

            kept++;
        }

        ballCount = kept;
        if (ballCount == 0)
        {
            EndTurn();
        }
    }

    private void StepBall(int k)
    {
        ref var b = ref balls[k];
        var speed = MoonfallGeometry.Hypot(b.Vx, b.Vy) + (MoonfallRules.Gravity * MoonfallRules.TickSeconds);
        var steps = Math.Clamp((int)Math.Ceiling(speed * MoonfallRules.TickSeconds / MoonfallRules.MaxSubStep), 1, MaxSubSteps);
        var h = MoonfallRules.TickSeconds / steps;
        var bucketX = BucketX;
        var bucketV = feverHit ? 0.0 : MoonfallBucket.VelocityAt(gameTick);
        toucher = k;
        try
        {
            for (var s = 0; s < steps; s++)
            {
                var previousY = b.Y;
                b.X += b.Vx * h;
                b.Y += (b.Vy * h) + (0.5 * MoonfallRules.Gravity * h * h);
                b.Vy += MoonfallRules.Gravity * h;
                ResolveContacts(ref b, bucketX, bucketV, live: true);
                WatchRise(ref b);
                if (previousY < MoonfallRules.CatchLine && b.Y >= MoonfallRules.CatchLine)
                {
                    if (feverHit)
                    {
                        LandInFeverBucket(ref b);
                        return;
                    }

                    if (Math.Abs(b.X - bucketX) <= MouthHalf)
                    {
                        CatchInBucket(ref b);
                        return;
                    }
                }

                if (b.Y > MoonfallRules.FloorExit)
                {
                    if (TryGate(ref b))
                    {
                        return;
                    }

                    LoseBall(ref b);
                    return;
                }
            }
        }
        finally
        {
            toucher = -1;
        }
    }

    private enum ContactKind : byte
    {
        None,
        Peg,
        Wall,
        Rim,
        Flipper,
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
    /// <para>
    /// A Fireball shot meets no peg: each one it overlaps is lit and burns away at once (<see cref="Burn"/>), and the
    /// ball flies on. Off the live ball (a prediction, a guide, the path search) a peg it overlaps is noted in
    /// <see cref="probe"/>, so the caller can see what the flight touched.
    /// </para>
    /// </summary>
    private bool ResolveContacts(ref Ball b, double bucketX, double bucketV, bool live, int watch = -1)
    {
        const double r = MoonfallRules.BallRadius;
        var touched = false;
        var burning = PowerActive(MoonfallPower.Fireball);
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            contactCount = 0;

            // Walls and ceiling.
            AddContact(ContactKind.Wall, -1, MoonfallRules.LeftWall + r - b.X, 1, 0, 0, 0);
            AddContact(ContactKind.Wall, -1, b.X - (MoonfallRules.RightWall - r), -1, 0, 0, 0);
            AddContact(ContactKind.Wall, -1, MoonfallRules.Ceiling + r - b.Y, 0, 1, 0, 0);

            // The bucket's rims (wider apart under Brass Wings), or the Fever buckets' posts.
            if (b.Y > MoonfallRules.BucketTop - (2 * r) - 20)
            {
                if (feverHit)
                {
                    for (var k = 1; k < 5; k++)
                    {
                        var postX = MoonfallRules.LeftWall + (k * MoonfallBucket.FeverBucketWidth);
                        PostContact(in b, postX, MoonfallRules.BucketTop + MoonfallRules.FeverPostRadius, MoonfallRules.FeverPostRadius, 0);
                    }
                }
                else
                {
                    var offset = MouthHalf + MoonfallBucket.RimRadius;
                    PostContact(in b, bucketX - offset, MoonfallRules.BucketTop + MoonfallBucket.RimRadius, MoonfallBucket.RimRadius, bucketV);
                    PostContact(in b, bucketX + offset, MoonfallRules.BucketTop + MoonfallBucket.RimRadius, MoonfallBucket.RimRadius, bucketV);
                }
            }

            FlipperContacts(in b);

            // Pegs and bricks.
            for (var i = 0; i < bodies.Length; i++)
            {
                ref readonly var body = ref bodies[i];
                if (body.Cleared || !body.Near(b.X, b.Y) || !body.Overlap(b.X, b.Y, out var pnx, out var pny, out var pdepth))
                {
                    continue;
                }

                if (!live)
                {
                    touched |= i == watch;
                    NoteProbe(i);
                    if (burning)
                    {
                        continue;
                    }
                }
                else if (burning)
                {
                    Burn(ref b, i);
                    continue;
                }

                AddContact(ContactKind.Peg, i, pdepth, pnx, pny, body.Vx, body.Vy);
            }

            if (contactCount == 0)
            {
                return touched;
            }

            if (!live)
            {
                probe.Any = true;
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

            b.X += nx * push;
            b.Y += ny * push;
            var peg = main.Kind == ContactKind.Peg;
            var (en, et) = main.Kind switch
            {
                ContactKind.Peg => (MoonfallRules.PegNormalRestitution, MoonfallRules.PegTangentRestitution),
                ContactKind.Flipper => (MoonfallRules.FlipperRestitution, MoonfallRules.PegTangentRestitution),
                _ => (MoonfallRules.WallRestitution, MoonfallRules.WallRestitution),
            };
            var inX = b.Vx;
            var approach = -(((b.Vx - main.Vx) * nx) + ((b.Vy - main.Vy) * ny));
            var inY = b.Vy;
            var bounced = Bounce(ref b, nx, ny, main.Vx, main.Vy, en, et);
            if (!live)
            {
                continue;
            }

            if (bounced)
            {
                LastBounce = new MoonfallBounce(peg ? main.Index : -1, b.X, b.Y, nx, ny, inX, inY, b.Vx, b.Vy);
            }

            var wall = false;
            var rim = false;
            var anyPeg = false;
            for (var c = 0; c < contactCount; c++)
            {
                switch (contacts[c].Kind)
                {
                    case ContactKind.Peg:
                        anyPeg = true;
                        break;
                    case ContactKind.Wall:
                        wall = true;
                        break;
                    case ContactKind.Rim:
                        rim = true;
                        break;
                }
            }

            if (anyPeg)
            {
                NotePegContact(ref b, approach);
                for (var c = 0; c < contactCount; c++)
                {
                    if (contacts[c].Kind == ContactKind.Peg)
                    {
                        Touch(ref b, contacts[c].Index);
                    }
                }
            }

            if (bounced && wall)
            {
                NoteWall(ref b);
                Post(MoonfallEventKind.WallBounce, -1, 0, 0, b.X, b.Y);
            }

            if (bounced && rim)
            {
                NoteRim(ref b);
                Post(MoonfallEventKind.BucketBounce, -1, 0, 0, b.X, b.Y);
            }
        }

        return touched;
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
    private void PostContact(in Ball b, double x, double topY, double radius, double vx)
    {
        var qy = Math.Max(b.Y, topY);
        var dx = b.X - x;
        var dy = b.Y - qy;
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
    private static bool Bounce(ref Ball b, double nx, double ny, double ovx, double ovy, double en, double et)
    {
        var rvx = b.Vx - ovx;
        var rvy = b.Vy - ovy;
        var vn = (rvx * nx) + (rvy * ny);
        if (vn >= 0)
        {
            return false;
        }

        var tx = rvx - (vn * nx);
        var ty = rvy - (vn * ny);
        if (-vn < MoonfallRules.RestingSpeed)
        {
            b.Vx = tx + ovx;
            b.Vy = ty + ovy;
            return false;
        }

        b.Vx = (tx * et) - (vn * en * nx) + ovx;
        b.Vy = (ty * et) - (vn * en * ny) + ovy;
        return true;
    }

    // ---- Hits and scoring ----

    /// <summary>Lights peg <paramref name="index"/> for this shot; false when it was lit or gone already.</summary>
    private bool Light(int index)
    {
        ref var b = ref bodies[index];
        if (b.Lit || b.Cleared)
        {
            return false;
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

            NoteOrangeLit(index);
        }

        // A green triggers the level's power (plan v9 G5), after the peg itself has counted.
        if (b.Colour == PegColour.Green)
        {
            OnGreenLit(index);
        }

        return true;
    }

    private void AwardShotFreeBalls()
    {
        var thresholds = MoonfallRules.FreeBallThresholds;
        var score = ShotScore;
        while (freeBallsThisShot < thresholds.Length && score >= thresholds[freeBallsThisShot])
        {
            BallsLeft++;
            Post(MoonfallEventKind.FreeBall, -1, thresholds[freeBallsThisShot], freeBallsThisShot + 1, ball.X, ball.Y);
            freeBallsThisShot++;
        }
    }

    // ---- The stuck ball ----

    private void WatchStuck(ref Ball ball)
    {
        // Sinking: a new low point (by StuckRadius) restarts the hollow watch.
        if (ball.Y > ball.LowestY + MoonfallRules.StuckRadius)
        {
            ball.LowestY = ball.Y;
            ball.SinkTicks = 0;
        }
        else if (MoonfallGeometry.Hypot(ball.Vx, ball.Vy) < MoonfallRules.StuckSinkSpeed)
        {
            ball.SinkTicks++;
        }

        if (MoonfallGeometry.Hypot(ball.X - ball.StuckX, ball.Y - ball.StuckY) > MoonfallRules.StuckRadius)
        {
            ball.StuckX = ball.X;
            ball.StuckY = ball.Y;
            ball.StuckTicks = 0;
        }
        else
        {
            ball.StuckTicks++;
        }

        if ((ball.StuckTicks >= MoonfallRules.StuckTicks || ball.SinkTicks >= MoonfallRules.StuckSinkTicks) && ClearStuckPeg(in ball))
        {
            ball.ResetWatch();
        }
    }

    /// <summary>Clears early the lit peg the ball touches that was hit last; false when it touches none (it is in the air).</summary>
    private bool ClearStuckPeg(in Ball ball)
    {
        var pick = -1;
        var pickOrder = -1;
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (!b.Lit || b.Cleared || !b.Near(ball.X, ball.Y, MoonfallRules.StuckTouchSlack))
            {
                continue;
            }

            if (b.Gap(ball.X, ball.Y) <= MoonfallRules.StuckTouchSlack && b.HitIndex > pickOrder)
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

        // Any ball in play may be the one heading for it; the camera follows the first that is.
        var predicted = false;
        for (var k = 0; k < ballCount && lastOrange >= 0 && !predicted; k++)
        {
            if (PredictsTouch(in balls[k], lastOrange, MoonfallRules.FeverLookaheadTicks))
            {
                predicted = true;
                approachBall = k;
            }
        }

        if (predicted && !approach)
        {
            approach = true;
            speedMilli = MoonfallRules.ApproachSpeedMilli;
            Post(MoonfallEventKind.FeverApproach, lastOrange, 0, 0, balls[approachBall].X, balls[approachBall].Y);
        }
        else if (!predicted && approach)
        {
            approach = false;
            speedMilli = SpeedOne;
            Post(MoonfallEventKind.FeverApproachEnded, lastOrange, 0, 0, ball.X, ball.Y);
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

    /// <summary>
    /// Whether <paramref name="from"/> touches peg <paramref name="target"/> within <paramref name="ticks"/> game
    /// ticks, flown on a copy with the real contact solver: a ball rolling or sliding along a brick into the last orange,
    /// or bouncing off another peg on the way, is predicted as it will move. The bucket and the movers are taken where
    /// they are now.
    /// </summary>
    private bool PredictsTouch(in Ball from, int target, int ticks)
    {
        var b = from;
        var bucketX = BucketX;
        var bucketV = MoonfallBucket.VelocityAt(gameTick);
        for (var t = 0; t < ticks; t++)
        {
            var speed = MoonfallGeometry.Hypot(b.Vx, b.Vy) + (MoonfallRules.Gravity * MoonfallRules.TickSeconds);
            var steps = Math.Clamp((int)Math.Ceiling(speed * MoonfallRules.TickSeconds / MoonfallRules.MaxSubStep), 1, MaxSubSteps);
            var h = MoonfallRules.TickSeconds / steps;
            for (var s = 0; s < steps; s++)
            {
                b.X += b.Vx * h;
                b.Y += (b.Vy * h) + (0.5 * MoonfallRules.Gravity * h * h);
                b.Vy += MoonfallRules.Gravity * h;
                if (ResolveContacts(ref b, bucketX, bucketV, live: false, watch: target))
                {
                    return true;
                }

                if (b.Y >= MoonfallRules.CatchLine)
                {
                    return false;
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

    /// <summary>
    /// [J] The first ball to land in a Fever bucket pays the level's one Fever bonus and ends the turn; any other ball
    /// still in play goes with it (the tally has one Fever line, as the original's).
    /// </summary>
    private void LandInFeverBucket(ref Ball b)
    {
        feverBucket = MoonfallBucket.FeverBucketAt(b.X);
        feverBonus = Perfect ? MoonfallRules.PerfectFeverBucketValue : MoonfallRules.FeverBucketValues[feverBucket];
        feverLanded = true;
        speedMilli = SpeedOne;
        CheckClearNight(in b);
        Post(MoonfallEventKind.FeverLanded, feverBucket, feverBonus, 0, b.X, b.Y);
        EndTurn();
    }

    // ---- The end of a turn ----

    /// <summary>A ball lands in the bucket: a free ball for each one caught (a twin's too). The others fly on.</summary>
    private void CatchInBucket(ref Ball b)
    {
        BallsLeft++;
        b.Gone = true;
        Post(MoonfallEventKind.BucketCatch, -1, 0, 0, b.X, b.Y);
        Post(MoonfallEventKind.FreeBall, -1, 0, freeBallsThisShot, b.X, b.Y);
        NoteCatch(in b);
    }

    private void LoseBall(ref Ball b)
    {
        b.Gone = true;
        Post(MoonfallEventKind.BallLost, -1, 0, 0, b.X, b.Y);
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
        EndShotPowers();
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
        Post(MoonfallEventKind.ShotScored, -1, shot, shotPegs, ball.X, ball.Y);
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
        StartShot();
        BeginFlight(x, y, vx, vy);
    }

    /// <summary>Lights peg <paramref name="index"/> as a touch would (scoring tests).</summary>
    internal void LightForTest(int index) => Light(index);

    /// <summary>Triggers <paramref name="power"/> at peg <paramref name="index"/> as the drum would (powers combined).</summary>
    internal void TriggerForTest(MoonfallPower power, int index) => TriggerPower(power, index);

    /// <summary>
    /// Ball <paramref name="ballIndex"/> touches peg <paramref name="index"/> as a contact in this game tick would: the
    /// slide's watch, then the touch with the style shots' watch (style tests).
    /// </summary>
    internal void TouchForTest(int ballIndex, int index)
    {
        NotePegContact(ref balls[ballIndex]);
        Touch(ref balls[ballIndex], index);
    }

    /// <summary>Makes peg <paramref name="index"/> green (it must not be orange), so a test knows where the green is.</summary>
    internal void MakeGreenForTest(int index)
    {
        if (bodies[index].Colour == PegColour.Orange)
        {
            throw new InvalidOperationException("An orange peg cannot be made green.");
        }

        if (purple == index)
        {
            purple = -1;
        }

        bodies[index].Colour = PegColour.Green;
    }

    // ---- Determinism ----

    /// <summary>
    /// A fingerprint of the whole game state: the ball and its last tick, the clocks and both accumulators, the counter,
    /// the shot and the turn, the stuck watch, Fever and the camera, every peg, and every event still unread. Two games fed
    /// the same calls on the same machine have the same one (the engine uses <see cref="Math.Sin"/>, <see cref="Math.Cos"/>
    /// and <see cref="Math.Atan2"/>, whose last bit may differ between CPUs or runtimes, so a replay is promised to match
    /// on one machine and build, not across them).
    /// </summary>
    public ulong Fingerprint()
    {
        var hash = new FingerprintHash();
        hash.Add(ballCount);
        foreach (ref readonly var b in balls.AsSpan())
        {
            b.AddTo(ref hash);
        }

        AddPowersTo(ref hash);
        hash.Add(gameTick);
        hash.Add(realTick);
        hash.Add(speedMilli);
        hash.Add(speedAccumulator);
        hash.Add(realAccumulator);
        hash.Add(counter.Shown);
        hash.Add(counter.Target);
        hash.Add(counter.Hold);
        hash.Add(Score);
        hash.Add(levelScore);
        hash.Add(BallsLeft);
        hash.Add(OrangesLeft);
        hash.Add((long)Phase);
        hash.Add(purple);
        hash.Add(shotValue);
        hash.Add(shotPegs);
        hash.Add(freeBallsThisShot);
        hash.Add(hitCount);
        hash.Add(clearWait);
        hash.Add(clearCursor);
        hash.Add(clearedThisTurn);
        hash.Add(nextBallWait);
        hash.Add(lastOrange);
        hash.Add(approachBall);
        hash.Add((approach ? 1 : 0) | (feverHit ? 2 : 0) | (feverLanded ? 4 : 0) | (Perfect ? 8 : 0));
        hash.Add(feverRealTicks);
        hash.Add(feverBucket);
        hash.Add(feverBonus);
        hash.Add(zoom);
        hash.Add(focusX);
        hash.Add(focusY);
        foreach (ref readonly var b in bodies.AsSpan())
        {
            hash.Add(((long)b.Colour << 2) | (b.Lit ? 1L : 0L) | (b.Cleared ? 2L : 0L));
            hash.Add(b.HitIndex);
            hash.Add(b.X);
            hash.Add(b.Y);
        }

        hash.Add(eventCount);
        for (var k = 0; k < eventCount; k++)
        {
            var e = events[(eventHead + k) % EventCapacity];
            hash.Add((long)e.Kind);
            hash.Add(e.Peg);
            hash.Add(e.Value);
            hash.Add(e.Count);
            hash.Add(e.X);
            hash.Add(e.Y);
        }

        return hash.Value;
    }

    /// <summary>FNV-1a over 64-bit words.</summary>
    private struct FingerprintHash
    {
        public ulong Value;

        public FingerprintHash()
        {
            Value = 14695981039346656037UL;
        }

        public void Add(long word)
        {
            Value ^= (ulong)word;
            Value *= 1099511628211UL;
        }

        public void Add(double word) => Add(BitConverter.DoubleToInt64Bits(word));
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

    /// <summary>A ball's centre and velocity, where it was a game tick ago, its stuck watch and its style shots' watch.</summary>
    private struct Ball
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double PrevX;
        public double PrevY;

        /// <summary>Caught or lost this tick: dropped once every ball has moved.</summary>
        public bool Gone;

        // The stuck watch (MoonfallRules.StuckTicks, StuckSinkTicks).
        public double StuckX;
        public double StuckY;
        public int StuckTicks;
        public double LowestY;
        public int SinkTicks;

        // The style shots' watch (MoonfallGame.Style.cs).
        public int LegPeg;
        public double LegX;
        public double LegY;
        public int LegBlues;
        public bool LegWasLong;
        public bool AfterWall;
        public double WallX;
        public double WallY;
        public bool AfterRim;
        public bool RimArmed;
        public long RimTick;
        public double RimY;
        public double RimPeak;
        public long LastPegContact;
        public int SlideRun;

        /// <summary>Starts the stuck watch afresh from where the ball is.</summary>
        public void ResetWatch()
        {
            StuckX = X;
            StuckY = Y;
            StuckTicks = 0;
            LowestY = Y;
            SinkTicks = 0;
        }

        /// <summary>Starts the style shots' watch afresh (a new ball).</summary>
        public void ResetStyle()
        {
            LegPeg = -1;
            LegX = 0;
            LegY = 0;
            LegBlues = 0;
            LegWasLong = false;
            AfterWall = false;
            WallX = 0;
            WallY = 0;
            AfterRim = false;
            RimArmed = false;
            RimTick = 0;
            RimY = 0;
            RimPeak = 0;
            LastPegContact = long.MinValue / 2;
            SlideRun = 0;
        }

        public readonly void AddTo(ref FingerprintHash hash)
        {
            hash.Add(X);
            hash.Add(Y);
            hash.Add(Vx);
            hash.Add(Vy);
            hash.Add(PrevX);
            hash.Add(PrevY);
            hash.Add(Gone ? 1 : 0);
            hash.Add(StuckX);
            hash.Add(StuckY);
            hash.Add(StuckTicks);
            hash.Add(LowestY);
            hash.Add(SinkTicks);
            hash.Add(LegPeg);
            hash.Add(LegX);
            hash.Add(LegY);
            hash.Add(LegBlues);
            hash.Add((LegWasLong ? 1 : 0) | (AfterWall ? 2 : 0) | (AfterRim ? 4 : 0) | (RimArmed ? 8 : 0));
            hash.Add(WallX);
            hash.Add(WallY);
            hash.Add(RimTick);
            hash.Add(RimY);
            hash.Add(RimPeak);
            hash.Add(LastPegContact);
            hash.Add(SlideRun);
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
        public double PrevX;
        public double PrevY;
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
                PrevX = peg.X,
                PrevY = peg.Y,
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

        /// <summary>
        /// The unit normal of this peg's upper face at its centre (<see cref="BoundX"/>, <see cref="BoundY"/>): straight
        /// up for a round peg, square to a straight brick or out along an arc's radius, turned to point up. A vertical
        /// face has no upper side: it is the one towards <paramref name="side"/> (−1 left, +1 right).
        /// </summary>
        public readonly (double X, double Y) UpperNormal(double side)
        {
            double nx, ny;
            switch (Shape)
            {
                case PegShape.Line:
                    var length = MoonfallGeometry.Hypot(Dx, Dy);
                    if (length < 1e-9)
                    {
                        return (0, -1);
                    }

                    (nx, ny) = (-Dy / length, Dx / length);
                    break;

                case PegShape.Arc:
                    var mid = Start + (Sweep * 0.5);
                    (nx, ny) = (Math.Cos(mid), Math.Sin(mid));
                    break;

                default:
                    return (0, -1);
            }

            var flip = Math.Abs(ny) > 1e-9 ? ny > 0 : nx * side < 0;
            return flip ? (-nx, -ny) : (nx, ny);
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
