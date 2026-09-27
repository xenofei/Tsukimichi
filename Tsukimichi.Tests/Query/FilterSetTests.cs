using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Query;

public class FilterSetTests
{
    [Fact]
    public void Default_filter_set_is_inactive()
    {
        var filters = new FilterSet();

        Assert.False(filters.HideCompleted);
        Assert.False(filters.AvailableOnly);
        Assert.Equal(QuestStateMask.All, filters.StateMask);
        Assert.Empty(filters.Expansions);
        Assert.Equal(FilterSet.NoLevelMin, filters.LevelMin);
        Assert.Equal(FilterSet.NoLevelMax, filters.LevelMax);
        Assert.Null(filters.ClassJobCategoryId);
        Assert.Empty(filters.RewardKinds);
        Assert.False(filters.RepeatableOnly);
        Assert.False(filters.SeasonalActiveOnly);
        Assert.False(filters.IncludeUnlisted);
        Assert.False(filters.PinnedOnly);
        Assert.False(filters.IsActive);
    }

    [Fact]
    public void Clone_is_deep_and_equal()
    {
        var filters = new FilterSet
        {
            HideCompleted = true,
            AvailableOnly = true,
            StateMask = QuestStateMask.Ready | QuestStateMask.Accepted,
            LevelMin = 10,
            LevelMax = 50,
            ClassJobCategoryId = 34,
            RepeatableOnly = true,
            SeasonalActiveOnly = true,
            IncludeUnlisted = true,
            PinnedOnly = true,
        };
        filters.PerCategoryHideCompleted[10] = false;
        filters.PerCategoryAvailableOnly[11] = true;
        filters.Expansions.Add(3);
        filters.RewardKinds[RewardKind.Mount] = TriState.Only;

        var clone = filters.Clone();

        Assert.Equal(filters, clone);
        Assert.True(filters.Equals(clone));
        Assert.Equal(filters.GetHashCode(), clone.GetHashCode());

        clone.PerCategoryHideCompleted[12] = true;
        Assert.NotEqual(filters, clone);
        Assert.False(filters.PerCategoryHideCompleted.ContainsKey(12));

        clone = filters.Clone();
        clone.Expansions.Add(4);
        Assert.NotEqual(filters, clone);

        clone = filters.Clone();
        clone.RewardKinds[RewardKind.Mount] = TriState.Hidden;
        Assert.NotEqual(filters, clone);

        clone = filters.Clone();
        clone.LevelMax = 51;
        Assert.NotEqual(filters, clone);
    }

    [Fact]
    public void Dictionary_equality_ignores_insertion_order()
    {
        var a = new FilterSet();
        a.PerCategoryHideCompleted[1] = true;
        a.PerCategoryHideCompleted[2] = false;
        a.Expansions.Add(1);
        a.Expansions.Add(2);

        var b = new FilterSet();
        b.PerCategoryHideCompleted[2] = false;
        b.PerCategoryHideCompleted[1] = true;
        b.Expansions.Add(2);
        b.Expansions.Add(1);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Json_round_trip_preserves_every_field()
    {
        var filters = new FilterSet
        {
            HideCompleted = true,
            StateMask = QuestStateMask.Blocked | QuestStateMask.Unknown,
            LevelMin = 5,
            LevelMax = 60,
            ClassJobCategoryId = 1,
            SeasonalActiveOnly = true,
        };
        filters.PerCategoryHideCompleted[10] = false;
        filters.PerCategoryAvailableOnly[11] = true;
        filters.Expansions.Add(2);
        filters.RewardKinds[RewardKind.Emote] = TriState.Hidden;
        filters.RewardKinds[RewardKind.Minion] = TriState.Only;

        var json = JsonSerializer.Serialize(filters);
        var back = JsonSerializer.Deserialize<FilterSet>(json);

        Assert.NotNull(back);
        Assert.Equal(filters, back);
    }

    [Fact]
    public void Per_category_lookup_prefers_override()
    {
        var filters = new FilterSet { HideCompleted = true, AvailableOnly = false };
        filters.PerCategoryHideCompleted[10] = false;
        filters.PerCategoryAvailableOnly[10] = true;

        Assert.False(filters.HideCompletedFor(10));
        Assert.True(filters.HideCompletedFor(11));
        Assert.True(filters.AvailableOnlyFor(10));
        Assert.False(filters.AvailableOnlyFor(11));
    }

    [Fact]
    public void Reset_returns_to_defaults()
    {
        var filters = new FilterSet { HideCompleted = true, LevelMax = 3 };
        filters.Expansions.Add(1);
        filters.RewardKinds[RewardKind.Mount] = TriState.Only;

        filters.Reset();

        Assert.Equal(new FilterSet(), filters);
        Assert.False(filters.IsActive);
    }

    [Fact]
    public void State_mask_maps_every_state()
    {
        foreach (var state in Enum.GetValues<QuestState>())
        {
            var bit = state.ToMask();
            Assert.True(QuestStateMask.All.Contains(state));
            Assert.True(bit.Contains(state));
            Assert.Equal(1, System.Numerics.BitOperations.PopCount((uint)bit));
        }

        Assert.False((QuestStateMask.Ready | QuestStateMask.Accepted).Contains(QuestState.Blocked));
    }
}
