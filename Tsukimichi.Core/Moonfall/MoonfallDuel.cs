namespace Tsukimichi.Core.Moonfall;

/// <summary>How a duel ended, from the player's side.</summary>
public enum MoonfallDuelOutcome : byte
{
    /// <summary>Still being played.</summary>
    Undecided,

    Won,
    Lost,
    Drawn,
}

/// <summary>One finished turn of a duel.</summary>
/// <param name="Side">Who shot (<see cref="MoonfallDuel.PlayerSide"/> or <see cref="MoonfallDuel.OpponentSide"/>).</param>
/// <param name="Scored">What the shot scored (with a Full Moon bucket's bonus when it ended the level).</param>
/// <param name="Kept">What the side kept: all of it, or <see cref="MoonfallRules.DuelNoOrangeKeepPercent"/> when it lit no orange.</param>
/// <param name="Oranges">The oranges it lit.</param>
/// <param name="FreeBalls">The free balls it won (they are the side's own).</param>
public readonly record struct MoonfallDuelTurn(int Side, long Scored, long Kept, int Oranges, int FreeBalls);

/// <summary>
/// A duel against a companion (plan v9 G7) [R §6 l.127–128, §2 l.47, §3 l.92]: the player and the opponent take
/// alternate shots on one board, each with <see cref="MoonfallRules.DuelBallsPerSide"/> balls and their own companion's
/// power; the player shoots first. A turn that lights no orange keeps three quarters of its score; a free ball is the
/// shooter's own; a side out of balls passes, and the other shoots on. The last orange ends the duel in a Full Moon,
/// whose bucket pays (the duel's values) the side that hit it; running out of balls ends it too. The higher score wins.
/// <para>
/// The board is a <see cref="MoonfallGame"/> on duel rules (<see cref="MoonfallRuleSet.Duel"/>), which the window draws
/// and reads events from as in any game; this class keeps the sides, so drive the duel through <see cref="Advance"/>
/// (or <see cref="Tick"/>) and <see cref="Shoot"/>, never the game's own. The opponent weighs its shot a few angles a
/// tick (<see cref="MoonfallAi"/>) and shoots on the tick it chose. Deterministic: the same seed and the same calls give
/// the same duel. Allocates nothing per tick.
/// </para>
/// </summary>
public sealed class MoonfallDuel
{
    public const int PlayerSide = 0;

    public const int OpponentSide = 1;

    private readonly int[] balls = new int[2];
    private readonly long[] scores = new long[2];
    private readonly ScoreCounter[] counters = new ScoreCounter[2];
    private readonly MoonfallCompanion[] companions = new MoonfallCompanion[2];

    private bool turnOpen;
    private int shooter;
    private long turnScore;
    private int turnOranges;
    private int turnBalls;

    /// <param name="level">The board.</param>
    /// <param name="levelNumber">Its number (greens come from level 3; a duel always has them, so it plays at least 3).</param>
    /// <param name="seed">The board's oranges and greens, and the opponent's choices.</param>
    /// <param name="player">The player's companion (whose power the player's greens trigger); <see cref="MoonfallCompanion.None"/> for none.</param>
    /// <param name="opponent">The opponent: a companion ("Duel against Louisoix"), whose power its greens trigger.</param>
    /// <param name="difficulty">How well the opponent plays.</param>
    /// <param name="ballsPerSide">Balls each side starts with.</param>
    public MoonfallDuel(
        MoonfallLevel level,
        int levelNumber,
        ulong seed,
        MoonfallCompanion player,
        MoonfallCompanion opponent,
        MoonfallAiDifficulty difficulty,
        int ballsPerSide = MoonfallRules.DuelBallsPerSide)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfLessThan(ballsPerSide, 1);
        if (player != MoonfallCompanion.None && !MoonfallCompanions.TryGet(player, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(player), player, "not a companion");
        }

        if (!MoonfallCompanions.TryGet(opponent, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(opponent), opponent, "the opponent must be a companion");
        }

