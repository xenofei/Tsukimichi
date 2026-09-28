using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class PollScheduleTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private static PollSchedule NewSchedule() => new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));

    [Fact]
    public void Healthy_schedule_is_due_once_per_interval()
    {
        var s = NewSchedule();
        Assert.True(s.IsDue(T0, Interval));

        s.Attempt(T0);

        Assert.False(s.IsDue(T0.AddSeconds(0.9), Interval));
        Assert.True(s.IsDue(T0.AddSeconds(1), Interval));
        Assert.False(s.IsBackingOff);
        Assert.Equal(0, s.Succeed());
    }

    [Fact]
    public void Starting_a_first_pass_leaves_a_backoff_standing()
    {
        // A game read threw; the next poll captured fine but only started a first pass, so the poller does not
        // report a success for it. The cadence stays the backoff's until that pass commits.
        var s = NewSchedule();
        s.Attempt(T0);
        Assert.True(s.Fail(T0));

        s.Attempt(T0.AddSeconds(2));

        Assert.True(s.IsBackingOff);
        Assert.False(s.IsDue(T0.AddSeconds(3.5), Interval));
        Assert.True(s.IsDue(T0.AddSeconds(4), Interval));
    }

    [Fact]
    public void A_faulted_first_pass_backs_off_from_the_fault_not_from_its_capture()
    {
        var s = NewSchedule();
        s.Attempt(T0);

        // The worker faulted three seconds after the capture: the retry waits the backoff step from there.
        Assert.True(s.Fail(T0.AddSeconds(3)));

        Assert.Equal(TimeSpan.FromSeconds(2), s.Wait);
        Assert.False(s.IsDue(T0.AddSeconds(4.9), Interval));
        Assert.True(s.IsDue(T0.AddSeconds(5), Interval));
    }

    [Fact]
    public void Only_the_first_failure_since_a_success_warns_and_a_commit_clears_the_backoff()
    {
        var s = NewSchedule();

        Assert.True(s.Fail(T0));
        Assert.False(s.Fail(T0.AddSeconds(2)));
        Assert.False(s.Fail(T0.AddSeconds(6)));
        Assert.Equal(3, s.Failures);
        Assert.Equal(TimeSpan.FromSeconds(8), s.Wait);

        Assert.Equal(3, s.Succeed());

        Assert.False(s.IsBackingOff);
        Assert.Equal(TimeSpan.Zero, s.Wait);
        Assert.True(s.IsDue(T0.AddSeconds(7), Interval));

        // A new run of failures warns again.
        Assert.True(s.Fail(T0.AddSeconds(8)));
    }
}
