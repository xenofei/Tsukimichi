namespace Tsukimichi.Core.Ui;

/// <summary>
/// The guard on an <see cref="SafetyTier.Armed"/> button, menu item or checkbox (feature plan v6 S1): fed once per
/// click with whether Ctrl or Shift was down and the time. A click with the key down acts at once; a plain click (or
/// Enter on the focused button) does nothing but leave the "hold Shift or Ctrl" hint showing for
/// <see cref="SafetyRules.RefusedHintSeconds"/>. In two-click mode (hand strain) a plain click arms the guard instead,
/// and a second plain click between <see cref="SafetyRules.SecondClickMinGapSeconds"/> and
/// <see cref="SafetyRules.SecondClickWindowSeconds"/> later acts; a quicker one is a double-click and does nothing,
/// a later one arms again. There is no press-and-hold path: these act only while a key is held, or on two clicks.
/// One guard may serve a list of items (a Restore per row): each click names its target, and a second click on another
/// target is a first click there.
/// </summary>
public sealed class ClickGuard
{
    private double armedAt = double.NegativeInfinity;
    private double refusedAt = double.NegativeInfinity;
    private ulong armedTarget;
    private ulong refusedTarget;

    /// <summary>
    /// A click on the guarded item. Returns true when the action should happen now.
    /// </summary>
    /// <param name="modifierHeld">Ctrl or Shift is down.</param>
    /// <param name="twoClick">The user chose two clicks instead of a held key.</param>
    /// <param name="now">Seconds on any steady clock (ImGui's time).</param>
    /// <param name="target">Which item of the list was clicked (a row id); 0 for a guard on one item.</param>
    public bool Click(bool modifierHeld, bool twoClick, double now, ulong target = 0)
    {
        if (modifierHeld)
        {
            Cancel();
            return true;
        }

        if (!twoClick)
        {
            refusedAt = now;
            refusedTarget = target;
            return false;
        }

        var since = now - armedAt;
        if (target != armedTarget || since > SafetyRules.SecondClickWindowSeconds || since < 0.0)
        {
            armedAt = now;
            armedTarget = target;
            return false;
        }

        if (since < SafetyRules.SecondClickMinGapSeconds)
        {
            return false;
        }

        Cancel();
        return true;
    }

    /// <summary>Two-click mode: the first click on <paramref name="target"/> landed and the second is awaited ("Click again").</summary>
    public bool AwaitingSecond(double now, ulong target = 0)
    {
        var since = now - armedAt;
        return target == armedTarget && since >= 0.0 && since <= SafetyRules.SecondClickWindowSeconds;
    }

    /// <summary>A plain click on <paramref name="target"/> was refused a moment ago: the hint says which key arms the item.</summary>
    public bool ShowRefusedHint(double now, ulong target = 0)
    {
        var since = now - refusedAt;
        return target == refusedTarget && since >= 0.0 && since <= SafetyRules.RefusedHintSeconds;
    }

    /// <summary>Forgets a first click and the hint (the popup closed, the item went away).</summary>
    public void Cancel()
    {
        armedAt = double.NegativeInfinity;
        refusedAt = double.NegativeInfinity;
        armedTarget = 0;
        refusedTarget = 0;
    }
}
