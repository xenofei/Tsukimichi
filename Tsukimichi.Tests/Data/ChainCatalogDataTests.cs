using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>Chains over the real Quest sheet and the shipped <c>curated/chains.json</c>.</summary>
public class ChainCatalogDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    /// <summary>The plugin's shipped curated directory, found by walking up from the test assembly to the solution root.</summary>
    private static string CuratedDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return Path.Combine(dir, "Tsukimichi", "Data", "curated");
    }

    [GameDataFact]
    public void At_least_five_journal_genres_are_Hildibrand()
    {
        var hildibrand = Catalog.ByGenre
            .Where(kv => kv.Key != 0 && kv.Value[0].Journal.GenreName.Contains("Hildibrand", StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .OrderBy(id => id)
            .ToArray();

        foreach (var id in hildibrand)
        {
            output.WriteLine($"genre {id}: {Catalog.ByGenre[id][0].Journal.GenreName} ({Catalog.ByGenre[id].Count} quests)");
        }

        Assert.True(hildibrand.Length >= 5, $"expected at least 5 Hildibrand genres, found {hildibrand.Length}");
        Assert.Equal([82u, 83u, 84u, 85u, 87u], hildibrand);
        Assert.All(hildibrand, id => Assert.True(ChainCatalog.IsLinear(Catalog.ByGenre[id]), $"genre {id} is not linear"));
    }

    [GameDataFact]
    public void Shipped_chains_resolve_every_genre_and_span_the_sheet()
    {
        var curated = CuratedData.Load(CuratedDir());
        Assert.DoesNotContain(curated.Warnings, w => w.StartsWith(CuratedData.ChainsFileName, StringComparison.Ordinal));
        Assert.True(curated.Chains.Count >= 20, $"expected the seeded chains, found {curated.Chains.Count}");

        var chains = ChainCatalog.Build(Catalog, curated);
        Assert.Empty(chains.Warnings);

        foreach (var entry in curated.Chains)
        {
            var chain = chains.Chains.First(c => c.Name == entry.Name);
            output.WriteLine($"{chain.Name}: genres [{string.Join(", ", entry.GenreIds)}], {chain.RowIds.Count} quests");
            Assert.Equal(entry.GenreIds.Sum(id => Catalog.ByGenre[id].Count), chain.RowIds.Count);
            Assert.All(chain.RowIds, id => Assert.Same(chain, chains.ForQuest(id)));
        }

        // Hildibrand: the 57-quest chain from ARR to Endwalker, first quest first.
        var hildibrand = chains.Chains.First(c => c.Name == "Hildibrand");
        Assert.Equal(57, hildibrand.RowIds.Count);
        Assert.Equal(Catalog.ByGenre[82][0].RowId, hildibrand.RowIds[0]);
        Assert.Equal(Catalog.ByGenre[87][^1].RowId, hildibrand.RowIds[^1]);

        // Omega and Pandaemonium merge two genres each.
        Assert.Equal(17, chains.Chains.First(c => c.Name == "Omega").RowIds.Count);
        Assert.Equal(17, chains.Chains.First(c => c.Name == "Pandæmonium").RowIds.Count);
    }

    [GameDataFact]
    public void Derived_chains_cover_linear_genres_and_qualify_repeated_names()
    {
        var chains = ChainCatalog.Build(Catalog, CuratedData.Empty);
        output.WriteLine($"{chains.Chains.Count} derived chains");

        // Bahamut (genre 17) is a single line of six quests.
        var bahamut = chains.ForQuest(Catalog.ByGenre[17][0].RowId);
        Assert.NotNull(bahamut);
        Assert.Equal("Bahamut Quests", bahamut.Name);
        Assert.Equal(6, bahamut.RowIds.Count);

        // Seventh Umbral Era opens with three city starts, so the ARR MSQ genre is not a line.
        Assert.Null(chains.ForQuest(65621u));

        // Every allied society has a "Main Quests" genre; the chain name says which.
        var amaljaa = chains.ForQuest(Catalog.ByGenre[40][0].RowId);
        Assert.NotNull(amaljaa);
        Assert.Equal("Main Quests (Amalj'aa Quests)", amaljaa.Name);

        Assert.All(chains.Chains, c => Assert.True(c.RowIds.Count >= ChainCatalog.MinChainLength));
        Assert.True(chains.Chains.Count >= 80, $"expected dozens of derived chains, found {chains.Chains.Count}");
    }
}
