using Tsukimichi.Core.Model;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.HandIn;

/// <summary>
/// The hand-in rules (<see cref="QuestHandIns"/>): over plain tables, against the game's own sheets for quests whose
/// hand-ins the wiki lists (the RITEM semantics the rules rest on), and in the frozen fixture.
/// </summary>
public sealed class QuestHandInsTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const uint Materials = 501;
    private const uint Component = 502;
    private const uint LentTunic = 503;
    private const uint ArtifactHelm = 504;
    private const uint CraftedHammer = 505;
    private const uint Potion = 506;
    private const uint Ore = 507;
    private const uint Fish = 508;

    private static QuestHandIns.Sources TestSources()
    {
        var items = new Dictionary<uint, QuestHandIns.ItemFacts>
        {
            [QuestHandIns.PlaceholderItemId] = new(QuestHandIns.PlaceholderItemId, "Dated Bronze Gladius", 1, false, true, 7),
            [Materials] = new(Materials, "Rigging Component Materials", 2, true, false, 1),
            [Component] = new(Component, "Rigging Component", 3, true, false, 1),
            [LentTunic] = new(LentTunic, "Weathered Shepherd's Tunic", 4, false, true, 1),
            [ArtifactHelm] = new(ArtifactHelm, "Gallant Coronet", 5, true, true, 50),
            [CraftedHammer] = new(CraftedHammer, "Iron Cross-pein Hammer", 6, false, true, 18),
            [Potion] = new(Potion, "Hi-Potion", 7, false, false, 1),
            [Ore] = new(Ore, "Red Malachite", 8, true, false, 1),
            [Fish] = new(Fish, "Sunshell", 9, true, false, 1),
        };
        var recipes = new Dictionary<uint, IReadOnlyList<QuestHandIns.RecipeFacts>>
        {
            [Component] = [new(3001, 0, [Materials]), new(3002, 1, [Materials])],
            [CraftedHammer] = [new(3100, 1, [5056])],
            [Potion] = [new(3200, 6, [4800])],
        };
        var gather = new Dictionary<uint, GatherKind> { [Ore] = GatherKind.Gathered, [Fish] = GatherKind.Fish };
        return new QuestHandIns.Sources(id => items.GetValueOrDefault(id), recipes, gather);
    }

    [Fact]
    public void Given_materials_lent_gear_and_the_placeholder_are_not_hand_ins()
    {
        var items = QuestHandIns.Read(
            [("RITEM0", QuestHandIns.PlaceholderItemId), ("RITEM1", Materials), ("RITEM2", Component), ("RITEM3", LentTunic), ("RITEM4", ArtifactHelm), ("RITEM5", 2_000_100)],
            [],
            TestSources());

        var item = Assert.Single(items);
        Assert.Equal(Component, item.ItemId);
        Assert.Equal("Rigging Component", item.Name);
        Assert.Equal(0, item.Amount);
        Assert.False(item.AmountKnown);
        Assert.Equal(1, item.Needed);
        Assert.Equal([new HandInRecipe(3001, 0), new HandInRecipe(3002, 1)], item.Recipes);
        Assert.Equal(GatherKind.None, item.Gather);
    }

    [Fact]
    public void Tradable_and_crafted_gear_and_market_items_stay_hand_ins()
    {
        var items = QuestHandIns.Read([("RITEM0", CraftedHammer), ("RITEM1", Potion), ("RITEM2", Potion)], [], TestSources());

        Assert.Equal([CraftedHammer, Potion], items.Select(i => i.ItemId));
    }

    [Fact]
    public void Supply_rows_come_first_with_amount_quality_and_jobs_and_merge_per_item()
    {
        var supply = new QuestHandIns.SupplyRow[]
        {
            new(Component, 3, true, 33),
            new(Ore, 3, false, 17),
            new(Ore, 3, false, 18),
            new(Fish, 1, false, 19),
        };

        var items = QuestHandIns.Read([("RITEM0", QuestHandIns.PlaceholderItemId), ("RITEM1", Materials), ("QST_PRODUCT_ITEM", Component), ("RITEM2", Ore)], supply, TestSources());

        Assert.Equal([Component, Ore, Fish], items.Select(i => i.ItemId));
        Assert.Equal(3, items[0].Amount);
        Assert.True(items[0].IsHq);
        Assert.Equal([33u], items[0].ClassJobCategories);
        Assert.Equal([17u, 18u], items[1].ClassJobCategories);
        Assert.Equal(GatherKind.Gathered, items[1].Gather);
        Assert.Equal(GatherKind.Fish, items[2].Gather);
    }

    /// <summary>Quest row → the item ids the wiki lists as handed in (consolegameswiki, checked 2026-10-01).</summary>
    public static IEnumerable<object[]> WikiHandIns() =>
    [
        [65677u, new uint[] { 1823, 1895 }],                     // A Carpenter in Need: feathered harpoon, ash shortbow
        [65741u, new uint[] { 5361 }],                           // My First Saw: maple lumber
        [65828u, new uint[] { 5056 }],                           // My First Cross-pein Hammer: bronze ingot
        [65708u, new uint[] { 4552 }],                           // More than a Flesh Wound: a hi-potion
        [66781u, new uint[] { 7001 }],                           // Pulling Fangs: the Flamefang choker from the FATE
        [68727u, new uint[] { 22432, 19822, 19826 }],            // Wok on By: tako-yaki, grilled turbans, tempura platters
        [66655u, new uint[] { 1810, 6267 }],                     // A Relic Reborn (Bravura): the bardiche and the quenching oil, not the unfinished Bravura it lends
        [67041u, new uint[] { 8096 }],                           // Of Rodents and Rigging: the rigging components, not their materials
        [65893u, new uint[] { 9539, 9540, 9538, 9510, 9511 }],   // A Ponze of Flesh
        [69480u, new uint[] { 30886 }],                          // Lost No Longer: a fragment, though the quest also rewards some
        [70916u, new uint[] { 46854, 46852, 46855, 46856, 46857 }], // Keeping the Old Ways Alive
        [66594u, Array.Empty<uint>()],                           // Poisoned Hearts: the gallant armour is obtained, never handed in
        [66110u, Array.Empty<uint>()],                           // Dressed to Deceive: Isembard lends the shepherd's garb to wear
        [66832u, Array.Empty<uint>()],                           // Thank Heavensturn for You: kabuto to wear
    ];

    [GameDataTheory]
    [MemberData(nameof(WikiHandIns))]
    public void The_script_items_match_the_wiki(uint questRowId, uint[] expected)
    {
        var quest = fixture.Bundle.Catalog.GetByRowId(questRowId);
        Assert.NotNull(quest);
        output.WriteLine($"{questRowId} {quest.Name}: " + string.Join(", ", quest.HandInItems.Select(i => $"{i.ItemId} {i.Name}")));
        Assert.Equal(expected, quest.HandInItems.Select(i => i.ItemId));
        Assert.All(quest.HandInItems, i => Assert.False(i.AmountKnown));
    }

    [GameDataFact]
    public void An_allied_society_supply_quest_lists_each_jobs_delivery_with_its_amount()
    {
        // One Size Fits All (Namazu): 3 Happi Components as a crafter, 3 Red Malachite, 3 Azim Strawberries, 1 Sunshell.
        var quest = fixture.Bundle.Catalog.GetByRowId(68633)!;
        var items = quest.HandInItems;
        Assert.Equal([22720u, 22616u, 22642u, 22668u], items.Select(i => i.ItemId));
        Assert.Equal([3, 3, 3, 1], items.Select(i => (int)i.Amount));
        Assert.Equal([33u], items[0].ClassJobCategories);
        Assert.Equal([17u], items[1].ClassJobCategories);
        Assert.NotEmpty(items[0].Recipes);
        Assert.Equal(GatherKind.Gathered, items[1].Gather);
        Assert.Equal(GatherKind.Fish, items[3].Gather);

        // Into the Fire asks a crafter for a high-quality effigy component.
        Assert.True(fixture.Bundle.Catalog.GetByRowId(68637)!.HandInItems[0].IsHq);

        // Test of Talents: one staple, six of it, for a miner or a botanist.
        var staple = Assert.Single(fixture.Bundle.Catalog.GetByRowId(70526)!.HandInItems);
        Assert.Equal(6, staple.Amount);
        Assert.Equal([17u, 18u], staple.ClassJobCategories);
    }

    [GameDataFact]
    public void Recipes_and_gathering_are_read_for_hand_in_items()
    {
        var harpoon = fixture.Bundle.Catalog.GetByRowId(65677)!.HandInItems[0];
        Assert.Contains(harpoon.Recipes, r => r.CraftType == 0); // a carpenter's recipe
        var anchovy = Assert.Single(fixture.Bundle.Catalog.GetByRowId(66644)!.HandInItems); // My First Fishing Rod
        Assert.Equal(GatherKind.Fish, anchovy.Gather);
        var laurel = Assert.Single(fixture.Bundle.Catalog.GetByRowId(65546)!.HandInItems); // A Feast to Say the Least
        Assert.Equal(GatherKind.Gathered, laurel.Gather);
    }

    [GameDataFact]
    public void Hundreds_of_quests_ask_for_items_and_the_placeholder_never_shows()
    {
        var quests = fixture.Bundle.Catalog.All.Where(q => q.HandInItems.Count > 0).ToList();
        output.WriteLine($"{quests.Count} quests, {quests.Sum(q => q.HandInItems.Count)} items");
        Assert.InRange(quests.Count, 500, 800);
        Assert.DoesNotContain(quests, q => q.HandInItems.Any(i => i.ItemId == QuestHandIns.PlaceholderItemId));
    }

    [Fact]
    public void The_fixture_carries_the_hand_in_items()
    {
        var catalog = new FixtureCatalog().Bundle.Catalog;
        Assert.Equal([1823u, 1895u], catalog.GetByRowId(65677)!.HandInItems.Select(i => i.ItemId));
        Assert.Equal(3, catalog.GetByRowId(68633)!.HandInItems[0].Amount);
    }

    [Fact]
    public void Recipes_carry_what_one_craft_makes()
    {
        // Artisan's amount counts crafts, so the yield (Recipe.AmountResult) travels with every recipe: at least one,
        // and more for some hand-in items (ingredients made several at a time).
        var recipes = new FixtureCatalog().Bundle.Catalog.All.SelectMany(q => q.HandInItems).SelectMany(i => i.Recipes).ToList();
        Assert.NotEmpty(recipes);
        Assert.All(recipes, r => Assert.True(r.Yield >= 1));
        Assert.Contains(recipes, r => r.Yield > 1);
    }
}
