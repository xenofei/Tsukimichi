using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The holder behind the duty index (1.11.0, S3): built once on a worker, read for free, never built by the reader.
/// Before 1.11 a Lazy built it on the draw thread and cached a failed build as null for good.
/// </summary>
public sealed class WarmedValueTests
{
    public WarmedValueTests() => TestThreads.EnsurePool();

    [Fact]
    public void Nothing_is_built_before_start()
    {
        var builds = 0;
        var warmed = new WarmedValue<string>(() =>
        {
            builds++;
            return "index";
        });

        Assert.Null(warmed.Value);
        Assert.Null(warmed.Value);
        Assert.Equal(0, builds);
    }

    [Fact]
    public async Task The_value_lands_after_the_build()
    {
        var warmed = new WarmedValue<string>(static () => "index");

        await warmed.Start();

        Assert.Equal("index", warmed.Value);
    }

    [Fact]
    public async Task Start_builds_once_however_often_it_is_called()
    {
        var builds = 0;
        var warmed = new WarmedValue<object>(() =>
        {
            Interlocked.Increment(ref builds);
            return new object();
        });

        var first = warmed.Start();
        var second = warmed.Start();
        await Task.WhenAll(first, second, warmed.Start());

        Assert.Equal(1, builds);
        Assert.Same(warmed.Value, warmed.Value);
    }

    [Fact]
    public async Task The_build_runs_off_the_calling_thread_and_the_reader_never_waits()
    {
        using var gate = new ManualResetEventSlim();
        var caller = Environment.CurrentManagedThreadId;
        var builtOn = caller;
        var warmed = new WarmedValue<string>(() =>
        {
            builtOn = Environment.CurrentManagedThreadId;
            gate.Wait(TimeSpan.FromSeconds(10));
            return "index";
        });

        var task = warmed.Start();
        Assert.Null(warmed.Value);
        gate.Set();
        await task;

        Assert.NotEqual(caller, builtOn);
        Assert.Equal("index", warmed.Value);
    }

    [Fact]
    public async Task A_failed_build_reads_as_null_and_is_reported()
    {
        Exception? reported = null;
        var warmed = new WarmedValue<string>(static () => throw new InvalidOperationException("no language"), ex => reported = ex);

        await warmed.Start();

        Assert.Null(warmed.Value);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public async Task Wait_starts_the_build_once_and_waits_for_the_one_under_way()
    {
        using var gate = new ManualResetEventSlim();
        var builds = 0;
        var warmed = new WarmedValue<string>(() =>
        {
            Interlocked.Increment(ref builds);
            gate.Wait(TimeSpan.FromSeconds(10));
            return "index";
        });

        _ = warmed.Start();
        Assert.False(warmed.IsDone);
        var waiter = Task.Run(warmed.Wait);
        gate.Set();

        Assert.Equal("index", await waiter);
        Assert.Equal("index", warmed.Wait());
        Assert.Equal(1, builds);
        Assert.True(warmed.IsDone);
        Assert.True(warmed.BuildMs >= 0);
    }

    [Fact]
    public void Wait_without_a_start_builds_on_a_worker_and_a_failure_reads_as_null()
    {
        Assert.Equal("index", new WarmedValue<string>(static () => "index").Wait());
        Assert.Null(new WarmedValue<string>(static () => throw new InvalidOperationException("no sheet")).Wait());
    }
}
