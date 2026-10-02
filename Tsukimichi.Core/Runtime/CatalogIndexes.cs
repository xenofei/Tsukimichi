using System.Diagnostics;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Runtime;

/// <summary>How long one derived index took to build, for the log line of a catalog landing.</summary>
public readonly record struct IndexTiming(string Name, double Milliseconds);

/// <summary>
/// Everything derived from a catalog that the session swaps in with it, built on the catalog worker so the frame the
/// catalog lands on only assigns references: the reverse prerequisite index, the feature ("blue") quests, story
/// sidequests and the chain catalog. The indexes the panes build on first use and cache with the catalog (the path
/// choices, the main scenario graph, the search and patch indexes, the hand-in and quest-title indexes) are built
/// here too, so the first draw after a landing finds them ready: together they took 30–110 ms of that frame.
/// Pure apart from those caches, which are safe from any thread.
/// </summary>
/// <param name="Index">Quests by the quests they need, by level and by festival (<see cref="ReversePrereqIndex"/>).</param>
/// <param name="FeatureQuestIds">The derived feature quests (<see cref="FeaturePresets.Derive"/>).</param>
/// <param name="Stories">Story sidequests and their side stories; <see cref="StorySidequests.Empty"/> when they failed.</param>
/// <param name="Chains">Every chain, side stories included; <see cref="ChainCatalog.Empty"/> when they failed.</param>
/// <param name="ChainsError">Why the story sidequests or chains could not be built; null when they were.</param>
/// <param name="Timings">Each part's build time, in build order.</param>
public sealed record CatalogIndexes(
    ReversePrereqIndex Index,
    IReadOnlySet<uint> FeatureQuestIds,
    StorySidequests Stories,
    ChainCatalog Chains,
    Exception? ChainsError,
    IReadOnlyList<IndexTiming> Timings)
{
    /// <summary>
    /// Builds every index for <paramref name="catalog"/>. The reverse index and the feature quests throw on failure
    /// (the catalog is then unusable, as before); the story sidequests and chains fall back to empty with
    /// <see cref="ChainsError"/> set; a cache warm-up that fails is left for the first draw to build (and report).
    /// </summary>
    public static CatalogIndexes Build(QuestCatalog catalog, CuratedData curated, IEnumerable<UniqueRewardEntry> uniqueRewards)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(curated);
        ArgumentNullException.ThrowIfNull(uniqueRewards);

        var timings = new List<IndexTiming>();
        T Timed<T>(string name, Func<T> build)
        {
            var started = Stopwatch.GetTimestamp();
            var value = build();
            timings.Add(new IndexTiming(name, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            return value;
        }

        var entries = uniqueRewards as IReadOnlyCollection<UniqueRewardEntry> ?? uniqueRewards.ToList();
        var index = Timed("reverse prerequisites", () => ReversePrereqIndex.Build(catalog));
        var featureQuestIds = Timed("feature quests", () => FeaturePresets.Derive(catalog, curated, entries));

        var stories = StorySidequests.Empty;
        var chains = ChainCatalog.Empty;
        Exception? chainsError = null;
        try
        {
            stories = Timed("story sidequests", () => StorySidequests.Build(catalog, featureQuestIds, curated, entries));
            chains = Timed("chains", () => ChainCatalog.Build(catalog, curated, stories));
        }
        catch (Exception ex)
        {
            stories = StorySidequests.Empty;
            chains = ChainCatalog.Empty;
            chainsError = ex;
        }

        // The caches the first draw would otherwise fill on the framework thread.
        Warm(timings, "path choices", () => PathIndex.For(catalog));
        Warm(timings, "main scenario graph", () => MsqGraph.For(catalog));
        Warm(timings, "search", () => SearchIndex.For(catalog));
        Warm(timings, "patches", () => PatchIndex.For(catalog));
        Warm(timings, "hand-in items", () => HandInIndex.For(catalog));
        Warm(timings, "quest titles", () => QuestTitleIndex.For(catalog));

        return new CatalogIndexes(index, featureQuestIds, stories, chains, chainsError, timings);
    }

    /// <summary>Total build time of every part.</summary>
    public double TotalMilliseconds => Timings.Sum(static t => t.Milliseconds);

    /// <summary>"reverse prerequisites 9.8 ms, feature quests 2.8 ms, …" for the log.</summary>
    public string Describe() =>
        string.Join(", ", Timings.Select(static t => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{t.Name} {t.Milliseconds:0.0} ms")));

    private static void Warm(List<IndexTiming> timings, string name, Func<object> build)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            build();
        }
        catch (Exception)
        {
            // Left to the first use, which builds it again on the framework thread and reports the failure there.
            return;
        }

        timings.Add(new IndexTiming(name, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
    }
}
