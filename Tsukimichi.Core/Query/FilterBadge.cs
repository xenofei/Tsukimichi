namespace Tsukimichi.Core.Query;

/// <summary>
/// The number on the toolbar's Filters button (T14, game UX panel finding 7): how many narrowing filters from the
/// filter panel are engaged. Each filter counts once however many values it holds (three expansions ticked is one
/// Expansion filter; a per-category override is part of its toggle). Not counted: the search text (the search pill
/// shows it), the quick view (<see cref="FilterSet.Preset"/>, the toolbar's segmented control shows it), the tree scope
/// (the chip row's first chip shows it) and <see cref="FilterSet.IncludeUnlisted"/> (it widens the table). The chip row
/// draws one chip per counted filter, so the badge and the chips always agree.
/// </summary>
public static class FilterBadge
{
    /// <summary>Engaged narrowing filters in <paramref name="filters"/>; 0 hides the badge.</summary>
    public static int Count(FilterSet filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var count = 0;
        Add(ref count, filters.HideCompletedEngaged());
        Add(ref count, filters.AvailableOnlyEngaged());
        Add(ref count, filters.StateMask != QuestStateMask.All);
        Add(ref count, filters.Expansions.Count > 0);
        Add(ref count, filters.AddedInEngaged());
        Add(ref count, filters.LevelRangeEngaged());
        Add(ref count, filters.ClassJobCategoryId is not null);
        Add(ref count, filters.RewardKindsEngaged());
        Add(ref count, filters.RepeatableOnly);
        Add(ref count, filters.SeasonalActiveOnly);
        Add(ref count, filters.PinnedOnly);
        Add(ref count, filters.AbandonedOnly);
        Add(ref count, filters.OnceOnlyStory);
        return count;
    }

    private static void Add(ref int count, bool engaged)
    {
        if (engaged)
        {
            count++;
        }
    }
}
