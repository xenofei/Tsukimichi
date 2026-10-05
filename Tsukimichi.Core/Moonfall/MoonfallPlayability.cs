namespace Tsukimichi.Core.Moonfall;

/// <summary>One game of the greedy player (<see cref="MoonfallPlayability"/>).</summary>
/// <param name="Won">It won the level.</param>
/// <param name="Shots">The shots it took.</param>
/// <param name="OrangesLeft">Oranges left at the end (0 when won).</param>
/// <param name="Score">The level's score at the end.</param>
/// <param name="StuckFires">How often the stuck-ball rule fired in its real shots.</param>
public readonly record struct MoonfallGreedyGame(bool Won, int Shots, int OrangesLeft, long Score, int StuckFires);

/// <summary>The greedy player's verdict on one level (plan v9 decision 21).</summary>
/// <param name="LevelId">The level.</param>
/// <param name="Games">Every game, in seed order.</param>
public sealed record MoonfallPlayabilityReport(string LevelId, IReadOnlyList<MoonfallGreedyGame> Games)
{
    /// <summary>Games won.</summary>
    public int Wins => Games.Count(static g => g.Won);

    /// <summary>Whether the level is playable: at least <see cref="MoonfallPlayability.WinsNeeded"/> wins.</summary>
    public bool Passes => Wins >= MoonfallPlayability.WinsNeeded;
}

/// <summary>
/// The playability gate (plan v9 decision 21): "every level must play at least as well as the weakest shipped level in
/// the greedy-player test (5 of 48 wins)". The greedy player is the designer's (<c>docs/design/v9/rich/tools/mfcheck</c>,
/// <c>play</c>), ported here so the rule can run over every shipped level in the tests, move for move the same: before
/// each shot it tries every angle 2° apart from −84° to 84° on a copy of the game, takes the one that lights the most
/// (an orange worth 6 pegs more, a bucket catch 5, each stuck-ball fire −0.5), then misses its aim by up to 1.5°.
/// Game <c>k</c> (from 0) is dealt with seed <c>(k + 1) × 7919</c> and misses its aim with
/// <see cref="Random"/>(<c>k × 31 + 7</c>), as the tool's: the threshold was set on the tool's numbers, so the port
/// keeps its random numbers too (a seeded <see cref="Random"/> is the same sequence on every runtime). It plays level
/// number 5 (greens, no power) and gives up after 41 shots. The tool replays the game from the start for every trial;
/// the port copies it (<see cref="MoonfallGame.CopyFrom"/>), which is the same game bit for bit and far cheaper.
/// </summary>
public static class MoonfallPlayability
{
    /// <summary>Games a level is played (decision 21).</summary>
    public const int Games = 48;

    /// <summary>Wins a level needs (decision 21).</summary>
    public const int WinsNeeded = 5;

    /// <summary>The level number the games are played at: the tool's default, with greens and no power.</summary>
    public const int LevelNumber = 5;

    /// <summary>The tool's limit: a game still going after this many shots is a loss.</summary>
    public const int MaxShots = 40;

    /// <summary>The tool's trial angles: every 2° from −84° to 84°.</summary>
    public const double TrialFrom = -84.0;

    /// <inheritdoc cref="TrialFrom"/>
    public const double TrialTo = 84.0;

    /// <inheritdoc cref="TrialFrom"/>
    public const double TrialStep = 2.0;

    /// <summary>The tool's aim error: up to this many degrees either way.</summary>
    public const double AimError = 1.5;

    /// <summary>The tool's limits on one flight and on the clearing after it (game ticks).</summary>
    private const int FlightLimit = 30_000;

    private const int SettleLimit = 20_000;

    /// <summary>Plays <paramref name="games"/> games of <paramref name="level"/>, in parallel; the result does not depend on the order they finish in.</summary>
    public static MoonfallPlayabilityReport Check(MoonfallLevel level, int games = Games)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfLessThan(games, 1);
        var results = new MoonfallGreedyGame[games];
        Parallel.For(0, games, k => results[k] = PlayOne(level, k));
        return new MoonfallPlayabilityReport(level.Id, results);
    }

    /// <summary>Game <paramref name="index"/> (from 0) of the greedy player on <paramref name="level"/>.</summary>
    public static MoonfallGreedyGame PlayOne(MoonfallLevel level, int index)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var seed = (ulong)(index + 1) * 7919UL;
        var aimError = new Random((index * 31) + 7);
        var live = new MoonfallGame(level, LevelNumber, seed);
        var trial = new MoonfallGame(level, LevelNumber, seed);
        var seen = new int[level.Pegs.Count];
        var stamp = 0;
        var shots = 0;
        var stuck = 0;
        while (live.Phase is not (MoonfallPhase.Won or MoonfallPhase.Lost) && shots <= MaxShots)
        {
            var best = 0.0;
            var bestScore = double.MinValue;
            for (var angle = TrialFrom; angle <= TrialTo; angle += TrialStep)
            {
                trial.CopyFrom(live);
                var (pegs, oranges, caught, trialStuck) = Fly(trial, angle, seen, ++stamp);
                var score = pegs + (oranges * 6) + (caught ? 5 : 0) - (trialStuck * 0.5);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = angle;
                }
            }

            var aim = best + ((aimError.NextDouble() * 2 * AimError) - AimError);
            stuck += Fly(live, aim, seen, ++stamp).Stuck;
            Settle(live);
            shots++;
        }

        return new MoonfallGreedyGame(live.Phase == MoonfallPhase.Won, shots, live.OrangesLeft, live.Score, stuck);
    }

    /// <summary>One shot's flight: the pegs it lit (each once), the oranges among them, a catch, and stuck-ball fires.</summary>
    private static (int Pegs, int Oranges, bool Caught, int Stuck) Fly(MoonfallGame game, double angle, int[] seen, int stamp)
    {
        if (!game.Shoot(angle))
        {
            return (0, 0, false, 0);
        }

        int pegs = 0, oranges = 0, stuck = 0;
        var caught = false;
        for (var i = 0; i < FlightLimit && game.Phase == MoonfallPhase.Flying; i++)
        {
            game.Tick();
            while (game.TryReadEvent(out var e))
            {
                switch (e.Kind)
                {
                    case MoonfallEventKind.PegHit when seen[e.Peg] != stamp:
                        seen[e.Peg] = stamp;
                        pegs++;
                        break;
                    case MoonfallEventKind.StuckClear:
                        stuck++;
                        break;
                    case MoonfallEventKind.BucketCatch:
                        caught = true;
                        break;
                }
            }
        }

        // The oranges among them, by their colour once the flight is over (as the tool counts them).
        for (var i = 0; i < seen.Length; i++)
        {
            if (seen[i] == stamp && game.Peg(i).Colour == PegColour.Orange)
            {
                oranges++;
            }
        }

        return (pegs, oranges, caught, stuck);
    }

    /// <summary>The turn's pegs clear and the next ball comes (or the level ends).</summary>
    private static void Settle(MoonfallGame game)
    {
        for (var i = 0; i < SettleLimit && game.Phase is not (MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost); i++)
        {
            game.Tick();
            while (game.TryReadEvent(out _))
            {
            }
        }
    }
}
