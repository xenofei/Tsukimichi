namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The art builds' parallel loops, kept to a few workers: they run inside the game's process beside its own threads,
/// so a scene build may use some cores, never all of them (spinning workers on every core would show as the game's frame
/// time). <see cref="Workers"/> is half the machine's cores, at least 1 and at most 4.
/// </summary>
public static class MoonfallParallel
{
    /// <summary>How many workers a build's loop uses at most.</summary>
    public static readonly int Workers = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    private static readonly ParallelOptions Options = new() { MaxDegreeOfParallelism = Workers };

    /// <summary><see cref="Parallel.For(int, int, Action{int})"/> with at most <see cref="Workers"/> workers.</summary>
    public static void For(int fromInclusive, int toExclusive, Action<int> body) => Parallel.For(fromInclusive, toExclusive, Options, body);
}
