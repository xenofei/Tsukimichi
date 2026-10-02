namespace Tsukimichi.Tests.Storage;

/// <summary>
/// Another program's read handle on a file (Windows then refuses to replace it), let go after a short hold. The
/// release runs on a dedicated thread, not the thread pool: the parallel test run can starve the pool for longer than
/// a write's whole retry budget (~1.3 s), and a release that comes late would fail the test for no fault of the code.
/// Dispose waits for the release, so the file is free afterwards whatever happened in between.
/// </summary>
internal sealed class HeldFile : IDisposable
{
    private readonly FileStream reader;
    private readonly Thread releaser;

    private HeldFile(string path, TimeSpan hold)
    {
        reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        releaser = new Thread(() =>
        {
            Thread.Sleep(hold);
            reader.Dispose();
        })
        {
            IsBackground = true,
            Name = "HeldFile release",
        };
        releaser.Start();
    }

    /// <summary>Opens <paramref name="path"/> for reading now and closes it after <paramref name="hold"/>.</summary>
    public static HeldFile ReleasedAfter(string path, TimeSpan hold) => new(path, hold);

    public void Dispose()
    {
        releaser.Join();
        reader.Dispose();
    }
}
