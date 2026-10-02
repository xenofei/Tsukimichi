using Tsukimichi.Core.Links;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Links;

/// <summary>
/// "Open on…" (1.8.0): the links <see cref="ExternalLinks"/> builds (encoding, the search fallbacks, the language),
/// the URL readers the link table's generator uses, <see cref="ExternalIds"/> parsing and writing, and the shipped
/// <c>external_ids.json</c> against the catalog.
/// </summary>
public class ExternalLinksTests
{
    private static readonly ExternalIds Ids = new(
        new Dictionary<uint, ExternalQuestIds>
        {
            [65537] = new("a7cbeb1c618", "A Good Adventurer Is Hard to Find"),
            [65538] = new(string.Empty, "A Bone to Pick (Quest)"),
            [65539] = new("a761973ed33", string.Empty),
        },
        new Dictionary<(RewardKind Kind, uint RewardId), uint> { [(RewardKind.Emote, 298)] = 302 });

    [Theory]
    [InlineData("English", "en")]
    [InlineData("Japanese", "ja")]
    [InlineData("German", "de")]
    [InlineData("French", "fr")]
    [InlineData("ChineseSimplified", "en")]
    [InlineData(null, "en")]
    public void The_site_language_follows_the_client(string? client, string expected) =>
        Assert.Equal(expected, ExternalLinks.SiteLanguage(client));

    [Fact]
    public void Quest_pages_come_from_the_table_and_fall_back_to_each_sites_search()
    {
        Assert.Equal("https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/a7cbeb1c618/", ExternalLinks.Quest(ExternalSite.Lodestone, 65537, "x", Ids, "en"));
        Assert.Equal("https://jp.finalfantasyxiv.com/lodestone/playguide/db/quest/a7cbeb1c618/", ExternalLinks.Quest(ExternalSite.Lodestone, 65537, "x", Ids, "ja"));
        Assert.Equal("https://de.finalfantasyxiv.com/lodestone/playguide/db/quest/a7cbeb1c618/", ExternalLinks.Quest(ExternalSite.Lodestone, 65537, "x", Ids, "de"));
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/A_Good_Adventurer_Is_Hard_to_Find", ExternalLinks.Quest(ExternalSite.ConsoleGamesWiki, 65537, "x", Ids, "en"));
        Assert.Equal("https://www.garlandtools.org/db/#quest/65537", ExternalLinks.Quest(ExternalSite.GarlandTools, 65537, "x", Ids, "en"));
        Assert.Equal("https://ffxivteamcraft.com/db/fr/quest/65537", ExternalLinks.Quest(ExternalSite.Teamcraft, 65537, "x", Ids, "fr"));
        Assert.Null(ExternalLinks.Quest(ExternalSite.FfxivCollect, 65537, "x", Ids, "en"));

        // No hash: the Lodestone's search; no title: the wiki's search, which opens a matching page outright.
        Assert.Equal("https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/?q=Ul%27dah%2C%20%22Steps%22%20%26%20more", ExternalLinks.Quest(ExternalSite.Lodestone, 65538, "Ul'dah, \"Steps\" & more", Ids, "en"));
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/Special:Search?search=Way%20of%20the%20Botanist&go=Go", ExternalLinks.Quest(ExternalSite.ConsoleGamesWiki, 65539, "Way of the Botanist", Ids, "en"));
        Assert.Equal("https://jp.finalfantasyxiv.com/lodestone/playguide/db/quest/?q=%E3%82%AF%E3%82%A8%E3%82%B9%E3%83%88", ExternalLinks.Quest(ExternalSite.Lodestone, 70000, "クエスト", Ids, "ja"));
    }

    [Fact]
    public void Wiki_titles_are_encoded_as_the_wiki_writes_them()
    {
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/A_Bone_to_Pick_(Quest)", ExternalLinks.WikiPage("A Bone to Pick (Quest)"));
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/A_Beastmaster's_Path", ExternalLinks.WikiPage("A Beastmaster's Path"));
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/%CE%91_Test_of_W%CE%B9ll", ExternalLinks.WikiPage("Α Test of Wιll"));
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/Fire_%26_Ice%3F", ExternalLinks.WikiPage("Fire & Ice?"));
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/O_Captain,_My_Captain", ExternalLinks.WikiPage("O Captain, My Captain"));
    }

