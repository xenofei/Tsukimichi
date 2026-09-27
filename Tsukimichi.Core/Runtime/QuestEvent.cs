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

    /// <summary>The quest left the journal without being completed.</summary>
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
    /// comes from evaluations whose state moved into Ready or ReadyOnOtherJob. Rows that kept the same
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
            if (!catalog.TryGet(questId, out var quest))
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
            else if (wasAccepted && !isAccepted && !completedNow && !@new.IsCompleted(questId))
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

    private static bool IsAvailable(QuestState state) => state is QuestState.Ready or QuestState.ReadyOnOtherJob;
}
