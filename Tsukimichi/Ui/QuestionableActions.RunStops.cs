using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>The "Why it stopped" card's restarts (feature plan v7, 1.18.0, A2).</summary>
public sealed partial class QuestionableActions
{
    /// <summary>
    /// Starts Questionable on <paramref name="rowId"/> now, with no confirmation, for a fix the player already chose on
    /// a card and that runs later, when its moment comes ("Keep going after it" once the duty is cleared, "Reload
    /// navmesh and retry" once the navmesh is back): the click on the card was the confirmation. One quest when
    /// <paramref name="single"/> and this Questionable can, otherwise it carries on as "Start here and keep going" does.
    /// False, with nothing started, while <see cref="StartQuestBlocker"/> has a reason.
    /// </summary>
    public bool StartChosen(uint rowId, bool single)
    {
        CompanionPlugins.ReadSetupNow();
        if (rowId == 0 || StartQuestBlocker(rowId) is not null)
        {
            return false;
        }

        if (single && ipc.CanStartSingle)
        {
            DoStartSingle(rowId);
        }
        else
        {
            DoStartOnly(rowId);
        }

        return true;
    }
}
