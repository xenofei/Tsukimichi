using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Every table filter from the filter panel. Mutable so the UI binds to it directly; <see cref="Clone"/> plus value
/// equality support dirty checks, and the shape round-trips through JSON for config storage. The derived checks
/// (<see cref="IsActive"/> and the <c>*Engaged</c> members) are methods so neither System.Text.Json nor Newtonsoft
/// (Dalamud's config serializer) writes them out.
/// </summary>
public sealed class FilterSet : IEquatable<FilterSet>
{
    public const byte NoLevelMin = 0;
    public const byte NoLevelMax = byte.MaxValue;

    /// <summary>
    /// The <see cref="AddedIn"/> value of "New since data" (feature plan v5, 1.5.0): keeps the quests
    /// <c>quest_patches.json</c> does not list, that is new since Tsukimichi's data was built
    /// (<see cref="QueryContext.NewSinceData"/>). Not a patch number, so it never matches a series.
    /// </summary>
    public const string NewSinceData = "new";

    /// <summary>Removes Completed and Foreclosed quests. Per-category overrides win.</summary>
    public bool HideCompleted { get; set; }

    /// <summary>Overrides of <see cref="HideCompleted"/> keyed by JournalCategory id.</summary>
    public Dictionary<uint, bool> PerCategoryHideCompleted { get; set; } = [];

    /// <summary>Keeps only Ready, ReadyOnOtherJob and Accepted quests. Per-category overrides win.</summary>
    public bool AvailableOnly { get; set; }

    /// <summary>Overrides of <see cref="AvailableOnly"/> keyed by JournalCategory id.</summary>
    public Dictionary<uint, bool> PerCategoryAvailableOnly { get; set; } = [];

    /// <summary>Advanced state checkboxes; a quest passes when its state bit is set.</summary>
    public QuestStateMask StateMask { get; set; } = QuestStateMask.All;

    /// <summary>Expansion ids to keep; empty keeps all.</summary>
    public HashSet<byte> Expansions { get; set; } = [];

    /// <summary>
    /// Keeps quests added in this patch series (P8): "7.5" keeps 7.5, 7.51 and 7.55 (<see cref="PatchVersion.InSeries"/>
    /// on <see cref="QuestRecord.AddedIn"/>); empty keeps all. A quest whose patch is unknown never passes an engaged
    /// filter. <see cref="NewSinceData"/> keeps the quests newer than the shipped data instead.
    /// </summary>
    public string AddedIn { get; set; } = string.Empty;

    /// <summary>Inclusive quest level bounds. <see cref="NoLevelMin"/> and <see cref="NoLevelMax"/> mean unbounded.</summary>
    public byte LevelMin { get; set; } = NoLevelMin;

    public byte LevelMax { get; set; } = NoLevelMax;

    /// <summary>Keeps quests whose ClassJobCategory or ClassJobCategory1 equals this id; null keeps all.</summary>
    public uint? ClassJobCategoryId { get; set; }

    /// <summary>Per reward kind. Missing kinds are <see cref="TriState.Show"/>.</summary>
    public Dictionary<RewardKind, TriState> RewardKinds { get; set; } = [];

    /// <summary>
    /// The Unlocks filter (plan v7, 1.19.0 K3): with any kind on, keeps only quests that open one of them
    /// (<see cref="Unlocks.QuestUnlocks.KindMask"/>); empty keeps every quest.
    /// </summary>
    public HashSet<Unlocks.UnlockFindKind> UnlockKinds { get; set; } = [];

    public bool RepeatableOnly { get; set; }

    /// <summary>Keeps only festival quests whose festival is active in the query context.</summary>
    public bool SeasonalActiveOnly { get; set; }

    /// <summary>Whether quests with no journal genre appear outside the Unlisted virtual node.</summary>
    public bool IncludeUnlisted { get; set; }

    /// <summary>
    /// Whether quests on a path the character did not take (another city's start, another starting class, another
    /// Grand Company, another choice) appear in their journal nodes too; off by default, so they are listed only
    /// under the "Other paths" virtual node.
    /// </summary>
    public bool IncludeOtherPaths { get; set; }

