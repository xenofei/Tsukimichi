using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Sources;

/// <summary>What kind of shop sells an item.</summary>
public enum ShopKind : byte
{
    /// <summary>A gil shop (<c>GilShop</c>): the price is the item's own (<c>Item.PriceMid</c>).</summary>
    Gil,

    /// <summary>An exchange (<c>SpecialShop</c>): tomestones, scrips, seals, MGP or other items.</summary>
    Exchange,

    /// <summary>A Grand Company quartermaster (<c>GCScripShopItem</c>): company seals.</summary>
    GrandCompany,
}

/// <summary>
/// A place in the world: the territory, its zone name, the world position (what a map flag takes) and the map
/// coordinates the game prints for it.
/// </summary>
/// <param name="Zone">The territory's place name ("Limsa Lominsa Lower Decks").</param>
/// <param name="X">World X.</param>
/// <param name="Z">World Z (the map's vertical axis).</param>
/// <param name="MapX">The map coordinate the game shows for <paramref name="X"/> (one decimal, 1-based).</param>
/// <param name="MapY">The map coordinate the game shows for <paramref name="Z"/>.</param>
public sealed record WorldSpot(uint TerritoryId, string Zone, float X, float Z, float MapX, float MapY);

/// <summary>An NPC that opens a shop, with where it stands when the data places it.</summary>
/// <param name="Name">The NPC's name as the game writes it ("Calamity salvager", "Ranaa Mihgo").</param>
/// <param name="Spot">Where it stands (a <c>Level</c> row, else the zone's event layout); null when the data does not say.</param>
public sealed record Vendor(uint NpcId, string Name, WorldSpot? Spot);

/// <summary>What one purchase costs in one currency.</summary>
/// <param name="ItemId">The currency's item row: 1 for gil, 20–22 for company seals, an item for an exchange.</param>
/// <param name="Name">The currency's name for <paramref name="Amount"/> (singular or plural as the sheet has it).</param>
public sealed record ShopCost(uint ItemId, string Name, uint Amount)
{
    /// <summary>Item row of gil.</summary>
    public const uint GilItemId = 1;

    /// <summary>Whether the cost is gil.</summary>
    public bool IsGil => ItemId == GilItemId;
}

/// <summary>
/// One shop that hands an item out. <see cref="Costs"/> is empty when the data cannot say what it costs (an exchange
/// whose currency the sheet names by an index rather than an item): the shop is still named, the price never guessed.
/// </summary>
/// <param name="Vendors">The NPCs that open the shop, located ones first; empty when no NPC opens it.</param>
public sealed record ShopOffer(ShopKind Kind, uint ShopId, string ShopName, IReadOnlyList<Vendor> Vendors, IReadOnlyList<ShopCost> Costs)
{
    /// <summary>
    /// The quest the shop wants done before it sells the item (<c>GilShopItem.QuestRequired</c>, the exchange slot's
    /// quest); 0 when none. A Calamity salvager's "Purchase Quest Rewards" row names the quest that rewards the item.
    /// </summary>
    public uint RequiredQuest { get; init; }

    /// <summary>The achievement the shop wants earned first (<c>GilShopItem.AchievementRequired</c>); 0 when none.</summary>
    public uint RequiredAchievement { get; init; }

    /// <summary>
    /// Whether the row sells the item only to a character that did something first (a quest or an achievement): the
    /// Calamity salvager's and the recompense officer's reclaim menus, and every vendor stock a quest opens.
    /// </summary>
    public bool Gated => RequiredQuest != 0 || RequiredAchievement != 0;

    /// <summary>The first vendor, the one a line names; null when no NPC opens the shop.</summary>
    public Vendor? FirstVendor => Vendors.Count == 0 ? null : Vendors[0];

    /// <summary>The gil price; null when the shop does not take gil alone.</summary>
    public uint? GilPrice => Costs.Count == 1 && Costs[0].IsGil ? Costs[0].Amount : null;
}

/// <summary>A gathering node, fishing hole or spearfishing spot that yields an item.</summary>
/// <param name="Kind">Gathered (a mining or botany node) or fished.</param>
/// <param name="JobId">ClassJob row of the gatherer: 16 Miner, 17 Botanist, 18 Fisher.</param>
/// <param name="JobName">That job's name in the catalog's language.</param>
/// <param name="Level">The node's or the hole's level.</param>
/// <param name="Place">The spot's own place name ("Black Brush", "The Vein"); empty when the sheet gives none.</param>
/// <param name="Spot">
/// Where it is (its <see cref="WorldSpot.Zone"/> is the zone); null for a node the sheets do not place, such as the
/// allied society quests' own nodes, which appear only while the quest is under way.
/// </param>
/// <param name="Timed">An unspoiled, legendary or ephemeral node: up only at set Eorzea times.</param>
public sealed record GatherSpot(GatherKind Kind, byte JobId, string JobName, byte Level, string Place, WorldSpot? Spot, bool Timed = false);

/// <summary>A recipe that makes an item, with the crafter and the level it needs.</summary>
/// <param name="JobName">The crafter's name in the catalog's language.</param>
/// <param name="Level">The crafter level (<c>RecipeLevelTable.ClassJobLevel</c>).</param>
/// <param name="Stars">The recipe's stars; zero for most.</param>
/// <param name="MasterBook">Whether a master recipe book must be read first.</param>
public sealed record CraftOption(uint RecipeId, byte CraftType, string JobName, byte Level, byte Stars = 0, bool MasterBook = false);

/// <summary>
/// Everything the game data says about where an item comes from, besides quests: shops, gathering, recipes and the
/// market board. Built per item by <c>Tsukimichi.GameData.ItemSourceIndex</c>; read by the Hand in section's "Where"
/// lines (<see cref="WhereToGet"/>) and the rewards' buy-back mark (<see cref="BuyBacks"/>).
/// </summary>
public sealed record ItemSources(uint ItemId)
{
    private static readonly ShopOffer[] NoShops = [];
    private static readonly GatherSpot[] NoSpots = [];
    private static readonly CraftOption[] NoCrafts = [];

    /// <summary>The item no source is known for.</summary>
    public static ItemSources None(uint itemId) => new(itemId);

    /// <summary>Every shop that sells it, gil shops first, cheapest and ungated first within each kind.</summary>
    public IReadOnlyList<ShopOffer> Shops { get; init; } = NoShops;

    /// <summary>Every node or hole that yields it: placed before unplaced, untimed before timed, lowest level first.</summary>
    public IReadOnlyList<GatherSpot> Gathering { get; init; } = NoSpots;

    /// <summary>Every recipe that makes it, by crafter.</summary>
    public IReadOnlyList<CraftOption> Crafts { get; init; } = NoCrafts;

    /// <summary>Whether it can be sold on the market board (<c>Item.ItemSearchCategory</c> set).</summary>
    public bool Marketable { get; init; }

    /// <summary>
    /// The duties it drops in, from the curated other sources (<c>curated/other_sources.json</c>); empty when the data
    /// names none.
    /// </summary>
    public string DropWhere { get; init; } = string.Empty;

    /// <summary>Whether any source is known.</summary>
    public bool Any => Shops.Count > 0 || Gathering.Count > 0 || Crafts.Count > 0 || Marketable || DropWhere.Length > 0;
}
