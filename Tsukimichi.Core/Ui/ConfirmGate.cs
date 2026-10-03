namespace Tsukimichi.Core.Ui;

/// <summary>
/// The guard on a <see cref="SafetyTier.Hold"/> button (bulk or irreversible changes), fed once per frame with what
/// the UI sees: whether the chord (Ctrl or Shift) is down, whether the button is being pressed, and the frame time.
/// Two ways through: the chord plus a press confirms at once; without the chord the press must last
/// <see cref="HoldSeconds"/> (600 ms by default, adjustable in Settings through <see cref="SetHoldSeconds"/>), and
/// releasing earlier cancels and resets (<see cref="Abandoned"/> says so for that one update). After a confirmation
/// the gate stays latched until the press ends, so one long press fires once. <see cref="Progress"/> drives the hold
/// arc (or the countdown text), <see cref="Confirmed"/> is true for the single update that fired. In two-click mode
/// (hand strain) the button feeds its clicks to <see cref="ClickTwice"/> instead, which follows the
/// <see cref="ClickGuard"/> rules with the chord still confirming at once. Only time held counts: the frame the press
/// begins adds nothing (its frame time passed before the press), and no frame adds more than
/// <see cref="MaxFrameSeconds"/>, so a hitch or a low frame rate can make the hold slower but never shorter.
/// </summary>
public sealed class ConfirmGate
{
    public const float DefaultHoldSeconds = 0.6f;

    /// <summary>The most one frame adds to a hold (a 20 fps frame): a long frame cannot finish the hold early.</summary>
    public const float MaxFrameSeconds = 0.05f;

    private readonly ClickGuard clicks = new();
    private bool latched;
    private bool wasPressed;

    public ConfirmGate(float holdSeconds = DefaultHoldSeconds)
    {
        if (!float.IsFinite(holdSeconds) || holdSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "The hold must be a positive number of seconds.");
        }

        HoldSeconds = holdSeconds;
    }

    /// <summary>How long the button must be held without the chord.</summary>
    public float HoldSeconds { get; private set; }

    /// <summary>Seconds the current press has lasted; 0 when idle.</summary>
    public float Elapsed { get; private set; }

    /// <summary>Fraction of the hold done, 0..1; 1 once fired and while the press continues.</summary>
    public float Progress => latched ? 1f : Math.Clamp(Elapsed / HoldSeconds, 0f, 1f);

    /// <summary>Seconds still to hold; 0 once fired.</summary>
    public float Remaining => latched ? 0f : Math.Max(0f, HoldSeconds - Elapsed);

    /// <summary>True on the update that confirmed, false on every other.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>True on the update where a press ended before the hold was done (the hint may say to hold longer).</summary>
    public bool Abandoned { get; private set; }

    /// <summary>True while a press is accumulating towards the hold (not idle, not yet fired).</summary>
    public bool Holding => !latched && Elapsed > 0f;

    /// <summary>The hold in whole tenths of a second (6 for the default 600 ms): the longest countdown label.</summary>
    public int HoldTenths => Math.Max(1, (int)MathF.Round(HoldSeconds * 10f));

    /// <summary>
    /// Tenths of a second still to hold, rounded up and never below one, while <see cref="Holding"/>; 0 otherwise. The
    /// countdown label shown in place of the hold arc under Reduce motion ("Hold… (0.4 s)").
    /// </summary>
    public int RemainingTenths => Holding ? Math.Clamp((int)MathF.Ceiling(Remaining * 10f - 0.0001f), 1, HoldTenths) : 0;

    /// <summary>
    /// Advances the gate one frame. Returns true on the frame the action is confirmed.
    /// </summary>
    /// <param name="chordHeld">Ctrl or Shift is down.</param>
    /// <param name="pressed">The button is active (mouse or nav key held on it).</param>
    /// <param name="deltaSeconds">
    /// Frame time; negative or non-finite values count as zero, longer ones as <see cref="MaxFrameSeconds"/>, and the
    /// frame the press begins counts as zero.
    /// </param>
    public bool Update(bool chordHeld, bool pressed, float deltaSeconds)
    {
        Confirmed = false;
        Abandoned = false;
        if (!pressed)
        {
            // Release: a finished press unlatches, an unfinished one is cancelled. Either way the next press starts from zero.
            Abandoned = wasPressed && !latched;
            wasPressed = false;
            Elapsed = 0f;
            latched = false;
            return false;
        }

        var began = !wasPressed;
        wasPressed = true;
        if (latched)
        {
            return false;
        }

        if (chordHeld)
        {
            Fire();
            return true;
        }

        // The frame the press begins: its frame time passed before the button went down, so none of it was held.
        if (!began && float.IsFinite(deltaSeconds) && deltaSeconds > 0f)
        {
            Elapsed += MathF.Min(deltaSeconds, MaxFrameSeconds);
        }

        if (Elapsed >= HoldSeconds)
        {
            Fire();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Changes the hold length (Settings › Keyboard › Safety); a press in progress starts over. Values outside the
    /// range Settings offers are clamped (<see cref="SafetyRules.ClampHoldSeconds"/>).
    /// </summary>
    public void SetHoldSeconds(float seconds)
    {
        var clamped = SafetyRules.ClampHoldSeconds(seconds);
        if (clamped == HoldSeconds)
        {
            return;
        }

        HoldSeconds = clamped;
        Cancel();
    }

    /// <summary>
    /// Two-click mode: a click on the button. The chord confirms at once; otherwise the first click arms the gate and a
    /// second one confirms (<see cref="ClickGuard"/>'s timing). Returns true when the action should happen now.
    /// </summary>
    public bool ClickTwice(bool chordHeld, double now)
    {
        Confirmed = clicks.Click(chordHeld, twoClick: true, now);
        return Confirmed;
    }

    /// <summary>Two-click mode: the first click landed and the second is awaited.</summary>
    public bool AwaitingSecond(double now) => clicks.AwaitingSecond(now);

    /// <summary>Drops any press in progress, and any first click (the popup closed, Escape was pressed), without confirming.</summary>
    public void Cancel()
    {
        Elapsed = 0f;
        latched = false;
        wasPressed = false;
        Confirmed = false;
        Abandoned = false;
        clicks.Cancel();
    }

    private void Fire()
    {
        Elapsed = HoldSeconds;
        latched = true;
        Confirmed = true;
    }
}
