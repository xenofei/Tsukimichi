namespace Tsukimichi.Core.Ui;

/// <summary>
/// The guard on a destructive button, fed once per frame with what the UI sees: whether the chord key (Shift) is
/// down, whether the button is being pressed, and the frame time. Two ways through: the chord plus a press confirms
/// at once; without the chord the press must last <see cref="HoldSeconds"/> (600 ms by default), and releasing
/// earlier cancels and resets. After a confirmation the gate stays latched until the press ends, so one long press
/// fires once. <see cref="Progress"/> drives the hold arc (or the countdown text), <see cref="Confirmed"/> is true
/// for the single update that fired.
/// </summary>
public sealed class ConfirmGate
{
    public const float DefaultHoldSeconds = 0.6f;

    private bool latched;

    public ConfirmGate(float holdSeconds = DefaultHoldSeconds)
    {
        if (!float.IsFinite(holdSeconds) || holdSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "The hold must be a positive number of seconds.");
        }

        HoldSeconds = holdSeconds;
    }

    /// <summary>How long the button must be held without the chord.</summary>
    public float HoldSeconds { get; }

    /// <summary>Seconds the current press has lasted; 0 when idle.</summary>
    public float Elapsed { get; private set; }

    /// <summary>Fraction of the hold done, 0..1; 1 once fired and while the press continues.</summary>
    public float Progress => latched ? 1f : Math.Clamp(Elapsed / HoldSeconds, 0f, 1f);

    /// <summary>Seconds still to hold; 0 once fired.</summary>
    public float Remaining => latched ? 0f : Math.Max(0f, HoldSeconds - Elapsed);

    /// <summary>True on the update that confirmed, false on every other.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>True while a press is accumulating towards the hold (not idle, not yet fired).</summary>
    public bool Holding => !latched && Elapsed > 0f;

    /// <summary>
    /// Advances the gate one frame. Returns true on the frame the action is confirmed.
    /// </summary>
    /// <param name="chordHeld">The Shift key is down.</param>
    /// <param name="pressed">The button is active (mouse or nav key held on it).</param>
    /// <param name="deltaSeconds">Frame time; negative or non-finite values count as zero.</param>
    public bool Update(bool chordHeld, bool pressed, float deltaSeconds)
    {
        Confirmed = false;
        if (!pressed)
        {
            // Release: a finished press unlatches, an unfinished one is cancelled. Either way the next press starts from zero.
            Elapsed = 0f;
            latched = false;
            return false;
        }

        if (latched)
        {
            return false;
        }

        if (chordHeld)
        {
            Fire();
            return true;
        }

        if (float.IsFinite(deltaSeconds) && deltaSeconds > 0f)
        {
            Elapsed += deltaSeconds;
        }

        if (Elapsed >= HoldSeconds)
        {
            Fire();
            return true;
        }

        return false;
    }

    /// <summary>Drops any press in progress (the popup closed, Escape was pressed) without confirming.</summary>
    public void Cancel()
    {
        Elapsed = 0f;
        latched = false;
        Confirmed = false;
    }

    private void Fire()
    {
        Elapsed = HoldSeconds;
        latched = true;
        Confirmed = true;
    }
}
