using System.Runtime.InteropServices;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>Completed count over total for one tree node; <see cref="Fraction"/> feeds the filling moon.</summary>
public readonly record struct NodeCount(int Done, int Total)
{
    public float Fraction => Total == 0 ? 0f : (float)Done / Total;
}

/// <summary>
/// Done/total per section, category and genre for the tree labels. Done counts only <see cref="QuestState.Completed"/>.
/// Unlisted quests (genre 0) are left out of every node and the overall total unless included; they always appear in
/// <see cref="Unlisted"/> and never get a genre entry.
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

    public static TreeCounts Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states, bool includeUnlisted)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);

        var sections = new Dictionary<uint, NodeCount>(catalog.BySection.Count);
        var categories = new Dictionary<uint, NodeCount>(catalog.ByCategory.Count);
        var genres = new Dictionary<uint, NodeCount>(catalog.ByGenre.Count);
        var unlisted = default(NodeCount);
        var overall = default(NodeCount);

        foreach (var quest in catalog.All)
        {
            var done = states.GetValueOrDefault(quest.RowId, QuestState.Unknown) == QuestState.Completed ? 1 : 0;

            if (quest.IsUnlisted)
            {
                unlisted = Add(unlisted, done);
                if (!includeUnlisted)
                {
                    continue;
                }
            }
            else
            {
                Bump(genres, quest.Journal.GenreId, done);
            }

            Bump(sections, quest.Journal.SectionId, done);
            Bump(categories, quest.Journal.CategoryId, done);
            overall = Add(overall, done);
        }

        return new TreeCounts(sections, categories, genres, unlisted, overall);
    }

    private static NodeCount Add(NodeCount count, int done) => new(count.Done + done, count.Total + 1);

    private static void Bump(Dictionary<uint, NodeCount> counts, uint key, int done)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);
        slot = Add(slot, done);
    }
}
