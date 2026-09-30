using System.Collections.Frozen;
using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.Core.Query;

/// <summary>Per-character and per-session inputs a query needs beyond the catalog and states.</summary>
/// <param name="ActiveFestivals">Festival ids the client reports as running; drives Seasonal-active only.</param>
/// <param name="Pinned">Pinned quest row ids for the viewed character.</param>
/// <param name="FeatureQuestIds">Row ids shown under the Feature Unlocks virtual node and by the Feature quests preset; see <see cref="FeaturePresets.Derive"/>.</param>
/// <param name="NextStepText">
/// Row id to "Next step" text for the <see cref="Model.QuestState"/> overload of <see cref="QuestQuery.Apply(Model.QuestCatalog, IReadOnlyDictionary{uint, Model.QuestState}, FilterSet, QuestScope, SortSpec, string?, QueryContext)"/>.
/// Obsolete: pass the evaluator's <see cref="Evaluation.QuestEvaluation"/> map to the other overload and the text is read from each evaluation.
/// </param>
/// <param name="SearchIndex">Optional prebuilt index; when null the query uses <see cref="Query.SearchIndex.For"/> on the catalog.</param>
/// <param name="AcceptedSince">Runtime quest id to the UTC time it entered the journal, for the Stalled preset; null or missing ids read as unknown and never count as stalled.</param>
/// <param name="NowUtc">Clock the Stalled preset measures against.</param>
/// <param name="CurrentLevel">Unsynced level of the current job for the Around-my-level preset; 0 means unknown and the preset keeps nothing.</param>
/// <param name="StalledDays">Age in days from which an accepted quest counts as stalled.</param>
/// <param name="Names">Name lookups for the Status text of each row (<see cref="Evaluation.BlockerText.StatusText"/>); null uses <see cref="BlockerNames.Default"/>, which names quests from the catalog only.</param>
/// <param name="Spoilers">
/// The viewed character's spoiler shield: search matches a masked quest by its placeholder, the Name sort orders it
/// by the placeholder, and Sprout mode reads <see cref="SpoilerMask.ReachExpansion"/>. Null masks nothing and leaves
/// Sprout mode without a limit.
/// </param>
public sealed record QueryContext(
    IReadOnlySet<ushort> ActiveFestivals,
    IReadOnlySet<uint> Pinned,
    IReadOnlySet<uint> FeatureQuestIds,
    [property: Obsolete("Use the QuestEvaluation overload of QuestQuery.Apply; the Status text is built from each evaluation.")]
    IReadOnlyDictionary<uint, string>? NextStepText = null,
    SearchIndex? SearchIndex = null,
    IReadOnlyDictionary<ushort, DateTime>? AcceptedSince = null,
    DateTime NowUtc = default,
    byte CurrentLevel = 0,
    int StalledDays = QueryContext.DefaultStalledDays,
    BlockerNames? Names = null,
    SpoilerMask? Spoilers = null)
{
    public const int DefaultStalledDays = 7;

    public static readonly QueryContext Empty = new(FrozenSet<ushort>.Empty, FrozenSet<uint>.Empty, FrozenSet<uint>.Empty);
}
