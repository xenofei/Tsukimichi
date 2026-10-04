using System.Collections.Concurrent;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;

namespace Tsukimichi.GameData;

/// <summary>
/// Where an item comes from besides quests (feature plan v7, 1.19.0, C6 and N5), read once from the sheets: every
/// shop that sells it with the NPCs that open the shop and where they stand, every gathering node, fishing hole and
/// spearfishing spot that yields it, every recipe that makes it, and whether the market board takes it.
/// <list type="bullet">
/// <item><b>Shops.</b> <c>GilShopItem</c> (priced at <c>Item.PriceMid</c>, with the row's <c>QuestRequired</c> and
/// <c>AchievementRequired</c>: a Calamity salvager's "Purchase Quest Rewards" row names the quest that rewards the item),
/// <c>SpecialShop</c> (the slot's costs; an exchange whose currency the sheet names by an index below 100 rather than an
/// item keeps no price, so none is guessed) and <c>GCScripShopItem</c> (company seals). A shop that only runs during a
/// seasonal event is left out: it does not sell anything most of the year.</item>
/// <item><b>Vendors.</b> The NPCs whose <c>ENpcBase.ENpcData</c> opens the shop, directly or through a
/// <c>TopicSelect</c> menu, a <c>PreHandler</c>, a <c>CustomTalk</c> script or an <c>InclusionShop</c>. An NPC stands
/// where its <c>Level</c> row (type 8) says, else where the zone's event layout (<c>planevent.lgb</c>) places it; some
/// stand in neither (the Limsa Lominsa and Gridania Calamity salvagers live in the much larger <c>planner.lgb</c>, too
/// slow to read at load), and are named without a place.</item>
/// <item><b>Gathering.</b> <c>GatheringPointBase</c> items through <c>GatheringItem</c>, placed by the first
/// <c>GatheringPoint</c> of the base and <c>ExportedGatheringPoint</c> (unplaced when its territory has no map: the
/// allied society quests' own nodes); timed when <c>GatheringPointTransient</c> gives it a pop window. Fishing holes from <c>FishingSpot</c>, spearfishing from <c>SpearfishingNotebook</c>.</item>
/// <item><b>Recipes.</b> <c>Recipe</c> with the crafter's level and stars from <c>RecipeLevelTable</c>.</item>
/// </list>
/// Built on a worker (<c>IndexWarmer</c>); <see cref="For"/> is safe from any thread.
/// </summary>
public sealed class ItemSourceIndex
{
    /// <summary>Handler kinds of an NPC's event id (its high 16 bits), as FFXIVClientStructs' <c>EventHandlerContent</c> names them.</summary>
    private const uint GilShopHandler = 0x4;
    private const uint CustomTalkHandler = 0xB;
    private const uint GcShopHandler = 0x16;
    private const uint SpecialShopHandler = 0x1B;
    private const uint TopicSelectHandler = 0x32;
    private const uint PreHandlerHandler = 0x36;
    private const uint InclusionShopHandler = 0x3A;

    /// <summary>How deep menus inside menus are followed.</summary>
    private const int MaxDepth = 4;

    /// <summary><c>Level.Type</c> of an NPC placement.</summary>
    private const byte LevelNpc = 8;

    /// <summary>ClassJob rows of the gatherers and of the first crafter (CraftType n is ClassJob row 8 + n).</summary>
    private const byte Miner = 16;
    private const byte Botanist = 17;
    private const byte Fisher = 18;
    private const byte FirstCrafter = 8;

    /// <summary>Item rows of the company seals by GrandCompany row: Maelstrom, Order of the Twin Adder, Immortal Flames.</summary>
    private static readonly uint[] SealItems = [0, 20, 21, 22];

    public static readonly ItemSourceIndex Empty = new(
        new Dictionary<uint, ShopOffer[]>(),
        new Dictionary<uint, GatherSpot[]>(),
        new Dictionary<uint, CraftOption[]>(),
        static _ => false,
        new Dictionary<uint, string>());

    private readonly Dictionary<uint, ShopOffer[]> shops;
    private readonly Dictionary<uint, GatherSpot[]> gathering;
    private readonly Dictionary<uint, CraftOption[]> crafts;
    private readonly Func<uint, bool> marketable;
    private readonly IReadOnlyDictionary<uint, string> drops;
    private readonly ConcurrentDictionary<uint, ItemSources> cache = new();

