namespace Tsukimichi.Core.Ui;

/// <summary>
/// When a setting that applies live (the window scale, the text size, a slider being dragged) is written to disk
/// (feature plan v6 U7): every change shows on the same frame, but the save waits until the value has been still for
/// <see cref="QuietSeconds"/> and nothing is held, so a drag writes the file once instead of once per frame. Times are
/// in seconds on any steady clock (ImGui's time). Without ImGui, so it can be tested.
/// </summary>
public sealed class SaveDebounce
{
    /// <summary>How long a value must stay still before it is saved.</summary>
    public const double QuietSeconds = 0.6;

    private double dueAt = double.NaN;

    /// <summary>True while a change waits to be saved.</summary>
    public bool Pending => !double.IsNaN(dueAt);

    /// <summary>Notes a change at <paramref name="now"/>: the save moves to <see cref="QuietSeconds"/> after it.</summary>
    public void Changed(double now) => dueAt = now + QuietSeconds;

    /// <summary>
    /// Whether to save now: a change is waiting, it has been still long enough (or the clock went back), and no control
    /// is held (<paramref name="holding"/>). Returns true once per wait; the caller saves.
    /// </summary>
    public bool Due(double now, bool holding)
    {
        if (!Pending || holding || (now < dueAt && now >= dueAt - QuietSeconds))
        {
            return false;
        }

        dueAt = double.NaN;
        return true;
    }

    /// <summary>Whether a change is waiting, clearing it: for a flush when the window closes.</summary>
    public bool Flush()
    {
        var pending = Pending;
        dueAt = double.NaN;
        return pending;
    }
}
