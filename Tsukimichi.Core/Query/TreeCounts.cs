using System.Runtime.InteropServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Progress for one tree node. <see cref="Total"/> leaves out <see cref="QuestState.Foreclosed"/> quests, which the
/// character can never do, so a node whose remainder is foreclosed reads as complete; they are reported in
/// <see cref="Foreclosed"/> instead. <see cref="Fraction"/> feeds the filling moon.
/// </summary>
public readonly record struct NodeCount(int Done, int Total, int Foreclosed)
{
    public NodeCount(int done, int total)
        : this(done, total, 0)
    {
    }

    public float Fraction => Total == 0 ? 0f : (float)Done / Total;
}

/// <summary>
/// Done/total per section, category and genre for the tree labels. Done counts only <see cref="QuestState.Completed"/>.
/// Unlisted quests (genre 0) never enter a section, category or genre node, whatever ids the sheet gave them; they
/// always land in <see cref="Unlisted"/> and join <see cref="Overall"/> only when included.
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

    /// <summary>Counts for the Unlisted virtual node, independent of the include flag.</summary>
    public NodeCount Unlisted { get; }

    /// <summary>Counts across every listed quest, plus Unlisted when included.</summary>
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

    /// <summary>Counts from a plain state map; missing rows read as <see cref="QuestState.Unknown"/>.</summary>
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
            var state = source.StateOf(quest.RowId);
            var done = state == QuestState.Completed ? 1 : 0;
            var foreclosed = state == QuestState.Foreclosed ? 1 : 0;

            if (quest.IsUnlisted)
            {
                unlisted = Add(unlisted, done, foreclosed);
                if (includeUnlisted)
                {
                    overall = Add(overall, done, foreclosed);
                }

                continue;
            }

            Bump(sections, quest.Journal.SectionId, done, foreclosed);
            Bump(categories, quest.Journal.CategoryId, done, foreclosed);
            Bump(genres, quest.Journal.GenreId, done, foreclosed);
            overall = Add(overall, done, foreclosed);
        }

        return new TreeCounts(sections, categories, genres, unlisted, overall);
    }

    private static NodeCount Add(NodeCount count, int done, int foreclosed) =>
        new(count.Done + done, count.Total + 1 - foreclosed, count.Foreclosed + foreclosed);

    private static void Bump(Dictionary<uint, NodeCount> counts, uint key, int done, int foreclosed)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);
        slot = Add(slot, done, foreclosed);
    }
}
