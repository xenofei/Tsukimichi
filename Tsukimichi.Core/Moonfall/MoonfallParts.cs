namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The game's own random numbers (SplitMix64): the same seed gives the same oranges, greens and purples on every
/// machine and every .NET version, which <see cref="Random"/> does not promise. Allocation-free.
/// </summary>
public struct MoonfallRandom
{
    private ulong state;

    public MoonfallRandom(ulong seed)
    {
        state = seed;
    }

    /// <summary>The next 64 random bits.</summary>
    public ulong NextBits()
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A whole number in [0, <paramref name="count"/>); 0 when <paramref name="count"/> is 0 or less.</summary>
    public int Next(int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        // Lemire's multiply-shift: unbiased enough for a few hundred pegs, and no division.
        return (int)((NextBits() >> 32) * (ulong)count >> 32);
    }
}

/// <summary>
/// The score counter's count-up (plan v9 G2, measured [M 9]): every 10 ms tick it adds 1,000 while 10,000 or more is
/// still to come, 200 while 1,000 or more is, 100 while 100 or more is, and 10 below that (the last few points at once
/// when fewer than 10 remain). A new target can wait a hold first (<see cref="MoonfallRules.CountUpHoldTicks"/>).
/// </summary>
public struct ScoreCounter
{
    /// <summary>The score the counter is heading for.</summary>
    public long Target { get; private set; }

    /// <summary>The score it shows now.</summary>
    public long Shown { get; private set; }

    /// <summary>Ticks to wait before counting on.</summary>
    public int Hold { get; private set; }

    /// <summary>Whether it has caught up.</summary>
    public readonly bool Settled => Shown == Target;

    /// <summary>Sets a new target, counted towards after <paramref name="holdTicks"/>.</summary>
    public void SetTarget(long target, int holdTicks = 0)
    {
        Target = target;
        Hold = Math.Max(Hold, holdTicks);
    }

    /// <summary>Shows the target at once (a new level).</summary>
    public void Reset(long value)
    {
        Target = value;
        Shown = value;
        Hold = 0;
    }

    /// <summary>One 10 ms tick of the count-up.</summary>
    public void Tick()
    {
        if (Hold > 0)
        {
            Hold--;
            return;
        }

        var left = Target - Shown;
        if (left == 0)
        {
            return;
        }

        if (left < 0)
        {
            Shown = Target;
            return;
        }

        Shown += Step(left);
    }

    /// <summary>What one tick adds with <paramref name="left"/> still to count [M 9].</summary>
    public static long Step(long left) => left switch
    {
        >= 10_000 => 1_000,
        >= 1_000 => 200,
        >= 100 => 100,
        >= 10 => 10,
        _ => left,
    };
}

/// <summary>The bucket's sweep [M 5]: x(t) = 401.5 + 260·sin(2π t / 6 s), in game ticks since the level began.</summary>
public static class MoonfallBucket
{
    /// <summary>The bucket's centre at game tick <paramref name="tick"/>.</summary>
    public static double CentreAt(long tick) =>
        MoonfallRules.BucketCentre + (MoonfallRules.BucketAmplitude * Math.Sin(Phase(tick)));

    /// <summary>The bucket's speed (px/s, positive to the right) at game tick <paramref name="tick"/>.</summary>
    public static double VelocityAt(long tick) =>
        MoonfallRules.BucketAmplitude * (2 * Math.PI / (MoonfallRules.BucketPeriodTicks * MoonfallRules.TickSeconds)) * Math.Cos(Phase(tick));

    /// <summary>The bucket's centre at a fractional game tick (the window draws between ticks).</summary>
    public static double CentreAt(double tick) =>
        MoonfallRules.BucketCentre + (MoonfallRules.BucketAmplitude * Math.Sin(2 * Math.PI * (tick % MoonfallRules.BucketPeriodTicks) / MoonfallRules.BucketPeriodTicks));

    private static double Phase(long tick) => 2 * Math.PI * (tick % MoonfallRules.BucketPeriodTicks) / MoonfallRules.BucketPeriodTicks;

    /// <summary>A rim post's radius: half of (131 − 104) / 2.</summary>
    public const double RimRadius = (MoonfallRules.BucketRimWidth - MoonfallRules.BucketMouth) / 4;

    /// <summary>A rim post's centre line, from the bucket's centre.</summary>
    public const double RimOffset = (MoonfallRules.BucketMouth * 0.5) + RimRadius;

    /// <summary>The Fever buckets' width: the board's width over five.</summary>
    public const double FeverBucketWidth = (MoonfallRules.RightWall - MoonfallRules.LeftWall) / 5;

    /// <summary>Which Fever bucket (0–4, left to right) a ball at <paramref name="x"/> falls into.</summary>
    public static int FeverBucketAt(double x) =>
        Math.Clamp((int)Math.Floor((x - MoonfallRules.LeftWall) / FeverBucketWidth), 0, 4);
}

/// <summary>What happened in the game, for the window's popups and sounds and for later style shots and powers.</summary>
public enum MoonfallEventKind : byte
{
    /// <summary>A peg was lit (<see cref="MoonfallEvent.Peg"/>, its score with the multiplier in <see cref="MoonfallEvent.Value"/>, and the shot's peg count in <see cref="MoonfallEvent.Count"/>).</summary>
    PegHit,

