using System.Runtime.CompilerServices;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>One patch series in the "Added in" filter: "7.5" (shown "7.5x") and how many quests it added.</summary>
public readonly record struct PatchSeries(string Series, int Quests);

/// <summary>
/// The patches a catalog's quests were added in (P8, <see cref="QuestRecord.AddedIn"/>), over the quests still in the
/// game (removed quests are never "new" and never offered as a filter value). Built once per catalog and cached with
/// it, like <see cref="SearchIndex"/>.
/// </summary>
public sealed class PatchIndex
{
    private static readonly ConditionalWeakTable<QuestCatalog, PatchIndex> Cache = [];

    private PatchIndex(string newest, IReadOnlyList<PatchSeries> series, int known)
    {
        Newest = newest;
        NewestSeries = PatchVersion.Series(newest);
        Series = series;
        Known = known;
        NewestSeriesCount = series.Count > 0 && series[0].Series == NewestSeries ? series[0].Quests : 0;
    }

    /// <summary>The newest patch any quest in the game carries ("7.56"); empty when no quest has a patch.</summary>
    public string Newest { get; }

    /// <summary>
    /// The series of <see cref="Newest"/> ("7.5", shown "7.5x": 7.5, 7.51, 7.55, 7.56): what "new" means for the
    /// Unlocks quick view's first group, the same unit as the "Added in" filter. Empty when no quest has a patch.
    /// </summary>
    public string NewestSeries { get; }

    /// <summary>How many quests in the game were added in <see cref="NewestSeries"/>.</summary>
    public int NewestSeriesCount { get; }

    /// <summary>Every series some quest was added in, newest first ("7.5", "7.4" … "2.0").</summary>
    public IReadOnlyList<PatchSeries> Series { get; }

    /// <summary>Quests in the game with a known patch.</summary>
    public int Known { get; }

    /// <summary>Whether the quest is new: still in the game and added in <see cref="NewestSeries"/>.</summary>
    public bool IsNew(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return NewestSeries.Length > 0 && !quest.IsRemoved && PatchVersion.InSeries(quest.AddedIn, NewestSeries);
    }

    /// <summary>The index for <paramref name="catalog"/>, built on first use and kept while the catalog lives.</summary>
    public static PatchIndex For(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, Build);
    }

    private static PatchIndex Build(QuestCatalog catalog)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var newest = string.Empty;
        var known = 0;
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || !PatchVersion.IsPatch(quest.AddedIn))
            {
                continue;
            }

            known++;
            if (newest.Length == 0 || PatchVersion.Compare(quest.AddedIn, newest) > 0)
            {
                newest = quest.AddedIn;
            }

            var series = PatchVersion.Series(quest.AddedIn);
            counts[series] = counts.GetValueOrDefault(series) + 1;
        }

        var list = counts.Select(kv => new PatchSeries(kv.Key, kv.Value)).OrderBy(s => s.Series, PatchVersion.NewestFirst).ToArray();
        return new PatchIndex(newest, list, known);
    }
}
