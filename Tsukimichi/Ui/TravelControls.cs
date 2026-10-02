using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// The travel controls every quest row shares (feature plan v5, 1.6.0): Teleport (always shown; without Lifestream
/// greyed with a tooltip naming it, decision 2), Walk to giver and Go to giver (when shown in Settings; Stop while the
/// character moves; greyed naming vnavmesh without it). <see cref="MenuItems"/> for a row's "…" or right-click menu,
/// <see cref="Buttons"/> for a row's small buttons. Tooltips are composed only on hover.
/// </summary>
internal static class TravelControls
{
    /// <summary>Teleport, then Walk to giver and Go to giver (one Stop while moving), as menu items.</summary>
    public static void MenuItems(GameLinks links, QuestRecord quest, string teleportLabel)
    {
        var teleport = links.CheckTeleport(quest);
        if (ImGui.MenuItem(teleportLabel, enabled: teleport.Ready))
        {
            links.TeleportToGiver(quest);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.TeleportTooltip(quest, teleport));
        }

        if (links.IsTraveling && (links.WalkShown || links.GoToShown))
        {
            if (ImGui.MenuItem(Strings.TravelStop))
            {
                links.StopTravel();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.TravelStopTooltip);
            }

            return;
        }

        if (links.WalkShown)
        {
            var walk = links.CheckWalk(quest);
            if (ImGui.MenuItem(GameLinks.WalkLabel(walk, Strings.TravelWalk), enabled: walk.Ready))
            {
                links.WalkToGiver(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.WalkTooltip(quest, walk));
            }
        }

        if (links.GoToShown)
        {
            var go = links.CheckGoTo(quest);
            if (ImGui.MenuItem(Strings.TravelGoTo, enabled: go.Ready))
            {
                links.GoToGiver(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.GoToTooltip(quest, go));
            }
        }
    }

    /// <summary>
    /// The width <see cref="Buttons"/> takes: Teleport, and Walk when shown (sized for the longer of Walk and Stop),
    /// each with its leading item spacing.
    /// </summary>
    public static float ButtonsWidth(GameLinks links, string teleportLabel)
    {
        var style = ImGui.GetStyle();
        var padding = style.FramePadding.X * 2f;
        var width = style.ItemSpacing.X + ImGui.CalcTextSize(teleportLabel).X + padding;
        if (links.WalkShown)
        {
            width += style.ItemSpacing.X + padding
                + System.MathF.Max(ImGui.CalcTextSize(Strings.TravelWalkShort).X, ImGui.CalcTextSize(Strings.TravelStop).X);
        }

        return width;
    }

    /// <summary>
    /// Teleport and, when shown, Walk (Stop while the character moves) as small buttons, each after
    /// <see cref="ImGui.SameLine()"/>. Teleport goes quiet (secondary text) when the player already stands closer to
    /// the giver than its aetheryte.
    /// </summary>
    public static void Buttons(GameLinks links, QuestRecord quest, string teleportLabel)
    {
        ImGui.SameLine();
        var teleport = links.CheckTeleport(quest);
        using (ImRaii.Disabled(!teleport.Ready))
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Surface.TextSecondary, teleport.Ready && teleport.AlreadyHere))
        {
            if (ImGui.SmallButton(teleportLabel))
            {
                links.TeleportToGiver(quest);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.TeleportTooltip(quest, teleport));
        }

        if (!links.WalkShown)
        {
            return;
        }

        ImGui.SameLine();
        var walk = links.CheckWalk(quest);
        using (ImRaii.PushId("walk"))
        using (ImRaii.Disabled(!walk.Ready && !walk.Stoppable))
        {
            if (ImGui.SmallButton(walk.Stoppable ? Strings.TravelStop : Strings.TravelWalkShort))
            {
                if (walk.Stoppable)
                {
                    links.StopTravel();
                }
                else
                {
                    links.WalkToGiver(quest);
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.WalkTooltip(quest, walk));
        }
    }
}
