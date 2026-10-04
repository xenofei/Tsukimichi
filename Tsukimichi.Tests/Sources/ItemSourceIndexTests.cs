using System.Diagnostics;
using Lumina.Data;
using Lumina.Data.Files;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Sources;

/// <summary>Builds the item sources once per test class from the game, with the event layouts, as the plugin's warmer does.</summary>
public sealed class ItemSourceFixture : IDisposable
{
    private readonly GameDataFixture game = new();
    private readonly Lazy<(ItemSourceIndex Index, TimeSpan Elapsed)> built;

    public ItemSourceFixture()
    {
        built = new Lazy<(ItemSourceIndex, TimeSpan)>(
            () =>
            {
                var stopwatch = Stopwatch.StartNew();
                var index = ItemSourceIndex.Build(game.Game.Excel, Language.English, readLayout: path => game.Game.GetFile<LgbFile>(path));
                return (index, stopwatch.Elapsed);
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ItemSourceIndex Index => built.Value.Index;

    public TimeSpan Elapsed => built.Value.Elapsed;

    public GameDataFixture Game => game;

    public void Dispose() => game.Dispose();
}

/// <summary>
/// The item sources against the game's own sheets (C6, N5): the reclaim rows of the Calamity salvager and the
/// recompense officer name the quest, gil vendors and Grand Company quartermasters carry their prices, gathering and
/// fishing are placed on the map, recipes carry their levels; and how much of the hand-in items and Moonlit rewards the
/// data covers.
/// </summary>
public sealed class ItemSourceIndexTests(ItemSourceFixture fixture, ITestOutputHelper output) : IClassFixture<ItemSourceFixture>
{
    private const uint MonoaMask = 2651;            // Festive Endeavors rewards it; the Calamity salvager sells it back
    private const uint FestiveEndeavors = 65984;
    private const uint SummerEveningTop = 6094;     // Remember Me This Moonfire Faire; the recompense officer sells it back
    private const uint MoonfireFaire = 67079;
    private const uint ButchersCrown = 2907;        // an achievement's reward: the salvager's achievement menu
    private const uint HiPotion = 4552;
    private const uint MapleLumber = 5361;
    private const uint RedMalachite = 22616;        // the Namazu quests' own node: gathered, unplaced
    private const uint MegaPotion = 6141;           // Grand Company quartermasters, for seals
    private const uint Merlthor = 4870;             // Merlthor goby: fished in La Noscea, sold by a fishmonger

    [GameDataFact]
    public void The_index_builds_quickly_and_covers_thousands_of_items()
    {
        var index = fixture.Index;
        output.WriteLine($"built in {fixture.Elapsed.TotalMilliseconds:0} ms; shop items {index.ShopItemCount}, gathered/fished {index.GatherItemCount}, crafted {index.CraftItemCount}");
        Assert.InRange(index.ShopItemCount, 10_000, 40_000);
        Assert.InRange(index.GatherItemCount, 1_500, 6_000);
        Assert.InRange(index.CraftItemCount, 8_000, 25_000);
        Assert.False(ItemSourceIndex.Empty.For(MonoaMask).Any);
        Assert.False(index.For(0).Any);
    }

    [GameDataFact]
    public void A_quest_reward_reclaim_row_names_its_quest_and_the_Calamity_salvager()
    {
        var sources = fixture.Index.For(MonoaMask);
        var buyBack = BuyBacks.For(sources, FestiveEndeavors);
        Assert.NotNull(buyBack);
        Assert.Equal(BuyBackKind.BuyBack, buyBack.Kind);
        Assert.Equal(ShopKind.Gil, buyBack.Offer.Kind);
        Assert.Equal(FestiveEndeavors, buyBack.Offer.RequiredQuest);
        Assert.Equal(100u, buyBack.Offer.GilPrice);
        Assert.Equal("Calamity salvager", buyBack.Vendor.Name);
        Assert.True(buyBack.Vendor.Generic);
        output.WriteLine(BuyBacks.Line(buyBack));
        Assert.StartsWith("Can be bought back from a Calamity salvager", BuyBacks.Line(buyBack), StringComparison.Ordinal);
        Assert.Equal("Re-buyable · 100 gil", BuyBacks.Short(buyBack));
        Assert.EndsWith("for 100 gil", BuyBacks.Line(buyBack), StringComparison.Ordinal);

        // Another quest's reward is not this quest's buy-back.
        Assert.Null(BuyBacks.For(sources, MoonfireFaire));
    }

    [GameDataFact]
    public void A_seasonal_reward_is_bought_back_from_the_recompense_officer_for_gil_not_swapped()
    {
        var buyBack = BuyBacks.For(fixture.Index.For(SummerEveningTop), MoonfireFaire);
        Assert.NotNull(buyBack);
        Assert.Equal(BuyBackKind.BuyBack, buyBack.Kind);
        Assert.Equal(ShopKind.Gil, buyBack.Offer.Kind);
        Assert.Equal("recompense officer", buyBack.Vendor.Name);
        Assert.StartsWith("Can be bought back from a recompense officer", BuyBacks.Line(buyBack), StringComparison.Ordinal);
    }

    [GameDataFact]
    public void An_achievement_reclaim_row_never_counts_for_a_quest()
    {
        var sources = fixture.Index.For(ButchersCrown);
        Assert.Contains(sources.Shops, s => s.RequiredAchievement != 0 && s.RequiredQuest == 0);
        Assert.DoesNotContain(sources.Shops, s => !s.Gated && s.Kind == ShopKind.Gil);
        Assert.Null(BuyBacks.For(sources, FestiveEndeavors));
    }

    [GameDataFact]
    public void Gil_vendors_carry_the_price_and_a_placed_vendor_leads()
    {
        var sources = fixture.Index.For(HiPotion);
        var vendor = WhereToGet.Lines(sources).First(l => l.Kind == WhereKind.Vendor);
        output.WriteLine(WhereToGet.Summary(sources)!.Text);
        Assert.NotNull(vendor.Spot);
        Assert.InRange(vendor.Spot.MapX, 1f, 43f);
        Assert.InRange(vendor.Spot.MapY, 1f, 43f);
        Assert.NotEmpty(vendor.Others);
        Assert.Contains("gil", vendor.Lead, StringComparison.Ordinal);
        Assert.All(sources.Shops.Where(s => s.Kind == ShopKind.Gil && !s.Gated), s => Assert.Equal(sources.Shops.First(o => o.Kind == ShopKind.Gil).GilPrice, s.GilPrice));
        Assert.Contains(WhereToGet.Lines(sources), l => l.Kind == WhereKind.Crafted && l.Lead == "Crafted · Alchemist Lv 25");
        Assert.EndsWith("· or crafted, Alchemist Lv 25", WhereToGet.Summary(sources)!.Text, StringComparison.Ordinal);
        Assert.Equal(BuyBackKind.Sold, BuyBacks.For(sources)!.Kind);
    }

    [GameDataFact]
    public void Recipes_carry_the_crafter_and_its_level()
    {
        var craft = Assert.Single(fixture.Index.For(MapleLumber).Crafts);
        Assert.Equal(0, craft.CraftType);
        Assert.Equal("carpenter", craft.JobName);
        Assert.Equal(1, craft.Level);
        Assert.Contains(fixture.Index.For(MegaPotion).Crafts, c => c.Stars > 0);
    }

    [GameDataFact]
    public void Grand_Company_quartermasters_sell_for_their_seals()
    {
        var sources = fixture.Index.For(MegaPotion);
        var companies = sources.Shops.Where(s => s.Kind == ShopKind.GrandCompany).ToList();
        Assert.Equal(3, companies.Count);
        Assert.Equal([20u, 21u, 22u], companies.Select(s => s.Costs.Single().ItemId).Order());
        Assert.All(companies, s => Assert.Contains("quartermaster", s.FirstVendor!.Name, StringComparison.Ordinal));
        Assert.Contains(WhereToGet.Lines(sources), l => l.Kind == WhereKind.GrandCompany && l.Lead.Contains("Seals", StringComparison.Ordinal));
    }

    [GameDataFact]
    public void Fishing_holes_and_nodes_are_placed_or_named_without_a_place()
    {
        var fish = fixture.Index.For(Merlthor);
        Assert.Contains(fish.Gathering, s => s.Kind == GatherKind.Fish && s.Spot is { } spot && spot.MapX is >= 1f and <= 43f && spot.MapY is >= 1f and <= 43f);
        Assert.Equal("fisher", fish.Gathering[0].JobName);

        // The Namazu quests' Red Malachite comes from the quest's own node, which the sheets do not place.
        var ore = fixture.Index.For(RedMalachite).Gathering;
        Assert.NotEmpty(ore);
        Assert.All(ore, s => Assert.Equal("miner", s.JobName));
        Assert.All(ore, s => Assert.Equal(GatherMethod.Quarrying, s.Method));

        // The line names no node: the game's Gathering Log shows them (spec-1.19 decision 7).
        var summary = WhereToGet.Summary(fixture.Index.For(RedMalachite))!;
        Assert.Equal("Quarried · the Gathering Log shows the nodes", summary.Text);
        Assert.True(summary.GatheringLog);
    }

    [GameDataFact]
    public void Every_placed_spot_is_on_the_map()
    {
        var spots = fixture.Index.For(HiPotion).Shops.SelectMany(s => s.Vendors).Select(v => v.Spot)
            .Concat(fixture.Index.For(Merlthor).Gathering.Select(g => g.Spot))
            .OfType<WorldSpot>()
            .ToList();
        Assert.NotEmpty(spots);
        Assert.All(spots, s =>
        {
            Assert.InRange(s.MapX, 1f, 43f);
            Assert.InRange(s.MapY, 1f, 43f);
            Assert.False(string.IsNullOrWhiteSpace(s.Zone));
        });
    }

    /// <summary>How much of the hand-in items the "Where" lines can speak for, by kind (the report's coverage numbers).</summary>
    [GameDataFact]
    public void Most_hand_in_items_have_a_where_line()
    {
        var items = fixture.Game.Bundle.Catalog.All.SelectMany(q => q.HandInItems).Select(i => i.ItemId).Distinct().ToList();
        var kinds = new Dictionary<WhereKind, int>();
        var any = 0;
        var placed = 0;
        foreach (var id in items)
        {
            var lines = WhereToGet.Lines(fixture.Index.For(id));
            any += lines.Count > 0 ? 1 : 0;
            placed += lines.Any(l => l.Spot is not null) ? 1 : 0;
            foreach (var line in lines)
            {
                kinds[line.Kind] = kinds.GetValueOrDefault(line.Kind) + 1;
            }
        }

        output.WriteLine($"hand-in items {items.Count}: with a line {any} ({100.0 * any / items.Count:0}%), with a flaggable place {placed}");
        foreach (var (kind, count) in kinds.OrderBy(k => k.Key))
        {
            output.WriteLine($"  {kind}: {count}");
        }

        Assert.True(any >= items.Count * 0.6, $"{any} of {items.Count} hand-in items have a source");
    }

    /// <summary>How many Moonlit rewards a shop sells back (the report's coverage numbers), and that none is claimed for the wrong quest.</summary>
    [GameDataFact]
    public void Some_Moonlit_rewards_can_be_bought_back()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var items = unique.Entries.Where(e => e.ItemId != 0).ToList();
        var buyBack = 0;
        var sold = 0;
        foreach (var entry in items)
        {
            switch (BuyBacks.For(fixture.Index.For(entry.ItemId), entry.QuestRowId))
            {
                case { Kind: BuyBackKind.BuyBack } found:
                    Assert.Equal(entry.QuestRowId, found.Offer.RequiredQuest);
                    buyBack++;
                    break;
                case { Kind: BuyBackKind.Sold }:
                    sold++;
                    break;
            }
        }

        output.WriteLine($"Moonlit entries with an item {items.Count}: bought back after the quest {buyBack}, sold to anyone {sold}");
        Assert.True(buyBack > 50, $"{buyBack} reclaimable");
    }
}
