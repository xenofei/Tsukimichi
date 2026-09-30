using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Query;

public class FilterBadgeTests
{
    /// <summary>Every narrowing filter the badge counts, each engaged on its own.</summary>
    public static IEnumerable<object[]> EachCountedFilter() =>
    [
        [(Action<FilterSet>)(static f => f.HideCompleted = true)],
        [(Action<FilterSet>)(static f => f.PerCategoryHideCompleted[3] = true)],
        [(Action<FilterSet>)(static f => f.AvailableOnly = true)],
        [(Action<FilterSet>)(static f => f.PerCategoryAvailableOnly[3] = true)],
        [(Action<FilterSet>)(static f => f.StateMask = QuestStateMask.All & ~QuestState.Completed.ToMask())],
        [(Action<FilterSet>)(static f => f.Expansions.Add(2))],
        [(Action<FilterSet>)(static f => f.LevelMin = 50)],
        [(Action<FilterSet>)(static f => f.LevelMax = 60)],
        [(Action<FilterSet>)(static f => f.ClassJobCategoryId = 33)],
        [(Action<FilterSet>)(static f => f.RewardKinds[RewardKind.Mount] = TriState.Only)],
        [(Action<FilterSet>)(static f => f.RepeatableOnly = true)],
        [(Action<FilterSet>)(static f => f.SeasonalActiveOnly = true)],
        [(Action<FilterSet>)(static f => f.PinnedOnly = true)],
        [(Action<FilterSet>)(static f => f.AbandonedOnly = true)],
    ];

    [Fact]
    public void Default_filters_show_no_badge()
    {
        Assert.Equal(0, FilterBadge.Count(new FilterSet()));
    }

    [Theory]
    [MemberData(nameof(EachCountedFilter))]
    public void Each_narrowing_filter_counts_once(Action<FilterSet> engage)
    {
        var filters = new FilterSet();
        engage(filters);
        Assert.Equal(1, FilterBadge.Count(filters));
    }

    [Fact]
    public void A_filter_with_several_values_still_counts_once()
    {
        var filters = new FilterSet();
        filters.Expansions.UnionWith([1, 2, 3]);
        filters.HideCompleted = true;
        filters.PerCategoryHideCompleted[4] = true;
        filters.PerCategoryHideCompleted[5] = false;
        filters.RewardKinds[RewardKind.Mount] = TriState.Only;
        filters.RewardKinds[RewardKind.Minion] = TriState.Hidden;
        filters.LevelMin = 10;
        filters.LevelMax = 20;

        Assert.Equal(4, FilterBadge.Count(filters));
    }

    [Fact]
    public void Quick_views_and_include_removed_are_not_counted()
    {
        var filters = new FilterSet { IncludeUnlisted = true };
        foreach (var preset in Enum.GetValues<Preset>())
        {
            filters.Preset = preset;
            Assert.Equal(0, FilterBadge.Count(filters));
        }
    }

    [Fact]
    public void A_reward_kind_left_on_show_is_not_engaged()
    {
        var filters = new FilterSet();
        filters.RewardKinds[RewardKind.Mount] = TriState.Show;
        filters.PerCategoryAvailableOnly[7] = false;
        Assert.Equal(0, FilterBadge.Count(filters));
    }

    [Fact]
    public void Every_counted_filter_together_is_eleven()
    {
        var filters = new FilterSet();
        foreach (var args in EachCountedFilter())
        {
            ((Action<FilterSet>)args[0])(filters);
        }

        Assert.Equal(11, FilterBadge.Count(filters));
    }

    [Theory]
    [MemberData(nameof(EachCountedFilter))]
    public void The_badge_shows_exactly_when_a_filter_other_than_the_quick_view_is_active(Action<FilterSet> engage)
    {
        // IsActive also counts the quick view; with no quick view the two must agree.
        var filters = new FilterSet();
        Assert.Equal(filters.IsActive(), FilterBadge.Count(filters) > 0);
        engage(filters);
        Assert.Equal(filters.IsActive(), FilterBadge.Count(filters) > 0);
    }
}
