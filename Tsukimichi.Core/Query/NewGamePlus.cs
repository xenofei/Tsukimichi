using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>What New Game+ says about one quest (<see cref="NewGamePlus.Of"/>).</summary>
public enum ReplayKind : byte
{
    /// <summary>Nothing to say: the New Game+ data is not loaded, or the quest is a repeatable or removed from the game.</summary>
    None,

    /// <summary>One of the game's New Game+ chapters lists the quest: once done, it can be played again there.</summary>
    Replayable,

    /// <summary>No New Game+ chapter lists the quest: its story plays once.</summary>
    OnceOnly,
}

/// <summary>
/// The New Game+ replay badge (feature plan v5, collector extras; R9 F7). The game's New Game+ chapters (the
/// <c>QuestRedo</c> sheet: each row one chapter part, up to 32 quests; <c>QuestRedoChapterUI</c> names the chapters)
/// list every quest the player can replay: the main scenario but the starting classes' "Close to Home", the Chronicles
/// of a New Era, Hildibrand, the Scholasticate, Tales of the Dragonsong War, the class, job and role quests and the
/// crafter and gatherer quests. Everything else plays once. GameData reads the set; this class only interprets it.
/// </summary>
public static class NewGamePlus
{
    /// <summary>
    /// <see cref="ReplayKind.Replayable"/> when <paramref name="replayable"/> lists the quest; otherwise
    /// <see cref="ReplayKind.OnceOnly"/>, except for a repeatable or a quest removed from the game (nothing to replay)
    /// and when the set is empty (the sheet was not read), which say <see cref="ReplayKind.None"/>.
    /// </summary>
    public static ReplayKind Of(QuestRecord quest, IReadOnlySet<uint> replayable)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(replayable);
        if (replayable.Count == 0)
        {
            return ReplayKind.None;
        }

        if (replayable.Contains(quest.RowId))
        {
            return ReplayKind.Replayable;
        }

        return quest.IsRepeatable || quest.IsRemoved ? ReplayKind.None : ReplayKind.OnceOnly;
    }

    /// <summary>
    /// Whether the quest is a story quest that plays once and the character has not done: <see cref="ReplayKind.OnceOnly"/>,
    /// a step of a story (a chain of <paramref name="chains"/>, or a side story of <paramref name="stories"/>), not
    /// completed, and not out of the totals (<paramref name="leavesTotals"/>: locked out, out of season, or a spare
    /// alternative of a choice not made yet, which the choice's other option stands for). The "Once-only story quests
    /// I haven't done" filter keeps exactly these.
    /// </summary>
    /// <param name="leavesTotals">The quest's <see cref="Evaluation.QuestEvaluation.LeavesTotals"/>.</param>
    public static bool IsOnceOnlyStoryLeft(
        QuestRecord quest,
        QuestState state,
        bool leavesTotals,
        IReadOnlySet<uint> replayable,
        ChainCatalog? chains,
        StorySidequests? stories)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (leavesTotals || state is QuestState.Completed or QuestState.Foreclosed || Of(quest, replayable) != ReplayKind.OnceOnly)
        {
            return false;
        }

        return chains?.ForQuest(quest.RowId) is not null || stories?.Contains(quest.RowId) == true;
    }
}
