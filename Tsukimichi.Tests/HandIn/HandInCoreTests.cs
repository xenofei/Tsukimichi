using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.HandIn;

/// <summary>
/// The hand-in pieces that need no game: the item → quests index behind "Needed for", "Copy missing items" (the
/// Teamcraft link and the text list), and the hand-off rules (Artisan, GatherBuddy, Allagan Tools).
/// </summary>
public class HandInCoreTests
{
    private const uint Lumber = 5361;
    private const uint Ingot = 5056;
    private const uint Fish = 4870;

    private static readonly JournalRef Listed = new(1, "Side", 2, "Class", 3, "Carpenter", 0);

    private static HandInItem Item(uint id, string name, byte amount = 0, HandInRecipe[]? recipes = null, GatherKind gather = GatherKind.None, bool hq = false) =>
        new() { ItemId = id, Name = name, Amount = amount, Recipes = recipes ?? [], Gather = gather, IsHq = hq };

    private static QuestRecord Quest(uint rowId, string name, params HandInItem[] items) =>
        new() { RowId = rowId, QuestId = QuestRecord.ToQuestId(rowId), Name = name, Journal = Listed, HandInItems = items };

    private static QuestEvaluation Evaluation(QuestState state) => new(state, [], null, null, null);

    // ---- HandInIndex ----

    [Fact]
    public void The_index_names_only_open_quests_and_reads_hq_ids()
    {
        var accepted = Quest(65741, "My First Saw", Item(Lumber, "Maple Lumber"));
        var ready = Quest(65742, "Second Saw", Item(Lumber, "Maple Lumber", 3));
        var otherJob = Quest(65743, "Crafter Elsewhere", Item(Lumber, "Maple Lumber"));
        var done = Quest(65744, "Done Already", Item(Lumber, "Maple Lumber"));
        var blocked = Quest(65745, "Not Yet", Item(Lumber, "Maple Lumber"));
        var removed = new QuestRecord { RowId = 65746, QuestId = QuestRecord.ToQuestId(65746), Name = "Gone", HandInItems = [Item(Lumber, "Maple Lumber")] };
        var index = new HandInIndex(QuestCatalog.Build([accepted, ready, otherJob, done, blocked, removed]));
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [accepted.RowId] = Evaluation(QuestState.Accepted),
            [ready.RowId] = Evaluation(QuestState.Ready),
            [otherJob.RowId] = Evaluation(QuestState.ReadyOnOtherJob),
            [done.RowId] = Evaluation(QuestState.Completed),
            [blocked.RowId] = Evaluation(QuestState.Blocked),
            [removed.RowId] = Evaluation(QuestState.Ready),
        };

