using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// What the filter drawer's summary lines, its "2 set" pill and its Reset read from a filter set (plan v7 UI-2, spec
/// §2.3).
/// </summary>
public class FilterSummaryTests
{
    [Fact]
    public void A_fresh_set_has_nothing_set_and_nothing_to_reset()
    {
        var f = new FilterSet();
        foreach (var group in FilterSummary.Groups)
        {
            Assert.False(FilterSummary.IsSet(f, group), group.ToString());
        }

        Assert.Equal(0, FilterSummary.SetCount(f));
        Assert.Equal(FilterSummary.StateCount, FilterSummary.StatesOn(f));
        Assert.Equal(0, FilterSummary.MoreOn(f));
        Assert.False(FilterSummary.CanReset(f, string.Empty));
        Assert.False(FilterSummary.CanReset(f, null));
    }

    [Fact]
    public void There_are_seven_groups_and_eight_states()
    {
        Assert.Equal(7, FilterSummary.Groups.Length);
        Assert.Equal(8, FilterSummary.StateCount);
    }

    public static IEnumerable<object[]> EachGroup() =>
    [
        [FilterGroup.States, (Action<FilterSet>)(static f => f.StateMask = QuestStateMask.All & ~QuestState.Completed.ToMask())],
        [FilterGroup.Expansions, (Action<FilterSet>)(static f => f.Expansions.Add(1))],
        [FilterGroup.AddedIn, (Action<FilterSet>)(static f => f.AddedIn = "7.5")],
        [FilterGroup.Level, (Action<FilterSet>)(static f => f.LevelMin = 50)],
        [FilterGroup.Job, (Action<FilterSet>)(static f => f.ClassJobCategoryId = 33)],
        [FilterGroup.Rewards, (Action<FilterSet>)(static f => f.RewardKinds[RewardKind.Mount] = TriState.Only)],
        [FilterGroup.More, (Action<FilterSet>)(static f => f.IncludeOtherPaths = true)],
    ];

    [Theory]
    [MemberData(nameof(EachGroup))]
    public void Each_group_is_set_on_its_own(FilterGroup group, Action<FilterSet> engage)
    {
        var f = new FilterSet();
        engage(f);
        foreach (var other in FilterSummary.Groups)
        {
            Assert.Equal(other == group, FilterSummary.IsSet(f, other));
        }

        Assert.Equal(1, FilterSummary.SetCount(f));
        Assert.True(FilterSummary.CanReset(f, string.Empty));
    }

    [Fact]
    public void States_and_more_count_what_is_on()
    {
        var f = new FilterSet
        {
            StateMask = QuestStateMask.All & ~QuestState.Completed.ToMask() & ~QuestState.Foreclosed.ToMask(),
            RepeatableOnly = true,
            PinnedOnly = true,
            OnceOnlyStory = true,
        };

        Assert.Equal(6, FilterSummary.StatesOn(f));
        Assert.Equal(3, FilterSummary.MoreOn(f));
        Assert.Equal(2, FilterSummary.SetCount(f));
    }

    [Fact]
    public void A_reward_kind_set_back_to_show_is_not_set()
    {
        var f = new FilterSet();
        f.RewardKinds[RewardKind.Mount] = TriState.Show;
        Assert.False(FilterSummary.IsSet(f, FilterGroup.Rewards));
    }

    [Fact]
    public void Reset_has_work_for_a_search_a_quick_view_or_a_widening_toggle()
    {
        Assert.True(FilterSummary.CanReset(new FilterSet(), "chocobo"));
        Assert.True(FilterSummary.CanReset(new FilterSet { Preset = Preset.Stalled }, string.Empty));
        Assert.True(FilterSummary.CanReset(new FilterSet { IncludeUnlisted = true }, string.Empty));
    }

    [Fact]
    public void Six_common_reward_kinds_lead_and_the_rest_wait_unless_set()
    {
        var f = new FilterSet();
        Assert.Equal(6, FilterSummary.CommonRewardKinds.Length);
        Assert.Equal(17, Enum.GetValues<RewardKind>().Length - FilterSummary.CommonRewardKinds.Length);
        Assert.True(FilterSummary.ShowsRewardKind(f, RewardKind.Mount, expanded: false));
        Assert.False(FilterSummary.ShowsRewardKind(f, RewardKind.Barding, expanded: false));
        Assert.True(FilterSummary.ShowsRewardKind(f, RewardKind.Barding, expanded: true));

        // A set value is never hidden behind "more kinds".
        f.RewardKinds[RewardKind.Barding] = TriState.Hidden;
        Assert.True(FilterSummary.ShowsRewardKind(f, RewardKind.Barding, expanded: false));
    }
}
