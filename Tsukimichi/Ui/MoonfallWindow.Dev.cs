using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Ui;

// Developer mode: remove after all 115 Moonfall levels ship.
// The hidden developer mode (/tsukimichi moonfall dev): a compact bar above the screens with a level picker, Previous and
// Next, and the board's level id, code and Ace; R restarts the board's level. Unlocking, the open shield and the
// unrecorded results are MoonfallModes.UnlockAll's; this file is the bar and the key. English only, no animation.
// To remove the mode: delete this file and every "Developer mode" comment's code (grep for it), plus the flag in
// Configuration.Moonfall.cs.
public sealed partial class MoonfallWindow
{
    private static readonly string[] DevCampaigns = ["The Moon Road", "The Far Shore"];
    private static readonly string[] DevLevelNumbers = ["1", "2", "3", "4", "5"];
    private string[][]? devStageNames;

    private bool devMode;
    private int devCampaign;
    private int devStage;
    private int devLevel;
    private string devId = string.Empty;
    private string devSyncedFor = string.Empty;
    private string devNote = string.Empty;
    private string devInfo = string.Empty;
    private string devInfoFor = string.Empty;

    /// <summary>Developer mode: on lifts every lock and the spoiler shield and records nothing; set by the plugin and its hidden command.</summary>
    internal bool DevMode
    {
        get => devMode;
        set
        {
            if (devMode == value)
            {
                return;
            }

            devMode = value;
            modes.UnlockAll = value;
            devSyncedFor = string.Empty;
            devNote = string.Empty;
            devInfoFor = string.Empty;
            devStageNames = null;
            // The menus' views and the scene veil are remade for the new shield and locks.
            shieldSeen = int.MinValue;
            progressEpoch++;
        }
    }

    private string[] BuildDevStageNames(MoonfallCampaignKind campaign)
    {
        var names = new List<string>();
        foreach (var stage in MoonfallStages.Of(campaign))
        {
            names.Add(string.Create(CultureInfo.InvariantCulture, $"{stage.Number}. ") + modes.StageName(stage));
        }

        return [.. names];
    }

    /// <summary>The bar, and the restart key. Nothing is drawn or read with developer mode off.</summary>
    private void DrawDevBar()
    {
        if (!devMode)
        {
            return;
        }

        if (flow.Current == MoonfallScreen.Play && playLevel is { } shown && !string.Equals(devSyncedFor, shown.Id, StringComparison.Ordinal))
        {
            // The picker follows the board's level.
            devSyncedFor = shown.Id;
            devNote = string.Empty;
            if (MoonfallStages.TryPlace(shown.Id, out var at))
            {
                devCampaign = at.Campaign == MoonfallCampaignKind.Expansion ? 1 : 0;
                devStage = at.Index / MoonfallCharacters.LevelsPerStage;
                devLevel = at.Index % MoonfallCharacters.LevelsPerStage;
                devId = shown.Id;
            }
        }

        if (flow.Current == MoonfallScreen.Play && game is not null && Keyboard.RestartLevelPressed())
        {
            Restart();
            return;
        }

        var kind = devCampaign == 1 ? MoonfallCampaignKind.Expansion : MoonfallCampaignKind.Base;
        devStageNames ??= [BuildDevStageNames(MoonfallCampaignKind.Base), BuildDevStageNames(MoonfallCampaignKind.Expansion)];
        var stageNames = devStageNames[devCampaign];
        devStage = Math.Clamp(devStage, 0, stageNames.Length - 1);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Dev");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(110f));
        if (ImGui.Combo("##devCampaign", ref devCampaign, DevCampaigns, DevCampaigns.Length))
        {
            devStage = 0;
            devLevel = 0;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(170f));
        ImGui.Combo("##devStage", ref devStage, stageNames, stageNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(44f));
        ImGui.Combo("##devLevel", ref devLevel, DevLevelNumbers, DevLevelNumbers.Length);
        ImGui.SameLine();
        if (ImGui.Button("Go##devGo"))
        {
            StartDev(kind, (devStage * MoonfallCharacters.LevelsPerStage) + devLevel);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(84f));
        if (ImGui.InputTextWithHint("##devId", "base-07", ref devId, 16, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            StartDevId();
        }

        ImGui.SameLine();
        if (ImGui.Button("Go##devGoId"))
        {
            StartDevId();
        }

        var here = flow.Current == MoonfallScreen.Play && playLevel is not null && MoonfallStages.TryPlace(playLevel.Id, out _);
        MoonfallLevelPlace place = default;
        if (here)
        {
            MoonfallStages.TryPlace(playLevel!.Id, out place);
        }

        ImGui.BeginDisabled(!here);
        if (ImGui.Button("Previous##devPrev"))
        {
            StartDev(place.Campaign, place.Index - 1);
        }

        ImGui.SameLine();
        if (ImGui.Button("Next##devNext"))
        {
            StartDev(place.Campaign, place.Index + 1);
        }

        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(devNote.Length > 0 ? devNote : DevInfo());
    }

    private void StartDevId()
    {
        var typed = devId.Trim();
        if (!MoonfallStages.TryPlace(typed, out var place))
        {
            devNote = $"\"{typed}\" is not a level id (base-01 to base-55, expansion-01 to expansion-60).";
            return;
        }

        StartDev(place.Campaign, place.Index);
    }

    /// <summary>Starts a level of Adventure at once, with its stage's companion (on a "Your Pick" stage the last pick, else none).</summary>
    private void StartDev(MoonfallCampaignKind kind, int index)
    {
        devNote = string.Empty;
        if (index < 0 || index >= MoonfallStages.LevelCount(kind))
        {
            devNote = "No level that way.";
            return;
        }

        var pick = MoonfallStages.StageOf(kind, index) is { PlayerPicks: true } ? adventurePick : MoonfallCompanion.None;
        if (!PlayAdventure(kind, index, pick))
        {
            devNote = MoonfallStages.LevelId(kind, index) + " does not ship yet.";
        }
    }

    /// <summary>"base-07 · 2-2 · Ace 290,000" for the board's level, made when the level changes.</summary>
    private string DevInfo()
    {
        if (flow.Current != MoonfallScreen.Play || playLevel is not { } level)
        {
            return "R restarts a level.";
        }

        if (!string.Equals(devInfoFor, level.Id, StringComparison.Ordinal))
        {
            devInfoFor = level.Id;
            var c = CultureInfo.InvariantCulture;
            var code = MoonfallStages.TryPlace(level.Id, out var place) ? PlaceCode(place) : "?";
            var ace = modes.AceOf(level.Id) is { } a ? "Ace " + a.ToString("N0", c) : "no Ace";
            devInfo = $"{level.Id} · {code} · {ace} · R restarts";
        }

        return devInfo;
    }
}
