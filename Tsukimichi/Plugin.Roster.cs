using System;
using System.Linq;
using Tsukimichi.Commands;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.21.0 wiring for every character (plan v7 P1, P3, N11): the linked launcher folders' reader, the roster source
/// that Up next, the roster, the goal card, the detail hero's "your other characters" line and <c>/tsuki next</c>
/// share, and Settings › Data › Characters' linked folders.
/// </summary>
public sealed partial class Plugin
{
    private Game.LinkedFolderService? linkedFolders;

    /// <summary>The <c>/tsuki msq|next|go</c> command once wired (Plugin.Guidance.cs), so Up next's goal and level gate reach it.</summary>
    private GuidanceCommand? guidanceCommand;

    private void WireRoster(
        MainWindow mainWindow,
        CharactersPane characters,
        ConfigWindow config,
        Game.ActiveRouteService routes,
        Func<UniqueRewardCatalog> rewards,
        Func<DutyRunIndex?> dutyRuns,
        DutyUnlockIndexSource dutyUnlocks,
        Func<FlightIndex?> flight)
    {
        linkedFolders = new Game.LinkedFolderService(Settings, Session, Paths.ConfigDir, Log);
        var roster = new RosterSource(Session, Roster, Snapshots.Load, linkedFolders, Log)
        {
            Rewards = rewards,
            DutyRuns = dutyRuns,
            DutyUnlocks = dutyUnlocks,
            Flight = flight,
        };

        characters.Board = roster;
        mainWindow.AttachRoster(roster, routes);
        config.LinkedFolders = linkedFolders;
        if (guidanceCommand is { } guidance)
        {
            // /tsuki next and Say what's next follow Up next's own pick: the goal second, the level gate last.
            guidance.Goal = id => roster.GoalProgressOf(id)?.Quests.Select(static q => q.RowId) ?? [];
            guidance.LevelGate = roster.LevelGateOf;
        }
    }
}
