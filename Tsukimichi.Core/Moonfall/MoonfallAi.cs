namespace Tsukimichi.Core.Moonfall;

/// <summary>How well a duel's opponent plays (plan v9 G7). Never renumber: progress keys duel records by name.</summary>
public enum MoonfallAiDifficulty : byte
{
    /// <summary>Weighs a few angles, aims loosely and often takes a good shot rather than the best.</summary>
    Novice,

    /// <summary>Weighs twice as many, aims fairly well, takes the best it finds.</summary>
    Adept,

    /// <summary>Weighs every 1.5°, aims closely, and prizes the oranges the meter pays for.</summary>
    Master,
}

/// <summary>How one difficulty plays (<see cref="MoonfallAi.ProfileOf"/>).</summary>
/// <param name="StepDegrees">The angles it weighs, this far apart from −84° to 84°.</param>
/// <param name="AimErrorDegrees">How far it may miss the angle it chose, either way.</param>
/// <param name="SubStepsPerShot">One weighed angle's budget of physics sub-steps.</param>
/// <param name="AnglesPerTick">How many angles it weighs in one game tick (the cost of a tick is this × the budget at most).</param>
/// <param name="TopChoices">It takes one of its best this many, at random (1: always the best).</param>
/// <param name="OrangeWeight">What an orange the probe touches adds to a shot's worth (beside the pegs' value × count).</param>
/// <param name="CatchWeight">What a bucket catch adds.</param>
/// <param name="MinThinkTicks">The fewest game ticks it waits at the launcher, so its turn reads as a turn.</param>
public readonly record struct MoonfallAiProfile(
    double StepDegrees,
    double AimErrorDegrees,
    int SubStepsPerShot,
    int AnglesPerTick,
    int TopChoices,
    long OrangeWeight,
    long CatchWeight,
    int MinThinkTicks)
{
    /// <summary>How many angles it weighs a shot.</summary>
    public int Angles => (int)Math.Floor((2 * MoonfallAi.Reach / StepDegrees) + 1e-9) + 1;

    /// <summary>The game ticks its weighing takes (it shoots on the tick after, or after <see cref="MinThinkTicks"/>).</summary>
    public int WeighTicks => (Angles + AnglesPerTick - 1) / AnglesPerTick;

    /// <summary>The most physics sub-steps one shot's weighing takes.</summary>
    public int SubStepsPerTurn => Angles * SubStepsPerShot;
}

/// <summary>
/// A duel's opponent (plan v9 G7): it picks each shot by flying probe balls on the live board (the engine's own
/// physics, <see cref="MoonfallGame.WeighShot"/>, as Sage's Path weighs its lines) at a spread of angles, each with a
/// bounded budget, a few a game tick, so a shot's weighing never costs a frame more than
/// <see cref="MoonfallAiProfile.AnglesPerTick"/> × <see cref="MoonfallAiProfile.SubStepsPerShot"/> sub-steps. It decides
/// when its turn starts on which tick it will shoot, so every probe sees the bucket where it will be, and it shoots on
/// that tick. Deterministic: the same seed and the same game give the same shots. Allocates nothing per tick.
/// </summary>
public sealed class MoonfallAi
{
    /// <summary>The widest angle it weighs either way (inside the launcher's limit, as the designer's greedy player).</summary>
    public const double Reach = 84;

    private readonly double[] bestAngles;
    private readonly long[] bestWorths;
    private MoonfallRandom random;
    private int next = -1;
    private long launchTick;

    public MoonfallAi(MoonfallAiDifficulty difficulty, ulong seed)
    {
        Difficulty = difficulty;
        Profile = ProfileOf(difficulty);
        random = new MoonfallRandom(seed ^ 0xD1B54A32D192ED03UL);
        bestAngles = new double[Profile.TopChoices];
        bestWorths = new long[Profile.TopChoices];
    }

    public MoonfallAiDifficulty Difficulty { get; }

    public MoonfallAiProfile Profile { get; }

    /// <summary>Weighing a shot (between <see cref="Begin"/> and the shot).</summary>
    public bool Thinking => next >= 0;

    /// <summary>The game tick it will shoot on (while <see cref="Thinking"/>).</summary>
    public long LaunchTick => launchTick;

    /// <summary>The physics sub-steps its last game tick of weighing took (the cost test reads it).</summary>
    public int LastTickSubSteps { get; private set; }

