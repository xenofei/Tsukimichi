namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Overlay &amp; routes, the 1.13.0 hiding rows (feature plan v6 M3, decision 12): hide the overlay in combat,
/// while talking to NPCs and in group pose, each off by default. Drawn after the overlay's opacity in
/// <c>ConfigWindow.Overlay.cs</c>, in the rebuilt row design (U7).
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The overlay's hiding options, as sub-settings dimmed while the overlay is off (<paramref name="overlayOn"/>).</summary>
    private void DrawTodoHiding(bool overlayOn, string offReason)
    {
        DrawTodoHideRow(Strings.TodoConfigHideInCombat, Strings.TodoConfigHideInCombatHint, "overlay hide combat fight battle", overlayOn, offReason, static c => c.TodoHideInCombat, static (c, v) => c.TodoHideInCombat = v);
        DrawTodoHideRow(Strings.TodoConfigHideTalking, Strings.TodoConfigHideTalkingHint, "overlay hide talk npc dialogue cutscene event shop", overlayOn, offReason, static c => c.TodoHideTalking, static (c, v) => c.TodoHideTalking = v);
        DrawTodoHideRow(Strings.TodoConfigHideGroupPose, Strings.TodoConfigHideGroupPoseHint, "overlay hide group pose gpose screenshot", overlayOn, offReason, static c => c.TodoHideGroupPose, static (c, v) => c.TodoHideGroupPose = v);
    }

    private void DrawTodoHideRow(string label, string hint, string keywords, bool overlayOn, string offReason, System.Func<Config.Configuration, bool> get, System.Action<Config.Configuration, bool> set)
    {
        var value = get(settings);
        if (Toggle(label, hint, ref value, keywords, overlayOn, sub: true, reason: offReason))
        {
            set(settings, value);
            Save();
        }
    }
}
