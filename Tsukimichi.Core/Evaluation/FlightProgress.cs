using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>The Flight view's counting rule for quest currents.</summary>
public static class FlightProgress
{
    /// <summary>
    /// Whether a quest current counts as done. The attunement flag decides whenever it can be read (the logged-in
    /// character): set means done, unset means not done, whatever the quest's state. Only when it cannot be read
    /// (<paramref name="attuned"/> is null: a stored character) does the awarding quest's completion in the snapshot stand in.
    /// </summary>
    /// <param name="attuned">The live attunement flag, or null when it cannot be read.</param>
    /// <param name="questState">The viewed character's state of the quest that awards the current, or null when unknown.</param>
    public static bool QuestCurrentDone(bool? attuned, QuestState? questState) =>
        attuned ?? questState == QuestState.Completed;
}
