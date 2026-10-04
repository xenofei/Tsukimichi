using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Sources;

/// <summary>
/// The pieces of C6 (buy-back) and N5 (where to get hand-in items) that need no game: which shop row counts as a
/// buy-back for which quest, the reward state lines, the order the sources come in and the words they use
/// (docs/design/v7/ui/spec-1.19.md, "C6" and "N5").
/// </summary>
public class SourcesCoreTests
{
    private const uint Quest = 65984;
    private const uint OtherQuest = 67079;

    private static readonly WorldSpot Uldah = new(130, "Ul'dah - Steps of Thal", 0f, 0f, 12.66f, 13.24f);
    private static readonly WorldSpot Limsa = new(129, "Limsa Lominsa Lower Decks", 0f, 0f, 9.4f, 11.2f);

    private static Vendor Salvager(WorldSpot? spot = null) => new(1006006, "Calamity salvager", spot) { Generic = true };

    private static Vendor Person(string name, WorldSpot? spot = null) => new(1018986, name, spot);

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
        Assert.Equal("Can be bought back from a Calamity salvager, Ul'dah - Steps of Thal (12.7, 13.2) for 100 gil", BuyBacks.Line(found));
        Assert.Equal("Re-buyable · 100 gil", BuyBacks.Short(found));
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
        var vendor = GilShop(140, new Vendor(1000200, "material supplier", null) { Generic = true }, shopId: 262175);
        var cheaper = GilShop(90, Person("Merchant & mender", Limsa), shopId: 262176);
        var swap = new ShopOffer(ShopKind.Exchange, 1769512, "Gender-specific Gear Exchange", [Salvager(Uldah)], [new ShopCost(6096, "Striped Summer Top", 1)]);

        Assert.Same(cheaper, BuyBacks.For(new ItemSources(1) { Shops = [reclaim, vendor, cheaper, swap] }, Quest)!.Offer);
        Assert.Equal(BuyBackKind.Sold, BuyBacks.For(new ItemSources(1) { Shops = [reclaim, cheaper] }, Quest)!.Kind);

        // The swap sells to anyone, but a reclaim row for gil says more.
        var best = BuyBacks.For(new ItemSources(1) { Shops = [swap, reclaim] }, Quest)!;
        Assert.Same(reclaim, best.Offer);

