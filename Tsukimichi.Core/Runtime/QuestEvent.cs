using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>Something that happened to one quest between two polls.</summary>
public enum QuestEventKind
{
    /// <summary>The completion bit was set.</summary>
    Completed,

    /// <summary>The quest entered the journal.</summary>
    Accepted,

    /// <summary>
    /// The quest left the journal without being completed. Not raised for a seasonal quest whose event is no longer
    /// running: the game clears those from the journal when the event ends, which is not the player's doing.
    /// </summary>
    Abandoned,

    /// <summary>The quest moved into Ready or ReadyOnOtherJob from any other state.</summary>
    NewlyAvailable,
}

/// <summary>One session-scoped notice about a quest, identified by its catalog row id.</summary>
public sealed record QuestEvent(QuestEventKind Kind, uint RowId, DateTime TimeUtc);

/// <summary>Derives <see cref="QuestEvent"/>s from a diff and the before/after evaluations. Pure.</summary>
public static class QuestEvents
{
    /// <summary>
    /// Completed, Accepted and Abandoned come from <paramref name="diff"/> for quests the catalog knows; NewlyAvailable
    /// comes from evaluations whose state moved into Ready or ReadyOnOtherJob. A seasonal quest that left the journal
    /// while its event is not running (<see cref="LeftWithItsFestival"/>) was removed by the game at the event's end,
    /// so it yields no Abandoned event: no ledger entry, no chat line. Rows that kept the same
    /// <see cref="QuestEvaluation"/> instance are skipped without comparison, so an incremental resolve stays cheap.
    /// </summary>
    public static List<QuestEvent> Derive(
        SnapshotDiff diff,
        CharacterSnapshot old,
        CharacterSnapshot @new,
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> oldStates,
        IReadOnlyDictionary<uint, QuestEvaluation> newStates,
        DateTime timeUtc)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(@new);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(oldStates);
        ArgumentNullException.ThrowIfNull(newStates);

        var events = new List<QuestEvent>();
        var oldAccepted = new HashSet<ushort>(old.Accepted.Count);
        foreach (var q in old.Accepted)
        {
            oldAccepted.Add(q.QuestId);
        }

        var newAccepted = new HashSet<ushort>(@new.Accepted.Count);
        foreach (var q in @new.Accepted)
        {
            newAccepted.Add(q.QuestId);
        }

        foreach (var questId in diff.ChangedQuestIds)
        {
            if (!catalog.TryGetByQuestId(questId, out var quest))
            {
                continue;
            }

            var completedNow = !old.IsCompleted(questId) && @new.IsCompleted(questId);
            if (completedNow)
            {
                events.Add(new QuestEvent(QuestEventKind.Completed, quest.RowId, timeUtc));
            }

            var wasAccepted = oldAccepted.Contains(questId);
            var isAccepted = newAccepted.Contains(questId);
            if (!wasAccepted && isAccepted)
            {
                events.Add(new QuestEvent(QuestEventKind.Accepted, quest.RowId, timeUtc));
            }
            else if (wasAccepted && !isAccepted && !completedNow && !@new.IsCompleted(questId) && !LeftWithItsFestival(quest, @new, newStates))
            {
                events.Add(new QuestEvent(QuestEventKind.Abandoned, quest.RowId, timeUtc));
            }
        }

        var newlyAvailable = new List<uint>();
        foreach (var (rowId, eval) in newStates)
        {
            if (!IsAvailable(eval.State))
            {
                continue;
            }

            if (!oldStates.TryGetValue(rowId, out var previous) || ReferenceEquals(previous, eval) || IsAvailable(previous.State))
            {
                continue;
            }

            newlyAvailable.Add(rowId);
        }

        newlyAvailable.Sort();
        foreach (var rowId in newlyAvailable)
        {
            events.Add(new QuestEvent(QuestEventKind.NewlyAvailable, rowId, timeUtc));
        }

        return events;
    }

    /// <summary>
    /// Whether <paramref name="quest"/> is seasonal and its event is not running in <paramref name="new"/>: the
    /// evaluator's seasonal requirement says so (inactive), or, without an evaluation for the row, the snapshot's
    /// running festivals do not list it.
    /// </summary>
    internal static bool LeftWithItsFestival(QuestRecord quest, CharacterSnapshot @new, IReadOnlyDictionary<uint, QuestEvaluation> newStates)
    {
        if (quest.Festival == 0)
        {
            return false;
        }

        if (newStates.TryGetValue(quest.RowId, out var evaluation))
        {
            foreach (var result in evaluation.Requirements)
            {
                if (result.Req is SeasonalRequirement seasonal)
                {
                    return !seasonal.Active;
                }
            }
        }

        return !@new.ActiveFestivals.Contains(quest.Festival);
    }

    private static bool IsAvailable(QuestState state) => state is QuestState.Ready or QuestState.ReadyOnOtherJob;
}
