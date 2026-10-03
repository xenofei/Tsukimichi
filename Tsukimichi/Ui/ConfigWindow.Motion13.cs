using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Todo overlay, the 1.13.0 motion rows (feature plan v6 M3, decision 12): hide the overlay in combat, while
/// talking to NPCs and in group pose, each off by default. Kept in its own file and called from one line in
/// <see cref="DrawTodoOverlay"/>, so the Settings rebuild (U7) can move the rows wherever its pages put the overlay.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The overlay's hiding options, as sub-settings greyed while the overlay is off (<paramref name="overlayOn"/>).</summary>
    private void DrawTodoHiding(bool overlayOn)
    {
        DrawTodoHideRow(Strings.TodoConfigHideInCombat, Strings.TodoConfigHideInCombatHint, "overlay hide combat fight battle", overlayOn, static c => c.TodoHideInCombat, static (c, v) => c.TodoHideInCombat = v);
        DrawTodoHideRow(Strings.TodoConfigHideTalking, Strings.TodoConfigHideTalkingHint, "overlay hide talk npc dialogue cutscene event shop", overlayOn, static c => c.TodoHideTalking, static (c, v) => c.TodoHideTalking = v);
        DrawTodoHideRow(Strings.TodoConfigHideGroupPose, Strings.TodoConfigHideGroupPoseHint, "overlay hide group pose gpose screenshot", overlayOn, static c => c.TodoHideGroupPose, static (c, v) => c.TodoHideGroupPose = v);
    }

    private void DrawTodoHideRow(string label, string hint, string keywords, bool overlayOn, System.Func<Config.Configuration, bool> get, System.Action<Config.Configuration, bool> set)
    {
        if (!Row(label, hint, keywords))
        {
            return;
        }

        using var sub = SubSetting(overlayOn);
        var value = get(settings);
        if (ImGui.Checkbox(label, ref value))
        {
            set(settings, value);
            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(hint);
        }
    }
}
