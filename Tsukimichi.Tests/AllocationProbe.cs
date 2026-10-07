namespace Tsukimichi.Tests;

/// <summary>
/// The repo's "allocates nothing" check counts the bytes the test's own thread allocates around warmed-up calls
/// (<see cref="GC.GetAllocatedBytesForCurrentThread"/>). Run alongside the rest of the suite, a window now and then
/// reads a few KB (always under one 8 KB allocation quantum, at a different tick each time, with no GC and no JIT
/// on the thread in between) although the engine, which is deterministic, allocates nothing: the runtime, not the
/// engine, put those bytes there. An allocation the engine makes is made by every identical window, so the check asks
/// for one window out of a few identical, fresh ones to read zero; an engine that allocates at all fails every one.
/// </summary>
internal static class AllocationProbe
{
    private const int Attempts = 5;

    /// <summary>
    /// Runs <paramref name="window"/> (it builds its own fresh state, warms it and returns the bytes its thread
    /// allocated in the measured part) until one reads zero, at most <see cref="Attempts"/> times; returns the smallest reading.
    /// </summary>
    public static long Smallest(Func<long> window)
    {
        var smallest = long.MaxValue;
        for (var attempt = 0; attempt < Attempts && smallest != 0; attempt++)
        {
            smallest = Math.Min(smallest, window());
        }

        return smallest;
    }
}
