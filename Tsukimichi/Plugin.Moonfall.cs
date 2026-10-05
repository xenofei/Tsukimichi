using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Game;
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
    /// <summary>The game's window, so unloading saves progress a failed save left unsaved.</summary>
    private MoonfallWindow? moonfallWindow;

    /// <summary>Moonfall's sound (plan v9 G8), stopped and let go when the plugin unloads.</summary>
    private MoonfallAudio? moonfallAudio;

    /// <summary>The keys Moonfall claims from the game, consumed on each framework update while it answers them.</summary>
    private GameKeyClaim? moonfallKeys;

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

        var pluginDirectory = PluginInterface.AssemblyLocation.DirectoryName;
        var options = new MoonfallConfigOptions(Settings, () => Settings.Save(PluginInterface));
        // The companions follow the viewed character's spoiler shield (its NPC rule): a name not yet met shows the card back.
        var story = new MoonfallStory(name => !Session.Spoilers.IsNameMasked(Core.Query.SpoilerKind.Npc, name));
        var window = new MoonfallWindow(campaigns, progress, path, MoonfallCausesNow, Log, TextureProvider, pluginDirectory, DataManager, PluginInterface.UiBuilder.FontAtlas, options, story);
        // The keys Moonfall answers (Esc; the arrows, Tab, Enter and Space on its menus) are taken back from the game.
        moonfallKeys = GameKeyClaim.ForGame();
        window.Keys = moonfallKeys;
        Framework.Update += moonfallKeys.Consume;
        // Its sound: the output starts when the window opens; the volume lives in the settings.
        moonfallAudio = new MoonfallAudio(
            Log,
            GameConfig,
            () => Settings.MoonfallSoundPercent,
            percent =>
            {
                Settings.MoonfallSoundPercent = percent;
                Settings.Save(PluginInterface);
            });
        window.Audio = moonfallAudio;
        windowSystem.AddWindow(window);
        command.ToggleMoonfall = window.Toggle;
        mainWindow.OpenMoonfall = window.Toggle;
        moonfallWindow = window;
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
        if (moonfallKeys is not null)
        {
            Framework.Update -= moonfallKeys.Consume;
            moonfallKeys = null;
        }

        moonfallWindow?.SaveNow();
        moonfallWindow?.DisposeArt();
        if (moonfallWindow is not null)
        {
            moonfallWindow.Audio = null;
        }

        moonfallWindow = null;
        moonfallAudio?.Dispose();
        moonfallAudio = null;
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
