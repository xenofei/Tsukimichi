using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Reverse lookups over a catalog for incremental re-evaluation: which quests depend on a given quest,
/// and which quests sit at a level or belong to a festival. Immutable; build once per catalog.
/// </summary>
public sealed class ReversePrereqIndex
{
    private static readonly uint[] Empty = [];

    private readonly FrozenDictionary<uint, uint[]> dependents;
    private readonly FrozenDictionary<byte, uint[]> byLevel;
    private readonly FrozenDictionary<ushort, uint[]> byFestival;

    private ReversePrereqIndex(
        FrozenDictionary<uint, uint[]> dependents,
        FrozenDictionary<byte, uint[]> byLevel,
        FrozenDictionary<ushort, uint[]> byFestival)
    {
        this.dependents = dependents;
        this.byLevel = byLevel;
        this.byFestival = byFestival;
    }

    public static ReversePrereqIndex Build(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var dependents = new Dictionary<uint, List<uint>>();
        var byLevel = new Dictionary<byte, List<uint>>();
        var byFestival = new Dictionary<ushort, List<uint>>();

        foreach (var quest in catalog.All)
        {
            foreach (var prereq in catalog.PrerequisitesOf(quest).QuestIds)
            {
                Add(dependents, prereq, quest.RowId);
            }

            foreach (var lockId in quest.QuestLocks)
            {
                Add(dependents, lockId, quest.RowId);
            }

            Add(byLevel, quest.Level, quest.RowId);

            if (quest.Festival != 0)
            {
                Add(byFestival, quest.Festival, quest.RowId);
            }
        }

        return new(Freeze(dependents), Freeze(byLevel), Freeze(byFestival));
    }

    /// <summary>Row ids of quests that list <paramref name="rowId"/> in their previous quests (accept conditions that name a quest included) or quest locks.</summary>
    public IReadOnlyList<uint> Dependents(uint rowId) => dependents.GetValueOrDefault(rowId) ?? Empty;

    /// <summary>Row ids of quests whose level is exactly <paramref name="level"/>.</summary>
    public IReadOnlyList<uint> ByLevel(byte level) => byLevel.GetValueOrDefault(level) ?? Empty;

    /// <summary>Row ids of quests belonging to <paramref name="festival"/>.</summary>
    public IReadOnlyList<uint> ByFestival(ushort festival) => byFestival.GetValueOrDefault(festival) ?? Empty;

    private static void Add<TKey>(Dictionary<TKey, List<uint>> map, TKey key, uint rowId)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        // Catalog order is stable, so a duplicate can only be the immediately preceding entry.
        if (list.Count == 0 || list[^1] != rowId)
        {
            list.Add(rowId);
        }
    }

    private static FrozenDictionary<TKey, uint[]> Freeze<TKey>(Dictionary<TKey, List<uint>> map)
        where TKey : notnull =>
        map.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray());
}
