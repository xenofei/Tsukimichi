using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Automation › Travel (1.6.0; getting there faster, 1.10): the Walk and Go to giver buttons, "Mount for
/// walks over N yalms" (40 by default, Off never mounts), which mount (Mount Roulette or one the character owns), "Fly
/// where unlocked" and "Sprint in towns". They apply to Walk to giver and Go to giver, which only move on a click; the
/// panes read them per draw through GameLinks, so no callback is needed.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The longest mount distance the slider offers, in yalms.</summary>
    private const int MaxMountDistance = 200;

    /// <summary>The mounts the character owns (Mount sheet row and name, sorted by name), for the mount choice; empty when unknown.</summary>
    public Func<IReadOnlyList<(uint Id, string Name)>>? OwnedMounts { get; set; }

    private void DrawTravelSettings()
    {
        Header(Strings.ConfigSectionTravel);
        var showWalk = settings.ShowWalkToGiver;
        if (Toggle(Strings.ConfigShowWalk, Strings.ConfigShowWalkHint, ref showWalk, "travel vnavmesh walk move button"))
        {
            settings.ShowWalkToGiver = showWalk;
            Save();
        }

        var showGoTo = settings.ShowGoToGiver;
        if (Toggle(Strings.ConfigShowGoTo, Strings.ConfigShowGoToHint, ref showGoTo, "travel teleport lifestream aethernet vnavmesh go to giver button"))
        {
            settings.ShowGoToGiver = showGoTo;
            Save();
        }

        if (Setting(Strings.ConfigTravelMountDistance, Strings.ConfigTravelMountDistanceHint, "travel mount ride distance yalms walk vnavmesh"))
        {
            var distance = Math.Clamp(settings.TravelMountDistance, 0, MaxMountDistance);
            ImGui.SetNextItemWidth(ControlWidth);
            if (ImGui.SliderInt("##travelMountDistance", ref distance, 0, MaxMountDistance, distance == 0 ? Strings.WelcomeBackConfigOff : Strings.ConfigTravelMountDistanceFormat, ImGuiSliderFlags.AlwaysClamp))
            {
                settings.TravelMountDistance = distance;
                SaveSoon();
            }

            EndSetting();
        }

        var mounting = settings.TravelMountDistance > 0;
        if (Setting(Strings.ConfigTravelMount, Strings.ConfigTravelMountHint, "travel mount roulette favourite favorite", enabled: mounting, sub: true, reason: Strings.SettingsMountOffReason))
        {
            DrawMountChoice();
            EndSetting();
        }

        var fly = settings.TravelFly;
        if (Toggle(Strings.ConfigTravelFly, Strings.ConfigTravelFlyHint, ref fly, "travel fly flying aether currents mount", mounting, sub: true, reason: Strings.SettingsMountOffReason))
        {
            settings.TravelFly = fly;
            Save();
        }

        var sprint = settings.TravelSprintInTowns;
        if (Toggle(Strings.ConfigTravelSprint, Strings.ConfigTravelSprintHint, ref sprint, "travel sprint town city walk"))
        {
            settings.TravelSprintInTowns = sprint;
            Save();
        }
    }

    /// <summary>Mount Roulette or one of the character's own mounts; a chosen mount no longer owned reads as Roulette when used.</summary>
    private void DrawMountChoice()
    {
        var mounts = OwnedMounts?.Invoke() ?? [];
        var current = Strings.ConfigTravelMountRoulette;
        foreach (var (id, name) in mounts)
        {
            if (id == settings.TravelMountId)
            {
                current = name;
                break;
            }
        }

        ImGui.SetNextItemWidth(ControlWidth);
        using var combo = ImRaii.Combo("##travelMount", current);
        if (!combo)
        {
            return;
        }

        if (ImGui.Selectable(Strings.ConfigTravelMountRoulette, settings.TravelMountId == 0))
        {
            settings.TravelMountId = 0;
            Save();
        }

        foreach (var (id, name) in mounts)
        {
            if (ImGui.Selectable(name + "##mount" + id, settings.TravelMountId == id))
            {
                settings.TravelMountId = id;
                Save();
            }
        }
    }
}
