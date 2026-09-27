using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class EmptyReasonTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Done quest", level: 10, expansion: 0),
        Quest(2, "Blocked quest", level: 60, expansion: 1),
    ]);

    private static readonly Dictionary<uint, QuestState> AllStates = States(
        (1, QuestState.Completed),
        (2, QuestState.Blocked));

    [Fact]
    public void Non_empty_result_has_no_reason()
    {
        var result = Run(Catalog, AllStates);
        Assert.Null(result.Empty);
    }

    [Fact]
    public void Single_filter_that_empties_is_named()
    {
        var result = Run(Catalog, AllStates, new FilterSet { AvailableOnly = true });

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Empty);
        Assert.False(result.Empty!.ScopeIsEmpty);
        Assert.Equal([FilterNames.AvailableOnly], result.Empty.Filters);
    }

    [Fact]
    public void Each_filter_that_would_alone_restore_rows_is_named()
    {
        // Hide completed removes only 1 (level 60); the level range removes only 2 (level 10).
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Done high", level: 60),
            Quest(2, "Blocked low", level: 10),
        ]);
        var result = Run(catalog, AllStates, new FilterSet { HideCompleted = true, LevelMin = 50 });

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.HideCompleted, FilterNames.LevelRange], result.Empty!.Filters);
    }

    [Fact]
    public void Available_now_subsumes_hide_completed()
    {
        // Completed quests are never available, so removing Hide completed alone restores nothing here.
        var result = Run(Catalog, AllStates, new FilterSet { HideCompleted = true, AvailableOnly = true });

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.AvailableOnly], result.Empty!.Filters);
    }

    [Fact]
    public void Filters_that_would_not_help_alone_are_not_named()
    {
        // Search "zzz" kills everything on its own; hide completed does too when paired with search.
        var result = Run(Catalog, AllStates, new FilterSet { HideCompleted = true }, search: "zzz");

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.Search], result.Empty!.Filters);
    }

    [Fact]
    public void No_single_filter_helps_gives_empty_list()
    {
        // Level 1..5 excludes both quests; expansion {3} excludes both. Neither alone restores rows.
        var filters = new FilterSet { LevelMin = 1, LevelMax = 5 };
        filters.Expansions.Add(3);

        var result = Run(Catalog, AllStates, filters);

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Empty);
        Assert.False(result.Empty!.ScopeIsEmpty);
        Assert.Empty(result.Empty.Filters);
    }

    [Fact]
    public void Per_category_override_counts_as_the_global_filter()
    {
        var filters = new FilterSet();
        filters.PerCategoryHideCompleted[10] = true;
        var states = States((1, QuestState.Completed), (2, QuestState.Completed));

        var result = Run(Catalog, states, filters);

        Assert.Equal([FilterNames.HideCompleted], result.Empty!.Filters);
    }

    [Fact]
    public void Every_named_filter_can_be_blamed()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Only quest", level: 10, expansion: 0, classJobCategory: 1, rewards: [Reward(RewardKind.Emote, "Wave")]),
            Quest(2, "Hidden quest", genre: 0),
        ]);
        var states = States((1, QuestState.Ready), (2, QuestState.Ready));

        Assert.Equal([FilterNames.State], Run(catalog, states, new FilterSet { StateMask = QuestStateMask.Blocked }).Empty!.Filters);

        var expansions = new FilterSet();
        expansions.Expansions.Add(2);
        Assert.Equal([FilterNames.Expansion], Run(catalog, states, expansions).Empty!.Filters);

        Assert.Equal([FilterNames.LevelRange], Run(catalog, states, new FilterSet { LevelMin = 20 }).Empty!.Filters);
        Assert.Equal([FilterNames.JobCategory], Run(catalog, states, new FilterSet { ClassJobCategoryId = 34 }).Empty!.Filters);

        var rewards = new FilterSet();
        rewards.RewardKinds[RewardKind.Mount] = TriState.Only;
        Assert.Equal([FilterNames.RewardKinds], Run(catalog, states, rewards).Empty!.Filters);

        Assert.Equal([FilterNames.Repeatable], Run(catalog, states, new FilterSet { RepeatableOnly = true }).Empty!.Filters);
        Assert.Equal([FilterNames.SeasonalActive], Run(catalog, states, new FilterSet { SeasonalActiveOnly = true }).Empty!.Filters);
        Assert.Equal([FilterNames.Pinned], Run(catalog, states, new FilterSet { PinnedOnly = true }).Empty!.Filters);
        Assert.Equal([FilterNames.Search], Run(catalog, states, search: "nothing here").Empty!.Filters);

        // Only the unlisted quest matches the search; including Unlisted would restore it.
        Assert.Equal([FilterNames.IncludeUnlisted, FilterNames.Search], Run(catalog, states, search: "hidden").Empty!.Filters);
    }

    [Fact]
    public void Empty_scope_is_reported_without_filters()
    {
        var result = Run(Catalog, AllStates, new FilterSet { HideCompleted = true }, scope: QuestScope.Category(99));

        Assert.True(result.Empty!.ScopeIsEmpty);
        Assert.Empty(result.Empty.Filters);
    }
}
