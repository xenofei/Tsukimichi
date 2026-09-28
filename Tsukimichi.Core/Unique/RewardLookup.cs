using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// Item-keyed view over a <see cref="UniqueRewardCatalog"/> for the item hover hint and the context-menu link: which
/// unique-reward entries an item id belongs to, and the quest record behind an entry. Immutable; built from the
/// merged catalog (its unique view, so quests the user marked not unique are absent) and the quest catalog when
/// loaded. Item ids from the game carry offsets for HQ and collectable items; every lookup normalises them first.
/// </summary>
public sealed class RewardLookup
{
    /// <summary>Item ids at or above this are EventItem rows (key items) and carry no quality offset.</summary>
    public const uint EventItemBase = 2_000_000;

    /// <summary>Added to an item id by the game for the high-quality variant.</summary>
    public const uint HqOffset = 1_000_000;

    /// <summary>Added to an item id by the game for the collectable variant.</summary>
    public const uint CollectableOffset = 500_000;

    private static readonly UniqueRewardEntry[] NoEntries = [];

    /// <summary>Lookup over the empty catalog; every item resolves to no entries.</summary>
    public static readonly RewardLookup Empty = new(UniqueRewardCatalog.Empty, null);

    private readonly FrozenDictionary<uint, IReadOnlyList<UniqueRewardEntry>> byItem;

    public RewardLookup(UniqueRewardCatalog rewards, QuestCatalog? quests)
    {
        Rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        Quests = quests;

        var grouped = new Dictionary<uint, List<UniqueRewardEntry>>();
        foreach (var entry in rewards.All)
        {
            if (entry.ItemId == 0)
            {
                continue;
            }

            if (!grouped.TryGetValue(entry.ItemId, out var list))
            {
                list = [];
                grouped[entry.ItemId] = list;
            }

            list.Add(entry);
        }

        byItem = grouped.Count == 0
            ? FrozenDictionary<uint, IReadOnlyList<UniqueRewardEntry>>.Empty
            : grouped.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<UniqueRewardEntry>)kv.Value.ToArray());
    }

    /// <summary>The catalog this lookup was built from.</summary>
    public UniqueRewardCatalog Rewards { get; }

    /// <summary>The quest catalog used by <see cref="QuestFor"/>; null before the catalog build finished.</summary>
    public QuestCatalog? Quests { get; }

    /// <summary>Number of distinct item ids with at least one entry.</summary>
    public int ItemCount => byItem.Count;

    /// <summary>Whether this lookup was built from exactly these inputs (by reference), so a caller can tell when to rebuild.</summary>
    public bool Matches(UniqueRewardCatalog rewards, QuestCatalog? quests) => ReferenceEquals(Rewards, rewards) && ReferenceEquals(Quests, quests);

    /// <summary>
    /// The base item id behind an id the game hands out: HQ (+1,000,000) and collectable (+500,000) offsets are
    /// removed; EventItem ids (2,000,000 and up) are returned as they are. Zero stays zero.
    /// </summary>
    public static uint NormalizeItemId(ulong itemId)
    {
        if (itemId >= EventItemBase)
        {
            return itemId <= uint.MaxValue ? (uint)itemId : 0;
        }

        if (itemId >= HqOffset)
        {
            return (uint)(itemId - HqOffset);
        }

        if (itemId >= CollectableOffset)
        {
            return (uint)(itemId - CollectableOffset);
        }

        return (uint)itemId;
    }

    /// <summary>Entries whose reward is this item, in catalog order; empty when none. Accepts HQ and collectable ids.</summary>
    public IReadOnlyList<UniqueRewardEntry> ByItem(ulong itemId)
    {
        var id = NormalizeItemId(itemId);
        return id == 0 ? NoEntries : byItem.GetValueOrDefault(id) ?? NoEntries;
    }

    /// <summary>The quest an entry belongs to, or null without a quest catalog or when the row id is unknown to it.</summary>
    public QuestRecord? QuestFor(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Quests?.GetByRowId(entry.QuestRowId);
    }
}

/// <summary>
/// Hands out a <see cref="RewardLookup"/> that follows two live inputs: the merged reward catalog (rebuilt after an
/// override change) and the quest catalog (set once the build finishes). <see cref="Current"/> rebuilds only when
/// either reference changed, so per-frame callers pay a couple of reference comparisons. Not thread-safe; the plugin
/// reads it from the framework thread only.
/// </summary>
public sealed class RewardLookupSource
{
    private readonly Func<UniqueRewardCatalog> rewards;
    private readonly Func<QuestCatalog?> quests;
    private RewardLookup current = RewardLookup.Empty;

    public RewardLookupSource(Func<UniqueRewardCatalog> rewards, Func<QuestCatalog?> quests)
    {
        this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
    }

    /// <summary>The lookup for the inputs as they are now.</summary>
    public RewardLookup Current
    {
        get
        {
            var r = rewards();
            var q = quests();
            if (!current.Matches(r, q))
            {
                current = new RewardLookup(r, q);
            }

            return current;
        }
    }
}
