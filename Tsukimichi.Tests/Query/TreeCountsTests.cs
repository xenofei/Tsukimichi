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
    public void Counts_per_node_exclude_unlisted_by_default()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: false);

        Assert.Equal(new NodeCount(2, 4), counts.Sections[1]);
        Assert.Equal(new NodeCount(1, 1), counts.Sections[2]);
        Assert.Equal(new NodeCount(2, 3), counts.Categories[10]);
        Assert.Equal(new NodeCount(0, 1), counts.Categories[11]);
        Assert.Equal(new NodeCount(1, 1), counts.Categories[12]);
        Assert.Equal(new NodeCount(1, 2), counts.Genres[100]);
        Assert.Equal(new NodeCount(1, 1), counts.Genres[101]);
        Assert.Equal(new NodeCount(0, 1), counts.Genres[102]);
        Assert.Equal(new NodeCount(1, 1), counts.Genres[103]);
        Assert.False(counts.Genres.ContainsKey(0));
        Assert.False(counts.Sections.ContainsKey(0));
        Assert.Equal(new NodeCount(3, 5), counts.Overall);
    }

    [Fact]
    public void Unlisted_bucket_is_always_counted()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: false);
        Assert.Equal(new NodeCount(1, 2), counts.Unlisted);
    }

    [Fact]
    public void Include_unlisted_adds_them_to_their_section_and_category()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: true);

        Assert.Equal(new NodeCount(2, 2), counts.Sections[2]);
        Assert.Equal(new NodeCount(2, 2), counts.Categories[12]);
        Assert.Equal(new NodeCount(0, 1), counts.Sections[0]);
        Assert.Equal(new NodeCount(0, 1), counts.Categories[0]);
        Assert.Equal(new NodeCount(4, 7), counts.Overall);
        Assert.False(counts.Genres.ContainsKey(0));
    }

    [Fact]
    public void Foreclosed_and_missing_states_are_not_done()
    {
        var counts = TreeCounts.Compute(Catalog, new Dictionary<uint, QuestState>(), includeUnlisted: false);
        Assert.Equal(new NodeCount(0, 5), counts.Overall);
    }

    [Fact]
    public void Fraction_is_safe_on_empty_nodes()
    {
        Assert.Equal(0f, new NodeCount(0, 0).Fraction);
        Assert.Equal(0.5f, new NodeCount(1, 2).Fraction);
    }

    [Fact]
    public void Missing_nodes_read_as_zero()
    {
        var counts = TreeCounts.Compute(Catalog, AllStates, includeUnlisted: false);
        Assert.Equal(default, counts.Section(999));
        Assert.Equal(default, counts.Category(999));
        Assert.Equal(default, counts.Genre(999));
        Assert.Equal(new NodeCount(2, 4), counts.Section(1));
    }
}