        companions[PlayerSide] = player;
        companions[OpponentSide] = opponent;
        balls[PlayerSide] = ballsPerSide;
        balls[OpponentSide] = ballsPerSide;
        Game = new MoonfallGame(
            level,
            Math.Max(levelNumber, MoonfallRules.FirstGreenLevel),
            seed,
            2 * ballsPerSide,
            PowerOf(player),
            MoonfallRuleSet.Duel);
        Opponent = new MoonfallAi(difficulty, seed);
    }

    /// <summary>The board, on duel rules: draw it and read its events; drive it through this class.</summary>
    public MoonfallGame Game { get; }

    /// <summary>The opponent's mind.</summary>
    public MoonfallAi Opponent { get; }

    /// <summary>The side at the launcher (or that shot last, once the duel is over).</summary>
    public int Turn => Game.Side;

    /// <summary>Whether the player may shoot now.</summary>
    public bool PlayersTurn => Outcome == MoonfallDuelOutcome.Undecided && Game.Side == PlayerSide && Game.Phase == MoonfallPhase.Aiming;

    /// <summary>The side's companion.</summary>
    public MoonfallCompanion Companion(int side) => companions[Check(side)];

    /// <summary>The side's balls left.</summary>
    public int BallsLeft(int side) => balls[Check(side)];

    /// <summary>The side's score.</summary>
    public long Score(int side) => scores[Check(side)];

    /// <summary>The side's score as its counter shows it (counting up as the game's does).</summary>
    public long ShownScore(int side) => counters[Check(side)].Shown;

    /// <summary>The last finished turn, or null before any.</summary>
    public MoonfallDuelTurn? LastTurn { get; private set; }

    /// <summary>How it ended, from the player's side; <see cref="MoonfallDuelOutcome.Undecided"/> while it is played.</summary>
    public MoonfallDuelOutcome Outcome { get; private set; }

    /// <summary>The player shoots at <paramref name="angleDegrees"/>; false unless it is the player's turn.</summary>
    public bool Shoot(double angleDegrees) => PlayersTurn && ShootFor(PlayerSide, angleDegrees);

    /// <summary>Runs whole real ticks for <paramref name="realSeconds"/> of wall-clock time, as <see cref="MoonfallGame.Advance"/>.</summary>
    public void Advance(double realSeconds)
    {
        Game.AddTime(realSeconds);
        while (Game.TickDue)
        {
            BeforeTick();
            Game.TakeTick();
            AfterTick();
        }
    }

    /// <summary>One real tick: the opponent's thought or shot, the board's tick, then the turn's end if it came.</summary>
    public void Tick()
    {
        BeforeTick();
        Game.Tick();
        AfterTick();
    }

    private void BeforeTick()
    {
        if (Outcome != MoonfallDuelOutcome.Undecided || Game.Side != OpponentSide || Game.Phase != MoonfallPhase.Aiming || turnOpen)
        {
            return;
        }

        if (!Opponent.Thinking)
        {
            Opponent.Begin(Game);
        }

        if (Opponent.Step(Game, out var angle))
        {
            ShootFor(OpponentSide, angle);
        }
    }

    private void AfterTick()
    {
        counters[PlayerSide].Tick();
        counters[OpponentSide].Tick();
        if (turnOpen && Game.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost)
        {
            CloseTurn();
        }
    }

    private bool ShootFor(int side, double angle)
    {
        var score = Game.Score;
        var oranges = Game.OrangesLeft;
        var before = Game.BallsLeft;
        if (balls[side] <= 0 || !Game.Shoot(angle))
        {
            return false;
        }

        turnOpen = true;
        shooter = side;
        turnScore = score;
        turnOranges = oranges;
        turnBalls = before;
        balls[side]--;
        return true;
    }

    private void CloseTurn()
    {
        turnOpen = false;
        var scored = Game.Score - turnScore;
        var oranges = turnOranges - Game.OrangesLeft;
        var free = Math.Max(0, Game.BallsLeft - (turnBalls - 1));
        var kept = oranges > 0 ? scored : scored * MoonfallRules.DuelNoOrangeKeepPercent / 100;
        balls[shooter] += free;
        scores[shooter] += kept;
        counters[shooter].SetTarget(scores[shooter], MoonfallRules.CountUpHoldTicks);
        LastTurn = new MoonfallDuelTurn(shooter, scored, kept, oranges, free);

        if (Game.Phase is MoonfallPhase.Won or MoonfallPhase.Lost)
        {
            Outcome = scores[PlayerSide] > scores[OpponentSide] ? MoonfallDuelOutcome.Won
                : scores[PlayerSide] < scores[OpponentSide] ? MoonfallDuelOutcome.Lost
                : MoonfallDuelOutcome.Drawn;
            Opponent.Cancel();
            return;
        }

        // The other side shoots next if it has a ball; otherwise this side shoots on.
        var other = 1 - shooter;
        var nextSide = balls[other] > 0 ? other : shooter;
        Game.HandOver(nextSide, PowerOf(companions[nextSide]));
    }

    private static MoonfallPower PowerOf(MoonfallCompanion companion) =>
        MoonfallCompanions.TryGet(companion, out var info) ? info.Power : MoonfallPower.None;

    private static int Check(int side) => side is PlayerSide or OpponentSide ? side : throw new ArgumentOutOfRangeException(nameof(side), side, "a side is 0 or 1");
}
