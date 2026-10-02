using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.HandIn;

/// <summary>
/// Item id to the quests that ask for it (<see cref="QuestRecord.HandInItems"/>), the reverse of the detail pane's
/// "Hand in" section: what the item context menu and the hover hint read to say "Needed for: quest". Removed quests are
/// left out. Immutable; built once per catalog (<see cref="HandInIndexSource"/>). Item ids from the game carry HQ and
/// collectable offsets; every lookup normalises them first (<see cref="RewardLookup.NormalizeItemId"/>).
/// </summary>
public sealed class HandInIndex
{
    private static readonly QuestRecord[] NoQuests = [];

    /// <summary>The index of no catalog: every item needs no quest.</summary>
    public static readonly HandInIndex Empty = new(null);

    private static readonly ConditionalWeakTable<QuestCatalog, HandInIndex> Cache = [];

    private readonly FrozenDictionary<uint, QuestRecord[]> byItem;

    /// <summary>
    /// The index for a catalog, built on first use and kept while the catalog lives; safe from any thread, so the
    /// catalog worker builds it before the catalog lands (<see cref="Runtime.CatalogIndexes"/>).
    /// </summary>
    public static HandInIndex For(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, static c => new HandInIndex(c));
    }

    public HandInIndex(QuestCatalog? catalog)
    {
        Catalog = catalog;
        if (catalog is null)
        {
            byItem = FrozenDictionary<uint, QuestRecord[]>.Empty;
            return;
        }

        var grouped = new Dictionary<uint, List<QuestRecord>>();
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || quest.HandInItems.Count == 0)
            {
                continue;
            }

            foreach (var item in quest.HandInItems)
            {
                if (!grouped.TryGetValue(item.ItemId, out var list))
                {
                    grouped[item.ItemId] = list = [];
                }

                if (list.Count == 0 || list[^1].RowId != quest.RowId)
                {
                    list.Add(quest);
                }
            }
        }

        byItem = grouped.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    /// <summary>The catalog this index was built from; null for <see cref="Empty"/>.</summary>
    public QuestCatalog? Catalog { get; }

    /// <summary>Number of distinct items some quest asks for.</summary>
    public int ItemCount => byItem.Count;

    /// <summary>Every live quest that asks for the item, in catalog order. Accepts HQ and collectable ids.</summary>
    public IReadOnlyList<QuestRecord> QuestsFor(ulong itemId)
    {
        var id = RewardLookup.NormalizeItemId(itemId);
        return id == 0 ? NoQuests : byItem.GetValueOrDefault(id) ?? NoQuests;
    }

    /// <summary>
    /// The quests that ask for the item and are worth naming now: in the journal, or ready to take (on this job or
    /// another, <see cref="IsOpen"/>). Empty when none, or when the character has no states.
    /// </summary>
    public IReadOnlyList<QuestRecord> NeededFor(ulong itemId, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var all = QuestsFor(itemId);
        if (all.Count == 0 || states.Count == 0)
        {
            return NoQuests;
        }

        List<QuestRecord>? open = null;
        foreach (var quest in all)
        {
            if (states.TryGetValue(quest.RowId, out var evaluation) && IsOpen(evaluation.State))
            {
                (open ??= []).Add(quest);
            }
        }

        return open is null ? NoQuests : open;
    }

    /// <summary>
    /// A quest whose hand-in items the hint names: accepted (in the journal), Ready, or Ready on another job (a crafter
    /// quest while the character is on a combat job is exactly when the reminder helps).
    /// </summary>
    public static bool IsOpen(QuestState state) => state is QuestState.Accepted or QuestState.Ready or QuestState.ReadyOnOtherJob;
}

/// <summary>
/// Hands out the <see cref="HandInIndex"/> of the current catalog, rebuilding only when the catalog reference changes.
/// Not thread-safe; the plugin reads it on the framework thread.
/// </summary>
public sealed class HandInIndexSource
{
    private readonly Func<QuestCatalog?> catalog;
    private HandInIndex current = HandInIndex.Empty;

    public HandInIndexSource(Func<QuestCatalog?> catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public HandInIndex Current
    {
        get
        {
            var now = catalog();
            if (!ReferenceEquals(current.Catalog, now))
            {
                current = now is null ? HandInIndex.Empty : HandInIndex.For(now);
            }

            return current;
        }
    }
}
