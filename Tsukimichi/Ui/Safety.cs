using System;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The safety table as the UI reads it each frame (feature plan v6 S1/S2): the user's hold length and two-click choice
/// from <see cref="Configuration"/>, whether a safety key (Ctrl or Shift) is down, and the one helper that words every
/// guarded tooltip from the action's tier in <see cref="SafetyRules"/>. <see cref="Update"/> runs with
/// <see cref="UiMetrics.Update"/> before the windows draw.
/// </summary>
public static class Safety
{
    /// <summary>The user's safety settings as of the last <see cref="Update"/>.</summary>
    public static SafetySettings Settings { get; private set; } = SafetySettings.Default;

    /// <summary>Reads the safety settings; call once per frame before drawing.</summary>
    public static void Update(Configuration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = new SafetySettings(settings.SafetyHoldSecondsClamped, settings.SafetyTwoClick);
    }

    /// <summary>Ctrl or Shift is down: the key that arms a guarded action.</summary>
    public static bool KeyHeld
    {
        get
        {
            var io = ImGui.GetIO();
            return io.KeyCtrl || io.KeyShift;
        }
    }

    /// <summary>
    /// The tooltip of a guarded item: <paramref name="body"/> (what it does), then one quieter line saying how to
    /// confirm it, worded from the action's tier and the two-click choice.
    /// </summary>
    public static void Tooltip(string body, GuardedAction action) => UiMetrics.Tooltip(body, Suffix(action));

    /// <summary>How to confirm the action, and whether it can be undone.</summary>
    public static string Suffix(GuardedAction action)
    {
        var twoClick = Settings.TwoClick;
        return SafetyRules.TierOf(action) switch
        {
            SafetyTier.Armed => twoClick ? Strings.SafetyArmedTwoClickSuffix : Strings.SafetyArmedSuffix,
            SafetyTier.Hold when SafetyRules.OffersUndo(action) => twoClick ? Strings.SafetyHoldTwoClickUndoSuffix : Strings.SafetyHoldUndoSuffix,
            SafetyTier.Hold => twoClick ? Strings.SafetyHoldTwoClickSuffix : Strings.SafetyHoldSuffix,
            _ => Strings.SafetyNoneSuffix,
        };
    }
}
