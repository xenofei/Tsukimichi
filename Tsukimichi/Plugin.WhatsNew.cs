using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Tsukimichi.Core.Releases;
using Tsukimichi.Core.Ui;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// What's new (feature plan v8 W1–W4; spec-1.22): the plain release notes are read once at load
/// (<c>Data/curated/whats_new.json</c>) and shared with every surface through <see cref="WhatsNewNotes"/> (the update
/// note's hover reads them too); the popup decides at load whether an update brought something to show, and waits for
/// the first quiet moment: a character in the world for 10 s, not in combat, a duty, a cutscene, group pose or a loading
/// screen. Settings › Advanced › What's new lists every release and opens the popup on one.
/// </summary>
public sealed partial class Plugin
{
    private WhatsNewPopup? whatsNewPopup;

    // When the character was first seen in the world since login (Stopwatch ticks); 0 while logged out.
    private long inWorldSince;

    private void InitializeWhatsNew(MainWindow main, ConfigWindow settingsWindow)
    {
        var notes = ReleaseNotes.Load(System.IO.Path.Combine(Paths.CuratedDir, ReleaseNotes.FileName));
        foreach (var warning in notes.Warnings)
        {
            Log.Warning("What's new: {Warning}", warning);
        }

        WhatsNewNotes.Use(notes);
        var pluginDir = System.IO.Path.GetDirectoryName(PluginInterface.AssemblyLocation.FullName) ?? PluginInterface.AssemblyLocation.DirectoryName ?? ".";
        var running = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
        whatsNewPopup = new WhatsNewPopup(Settings, PluginInterface, Log, TextureProvider, notes, running, pluginDir)
        {
            Moment = WhatsNewMomentNow,
            MainWindowRect = () => main.DrawnRect,
            OpenAllReleases = () => settingsWindow.OpenAt(SettingsSection.Advanced, SettingsAnchor.WhatsNew),
        };
        windowSystem.AddWindow(whatsNewPopup);
        settingsWindow.WhatsNew = whatsNewPopup;
        whatsNewPopup.CheckAfterLoad();
    }

    /// <summary>What the player is doing now, for the popup's quiet moment (the conditions the Todo overlay and travel read).</summary>
    private WhatsNewMoment WhatsNewMomentNow()
    {
        var condition = Condition;
        var loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
        var inWorld = ClientState.IsLoggedIn && ObjectTable.LocalPlayer is not null;
        if (!ClientState.IsLoggedIn)
        {
            inWorldSince = 0;
        }
        else if (inWorld && !loading && inWorldSince == 0)
        {
            inWorldSince = Stopwatch.GetTimestamp();
        }

        var seconds = inWorldSince == 0 ? 0.0 : Stopwatch.GetElapsedTime(inWorldSince).TotalSeconds;
        return new WhatsNewMoment(
            inWorld,
            seconds,
            condition[ConditionFlag.InCombat],
            condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95],
            condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78],
            ClientState.IsGPosing,
            loading);
    }
}
