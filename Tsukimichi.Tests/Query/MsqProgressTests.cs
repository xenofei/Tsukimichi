using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class MsqProgressTests
{
    // Section 0 (ARR through EW) then section 1 (DT), each in journal order; 20 is a side quest, 99 an unlisted row in section 0.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Coming to Gridania", section: 0, category: 1, genre: 1, sortKey: 10, expansion: 0),
        Quest(2, "Close to Home", section: 0, category: 1, genre: 1, sortKey: 20, expansion: 0),
        Quest(3, "The Company You Keep (Maelstrom)", section: 0, category: 1, genre: 1, sortKey: 30, expansion: 0),
        Quest(4, "The Company You Keep (Twin Adder)", section: 0, category: 1, genre: 1, sortKey: 31, expansion: 0),
        Quest(5, "Coming to Ishgard", section: 0, category: 2, genre: 2, sortKey: 40, expansion: 1),
        Quest(6, "The Vows of Virtue", section: 1, category: 3, genre: 3, sortKey: 50, expansion: 5),
        Quest(20, "Side quest", section: 2, category: 10, genre: 100, sortKey: 5),
        Quest(99, "Removed", section: 0, category: 0, genre: 0, sortKey: 1),
    ]);

    [Fact]
    public void First_quest_not_completed_in_journal_order_is_the_position()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Completed), (3, QuestState.Completed), (4, QuestState.Foreclosed), (5, QuestState.Ready), (6, QuestState.Blocked), (20, QuestState.Ready), (99, QuestState.Ready));

        var position = MsqProgress.Compute(Catalog, states);

        Assert.NotNull(position);
        Assert.Equal(5u, position.Next?.RowId);
        Assert.Equal(QuestState.Ready, position.State);
        Assert.Equal(3, position.Done);
        Assert.Equal(5, position.Total);
        Assert.False(position.IsComplete);
    }

    [Fact]
    public void A_blocked_quest_earlier_in_the_journal_wins_over_a_ready_one_later()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Blocked), (3, QuestState.Ready), (4, QuestState.Ready), (5, QuestState.Ready), (6, QuestState.Ready));

        var position = MsqProgress.Compute(Catalog, states);

        Assert.Equal(2u, position?.Next?.RowId);
        Assert.Equal(QuestState.Blocked, position?.State);
    }

    [Fact]
    public void Section_zero_is_walked_before_section_one_whatever_the_sort_keys_say()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(6, "Dawntrail", section: 1, category: 3, genre: 3, sortKey: 1),
            Quest(5, "Endwalker", section: 0, category: 2, genre: 2, sortKey: 2),
        ]);

        var position = MsqProgress.Compute(catalog, States(catalog, QuestState.Ready));

        Assert.Equal(5u, position?.Next?.RowId);
    }

    [Fact]
    public void Accepted_quest_is_the_position_with_its_state()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Accepted), (3, QuestState.Blocked), (4, QuestState.Blocked), (5, QuestState.Blocked), (6, QuestState.Blocked));

        var position = MsqProgress.Compute(Catalog, states);

        Assert.Equal(2u, position?.Next?.RowId);
        Assert.Equal(QuestState.Accepted, position?.State);
    }

    [Fact]
    public void Everything_completed_reports_complete_with_the_counts()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Completed), (3, QuestState.Completed), (4, QuestState.Foreclosed), (5, QuestState.Completed), (6, QuestState.Completed));

        var position = MsqProgress.Compute(Catalog, states);

        Assert.NotNull(position);
        Assert.True(position.IsComplete);
        Assert.Null(position.Next);
        Assert.Equal(QuestState.Completed, position.State);
        Assert.Equal(5, position.Done);
        Assert.Equal(5, position.Total);
    }

    [Fact]
    public void Missing_states_read_as_unknown_and_point_at_the_first_quest()
    {
        var position = MsqProgress.Compute(Catalog, new Dictionary<uint, QuestState>());

        Assert.Equal(1u, position?.Next?.RowId);
        Assert.Equal(QuestState.Unknown, position?.State);
        Assert.Equal(0, position?.Done);
        Assert.Equal(6, position?.Total);
    }

    [Fact]
    public void Unlisted_rows_in_section_zero_and_side_quests_are_ignored()
    {
        var states = States(Catalog, QuestState.Completed);
        states[99] = QuestState.Ready;
        states[20] = QuestState.Ready;

        var position = MsqProgress.Compute(Catalog, states);

        Assert.True(position?.IsComplete);
        Assert.Equal(6, position?.Total);
    }

    [Fact]
    public void A_catalog_without_main_scenario_quests_yields_null()
    {
        var catalog = QuestCatalog.Build([Quest(20, "Side quest", section: 2)]);
        Assert.Null(MsqProgress.Compute(catalog, States(catalog, QuestState.Ready)));
        Assert.Null(MsqProgress.Compute(QuestCatalog.Empty, new Dictionary<uint, QuestState>()));
    }

    [Fact]
    public void Evaluation_overload_agrees_with_the_state_map()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Ready), (3, QuestState.Blocked), (4, QuestState.Blocked), (5, QuestState.Blocked), (6, QuestState.Blocked));

        var fromStates = MsqProgress.Compute(Catalog, states);
        var fromEvaluations = MsqProgress.Compute(Catalog, Evaluations(states));

        Assert.Equal(fromStates, fromEvaluations);
        Assert.Equal(2u, fromEvaluations?.Next?.RowId);
    }
}
