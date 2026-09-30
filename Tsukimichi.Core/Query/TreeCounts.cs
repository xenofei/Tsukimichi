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

    /// <summary>
    /// How many of <see cref="Excluded"/> lie on a path the character did not take (<see cref="QuestEvaluation.IsOtherPath"/>);
    /// the tree's tooltip names them ("53 on other paths: another city's start 49, …", <see cref="TreeCounts.OtherPathsIn"/>).
    /// </summary>
    public int OtherPaths { get; init; }
}

/// <summary>
/// Done/total per section, category and genre for the tree labels. Done counts <see cref="QuestState.Completed"/>, and an
/// allied society daily the character has completed at least once (<see cref="QuestEvaluation.CountsAsDone"/>).
/// Removed quests (<see cref="QuestRecord.IsRemoved"/>: retired rows and the genre-0 leftovers) never enter a
/// section, category or genre node, whatever ids the sheet gave them; they always land in <see cref="Unlisted"/> and
/// join <see cref="Overall"/> only when included. That bucket counts every row, exclusions aside: a removed quest
/// evaluates Locked out, and "118 of 179 done before they went" is the number the bucket is for. A listed quest with
/// <see cref="QuestRecord.CountsInTotals"/> false (the class intros, the hidden progress trackers) is in no count at all,
/// done or not, and neither is a repeatable other than an allied society daily (<see cref="QuestRecord.EntersCounts"/>:
/// a weekly, a relic or seasonal repeatable, Primal Focus), which a finished section would otherwise never close.
///
/// Alongside the progress, each node also carries how many of its counted quests are <see cref="QuestState.Ready"/>
/// (<see cref="SectionReady"/>, <see cref="CategoryReady"/>, <see cref="GenreReady"/>, <see cref="OverallReady"/>): the
/// tree's Ready badge and the Journal tab's badge (T11). Removed quests are never Ready and are not counted, and neither
/// is a daily already counted as done, nor a spare alternative of a choice not made yet (it counts once, through the
/// option presumed).
///
/// Quests on a path the character did not take leave the totals like any Locked-out quest; each node also tallies
/// them by kind (<see cref="OtherPathsIn"/>), and <see cref="OtherPathsNode"/> tallies every listed one for the
/// "Other paths" virtual node, class intros included.
/// </summary>
public sealed class TreeCounts
{
    private TreeCounts(
        Dictionary<uint, NodeCount> sections,
        Dictionary<uint, NodeCount> categories,
        Dictionary<uint, NodeCount> genres,
        NodeCount unlisted,
        NodeCount overall,
        ReadyCounts ready,
        PathTallies paths)
    {
        this.paths = paths;
        Sections = sections;
        Categories = categories;
        Genres = genres;
        Unlisted = unlisted;
        Overall = overall;
        this.ready = ready;
    }

    private readonly ReadyCounts ready;
    private readonly PathTallies paths;

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

    /// <summary>Ready quests across every listed, counted quest.</summary>
    public int OverallReady => ready.Overall;

    public int SectionReady(uint id) => ready.Sections.GetValueOrDefault(id);

    public int CategoryReady(uint id) => ready.Categories.GetValueOrDefault(id);

    public int GenreReady(uint id) => ready.Genres.GetValueOrDefault(id);

    /// <summary>Every listed quest on another path, by kind: the "Other paths" node's numbers.</summary>
    public PathTally OtherPathsNode => paths.Node;

    /// <summary>
    /// The counted quests on another path under a tree node, by kind: a section, category or genre, the whole
    /// journal for <see cref="QuestScope.None"/>, and <see cref="OtherPathsNode"/> for the Other paths node.
    /// </summary>
    public PathTally OtherPathsIn(QuestScope scope) => scope.Kind switch
    {
        ScopeKind.None => paths.Overall,
        ScopeKind.Section => paths.Sections.GetValueOrDefault(scope.Id),
        ScopeKind.Category => paths.Categories.GetValueOrDefault(scope.Id),
        ScopeKind.Genre => paths.Genres.GetValueOrDefault(scope.Id),
        ScopeKind.VirtualOtherPaths => paths.Node,
        _ => default,
    };

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
        var ready = new ReadyCounts();
        var paths = new PathTallies();

