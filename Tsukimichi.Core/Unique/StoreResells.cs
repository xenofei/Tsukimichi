using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// Which rewards the FFXIV Online Store also sells, indexed from the unique-reward entries that carry
/// <see cref="OtherSource.OnlineStore"/>: by item id (the store item is the quest's item) and by collectible
/// (kind and reward id, for rewards a quest grants directly). Lets the reward tooltip, which only has a
/// <see cref="RewardRef"/> from the quest catalog, say "Store only". Immutable; build once per data file.
/// </summary>
public sealed class StoreResells
{
    public static readonly StoreResells Empty = new(FrozenSet<uint>.Empty, FrozenSet<(RewardKind, uint)>.Empty);

    private readonly FrozenSet<uint> items;
    private readonly FrozenSet<(RewardKind Kind, uint Id)> rewards;

    private StoreResells(FrozenSet<uint> items, FrozenSet<(RewardKind, uint)> rewards)
    {
        this.items = items;
        this.rewards = rewards;
    }

    /// <summary>Number of distinct rewards (by kind and id) the store also sells.</summary>
    public int Count => rewards.Count;

    public static StoreResells Build(IEnumerable<UniqueRewardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var items = new HashSet<uint>();
        var rewards = new HashSet<(RewardKind, uint)>();
        foreach (var entry in entries)
        {
            if (!entry.SoldOnOnlineStore)
            {
                continue;
            }

            if (entry.ItemId != 0)
            {
                items.Add(entry.ItemId);
            }

            if (entry.RewardId != 0)
            {
                rewards.Add((entry.Kind, entry.RewardId));
            }
        }

        return items.Count == 0 && rewards.Count == 0 ? Empty : new StoreResells(items.ToFrozenSet(), rewards.ToFrozenSet());
    }

    /// <summary>Whether the store sells this reward: its item, or the collectible behind the kind and id.</summary>
    public bool Contains(RewardRef reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        return Contains(reward.Kind, reward.Id, reward.ItemId);
    }

    /// <summary>Whether the store sells item <paramref name="itemId"/> (non-zero) or the collectible (<paramref name="kind"/>, <paramref name="id"/>) (id non-zero).</summary>
    public bool Contains(RewardKind kind, uint id, uint itemId) =>
        (itemId != 0 && items.Contains(itemId)) || (id != 0 && rewards.Contains((kind, id)));
}
