using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Tsukimichi.Core.Model;

/// <summary>
/// Immutable index over every <see cref="QuestRecord"/>. Built once per load; all lists preserve journal sort order.
/// <para>
/// Two id spaces meet here. The Quest sheet <b>row id</b> (65536 + n) is what <see cref="QuestRecord.RowId"/>,
/// previous quests, quest locks, pins, overrides and unique-reward entries carry. The runtime <b>quest id</b> is its
/// low 16 bits (<see cref="QuestRecord.QuestId"/>), what the completion bitmask, the journal and daily flags use.
/// Every lookup names the space it takes; the untyped <c>Get</c> overloads are kept only for older callers.
/// </para>
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

    /// <summary>Keyed by Quest sheet row id.</summary>
    public IReadOnlyDictionary<uint, QuestRecord> ByRowId { get; }

    /// <summary>Keyed by runtime quest id (low 16 bits of the row id). First record wins on a collision.</summary>
    public IReadOnlyDictionary<ushort, QuestRecord> ByQuestId { get; }

    /// <summary>Keyed by JournalSection id. Unlisted quests (genre 0) are listed under whatever section id the sheet gave them; the tree and query layers never show them there.</summary>
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> BySection { get; }

    /// <summary>Keyed by JournalCategory id; see <see cref="BySection"/> for how unlisted quests are treated.</summary>
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> ByCategory { get; }

    /// <summary>Keyed by JournalGenre id; key 0 holds every unlisted quest.</summary>
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> ByGenre { get; }

    /// <summary>Lookup by Quest sheet row id (65536 + n): the id prerequisites, locks, pins and unique-reward entries carry.</summary>
    public QuestRecord? GetByRowId(uint rowId) => ByRowId.GetValueOrDefault(rowId);

    /// <summary>Lookup by runtime quest id (low 16 bits of the row id): the id the completion bitmask, journal and daily flags use.</summary>
    public QuestRecord? GetByQuestId(ushort questId) => ByQuestId.GetValueOrDefault(questId);

    /// <summary>Lookup by Quest sheet row id; see <see cref="GetByRowId"/>.</summary>
    public bool TryGetByRowId(uint rowId, [NotNullWhen(true)] out QuestRecord? quest) => ByRowId.TryGetValue(rowId, out quest);

    /// <summary>Lookup by runtime quest id; see <see cref="GetByQuestId"/>.</summary>
    public bool TryGetByQuestId(ushort questId, [NotNullWhen(true)] out QuestRecord? quest) => ByQuestId.TryGetValue(questId, out quest);

    /// <summary>Lookup by Quest sheet row id. Kept for older callers; the name does not say which id it takes.</summary>
    [Obsolete("Use GetByRowId: this overload takes a Quest sheet row id (65536 + n), not a runtime quest id.")]
    public QuestRecord? Get(uint rowId) => GetByRowId(rowId);

    /// <summary>Lookup by runtime quest id. Kept for older callers; the name does not say which id it takes.</summary>
    [Obsolete("Use GetByQuestId: this overload takes a runtime quest id (low 16 bits), not a row id.")]
    public QuestRecord? Get(ushort questId) => GetByQuestId(questId);

    /// <summary>Lookup by runtime quest id. Kept for older callers; see <see cref="TryGetByQuestId"/>.</summary>
    [Obsolete("Use TryGetByQuestId: this overload takes a runtime quest id (low 16 bits), not a row id.")]
    public bool TryGet(ushort questId, out QuestRecord quest)
    {
        if (TryGetByQuestId(questId, out var found))
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
