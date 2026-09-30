using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// Which quest rewards can be had elsewhere, indexed from the unique-reward entries: the ones the FFXIV Online Store
/// also sells (<see cref="OtherSource.OnlineStore"/>) and the ones that also drop in a duty
/// (<see cref="OtherSource.DungeonDrop"/>, with the duties from <see cref="UniqueRewardEntry.DropWhere"/>). Indexed by
/// item id (the store or drop item is the quest's item) and by collectible (kind and reward id, for rewards a quest
/// grants directly). Lets the reward tooltip, which only has a <see cref="RewardRef"/> from the quest catalog, say
/// "Store only" or "Also drops in …". Immutable; build once per data file.
/// </summary>
public sealed class StoreResells
{
    public static readonly StoreResells Empty = new(
        FrozenSet<uint>.Empty,
        FrozenSet<(RewardKind, uint)>.Empty,
        FrozenDictionary<uint, string>.Empty,
        FrozenDictionary<(RewardKind, uint), string>.Empty);

    private readonly FrozenSet<uint> items;
    private readonly FrozenSet<(RewardKind Kind, uint Id)> rewards;
    private readonly FrozenDictionary<uint, string> dropItems;
    private readonly FrozenDictionary<(RewardKind Kind, uint Id), string> dropRewards;

    private StoreResells(
        FrozenSet<uint> items,
        FrozenSet<(RewardKind, uint)> rewards,
        FrozenDictionary<uint, string> dropItems,
        FrozenDictionary<(RewardKind, uint), string> dropRewards)
    {
        this.items = items;
        this.rewards = rewards;
        this.dropItems = dropItems;
        this.dropRewards = dropRewards;
    }

    /// <summary>Number of distinct rewards (by kind and id) the store also sells.</summary>
    public int Count => rewards.Count;

    /// <summary>Number of distinct rewards (by kind and id) that also drop in a duty.</summary>
    public int DropCount => dropRewards.Count;

    public static StoreResells Build(IEnumerable<UniqueRewardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var items = new HashSet<uint>();
        var rewards = new HashSet<(RewardKind, uint)>();
        var dropItems = new Dictionary<uint, string>();
        var dropRewards = new Dictionary<(RewardKind, uint), string>();
        foreach (var entry in entries)
        {
            if (entry.SoldOnOnlineStore)
            {
                if (entry.ItemId != 0)
                {
                    items.Add(entry.ItemId);
                }

                if (entry.RewardId != 0)
                {
                    rewards.Add((entry.Kind, entry.RewardId));
                }
            }

            if (entry.DropsInDuty)
            {
                var where = entry.DropWhere;
                if (entry.ItemId != 0)
                {
                    dropItems.TryAdd(entry.ItemId, where);
                }

                if (entry.RewardId != 0)
                {
                    dropRewards.TryAdd((entry.Kind, entry.RewardId), where);
                }
            }
        }

        return items.Count == 0 && rewards.Count == 0 && dropItems.Count == 0 && dropRewards.Count == 0
            ? Empty
            : new StoreResells(items.ToFrozenSet(), rewards.ToFrozenSet(), dropItems.ToFrozenDictionary(), dropRewards.ToFrozenDictionary());
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

    /// <summary>
    /// Where this reward also drops: the duties (<see cref="UniqueRewardEntry.DropWhere"/>, possibly empty when the data
    /// names none), or null when it does not drop anywhere the data knows of.
    /// </summary>
    public string? DropWhere(RewardRef reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        return DropWhere(reward.Kind, reward.Id, reward.ItemId);
    }

    /// <summary>
    /// Where item <paramref name="itemId"/> (non-zero) or the collectible (<paramref name="kind"/>, <paramref name="id"/>)
    /// (id non-zero) also drops; null when it does not.
    /// </summary>
    public string? DropWhere(RewardKind kind, uint id, uint itemId)
    {
        if (itemId != 0 && dropItems.TryGetValue(itemId, out var byItem))
        {
            return byItem;
        }

        return id != 0 && dropRewards.TryGetValue((kind, id), out var byReward) ? byReward : null;
    }
}
