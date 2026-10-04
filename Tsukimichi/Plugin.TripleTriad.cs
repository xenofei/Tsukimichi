using System;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Triad;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.21.0 wiring for P6 and P7 (feature plan v7): the Triple Triad opponents, read once on a warm-up worker, feed the
/// capture (beaten flags and cards, behind the shared hook gate), the Characters dashboard's Triple Triad card and the
/// detail pane's Unlocks line; Nearby's Everywhere view reads the zones of the unlock index, the plan's blues, the side
/// stories and the Moonlit rewards. Called once from the constructor, after the panes and Nearby exist.
/// </summary>
public sealed partial class Plugin
{
    private void InitializeTripleTriad(Game.IndexWarmer warmer, Game.RewardUnlockReader unlockReader, MoonlitPane moonlit, QuestionableActions questionable)
    {
        Func<TriadOpponents?> opponents = () => warmer.Triad.Value;
        if (stateReader is not null)
        {
            stateReader.TriadIndex = opponents;
        }

        if (charactersPane is not null)
        {
            charactersPane.TriadBoard = new TriadBoardSource(Session, opponents, gameLinks);
            charactersPane.TriadLinks = gameLinks;
        }

        mainWindow.AttachTriad(opponents);

        if (discoveryWindow is not { } nearby)
        {
            return;
        }

        var session = Session;
        var plans = planSource;
        nearby.Zones = () => questUnlocks?.Current.Zones;
        nearby.Questionable = questionable;
        nearby.QuestKinds = quest =>
        {
            var reward = false;
            foreach (var entry in moonlit.Catalog.ForQuest(quest.RowId))
            {
                if (unlockReader.IsObtained(entry) != true)
                {
                    reward = true;
                    break;
                }
            }

            return new ZoneQuestKinds(plans?.Tags.Contains(quest.RowId) == true, session.Stories.Contains(quest.RowId), reward);
        };
    }
}
