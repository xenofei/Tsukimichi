using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// "Next stops" in the Tonight card (1.6.0, R6 B): the first <see cref="MaxStops"/> stops of
/// <see cref="NextStopsSource"/>, each the aetheryte's name (a click shows its first quest), how many Ready quests
/// wait there, and Teleport. Kept apart from the rest of the card so the card's other rows can change without
/// touching this.
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>Stops the card lists.</summary>
    public const int MaxStops = 3;

    /// <summary>ImGui ids of the stop rows, clear of the pinned and main scenario rows'.</summary>
    private const int StopRowIdBase = 200;

    /// <summary>Next stops; set by the plugin. Null draws nothing.</summary>
    public NextStopsSource? Stops { get; set; }

    /// <summary>Teleports for the stops; set by the plugin with <see cref="Stops"/>.</summary>
    public GameLinks? Links { get; set; }

    // The stop lines, composed when the source rebuilds.
    private int stopsRevision = -1;
    private int stopsLanguage = -1;
    private (Stop Stop, string Text)[] stopLines = [];

    private void DrawStops()
    {
        if (Stops is not { } source || Links is not { } links)
        {
            return;
        }

        var stops = source.Stops;
        if (stopsRevision != source.Revision || stopsLanguage != Localization.Loc.Version)
        {
            stopsRevision = source.Revision;
            stopsLanguage = Localization.Loc.Version;
            var count = Math.Min(MaxStops, stops.Count);
            stopLines = new (Stop, string)[count];
            for (var i = 0; i < count; i++)
            {
                var stop = stops[i];
                var text = string.Format(CultureInfo.CurrentCulture, Strings.TonightStopFormat, stop.Place.Name, stop.CountText);
                stopLines[i] = (stop, stop.IsHere ? text + Strings.TonightStopHereSuffix : text);
            }
        }

        if (stopLines.Length == 0)
        {
            return;
        }

        Chrome.Hairline();
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextUnformatted(Strings.TonightStopsTitle);
        }

        for (var i = 0; i < stopLines.Length; i++)
        {
            var (stop, text) = stopLines[i];
            if (stop.Quests.Count == 0)
            {
                continue;
            }

            var first = stop.Quests[0].Quest;
            using var id = ImRaii.PushId(StopRowIdBase + i);
            var teleportWidth = Chrome.ActionPillWidth(ActionIcons.TeleportIcon, Strings.TonightStopTeleport, PillLayout.Row);
            var room = MathF.Max(1f, Chrome.RoomX() - teleportWidth - ImGui.GetStyle().ItemSpacing.X);
            if (Chrome.EllipsisSelectable(text, false, room, out var cut))
            {
                ui.Reveal(first);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(cut ? text : stop.Place.Name, Strings.TonightStopTooltip);
            }

            ImGui.SameLine();
            var canTeleport = links.CanTeleport(first);
            if (Chrome.ActionPill("##stopTeleport", ActionIcons.TeleportIcon, Strings.TonightStopTeleport, PillTone.Normal, canTeleport, size: PillLayout.Row))
            {
                links.TeleportToGiver(first);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                // The same text as every Teleport: the aetheryte, its gil cost, "already here", or why it cannot.
                UiMetrics.Tooltip(links.TeleportTooltip(first));
            }
        }
    }
}
