using System.Collections.Frozen;
using Tsukimichi.Core.Chains;
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
/// <param name="Abandoned">Runtime quest ids the viewed character abandoned (<see cref="Runtime.AbandonedLedger"/>), for the Abandoned filter; null keeps nothing under that filter.</param>
/// <param name="Names">Name lookups for the Status text of each row (<see cref="Evaluation.BlockerText.StatusText"/>); null uses <see cref="BlockerNames.Default"/>, which names quests from the catalog only.</param>
/// <param name="Spoilers">
/// The viewed character's spoiler shield: search matches a masked quest by its placeholder, the Name sort orders it
/// by the placeholder, and Sprout mode reads <see cref="SpoilerMask.ReachExpansion"/>. Null masks nothing and leaves
/// Sprout mode without a limit.
/// </param>
/// <param name="Stories">The story sidequests and side stories for the Story sidequests preset; null keeps nothing under it.</param>
/// <param name="JournalHits">
/// Row ids whose journal text matches the whole search (P9, <see cref="Text.JournalTextIndex"/>), already limited to
/// the quests the viewed character completed; such a quest passes the search even when its name does not match. Null
/// when journal search is off, not ready, or the query has nothing to look for in the journal.
/// </param>
/// <param name="NewSinceData">
/// Row ids newer than Tsukimichi's shipped data (<see cref="Diagnostics.DataFreshnessReport.NewQuestIds"/>), for the
/// Added in filter's "New since data" value (<see cref="FilterSet.NewSinceData"/>); null keeps nothing under it.
/// </param>
/// <param name="JustOpened">
/// Row ids the "Just opened" scope lists (<see cref="QuestScope.JustOpened"/>, 1.7.0): what the last completions opened,
/// from the "Opened:" chat line's Show link; null or empty lists nothing there.
/// </param>
/// <param name="NewGamePlus">
/// Row ids some New Game+ chapter lists (feature plan v5 collector extras, <see cref="Query.NewGamePlus"/>), for the
/// "Once-only story quests I haven't done" filter (<see cref="FilterSet.OnceOnlyStory"/>); null or empty keeps nothing
/// under it.
/// </param>
/// <param name="Chains">The story chains that filter counts as story; null leaves only <paramref name="Stories"/>.</param>
/// <param name="FreeTrial">
/// The free-trial view (<see cref="Query.FreeTrial"/>): quests beyond the trial read "Beyond your trial" instead of their
/// blocker and are listed after the rest (<see cref="QueryResult.BeyondTrial"/>).
/// </param>
/// <param name="NewlyReady">
/// Row ids the "Newly ready" scope lists (<see cref="QuestScope.NewlyReady"/>, plan v7): what the Journal badge counted
/// when it was clicked; null or empty lists nothing there.
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
    IReadOnlySet<ushort>? Abandoned = null,
    SpoilerMask? Spoilers = null,
    StorySidequests? Stories = null,
    IReadOnlySet<uint>? JournalHits = null,
    IReadOnlySet<uint>? NewSinceData = null,
    IReadOnlySet<uint>? JustOpened = null,
    IReadOnlySet<uint>? NewGamePlus = null,
    ChainCatalog? Chains = null,
    bool FreeTrial = false,
    IReadOnlySet<uint>? NewlyReady = null)
{
    public const int DefaultStalledDays = 7;

    public static readonly QueryContext Empty = new(FrozenSet<ushort>.Empty, FrozenSet<uint>.Empty, FrozenSet<uint>.Empty);
}
