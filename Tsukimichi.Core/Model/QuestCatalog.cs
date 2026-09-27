using System.Collections.Frozen;

namespace Tsukimichi.Core.Model;

/// <summary>
/// Immutable index over every <see cref="QuestRecord"/>. Built once per load; all lists preserve journal sort order.
/// </summary>
public sealed class QuestCatalog
{
    public static readonly QuestCatalog Empty = new([]);

    private QuestCatalog(IReadOnlyList<QuestRecord> all)
    {
        All = all;
        ByRowId = all.ToFrozenDictionary(q => q.RowId);

        var byQuestId = new Dictionary<ushort, QuestRecord>(all.Count);
        foreach (var quest in all)
        {
            byQuestId.TryAdd(quest.QuestId, quest);
        }

        ByQuestId = byQuestId.ToFrozenDictionary();

        BySection = Group(all, q => q.Journal.SectionId);
        ByCategory = Group(all, q => q.Journal.CategoryId);
        ByGenre = Group(all, q => q.Journal.GenreId);

        LowercaseNames = all.ToFrozenDictionary(q => q.RowId, q => q.Name.ToLowerInvariant());
    }

    /// <summary>Builds a catalog. Records are ordered by <see cref="JournalRef.SortKey"/> then row id; duplicate row ids throw.</summary>
    public static QuestCatalog Build(IEnumerable<QuestRecord> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var ordered = quests
            .OrderBy(q => q.Journal.SortKey)
            .ThenBy(q => q.RowId)
            .ToArray();
        return new QuestCatalog(ordered);
    }

    public int Count => All.Count;

    /// <summary>Every quest in journal order.</summary>
    public IReadOnlyList<QuestRecord> All { get; }

    public IReadOnlyDictionary<uint, QuestRecord> ByRowId { get; }

    /// <summary>Lookup by runtime quest id (low 16 bits of the row id). First record wins on a collision.</summary>
    public IReadOnlyDictionary<ushort, QuestRecord> ByQuestId { get; }

    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> BySection { get; }
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> ByCategory { get; }
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> ByGenre { get; }

    /// <summary>Row id to lowercased name, for case-insensitive search without per-query allocation.</summary>
    public IReadOnlyDictionary<uint, string> LowercaseNames { get; }

    public QuestRecord? Get(uint rowId) => ByRowId.GetValueOrDefault(rowId);

    public QuestRecord? Get(ushort questId) => ByQuestId.GetValueOrDefault(questId);

    public bool TryGet(ushort questId, out QuestRecord quest)
    {
        if (ByQuestId.TryGetValue(questId, out var found))
        {
            quest = found;
            return true;
        }

        quest = null!;
        return false;
    }

    private static FrozenDictionary<uint, IReadOnlyList<QuestRecord>> Group(
        IReadOnlyList<QuestRecord> ordered,
        Func<QuestRecord, uint> key)
    {
        // Input is already in journal order, so each group keeps that order.
        var groups = new Dictionary<uint, List<QuestRecord>>();
        foreach (var quest in ordered)
        {
            var k = key(quest);
            if (!groups.TryGetValue(k, out var list))
            {
                list = [];
                groups[k] = list;
            }

            list.Add(quest);
        }

        return groups.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<QuestRecord>)kv.Value.ToArray());
    }
}