        foreach (var quest in catalog.All)
        {
            var state = source.StateOf(quest.RowId);
            var done = source.CountsAsDone(quest.RowId) ? 1 : 0;

            if (quest.IsRemoved)
            {
                unlisted = Add(unlisted, done, 0);
                if (includeUnlisted)
                {
                    overall = Add(overall, done, 0);
                }

                continue;
            }

            var otherPath = source.OtherPathKind(quest.RowId);
            if (otherPath is { } nodeKind)
            {
                paths.Node = paths.Node.Add(nodeKind);
            }

            if (!quest.EntersCounts)
            {
                // A class intro, a progress tracker or a repeatable other than an allied society daily: listed under
                // its genre, never in its numbers (done or total).
                continue;
            }

            var excluded = source.LeavesTotals(quest.RowId) ? 1 : 0;
            var other = otherPath is null ? 0 : 1;

            Bump(sections, quest.Journal.SectionId, done, excluded, other);
            Bump(categories, quest.Journal.CategoryId, done, excluded, other);
            Bump(genres, quest.Journal.GenreId, done, excluded, other);
            overall = Add(overall, done, excluded, other);
            if (otherPath is { } kind)
            {
                paths.Add(quest.Journal, kind);
            }

            // A spare alternative (an option of a choice not made yet, not the one presumed) is out of the totals,
            // so it does not light the Ready badge either.
            if (state == QuestState.Ready && done == 0 && excluded == 0)
            {
                ready.Add(quest.Journal);
            }
        }

        return new TreeCounts(sections, categories, genres, unlisted, overall, ready, paths);
    }

    /// <summary>Other-path tallies per node; only nodes with at least one such quest get an entry.</summary>
    private sealed class PathTallies
    {
        public Dictionary<uint, PathTally> Sections { get; } = new();
        public Dictionary<uint, PathTally> Categories { get; } = new();
        public Dictionary<uint, PathTally> Genres { get; } = new();
        public PathTally Overall { get; private set; }
        public PathTally Node { get; set; }

        public void Add(JournalRef journal, PathKind kind)
        {
            ref var section = ref CollectionsMarshal.GetValueRefOrAddDefault(Sections, journal.SectionId, out _);
            section = section.Add(kind);
            ref var category = ref CollectionsMarshal.GetValueRefOrAddDefault(Categories, journal.CategoryId, out _);
            category = category.Add(kind);
            ref var genre = ref CollectionsMarshal.GetValueRefOrAddDefault(Genres, journal.GenreId, out _);
            genre = genre.Add(kind);
            Overall = Overall.Add(kind);
        }
    }

    /// <summary>Ready tallies per node; only nodes with at least one Ready quest get an entry.</summary>
    private sealed class ReadyCounts
    {
        public Dictionary<uint, int> Sections { get; } = new();
        public Dictionary<uint, int> Categories { get; } = new();
        public Dictionary<uint, int> Genres { get; } = new();
        public int Overall { get; private set; }

        public void Add(JournalRef journal)
        {
            CollectionsMarshal.GetValueRefOrAddDefault(Sections, journal.SectionId, out _)++;
            CollectionsMarshal.GetValueRefOrAddDefault(Categories, journal.CategoryId, out _)++;
            CollectionsMarshal.GetValueRefOrAddDefault(Genres, journal.GenreId, out _)++;
            Overall++;
        }
    }

    private static NodeCount Add(NodeCount count, int done, int excluded, int otherPaths = 0) =>
        new(count.Done + done, count.Total + 1 - excluded, count.Excluded + excluded) { OtherPaths = count.OtherPaths + otherPaths };

    private static void Bump(Dictionary<uint, NodeCount> counts, uint key, int done, int excluded, int otherPaths)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);
        slot = Add(slot, done, excluded, otherPaths);
    }
}
