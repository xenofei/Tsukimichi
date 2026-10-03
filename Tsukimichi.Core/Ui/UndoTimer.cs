namespace Tsukimichi.Core.Ui;

/// <summary>
/// The Undo toast's clock (feature plan v6 S2): it shows for <see cref="SafetyRules.UndoSeconds"/> after a change,
/// the time stands still while the pointer rests on it, and a newer change starts it over. Fed with a steady clock
/// (ImGui's time) once per frame through <see cref="Tick"/>; kept free of ImGui so the timing is tested.
/// </summary>
public sealed class UndoTimer
{
    private double remaining;
    private double lastTick;

    /// <summary>Whether the toast is up.</summary>
    public bool Showing { get; private set; }

    /// <summary>When the toast last started, on the caller's clock: the fade-in reads its age from this.</summary>
    public double StartedAt { get; private set; }

    /// <summary>Seconds left before the toast goes, not counting pauses still to come.</summary>
    public double Remaining => Showing ? remaining : 0.0;

    /// <summary>Starts (or starts over) the toast at <paramref name="now"/> for <paramref name="seconds"/>.</summary>
    public void Start(double now, double seconds = SafetyRules.UndoSeconds)
    {
        remaining = double.IsFinite(seconds) && seconds > 0.0 ? seconds : SafetyRules.UndoSeconds;
        StartedAt = now;
        lastTick = now;
        Showing = true;
    }

    /// <summary>
    /// Advances the clock to <paramref name="now"/>; while <paramref name="paused"/> (the pointer is on the toast) no
    /// time is used. Returns whether the toast is still up.
    /// </summary>
    public bool Tick(double now, bool paused)
    {
        if (!Showing)
        {
            return false;
        }

        var step = now - lastTick;
        lastTick = now;
        if (!paused && double.IsFinite(step) && step > 0.0)
        {
            remaining -= step;
        }

        if (remaining <= 0.0)
        {
            Stop();
        }

        return Showing;
    }

    /// <summary>Takes the toast down (Undo was used, or what it would undo is gone).</summary>
    public void Stop()
    {
        Showing = false;
        remaining = 0.0;
    }
}
