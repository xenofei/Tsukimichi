using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// Moonfall wiring (feature plan v9, 1.23.0): the shipped levels and the account's progress (<c>user/moonfall.json</c>)
/// read at load, the game's window, the main window's button and <c>/tsuki moonfall</c>. The window pauses itself in
/// combat, duties and cutscenes (<see cref="MoonfallCausesNow"/>, the conditions the moon icon and travel read). Called
/// once from the constructor after the moon icon (<see cref="InitializeMoonfall"/>), unwound by
/// <see cref="DisposeMoonfall"/>; the window itself goes with the window system's.
/// </summary>
public sealed partial class Plugin
{
    /// <summary>Sets Moonfall up; a failure is logged and leaves the game out, never the plugin.</summary>
    private void InitializeMoonfall()
    {
        try
        {
            CreateMoonfall();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Moonfall could not be set up; the rest of Tsukimichi runs without it");
        }
    }

    private void CreateMoonfall()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        foreach (var error in campaigns.Errors)
        {
            Log.Warning("Moonfall level not loaded: {Error}", error);
        }

        var path = MoonfallProgress.PathFor(Paths);
        var warnings = new List<string>();
        var progress = MoonfallProgress.Load(path, warnings);
        foreach (var warning in warnings)
        {
            Log.Warning("Moonfall progress: {Warning}", warning);
        }

        var window = new MoonfallWindow(campaigns, progress, path, MoonfallCausesNow, Log);
        windowSystem.AddWindow(window);
        command.ToggleMoonfall = window.Toggle;
        mainWindow.OpenMoonfall = window.Toggle;
    }

    /// <summary>What pauses the board now: combat, a duty or a cutscene.</summary>
    private MoonfallPauseReason MoonfallCausesNow()
    {
        var condition = Condition;
        var reasons = MoonfallPauseReason.None;
        if (condition[ConditionFlag.InCombat])
        {
            reasons |= MoonfallPauseReason.Combat;
        }

        if (condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            reasons |= MoonfallPauseReason.Duty;
        }

        if (condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78])
        {
            reasons |= MoonfallPauseReason.Cutscene;
        }

        return reasons;
    }

    private void DisposeMoonfall()
    {
        if (command is not null)
        {
            command.ToggleMoonfall = null;
        }

        if (mainWindow is not null)
        {
            mainWindow.OpenMoonfall = null;
        }
    }
}
