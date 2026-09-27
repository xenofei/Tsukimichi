using System.Collections.Frozen;

namespace Tsukimichi.Core.Query;

/// <summary>Per-character and per-session inputs a query needs beyond the catalog and states.</summary>
/// <param name="ActiveFestivals">Festival ids the client reports as running; drives Seasonal-active only.</param>
/// <param name="Pinned">Pinned quest row ids for the viewed character.</param>
/// <param name="FeatureQuestIds">Row ids shown under the Feature Unlocks virtual node.</param>
/// <param name="NextStepText">Optional row id to "Next step" text produced by the evaluator; missing rows show empty.</param>
/// <param name="SearchIndex">Optional prebuilt index; when null the query uses <see cref="Query.SearchIndex.For"/> on the catalog.</param>
public sealed record QueryContext(
    IReadOnlySet<ushort> ActiveFestivals,
    IReadOnlySet<uint> Pinned,
    IReadOnlySet<uint> FeatureQuestIds,
    IReadOnlyDictionary<uint, string>? NextStepText = null,
    SearchIndex? SearchIndex = null)
{
    public static readonly QueryContext Empty = new(FrozenSet<ushort>.Empty, FrozenSet<uint>.Empty, FrozenSet<uint>.Empty);
}
