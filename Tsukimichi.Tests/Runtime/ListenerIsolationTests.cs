using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The session's change event fans out to Discovery, the chat notices, the todo overlay, Since you were away and the
/// IPC gates: one listener that throws must not keep the others on stale data, and a listener that throws on every
/// change must not flood the log.
/// </summary>
public sealed class ListenerIsolationTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<(string Listener, Exception Error, int Held)> reports = [];
    private DateTime now = Start;

    private ListenerIsolation Isolation() => new((listener, error, held) => reports.Add((listener, error, held)), TimeSpan.FromMinutes(1), () => now);

    [Fact]
    public void Every_listener_runs_in_order_when_an_earlier_one_throws()
    {
        var isolation = Isolation();
        var ran = new List<string>();
        Action? changed = null;
        changed += () => ran.Add("discovery");
        changed += () => throw new InvalidOperationException("chat broke");
        changed += () => ran.Add("todo");
        changed += () => ran.Add("ipc");

        var failed = isolation.Raise(changed);

        Assert.Equal(1, failed);
        Assert.Equal(["discovery", "todo", "ipc"], ran);
        var report = Assert.Single(reports);
        Assert.Equal("chat broke", report.Error.Message);
        Assert.Equal(0, report.Held);
        Assert.Contains(nameof(ListenerIsolationTests), report.Listener);
    }

    [Fact]
    public void The_argument_reaches_every_listener_of_a_one_argument_event()
    {
        var isolation = Isolation();
        var seen = new List<ulong>();
        Action<ulong>? forgotten = null;
        forgotten += id => throw new InvalidOperationException("spoilers broke");
        forgotten += seen.Add;

        Assert.Equal(1, isolation.Raise(forgotten, 42UL));
        Assert.Equal([42UL], seen);
    }

    [Fact]
    public void No_listeners_is_no_failure()
    {
        var isolation = Isolation();

        Assert.Equal(0, isolation.Raise(null));
        Assert.Equal(0, isolation.Raise<int>(null, 1));
        Assert.Empty(reports);
    }

    [Fact]
    public void A_listener_failing_on_every_change_is_reported_once_per_quiet_period_with_the_count_held_back()
    {
        var isolation = Isolation();
        Action changed = ThrowingListener;

        for (var i = 0; i < 5; i++)
        {
            isolation.Raise(changed);
            now = now.AddSeconds(10);
        }

        // 0 s reported; 10, 20, 30 and 40 s held back.
        Assert.Single(reports);

        now = Start.AddMinutes(1);
        isolation.Raise(changed);

        Assert.Equal(2, reports.Count);
        Assert.Equal(4, reports[1].Held);
        Assert.Equal(reports[0].Listener, reports[1].Listener);
    }

    [Fact]
    public void Two_failing_listeners_are_each_reported()
    {
        var isolation = Isolation();
        Action? changed = null;
        changed += ThrowingListener;
        changed += OtherThrowingListener;

        Assert.Equal(2, isolation.Raise(changed));
        Assert.Equal(2, reports.Count);
        Assert.NotEqual(reports[0].Listener, reports[1].Listener);
    }

    [Fact]
    public void A_reporter_that_throws_does_not_stop_the_remaining_listeners()
    {
        var isolation = new ListenerIsolation((_, _, _) => throw new IOException("log full"));
        var ran = false;
        Action? changed = null;
        changed += ThrowingListener;
        changed += () => ran = true;

        Assert.Equal(1, isolation.Raise(changed));
        Assert.True(ran);
    }

    [Fact]
    public void The_listener_name_is_its_type_and_method()
    {
        Assert.Equal($"{typeof(ListenerIsolationTests).FullName}.{nameof(ThrowingListener)}", ListenerIsolation.Describe(new Action(ThrowingListener)));
    }

    private static void ThrowingListener() => throw new InvalidOperationException("always");

    private static void OtherThrowingListener() => throw new InvalidOperationException("also always");
}
