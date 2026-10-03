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
/// target is a first click there. A first click made in a context menu lasts only as long as the menu: the menu item
/// reports each frame it is drawn (<see cref="MenuShown"/>), and <see cref="KeepMenuArm"/>, once a frame, forgets the
/// click once a frame passed without it. A guard shared by a menu item and a button keeps their first clicks apart, so
/// a click in one never completes a click in the other.
/// </summary>
public sealed class ClickGuard
{
    private double armedAt = double.NegativeInfinity;
    private double refusedAt = double.NegativeInfinity;
    private ulong armedTarget;
    private ulong refusedTarget;
    private bool armedInMenu;
    private long menuFrame = long.MinValue;

    /// <summary>
    /// A click on the guarded item. Returns true when the action should happen now.
    /// </summary>
    /// <param name="modifierHeld">Ctrl or Shift is down.</param>
    /// <param name="twoClick">The user chose two clicks instead of a held key.</param>
    /// <param name="now">Seconds on any steady clock (ImGui's time).</param>
    /// <param name="target">Which item of the list was clicked (a row id); 0 for a guard on one item.</param>
    /// <param name="inMenu">The item is in a context menu (its first click ends with the menu, <see cref="KeepMenuArm"/>).</param>
    public bool Click(bool modifierHeld, bool twoClick, double now, ulong target = 0, bool inMenu = false)
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
        if (target != armedTarget || inMenu != armedInMenu || since > SafetyRules.SecondClickWindowSeconds || since < 0.0)
        {
            armedAt = now;
            armedTarget = target;
            armedInMenu = inMenu;
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
    public bool AwaitingSecond(double now, ulong target = 0, bool inMenu = false)
    {
        var since = now - armedAt;
        return target == armedTarget && inMenu == armedInMenu && since >= 0.0 && since <= SafetyRules.SecondClickWindowSeconds;
    }

    /// <summary>
    /// The menu item for <paramref name="target"/> is drawn on <paramref name="frame"/>, after its <see cref="Click"/>:
    /// its menu is open. Returns whether the guard holds a first click made in that menu, which then needs
    /// <see cref="KeepMenuArm"/> once a frame.
    /// </summary>
    public bool MenuShown(long frame, ulong target = 0)
    {
        if (!armedInMenu || target != armedTarget || double.IsNegativeInfinity(armedAt))
        {
            return false;
        }

        menuFrame = frame;
        return true;
    }

    /// <summary>
    /// Once a frame, before anything draws (<paramref name="frame"/> is the new frame's number): a first click made in
    /// a context menu whose item was not drawn on the frame before is forgotten (<see cref="Cancel"/>), since its menu
    /// closed. Returns whether a first click made in a menu is still held.
    /// </summary>
    public bool KeepMenuArm(long frame)
    {
        if (!armedInMenu || double.IsNegativeInfinity(armedAt))
        {
            return false;
        }

        if (menuFrame >= frame - 1)
        {
            return true;
        }

        Cancel();
        return false;
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
        armedInMenu = false;
        menuFrame = long.MinValue;
    }
}