        Assert.Equal(5, index.QuestsFor(Lumber).Count);
        Assert.Equal([65741u, 65742u, 65743u], index.NeededFor(Lumber, states).Select(q => q.RowId));
        Assert.Equal(3, index.NeededFor(Lumber + 1_000_000, states).Count); // an HQ stack
        Assert.Empty(index.NeededFor(Ingot, states));
        Assert.Empty(index.NeededFor(Lumber, new Dictionary<uint, QuestEvaluation>()));
        Assert.Empty(HandInIndex.Empty.QuestsFor(Lumber));
    }

    [Fact]
    public void The_index_source_rebuilds_only_for_a_new_catalog()
    {
        QuestCatalog? catalog = QuestCatalog.Build([Quest(65741, "My First Saw", Item(Lumber, "Maple Lumber"))]);
        var source = new HandInIndexSource(() => catalog);
        var first = source.Current;
        Assert.Same(first, source.Current);
        Assert.Equal(1, first.ItemCount);
        catalog = null;
        Assert.Null(source.Current.Catalog);
        Assert.Equal(0, source.Current.ItemCount);
    }

    // ---- MissingItems ----

    [Fact]
    public void Missing_items_sum_over_quests_and_subtract_what_is_held()
    {
        var a = Quest(1, "A", Item(Lumber, "Maple Lumber", 3), Item(Ingot, "Bronze Ingot"));
        var b = Quest(2, "B", Item(Lumber, "Maple Lumber", 2), Item(Fish, "Lominsan Anchovy"));
        var held = new Dictionary<uint, int> { [Lumber] = 1, [Fish] = 5 };

        var missing = MissingItems.For([a, b], (id, _) => held.TryGetValue(id, out var n) ? n : null);

        Assert.Equal([new MissingItem(Lumber, "Maple Lumber", 4), new MissingItem(Ingot, "Bronze Ingot", 1)], missing);
        Assert.Empty(MissingItems.For([Quest(3, "C", Item(Fish, "Lominsan Anchovy"))], (_, _) => 1));
    }

    [Fact]
    public void Missing_items_count_only_hq_against_an_hq_hand_in()
    {
        // 3 HQ Maple Lumber for one quest, 2 of any quality for another: two entries, each against its own count.
        var a = Quest(1, "A", Item(Lumber, "Maple Lumber", 3, hq: true));
        var b = Quest(2, "B", Item(Lumber, "Maple Lumber", 2));
        var stock = new HandInCount(Held: 5, HeldHq: 1, Retainers: 10);

        var missing = MissingItems.For([a, b], (_, hq) => stock.Usable(hq));

        Assert.Equal([new MissingItem(Lumber, "Maple Lumber", 2, IsHq: true)], missing);
    }

    // ---- HandInCount ----

    [Fact]
    public void An_hq_hand_in_counts_only_the_games_hq_and_never_allagan_tools_mixed_counts()
    {
        var hq = Item(Lumber, "Maple Lumber", 3, hq: true);
        var nq = Item(Lumber, "Maple Lumber", 3);

        // The game says 1 HQ of 2 held; the retainers hold 9, NQ and HQ unknown.
        var game = new HandInCount(Held: 2, HeldHq: 1, Retainers: 9);
        Assert.Equal(11, game.Total);
        Assert.Equal(1, game.UsableFor(hq));
        Assert.Equal(11, game.UsableFor(nq));
        Assert.False(game.IsEnough(hq));
        Assert.True(game.IsEnough(nq));
        Assert.True(game.HasMixed);

        // Enough HQ in the bags: enough, whatever the retainers hold.
        Assert.True(new HandInCount(3, 3, null).IsEnough(hq));
        Assert.False(new HandInCount(3, 3, null).HasMixed);

        // The game read paused: Allagan Tools' character count stands in, without an HQ part.
        var paused = new HandInCount(Held: 50, HeldHq: null, Retainers: 50);
        Assert.Null(paused.UsableFor(hq));
        Assert.False(paused.IsEnough(hq));
        Assert.True(paused.IsEnough(nq));
        Assert.True(paused.HasMixed);

        Assert.False(default(HandInCount).Known);
        Assert.Null(default(HandInCount).Total);
        Assert.True(new HandInCount(null, null, 0).Known);
    }

    [Fact]
    public void The_teamcraft_link_encodes_item_null_quantity_rows_without_a_trailing_separator()
    {
        var link = MissingItems.TeamcraftLink([new MissingItem(1823, "Feathered Harpoon", 1), new MissingItem(1895, "Ash Shortbow", 2), new MissingItem(0, "None", 1)]);

        Assert.NotNull(link);
        Assert.StartsWith(MissingItems.TeamcraftImportBase, link);
        var segment = link[MissingItems.TeamcraftImportBase.Length..];
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(Uri.UnescapeDataString(segment)));
        Assert.Equal("1823,null,1;1895,null,2", decoded);
        Assert.Null(MissingItems.TeamcraftLink([]));
    }

    [Fact]
    public void The_teamcraft_link_escapes_base64_characters_a_path_cannot_hold()
    {
        // Teamcraft URI-decodes the segment before base64-decoding it, so '/', '+' and '=' must travel escaped; this
        // payload's base64 ends in padding.
        var items = new[] { new MissingItem(16383, "x", 63), new MissingItem(4095, "y", 1) };
        var link = MissingItems.TeamcraftLink(items)!;
        var segment = link[MissingItems.TeamcraftImportBase.Length..];
        Assert.DoesNotContain('/', segment);
        Assert.DoesNotContain('+', segment);
        Assert.DoesNotContain('=', segment);
        Assert.Equal("16383,null,63;4095,null,1", Encoding.UTF8.GetString(Convert.FromBase64String(Uri.UnescapeDataString(segment))));
    }

    [Fact]
    public void The_text_list_is_one_quantity_x_name_line_per_item()
    {
        var text = MissingItems.TextList([new MissingItem(Lumber, "Maple Lumber", 3), new MissingItem(Ingot, "Bronze Ingot", 1)]);
        Assert.Equal("3x Maple Lumber\n1x Bronze Ingot", text);
        Assert.Equal(string.Empty, MissingItems.TextList([]));
    }

    // ---- HandInActions ----

    private static readonly HandInItem Craftable = Item(Lumber, "Maple Lumber", 3, [new HandInRecipe(100, 0), new HandInRecipe(101, 1), new HandInRecipe(107, 7)]);
    private static readonly HandInItem Gatherable = Item(4839, "Laurel", gather: GatherKind.Gathered);
    private static readonly HandInItem Fishable = Item(Fish, "Lominsan Anchovy", gather: GatherKind.Fish);

    [Fact]
    public void The_craft_button_needs_a_recipe_then_artisan_then_an_idle_artisan()
    {
        Assert.Equal(HandOffState.NotApplicable, HandInActions.Craft(Gatherable, artisanLoaded: true, artisanBusy: false));
        Assert.Equal(HandOffState.NotApplicable, HandInActions.Craft(Gatherable, artisanLoaded: false, artisanBusy: false));
        Assert.Equal(HandOffState.PluginMissing, HandInActions.Craft(Craftable, artisanLoaded: false, artisanBusy: false));
        Assert.Equal(HandOffState.Busy, HandInActions.Craft(Craftable, artisanLoaded: true, artisanBusy: true));
        Assert.Equal(HandOffState.Ready, HandInActions.Craft(Craftable, artisanLoaded: true, artisanBusy: false));

        // Artisan's gate takes a ushort recipe id: a recipe beyond it is not offered.
        var tooBig = Item(1, "Big", recipes: [new HandInRecipe(70000, 0)]);
        Assert.Equal(HandOffState.NotApplicable, HandInActions.Craft(tooBig, true, false));
    }

    [Fact]
    public void The_gather_button_takes_either_gatherbuddy()
    {
        Assert.Equal(HandOffState.NotApplicable, HandInActions.Gather(Craftable, GatherPlugin.GatherBuddy));
        Assert.Equal(HandOffState.PluginMissing, HandInActions.Gather(Gatherable, GatherPlugin.None));
        Assert.Equal(HandOffState.Ready, HandInActions.Gather(Gatherable, GatherPlugin.GatherBuddy));
        Assert.Equal(HandOffState.Ready, HandInActions.Gather(Fishable, GatherPlugin.GatherBuddyReborn));

        Assert.Equal(GatherPlugin.None, HandInActions.GatherPluginOf(false, false));
        Assert.Equal(GatherPlugin.GatherBuddy, HandInActions.GatherPluginOf(true, false));
        Assert.Equal(GatherPlugin.GatherBuddyReborn, HandInActions.GatherPluginOf(false, true));
        Assert.Equal(GatherPlugin.GatherBuddyReborn, HandInActions.GatherPluginOf(true, true));

        Assert.Equal("/gather Laurel", HandInActions.GatherCommandFor(Gatherable));
        Assert.Equal("/gatherfish Lominsan Anchovy", HandInActions.GatherCommandFor(Fishable));
        Assert.Null(HandInActions.GatherCommandFor(Craftable));
    }

    [Fact]
    public void The_recipe_follows_the_current_crafter_then_the_best_levelled_then_the_first()
    {
        // On Blacksmith (ClassJob 9 = CraftType 1).
        Assert.Equal(101u, HandInActions.ChooseRecipe(Craftable, 9, null)!.RecipeId);

        // On a combat job: the highest crafter with a recipe (Culinarian 50 over Carpenter 20; Alchemist has none).
        var levels = new Dictionary<byte, short> { [8] = 20, [15] = 50, [14] = 90 };
        Assert.Equal(107u, HandInActions.ChooseRecipe(Craftable, 19, levels)!.RecipeId);

        // No levels known: the first recipe.
        Assert.Equal(100u, HandInActions.ChooseRecipe(Craftable, 19, new Dictionary<byte, short>())!.RecipeId);
        Assert.Null(HandInActions.ChooseRecipe(Gatherable, 8, null));

        Assert.Equal((byte)8, HandInActions.CrafterJob(0));
        Assert.Equal((byte)15, HandInActions.CrafterJob(7));
        Assert.Equal((byte)0, HandInActions.CrafterJob(8));
    }

    [Fact]
    public void The_craft_amount_is_the_crafts_for_what_is_still_needed()
    {
        var once = new HandInRecipe(100, 0);
        Assert.Equal(3, HandInActions.CraftAmount(Craftable, once, null));
        Assert.Equal(2, HandInActions.CraftAmount(Craftable, once, 1));
        Assert.Equal(0, HandInActions.CraftAmount(Craftable, once, 3));
        Assert.Equal(0, HandInActions.CraftAmount(Craftable, once, 9));
        Assert.Equal(1, HandInActions.CraftAmount(Gatherable, null, 0)); // no amount in the data: one

        // Artisan's amount counts crafts: a recipe that makes 3 is asked once for 3 items, twice for 4.
        var triple = new HandInRecipe(100, 0, Yield: 3);
        Assert.Equal(1, HandInActions.CraftAmount(Craftable, triple, null));
        Assert.Equal(1, HandInActions.CraftAmount(Craftable, triple, 2));
        Assert.Equal(2, HandInActions.CraftAmount(Item(Lumber, "Maple Lumber", 4), triple, 0));
        Assert.Equal(0, HandInActions.CraftAmount(Craftable, triple, 3));

        // A yield of zero (data that does not say) reads as one.
        Assert.Equal(3, HandInActions.CraftAmount(Craftable, new HandInRecipe(100, 0, Yield: 0), null));
        Assert.Equal(2, HandInActions.MissingCount(Craftable, 1));
        Assert.Equal(0, HandInActions.MissingCount(Craftable, 7));
    }

    [Fact]
    public void The_craft_button_is_off_when_the_character_holds_enough()
    {
        Assert.Equal(HandOffState.Enough, HandInActions.Craft(Craftable, artisanLoaded: true, artisanBusy: false, enough: true));
        Assert.Equal(HandOffState.Enough, HandInActions.Craft(Craftable, artisanLoaded: false, artisanBusy: false, enough: true));
        Assert.Equal(HandOffState.NotApplicable, HandInActions.Craft(Gatherable, artisanLoaded: true, artisanBusy: false, enough: true));
    }

    [Fact]
    public void Allagan_tools_counts_make_gear_owned_or_leave_it_unknown()
    {
        Assert.True(HandInActions.OwnedFromCount(1));
        Assert.Null(HandInActions.OwnedFromCount(0));
        Assert.Null(HandInActions.OwnedFromCount(null));
    }
}
