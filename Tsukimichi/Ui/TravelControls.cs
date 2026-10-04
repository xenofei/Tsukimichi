using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The travel controls every quest row shares (feature plan v5, 1.6.0): Teleport (shown from the Travel automation
/// level, 1.18; without Lifestream greyed with a tooltip naming it, decision 2), Walk to giver and Go to giver (from
/// Travel and walking; Stop while the character moves; greyed naming vnavmesh without it). <see cref="MenuItems"/> for a row's "…" or right-click menu,
/// <see cref="Buttons"/> for a row's small buttons. Tooltips are composed only on hover.
/// </summary>
internal static class TravelControls
{
    /// <summary>Teleport, then Walk to giver and Go to giver (one Stop while moving), as menu items; each only while its automation level shows it.</summary>
    public static void MenuItems(GameLinks links, QuestRecord quest, string teleportLabel)
    {
        if (links.TeleportShown)
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
        }

        if (links.IsTraveling && (links.WalkShown || links.GoToShown))
        {
            if (ImGui.MenuItem(Strings.TravelStop))
            {
                links.StopTravel();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(links.StopTooltip());
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
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var width = links.TeleportShown ? gap + Chrome.ActionPillWidth(ActionIcons.TeleportIcon, teleportLabel, PillLayout.Row) : 0f;
        if (links.WalkShown)
        {
            width += gap + WalkWidth();
        }

        return width;
    }

    /// <summary>Walk's width in a row, sized for the longer of Walk and Stop so the row does not shift when it turns.</summary>
    public static float WalkWidth() =>
        System.MathF.Max(
            Chrome.ActionPillWidth(ActionIcons.WalkIcon, Strings.TravelWalkShort, PillLayout.Row),
            Chrome.ActionPillWidth(ActionGlyphs.Stop, Strings.TravelStop, PillLayout.Row));

    /// <summary>
    /// Teleport and, when shown, Walk (Stop while the character moves) as a row's small icon-and-label buttons (UI-5e:
    /// the aetheryte, Sprint), each after <see cref="ImGui.SameLine()"/>. Teleport goes quiet (secondary text) when the
    /// player already stands closer to the giver than its aetheryte.
    /// </summary>
    public static void Buttons(GameLinks links, QuestRecord quest, string teleportLabel)
    {
        if (links.TeleportShown)
        {
            ImGui.SameLine();
            TeleportButton(links, quest, teleportLabel);
        }

        if (!links.WalkShown)
        {
            return;
        }

        ImGui.SameLine();
        WalkButton(links, quest);
    }

    /// <summary>Teleport to the giver's aetheryte as a row's button; quiet when the player already stands closer; disabled, saying why, when it cannot.</summary>
    public static void TeleportButton(GameLinks links, QuestRecord quest, string label)
    {
        var teleport = links.CheckTeleport(quest);
        if (Chrome.ActionPill("##travelTeleport", ActionIcons.TeleportIcon, label, teleport.AlreadyHere ? PillTone.Quiet : PillTone.Normal, teleport.Ready, size: PillLayout.Row))
        {
            links.TeleportToGiver(quest);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.TeleportTooltip(quest, teleport));
        }
    }

    /// <summary>Walk to giver through vnavmesh as a row's button, or Stop while the character moves; disabled, saying why, when it cannot start.</summary>
    public static void WalkButton(GameLinks links, QuestRecord quest)
    {
        var walk = links.CheckWalk(quest);
        var stop = walk.Stoppable;
        if (Chrome.ActionPill("##travelWalk", stop ? ActionGlyphs.Stop : ActionIcons.WalkIcon, stop ? Strings.TravelStop : Strings.TravelWalkShort,
                stop ? PillTone.Danger : PillTone.Normal, walk.Ready || stop, size: PillLayout.Row))
        {
            if (stop)
            {
                links.StopTravel();
            }
            else
            {
                links.WalkToGiver(quest);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.WalkTooltip(quest, walk));
        }
    }

    /// <summary>
    /// A row's Flag on the map button (the flag marker, UI-5e), labelled <paramref name="label"/>; disabled when
    /// <paramref name="enabled"/> is false. Returns true when clicked; the caller adds its tooltip.
    /// </summary>
    public static bool FlagButton(string label, bool enabled, string id = "##travelFlag") =>
        Chrome.ActionPill(id, ActionIcons.FlagIcon, label, PillTone.Normal, enabled, size: PillLayout.Row);

    /// <summary>The width of <see cref="FlagButton"/>.</summary>
    public static float FlagWidth(string label) => Chrome.ActionPillWidth(ActionIcons.FlagIcon, label, PillLayout.Row);

    /// <summary>
    /// A row's small icon-and-label button with a FontAwesome glyph (Reveal, Show in journal, Route; actions the game has
    /// no icon for). Returns true when clicked.
    /// </summary>
    public static bool RowButton(string id, PillIcon icon, string label, bool enabled = true) =>
        Chrome.ActionPill(id, icon, label, PillTone.Normal, enabled, size: PillLayout.Row);

    /// <summary>
    /// A toolbar's icon-and-label button beside framed ones (Flag next stop, Follow this route, Route, Pin): the frame's
    /// height so it lines up with them. Returns true when clicked.
    /// </summary>
    public static bool ToolbarButton(string id, PillIcon icon, string label, bool enabled = true) =>
        Chrome.ActionPill(id, icon, label, PillTone.Normal, enabled, size: PillLayout.Frame);

    /// <summary>The width of <see cref="ToolbarButton"/>.</summary>
    public static float ToolbarButtonWidth(PillIcon icon, string label) => Chrome.ActionPillWidth(icon, label, PillLayout.Frame);

    /// <summary>The width of <see cref="RowButton"/>.</summary>
    public static float RowButtonWidth(PillIcon icon, string label) => Chrome.ActionPillWidth(icon, label, PillLayout.Row);
}
