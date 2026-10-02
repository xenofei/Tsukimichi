using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// "Run with AutoDuty" (<see cref="AutoDutyRunSteps"/>): the temporary settings pushed before Run are popped on every
/// path where the run did not start, so AutoDuty never keeps Tsukimichi's run mode, queue and loop count.
/// </summary>
public class AutoDutyRunStepsTests
{
    private sealed class Fake
    {
        public bool AcceptPush { get; init; } = true;

        public bool ThrowOnPush { get; init; }

        public bool ThrowOnRun { get; init; }

        public bool ThrowOnStopped { get; init; }

        public bool StoppedAfterRun { get; init; }

        public List<string> Calls { get; } = [];

        public List<Exception> Failures { get; } = [];

        public AutoDutyStart Run() => AutoDutyRunSteps.Run(
            () =>
            {
                Calls.Add("push");
                return ThrowOnPush ? throw new InvalidOperationException("push") : AcceptPush;
            },
            () =>
            {
                Calls.Add("run");
                if (ThrowOnRun)
                {
                    throw new InvalidOperationException("run");
                }
            },
            () =>
            {
                Calls.Add("stopped?");
                return ThrowOnStopped ? throw new InvalidOperationException("stopped") : StoppedAfterRun;
            },
            () => Calls.Add("pop"),
            Failures.Add);
    }

    [Fact]
    public void A_run_that_starts_leaves_the_settings_to_AutoDuty()
    {
        var fake = new Fake { StoppedAfterRun = false };

        Assert.Equal(AutoDutyStart.Started, fake.Run());
        Assert.Equal(["push", "run", "stopped?"], fake.Calls);
        Assert.Empty(fake.Failures);
    }

    [Fact]
    public void A_refused_mode_runs_nothing_and_pops_nothing()
    {
        var fake = new Fake { AcceptPush = false };

        Assert.Equal(AutoDutyStart.ModeRefused, fake.Run());
        Assert.Equal(["push"], fake.Calls);
    }

    [Fact]
    public void A_failed_push_runs_nothing_and_pops_nothing()
    {
        var fake = new Fake { ThrowOnPush = true };

        Assert.Equal(AutoDutyStart.Unavailable, fake.Run());
        Assert.Equal(["push"], fake.Calls);
        Assert.Single(fake.Failures);
    }

    [Fact]
    public void A_run_that_stays_stopped_pops_the_settings()
    {
        var fake = new Fake { StoppedAfterRun = true };

        Assert.Equal(AutoDutyStart.NotStarted, fake.Run());
        Assert.Equal(["push", "run", "stopped?", "pop"], fake.Calls);
    }

    [Fact]
    public void A_run_that_throws_pops_the_settings()
    {
        var fake = new Fake { ThrowOnRun = true, StoppedAfterRun = true };

        Assert.Equal(AutoDutyStart.Unavailable, fake.Run());
        Assert.Equal(["push", "run", "stopped?", "pop"], fake.Calls);
        Assert.Single(fake.Failures);
    }

    [Fact]
    public void A_run_that_throws_but_started_anyway_is_left_running()
    {
        var fake = new Fake { ThrowOnRun = true, StoppedAfterRun = false };

        Assert.Equal(AutoDutyStart.Started, fake.Run());
        Assert.DoesNotContain("pop", fake.Calls);
    }

    [Fact]
    public void A_state_that_cannot_be_read_counts_as_stopped_and_pops()
    {
        var fake = new Fake { ThrowOnStopped = true };

        Assert.Equal(AutoDutyStart.NotStarted, fake.Run());
        Assert.Equal("pop", fake.Calls[^1]);
        Assert.Single(fake.Failures);
    }

    [Fact]
    public void A_failing_pop_does_not_escape()
    {
        var failures = new List<Exception>();
        var outcome = AutoDutyRunSteps.Run(
            static () => true,
            static () => throw new InvalidOperationException("run"),
            static () => true,
            static () => throw new InvalidOperationException("pop"),
            failures.Add);

        Assert.Equal(AutoDutyStart.Unavailable, outcome);
        Assert.Equal(2, failures.Count);
    }
}
