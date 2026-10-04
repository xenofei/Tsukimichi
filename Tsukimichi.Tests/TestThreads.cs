namespace Tsukimichi.Tests;

/// <summary>
/// Tests that hand work to the thread pool and wait for it (the save writer) share the pool with tests that block pool
/// threads on purpose (a gate held for seconds). Under the full suite the pool then runs short and grows slowly, so
/// queued work can sit unstarted past a test's wait. <see cref="EnsurePool"/> raises the pool's floor once, so a
/// blocked test cannot starve the others; it changes no deadline and no assertion.
/// </summary>
internal static class TestThreads
{
    private const int MinWorkers = 64;

    private static readonly Lazy<bool> Raised = new(static () =>
    {
        ThreadPool.GetMinThreads(out var workers, out var io);
        return workers >= MinWorkers || ThreadPool.SetMinThreads(MinWorkers, io);
    });

    /// <summary>Raises the thread pool's minimum worker count for this test run (once).</summary>
    public static void EnsurePool() => _ = Raised.Value;
}