    /// <summary>The physics sub-steps its last shot's weighing took in all.</summary>
    public int LastTurnSubSteps { get; private set; }

    /// <summary>The angle it shot last (after its aim error).</summary>
    public double LastAngle { get; private set; }

    /// <summary>
    /// [J] The three difficulties. The costs are bounded by the shipped levels' Sage's Path figures (24,000 sub-steps, a
    /// couple of milliseconds, on the largest): at most 12,000 sub-steps a tick, about a millisecond.
    /// </summary>
    public static MoonfallAiProfile ProfileOf(MoonfallAiDifficulty difficulty) => difficulty switch
    {
        MoonfallAiDifficulty.Novice => new(6.0, 5.0, 2_000, 6, 3, 500, 2_000, 80),
        MoonfallAiDifficulty.Adept => new(3.0, 2.0, 3_000, 4, 1, 1_500, 6_000, 70),
        _ => new(1.5, 0.75, 4_000, 3, 1, 3_000, 10_000, 60),
    };

    /// <summary>Starts weighing a shot on <paramref name="game"/>, which must be waiting for one: it will shoot on <see cref="LaunchTick"/>.</summary>
    public void Begin(MoonfallGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        next = 0;
        launchTick = game.GameTick + Math.Max(Profile.MinThinkTicks, Profile.WeighTicks + 1);
        LastTurnSubSteps = 0;
        Array.Fill(bestWorths, long.MinValue);
        Array.Fill(bestAngles, 0.0);
    }

    /// <summary>Stops weighing (the duel ended or the turn passed).</summary>
    public void Cancel() => next = -1;

    /// <summary>
    /// One game tick of its turn: weighs the next angles; on <see cref="LaunchTick"/> it returns true with the angle to
    /// shoot (the best it found, or one of its best few, with its aim error). False while it is still weighing or waiting.
    /// </summary>
    public bool Step(MoonfallGame game, out double angle)
    {
        ArgumentNullException.ThrowIfNull(game);
        angle = 0;
        if (next < 0)
        {
            return false;
        }

        LastTickSubSteps = 0;
        var angles = Profile.Angles;
        var ticksFromNow = (int)Math.Max(0, launchTick - game.GameTick);
        for (var k = 0; k < Profile.AnglesPerTick && next < angles; k++, next++)
        {
            var candidate = -Reach + (next * Profile.StepDegrees);
            var weight = game.WeighShot(candidate, Profile.SubStepsPerShot, ticksFromNow);
            LastTickSubSteps += weight.SubSteps;
            Keep(candidate, Worth(weight));
        }

        LastTurnSubSteps += LastTickSubSteps;
        if (next < angles || game.GameTick < launchTick)
        {
            return false;
        }

        // One of its best few (the best on Adept and Master), then its aim error.
        var kept = 0;
        while (kept < bestWorths.Length && bestWorths[kept] != long.MinValue)
        {
            kept++;
        }

        var pick = kept > 1 ? random.Next(kept) : 0;
        var chosen = bestAngles[pick];
        var error = ((random.NextBits() >> 11) * (1.0 / (1UL << 53)) * 2 * Profile.AimErrorDegrees) - Profile.AimErrorDegrees;
        angle = Math.Clamp(chosen + error, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees);
        LastAngle = angle;
        next = -1;
        return true;
    }

    /// <summary>What a probe's flight is worth to it: the pegs' value × their count (as a shot scores), and its weights.</summary>
    private long Worth(in MoonfallShotWeight w) =>
        (w.Value * w.Pegs) + (w.Oranges * Profile.OrangeWeight) + (w.Caught ? Profile.CatchWeight : 0);

    /// <summary>Keeps the best few angles, best first; an equal worth keeps the earlier angle.</summary>
    private void Keep(double candidate, long worth)
    {
        var slot = bestWorths.Length;
        while (slot > 0 && worth > bestWorths[slot - 1])
        {
            slot--;
        }

        if (slot == bestWorths.Length)
        {
            return;
        }

        for (var k = bestWorths.Length - 1; k > slot; k--)
        {
            bestWorths[k] = bestWorths[k - 1];
            bestAngles[k] = bestAngles[k - 1];
        }

        bestWorths[slot] = worth;
        bestAngles[slot] = candidate;
    }
}
