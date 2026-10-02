using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;

namespace Tsukimichi.GameData;

/// <summary>
/// The items a quest asks the player to hand over (<see cref="QuestRecord.HandInItems"/>), from two places in the
/// sheets:
/// <list type="number">
/// <item><c>Quest.QuestClassJobSupply</c>: the allied society gatherer and crafter quests and the Dawntrail role
/// quests list each delivery with its amount, whether it must be high quality and the ClassJobCategory it is for
/// ("One Size Fits All": 3 Happi Components for a crafter, 3 Red Malachite for a miner, 1 Sunshell for a fisher).</item>
/// <item>The script's <c>RITEMn</c> constants (<c>Quest.QuestParams</c>, read as <see cref="QuestScriptDuties"/> reads
/// them): every Item row the script's request or equip checks name. They carry no amount and no quality.</item>
/// </list>
/// <para>
/// <c>RITEM</c> is not only what the player hands in. Checked against each quest's own objective lines (all 1,023
/// constants of 632 quests in the 7.5 sheets) and the wiki, it also names: the allied society placeholder item 100
/// (Dated Bronze Gladius, on every Namazu, Qitari, Omicron and Mamool Ja quest); the materials an NPC hands out for the
/// player to craft the delivered item from ("Rigging Component Materials" beside "Rigging Component"); and gear the
/// player is given to wear (the job quests' artifact armour, festival hats and tunics, disguises, an unfinished relic
/// weapon). So a <c>RITEM</c> item is left out when it is the placeholder, when it is an untradable ingredient of a
/// recipe that makes another of the quest's items, or when it is equipment no recipe makes that is untradable or
/// item level 1 (the shepherd's garb an NPC lends for "Dressed to Deceive"). Every other one stayed a hand-in in the
/// check: 449 named by a "Deliver/Give/Present/Show …" objective, the rest on lines like "Deliver the requested
/// items" (QuestHandInsTests pins examples).
/// </para>
/// </summary>
public static class QuestHandIns
{
    public const string RequestItemPrefix = "RITEM";
    public const string ProductItemPrefix = "QST_PRODUCT_ITEM";

    /// <summary>Item 100 ("Dated Bronze Gladius"): the placeholder <c>RITEM0</c> of the allied society supply quests.</summary>
    public const uint PlaceholderItemId = 100;

    /// <summary>Item ids from here on are EventItem rows (key items); the quest itself hands those out.</summary>
    public const uint EventItemBase = 2_000_000;

    /// <summary>What the rules read about an item.</summary>
    /// <param name="IsEquipment">The item has an equip slot (<c>Item.EquipSlotCategory</c> non-zero).</param>
    /// <param name="ItemLevel"><c>Item.LevelItem</c>.</param>
    public sealed record ItemFacts(uint ItemId, string Name, uint Icon, bool IsUntradable, bool IsEquipment, uint ItemLevel);

    /// <summary>One <c>QuestClassJobSupply</c> subrow.</summary>
    public readonly record struct SupplyRow(uint ItemId, byte Amount, bool IsHq, uint ClassJobCategory);

    /// <summary>A recipe as the rules read it: what it is, which crafter, what goes in and how many one craft makes.</summary>
    public sealed record RecipeFacts(uint RecipeId, byte CraftType, IReadOnlyList<uint> Ingredients, byte Yield = 1);

