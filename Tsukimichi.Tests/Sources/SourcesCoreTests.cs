using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Sources;

/// <summary>
/// The pieces of C6 (buy-back) and N5 (where to get hand-in items) that need no game: which shop row counts as a
/// buy-back for which quest, the order the lines come in, and the words they use.
/// </summary>
public class SourcesCoreTests
{
    private const uint Quest = 65984;
    private const uint OtherQuest = 67079;

    private static readonly WorldSpot Uldah = new(130, "Ul'dah - Steps of Thal", 0f, 0f, 12.66f, 13.24f);
    private static readonly WorldSpot Limsa = new(129, "Limsa Lominsa Lower Decks", 0f, 0f, 9.4f, 11.2f);

    private static Vendor Salvager(WorldSpot? spot = null) => new(1006006, "Calamity salvager", spot);

    private static ShopCost Gil(uint amount) => new(ShopCost.GilItemId, "gil", amount);

    private static ShopOffer GilShop(uint price, Vendor vendor, uint quest = 0, uint achievement = 0, uint shopId = 262446) =>
        new(ShopKind.Gil, shopId, "Purchase Quest Rewards I", [vendor], [Gil(price)]) { RequiredQuest = quest, RequiredAchievement = achievement };

    // ---- BuyBacks ----

    [Fact]
    public void A_reclaim_row_counts_only_for_its_own_quest()
    {
        var sources = new ItemSources(2651) { Shops = [GilShop(100, Salvager(Uldah), quest: Quest)] };

        var found = BuyBacks.For(sources, Quest);
        Assert.NotNull(found);
        Assert.Equal(BuyBackKind.BuyBack, found.Kind);
        Assert.Equal("Can be bought back from Calamity salvager, Ul'dah - Steps of Thal (12.7, 13.2) for 100 gil", BuyBacks.Line(found));
        Assert.Null(BuyBacks.For(sources, OtherQuest));

        // With no quest given, any quest's reclaim row counts.
        Assert.Equal(BuyBackKind.BuyBack, BuyBacks.For(sources)!.Kind);
    }

    [Fact]
    public void An_achievement_row_and_a_shop_no_npc_opens_never_count()
    {
        var achievement = GilShop(1000, Salvager(), achievement: 7);
        var nobody = new ShopOffer(ShopKind.Gil, 262000, "Purchase Items", [], [Gil(5)]);
        var sources = new ItemSources(2907) { Shops = [achievement, nobody] };

        Assert.Null(BuyBacks.For(sources, Quest));
        Assert.Null(BuyBacks.For(sources));
        Assert.Null(BuyBacks.For(null, Quest));
        Assert.Null(BuyBacks.For(ItemSources.None(1), Quest));
    }

    [Fact]
    public void A_shop_anyone_can_use_beats_a_reclaim_row_and_gil_beats_an_exchange()
    {
        var reclaim = GilShop(100, Salvager(Uldah), quest: Quest);
        var vendor = GilShop(140, new Vendor(1000200, "Material supplier", null), shopId: 262175);
        var cheaper = GilShop(90, new Vendor(1000201, "Merchant & mender", Limsa), shopId: 262176);
        var swap = new ShopOffer(ShopKind.Exchange, 1769512, "Gender-specific Gear Exchange", [Salvager(Uldah)], [new ShopCost(6096, "Striped Summer Top", 1)]);

        Assert.Same(cheaper, BuyBacks.For(new ItemSources(1) { Shops = [reclaim, vendor, cheaper, swap] }, Quest)!.Offer);
        Assert.Equal(BuyBackKind.Sold, BuyBacks.For(new ItemSources(1) { Shops = [reclaim, cheaper] }, Quest)!.Kind);

        // The swap sells to anyone, but a reclaim row for gil says more.
        var best = BuyBacks.For(new ItemSources(1) { Shops = [swap, reclaim] }, Quest)!;
        Assert.Same(reclaim, best.Offer);

        // Alone, the swap is still a way back.
        Assert.Equal("Also sold by Calamity salvager, Ul'dah - Steps of Thal (12.7, 13.2) for 1 Striped Summer Top", BuyBacks.Line(BuyBacks.For(new ItemSources(1) { Shops = [swap] }, Quest)!));
    }

