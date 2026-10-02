using System.Diagnostics;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// A save must never freeze the game (1.2 review): a read-only or ACL-denied target fails at once instead of being
/// retried for a second, the framework thread's budget is a few milliseconds, and the heavy saves run on one
/// background writer whose completions come back to the caller's thread.
/// </summary>
public sealed class SaveBudgetTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    [Fact]
    public void A_read_only_target_fails_and_leaves_no_temporary_file()
    {
        WriteOverReadOnlyTarget();
    }

    /// <summary>
    /// The wall-clock half of the read-only rule (<see cref="Retry_rules_never_wait_out_what_waiting_cannot_change"/>
    /// is the deterministic half): a Perf test, since a busy machine can stretch even one attempt past the budget.
    /// </summary>
    [Fact]
    [Trait("Category", "Perf")]
    public void A_read_only_target_fails_at_once()
    {
        var elapsed = WriteOverReadOnlyTarget();

        // The default budget is ~1.1 s of retries; a read-only file is not retried at all.
        Assert.True(elapsed < TimeSpan.FromMilliseconds(200), $"took {elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public void Retry_rules_never_wait_out_what_waiting_cannot_change()
    {
        var sharing = new IOException("in use");
        var denied = new UnauthorizedAccessException("denied");
        const int Full = AtomicFile.DefaultAttempts;

        // In use (a sharing violation, or Windows' "access denied" while a reader holds the file): retried until the
        // budget runs out, a long one on the writer, a few milliseconds' worth on the framework thread.
        Assert.True(AtomicFile.ShouldRetry(sharing, 1, Full, AtomicFile.Refusal.InUse, 0));
        Assert.True(AtomicFile.ShouldRetry(denied, 49, Full, AtomicFile.Refusal.InUse, 0));
        Assert.False(AtomicFile.ShouldRetry(sharing, Full, Full, AtomicFile.Refusal.InUse, 0));
        Assert.False(AtomicFile.ShouldRetry(sharing, AtomicFile.QuickAttempts, AtomicFile.QuickAttempts, AtomicFile.Refusal.InUse, 0));

        // Access denied on a file nobody holds (permissions, a pending delete): a few tries, then final.
        Assert.True(AtomicFile.ShouldRetry(denied, 1, Full, AtomicFile.Refusal.Denied, 1));
        Assert.True(AtomicFile.ShouldRetry(denied, 20, Full, AtomicFile.Refusal.Denied, AtomicFile.AccessDeniedAttempts - 1));
        Assert.False(AtomicFile.ShouldRetry(denied, 3, Full, AtomicFile.Refusal.Denied, AtomicFile.AccessDeniedAttempts));

        // A read-only target: never.
        Assert.False(AtomicFile.ShouldRetry(denied, 1, Full, AtomicFile.Refusal.ReadOnly, 0));

        // A missing source or folder: never.
        Assert.False(AtomicFile.ShouldRetry(new FileNotFoundException(), 1, Full, AtomicFile.Refusal.InUse, 0));
        Assert.False(AtomicFile.ShouldRetry(new DirectoryNotFoundException(), 1, Full, AtomicFile.Refusal.InUse, 0));
    }

    [Fact]
    public void The_full_budget_waits_out_a_held_file_far_longer_than_the_quick_one()
    {
        var path = tmp.File("snap.json");
        AtomicFile.Write(path, "first");

        // Another program holds the file: no rename can land while it does (Windows says "access denied").
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.ThrowsAny<Exception>(() => AtomicFile.Write(path, "second", AtomicFile.QuickAttempts));

            // The writer's budget waits a reader out far longer: this is sharing, not a refusal for good. A lower
            // bound, so a slow machine only makes it pass by more.
            var full = Stopwatch.StartNew();
            Assert.ThrowsAny<Exception>(() => AtomicFile.Write(path, "second"));
            full.Stop();
            Assert.True(full.ElapsedMilliseconds > 500, $"default budget gave up after {full.ElapsedMilliseconds} ms");
        }

        Assert.Equal("first", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(tmp.Path, "*" + AtomicFile.TempSuffix));
    }

    /// <summary>The framework thread's budget is a few milliseconds; an upper bound on wall-clock time, hence Perf.</summary>
    [Fact]
    [Trait("Category", "Perf")]
    public void The_quick_budget_gives_up_on_a_held_file_within_milliseconds()
    {
        var path = tmp.File("snap.json");
        AtomicFile.Write(path, "first");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var quick = Stopwatch.StartNew();
            Assert.ThrowsAny<Exception>(() => AtomicFile.Write(path, "second", AtomicFile.QuickAttempts));
            quick.Stop();
            Assert.True(quick.ElapsedMilliseconds < 100, $"quick budget took {quick.ElapsedMilliseconds} ms");
        }

        Assert.Equal("first", File.ReadAllText(path));
    }

    [Fact]
    public void The_writer_runs_work_in_order_off_the_caller_and_hands_outcomes_back_on_drain()
    {
        using var writer = new SerialWriter();
        var caller = Environment.CurrentManagedThreadId;
        var order = new List<int>();
        var threads = new List<int>();
        var outcomes = new List<(int? Result, string? Error)>();
        using var gate = new ManualResetEventSlim();

        writer.Enqueue(
            () =>
            {
                gate.Wait(TimeSpan.FromSeconds(5));
                lock (order)
                {
                    order.Add(1);
                    threads.Add(Environment.CurrentManagedThreadId);
                }

                return 1;
            },
            (result, error) => outcomes.Add((result, error?.Message)));
        writer.Enqueue<int>(
            () =>
            {
                lock (order)
                {
                    order.Add(2);
                }

                throw new IOException("disk full");
            },
            (result, error) => outcomes.Add((result, error?.Message)));

        // Queued, not run: the caller never waits on the work.
        Assert.Equal(2, writer.Pending);
        Assert.Equal(0, writer.DrainCompletions());

        gate.Set();
        Assert.True(writer.WaitIdle(TimeSpan.FromSeconds(5)));
        Assert.Empty(outcomes);

        // Completions run only when drained, on the draining thread, oldest first.
        Assert.Equal(2, writer.DrainCompletions());
        Assert.Equal([1, 2], order);
        Assert.DoesNotContain(caller, threads);
        Assert.Equal((1, null), outcomes[0]);
        Assert.Equal("disk full", outcomes[1].Error);
    }

    [Fact]
    public void Dispose_waits_for_queued_saves_and_runs_later_ones_inline()
    {
        var path = tmp.File("pins.json");
        var writer = new SerialWriter();
        writer.Enqueue(() => AtomicFile.Write(path, "queued"));
        writer.Dispose();
        Assert.Equal("queued", File.ReadAllText(path));

        // After dispose (unload), a last save is never dropped.
        writer.Enqueue(() => AtomicFile.Write(path, "late"));
        Assert.Equal("late", File.ReadAllText(path));
    }

    [Fact]
    public void Close_waits_no_longer_than_it_is_given_and_the_queued_save_still_lands()
    {
        // Unload shares one deadline among its waits: a writer stuck on a slow disk gives up its share, not more.
        var path = tmp.File("pins.json");
        using var gate = new ManualResetEventSlim();
        var writer = new SerialWriter();
        writer.Enqueue(() =>
        {
            gate.Wait(TimeSpan.FromSeconds(5));
            AtomicFile.Write(path, "queued");
            return 0;
        });

        Assert.False(writer.Close(TimeSpan.Zero));

        gate.Set();
        Assert.True(writer.Close(TimeSpan.FromSeconds(5)));
        Assert.Equal("queued", File.ReadAllText(path));
    }

    /// <summary>A write over a read-only file throws, keeps the old contents and leaves no temporary file; returns how long it took.</summary>
    private TimeSpan WriteOverReadOnlyTarget()
    {
        var path = tmp.File("snap.json");
        AtomicFile.Write(path, "first");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var watch = Stopwatch.StartNew();
            Assert.Throws<UnauthorizedAccessException>(() => AtomicFile.Write(path, "second"));
            watch.Stop();

            Assert.Equal("first", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(tmp.Path, "*" + AtomicFile.TempSuffix));
            return watch.Elapsed;
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }
}