    /// <summary>
    /// The item side of the rules: item facts, the recipes that make each item and how each item is gathered. Built
    /// once per catalog from the sheets (<see cref="Build"/>), or from plain tables in tests.
    /// </summary>
    public sealed class Sources(
        Func<uint, ItemFacts?> item,
        IReadOnlyDictionary<uint, IReadOnlyList<RecipeFacts>> recipesByResult,
        IReadOnlyDictionary<uint, GatherKind> gather)
    {
        private static readonly RecipeFacts[] NoRecipes = [];

        public ItemFacts? Item(uint itemId) => itemId == 0 ? null : item(itemId);

        public IReadOnlyList<RecipeFacts> RecipesOf(uint itemId) => recipesByResult.GetValueOrDefault(itemId) ?? NoRecipes;

        public GatherKind GatherOf(uint itemId) => gather.GetValueOrDefault(itemId);

        /// <summary>Reads the Item, Recipe, GatheringItem, FishParameter and SpearfishingItem sheets.</summary>
        public static Sources Build(ExcelModule excel, Language? language = null)
        {
            ArgumentNullException.ThrowIfNull(excel);
            var items = excel.GetSheet<Item>(language);
            var recipes = new Dictionary<uint, List<RecipeFacts>>();
            foreach (var recipe in excel.GetSheet<Recipe>(language))
            {
                var result = recipe.ItemResult.RowId;
                if (result == 0)
                {
                    continue;
                }

                var ingredients = new List<uint>(recipe.Ingredient.Count);
                foreach (var ingredient in recipe.Ingredient)
                {
                    if (ingredient.RowId != 0)
                    {
                        ingredients.Add(ingredient.RowId);
                    }
                }

                if (!recipes.TryGetValue(result, out var list))
                {
                    recipes[result] = list = [];
                }

                list.Add(new RecipeFacts(recipe.RowId, (byte)Math.Min(recipe.CraftType.RowId, byte.MaxValue), ingredients, Math.Max(recipe.AmountResult, (byte)1)));
            }

            var gather = new Dictionary<uint, GatherKind>();
            foreach (var row in excel.GetSheet<GatheringItem>(language))
            {
                if (row.Item.RowId is > 0 and < EventItemBase)
                {
                    gather.TryAdd(row.Item.RowId, GatherKind.Gathered);
                }
            }

            // A fish is a fish even when a node also lists it.
            foreach (var row in excel.GetSheet<FishParameter>(language))
            {
                if (row.Item.RowId is > 0 and < EventItemBase)
                {
                    gather[row.Item.RowId] = GatherKind.Fish;
                }
            }

            foreach (var row in excel.GetSheet<SpearfishingItem>(language))
            {
                if (row.Item.RowId is > 0 and < EventItemBase)
                {
                    gather[row.Item.RowId] = GatherKind.Fish;
                }
            }

            var ordered = recipes.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<RecipeFacts>)kv.Value.OrderBy(r => r.CraftType).ThenBy(r => r.RecipeId).ToArray());
            return new Sources(id => Facts(items, id), ordered, gather);
        }

        private static ItemFacts? Facts(ExcelSheet<Item> items, uint itemId)
        {
            if (items.GetRowOrDefault(itemId) is not { } row)
            {
                return null;
            }

            var name = row.Name.ExtractText();
            return name.Length == 0
                ? null
                : new ItemFacts(itemId, name, row.Icon, row.IsUntradable, row.EquipSlotCategory.RowId != 0, row.LevelItem.RowId);
        }
    }

    /// <summary>
    /// One quest's hand-in items from its sheet row: the supply rows of <c>Quest.QuestClassJobSupply</c> and the script's
    /// constants. Empty for most quests.
    /// </summary>
    public static IReadOnlyList<HandInItem> Of(in Quest quest, SubrowExcelSheet<QuestClassJobSupply> supplySheet, Sources sources)
    {
        ArgumentNullException.ThrowIfNull(supplySheet);
        ArgumentNullException.ThrowIfNull(sources);
        List<(string Name, uint Arg)>? constants = null;
        foreach (var param in quest.QuestParams)
        {
            var name = param.ScriptInstruction.ExtractText();
            if (name.StartsWith(RequestItemPrefix, StringComparison.Ordinal) || name.StartsWith(ProductItemPrefix, StringComparison.Ordinal))
            {
                (constants ??= []).Add((name, param.ScriptArg));
            }
        }

        List<SupplyRow>? supply = null;
        if (quest.QuestClassJobSupply.RowId != 0 && supplySheet.GetRowOrDefault(quest.QuestClassJobSupply.RowId) is { } rows)
        {
            foreach (var row in rows)
            {
                if (row.Item.RowId != 0)
                {
                    (supply ??= []).Add(new SupplyRow(row.Item.RowId, row.AmountRequired, row.ItemHQ, row.ClassJobCategory.RowId));
                }
            }
        }

        return constants is null && supply is null ? [] : Read(constants ?? [], supply ?? [], sources);
    }

    /// <summary>
    /// The rules over plain inputs: <paramref name="constants"/> are the script's (name, argument) pairs in script
    /// order (only <c>RITEM</c> and <c>QST_PRODUCT_ITEM</c> are read), <paramref name="supply"/> the supply subrows in
    /// order. Supply items come first, each once (an item listed for two jobs carries both categories), then the
    /// <c>RITEM</c> items the rules keep, each once.
    /// </summary>
    public static IReadOnlyList<HandInItem> Read(IEnumerable<(string Name, uint Arg)> constants, IEnumerable<SupplyRow> supply, Sources sources)
    {
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(supply);
        ArgumentNullException.ThrowIfNull(sources);

        var requested = new List<uint>();
        var questItems = new HashSet<uint>();
        foreach (var (name, arg) in constants)
        {
            if (arg == 0)
            {
                continue;
            }

            if (name.StartsWith(RequestItemPrefix, StringComparison.Ordinal))
            {
                requested.Add(arg);
                questItems.Add(arg);
            }
            else if (name.StartsWith(ProductItemPrefix, StringComparison.Ordinal))
            {
                questItems.Add(arg);
            }
        }

        var result = new List<HandInItem>();
        var index = new Dictionary<uint, int>();
        foreach (var row in supply)
        {
            questItems.Add(row.ItemId);
            if (sources.Item(row.ItemId) is not { } facts)
            {
                continue;
            }

            if (index.TryGetValue(row.ItemId, out var at))
            {
                var known = result[at];
                var categories = row.ClassJobCategory == 0 || known.ClassJobCategories.Length == 0
                    ? []
                    : known.ClassJobCategories.Contains(row.ClassJobCategory) ? known.ClassJobCategories : [.. known.ClassJobCategories, row.ClassJobCategory];
                result[at] = known with
                {
                    Amount = Math.Max(known.Amount, row.Amount),
                    IsHq = known.IsHq || row.IsHq,
                    ClassJobCategories = categories,
                };
                continue;
            }

            index[row.ItemId] = result.Count;
            result.Add(Item(facts, sources, row.Amount, row.IsHq, row.ClassJobCategory == 0 ? [] : [row.ClassJobCategory]));
        }

        foreach (var itemId in requested)
        {
            if (index.ContainsKey(itemId) || !IsHandIn(itemId, questItems, sources) || sources.Item(itemId) is not { } facts)
            {
                continue;
            }

            index[itemId] = result.Count;
            result.Add(Item(facts, sources, 0, false, []));
        }

        return result.Count == 0 ? [] : result.ToArray();
    }

    /// <summary>
    /// Whether a <c>RITEM</c> item is one the player hands in rather than one the script hands out (see the class
    /// summary): not the placeholder, not a key item, not an untradable material of another of the quest's items, not
    /// gear the quest lends.
    /// </summary>
    public static bool IsHandIn(uint itemId, IReadOnlySet<uint> questItems, Sources sources)
    {
        ArgumentNullException.ThrowIfNull(questItems);
        ArgumentNullException.ThrowIfNull(sources);
        if (itemId is 0 or PlaceholderItemId or >= EventItemBase || sources.Item(itemId) is not { } facts)
        {
            return false;
        }

        var made = sources.RecipesOf(itemId).Count > 0;
        if (facts.IsEquipment && !made && (facts.IsUntradable || facts.ItemLevel <= 1))
        {
            return false;
        }

        return !(facts.IsUntradable && IsMaterialOfAnother(itemId, questItems, sources));
    }

    private static bool IsMaterialOfAnother(uint itemId, IReadOnlySet<uint> questItems, Sources sources)
    {
        foreach (var other in questItems)
        {
            if (other == itemId)
            {
                continue;
            }

            foreach (var recipe in sources.RecipesOf(other))
            {
                if (recipe.Ingredients.Contains(itemId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HandInItem Item(ItemFacts facts, Sources sources, byte amount, bool hq, uint[] categories)
    {
        var recipes = sources.RecipesOf(facts.ItemId);
        var refs = recipes.Count == 0 ? [] : recipes.Select(r => new HandInRecipe(r.RecipeId, r.CraftType, r.Yield)).ToArray();
        return new HandInItem
        {
            ItemId = facts.ItemId,
            Name = facts.Name,
            Icon = facts.Icon,
            Amount = amount,
            IsHq = hq,
            ClassJobCategories = categories,
            Recipes = refs,
            Gather = sources.GatherOf(facts.ItemId),
        };
    }
}
