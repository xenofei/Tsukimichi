using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Tests.Evaluation;
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
    public void A_quest_that_does_not_count_in_totals_is_in_no_number_done_or_not()
    {
        // A class intro under its class's genre: the class a character started as never gets it, so it is neither
        // a done nor a total; its neighbours count as usual and the node can reach a full moon without it.
        var catalog = QuestCatalog.Build(
        [
            Quest(65713, "So You Want to Be a Gladiator", section: 6, category: 93, genre: 156) with { CountsInTotals = false },
            Quest(65821, "Way of the Gladiator", section: 6, category: 93, genre: 156),
            Quest(65714, "So You Want to Be a Pugilist", section: 6, category: 93, genre: 157) with { CountsInTotals = false },
            Quest(66068, "Way of the Pugilist", section: 6, category: 93, genre: 157),
        ]);
        var states = States((65713, QuestState.Ready), (65821, QuestState.Completed), (65714, QuestState.Completed), (66068, QuestState.Ready));

        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: true);

        Assert.Equal(new NodeCount(1, 1, 0), counts.Genre(156));
        Assert.Equal(1f, counts.Genre(156).Fraction);
        Assert.Equal(new NodeCount(0, 1, 0), counts.Genre(157));
        Assert.Equal(new NodeCount(1, 2, 0), counts.Category(93));
        Assert.Equal(new NodeCount(1, 2, 0), counts.Section(6));
        Assert.Equal(new NodeCount(1, 2, 0), counts.Overall);
        Assert.Equal(default, counts.Unlisted);
        Assert.Equal(4, catalog.Count);
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
    public void Out_of_season_quest_leaves_the_total_the_way_foreclosed_does()
    {
        // Two seasonal quests: festival 7 is running, festival 8 is not (and the character never saw it, so it is
        // Blocked, not Foreclosed). Only the running one counts, so "Seasonal Events" can reach 100 %.
        var catalog = QuestCatalog.Build(
        [
            Quest(65600, "Running event", festival: 7),
            Quest(65601, "Event not running", festival: 8),
        ]);
        var snapshot = Fixture.Snapshot() with { ActiveFestivals = [7] };
        var evaluations = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

        Assert.Equal(QuestState.Blocked, evaluations[65601].State);
        Assert.True(evaluations[65601].IsOutOfSeason);
        Assert.False(evaluations[65600].IsOutOfSeason);

        var counts = TreeCounts.Compute(catalog, evaluations, includeUnlisted: false);

        Assert.Equal(new NodeCount(0, 1, 1), counts.Overall);
        Assert.Equal(new NodeCount(0, 1, 1), counts.Genres[100]);
    }

    [Fact]
    public void Out_of_season_remainder_lets_a_node_reach_100_percent()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(65600, "Done last year", festival: 8),
            Quest(65601, "Not running", festival: 9),
        ]);
        var snapshot = Fixture.Snapshot(65600);
        var evaluations = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

        var counts = TreeCounts.Compute(catalog, evaluations, includeUnlisted: false);

        Assert.Equal(new NodeCount(1, 1, 1), counts.Overall);
        Assert.Equal(1f, counts.Overall.Fraction);
    }

    [Fact]
    public void Blocked_for_another_reason_still_counts()
    {
        // The exclusion is the seasonal blocker only, never Blocked in general.
        var catalog = QuestCatalog.Build([Quest(65600, "Needs level 90", level: 90)]);
        var evaluations = StateResolver.ResolveAll(catalog, Fixture.Snapshot(), EvalContext.Default);

        Assert.Equal(QuestState.Blocked, evaluations[65600].State);
        Assert.False(evaluations[65600].LeavesTotals);
        Assert.Equal(new NodeCount(0, 1, 0), TreeCounts.Compute(catalog, evaluations, includeUnlisted: false).Overall);
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
    public void Ready_quests_are_counted_per_node_for_the_tree_badges()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "A", section: 1, category: 10, genre: 100),
            Quest(2, "B", section: 1, category: 10, genre: 100),
            Quest(3, "C", section: 1, category: 10, genre: 101),
            Quest(4, "D", section: 1, category: 11, genre: 102),
            Quest(5, "E", section: 2, category: 12, genre: 103),
            Quest(6, "Removed", section: 2, category: 12, genre: 0),
            Quest(65713, "Class intro", section: 2, category: 12, genre: 103) with { CountsInTotals = false },
        ]);
        var states = States(
            (1, QuestState.Ready),
            (2, QuestState.Ready),
            (3, QuestState.ReadyOnOtherJob),
            (4, QuestState.Ready),
            (5, QuestState.Completed),
            (6, QuestState.Ready),
            (65713, QuestState.Ready));

        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: true);

        Assert.Equal(3, counts.OverallReady);
        Assert.Equal(3, counts.SectionReady(1));
        Assert.Equal(0, counts.SectionReady(2));        // removed and uncounted quests never badge
        Assert.Equal(2, counts.CategoryReady(10));
        Assert.Equal(1, counts.CategoryReady(11));
        Assert.Equal(2, counts.GenreReady(100));
        Assert.Equal(0, counts.GenreReady(101));        // Ready on another job is not Ready
        Assert.Equal(0, counts.GenreReady(999));

        // The progress numbers are untouched by the Ready tally.
        Assert.Equal(new NodeCount(0, 4, 0), counts.Section(1));
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