    [Fact]
    public void Garland_and_Collect_links_use_ids_and_the_verified_kind_paths()
    {
        Assert.Equal("https://www.garlandtools.org/db/#item/4552", ExternalLinks.GarlandItem(4552));
        Assert.Equal("mounts", ExternalLinks.CollectPath(RewardKind.Mount));
        Assert.Equal("minions", ExternalLinks.CollectPath(RewardKind.Minion));
        Assert.Equal("emotes", ExternalLinks.CollectPath(RewardKind.Emote));
        Assert.Equal("orchestrions", ExternalLinks.CollectPath(RewardKind.Orchestrion));
        Assert.Equal("bardings", ExternalLinks.CollectPath(RewardKind.Barding));
        Assert.Equal("hairstyles", ExternalLinks.CollectPath(RewardKind.Hairstyle));
        Assert.Equal("fashions", ExternalLinks.CollectPath(RewardKind.Ornament));
        Assert.Equal("achievements", ExternalLinks.CollectPath(RewardKind.Achievement));
        Assert.Equal("triad/cards", ExternalLinks.CollectPath(RewardKind.TripleTriadCard));
        Assert.Null(ExternalLinks.CollectPath(RewardKind.Item));
        Assert.Equal(RewardKind.TripleTriadCard, ExternalLinks.CollectKind("triad/cards"));
        Assert.Null(ExternalLinks.CollectKind("quests"));

        // The table's id (FFXIV Collect's own, not always the game's), else the kind's list filtered by name.
        Assert.Equal("https://ffxivcollect.com/emotes/302", ExternalLinks.Collect(RewardKind.Emote, 298, "Wow", Ids));
        Assert.Equal("https://ffxivcollect.com/mounts?q%5Bname_en_cont%5D=Company%20Chocobo", ExternalLinks.Collect(RewardKind.Mount, 1, "Company Chocobo", Ids));
        Assert.Null(ExternalLinks.Collect(RewardKind.Action, 1, "x", Ids));

        // A copied reward row's one link: Collect, else the item, else the quest on Garland Tools.
        Assert.Equal("https://ffxivcollect.com/emotes/302", ExternalLinks.Reward(RewardKind.Emote, 298, 0, 65600, "Wow", Ids));
        Assert.Equal("https://www.garlandtools.org/db/#item/10", ExternalLinks.Reward(RewardKind.Item, 1, 10, 65600, "x", Ids));
        Assert.Equal("https://www.garlandtools.org/db/#quest/65600", ExternalLinks.Reward(RewardKind.Action, 1, 0, 65600, "x", Ids));
    }

    [Fact]
    public void The_preferred_link_is_the_Lodestone_page_else_Garland_Tools()
    {
        Assert.Equal("https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/a7cbeb1c618/", ExternalLinks.Preferred(65537, Ids, "en"));
        Assert.Equal("https://www.garlandtools.org/db/#quest/65538", ExternalLinks.Preferred(65538, Ids, "en"));
    }

    [Fact]
    public void The_generators_url_readers_take_only_real_pages()
    {
        Assert.True(ExternalLinks.TryParseLodestoneQuest("https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/a7cbeb1c618/", out var hash));
        Assert.Equal("a7cbeb1c618", hash);
        Assert.True(ExternalLinks.TryParseLodestoneQuest("https://jp.finalfantasyxiv.com/lodestone/playguide/db/quest/a7cbeb1c618", out _));
        Assert.False(ExternalLinks.TryParseLodestoneQuest("https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/", out _));
        Assert.False(ExternalLinks.TryParseLodestoneQuest("https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/?category2=1", out _));
        Assert.False(ExternalLinks.TryParseLodestoneQuest(null, out _));

        Assert.True(ExternalLinks.TryParseWikiPage("https://ffxiv.consolegameswiki.com/wiki/%CE%91_Test_of_W%CE%B9ll", out var title));
        Assert.Equal("Α Test of Wιll", title);
        Assert.True(ExternalLinks.TryParseWikiPage("https://ffxiv.consolegameswiki.com/wiki/A_Bone_to_Pick_(Quest)", out title));
        Assert.Equal("A Bone to Pick (Quest)", title);
        Assert.Equal("https://ffxiv.consolegameswiki.com/wiki/A_Bone_to_Pick_(Quest)", ExternalLinks.WikiPage(title));
        Assert.False(ExternalLinks.TryParseWikiPage("https://ffxiv.consolegameswiki.com/wiki/Special:Search?search=x", out _));
        Assert.False(ExternalLinks.TryParseWikiPage("https://example.org/wiki/X", out _));

        Assert.True(ExternalLinks.TryParseCollectEntry("https://ffxivcollect.com/triad/cards/444", out var path, out var id));
        Assert.Equal(("triad/cards", 444u), (path, id));
        Assert.False(ExternalLinks.TryParseCollectEntry("https://ffxivcollect.com/mounts", out _, out _));
        Assert.False(ExternalLinks.TryParseCollectEntry("https://ffxivcollect.com/mounts/0", out _, out _));
    }

