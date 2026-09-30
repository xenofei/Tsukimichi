using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
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
        var lookups = new FakeLookups(new() { [3] = (112005, 4), [4] = (112005, 4) }, new() { [134] = "ui/loadingimage/-nowloading_base01.tex" });

        var index = BannerIndex.Build(catalog, lookups);

        Assert.Equal(new BannerChoice(BannerSource.Own, 100001, null, BannerArt.SideLaNoscea, 0), index.For(own));
        Assert.Equal(new BannerChoice(BannerSource.Sibling, 100002, null, BannerArt.SideLaNoscea, 2), index.For(sibling));
        Assert.Equal(new BannerChoice(BannerSource.Duty, 112005, null, BannerArt.Other, 4), index.For(dutyOnly));
        Assert.Equal(BannerSource.Zone, index.For(zoneOnly).Source);
        Assert.Equal("ui/loadingimage/-nowloading_base01.tex", index.For(zoneOnly).GamePath);
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
    public void The_nearest_earlier_quest_in_the_genre_wins_and_a_later_one_only_when_none_is_earlier()
    {
        var first = Quest(19, 151, sort: 0);                 // nothing earlier: the nearest later banner
        var a = Quest(20, 151, icon: 100020, sort: 1);
        var b = Quest(21, 151, sort: 2);
        var c = Quest(22, 151, sort: 3);
        var d = Quest(23, 151, sort: 4);
        var near = Quest(24, 151, icon: 100024, sort: 5);    // right after 23, but later
        var e = Quest(25, 151, sort: 6);
        var other = Quest(26, 152, icon: 100026, sort: 7);
        var donors = BannerIndex.SiblingDonors(QuestCatalog.Build([first, a, b, c, d, near, e, other]));

        Assert.Equal(20u, donors[19].RowId);
        Assert.Equal(20u, donors[21].RowId);
        Assert.Equal(20u, donors[22].RowId);
        Assert.Equal(20u, donors[23].RowId);   // the earlier banner, three back, over the later one next door
        Assert.Equal(24u, donors[25].RowId);   // never across genres
    }

    [Fact]
    public void Chain_predecessors_come_before_the_genre_neighbours()
    {
        // 40 (art, sort 9) is the chain predecessor of 41 (sort 3), which sits after 42 (art, sort 2) in journal order.
        var predecessor = Quest(40, 153, icon: 100040, sort: 9);
        var borrower = Quest(41, 153, previous: [40], sort: 3);
        var neighbour = Quest(42, 153, icon: 100042, sort: 2);
        var donors = BannerIndex.SiblingDonors(QuestCatalog.Build([predecessor, borrower, neighbour]));

        Assert.Equal(40u, donors[41].RowId);
    }

    [Fact]
    public void Under_hide_artwork_a_borrowed_banner_follows_its_own_source()
    {
        var early = Quest(50, 154, icon: 100050, sort: 1);
        var borrower = Quest(51, 154, sort: 2);
        var late = Quest(52, 154, icon: 100052, sort: 3);
        var lonely = Quest(53, 155, sort: 1);                 // only a later banner in its genre
        var lonelyDonor = Quest(54, 155, icon: 100054, sort: 2);
        var catalog = QuestCatalog.Build([early, borrower, late, lonely, lonelyDonor]);
        var index = BannerIndex.Build(catalog, null);
        var shield = SpoilerMask.Build(catalog, new Dictionary<uint, QuestState>(), SpoilerOptions.Default);
        var open = SpoilerMask.Build(catalog, new Dictionary<uint, QuestState>(), SpoilerOptions.Default with { HideArtwork = false });
        Dictionary<uint, QuestEvaluation> States(params (uint Id, QuestState State)[] states) =>
            states.ToDictionary(s => s.Id, s => new QuestEvaluation(s.State, [], null, null, null));

        // 51 borrows 50's banner: shown once 50 is in the journal or done, whatever 51's own state.
        Assert.Equal(50u, index.For(borrower).SourceId);
        var choice = index.For(borrower);
        Assert.Equal(choice, BannerShield.Apply(in choice, borrower, QuestState.Ready, shield, catalog, States((50, QuestState.Completed))));
        Assert.Equal(BannerChoice.ForArt(choice.Art), BannerShield.Apply(in choice, borrower, QuestState.Accepted, shield, catalog, States((50, QuestState.Ready))));

        // 53 can only borrow the later 54: with 53 accepted and 54 not reached, the category art stands in.
        var later = index.For(lonely);
        Assert.Equal(54u, later.SourceId);
        Assert.Equal(BannerSource.Category, BannerShield.Apply(in later, lonely, QuestState.Accepted, shield, catalog, States((53, QuestState.Accepted))).Source);
        Assert.Equal(BannerSource.Category, BannerShield.Apply(in later, lonely, QuestState.Accepted, shield, catalog, null).Source);
        Assert.Equal(later, BannerShield.Apply(in later, lonely, QuestState.Ready, open, catalog, null));

        // A duty's banner waits for the quest that unlocks it to be completed; the own banner and the zone for the journal.
        var duty = new BannerChoice(BannerSource.Duty, 112005, null, BannerArt.Other, 4);
        Assert.False(BannerShield.Allows(in duty, borrower, QuestState.Accepted, shield, catalog, null));
        Assert.True(BannerShield.Allows(in duty, borrower, QuestState.Completed, shield, catalog, null));
        var own = index.For(early);
        Assert.False(BannerShield.Allows(in own, early, QuestState.Ready, shield, catalog, null));
        Assert.True(BannerShield.Allows(in own, early, QuestState.Accepted, shield, catalog, null));
        var zone = new BannerChoice(BannerSource.Zone, 0, "ui/loadingimage/x.tex", BannerArt.Other, 134);
        Assert.False(BannerShield.Allows(in zone, borrower, QuestState.Blocked, shield, catalog, null));
        Assert.True(BannerShield.Allows(in zone, borrower, QuestState.Accepted, shield, catalog, null));
    }

    [Fact]
    public void Under_hide_artwork_no_accepted_quest_shows_a_later_donors_banner()
    {
        // Over the frozen catalog: in each genre the quests before a point are completed, the one there accepted, the
        // rest not reached. No accepted quest may show art lent by a quest later in journal order.
        var catalog = fixture.Bundle.Catalog;
        var index = BannerIndex.Build(catalog, null);
        var shield = SpoilerMask.Build(catalog, new Dictionary<uint, QuestState>(), SpoilerOptions.Default);
        var borrowedLater = 0;
        var checkedAccepted = 0;
        foreach (var (genreId, quests) in catalog.ByGenre)
        {
            if (genreId == 0)
            {
                continue;
            }

            var position = new Dictionary<uint, int>();
            for (var i = 0; i < quests.Count; i++)
            {
                position[quests[i].RowId] = i;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                var choice = index.For(quest);
                if (choice.Source != BannerSource.Sibling || !position.TryGetValue(choice.SourceId, out var donorAt) || donorAt <= i)
                {
                    continue;
                }

                borrowedLater++;
                var states = new Dictionary<uint, QuestEvaluation>();
                for (var j = 0; j < quests.Count; j++)
                {
                    var state = j < i ? QuestState.Completed : j == i ? QuestState.Accepted : QuestState.Ready;
                    states[quests[j].RowId] = new QuestEvaluation(state, [], null, null, null);
                }

                checkedAccepted++;
                Assert.Equal(BannerSource.Category, BannerShield.Apply(in choice, quest, QuestState.Accepted, shield, catalog, states).Source);
            }
        }

        output.WriteLine($"sibling banners lent by a later quest: {borrowedLater}; accepted with the later donor not reached, all withheld: {checkedAccepted}");
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
        Assert.DoesNotContain(catalog.All.Select(q => index.For(q)), c => c.GamePath is { } p && p.Contains("_hr1", StringComparison.Ordinal));

        // Sibling banners lent by a quest later in journal order: only where no earlier quest of the genre has one.
        var position = catalog.ByGenre.Where(g => g.Key != 0).SelectMany(g => g.Value.Select((q, i) => (q.RowId, i))).ToDictionary(p => p.RowId, p => p.i);
        var siblings = catalog.All.Where(q => index.For(q).Source == BannerSource.Sibling).ToArray();
        var later = siblings.Count(q => position.TryGetValue(index.For(q).SourceId, out var d) && position.TryGetValue(q.RowId, out var b) && d > b);
        output.WriteLine($"  sibling banners: {siblings.Length}, lent by a later quest: {later}");

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