    /// <summary>A green peg was lit: the hook powers (G5) attach to.</summary>
    GreenHit,

    /// <summary>A lit peg cleared (<see cref="MoonfallEvent.Count"/>: how many have cleared this turn).</summary>
    PegCleared,

    /// <summary>A stuck ball's peg cleared early.</summary>
    StuckClear,

    /// <summary>The ball bounced off a wall or the ceiling.</summary>
    WallBounce,

    /// <summary>The ball bounced off the bucket's rim.</summary>
    BucketBounce,

    /// <summary>The ball landed in the bucket: a free ball.</summary>
    BucketCatch,

    /// <summary>The ball left the board.</summary>
    BallLost,

    /// <summary>A free ball (from the shot's score: <see cref="MoonfallEvent.Value"/> is the threshold; 0 for the bucket).</summary>
    FreeBall,

    /// <summary>The approach to the last orange began: slow motion and the zoom.</summary>
    FeverApproach,

    /// <summary>The approach ended without the hit.</summary>
    FeverApproachEnded,

    /// <summary>The last orange was hit (<see cref="MoonfallEvent.Value"/> 1 for a perfect clear).</summary>
    FeverHit,

    /// <summary>The ball landed in a Fever bucket (<see cref="MoonfallEvent.Peg"/> the bucket, <see cref="MoonfallEvent.Value"/> its bonus).</summary>
    FeverLanded,

    /// <summary>The turn's pegs have cleared and its score was added (<see cref="MoonfallEvent.Value"/>).</summary>
    ShotScored,

    /// <summary>The level was won; the tally is ready.</summary>
    LevelWon,

    /// <summary>The balls ran out with oranges left.</summary>
    LevelLost,

    /// <summary>A new ball is in the launcher.</summary>
    NextBall,

    // ---- Powers (plan v9 G5) and style shots (G4) ----

    /// <summary>
    /// A power was triggered at a green (<see cref="MoonfallEvent.Peg"/>; <see cref="MoonfallEvent.Value"/> the
    /// <see cref="MoonfallPower"/>, <see cref="MoonfallEvent.Count"/> its shots left). A burst lights its pegs next.
    /// </summary>
    PowerTriggered,

    /// <summary>A power has no shots left (<see cref="MoonfallEvent.Value"/> the <see cref="MoonfallPower"/>).</summary>
    PowerEnded,

    /// <summary>A twin ball came into play at a green (<see cref="MoonfallEvent.Count"/>: balls in play now).</summary>
    BallAdded,

    /// <summary>A ball fell out and came back in at the top through the Moon Gate (<see cref="MoonfallEvent.Value"/>: re-entries left).</summary>
    BallReentered,

    /// <summary>
    /// The drum drew (<see cref="MoonfallEvent.Value"/> the <see cref="MoonfallDrawOutcome"/>; <see cref="MoonfallEvent.Count"/>
    /// the <see cref="MoonfallPower"/> it gave, for another power).
    /// </summary>
    Drawn,

    /// <summary>
    /// The bolt struck from its first peg (<see cref="MoonfallEvent.Peg"/>), lighting <see cref="MoonfallEvent.Value"/>
    /// pegs; its path is <see cref="MoonfallGame.BoltPoints"/>.
    /// </summary>
    BoltStruck,

    /// <summary>Sage's Path chose the shot's line (<see cref="MoonfallEvent.Value"/>: the nudge in hundredths of a degree).</summary>
    PathChosen,

    /// <summary>
    /// A style shot (<see cref="MoonfallEvent.Count"/> the <see cref="MoonfallStyleShot"/>, <see cref="MoonfallEvent.Value"/>
    /// its bonus), where it happened.
    /// </summary>
    StyleShot,
}

/// <summary>One game event; a value type, so queuing one allocates nothing.</summary>
public readonly record struct MoonfallEvent(MoonfallEventKind Kind, int Peg, long Value, int Count, double X, double Y);

/// <summary>What the game is waiting for or doing.</summary>
public enum MoonfallPhase : byte
{
    /// <summary>A ball is in the launcher: aim and shoot.</summary>
    Aiming,

    /// <summary>The ball is in play.</summary>
    Flying,

    /// <summary>The ball is gone: lit pegs clear one by one, then the next ball comes.</summary>
    Clearing,

    /// <summary>The level is won: the tally shows.</summary>
    Won,

    /// <summary>The balls ran out.</summary>
    Lost,
}

/// <summary>The end-of-level tally (plan v9 G3).</summary>
/// <param name="LevelScore">The score before the end bonuses.</param>
/// <param name="FeverBonus">The Fever bucket's bonus.</param>
/// <param name="Perfect">Every peg was lit or gone at the last orange.</param>
/// <param name="BallsLeft">Balls left in the launcher.</param>
/// <param name="BallBonus">10,000 for each ball left.</param>
/// <param name="Total">The level's final score.</param>
public readonly record struct MoonfallTally(long LevelScore, long FeverBonus, bool Perfect, int BallsLeft, long BallBonus, long Total);