    [Fact]
    public void The_table_round_trips_and_skips_what_does_not_parse()
    {
        var text = ExternalIds.Serialize(Ids.Quests, Ids.Collect);
        var back = ExternalIds.Parse(text);

        Assert.Empty(back.Warnings);
        Assert.Equal(Ids.Quests.OrderBy(q => q.Key), back.Quests.OrderBy(q => q.Key));
        Assert.Equal(302u, back.CollectId(RewardKind.Emote, 298));
        Assert.Null(back.CollectId(RewardKind.Emote, 1));
        Assert.Null(back.LodestoneId(65538));
        Assert.Null(back.WikiTitle(65539));
        Assert.Contains("\"65537\": [\"a7cbeb1c618\", \"A Good Adventurer Is Hard to Find\"]", text, StringComparison.Ordinal);

        var odd = ExternalIds.Parse("""{ "quests": { "x": ["a", "b"], "65540": ["NOT-HEX", "Title"], "65541": "flat" }, "collect": { "Spaceship": { "1": 2 }, "Mount": { "1": -3 } } }""");
        Assert.Equal(4, odd.Warnings.Count);
        Assert.Null(odd.LodestoneId(65540));
        Assert.Equal("Title", odd.WikiTitle(65540));
        Assert.Empty(ExternalIds.Parse("not json").Quests);
        Assert.NotEmpty(ExternalIds.Parse("not json").Warnings);
        Assert.NotEmpty(ExternalIds.Load(Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid(), "x.json")).Warnings);
    }
}

/// <summary>The shipped <c>Tsukimichi/Data/external_ids.json</c> against the frozen catalog.</summary>
public class ShippedExternalIdsTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static readonly Lazy<ExternalIds> Shipped = new(() => ExternalIds.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), ExternalIds.FileName)));

    [Fact]
    public void The_shipped_table_loads_cleanly_and_covers_most_quests()
    {
        var ids = Shipped.Value;
        Assert.Empty(ids.Warnings);

        var named = fixture.Bundle.Catalog.All.Where(q => !q.IsRemoved && q.Name.Length > 0).ToList();
        var lodestone = named.Count(q => ids.LodestoneId(q.RowId) is not null);
        var wiki = named.Count(q => ids.WikiTitle(q.RowId) is not null);
        Assert.True(lodestone >= named.Count * 0.85, $"Lodestone pages for {lodestone} of {named.Count} quests");
        Assert.True(wiki >= named.Count * 0.9, $"wiki pages for {wiki} of {named.Count} quests");

        // Every id names a quest of the catalog, and the hashes are hashes.
        Assert.All(ids.Quests.Keys, rowId => Assert.NotNull(fixture.Bundle.Catalog.GetByRowId(rowId)));
        Assert.All(ids.Quests.Values.Where(v => v.LodestoneId.Length > 0), v => Assert.True(ExternalIds.IsLodestoneHash(v.LodestoneId)));
        Assert.Equal("a7cbeb1c618", ids.LodestoneId(65537));
        Assert.Equal("A Good Adventurer Is Hard to Find", ids.WikiTitle(65537));
    }

    [Fact]
    public void The_shipped_Collect_ids_are_for_kinds_the_site_lists()
    {
        var ids = Shipped.Value;
        Assert.True(ids.Collect.Count >= 200, $"{ids.Collect.Count} FFXIV Collect ids");
        Assert.All(ids.Collect.Keys, key => Assert.NotNull(ExternalLinks.CollectPath(key.Kind)));
    }
}
