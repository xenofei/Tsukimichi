using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.GamePanels;

/// <summary>A quest one completion opened.</summary>
/// <param name="Quest">The quest.</param>
/// <param name="OtherJob">It opened for another class or job: the current one cannot take it.</param>
public readonly record struct OpenedQuest(QuestRecord Quest, bool OtherJob);

/// <summary>
/// "What this opened" (1.7.0): the quests a completion makes ready, from the reverse prerequisite index (the quests
/// that name the completed one among their previous quests). A dependent counts when the completion is what its
/// previous-quests gate was waiting for (without it the gate stays shut, with it the gate opens) and nothing else
/// stands in the way: every other requirement is met, or only the class or job is wrong (<see cref="OpenedQuest.OtherJob"/>).
/// <para>
/// The answer is the same before and after the state poller applies the completion: the quest-complete window is up
/// while the quest still sits in the journal, so the completed quest is counted as done whatever its evaluation says,
/// and a dependent whose gate already reads met (the poller got there first) is checked without it. A dependent the
/// completion locks out (<see cref="QuestRecord.QuestLocks"/>), one already done or in the journal, an option of an
/// open choice and a removed quest are never listed. Main scenario quests come first, then feature quests, then the
/// rest, each in catalog order. Pure.
/// </para>
/// </summary>
public static class NewlyOpened
{
    public static List<OpenedQuest> By(
        uint completedRowId,
        QuestCatalog catalog,
        ReversePrereqIndex index,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlySet<uint>? featureQuestIds = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(states);

        var opened = new List<OpenedQuest>();
        foreach (var rowId in index.Dependents(completedRowId))
        {
            if (rowId == completedRowId || !catalog.TryGetByRowId(rowId, out var quest) || quest.IsRemoved
                || Array.IndexOf(quest.QuestLocks, completedRowId) >= 0
                || !states.TryGetValue(rowId, out var evaluation) || evaluation.IsSpareAlternative
                || evaluation.State is QuestState.Completed or QuestState.Accepted or QuestState.DoneThisCycle or QuestState.Foreclosed or QuestState.Unknown)
            {
                continue;
            }

            if (Opens(completedRowId, evaluation, out var otherJob))
            {
                opened.Add(new OpenedQuest(quest, otherJob));
            }
        }

        // Stable: List.Sort is not, so the catalog order rides along as the last key.
        var order = new Dictionary<uint, int>(opened.Count);
        for (var i = 0; i < opened.Count; i++)
        {
            order[opened[i].Quest.RowId] = i;
        }

        opened.Sort((a, b) =>
        {
            var byRank = Rank(a.Quest, featureQuestIds).CompareTo(Rank(b.Quest, featureQuestIds));
            return byRank != 0 ? byRank : order[a.Quest.RowId].CompareTo(order[b.Quest.RowId]);
        });
        return opened;
    }

    /// <summary>How many of <paramref name="opened"/> are main scenario quests, feature quests and the rest.</summary>
    public static (int MainScenario, int Feature, int Other) Count(IReadOnlyList<OpenedQuest> opened, IReadOnlySet<uint>? featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(opened);
        int msq = 0, feature = 0, other = 0;
        foreach (var o in opened)
        {
            switch (Rank(o.Quest, featureQuestIds))
            {
                case 0:
                    msq++;
                    break;
                case 1:
                    feature++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        return (msq, feature, other);
    }

    private static int Rank(QuestRecord quest, IReadOnlySet<uint>? featureQuestIds) =>
        FeaturePresets.IsMainScenario(quest) ? 0
        : featureQuestIds is not null && featureQuestIds.Contains(quest.RowId) ? 1
        : 2;

    /// <summary>
    /// Whether the completion of <paramref name="completedRowId"/> is what opens the quest <paramref name="evaluation"/>
    /// describes: some previous-quests gate naming it shuts without it and opens with it, every previous-quests gate
    /// opens with it, and every other requirement is met but for the class or job.
    /// </summary>
    internal static bool Opens(uint completedRowId, QuestEvaluation evaluation, out bool otherJob)
    {
        // Ready on another job: the requirements were read on the current job, whose class, job or level is all that is left.
        var onOtherJob = evaluation.State == QuestState.ReadyOnOtherJob;
        otherJob = onOtherJob;
        var decisive = false;
        foreach (var result in evaluation.Requirements)
        {
            if (result.Req is PreviousQuestsRequirement previous)
            {
                var names = Array.IndexOf(previous.QuestIds, completedRowId) >= 0;
                // Without the done ids (an evaluation written before they were kept) a met gate is read as counting it.
                var counted = names && (previous.DoneIds is { } doneIds ? Array.IndexOf(doneIds, completedRowId) >= 0 : result.Met && previous.DoneCount > 0);
                var without = previous.DoneCount - (counted ? 1 : 0);
                var with = without + (names ? 1 : 0);
                var metWith = Met(previous, with);
                if (!metWith)
                {
                    return false;
                }

                decisive |= names && !Met(previous, without);
                continue;
            }

            if (result.Met)
            {
                continue;
            }

            if (onOtherJob || result.Req.Kind == RequirementKind.ClassJob)
            {
                otherJob = true;
                continue;
            }

            return false;
        }

        return decisive;
    }

    private static bool Met(PreviousQuestsRequirement previous, int done) =>
        previous.Join == JoinKind.Any ? done >= 1 : done >= previous.QuestIds.Length;
}