    [Fact]
    public void A_placed_vendor_wins_a_tie_and_an_unknown_price_is_never_guessed()
    {
        var unplaced = GilShop(100, Salvager(), shopId: 1);
        var placed = GilShop(100, Salvager(Uldah), shopId: 2);
        Assert.Same(placed, BuyBacks.For(new ItemSources(1) { Shops = [unplaced, placed] })!.Offer);

        var unpriced = new ShopOffer(ShopKind.Exchange, 3, "Tomestone Exchange", [new Vendor(1, "Auriana", null)], []);
        var line = BuyBacks.Line(BuyBacks.For(new ItemSources(1) { Shops = [unpriced] })!);
        Assert.Equal("Also sold by Auriana", line);
        Assert.Equal("Can be bought back from Calamity salvager", BuyBacks.Line(new BuyBack(BuyBackKind.BuyBack, unplaced with { Costs = [] })));
    }

    [Fact]
    public void The_tooltip_says_what_the_mark_means()
    {
        var reclaim = new BuyBack(BuyBackKind.BuyBack, GilShop(100, Salvager(), quest: Quest));
        Assert.Equal("Re-buyable", BuyBacks.Mark);
        Assert.Contains("Once the quest is done", BuyBacks.Tooltip(reclaim), StringComparison.Ordinal);
        Assert.Contains("Anyone can buy it", BuyBacks.Tooltip(reclaim with { Kind = BuyBackKind.Sold }), StringComparison.Ordinal);
    }

    // ---- WhereToGet ----

    [Fact]
    public void Lines_come_one_per_kind_in_order_with_the_first_shop_leading()
    {
        var sources = new ItemSources(4552)
        {
            Shops =
            [
                GilShop(146, new Vendor(1, "Albgast", Limsa), shopId: 10),
                GilShop(146, new Vendor(2, "Rarakiya", Uldah), shopId: 11),
                new ShopOffer(ShopKind.GrandCompany, 1441793, "Maelstrom", [new Vendor(3, "Storm quartermaster", null)], [new ShopCost(20, "Storm Seals", 12)]),
                new ShopOffer(ShopKind.Exchange, 1769553, "Assorted Accoutrements", [new Vendor(4, "Talan", null)], []),
            ],
            Crafts = [new CraftOption(1, 6, "alchemist", 25), new CraftOption(2, 6, "alchemist", 20), new CraftOption(3, 7, "culinarian", 30, 2)],
            Gathering =
            [
                new GatherSpot(GatherKind.Gathered, 17, "botanist", 15, "Black Brush", Uldah),
                new GatherSpot(GatherKind.Gathered, 17, "botanist", 30, "The Vein", Limsa, Timed: true),
                new GatherSpot(GatherKind.Fish, 18, "fisher", 5, string.Empty, Limsa),
            ],
            Marketable = true,
            DropWhere = "Sastasha",
        };

        var lines = WhereToGet.Lines(sources);

        Assert.Equal(
            [WhereKind.Vendor, WhereKind.Crafted, WhereKind.Gathered, WhereKind.Fished, WhereKind.GrandCompany, WhereKind.Exchange, WhereKind.Drops],
            lines.Select(l => l.Kind));
        Assert.Equal("Sold by Albgast, Limsa Lominsa Lower Decks (9.4, 11.2) · 146 gil", lines[0].Text);
        Assert.Equal(1, lines[0].More);
        Assert.Equal(["Sold by Rarakiya, Ul'dah - Steps of Thal (12.7, 13.2) · 146 gil"], lines[0].Others);
        Assert.Same(Limsa, lines[0].Spot);
        Assert.Equal("Crafted: Alchemist Lv. 20, Culinarian Lv. 30 (2-star)", lines[1].Text);
        Assert.Null(lines[1].Spot);
        Assert.Equal("Gathered: Botanist Lv. 15 · Black Brush, Ul'dah - Steps of Thal (12.7, 13.2)", lines[2].Text);
        Assert.Equal(["Gathered: Botanist Lv. 30 · The Vein, Limsa Lominsa Lower Decks (9.4, 11.2) (timed)"], lines[2].Others);
        Assert.Equal("Fished: Lv. 5 · Limsa Lominsa Lower Decks (9.4, 11.2)", lines[3].Text);
        Assert.Equal("Grand Company: Storm quartermaster · 12 Storm Seals", lines[4].Text);
        Assert.Equal("Exchange: Talan", lines[5].Text);
        Assert.Equal("Drops in Sastasha", lines[6].Text);
        Assert.Equal("+1 more", WhereToGet.MoreText(lines[0].More));
        Assert.Equal(string.Empty, WhereToGet.MoreText(0));
    }

