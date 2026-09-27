using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class PollBackoffTests
{
    [Fact]
    public void Healthy_backoff_uses_the_normal_interval()
    {
        var b = new PollBackoff(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));

        Assert.False(b.IsActive);
        Assert.Equal(TimeSpan.Zero, b.Current);
        Assert.Equal(TimeSpan.FromSeconds(1), b.IntervalOr(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Failures_double_from_the_initial_interval_and_cap_at_max()
    {
        var b = new PollBackoff(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
        var seen = new List<double>();

        for (var i = 0; i < 6; i++)
        {
            b.RecordFailure();
            seen.Add(b.Current.TotalSeconds);
        }

        Assert.Equal([2, 4, 8, 16, 30, 30], seen);
        Assert.Equal(6, b.Failures);
        Assert.True(b.IsActive);
        Assert.Equal(TimeSpan.FromSeconds(30), b.IntervalOr(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Reset_returns_to_healthy()
    {
        var b = new PollBackoff(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
        b.RecordFailure();
        b.RecordFailure();

        b.Reset();

        Assert.False(b.IsActive);
        Assert.Equal(0, b.Failures);
        Assert.Equal(TimeSpan.Zero, b.Current);

        b.RecordFailure();
        Assert.Equal(TimeSpan.FromSeconds(2), b.Current);
    }

    [Fact]
    public void Initial_equal_to_max_stays_at_max()
    {
        var b = new PollBackoff(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        b.RecordFailure();
        b.RecordFailure();

        Assert.Equal(TimeSpan.FromSeconds(30), b.Current);
    }

    [Fact]
    public void Invalid_intervals_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PollBackoff(TimeSpan.Zero, TimeSpan.FromSeconds(30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PollBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
    }
}
