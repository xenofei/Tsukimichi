using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The unload's shared deadline: the windows and hooks torn down first (the journal index's own short wait among them)
/// must not eat the last saves' time, and the waits after the save writer share only what it left.
/// </summary>
public sealed class WaitBudgetTests
{
    private static readonly TimeSpan Five = TimeSpan.FromSeconds(5);

    private TimeSpan now = TimeSpan.FromSeconds(100);

    private WaitBudget Budget() => new(Five, () => now);

    [Fact]
    public void Time_before_the_first_wait_does_not_count()
    {
        var budget = Budget();
        // The UI steps: 3 s, the journal index's 2 s wait included.
        now += TimeSpan.FromSeconds(3);

        Assert.Equal(Five, budget.Remaining());
    }

    [Fact]
    public void Later_waits_share_what_the_first_left()
    {
        var budget = Budget();
        var writer = budget.Remaining();
        now += TimeSpan.FromSeconds(4);
        var multibox = budget.Remaining();
        now += TimeSpan.FromSeconds(0.5);
        var catalog = budget.Remaining();

        Assert.Equal(Five, writer);
        Assert.Equal(TimeSpan.FromSeconds(1), multibox);
        Assert.Equal(TimeSpan.FromSeconds(0.5), catalog);
    }

    [Fact]
    public void A_spent_budget_leaves_zero_never_less()
    {
        var budget = Budget();
        budget.Remaining();
        now += TimeSpan.FromSeconds(9);

        Assert.Equal(TimeSpan.Zero, budget.Remaining());
    }

    [Fact]
    public void A_negative_total_counts_as_zero()
    {
        var budget = new WaitBudget(TimeSpan.FromSeconds(-1), () => now);

        Assert.Equal(TimeSpan.Zero, budget.Total);
        Assert.Equal(TimeSpan.Zero, budget.Remaining());
    }

    [Fact]
    public void The_default_clock_runs()
    {
        var budget = new WaitBudget(Five);

        var first = budget.Remaining();
        Thread.Sleep(20);

        Assert.Equal(Five, first);
        Assert.True(budget.Remaining() < Five);
    }
}