        // Alone, the swap is still a way back.
        Assert.Equal("Also sold by a Calamity salvager, Ul'dah - Steps of Thal (12.7, 13.2) for 1 Striped Summer Top", BuyBacks.Line(BuyBacks.For(new ItemSources(1) { Shops = [swap] }, Quest)!));
    }

    [Fact]
    public void A_placed_vendor_wins_a_tie_and_an_unknown_price_is_never_guessed()
    {
        var unplaced = GilShop(100, Salvager(), shopId: 1);
        var placed = GilShop(100, Salvager(Uldah), shopId: 2);
        Assert.Same(placed, BuyBacks.For(new ItemSources(1) { Shops = [unplaced, placed] })!.Offer);

        var unpriced = new ShopOffer(ShopKind.Exchange, 3, "Tomestone Exchange", [Person("Auriana")], []);
        var found = BuyBacks.For(new ItemSources(1) { Shops = [unpriced] })!;
        Assert.Equal("Also sold by Auriana", BuyBacks.Line(found));
        Assert.Equal("Re-buyable", BuyBacks.Short(found));
        Assert.Equal("Can be bought back from a Calamity salvager", BuyBacks.Line(new BuyBack(BuyBackKind.BuyBack, unplaced with { Costs = [] })));
    }

    [Fact]
    public void The_tooltip_says_what_the_mark_means()
    {
        var reclaim = new BuyBack(BuyBackKind.BuyBack, GilShop(100, Salvager(), quest: Quest));
        Assert.Equal("Re-buyable", BuyBacks.Mark);
        Assert.Contains("Once the quest is done", BuyBacks.Tooltip(reclaim), StringComparison.Ordinal);
        Assert.Contains("Anyone can buy it", BuyBacks.Tooltip(reclaim with { Kind = BuyBackKind.Sold }), StringComparison.Ordinal);
    }

    // ---- RewardStates (C6) ----

    [Fact]
    public void A_collectible_not_learned_says_where_to_reclaim_it()
    {
        var officer = new BuyBack(BuyBackKind.BuyBack, GilShop(100, new Vendor(1017615, "recompense officer", Uldah) { Generic = true }, quest: Quest));

        var line = RewardStates.For(collectible: true, learned: false, RewardWhereabouts.NotHeld, officer, exclusive: true);
        Assert.NotNull(line);
        Assert.Equal("Done, not learned · reclaim at a recompense officer, 100 gil", line.Text);
        Assert.True(line.IsBuyBack);

        Assert.Null(RewardStates.For(true, true, RewardWhereabouts.NotHeld, officer, true));
        Assert.Equal("Done, not learned · it is on you", RewardStates.For(true, false, RewardWhereabouts.OnYou, officer, true)!.Text);
        Assert.Equal("Done, not learned · in your armoury chest", RewardStates.For(true, false, RewardWhereabouts.InArmoury, officer, true)!.Text);
        Assert.Equal("Done, not learned · not offered by the Salvager", RewardStates.For(true, false, RewardWhereabouts.NotHeld, null, true)!.Text);
        Assert.Null(RewardStates.For(true, false, RewardWhereabouts.NotHeld, null, exclusive: false));
    }

    [Fact]
    public void An_item_not_on_you_says_where_to_buy_it_back_and_one_on_you_says_nothing()
    {
        var salvager = new BuyBack(BuyBackKind.BuyBack, GilShop(100, Salvager(Uldah), quest: Quest));

        var gone = RewardStates.For(collectible: false, learned: null, RewardWhereabouts.NotHeld, salvager, exclusive: true);
        Assert.Equal("Not on you · buy it back from a Calamity salvager, 100 gil", gone!.Text);
        Assert.True(gone.IsBuyBack);
        Assert.Null(RewardStates.For(false, null, RewardWhereabouts.OnYou, salvager, true));
        Assert.Equal("In your saddlebag", RewardStates.For(false, null, RewardWhereabouts.InSaddlebag, salvager, true)!.Text);
        Assert.Equal("With your retainers", RewardStates.For(false, null, RewardWhereabouts.WithRetainers, null, true)!.Text);

        // Another character on view, or a place not read: the buy-back fact without "Not on you", the gold dot or the actions.
        var unsure = RewardStates.For(false, null, RewardWhereabouts.Unknown, salvager, true)!;
        Assert.Equal("If you no longer have it · buy it back from a Calamity salvager, 100 gil", unsure.Text);
        Assert.False(unsure.IsBuyBack);
        Assert.True(unsure.IfGone);
        Assert.Same(salvager, unsure.BuyBack);
        var sold = RewardStates.For(false, null, RewardWhereabouts.Unknown, new BuyBack(BuyBackKind.Sold, GilShop(1053, Person("Kurogai"))), false)!;
        Assert.Equal("Also sold by Kurogai, 1,053 gil", sold.Text);
        Assert.False(sold.IsBuyBack);

        // No shop sells it back: said only for a reward the quest alone gives.
        Assert.Equal("Not offered by the Salvager", RewardStates.For(false, null, RewardWhereabouts.NotHeld, null, true)!.Text);
        Assert.Null(RewardStates.For(false, null, RewardWhereabouts.NotHeld, null, false));
        Assert.False(RewardStates.For(false, null, RewardWhereabouts.NotHeld, null, true)!.IsBuyBack);
    }

    [Fact]
    public void Whereabouts_come_from_the_inventory_read()
    {
        Assert.Equal(RewardWhereabouts.OnYou, RewardStates.WhereaboutsOf(new HandInCount(1, 0, null, 0, 0, 0)));
        Assert.Equal(RewardWhereabouts.OnYou, RewardStates.WhereaboutsOf(new HandInCount(0, 0, null, 0, 0, 1)));
        Assert.Equal(RewardWhereabouts.InArmoury, RewardStates.WhereaboutsOf(new HandInCount(0, 0, null, 0, 1, 0)));
        Assert.Equal(RewardWhereabouts.InSaddlebag, RewardStates.WhereaboutsOf(new HandInCount(0, 0, null, 2, 0, 0)));
        Assert.Equal(RewardWhereabouts.WithRetainers, RewardStates.WhereaboutsOf(new HandInCount(0, 0, 3, 0, 0, 0)));
        Assert.Equal(RewardWhereabouts.NotHeld, RewardStates.WhereaboutsOf(new HandInCount(0, 0, 0, 0, 0, 0)));
        Assert.Equal(RewardWhereabouts.Unknown, RewardStates.WhereaboutsOf(default));

        // The game read paused: Allagan Tools' count of the character stands in.
        Assert.Equal(RewardWhereabouts.OnYou, RewardStates.WhereaboutsOf(new HandInCount(2, null, 0)));
    }

    [Fact]
    public void A_place_not_read_is_never_read_as_not_held()
    {
        // The saddlebag not opened this session reads null, not 0: the reward may be in it, so no "Not on you".
        var saddlebagUnread = new HandInCount(0, 0, Retainers: 0, Saddlebag: null, Armoury: 0, Equipped: 0);
        Assert.Equal(RewardWhereabouts.Unknown, RewardStates.WhereaboutsOf(saddlebagUnread));

        // No Allagan Tools: the retainers are not counted, so the same holds.
        var retainersUnread = new HandInCount(0, 0, Retainers: null, Saddlebag: 0, Armoury: 0, Equipped: 0);
        Assert.Equal(RewardWhereabouts.Unknown, RewardStates.WhereaboutsOf(retainersUnread));

        // What was read still places it.
        Assert.Equal(RewardWhereabouts.InArmoury, RewardStates.WhereaboutsOf(new HandInCount(0, 0, null, null, 1, 0)));
        Assert.Equal(RewardWhereabouts.WithRetainers, RewardStates.WhereaboutsOf(new HandInCount(0, 0, 2, null, 0, 0)));

        // The hand-in side reads a saddlebag not read as holding nothing to name.
        var soup = new HandInItem { ItemId = 4733, Name = "Beet Soup", Amount = 1 };
        Assert.Equal(HandInPlace.None, saddlebagUnread.MisplacedIn(soup));
        Assert.Equal(0, saddlebagUnread.CountIn(HandInPlace.Saddlebag));
    }

    // ---- HandInCount places (N5) ----

    [Fact]
    public void An_item_short_in_the_inventory_names_where_the_rest_sits()
    {
        var soup = new HandInItem { ItemId = 4733, Name = "Beet Soup", Amount = 1 };
        var hq = soup with { IsHq = true };

        var inSaddlebag = new HandInCount(0, 0, null, Saddlebag: 1, Armoury: 0, Equipped: 0);
        Assert.Equal(HandInPlace.Saddlebag, inSaddlebag.MisplacedIn(soup));
        Assert.Equal(1, inSaddlebag.CountIn(HandInPlace.Saddlebag));
        Assert.Equal(HandInPlace.Armoury, new HandInCount(0, 0, null, 0, 2, 0).MisplacedIn(soup));
        Assert.Equal(HandInPlace.None, new HandInCount(1, 0, null, 3, 0, 0).MisplacedIn(soup));
        Assert.Equal(HandInPlace.None, inSaddlebag.MisplacedIn(hq));
        Assert.Equal(HandInPlace.None, new HandInCount(0, null, 5).MisplacedIn(soup));
        Assert.Equal(0, inSaddlebag.CountIn(HandInPlace.None));
    }

    // ---- WhereToGet (N5) ----

    [Fact]
    public void Sources_come_one_per_kind_in_order_and_the_line_joins_two()
    {
        var sources = new ItemSources(4552)
        {
            Shops =
            [
                GilShop(1053, Person("Kurogai", Limsa), shopId: 10),
                GilShop(1053, Person("Rarakiya", Uldah), shopId: 11),
                new ShopOffer(ShopKind.GrandCompany, 1441793, "Maelstrom", [new Vendor(3, "Storm quartermaster", null) { Generic = true }], [new ShopCost(20, "Storm Seals", 12)]),
                new ShopOffer(ShopKind.Exchange, 1769553, "Assorted Accoutrements", [Person("Talan")], []),
            ],
            Crafts = [new CraftOption(1, 7, "culinarian", 55), new CraftOption(2, 7, "culinarian", 54), new CraftOption(3, 6, "alchemist", 30, 2)],
            Gathering =
            [
                new GatherSpot(GatherKind.Gathered, 17, "botanist", 15, "Black Brush", Uldah) { Method = GatherMethod.Logging },
                new GatherSpot(GatherKind.Fish, 18, "fisher", 5, string.Empty, Limsa),
            ],
            Marketable = true,
            DropWhere = "Sastasha",
        };

        var lines = WhereToGet.Lines(sources);

        Assert.Equal(
            [WhereKind.Vendor, WhereKind.Crafted, WhereKind.Gathered, WhereKind.Fished, WhereKind.GrandCompany, WhereKind.Exchange, WhereKind.Drops],
            lines.Select(l => l.Kind));
        Assert.Equal("Sold by Kurogai · 1,053 gil", lines[0].Lead);
        Assert.Equal("or sold by Kurogai · 1,053 gil", lines[0].Or);
        Assert.Equal(["Sold by Rarakiya · 1,053 gil (Ul'dah - Steps of Thal (12.7, 13.2))"], lines[0].Others);
        Assert.Same(Limsa, lines[0].Spot);
        Assert.Equal(WhereToGet.ShopIcon, lines[0].Icon);
        Assert.Equal("Crafted · Culinarian Lv 54, Alchemist Lv 30 (2-star)", lines[1].Lead);
        Assert.Equal(WhereToGet.CraftingLogIcon, lines[1].Icon);
        Assert.Null(lines[1].Spot);
        Assert.Equal("Logged · the Gathering Log shows the nodes", lines[2].Lead);
        Assert.Null(lines[2].Spot);
        Assert.Equal("Fished · the Fishing Log shows the holes", lines[3].Lead);
        Assert.Equal(WhereToGet.FishingLogIcon, lines[3].Icon);
        Assert.Equal("Sold by a Storm quartermaster · 12 Storm Seals", lines[4].Lead);
        Assert.Equal("Exchanged by Talan", lines[5].Lead);
        Assert.Equal("Drops in Sastasha", lines[6].Lead);

        var summary = WhereToGet.Summary(lines)!;
        Assert.Equal("Sold by Kurogai · 1,053 gil · or crafted, Culinarian Lv 54, Alchemist Lv 30 (2-star)", summary.Text);
        Assert.Equal(WhereToGet.ShopIcon, summary.Icon);
        Assert.Same(Limsa, summary.Spot);
        Assert.False(summary.GatheringLog);
        Assert.Equal(7, summary.Sources.Count);
    }

    [Fact]
    public void A_gathered_item_opens_the_gathering_log_and_names_no_node()
    {
        var ore = new ItemSources(5118)
        {
            Gathering = [new GatherSpot(GatherKind.Gathered, 16, "miner", 53, "Black Brush", Uldah)],
            Crafts = [new CraftOption(1, 1, "blacksmith", 50)],
        };

        var summary = WhereToGet.Summary(ore)!;
        Assert.Equal("Crafted · Blacksmith Lv 50 · or mined, see the Gathering Log", summary.Text);
        Assert.True(summary.GatheringLog);
        Assert.Null(summary.Spot);
        Assert.DoesNotContain("Black Brush", summary.Text, StringComparison.Ordinal);

        var mined = WhereToGet.Summary(new ItemSources(5118) { Gathering = [new GatherSpot(GatherKind.Gathered, 16, "miner", 53, string.Empty, null)] })!;
        Assert.Equal("Mined · the Gathering Log shows the nodes", mined.Text);
        Assert.Equal(WhereToGet.GatheringLogIcon, mined.Icon);
    }

    [Fact]
    public void No_market_board_source_and_nothing_when_unknown()
    {
        Assert.Null(WhereToGet.Summary(new ItemSources(1) { Marketable = true }));
        Assert.Null(WhereToGet.Summary(ItemSources.None(1)));
        Assert.Null(WhereToGet.Summary((ItemSources?)null));
        Assert.Empty(WhereToGet.Lines(null));
    }

    [Fact]
    public void A_gated_shop_says_so_and_each_gathering_method_has_its_words()
    {
        Assert.Equal("Sold by a Calamity salvager · 100 gil (after a quest or achievement)", WhereToGet.ShopText(GilShop(100, Salvager(), quest: Quest), WhereKind.Vendor));
        Assert.Equal("Quarried · the Gathering Log shows the nodes", WhereToGet.GatherText(GatherMethod.Quarrying));
        Assert.Equal("Harvested · the Gathering Log shows the nodes", WhereToGet.GatherText(GatherMethod.Harvesting));
        Assert.Equal("Spearfished · the Fishing Log shows the spots", WhereToGet.GatherText(GatherMethod.Spearfishing));
        Assert.Equal("or logged, see the Gathering Log", WhereToGet.GatherText(GatherMethod.Logging, or: true));
        Assert.Equal(GatherMethod.Fishing, new GatherSpot(GatherKind.Fish, 18, "fisher", 1, string.Empty, null).Method);
        Assert.Equal(GatherMethod.Mining, new GatherSpot(GatherKind.Gathered, 16, "miner", 1, string.Empty, null).Method);
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
        Assert.Equal("a Calamity salvager", SourceText.VendorInSentence(Salvager()));
        Assert.Equal("an independent merchant", SourceText.VendorInSentence(new Vendor(1, "independent merchant", null) { Generic = true, StartsWithVowel = true }));
        Assert.Equal("Kurogai", SourceText.VendorInSentence(Person("Kurogai")));
        Assert.Equal("Calamity salvager", SourceText.VendorName(Salvager()));
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
