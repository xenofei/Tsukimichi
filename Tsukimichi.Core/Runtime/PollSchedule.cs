namespace Tsukimichi.Core.Runtime;

/// <summary>
/// When the poller's next attempt is due, and the one backoff and warning latch its two failure sources share: a
/// game read that throws on the framework thread, and a first pass that faults on the worker. A poll that only
/// started a first pass has not succeeded yet, so the poller does not call <see cref="Succeed"/> for it; the backoff
/// stands until the pass commits or faults. Not thread-safe: the framework thread owns it.
/// </summary>
public sealed class PollSchedule
{
    private readonly PollBackoff backoff;
    private bool warned;

    public PollSchedule(TimeSpan initialBackoff, TimeSpan maxBackoff)
    {
        backoff = new PollBackoff(initialBackoff, maxBackoff);
    }

    /// <summary>When the last attempt began, or the last failure was recorded; the next attempt is measured from here.</summary>
    public DateTime LastAttemptUtc { get; private set; } = DateTime.MinValue;

    /// <summary>Consecutive failures since the last success.</summary>
    public int Failures => backoff.Failures;

    /// <summary>Wait before the next attempt while backing off; zero while healthy.</summary>
    public TimeSpan Wait => backoff.Current;

    public bool IsBackingOff => backoff.IsActive;

    /// <summary>Whether an attempt is due: <paramref name="interval"/> after the last one while healthy, the backoff while not.</summary>
    public bool IsDue(DateTime nowUtc, TimeSpan interval) => nowUtc - LastAttemptUtc >= backoff.IntervalOr(interval);

    /// <summary>An attempt begins. It leaves the backoff as it is: only <see cref="Succeed"/> clears it.</summary>
    public void Attempt(DateTime nowUtc) => LastAttemptUtc = nowUtc;

    /// <summary>
    /// A failed attempt. The next one is due a backoff step after <paramref name="nowUtc"/>, not after the attempt
    /// began: a first pass faults on the worker some time after its capture. Returns true for the first failure since
    /// the last success, the one worth a warning.
    /// </summary>
    public bool Fail(DateTime nowUtc)
    {
        LastAttemptUtc = nowUtc;
        backoff.RecordFailure();
        if (warned)
        {
            return false;
        }

        warned = true;
        return true;
    }

    /// <summary>A committed attempt. Returns the failures it recovered from, zero when the schedule was healthy already.</summary>
    public int Succeed()
    {
        var recovered = backoff.Failures;
        backoff.Reset();
        warned = false;
        return recovered;
    }
}
