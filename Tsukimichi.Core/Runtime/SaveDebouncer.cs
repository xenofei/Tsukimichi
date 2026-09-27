namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Decides when a changed snapshot goes to disk: at most once per <see cref="MinInterval"/> while changes keep
/// arriving, plus whenever the caller flushes (logout, dispose). Holds no data itself; the caller keeps the snapshot.
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

    /// <summary>True when something changed since the last save.</summary>
    public bool Pending { get; private set; }

    /// <summary>When the last save happened; null before the first.</summary>
    public DateTime? LastSavedUtc { get; private set; }

    public void MarkDirty() => Pending = true;

    /// <summary>Whether a save is due now: something is pending and the interval since the last save has elapsed.</summary>
    public bool ShouldSave(DateTime nowUtc) =>
        Pending && (LastSavedUtc is not { } last || nowUtc - last >= MinInterval);

    public void MarkSaved(DateTime nowUtc)
    {
        Pending = false;
        LastSavedUtc = nowUtc;
    }
}
