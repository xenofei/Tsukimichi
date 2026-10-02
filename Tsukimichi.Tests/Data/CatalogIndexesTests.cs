using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The derived indexes the catalog worker builds before a catalog lands (<see cref="CatalogIndexes"/>) are the ones the
/// session built on the framework thread before, and the caches the first draw used to fill are filled with them.
/// </summary>
public class CatalogIndexesTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static UniqueRewardsData Unique() => UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));

    [Fact]
    public void The_worker_builds_what_the_session_built_on_the_framework_thread()
    {
        var catalog = fixture.Bundle.Catalog;
        var unique = Unique();

        var indexes = CatalogIndexes.Build(catalog, fixture.Curated, unique.Entries);

        var features = FeaturePresets.Derive(catalog, fixture.Curated, unique.Entries);
        var stories = StorySidequests.Build(catalog, features, fixture.Curated, unique.Entries);
        var chains = ChainCatalog.Build(catalog, fixture.Curated, stories);
        Assert.Null(indexes.ChainsError);
        Assert.True(features.SetEquals(indexes.FeatureQuestIds));
        Assert.Equal(stories.Count, indexes.Stories.Count);
        Assert.Equal(chains.Chains.Count, indexes.Chains.Chains.Count);

        var reverse = ReversePrereqIndex.Build(catalog);
        foreach (var quest in catalog.All.Take(500))
        {
            Assert.Equal(reverse.Dependents(quest.RowId).Order(), indexes.Index.Dependents(quest.RowId).Order());
        }
    }

    [Fact]
    public void The_first_draw_finds_its_caches_filled()
    {
        var catalog = fixture.Bundle.Catalog;

        var indexes = CatalogIndexes.Build(catalog, fixture.Curated, Unique().Entries);

        Assert.Equal(
            ["reverse prerequisites", "feature quests", "story sidequests", "chains", "path choices", "main scenario graph", "search", "patches", "hand-in items", "quest titles"],
            indexes.Timings.Select(static t => t.Name));
        Assert.Same(PathIndex.For(catalog), PathIndex.For(catalog));
        Assert.Same(HandInIndex.For(catalog), new HandInIndexSource(() => catalog).Current);
        Assert.Same(QuestTitleIndex.For(catalog), QuestTitleIndex.For(catalog));
        Assert.True(QuestTitleIndex.For(catalog).Count > 0);
        Assert.Contains("reverse prerequisites", indexes.Describe(), StringComparison.Ordinal);
        Assert.True(indexes.TotalMilliseconds >= 0);
    }

    [Fact]
    public void A_source_without_a_catalog_hands_out_the_empty_index()
    {
        Assert.Same(HandInIndex.Empty, new HandInIndexSource(static () => null).Current);
    }
}

/// <summary>
/// What a catalog landing cost the framework thread before 1.8.0, now spent on the catalog worker: every derived index
/// built cold on a catalog no test touched (its own class fixture), then the stored character's resolve.
/// </summary>
public class CatalogIndexesPerfTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>Generous for CI; the owner's machine builds them in about a hundred milliseconds.</summary>
    public const int BudgetMs = 3000;

    [Fact]
    [Trait("Category", "Perf")]
    public void Every_derived_index_builds_off_the_framework_thread_within_budget()
    {
        var catalog = fixture.Bundle.Catalog;
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));

        var indexes = CatalogIndexes.Build(catalog, fixture.Curated, unique.Entries);

        output.WriteLine($"CatalogIndexes over {catalog.Count} quests, cold: {indexes.TotalMilliseconds:0.0} ms ({indexes.Describe()})");
        Assert.True(indexes.TotalMilliseconds < BudgetMs, $"derived indexes took {indexes.TotalMilliseconds:0} ms, budget {BudgetMs} ms");

        // Warm: a second catalog landing of the same instance finds every cache filled.
        var again = CatalogIndexes.Build(catalog, fixture.Curated, unique.Entries);
        output.WriteLine($"Again, caches warm: {again.TotalMilliseconds:0.0} ms ({again.Describe()})");
    }
}
