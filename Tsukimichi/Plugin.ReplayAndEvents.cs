using System;
using System.Collections.Generic;
using Tsukimichi.Core.Unique;

namespace Tsukimichi;

/// <summary>
/// 1.19.0 "Right answers" wiring for New Game+ and seasonal events (feature plan v7, C4 and C10): the New Game+ HUD watch
/// (<see cref="Game.NewGamePlusWatch"/>, behind the shared hook gate) feeding the session; the player's entered event
/// end dates handed to the session; and the ending-soon warnings (<see cref="Ui.EventWarningSource"/>) shared by the
/// Journal rows' chips, the Tonight card, the Todo overlay and the chat line. Called once from the constructor after
/// the Todo overlay exists; the watch is unwound with the rest.
/// </summary>
public sealed partial class Plugin
{
    private Game.NewGamePlusWatch? newGamePlusWatch;

    private void InitializeReplayAndEvents(Core.Runtime.HookGate gate, Game.RewardUnlockReader unlocks, Func<UniqueRewardCatalog> rewards)
    {
        newGamePlusWatch = new Game.NewGamePlusWatch(Framework, ClientState, GameGui, Session, gate, Log);
        Session.SetEnteredFestivalEnds(new Dictionary<ushort, DateTime>(Settings.SeasonalEndDates));

        // "4 rewards you don't have": the event quests' unique rewards the character has not obtained (unknown ones aside).
        int RewardsMissing(Core.Model.QuestRecord quest)
        {
            var missing = 0;
            foreach (var entry in rewards().ForQuest(quest.RowId))
            {
                missing += unlocks.IsObtained(entry) == false ? 1 : 0;
            }

            return missing;
        }

        var events = new Ui.EventWarningSource(Session, () => Settings.SeasonalWarnDays, RewardsMissing);
        eventWarnings = events;
        mainWindow.AttachReplayAndEvents(new Ui.RowChipSource(Session, events), events);
        if (todoOverlay is { } overlay)
        {
            overlay.EventWarnings = events;
        }

        if (chatNotifier is { } chat)
        {
            chat.EventWarnings = events;
        }
    }
}
