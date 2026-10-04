using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The current step's items at the top of a quest row's right-click menu (plan v7, 1.21.0 P2; the Todo overlay and
/// Nearby's "…"): Flag the current step, Teleport near the current step and Walk to the current step, each shown by the
/// automation level like every travel item (1.18: hidden above it), then a separator; the caller's Flag then reads
/// "Flag the giver". Nothing for a quest that is not in the journal or whose step has no place.
/// </summary>
internal static class StepTravelMenu
{
    /// <summary>Draws the step's items; true when it did (the caller labels its own Flag item "Flag the giver").</summary>
    public static bool Draw(GameLinks links, SessionState session, QuestRecord quest)
    {
        session.States.TryGetValue(quest.RowId, out var evaluation);
        var live = session.IsLive;
        if (links.CurrentStep(quest, evaluation, live, live ? null : session.ViewedSnapshot?.Name, session.ViewedContentId ?? 0) is not { Travel: { } step })
        {
            return false;
        }

        if (ImGui.MenuItem(Strings.StepMenuFlag, enabled: links.CanFlagMap(step)))
        {
            links.FlagMap(step);
        }

        if (links.TeleportShown)
        {
            var teleport = links.CheckTeleport(step);
            if (ImGui.MenuItem(Strings.StepMenuTeleport, enabled: teleport.Ready))
            {
                links.TeleportToGiver(step);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.TeleportTooltip(step, teleport));
            }
        }

        if (links.WalkShown && !links.IsTraveling)
        {
            var walk = links.CheckWalk(step);
            if (ImGui.MenuItem(Strings.StepMenuWalk, enabled: walk.Ready))
            {
                links.WalkToGiver(step);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.WalkTooltip(step, walk));
            }
        }

        ImGui.Separator();
        return true;
    }
}