    /// <summary>Keeps only quests pinned in the query context.</summary>
    public bool PinnedOnly { get; set; }

    /// <summary>Keeps only quests the viewed character abandoned (<see cref="QueryContext.Abandoned"/>) and has not taken up again.</summary>
    public bool AbandonedOnly { get; set; }

    /// <summary>
    /// Keeps only the story quests that play once and the viewed character has not done
    /// (<see cref="NewGamePlus.IsOnceOnlyStoryLeft"/>, feature plan v5 collector extras): quests no New Game+ chapter
    /// lists, on a story chain or a side story, neither completed nor locked out. Keeps nothing while the New Game+
    /// data is not loaded (<see cref="QueryContext.NewGamePlus"/>).
    /// </summary>
    public bool OnceOnlyStory { get; set; }

    /// <summary>The active one-click preset (feature quests, level band, stalled); <see cref="Preset.None"/> when none.</summary>
    public Preset Preset { get; set; }

    /// <summary>True when any narrowing filter is engaged. <see cref="IncludeUnlisted"/> and <see cref="IncludeOtherPaths"/> widen, so they do not count.</summary>
    public bool IsActive() =>
        Preset != Preset.None
        || HideCompletedEngaged()
        || AvailableOnlyEngaged()
        || StateMask != QuestStateMask.All
        || Expansions.Count > 0
        || AddedInEngaged()
        || LevelRangeEngaged()
        || ClassJobCategoryId is not null
        || RewardKindsEngaged()
        || UnlockKindsEngaged()
        || RepeatableOnly
        || SeasonalActiveOnly
        || PinnedOnly
        || AbandonedOnly
        || OnceOnlyStory;

    public bool HideCompletedEngaged() => HideCompleted || PerCategoryHideCompleted.ContainsValue(true);

    public bool AvailableOnlyEngaged() => AvailableOnly || PerCategoryAvailableOnly.ContainsValue(true);

    public bool LevelRangeEngaged() => LevelMin > NoLevelMin || LevelMax < NoLevelMax;

    /// <summary>A null (a config written before 0.9.0 by a serializer that kept nulls) reads as not engaged.</summary>
    public bool AddedInEngaged() => !string.IsNullOrEmpty(AddedIn);

    /// <summary>Whether <see cref="AddedIn"/> is "New since data" (<see cref="NewSinceData"/>) rather than a patch series.</summary>
    public bool AddedInNewSinceData() => string.Equals(AddedIn?.Trim(), NewSinceData, StringComparison.Ordinal);

