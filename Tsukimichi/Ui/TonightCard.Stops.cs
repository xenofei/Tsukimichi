using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// "Next stops" in the Tonight card (1.6.0, R6 B): the first <see cref="MaxStops"/> stops of
/// <see cref="NextStopsSource"/>, each the aetheryte's name (a click shows its first quest), how many Ready quests
/// wait there, and Teleport. An aetheryte the story has not reached reads as its placeholder, in Secondary, with the
/// shield's hover and right-click, and no Teleport (1.20.0 N6). Kept apart from the rest of the card so the card's other rows can change without
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

    // The stop lines, composed when the source rebuilds, the language or the spoiler shield changes; Place is the
    // aetheryte as printed, Hidden while that is its placeholder.
    private int stopsRevision = -1;
    private int stopsLanguage = -1;
    private int stopsShield = int.MinValue;
    private (Stop Stop, string Text, string Place, bool Hidden)[] stopLines = [];

    private void DrawStops()
    {
        if (Stops is not { } source || Links is not { } links)
        {
            return;
        }

        var stops = source.Stops;
        var spoilers = runner.Spoilers;
        if (stopsRevision != source.Revision || stopsLanguage != Localization.Loc.Version || stopsShield != spoilers.Fingerprint)
        {
            stopsRevision = source.Revision;
            stopsLanguage = Localization.Loc.Version;
            stopsShield = spoilers.Fingerprint;
            var count = Math.Min(MaxStops, stops.Count);
            stopLines = new (Stop, string, string, bool)[count];
            for (var i = 0; i < count; i++)
            {
                var stop = stops[i];
                var hidden = spoilers.IsNameMasked(SpoilerKind.Aetheryte, stop.Place.Name);
                var place = spoilers.Name(SpoilerKind.Aetheryte, stop.Place.Name);
                var text = string.Format(CultureInfo.CurrentCulture, Strings.TonightStopFormat, place, stop.CountText);
                stopLines[i] = (stop, stop.IsHere ? text + Strings.TonightStopHereSuffix : text, place, hidden);
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
            var (stop, text, place, hidden) = stopLines[i];
            if (stop.Quests.Count == 0)
            {
                continue;
            }

            var first = stop.Quests[0].Quest;
            using var id = ImRaii.PushId(StopRowIdBase + i);
            // Teleport only while the automation level shows it (1.18, A10); the name then takes the row.
            var teleportShown = links.TeleportShown;
            var teleportWidth = teleportShown ? Chrome.ActionPillWidth(ActionIcons.TeleportIcon, Strings.TonightStopTeleport, PillLayout.Row) : 0f;

            // The first quest's giver as a 24 px avatar before the stop (1.15, spec A5), so you know who to walk up to.
            var rowTop = ImGui.GetCursorScreenPos();
            var row = 0f;
            if (GiverPortraits.Enabled)
            {
                var avatar = MathF.Round(UiMetrics.Px(PortraitPlate.AvatarSize));
                row = MathF.Max(avatar, Chrome.PillHeight(PillLayout.Row));
                StopAvatar(first, avatar, row);
                ImGui.SameLine(0f, UiMetrics.Px(PortraitPlate.AvatarGap));
            }

            var room = MathF.Max(1f, Chrome.RoomX() - (teleportShown ? teleportWidth + ImGui.GetStyle().ItemSpacing.X : 0f));
            bool clicked;
            bool cut;
            using (Theme.PushText(hidden ? Theme.Surface.TextSecondary : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]))
            {
                clicked = Chrome.EllipsisSelectable(text, false, room, out cut, height: row);
            }

            if (clicked)
            {
                ui.Reveal(first);
            }

            if (hidden && runner.Session is { } session)
            {
                // A stop by an aetheryte the story has not reached: the placeholder's hover and right-click.
                ShieldText.InteractItem(session, SpoilerKind.Aetheryte, stop.Place.Name, place, first, links, lead: cut ? text : null);
            }
            else if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(cut ? text : place, Strings.TonightStopTooltip);
            }

            // No Teleport to a place either story has not reached (hidden, never greyed; its room stays).
            if (!teleportShown || hidden || links.GiverPlaceHidden(first))
            {
                continue;
            }

            ImGui.SameLine();
            if (row > 0f)
            {
                // The pill on the row's middle, beside the avatar's taller line.
                ImGui.SetCursorScreenPos(new System.Numerics.Vector2(ImGui.GetCursorScreenPos().X, rowTop.Y + MathF.Floor((row - Chrome.PillHeight(PillLayout.Row)) * 0.5f)));
            }

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

    /// <summary>
    /// A stop's giver avatar (1.15, F5): the plate <paramref name="avatar"/> px across, centred on a row
    /// <paramref name="row"/> tall, as an item whose hover shows the 128 px portrait.
    /// </summary>
    private void StopAvatar(Core.Model.QuestRecord quest, float avatar, float row)
    {
        var top = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new System.Numerics.Vector2(avatar, row));
        var request = GiverPortraits.For(quest, runner.Spoilers);
        Chrome.Portrait(ImGui.GetWindowDrawList(), new System.Numerics.Vector2(top.X, top.Y + MathF.Round((row - avatar) * 0.5f)), avatar, request);
        if (ImGui.IsItemHovered())
        {
            Chrome.PortraitTooltip(request, GiverPortraits.Name(quest, runner.Spoilers), GiverPortraits.Place(quest, runner.Spoilers));
        }
    }
}
