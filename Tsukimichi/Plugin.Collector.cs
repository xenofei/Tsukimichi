using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Tsukimichi.Commands;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.9.0 collector extras (feature plan v5): the story recap window and <c>/tsuki recap</c>, the game's achievement
/// flags for the achievements that need several quests, and the free-trial hint in Settings. Wired from the
/// constructor once the panes and the settings window exist.
/// </summary>
public sealed partial class Plugin
{
    private RecapWindow? recapWindow;

    private void InitializeCollector(Game.RewardUnlockReader unlockReader)
    {
        // The story recap ("Previously…"): the Since you were away card, a chain quest's detail pane, the Characters
        // tab's achievement rows and /tsuki recap ask UiState for it.
        var recap = new RecapWindow(Session, Settings, () => QuestText);
        recapWindow = recap;
        windowSystem.AddWindow(recap);
        ui.RecapRequested += recap.Show;
        command.Recap = new RecapCommand(Session, ui, gameLinks).Run;

        // The achievements that need several quests read the game's own flag when it has one.
        mainWindow.AttachAchievementFlags(unlockReader.AchievementEarned);
        if (charactersPane is not null)
        {
            charactersPane.AchievementEarned = unlockReader.AchievementEarned;
        }

        // The free-trial view stays the player's choice; Settings only mentions what the game's condition flag says.
        if (configWindow is not null)
        {
            configWindow.FreeTrialDetected = () => Condition[ConditionFlag.OnFreeTrial];
        }
    }

    /// <summary>
    /// A new catalog landed: the snapshot records the game's flag for the achievements that need several quests while
    /// the achievement list is loaded, so a stored character keeps it.
    /// </summary>
    private void OnCollectorCatalog(CatalogBundle bundle)
    {
        if (stateReader is not null)
        {
            stateReader.AchievementIds = bundle.AchievementLadders.All.Select(static ladder => ladder.AchievementId).ToArray();
        }
    }
}