    public bool RewardKindsEngaged()
    {
        foreach (var value in RewardKinds.Values)
        {
            if (value != TriState.Show)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the Unlocks filter has a kind on; a null (a hand-edited config) reads as not engaged.</summary>
    public bool UnlockKindsEngaged() => UnlockKinds is { Count: > 0 };

    /// <summary>Effective hide-completed setting for a category: its override, else the global toggle.</summary>
    public bool HideCompletedFor(uint categoryId) =>
        PerCategoryHideCompleted.TryGetValue(categoryId, out var value) ? value : HideCompleted;

    /// <summary>Effective available-only setting for a category: its override, else the global toggle.</summary>
    public bool AvailableOnlyFor(uint categoryId) =>
        PerCategoryAvailableOnly.TryGetValue(categoryId, out var value) ? value : AvailableOnly;

    public FilterSet Clone() => new()
    {
        HideCompleted = HideCompleted,
        PerCategoryHideCompleted = new Dictionary<uint, bool>(PerCategoryHideCompleted),
        AvailableOnly = AvailableOnly,
        PerCategoryAvailableOnly = new Dictionary<uint, bool>(PerCategoryAvailableOnly),
        StateMask = StateMask,
        Expansions = [.. Expansions],
        AddedIn = AddedIn ?? string.Empty,
        LevelMin = LevelMin,
        LevelMax = LevelMax,
        ClassJobCategoryId = ClassJobCategoryId,
        RewardKinds = new Dictionary<RewardKind, TriState>(RewardKinds),
        UnlockKinds = [.. UnlockKinds ?? []],
        RepeatableOnly = RepeatableOnly,
        SeasonalActiveOnly = SeasonalActiveOnly,
        IncludeUnlisted = IncludeUnlisted,
        IncludeOtherPaths = IncludeOtherPaths,
        PinnedOnly = PinnedOnly,
        AbandonedOnly = AbandonedOnly,
        OnceOnlyStory = OnceOnlyStory,
        Preset = Preset,
    };

    /// <summary>Returns every field to its default; the one-click reset behind the empty-result guard.</summary>
    public void Reset()
    {
        HideCompleted = false;
        PerCategoryHideCompleted.Clear();
        AvailableOnly = false;
        PerCategoryAvailableOnly.Clear();
        StateMask = QuestStateMask.All;
        Expansions.Clear();
        AddedIn = string.Empty;
        LevelMin = NoLevelMin;
        LevelMax = NoLevelMax;
        ClassJobCategoryId = null;
        RewardKinds.Clear();
        UnlockKinds = [];
        RepeatableOnly = false;
        SeasonalActiveOnly = false;
        IncludeUnlisted = false;
        IncludeOtherPaths = false;
        PinnedOnly = false;
        AbandonedOnly = false;
        OnceOnlyStory = false;
        Preset = Preset.None;
    }

    public bool Equals(FilterSet? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return HideCompleted == other.HideCompleted
            && AvailableOnly == other.AvailableOnly
            && StateMask == other.StateMask
            && LevelMin == other.LevelMin
            && LevelMax == other.LevelMax
            && ClassJobCategoryId == other.ClassJobCategoryId
            && RepeatableOnly == other.RepeatableOnly
            && SeasonalActiveOnly == other.SeasonalActiveOnly
            && IncludeUnlisted == other.IncludeUnlisted
            && IncludeOtherPaths == other.IncludeOtherPaths
            && PinnedOnly == other.PinnedOnly
            && AbandonedOnly == other.AbandonedOnly
            && OnceOnlyStory == other.OnceOnlyStory
            && Preset == other.Preset
            && string.Equals(AddedIn ?? string.Empty, other.AddedIn ?? string.Empty, StringComparison.Ordinal)
            && Expansions.SetEquals(other.Expansions)
            && SameEntries(PerCategoryHideCompleted, other.PerCategoryHideCompleted)
            && SameEntries(PerCategoryAvailableOnly, other.PerCategoryAvailableOnly)
            && SameEntries(RewardKinds, other.RewardKinds)
            && (UnlockKinds ?? []).SetEquals(other.UnlockKinds ?? []);
    }

    public override bool Equals(object? obj) => Equals(obj as FilterSet);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HideCompleted);
        hash.Add(AvailableOnly);
        hash.Add(StateMask);
        hash.Add(LevelMin);
        hash.Add(LevelMax);
        hash.Add(ClassJobCategoryId);
        hash.Add(RepeatableOnly);
        hash.Add(SeasonalActiveOnly);
        hash.Add(IncludeUnlisted);
        hash.Add(IncludeOtherPaths);
        hash.Add(PinnedOnly);
        hash.Add(AbandonedOnly);
        hash.Add(OnceOnlyStory);
        hash.Add(Preset);
        hash.Add(AddedIn ?? string.Empty, StringComparer.Ordinal);
        hash.Add(OrderInsensitiveHash(Expansions));
        hash.Add(OrderInsensitiveHash(PerCategoryHideCompleted));
        hash.Add(OrderInsensitiveHash(PerCategoryAvailableOnly));
        hash.Add(OrderInsensitiveHash(RewardKinds));
        hash.Add(OrderInsensitiveHash(UnlockKinds ?? []));
        return hash.ToHashCode();
    }

    private static bool SameEntries<TKey, TValue>(Dictionary<TKey, TValue> a, Dictionary<TKey, TValue> b)
        where TKey : notnull
        where TValue : struct
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var otherValue) || !EqualityComparer<TValue>.Default.Equals(value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    private static int OrderInsensitiveHash<T>(IEnumerable<T> items)
    {
        // XOR is commutative, so insertion order does not change the result.
        var acc = 0;
        foreach (var item in items)
        {
            acc ^= item?.GetHashCode() ?? 0;
        }

        return acc;
    }
}