    /// <summary>The index over plain tables (tests); <see cref="Build"/> reads them from the sheets.</summary>
    public ItemSourceIndex(
        IReadOnlyDictionary<uint, ShopOffer[]> shops,
        IReadOnlyDictionary<uint, GatherSpot[]> gathering,
        IReadOnlyDictionary<uint, CraftOption[]> crafts,
        Func<uint, bool> marketable,
        IReadOnlyDictionary<uint, string>? drops = null)
    {
        ArgumentNullException.ThrowIfNull(shops);
        ArgumentNullException.ThrowIfNull(gathering);
        ArgumentNullException.ThrowIfNull(crafts);
        this.shops = shops.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(ShopOrder).ToArray());
        this.gathering = gathering.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(s => s.Spot is null).ThenBy(s => s.Timed).ThenBy(s => s.Level).ThenBy(s => s.Spot?.TerritoryId ?? 0).ToArray());
        this.crafts = crafts.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(c => c.CraftType).ThenBy(c => c.Level).ThenBy(c => c.RecipeId).ToArray());
        this.marketable = marketable ?? throw new ArgumentNullException(nameof(marketable));
        this.drops = drops ?? new Dictionary<uint, string>();
    }

    /// <summary>Distinct items some shop sells.</summary>
    public int ShopItemCount => shops.Count;

    /// <summary>Distinct items some node, hole or spot yields.</summary>
    public int GatherItemCount => gathering.Count;

    /// <summary>Distinct items some recipe makes.</summary>
    public int CraftItemCount => crafts.Count;

    /// <summary>Every source of the item; <see cref="ItemSources.Any"/> is false when the data knows none.</summary>
    public ItemSources For(uint itemId)
    {
        if (itemId == 0)
        {
            return ItemSources.None(0);
        }

        return cache.GetOrAdd(itemId, id => new ItemSources(id)
        {
            Shops = shops.GetValueOrDefault(id) ?? [],
            Gathering = gathering.GetValueOrDefault(id) ?? [],
            Crafts = crafts.GetValueOrDefault(id) ?? [],
            Marketable = marketable(id),
            DropWhere = drops.GetValueOrDefault(id) ?? string.Empty,
        });
    }

    /// <summary>
    /// Reads the sheets (and, with <paramref name="readLayout"/>, the zones' event layouts for NPCs no <c>Level</c> row
    /// places). <paramref name="drops"/> are the curated duties an item drops in, by item id.
    /// </summary>
    public static ItemSourceIndex Build(ExcelModule excel, Language? language = null, IReadOnlyDictionary<uint, string>? drops = null, Func<string, LgbFile?>? readLayout = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var items = excel.GetSheet<Item>(language);
        var places = new Places(excel, language);
        var jobs = JobNames(excel, language);

        var vendorsByShop = VendorsByShop(excel, language);
        var npcIds = vendorsByShop.Values.SelectMany(v => v).ToHashSet();
        var spots = NpcSpots(excel, language, npcIds, places, readLayout);
        var residents = excel.GetSheet<ENpcResident>(language);
        var vendorCache = new Dictionary<uint, Vendor?>();
        Vendor? VendorOf(uint npcId)
        {
            if (!vendorCache.TryGetValue(npcId, out var vendor))
            {
                var resident = residents.GetRowOrDefault(npcId);
                var name = resident?.Singular.ExtractText().Trim() ?? string.Empty;

                // Article 1 marks a person's name; 0 a role ("Calamity salvager") that a sentence gives an article.
                vendor = name.Length == 0 || resident is not { } row ? null : new Vendor(npcId, name, spots.GetValueOrDefault(npcId))
                {
                    Generic = row.Article == 0,
                    StartsWithVowel = row.StartsWithVowel != 0,
                };
                vendorCache[npcId] = vendor;
            }

            return vendor;
        }

        IReadOnlyList<Vendor> VendorsOf(uint shopId)
        {
            if (!vendorsByShop.TryGetValue(shopId, out var ids))
            {
                return [];
            }

            return ids.Select(VendorOf).OfType<Vendor>().OrderBy(v => v.Spot is null).ThenBy(v => v.NpcId).ToArray();
        }

        var offers = new Dictionary<uint, List<ShopOffer>>();
        void AddOffer(uint itemId, ShopOffer offer)
        {
            if (!offers.TryGetValue(itemId, out var list))
            {
                offers[itemId] = list = [];
            }

            list.Add(offer);
        }

        ReadGilShops(excel, language, items, VendorsOf, AddOffer);
        ReadSpecialShops(excel, language, items, VendorsOf, AddOffer);
        ReadGrandCompanyShops(excel, language, items, vendorsByShop, VendorOf, AddOffer);

        var gather = new Dictionary<uint, List<GatherSpot>>();
        void AddSpot(uint itemId, GatherSpot spot)
        {
            if (itemId == 0)
            {
                return;
            }

            if (!gather.TryGetValue(itemId, out var list))
            {
                gather[itemId] = list = [];
            }

            if (!list.Contains(spot))
            {
                list.Add(spot);
            }
        }

        ReadGathering(excel, language, places, jobs, AddSpot);
        ReadFishing(excel, language, places, jobs, AddSpot);

        var recipes = new Dictionary<uint, List<CraftOption>>();
        foreach (var recipe in excel.GetSheet<Recipe>(language))
        {
            var result = recipe.ItemResult.RowId;
            if (result == 0 || recipe.CraftType.RowId > 7)
            {
                continue;
            }

            var craftType = (byte)recipe.CraftType.RowId;
            var level = recipe.RecipeLevelTable.ValueNullable;
            if (!recipes.TryGetValue(result, out var list))
            {
                recipes[result] = list = [];
            }

            list.Add(new CraftOption(
                recipe.RowId,
                craftType,
                jobs.GetValueOrDefault((byte)(FirstCrafter + craftType)) ?? string.Empty,
                level?.ClassJobLevel ?? 0,
                level?.Stars ?? 0,
                recipe.SecretRecipeBook.RowId != 0));
        }

        return new ItemSourceIndex(
            offers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            gather.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            recipes.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            id => items.GetRowOrDefault(id) is { } row && row.ItemSearchCategory.RowId != 0,
            drops);
    }

    /// <summary>Shops sort gil first, then seals, then exchanges; ungated, then priced, then cheapest, then placed.</summary>
    private static (int Kind, bool Gated, bool NoPrice, ulong Price, bool Unplaced, uint Shop) ShopOrder(ShopOffer offer) => (
        offer.Kind switch { ShopKind.Gil => 0, ShopKind.GrandCompany => 1, _ => 2 },
        offer.Gated,
        offer.Costs.Count == 0,
        offer.Costs.Aggregate(0UL, (sum, c) => sum + c.Amount),
        offer.FirstVendor?.Spot is null,
        offer.ShopId);

    private static void ReadGilShops(ExcelModule excel, Language? language, ExcelSheet<Item> items, Func<uint, IReadOnlyList<Vendor>> vendorsOf, Action<uint, ShopOffer> add)
    {
        var shops = excel.GetSheet<GilShop>(language);
        var gilName = ItemName(items, ShopCost.GilItemId, 2);
        foreach (var rows in excel.GetSubrowSheet<GilShopItem>(language))
        {
            if (shops.GetRowOrDefault(rows.RowId) is not { } shop || shop.FestivalId != 0)
            {
                continue;
            }

            var vendors = vendorsOf(rows.RowId);
            if (vendors.Count == 0)
            {
                continue;
            }

            var shopName = shop.Name.ExtractText().Trim();
            foreach (var row in rows)
            {
                var itemId = row.Item.RowId;
                if (itemId == 0 || items.GetRowOrDefault(itemId) is not { } item)
                {
                    continue;
                }

                var quest = 0u;
                foreach (var required in row.QuestRequired)
                {
                    if (required.RowId != 0)
                    {
                        quest = required.RowId;
                        break;
                    }
                }

                ShopCost[] costs = item.PriceMid == 0 ? [] : [new ShopCost(ShopCost.GilItemId, gilName, item.PriceMid)];
                add(itemId, new ShopOffer(ShopKind.Gil, rows.RowId, shopName, vendors, costs)
                {
                    RequiredQuest = quest != 0 ? quest : shop.Quest.RowId,
                    RequiredAchievement = row.AchievementRequired.RowId,
                });
            }
        }
    }

    private static void ReadSpecialShops(ExcelModule excel, Language? language, ExcelSheet<Item> items, Func<uint, IReadOnlyList<Vendor>> vendorsOf, Action<uint, ShopOffer> add)
    {
        foreach (var shop in excel.GetSheet<SpecialShop>(language))
        {
            if (shop.RequiredFestival.RowId != 0)
            {
                continue;
            }

            var vendors = vendorsOf(shop.RowId);
            if (vendors.Count == 0)
            {
                continue;
            }

            var shopName = shop.Name.ExtractText().Trim();
            foreach (var slot in shop.Item)
            {
                List<ShopCost>? costs = [];
                foreach (var cost in slot.ItemCosts)
                {
                    var costId = cost.ItemCost.RowId;
                    if (costId == 0 || cost.CurrencyCost == 0)
                    {
                        continue;
                    }

                    // Below 100 a shop that uses a currency type names the currency by an index, not an item: no price.
                    if (shop.UseCurrencyType != 0 && costId < 100)
                    {
                        costs = null;
                        break;
                    }

                    var name = ItemName(items, costId, cost.CurrencyCost);
                    if (name.Length == 0)
                    {
                        costs = null;
                        break;
                    }

                    costs.Add(new ShopCost(costId, name, cost.CurrencyCost));
                }

                foreach (var receive in slot.ReceiveItems)
                {
                    if (receive.Item.RowId == 0)
                    {
                        continue;
                    }

                    add(receive.Item.RowId, new ShopOffer(ShopKind.Exchange, shop.RowId, shopName, vendors, costs?.ToArray() ?? [])
                    {
                        RequiredQuest = slot.Quest.RowId != 0 ? slot.Quest.RowId : shop.Quest.RowId,
                        RequiredAchievement = slot.AchievementUnlock.RowId,
                    });
                }
            }
        }
    }

    private static void ReadGrandCompanyShops(
        ExcelModule excel,
        Language? language,
        ExcelSheet<Item> items,
        Dictionary<uint, HashSet<uint>> vendorsByShop,
        Func<uint, Vendor?> vendorOf,
        Action<uint, ShopOffer> add)
    {
        // The quartermasters: NPCs that open a GCShop row, by the row's Grand Company.
        var gcShops = excel.GetSheet<GCShop>(language);
        var quartermasters = new Dictionary<uint, (uint ShopId, List<Vendor> Vendors)>();
        foreach (var (shopId, npcIds) in vendorsByShop)
        {
            if (shopId >> 16 != GcShopHandler || gcShops.GetRowOrDefault(shopId) is not { } shop || shop.GrandCompany.RowId == 0)
            {
                continue;
            }

            if (!quartermasters.TryGetValue(shop.GrandCompany.RowId, out var known))
            {
                quartermasters[shop.GrandCompany.RowId] = known = (shopId, []);
            }

            known.Vendors.AddRange(npcIds.Select(vendorOf).OfType<Vendor>());
        }

        var companies = excel.GetSheet<GrandCompany>(language);
        var categories = excel.GetSheet<GCScripShopCategory>(language);
        foreach (var rows in excel.GetSubrowSheet<GCScripShopItem>(language))
        {
            var company = categories.GetRowOrDefault(rows.RowId)?.GrandCompany.RowId ?? 0;
            if (company == 0 || company >= SealItems.Length || !quartermasters.TryGetValue(company, out var quartermaster) || quartermaster.Vendors.Count == 0)
            {
                continue;
            }

            var vendors = quartermaster.Vendors.DistinctBy(v => v.NpcId).OrderBy(v => v.Spot is null).ThenBy(v => v.NpcId).ToArray();
            var shopName = companies.GetRowOrDefault(company)?.Name.ExtractText().Trim() ?? string.Empty;
            foreach (var row in rows)
            {
                if (row.Item.RowId == 0 || row.CostGCSeals == 0)
                {
                    continue;
                }

                var seal = SealItems[company];
                add(row.Item.RowId, new ShopOffer(ShopKind.GrandCompany, quartermaster.ShopId, shopName, vendors, [new ShopCost(seal, ItemName(items, seal, row.CostGCSeals), row.CostGCSeals)]));
            }
        }
    }

    private static void ReadGathering(ExcelModule excel, Language? language, Places places, Dictionary<byte, string> jobs, Action<uint, GatherSpot> add)
    {
        var gatheringItems = excel.GetSheet<GatheringItem>(language);
        var exported = excel.GetSheet<ExportedGatheringPoint>(language);
        var transients = excel.GetSheet<GatheringPointTransient>(language);

        // One spot per base: its first point with a territory places it; a timed point makes the base timed.
        var points = new Dictionary<uint, (GatheringPoint Point, bool Timed)>();
        foreach (var point in excel.GetSheet<GatheringPoint>(language))
        {
            var baseId = point.GatheringPointBase.RowId;
            if (baseId == 0 || point.TerritoryType.RowId == 0)
            {
                continue;
            }

            var timed = transients.GetRowOrDefault(point.RowId) is { } transient
                && (transient.GatheringRarePopTimeTable.RowId != 0 || transient.EphemeralStartTime != transient.EphemeralEndTime);
            if (points.TryGetValue(baseId, out var known))
            {
                points[baseId] = (known.Point, known.Timed || timed);
            }
            else
            {
                points[baseId] = (point, timed);
            }
        }

        foreach (var pointBase in excel.GetSheet<GatheringPointBase>(language))
        {
            var type = pointBase.GatheringType.RowId;
            if (type > 3 || !points.TryGetValue(pointBase.RowId, out var placed))
            {
                continue;
            }

            // A point in a territory with no map (the allied society quests' own nodes sit in territory 1) stays unplaced.
            var spot = exported.GetRowOrDefault(pointBase.RowId) is { } at ? places.Spot(placed.Point.TerritoryType.RowId, at.X, at.Y) : null;

            var job = type <= 1 ? Miner : Botanist;
            var place = placed.Point.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
            var gatherSpot = new GatherSpot(GatherKind.Gathered, job, jobs.GetValueOrDefault(job) ?? string.Empty, pointBase.GatheringLevel, place, spot, placed.Timed)
            {
                // GatheringType 0 Mining, 1 Quarrying, 2 Logging, 3 Harvesting.
                Method = (GatherMethod)type,
            };
            foreach (var entry in pointBase.Item)
            {
                if (entry.RowId != 0 && gatheringItems.GetRowOrDefault(entry.RowId) is { } gatheringItem && gatheringItem.Item.RowId is > 0 and < QuestHandIns.EventItemBase)
                {
                    add(gatheringItem.Item.RowId, gatherSpot);
                }
            }
        }
    }

    private static void ReadFishing(ExcelModule excel, Language? language, Places places, Dictionary<byte, string> jobs, Action<uint, GatherSpot> add)
    {
        var fisher = jobs.GetValueOrDefault(Fisher) ?? string.Empty;
        foreach (var hole in excel.GetSheet<FishingSpot>(language))
        {
            if (hole.TerritoryType.RowId == 0 || places.SpotFromPixels(hole.TerritoryType.RowId, hole.X, hole.Z) is not { } spot)
            {
                continue;
            }

            var place = hole.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
            var gatherSpot = new GatherSpot(GatherKind.Fish, Fisher, fisher, hole.GatheringLevel, place, spot);
            foreach (var fish in hole.Item)
            {
                if (fish.RowId is > 0 and < QuestHandIns.EventItemBase)
                {
                    add(fish.RowId, gatherSpot);
                }
            }
        }

        var spearItems = excel.GetSheet<SpearfishingItem>(language);
        var bases = excel.GetSheet<GatheringPointBase>(language);
        foreach (var notebook in excel.GetSheet<SpearfishingNotebook>(language))
        {
            if (notebook.TerritoryType.RowId == 0
                || bases.GetRowOrDefault(notebook.GatheringPointBase.RowId) is not { } pointBase
                || places.SpotFromPixels(notebook.TerritoryType.RowId, notebook.X, notebook.Y) is not { } spot)
            {
                continue;
            }

            var place = notebook.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
            var gatherSpot = new GatherSpot(GatherKind.Fish, Fisher, fisher, notebook.GatheringLevel, place, spot) { Method = GatherMethod.Spearfishing };
            foreach (var entry in pointBase.Item)
            {
                if (entry.RowId != 0 && spearItems.GetRowOrDefault(entry.RowId) is { } spear && spear.Item.RowId is > 0 and < QuestHandIns.EventItemBase)
                {
                    add(spear.Item.RowId, gatherSpot);
                }
            }
        }
    }

    /// <summary>The shops each NPC opens, through menus, pre-handlers, scripts and inclusion shops: shop event id to NPC ids.</summary>
    private static Dictionary<uint, HashSet<uint>> VendorsByShop(ExcelModule excel, Language? language)
    {
        var topics = excel.GetSheet<TopicSelect>(language);
        var preHandlers = excel.GetSheet<PreHandler>(language);
        var talks = excel.GetSheet<CustomTalk>(language);
        var inclusion = excel.GetSheet<InclusionShop>(language);
        var series = excel.GetSubrowSheet<InclusionShopSeries>(language);
        var result = new Dictionary<uint, HashSet<uint>>();

        void Collect(uint handler, uint npcId, int depth)
        {
            if (handler == 0 || depth > MaxDepth)
            {
                return;
            }

            switch (handler >> 16)
            {
                case GilShopHandler or SpecialShopHandler or GcShopHandler:
                    if (!result.TryGetValue(handler, out var npcs))
                    {
                        result[handler] = npcs = [];
                    }

                    npcs.Add(npcId);
                    break;
                case TopicSelectHandler when topics.GetRowOrDefault(handler) is { } topic:
                    foreach (var shop in topic.Shop)
                    {
                        Collect(shop.RowId, npcId, depth + 1);
                    }

                    break;
                case PreHandlerHandler when preHandlers.GetRowOrDefault(handler) is { } pre:
                    Collect(pre.Target.RowId, npcId, depth + 1);
                    break;
                case CustomTalkHandler when talks.GetRowOrDefault(handler) is { } talk:
                    foreach (var script in talk.Script)
                    {
                        if (script.ScriptArg >> 16 is GilShopHandler or SpecialShopHandler or GcShopHandler or TopicSelectHandler or PreHandlerHandler or InclusionShopHandler)
                        {
                            Collect(script.ScriptArg, npcId, depth + 1);
                        }
                    }

                    break;
                case InclusionShopHandler when inclusion.GetRowOrDefault(handler) is { } shop:
                    foreach (var category in shop.Category)
                    {
                        if (category.RowId == 0 || category.ValueNullable is not { } value || series.GetRowOrDefault(value.InclusionShopSeries.RowId) is not { } rows)
                        {
                            continue;
                        }

                        foreach (var row in rows)
                        {
                            Collect(row.SpecialShop.RowId, npcId, depth + 1);
                        }
                    }

                    break;
            }
        }

        foreach (var npc in excel.GetSheet<ENpcBase>(language))
        {
            foreach (var data in npc.ENpcData)
            {
                Collect(data.RowId, npc.RowId, 0);
            }
        }

        return result;
    }

    /// <summary>Where each of <paramref name="npcIds"/> stands: its first <c>Level</c> row, else the first event layout that places it.</summary>
    internal static Dictionary<uint, WorldSpot> NpcSpots(ExcelModule excel, Language? language, HashSet<uint> npcIds, Places places, Func<string, LgbFile?>? readLayout)
    {
        var result = new Dictionary<uint, WorldSpot>();
        foreach (var level in excel.GetSheet<Level>(language))
        {
            if (level.Type == LevelNpc && npcIds.Contains(level.Object.RowId) && !result.ContainsKey(level.Object.RowId)
                && places.Spot(level.Territory.RowId, level.X, level.Z) is { } spot)
            {
                result[level.Object.RowId] = spot;
            }
        }

        if (readLayout is null || result.Count == npcIds.Count)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var territory in excel.GetSheet<TerritoryType>(language))
        {
            var bg = territory.Bg.ExtractText();
            var slash = bg.LastIndexOf('/');
            if (slash <= 0 || !places.HasMap(territory.RowId))
            {
                continue;
            }

            var path = $"bg/{bg[..slash]}/planevent.lgb";
            if (!seen.Add(path) || Read(readLayout, path) is not { } layout)
            {
                continue;
            }

            foreach (var layer in layout.Layers)
            {
                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.Object is LayerCommon.ENPCInstanceObject npc
                        && npc.ParentData.ParentData.BaseId is var id && npcIds.Contains(id) && !result.ContainsKey(id)
                        && places.Spot(territory.RowId, instance.Transform.Translation.X, instance.Transform.Translation.Z) is { } spot)
                    {
                        result[id] = spot;
                    }
                }
            }
        }

        return result;
    }

    private static LgbFile? Read(Func<string, LgbFile?> readLayout, string path)
    {
        try
        {
            return readLayout(path);
        }
        catch (Exception)
        {
            // A layout that does not parse (a newer format) places nobody: those NPCs are named without a place.
            return null;
        }
    }

    /// <summary>ClassJob names (the gatherers and crafters), by row.</summary>
    private static Dictionary<byte, string> JobNames(ExcelModule excel, Language? language)
    {
        var result = new Dictionary<byte, string>();
        var sheet = excel.GetSheet<ClassJob>(language);
        for (var row = FirstCrafter; row <= Fisher; row++)
        {
            if (sheet.GetRowOrDefault(row) is { } job)
            {
                result[row] = job.Name.ExtractText().Trim();
            }
        }

        return result;
    }

    /// <summary>The item's name for an amount: its plural when the sheet has one and the amount is not one.</summary>
    private static string ItemName(ExcelSheet<Item> items, uint itemId, uint amount)
    {
        if (items.GetRowOrDefault(itemId) is not { } item)
        {
            return string.Empty;
        }

        var plural = amount == 1 ? string.Empty : item.Plural.ExtractText().Trim();
        return plural.Length > 0 ? plural : item.Name.ExtractText().Trim();
    }

    /// <summary>Zone names and the maps that turn world positions into the coordinates the game prints, per territory.</summary>
    internal sealed class Places(ExcelModule excel, Language? language)
    {
        private readonly ExcelSheet<TerritoryType> territories = excel.GetSheet<TerritoryType>(language);
        private readonly Dictionary<uint, (string Zone, Map Map)?> cache = [];

        public bool HasMap(uint territoryId) => Of(territoryId) is not null;

        /// <summary>A world position (X, Z) in the territory; null when it has no map or no name.</summary>
        public WorldSpot? Spot(uint territoryId, float x, float z)
        {
            if (Of(territoryId) is not { } place)
            {
                return null;
            }

            var map = place.Map;
            return new WorldSpot(territoryId, place.Zone, x, z, MapCoordinate(x, map.OffsetX, map.SizeFactor), MapCoordinate(z, map.OffsetY, map.SizeFactor));
        }

        /// <summary>A map-image position (0–2048 pixels, as <c>FishingSpot</c> and <c>SpearfishingNotebook</c> give it).</summary>
        public WorldSpot? SpotFromPixels(uint territoryId, float x, float y)
        {
            if (Of(territoryId) is not { } place)
            {
                return null;
            }

            var map = place.Map;
            return Spot(territoryId, AetheryteIndex.ToRaw(x, map.OffsetX, map.SizeFactor), AetheryteIndex.ToRaw(y, map.OffsetY, map.SizeFactor));
        }

        private (string Zone, Map Map)? Of(uint territoryId)
        {
            if (territoryId == 0)
            {
                return null;
            }

            if (cache.TryGetValue(territoryId, out var known))
            {
                return known;
            }

            (string, Map)? place = null;
            if (territories.GetRowOrDefault(territoryId) is { } territory && territory.Map.ValueNullable is { } map)
            {
                var zone = territory.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
                if (zone.Length > 0)
                {
                    place = (zone, map);
                }
            }

            cache[territoryId] = place;
            return place;
        }
    }

    /// <summary>The game's map coordinate of a world coordinate (one-based, as the map and the chat print it).</summary>
    public static float MapCoordinate(float raw, short offset, ushort sizeFactor)
    {
        var scale = sizeFactor / 100f;
        if (scale <= 0f)
        {
            scale = 1f;
        }

        return 41f / scale * ((raw + offset) * scale + 1024f) / 2048f + 1f;
    }
}
