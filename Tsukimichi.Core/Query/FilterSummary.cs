using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>The groups of the filter drawer's Advanced section, in drawing order (plan v7 UI-2, spec §2.3).</summary>
public enum FilterGroup
{
    States,
    Expansions,
    AddedIn,
    Level,
    Job,
    Rewards,
    More,
}

/// <summary>
/// What the filter drawer says about a <see cref="FilterSet"/> without opening it (plan v7 UI-2, spec §2.3): whether
/// each Advanced group holds a value other than its default (its summary line gains a dot, and the section's "2 set"
/// pill counts it), how many states and More toggles are on, the reward kinds the Rewards group lists first, and
/// whether there is anything at all for Reset to clear. Allocation-free.
/// </summary>
public static class FilterSummary
{
    /// <summary>Every group, in drawing order.</summary>
    public static readonly FilterGroup[] Groups = Enum.GetValues<FilterGroup>();

    /// <summary>How many quest states the States group offers.</summary>
    public static int StateCount { get; } = System.Numerics.BitOperations.PopCount((uint)QuestStateMask.All);

    /// <summary>How many toggles the More group holds.</summary>
    public const int MoreCount = 7;

    /// <summary>
    /// The reward kinds players filter by most (Mount, Minion, Orchestrion roll, Emote, Hairstyle, Triple Triad card),
    /// listed first in the Rewards group; the rest wait behind "N more kinds".
    /// </summary>
    public static readonly RewardKind[] CommonRewardKinds =
    [
        RewardKind.Mount,
        RewardKind.Minion,
        RewardKind.Orchestrion,
        RewardKind.Emote,
        RewardKind.Hairstyle,
        RewardKind.TripleTriadCard,
    ];

    /// <summary>Whether <paramref name="kind"/> is one of <see cref="CommonRewardKinds"/>.</summary>
    public static bool IsCommonRewardKind(RewardKind kind) => Array.IndexOf(CommonRewardKinds, kind) >= 0;

    /// <summary>
    /// Whether the Rewards group lists <paramref name="kind"/>: the common kinds always, the others when the group is
    /// <paramref name="expanded"/> or when the kind holds a value other than Show (a set value is never hidden).
    /// </summary>
    public static bool ShowsRewardKind(FilterSet filters, RewardKind kind, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return expanded || IsCommonRewardKind(kind) || (filters.RewardKinds.TryGetValue(kind, out var value) && value != TriState.Show);
    }

    /// <summary>How many of the <see cref="StateCount"/> states the filter keeps.</summary>
    public static int StatesOn(FilterSet filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return System.Numerics.BitOperations.PopCount((uint)(filters.StateMask & QuestStateMask.All));
    }

    /// <summary>How many of the More group's <see cref="MoreCount"/> toggles are on.</summary>
    public static int MoreOn(FilterSet filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var count = 0;
        Add(ref count, filters.RepeatableOnly);
        Add(ref count, filters.SeasonalActiveOnly);
        Add(ref count, filters.IncludeUnlisted);
        Add(ref count, filters.IncludeOtherPaths);
        Add(ref count, filters.PinnedOnly);
        Add(ref count, filters.AbandonedOnly);
        Add(ref count, filters.OnceOnlyStory);
        return count;
    }

    /// <summary>Whether <paramref name="group"/> holds a value other than its default.</summary>
    public static bool IsSet(FilterSet filters, FilterGroup group)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return group switch
        {
            FilterGroup.States => (filters.StateMask & QuestStateMask.All) != QuestStateMask.All,
            FilterGroup.Expansions => filters.Expansions.Count > 0,
            FilterGroup.AddedIn => filters.AddedInEngaged(),
            FilterGroup.Level => filters.LevelRangeEngaged(),
            FilterGroup.Job => filters.ClassJobCategoryId is not null,
            FilterGroup.Rewards => filters.RewardKindsEngaged(),
            FilterGroup.More => MoreOn(filters) > 0,
            _ => false,
        };
    }

    /// <summary>How many Advanced groups hold a value: the Advanced heading's "2 set" pill (0 hides it).</summary>
    public static int SetCount(FilterSet filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var count = 0;
        foreach (var group in Groups)
        {
            Add(ref count, IsSet(filters, group));
        }

        return count;
    }

    /// <summary>
    /// Whether Reset has anything to clear: any field of <paramref name="filters"/> off its default (the quick view and
    /// the widening toggles included, since Reset clears them too) or a search.
    /// </summary>
    public static bool CanReset(FilterSet filters, string? search)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return !string.IsNullOrEmpty(search) || !filters.Equals(Defaults);
    }

    /// <summary>A filter set at its defaults, compared against and never handed out.</summary>
    private static readonly FilterSet Defaults = new();

    private static void Add(ref int count, bool on)
    {
        if (on)
        {
            count++;
        }
    }
}
