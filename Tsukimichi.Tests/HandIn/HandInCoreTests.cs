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

    private static HandInItem Item(uint id, string name, byte amount = 0, HandInRecipe[]? recipes = null, GatherKind gather = GatherKind.None) =>
        new() { ItemId = id, Name = name, Amount = amount, Recipes = recipes ?? [], Gather = gather };

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

        var missing = MissingItems.For([a, b], id => held.TryGetValue(id, out var n) ? n : null);

        Assert.Equal([new MissingItem(Lumber, "Maple Lumber", 4), new MissingItem(Ingot, "Bronze Ingot", 1)], missing);
        Assert.Empty(MissingItems.For([Quest(3, "C", Item(Fish, "Lominsan Anchovy"))], _ => 1));
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
    public void The_craft_amount_is_what_is_still_needed_and_at_least_one()
    {
        Assert.Equal(3, HandInActions.CraftAmount(Craftable, null));
        Assert.Equal(2, HandInActions.CraftAmount(Craftable, 1));
        Assert.Equal(1, HandInActions.CraftAmount(Craftable, 3));
        Assert.Equal(1, HandInActions.CraftAmount(Craftable, 9));
        Assert.Equal(1, HandInActions.CraftAmount(Gatherable, 0)); // no amount in the data: one
    }

    [Fact]
    public void Allagan_tools_counts_make_gear_owned_or_leave_it_unknown()
    {
        Assert.True(HandInActions.OwnedFromCount(1));
        Assert.Null(HandInActions.OwnedFromCount(0));
        Assert.Null(HandInActions.OwnedFromCount(null));
    }
}
