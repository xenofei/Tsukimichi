using System.Runtime.InteropServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Progress for one tree node. <see cref="Total"/> leaves out the quests that <see cref="QuestEvaluation.LeavesTotals"/>
/// names: <see cref="QuestState.Foreclosed"/> ones, which the character can never do, and out-of-season ones, which
/// they cannot do until the event returns. A node whose remainder is all of these reads as complete; they are
/// reported in <see cref="Excluded"/> instead. <see cref="Fraction"/> feeds the filling moon.
/// </summary>
public readonly record struct NodeCount(int Done, int Total, int Excluded)
{
    public NodeCount(int done, int total)
        : this(done, total, 0)
    {
    }

    public float Fraction => Total == 0 ? 0f : (float)Done / Total;
}

/// <summary>
/// Done/total per section, category and genre for the tree labels. Done counts only <see cref="QuestState.Completed"/>.
/// Removed quests (<see cref="QuestRecord.IsRemoved"/>: retired rows and the genre-0 leftovers) never enter a
/// section, category or genre node, whatever ids the sheet gave them; they always land in <see cref="Unlisted"/> and
/// join <see cref="Overall"/> only when included. That bucket counts every row, exclusions aside: a removed quest
/// evaluates Locked out, and "118 of 179 done before they went" is the number the bucket is for. A listed quest with
/// <see cref="QuestRecord.CountsInTotals"/> false (the class intros) is in no count at all, done or not.
/// </summary>
public sealed class TreeCounts
{
    private TreeCounts(
        Dictionary<uint, NodeCount> sections,
        Dictionary<uint, NodeCount> categories,
        Dictionary<uint, NodeCount> genres,
        NodeCount unlisted,
        NodeCount overall)
    {
        Sections = sections;
        Categories = categories;
        Genres = genres;
        Unlisted = unlisted;
        Overall = overall;
    }

    public IReadOnlyDictionary<uint, NodeCount> Sections { get; }
    public IReadOnlyDictionary<uint, NodeCount> Categories { get; }
    public IReadOnlyDictionary<uint, NodeCount> Genres { get; }

    /// <summary>Counts for the "Removed from the game" virtual node, independent of the include flag.</summary>
    public NodeCount Unlisted { get; }

    /// <summary>Counts across every listed quest, plus <see cref="Unlisted"/> when included.</summary>
    public NodeCount Overall { get; }

    public NodeCount Section(uint id) => Sections.GetValueOrDefault(id);

    public NodeCount Category(uint id) => Categories.GetValueOrDefault(id);

    public NodeCount Genre(uint id) => Genres.GetValueOrDefault(id);

    /// <summary>Counts from evaluator output; each quest's state is read from its <see cref="QuestEvaluation"/>.</summary>
    public static TreeCounts Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> evaluations, bool includeUnlisted)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        return Compute(catalog, new EvaluationSource(evaluations), includeUnlisted);
    }

    /// <summary>Counts from a plain state map; missing rows read as <see cref="QuestState.Unknown"/>. Without requirements only Foreclosed leaves the totals.</summary>
    public static TreeCounts Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states, bool includeUnlisted)
    {
        ArgumentNullException.ThrowIfNull(states);
        return Compute(catalog, new StateMapSource(states), includeUnlisted);
    }

    private static TreeCounts Compute<TSource>(QuestCatalog catalog, TSource source, bool includeUnlisted)
        where TSource : struct, IStateSource
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var sections = new Dictionary<uint, NodeCount>(catalog.BySection.Count);
        var categories = new Dictionary<uint, NodeCount>(catalog.ByCategory.Count);
        var genres = new Dictionary<uint, NodeCount>(catalog.ByGenre.Count);
        var unlisted = default(NodeCount);
        var overall = default(NodeCount);

        foreach (var quest in catalog.All)
        {
            var done = source.StateOf(quest.RowId) == QuestState.Completed ? 1 : 0;

            if (quest.IsRemoved)
            {
                unlisted = Add(unlisted, done, 0);
                if (includeUnlisted)
                {
                    overall = Add(overall, done, 0);
                }

                continue;
            }

            if (!quest.CountsInTotals)
            {
                // A class intro: listed under its genre, never in its numbers (done or total).
                continue;
            }

            var excluded = source.LeavesTotals(quest.RowId) ? 1 : 0;

            Bump(sections, quest.Journal.SectionId, done, excluded);
            Bump(categories, quest.Journal.CategoryId, done, excluded);
            Bump(genres, quest.Journal.GenreId, done, excluded);
            overall = Add(overall, done, excluded);
        }

        return new TreeCounts(sections, categories, genres, unlisted, overall);
    }

    private static NodeCount Add(NodeCount count, int done, int excluded) =>
        new(count.Done + done, count.Total + 1 - excluded, count.Excluded + excluded);

    private static void Bump(Dictionary<uint, NodeCount> counts, uint key, int done, int excluded)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);
        slot = Add(slot, done, excluded);
    }
}