    [Fact]
    public void Market_board_only_when_nothing_else_and_nothing_at_all_when_unknown()
    {
        var market = WhereToGet.Lines(new ItemSources(1) { Marketable = true });
        Assert.Equal("Market board only", Assert.Single(market).Text);
        Assert.Empty(WhereToGet.Lines(ItemSources.None(1)));
        Assert.Empty(WhereToGet.Lines(null));
    }

    [Fact]
    public void A_gated_shop_says_so_and_an_unplaced_node_names_job_and_level()
    {
        var gated = WhereToGet.ShopText(GilShop(100, Salvager(), quest: Quest), WhereKind.Vendor);
        Assert.Equal("Sold by Calamity salvager · 100 gil (after a quest or achievement)", gated);

        var unplaced = new GatherSpot(GatherKind.Gathered, 16, "miner", 60, string.Empty, null);
        Assert.Equal("Gathered: Miner Lv. 60", WhereToGet.GatherText(unplaced));
        Assert.Equal("Fished: Lv. 60", WhereToGet.GatherText(unplaced with { Kind = GatherKind.Fish }));
        Assert.Null(Assert.Single(WhereToGet.Lines(new ItemSources(1) { Gathering = [unplaced] })).Spot);
    }

    [Fact]
    public void Costs_and_names_read_naturally()
    {
        Assert.Equal("1,500 gil", SourceText.Cost([Gil(1500)]));
        Assert.Equal("3 Bicolor Gemstones + 100 gil", SourceText.Cost([new ShopCost(26807, "Bicolor Gemstones", 3), Gil(100)]));
        Assert.Equal(string.Empty, SourceText.Cost([]));
        Assert.Equal("Calamity salvager", SourceText.Capitalized("calamity salvager"));
        Assert.Equal("Ranaa Mihgo", SourceText.Capitalized("Ranaa Mihgo"));
        Assert.Equal(string.Empty, SourceText.Capitalized(string.Empty));
        Assert.True(Gil(1).IsGil);
        Assert.Equal(100u, GilShop(100, Salvager()).GilPrice);
        Assert.Null(new ShopOffer(ShopKind.Exchange, 1, "x", [], [new ShopCost(28, "Poetics", 1)]).GilPrice);
    }

    // ---- ItemSourceIndex over plain tables ----

    [Fact]
    public void The_index_sorts_shops_and_spots_and_caches_each_item()
    {
        var gated = GilShop(10, Salvager(), quest: Quest, shopId: 1);
        var dear = GilShop(50, Salvager(), shopId: 2);
        var cheap = GilShop(20, Salvager(), shopId: 3);
        var seals = new ShopOffer(ShopKind.GrandCompany, 4, "Maelstrom", [Salvager()], [new ShopCost(20, "Storm Seals", 5)]);
        var unplaced = new GatherSpot(GatherKind.Gathered, 16, "miner", 1, string.Empty, null);
        var timed = new GatherSpot(GatherKind.Gathered, 16, "miner", 5, string.Empty, Limsa, Timed: true);
        var low = new GatherSpot(GatherKind.Gathered, 16, "miner", 10, string.Empty, Uldah);
        var index = new ItemSourceIndex(
            new Dictionary<uint, ShopOffer[]> { [7] = [seals, gated, dear, cheap] },
            new Dictionary<uint, GatherSpot[]> { [7] = [unplaced, timed, low] },
            new Dictionary<uint, CraftOption[]> { [7] = [new CraftOption(2, 3, "goldsmith", 9), new CraftOption(1, 1, "blacksmith", 20)] },
            id => id == 7,
            new Dictionary<uint, string> { [7] = "Sastasha" });

        var sources = index.For(7);
        Assert.Equal([cheap, dear, gated, seals], sources.Shops);
        Assert.Equal([low, timed, unplaced], sources.Gathering);
        Assert.Equal([1u, 2u], sources.Crafts.Select(c => c.RecipeId));
        Assert.True(sources.Marketable);
        Assert.Equal("Sastasha", sources.DropWhere);
        Assert.Same(sources, index.For(7));
        Assert.False(index.For(8).Any);
        Assert.Equal(1, index.ShopItemCount);
        Assert.Equal(21.5f, ItemSourceIndex.MapCoordinate(0f, 0, 100), 2);
        Assert.Equal(11.25f, ItemSourceIndex.MapCoordinate(0f, 0, 200), 2);
    }
}
