using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The hero banner chain (feature plan V4: every quest has art): own journal banner, then a sibling's in the chain or
/// genre, then a duty's, then the zone's loading image, then the bundled category art; and the category art mapping,
/// held against every genre of the frozen catalog.
/// </summary>
public sealed class BannerResolverTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private sealed class FakeLookups(Dictionary<uint, (uint Icon, uint Cfc)> duties, Dictionary<uint, string> zones) : IBannerLookups
    {
        public bool TryGetDutyBanner(QuestRecord quest, out uint iconId, out uint contentFinderConditionId)
        {
            if (duties.TryGetValue(quest.RowId, out var hit))
            {
                (iconId, contentFinderConditionId) = hit;
                return true;
            }

            iconId = contentFinderConditionId = 0;
            return false;
        }

        public string? ZoneBannerPath(uint territoryId) => zones.GetValueOrDefault(territoryId);
    }

    private static QuestRecord Quest(uint row, uint genre, uint icon = 0, uint[]? previous = null, uint territory = 0, int sort = 0,
        uint section = BannerArts.SidequestSection, uint category = 59, byte expansion = 0) => new()
    {
        RowId = row,
        Name = "Q" + row.ToString(CultureInfo.InvariantCulture),
        Journal = new JournalRef(section, "S", category, "C", genre, "G", sort),
        Icon = icon,
        Expansion = expansion,
        PreviousQuests = previous is null ? Prereq.None : new Prereq(previous, JoinKind.All),
        Issuer = territory == 0 ? null : new Issuer(1, "N", territory, 1, 0, 0, 0),
    };

    [Fact]
    public void The_chain_takes_the_first_source_that_has_art_in_order()
    {
        var own = Quest(1, 112, icon: 100001, territory: 134, sort: 1);
        var donor = Quest(2, 112, icon: 100002, sort: 2);
        var sibling = Quest(3, 112, territory: 134, sort: 3);
        var dutyOnly = Quest(4, 0, territory: 134, sort: 4);
        var zoneOnly = Quest(5, 0, territory: 134, sort: 5);
        var nothing = Quest(6, 0, territory: 999, sort: 6);
        var catalog = QuestCatalog.Build([own, donor, sibling, dutyOnly, zoneOnly, nothing]);
        var lookups = new FakeLookups(new() { [3] = (112005, 4), [4] = (112005, 4) }, new() { [134] = "ui/loadingimage/-nowloading_base01_hr1.tex" });

        var index = BannerIndex.Build(catalog, lookups);

        Assert.Equal(new BannerChoice(BannerSource.Own, 100001, null, BannerArt.SideLaNoscea, 0), index.For(own));
        Assert.Equal(new BannerChoice(BannerSource.Sibling, 100002, null, BannerArt.SideLaNoscea, 2), index.For(sibling));
        Assert.Equal(new BannerChoice(BannerSource.Duty, 112005, null, BannerArt.Other, 4), index.For(dutyOnly));
        Assert.Equal(BannerSource.Zone, index.For(zoneOnly).Source);
        Assert.Equal("ui/loadingimage/-nowloading_base01_hr1.tex", index.For(zoneOnly).GamePath);
        Assert.Equal(134u, index.For(zoneOnly).SourceId);
        Assert.Equal(BannerChoice.ForArt(BannerArt.Other), index.For(nothing));
        Assert.Equal([0, 2, 1, 1, 1, 1], index.Counts);

        // Without sheet lookups the duty and zone steps are skipped.
        Assert.Equal(BannerSource.Category, BannerIndex.Build(catalog, null).For(zoneOnly).Source);
    }

    [Fact]
    public void A_sibling_comes_from_the_chain_before_the_nearest_in_journal_order()
    {
        // 10 has art and starts a chain 10 → 11 → 12; 13 (art) sits right next to 12 in journal order.
        var head = Quest(10, 150, icon: 100010, sort: 1);
        var mid = Quest(11, 150, previous: [10], sort: 5);
        var tail = Quest(12, 150, previous: [11], sort: 6);
        var neighbour = Quest(13, 150, icon: 100013, sort: 7);
        var catalog = QuestCatalog.Build([head, mid, tail, neighbour]);

        var donors = BannerIndex.SiblingDonors(catalog);

        Assert.Equal(10u, donors[12].RowId);
        Assert.Equal(10u, donors[11].RowId);
        Assert.False(donors.ContainsKey(10));
    }

    [Fact]
    public void The_nearest_in_the_genre_wins_and_a_tie_goes_to_the_earlier_quest()
    {
        var a = Quest(20, 151, icon: 100020, sort: 1);
        var b = Quest(21, 151, sort: 2);
        var c = Quest(22, 151, icon: 100022, sort: 3);
        var d = Quest(23, 151, sort: 4);
        var e = Quest(24, 151, sort: 5);
        var other = Quest(25, 152, icon: 100025, sort: 6);
        var donors = BannerIndex.SiblingDonors(QuestCatalog.Build([a, b, c, d, e, other]));

        Assert.Equal(20u, donors[21].RowId);   // tie between 20 and 22: the earlier
        Assert.Equal(22u, donors[23].RowId);
        Assert.Equal(22u, donors[24].RowId);   // never across genres
    }

    [Fact]
    public void Unlisted_quests_borrow_no_sibling_banner()
    {
        var catalog = QuestCatalog.Build([Quest(30, 0, icon: 100030, sort: 1), Quest(31, 0, sort: 2)]);
        Assert.Empty(BannerIndex.SiblingDonors(catalog));
    }

    [Fact]
    public void Category_art_follows_section_category_and_expansion()
    {
        Assert.Equal(BannerArt.MsqArr, BannerArts.For(0, 1, 1, 0));
        Assert.Equal(BannerArt.MsqEw, BannerArts.For(0, 12, 12, 4));
        Assert.Equal(BannerArt.MsqDt, BannerArts.For(1, 13, 13, 5));
        Assert.Equal(BannerArt.Chronicles, BannerArts.For(2, 22, 22, 2));
        Assert.Equal(BannerArt.Chronicles, BannerArts.For(3, 54, 79, 1));
        Assert.Equal(BannerArt.Hildibrand, BannerArts.For(3, 55, 82, 0));
        Assert.Equal(BannerArt.Relic, BannerArts.For(3, 56, 91, 3));
        Assert.Equal(BannerArt.Relic, BannerArts.For(3, 57, 103, 1));
        Assert.Equal(BannerArt.SideLaNoscea, BannerArts.For(3, 59, 112, 0));
        Assert.Equal(BannerArt.SideBlackShroud, BannerArts.For(3, 60, 113, 0));
        Assert.Equal(BannerArt.SideThanalan, BannerArts.For(3, 61, 114, 0));
        Assert.Equal(BannerArt.SideCoerthas, BannerArts.For(3, 63, 116, 0));
        Assert.Equal(BannerArt.SideDravania, BannerArts.For(3, 67, 123, 1));
        Assert.Equal(BannerArt.SideGyrAbaniaOthard, BannerArts.For(3, 70, 128, 2));
        Assert.Equal(BannerArt.SideNorvrandt, BannerArts.For(3, 76, 139, 3));
        Assert.Equal(BannerArt.SideIlsabard, BannerArts.For(3, 82, 147, 4));
        Assert.Equal(BannerArt.SideTural, BannerArts.For(3, 85, 155, 5));
        Assert.Equal(BannerArt.SideTural, BannerArts.For(3, 86, 300, 5));   // a later region, by expansion
        Assert.Equal(BannerArt.AlliedSocieties, BannerArts.For(5, 32, 34, 5));
        Assert.Equal(BannerArt.ClassJob, BannerArts.For(6, 95, 217, 3));
        Assert.Equal(BannerArt.GrandCompany, BannerArts.For(7, 96, 234, 0));
        Assert.Equal(BannerArt.Seasonal, BannerArts.For(7, 97, 246, 0));
        Assert.Equal(BannerArt.Other, BannerArts.For(7, 98, 251, 0));
        Assert.Equal(BannerArt.Other, BannerArts.For(3, 1, 0, 0));        // unlisted
    }

    [Fact]
    public void Every_genre_of_the_catalog_maps_to_a_bundled_banner()
    {
        var catalog = fixture.Bundle.Catalog;
        var assets = Path.Combine(OrnamentLayoutTests.AssetsDir(), "banners");
        var knownSections = new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        var genres = catalog.ByGenre.Where(g => g.Key != 0).OrderBy(g => g.Key).ToArray();
        Assert.True(genres.Length > 200, $"only {genres.Length} genres");

        var byArt = new Dictionary<BannerArt, int>();
        foreach (var (genreId, quests) in genres)
        {
            var listed = quests.FirstOrDefault(q => !q.IsRemoved) ?? quests[0];
            var j = listed.Journal;
            Assert.Contains(j.SectionId, knownSections);
            if (j.SectionId == BannerArts.SidequestSection && j.CategoryId is not (>= 54 and <= 58))
            {
                Assert.True(BannerArts.IsMappedRegion(j.CategoryId), $"sidequest category {j.CategoryId} {j.CategoryName} has no region family");
            }

            // Each quest of the genre gets an art whose file ships.
            foreach (var quest in quests)
            {
                var art = BannerArts.For(quest);
                Assert.True(File.Exists(Path.Combine(assets, BannerArts.Slug(art) + "@2x.png")), $"genre {genreId}: {art}");
            }

            var genreArt = BannerArts.For(listed);
            byArt[genreArt] = byArt.GetValueOrDefault(genreArt) + 1;
        }

        // Every bundled art is used by some genre.
        foreach (var art in BannerArts.All)
        {
            Assert.True(byArt.ContainsKey(art), $"{art} backs no genre");
            output.WriteLine($"{art,-22} {byArt[art],3} genres");
        }
    }

    [Fact]
    public void Every_bundled_banner_ships_at_2x_with_its_source_and_the_set_stays_small()
    {
        var assets = Path.Combine(OrnamentLayoutTests.AssetsDir(), "banners");
        var sources = Path.Combine(OrnamentLayoutTests.RepoRoot(), "docs", "design", "moon-road", "banners", "src");
        long total = 0;
        foreach (var art in BannerArts.All)
        {
            var png = Path.Combine(assets, BannerArts.Slug(art) + "@2x.png");
            Assert.Equal((BannerArts.PixelWidth, BannerArts.PixelHeight), OrnamentLayoutTests.PngSize(png));
            Assert.True(File.Exists(Path.Combine(sources, BannerArts.Slug(art) + ".svg")), art.ToString());
            Assert.Equal("Tsukimichi.assets.ui.banners." + BannerArts.Slug(art) + "@2x.png", BannerArts.ResourceName(art));
            total += new FileInfo(png).Length;
        }

        Assert.Equal(BannerArts.All.Count, Directory.GetFiles(assets, "*.png").Length);
        Assert.True(total < 1_500_000, $"banners total {total} bytes");
        output.WriteLine($"{BannerArts.All.Count} banners, {total / 1024.0:F1} KB");
    }

    [Fact]
    public void Every_quest_of_the_frozen_catalog_resolves_to_art()
    {
        var catalog = fixture.Bundle.Catalog;
        var index = BannerIndex.Build(catalog, null);
        Assert.Equal(catalog.Count, index.Count);
        foreach (var quest in catalog.All)
        {
            var choice = index.For(quest);
            Assert.NotEqual(BannerSource.None, choice.Source);
            Assert.True(choice.Source is BannerSource.Category || choice.IconId != 0, quest.Name);
        }

        Report(output, "frozen catalog, no sheet lookups", index, catalog);
    }

    [GameDataFact]
    public void Hit_rates_over_the_live_catalog()
    {
        using var game = new GameDataFixture();
        var catalog = game.Bundle.Catalog;
        var rewards = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var unlocks = DutyUnlockIndex.Build(fixture.Curated, UniqueRewardCatalog.Build(rewards, new Dictionary<uint, UniqueOverride>(), fixture.Curated));
        var sources = BannerSources.Build(game.Game.Excel, catalog, unlocks);
        var index = sources.Resolve(catalog);

        Assert.Equal(catalog.Count, index.Count);
        Assert.True(sources.ZoneCount > 500, $"{sources.ZoneCount} territories with a loading image");
        Assert.True(sources.DutyQuestCount > 100, $"{sources.DutyQuestCount} quests with a duty banner");

        // Every zone path and a sample of the banner icons exist in the game data.
        foreach (var path in catalog.All.Select(q => index.For(q)).Where(c => c.Source == BannerSource.Zone).Select(c => c.GamePath!).Distinct())
        {
            Assert.True(game.Game.FileExists(path), path);
        }

        foreach (var icon in catalog.All.Select(q => index.For(q)).Where(c => c.IconId != 0).Select(c => c.IconId).Distinct().Take(400))
        {
            var path = $"ui/icon/{icon / 1000 * 1000:D6}/{icon:D6}_hr1.tex";
            Assert.True(game.Game.FileExists(path), path);
        }

        Report(output, "live catalog, full chain", index, catalog);

        // What each later step could supply on its own, among the quests without a banner of their own.
        var bare = catalog.All.Where(q => q.Icon == 0).ToArray();
        var withDuty = bare.Count(q => sources.TryGetDutyBanner(q, out _, out _));
        var withZone = bare.Count(q => q.Issuer is { TerritoryId: not 0 } i && sources.ZoneBannerPath(i.TerritoryId) is not null);
        output.WriteLine($"  without an own banner: {bare.Length}; a duty banner available for {withDuty}, a zone image for {withZone}; {sources.DutyQuestCount} quests in all have a duty banner");
    }

    internal static void Report(ITestOutputHelper output, string title, BannerIndex index, QuestCatalog catalog)
    {
        output.WriteLine(title);
        var listed = catalog.All.Count(q => !q.IsRemoved);
        foreach (var source in new[] { BannerSource.Own, BannerSource.Sibling, BannerSource.Duty, BannerSource.Zone, BannerSource.Category })
        {
            var all = index.Counts[(int)source];
            var live = catalog.All.Count(q => !q.IsRemoved && index.For(q).Source == source);
            output.WriteLine($"  {source,-9} {all,5} of {catalog.Count} ({100.0 * all / catalog.Count,5:F1} %)   in the journal: {live,5} of {listed} ({100.0 * live / listed,5:F1} %)");
        }
    }
}
