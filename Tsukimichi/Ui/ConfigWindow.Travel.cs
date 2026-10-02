using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Integrations › Travel, getting there faster (travel review, 1.10): "Mount for walks longer than N yalms"
/// (40 by default, 0 never mounts), which mount (Mount Roulette or one the character owns), "Fly where flying is
/// unlocked" and "Sprint in towns". They apply to Walk to giver and Go to giver, which only move on a click; drawn
/// under the Travel block's Show Walk and Show Go to giver rows.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The longest mount distance the slider offers, in yalms.</summary>
    private const int MaxMountDistance = 200;

    private bool mountDistanceDirty;

    /// <summary>The mounts the character owns (Mount sheet row and name, sorted by name), for the mount choice; empty when unknown.</summary>
    public Func<IReadOnlyList<(uint Id, string Name)>>? OwnedMounts { get; set; }

    private void DrawTravelMovement()
    {
        if (Row(Strings.ConfigTravelMountDistance, Strings.ConfigTravelMountDistanceHint, "travel mount ride distance yalms walk vnavmesh"))
        {
            var distance = Math.Clamp(settings.TravelMountDistance, 0, MaxMountDistance);
            ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(160f)));
            if (ImGui.SliderInt("##travelMountDistance", ref distance, 0, MaxMountDistance, Strings.ConfigTravelMountDistanceFormat, ImGuiSliderFlags.AlwaysClamp))
            {
                settings.TravelMountDistance = distance;
                mountDistanceDirty = true;
            }

            if (mountDistanceDirty && ImGui.IsItemDeactivatedAfterEdit())
            {
                mountDistanceDirty = false;
                Save();
            }

            HintOnHover(Strings.ConfigTravelMountDistanceHint);
            Chrome.TrailingLabel(Strings.ConfigTravelMountDistance);
        }

        if (Row(Strings.ConfigTravelMount, Strings.ConfigTravelMountHint, "travel mount roulette favourite favorite"))
        {
            using (SubSetting(settings.TravelMountDistance > 0))
            {
                DrawMountChoice();
            }
        }

        if (Row(Strings.ConfigTravelFly, Strings.ConfigTravelFlyHint, "travel fly flying aether currents mount"))
        {
            using (SubSetting(settings.TravelMountDistance > 0))
            {
                var fly = settings.TravelFly;
                if (ImGui.Checkbox(Strings.ConfigTravelFly, ref fly))
                {
                    settings.TravelFly = fly;
                    Save();
                }

                HintOnHover(Strings.ConfigTravelFlyHint);
            }
        }

        if (Row(Strings.ConfigTravelSprint, Strings.ConfigTravelSprintHint, "travel sprint town city walk"))
        {
            var sprint = settings.TravelSprintInTowns;
            if (ImGui.Checkbox(Strings.ConfigTravelSprint, ref sprint))
            {
                settings.TravelSprintInTowns = sprint;
                Save();
            }

            HintOnHover(Strings.ConfigTravelSprintHint);
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

        ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(220f)));
        using (var combo = ImRaii.Combo("##travelMount", current))
        {
            if (combo)
            {
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

        HintOnHover(Strings.ConfigTravelMountHint);
        Chrome.TrailingLabel(Strings.ConfigTravelMount);
    }
}
