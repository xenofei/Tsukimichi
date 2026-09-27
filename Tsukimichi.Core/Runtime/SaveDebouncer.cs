namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Decides when a changed snapshot goes to disk: at most once per <see cref="MinInterval"/> while changes keep
/// arriving, plus whenever the caller flushes (logout, dispose). A failed attempt counts against the interval too, so
/// a broken disk is retried at the save cadence rather than every poll. Holds no data itself; the caller keeps the snapshot.
/// </summary>
public sealed class SaveDebouncer
{
    public SaveDebouncer(TimeSpan minInterval)
    {
        if (minInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minInterval), minInterval, "Interval cannot be negative.");
        }

        MinInterval = minInterval;
    }

    public TimeSpan MinInterval { get; }

    /// <summary>True when something changed since the last successful save.</summary>
    public bool Pending { get; private set; }

    /// <summary>When the last successful save happened; null before the first.</summary>
    public DateTime? LastSavedUtc { get; private set; }

    /// <summary>When the last save was attempted, successful or not; null before the first.</summary>
    public DateTime? LastAttemptUtc { get; private set; }

    /// <summary>Consecutive failed attempts since the last successful save.</summary>
    public int Failures { get; private set; }

    public void MarkDirty() => Pending = true;

    /// <summary>Whether a save is due now: something is pending and the interval since the last attempt has elapsed.</summary>
    public bool ShouldSave(DateTime nowUtc) =>
        Pending && (LastAttemptUtc is not { } last || nowUtc - last >= MinInterval);

    public void MarkSaved(DateTime nowUtc)
    {
        Pending = false;
        LastSavedUtc = nowUtc;
        LastAttemptUtc = nowUtc;
        Failures = 0;
    }

    /// <summary>Records a failed attempt: the change stays pending and the next attempt waits a full interval.</summary>
    public void MarkFailed(DateTime nowUtc)
    {
        LastAttemptUtc = nowUtc;
        Failures++;
    }
}
