using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>Where a character stands in the main scenario.</summary>
/// <param name="Next">The first main scenario quest, in journal order, that is not completed; null once every one is.</param>
/// <param name="State">Resolved state of <paramref name="Next"/>; <see cref="QuestState.Completed"/> when there is no next quest.</param>
/// <param name="Done">Completed main scenario quests.</param>
/// <param name="Total">Main scenario quests the character can still do or has done; foreclosed branches (the other two Grand Company choices, say) are left out.</param>
public sealed record MsqPosition(QuestRecord? Next, QuestState State, int Done, int Total)
{
    public bool IsComplete => Next is null;
}

/// <summary>
/// Finds the main scenario position by walking sections 0 (A Realm Reborn through Endwalker) and 1 (Dawntrail) in
/// journal order and stopping at the first quest that is neither completed nor foreclosed. The journal's own
/// "hide" state plays no part, so a new player who lost the MSQ under the five-quest display finds it here.
/// Pure; the caller memoizes per session version.
/// </summary>
public static class MsqProgress
{
    /// <summary>JournalSection ids holding main scenario quests, in play order.</summary>
    public static readonly IReadOnlyList<uint> MainScenarioSections = [0, 1];

    /// <summary>Position from evaluator output. Returns null when the catalog has no main scenario quests at all.</summary>
    public static MsqPosition? Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        return Compute(catalog, new EvaluationSource(evaluations));
    }

    /// <summary>Position from a plain state map; missing rows read as <see cref="QuestState.Unknown"/>, which counts as not done.</summary>
    public static MsqPosition? Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        return Compute(catalog, new StateMapSource(states));
    }

    private static MsqPosition? Compute<TSource>(QuestCatalog catalog, TSource source)
        where TSource : struct, IStateSource
    {
        ArgumentNullException.ThrowIfNull(catalog);

        QuestRecord? next = null;
        var nextState = QuestState.Completed;
        var done = 0;
        var total = 0;
        var any = false;
        foreach (var section in MainScenarioSections)
        {
            if (catalog.BySection.GetValueOrDefault(section) is not { } quests)
            {
                continue;
            }

            foreach (var quest in quests)
            {
                if (quest.IsUnlisted)
                {
                    continue;
                }

                any = true;
                var state = source.StateOf(quest.RowId);
                switch (state)
                {
                    case QuestState.Completed:
                        done++;
                        total++;
                        break;
                    case QuestState.Foreclosed:
                        // A branch the character did not take; neither pending nor countable.
                        break;
                    default:
                        total++;
                        if (next is null)
                        {
                            next = quest;
                            nextState = state;
                        }

                        break;
                }
            }
        }

        return any ? new MsqPosition(next, nextState, done, total) : null;
    }
}
