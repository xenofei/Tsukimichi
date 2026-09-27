using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class SearchIndexTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(65575, "Coming to Gridania", internalId: "ManFst001_00039"),
        Quest(65576, "Close to Home", internalId: "ManFst002_00040", rewards: [Reward(RewardKind.Emote, "Hum", 5)]),
        Quest(66000, "The Ultimate Weapon", rewards: [Reward(RewardKind.Item, "Magitek Armor Identification Key", 6008), Reward(RewardKind.Mount, "Magitek Armor", 10)]),
        Quest(70000, "Quest 2020", genre: 0),
    ]);

    private static readonly SearchIndex Index = SearchIndex.Build(Catalog);

    [Theory]
    [InlineData("close", 65576u, true)]
    [InlineData("CLOSE", 65576u, true)]
    [InlineData("  Close   Home ", 65576u, true)]
    [InlineData("home close", 65576u, true)]
    [InlineData("close gridania", 65576u, false)]
    [InlineData("gridania", 65575u, true)]
    [InlineData("", 65575u, true)]
    [InlineData("   ", 65575u, true)]
    public void Matches_name_terms(string query, uint rowId, bool expected)
    {
        Assert.Equal(expected, Index.Matches(rowId, SearchIndex.Normalize(query)));
    }

    [Fact]
    public void Matches_reward_names()
    {
        Assert.True(Index.Matches(66000, SearchIndex.Normalize("magitek")));
        Assert.True(Index.Matches(66000, SearchIndex.Normalize("identification key")));
        Assert.True(Index.Matches(65576, SearchIndex.Normalize("hum")));
        Assert.False(Index.Matches(65575, SearchIndex.Normalize("hum")));
    }

    [Fact]
    public void Matches_internal_id()
    {
        Assert.True(Index.Matches(65575, SearchIndex.Normalize("manfst001")));
        Assert.False(Index.Matches(65576, SearchIndex.Normalize("manfst001")));
    }

    [Fact]
    public void Numeric_terms_match_row_id_and_quest_id_exactly()
    {
        Assert.True(Index.Matches(65576, SearchIndex.Normalize("65576")));
        Assert.True(Index.Matches(65576, SearchIndex.Normalize("40")));
        Assert.False(Index.Matches(65576, SearchIndex.Normalize("4")));
        Assert.False(Index.Matches(65576, SearchIndex.Normalize("6557")));
        Assert.False(Index.Matches(65575, SearchIndex.Normalize("40")));
    }

    [Fact]
    public void Numeric_terms_also_match_digits_inside_names()
    {
        Assert.True(Index.Matches(70000, SearchIndex.Normalize("2020")));
        Assert.True(Index.Matches(70000, SearchIndex.Normalize("quest 2020")));
    }

    [Fact]
    public void Mixed_terms_all_must_match()
    {
        Assert.True(Index.Matches(65576, SearchIndex.Normalize("close 40")));
        Assert.False(Index.Matches(65576, SearchIndex.Normalize("close 39")));
    }

    [Fact]
    public void Unknown_row_never_matches_a_query()
    {
        Assert.False(Index.Matches(1, SearchIndex.Normalize("close")));
        Assert.True(Index.Matches(1, SearchIndex.Normalize("")));
    }

    [Fact]
    public void For_caches_one_index_per_catalog()
    {
        var a = SearchIndex.For(Catalog);
        var b = SearchIndex.For(Catalog);
        Assert.Same(a, b);
        Assert.Equal(Catalog.Count, a.Count);
    }
}
