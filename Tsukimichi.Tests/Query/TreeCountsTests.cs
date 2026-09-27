using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class TreeCountsTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "A", section: 1, category: 10, genre: 100),
        Quest(2, "B", section: 1, category: 10, genre: 100),
        Quest(3, "C", section: 1, category: 10, genre: 101),
        Quest(4, "D", section: 1, category: 11, genre: 102),
        Quest(5, "E", section: 2, category: 12, genre: 103),
        Quest(6, "F unlisted", section: 2, category: 12, genre: 0),
        Quest(7, "G unlisted no section", section: 0, category: 0, genre: 0),
    ]);

    private static readonly Dictionary<uint, QuestState> AllStates = States(
        (1, QuestState.Completed),
        (2, QuestState.Ready),
        (3, QuestState.Completed),
        (4, QuestState.Foreclosed),
        (5, QuestState.Completed),
        (6, QuestState.Completed),
        (7, QuestState.Ready));

    [Fact]
    public void Counts_per_node_exclude_unlisted_and_foreclosed_from_totals()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: false);

        Assert.Equal(new NodeCount(2, 3, 1), counts.Sections[1]);
        Assert.Equal(new NodeCount(1, 1, 0), counts.Sections[2]);
        Assert.Equal(new NodeCount(2, 3, 0), counts.Categories[10]);
        Assert.Equal(new NodeCount(0, 0, 1), counts.Categories[11]);
        Assert.Equal(new NodeCount(1, 1, 0), counts.Categories[12]);
        Assert.Equal(new NodeCount(1, 2, 0), counts.Genres[100]);
        Assert.Equal(new NodeCount(1, 1, 0), counts.Genres[101]);
        Assert.Equal(new NodeCount(0, 0, 1), counts.Genres[102]);
        Assert.Equal(new NodeCount(1, 1, 0), counts.Genres[103]);
        Assert.False(counts.Genres.ContainsKey(0));
        Assert.False(counts.Sections.ContainsKey(0));
        Assert.False(counts.Categories.ContainsKey(0));
        Assert.Equal(new NodeCount(3, 4, 1), counts.Overall);
    }

    [Fact]
    public void Foreclosed_only_remainder_lets_a_node_reach_100_percent()
    {
        // An MSQ category whose remaining quests are the other Grand Companies' choices reads as complete.
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Chosen", category: 10),
            Quest(2, "Maelstrom path", category: 10),
            Quest(3, "Adder path", category: 10),
        ]);
        var states = States((1, QuestState.Completed), (2, QuestState.Foreclosed), (3, QuestState.Foreclosed));

        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: false);

        Assert.Equal(new NodeCount(1, 1, 2), counts.Categories[10]);
        Assert.Equal(1f, counts.Categories[10].Fraction);
    }

    [Fact]
    public void Unlisted_bucket_is_always_counted()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: false);
        Assert.Equal(new NodeCount(1, 2, 0), counts.Unlisted);
    }

    [Fact]
    public void Include_unlisted_adds_them_to_overall_only()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: true);

        // Section 0 is the real MSQ section; unlisted quests must never land in a section, category or genre node.
        Assert.Equal(new NodeCount(1, 1, 0), counts.Sections[2]);
        Assert.Equal(new NodeCount(1, 1, 0), counts.Categories[12]);
        Assert.False(counts.Sections.ContainsKey(0));
        Assert.False(counts.Categories.ContainsKey(0));
        Assert.False(counts.Genres.ContainsKey(0));
        Assert.Equal(new NodeCount(4, 6, 1), counts.Overall);
        Assert.Equal(new NodeCount(1, 2, 0), counts.Unlisted);
    }

    [Fact]
    public void Missing_states_are_neither_done_nor_foreclosed()
    {
        var counts = TreeCounts.Compute(Catalog, new Dictionary<uint, QuestState>(), includeUnlisted: false);
        Assert.Equal(new NodeCount(0, 5, 0), counts.Overall);
    }

    [Fact]
    public void Evaluation_overload_reads_state_from_each_evaluation()
    {
        var evaluations = Evaluations(AllStates);

        var fromEvaluations = TreeCounts.Compute(Catalog, evaluations, includeUnlisted: true);
        var fromStates = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: true);

        Assert.Equal(fromStates.Overall, fromEvaluations.Overall);
        Assert.Equal(fromStates.Unlisted, fromEvaluations.Unlisted);
        Assert.Equal(fromStates.Sections, fromEvaluations.Sections);
        Assert.Equal(fromStates.Categories, fromEvaluations.Categories);
        Assert.Equal(fromStates.Genres, fromEvaluations.Genres);
    }

    [Fact]
    public void Fraction_is_safe_on_empty_nodes()
    {
        Assert.Equal(0f, new NodeCount(0, 0).Fraction);
        Assert.Equal(0.5f, new NodeCount(1, 2).Fraction);
        Assert.Equal(new NodeCount(1, 2, 0), new NodeCount(1, 2));
    }

    [Fact]
    public void Missing_nodes_read_as_zero()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: false);
        Assert.Equal(default, counts.Section(999));
        Assert.Equal(default, counts.Category(999));
        Assert.Equal(default, counts.Genre(999));
        Assert.Equal(new NodeCount(2, 3, 1), counts.Section(1));
    }
}
