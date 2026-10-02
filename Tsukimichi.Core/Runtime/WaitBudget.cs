using System.Diagnostics;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// One deadline shared by several bounded waits in a row (the plugin's unload: the last saves, the multibox loop, the
/// catalog builds), so together they never take longer than <see cref="Total"/>. The clock starts at the first
/// <see cref="Remaining"/> call, not at construction: whatever runs before the first wait (the windows and hooks torn
/// down first) does not eat into it, and the first waiter (the save writer) gets the whole budget.
/// </summary>
public sealed class WaitBudget
{
    private readonly Func<TimeSpan> clock;
    private TimeSpan? startedAt;

    /// <param name="total">The longest all the waits take together; a negative one counts as zero.</param>
    /// <param name="clock">Elapsed time from any fixed origin; a <see cref="Stopwatch"/> started here when null.</param>
    public WaitBudget(TimeSpan total, Func<TimeSpan>? clock = null)
    {
        Total = total < TimeSpan.Zero ? TimeSpan.Zero : total;
        if (clock is null)
        {
            var stopwatch = Stopwatch.StartNew();
            clock = () => stopwatch.Elapsed;
        }

        this.clock = clock;
    }

    public TimeSpan Total { get; }

    /// <summary>What is left of <see cref="Total"/> for the next wait; never negative. The first call starts the clock.</summary>
    public TimeSpan Remaining()
    {
        var now = clock();
        startedAt ??= now;
        var left = Total - (now - startedAt.Value);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }
}
