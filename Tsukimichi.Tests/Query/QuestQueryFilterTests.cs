using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class QuestQueryFilterTests
{
    // One quest per state, all in category 10 except the last two which sit in category 11.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Ready", category: 10),
        Quest(2, "ReadyOnOtherJob", category: 10),
        Quest(3, "Accepted", category: 10),
        Quest(4, "Blocked", category: 10),
        Quest(5, "DoneThisCycle", category: 10),
        Quest(6, "Completed", category: 10),
        Quest(7, "Foreclosed", category: 10),
        Quest(8, "Unknown", category: 10),
        Quest(9, "Completed in 11", category: 11),
        Quest(10, "Ready in 11", category: 11),
    ]);

    private static readonly Dictionary<uint, QuestState> AllStates = States(
        (1, QuestState.Ready),
        (2, QuestState.ReadyOnOtherJob),
        (3, QuestState.Accepted),
        (4, QuestState.Blocked),
        (5, QuestState.DoneThisCycle),
        (6, QuestState.Completed),
        (7, QuestState.Foreclosed),
        (8, QuestState.Unknown),
        (9, QuestState.Completed),
        (10, QuestState.Ready));

    [Fact]
    public void No_filters_returns_everything_in_journal_order_with_states_and_next_step()
    {
        var evaluations = Evaluations(AllStates, new Dictionary<uint, string> { [4] = "needs level 50" });
        var result = Run(Catalog, evaluations);

        Assert.Null(result.Empty);
        Assert.Equal(10, result.TotalInScope);
        Assert.Equal(Enumerable.Range(1, 10).Select(i => (uint)i).ToArray(), RowIds(result));
        Assert.Equal(QuestState.Blocked, result.Rows[3].State);
        Assert.Equal("needs level 50", result.Rows[3].NextStep);
        Assert.Equal(string.Empty, result.Rows[0].NextStep);
    }

    [Fact]
    public void Evaluation_overload_matches_the_state_overload_row_for_row()
    {
        var filters = new FilterSet { HideCompleted = true };
        var sort = new SortSpec(SortColumn.State, true);

        var fromStates = Run(Catalog, AllStates, filters, sort: sort);
        var fromEvaluations = Run(Catalog, Evaluations(AllStates), filters, sort: sort);

        Assert.Equal(RowIds(fromStates), RowIds(fromEvaluations));
        Assert.Equal(fromStates.Rows.Select(r => r.State), fromEvaluations.Rows.Select(r => r.State));
        Assert.Equal(fromStates.TotalInScope, fromEvaluations.TotalInScope);
    }

    [Fact]
    public void Legacy_NextStepText_still_feeds_the_state_overload()
    {
#pragma warning disable CS0618 // kept for callers that have not moved to the QuestEvaluation overload
        var ctx = QueryContext.Empty with { NextStepText = new Dictionary<uint, string> { [4] = "needs level 50" } };
#pragma warning restore CS0618
        var result = Run(Catalog, AllStates, ctx: ctx);

        Assert.Equal("needs level 50", result.Rows[3].NextStep);
        Assert.Equal(string.Empty, result.Rows[0].NextStep);
    }

    [Fact]
    public void Missing_state_defaults_to_unknown()
    {
        var result = Run(Catalog, new Dictionary<uint, QuestState>());
        Assert.All(result.Rows, r => Assert.Equal(QuestState.Unknown, r.State));
    }

    [Fact]
    public void Hide_completed_removes_completed_and_foreclosed()
    {
        var result = Run(Catalog, AllStates, new FilterSet { HideCompleted = true });

        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 8, 10 }, RowIds(result));
    }

    [Fact]
    public void Available_only_keeps_ready_other_job_and_accepted()
    {
        var result = Run(Catalog, AllStates, new FilterSet { AvailableOnly = true });

        Assert.Equal(new uint[] { 1, 2, 3, 10 }, RowIds(result));
    }

    [Fact]
    public void Per_category_hide_completed_override_beats_global_off()
    {
        var filters = new FilterSet { HideCompleted = false };
        filters.PerCategoryHideCompleted[11] = true;

        var result = Run(Catalog, AllStates, filters);

        Assert.Contains(6u, RowIds(result));
        Assert.Contains(7u, RowIds(result));
        Assert.DoesNotContain(9u, RowIds(result));
        Assert.Contains(10u, RowIds(result));
    }

    [Fact]
    public void Per_category_hide_completed_override_beats_global_on()
    {
        var filters = new FilterSet { HideCompleted = true };
        filters.PerCategoryHideCompleted[11] = false;

        var result = Run(Catalog, AllStates, filters);

        Assert.DoesNotContain(6u, RowIds(result));
        Assert.DoesNotContain(7u, RowIds(result));
        Assert.Contains(9u, RowIds(result));
    }

    [Fact]
    public void Per_category_available_only_override_beats_global_off()
    {
        var filters = new FilterSet { AvailableOnly = false };
        filters.PerCategoryAvailableOnly[10] = true;

        var result = Run(Catalog, AllStates, filters);

        Assert.Equal(new uint[] { 1, 2, 3, 9, 10 }, RowIds(result));
    }

    [Fact]
    public void Per_category_available_only_override_beats_global_on()
    {
        var filters = new FilterSet { AvailableOnly = true };
        filters.PerCategoryAvailableOnly[11] = false;

        var result = Run(Catalog, AllStates, filters);

        Assert.Equal(new uint[] { 1, 2, 3, 9, 10 }, RowIds(result));
    }

    [Fact]
    public void State_mask_keeps_only_selected_states()
    {
        var filters = new FilterSet { StateMask = QuestStateMask.Blocked | QuestStateMask.Unknown };

        var result = Run(Catalog, AllStates, filters);

        Assert.Equal(new uint[] { 4, 8 }, RowIds(result));
    }

    [Fact]
    public void State_mask_none_hides_everything()
    {
        var result = Run(Catalog, AllStates, new FilterSet { StateMask = QuestStateMask.None });

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Empty);
    }

    [Fact]
    public void Expansion_filter_keeps_listed_expansions()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "ARR", expansion: 0),
            Quest(2, "HW", expansion: 1),
            Quest(3, "SB", expansion: 2),
        ]);
        var filters = new FilterSet();
        filters.Expansions.Add(1);
        filters.Expansions.Add(2);

        var result = Run(catalog, States(catalog, QuestState.Ready), filters);

        Assert.Equal(new uint[] { 2, 3 }, RowIds(result));
    }

    [Fact]
    public void Level_range_is_inclusive()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "L1", level: 1),
            Quest(2, "L10", level: 10),
            Quest(3, "L50", level: 50),
            Quest(4, "L51", level: 51),
        ]);

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { LevelMin = 10, LevelMax = 50 });

        Assert.Equal(new uint[] { 2, 3 }, RowIds(result));
    }

    [Fact]
    public void Level_range_uses_the_displayed_level()
    {
        // A Lv 5 quest with offset 5 shows as Lv 10 and belongs in a 10..50 band although its raw level is below it.
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Raw five", level: 5),
            Quest(2, "Shown as ten", level: 5) with { LevelOffset = 5 },
            Quest(3, "Shown as fifty-one", level: 50) with { LevelOffset = 1 },
        ]);

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { LevelMin = 10, LevelMax = 50 });

        Assert.Equal(new uint[] { 2 }, RowIds(result));
    }

    [Fact]
    public void Class_job_category_matches_either_slot()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Any", classJobCategory: 1),
            Quest(2, "Tank", classJobCategory: 34),
            Quest(3, "Second slot") with { ClassJobCategory = 1, ClassJobCategory1 = 34 },
            Quest(4, "None", classJobCategory: 0),
        ]);

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { ClassJobCategoryId = 34 });

        Assert.Equal(new uint[] { 2, 3 }, RowIds(result));
    }

    private static QuestCatalog RewardCatalog() => QuestCatalog.Build(
    [
        Quest(1, "Mount and emote", rewards: [Reward(RewardKind.Mount, "Horse"), Reward(RewardKind.Emote, "Wave")]),
        Quest(2, "Mount only", rewards: [Reward(RewardKind.Mount, "Bird")]),
        Quest(3, "Emote only", rewards: [Reward(RewardKind.Emote, "Bow")]),
        Quest(4, "Minion only", rewards: [Reward(RewardKind.Minion, "Cat")]),
        Quest(5, "Nothing"),
    ]);

    [Fact]
    public void Reward_only_keeps_quests_with_any_only_kind()
    {
        var catalog = RewardCatalog();
        var filters = new FilterSet();
        filters.RewardKinds[RewardKind.Mount] = TriState.Only;
        filters.RewardKinds[RewardKind.Minion] = TriState.Only;

        var result = Run(catalog, States(catalog, QuestState.Ready), filters);

        Assert.Equal(new uint[] { 1, 2, 4 }, RowIds(result));
    }

    [Fact]
    public void Reward_hidden_removes_quests_with_that_kind()
    {
        var catalog = RewardCatalog();
        var filters = new FilterSet();
        filters.RewardKinds[RewardKind.Emote] = TriState.Hidden;

        var result = Run(catalog, States(catalog, QuestState.Ready), filters);

        Assert.Equal(new uint[] { 2, 4, 5 }, RowIds(result));
    }

    [Fact]
    public void Reward_hidden_wins_over_only()
    {
        var catalog = RewardCatalog();
        var filters = new FilterSet();
        filters.RewardKinds[RewardKind.Mount] = TriState.Only;
        filters.RewardKinds[RewardKind.Emote] = TriState.Hidden;

        var result = Run(catalog, States(catalog, QuestState.Ready), filters);

        Assert.Equal(new uint[] { 2 }, RowIds(result));
    }

    [Fact]
    public void Reward_show_is_a_no_op()
    {
        var catalog = RewardCatalog();
        var filters = new FilterSet();
        filters.RewardKinds[RewardKind.Mount] = TriState.Show;

        var result = Run(catalog, States(catalog, QuestState.Ready), filters);

        Assert.Equal(5, result.Rows.Length);
        Assert.False(filters.IsActive());
    }

    [Fact]
    public void Repeatable_only_keeps_repeatables()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Once"),
            Quest(2, "Daily", repeatable: true),
        ]);

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { RepeatableOnly = true });

        Assert.Equal(new uint[] { 2 }, RowIds(result));
    }

    [Fact]
    public void Seasonal_active_only_keeps_quests_of_active_festivals()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Plain"),
            Quest(2, "Starlight", festival: 7),
            Quest(3, "Little Ladies", festival: 8),
        ]);
        var ctx = QueryContext.Empty with { ActiveFestivals = new HashSet<ushort> { 7 } };

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { SeasonalActiveOnly = true }, ctx: ctx);

        Assert.Equal(new uint[] { 2 }, RowIds(result));
    }

    [Fact]
    public void Pinned_only_keeps_pinned_rows()
    {
        var ctx = QueryContext.Empty with { Pinned = new HashSet<uint> { 3, 9 } };

        var result = Run(Catalog, AllStates, new FilterSet { PinnedOnly = true }, ctx: ctx);

        Assert.Equal(new uint[] { 3, 9 }, RowIds(result));
    }

    [Fact]
    public void Unlisted_is_excluded_by_default_and_included_on_request()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Listed"),
            Quest(2, "Hidden", genre: 0),
        ]);
        var states = States(catalog, QuestState.Ready);

        var withoutUnlisted = Run(catalog, states);
        Assert.Equal(new uint[] { 1 }, RowIds(withoutUnlisted));
        Assert.Equal(1, withoutUnlisted.TotalInScope);

        var withUnlisted = Run(catalog, states, new FilterSet { IncludeUnlisted = true });
        Assert.Equal(new uint[] { 1, 2 }, RowIds(withUnlisted));
        Assert.Equal(2, withUnlisted.TotalInScope);
    }

    [Fact]
    public void Search_filters_rows_through_the_index()
    {
        var result = Run(Catalog, AllStates, search: "  Ready ");

        Assert.Equal(new uint[] { 1, 2, 10 }, RowIds(result));
    }

    [Fact]
    public void Filters_combine_with_and()
    {
        var filters = new FilterSet { HideCompleted = true, AvailableOnly = true, PinnedOnly = true };
        var ctx = QueryContext.Empty with { Pinned = new HashSet<uint> { 2, 6 } };

        var result = Run(Catalog, AllStates, filters, ctx: ctx);

        Assert.Equal(new uint[] { 2 }, RowIds(result));
    }
}
