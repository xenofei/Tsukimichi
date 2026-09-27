namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Exponential retry interval for a poller that hit an exception: the first failure waits <see cref="Initial"/>,
/// each further failure doubles the wait up to <see cref="Max"/>, and a success clears it.
/// </summary>
public sealed class PollBackoff
{
    public PollBackoff(TimeSpan initial, TimeSpan max)
    {
        if (initial <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initial), initial, "Initial interval must be positive.");
        }

        if (max < initial)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Max interval must be at least the initial interval.");
        }

        Initial = initial;
        Max = max;
    }

    public TimeSpan Initial { get; }

    public TimeSpan Max { get; }

    /// <summary>Consecutive failures since the last success.</summary>
    public int Failures { get; private set; }

    /// <summary>Wait before the next attempt; zero while healthy.</summary>
    public TimeSpan Current { get; private set; }

    public bool IsActive => Failures > 0;

    public void RecordFailure()
    {
        Failures++;
        if (Failures == 1)
        {
            Current = Initial;
            return;
        }

        var doubled = Current.Ticks >= Max.Ticks / 2 ? Max : TimeSpan.FromTicks(Current.Ticks * 2);
        Current = doubled > Max ? Max : doubled;
    }

    public void Reset()
    {
        Failures = 0;
        Current = TimeSpan.Zero;
    }

    /// <summary>The interval to use now: the backoff while active, otherwise <paramref name="normal"/>.</summary>
    public TimeSpan IntervalOr(TimeSpan normal) => IsActive ? Current : normal;
}
