using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Tsukimichi.Core.Model;
using Action = Lumina.Excel.Sheets.Action;

namespace Tsukimichi.DataGen;

/// <summary>Builds the unique-reward entries from static sheets (spec section 8, steps 1-4).</summary>
internal sealed class UniqueRewardGenerator
{
    // ItemAction type ids verified in docs/feasibility-report.md section 4. In Lumina.Excel 7.5.0 the
    // "Type" column is exposed as ItemAction.Action (RowRef<Action>), so the id lives in Action.RowId.
    private const uint ActionMount = 1322;
    private const uint ActionMinion = 853;
    private const uint ActionOrchestrion = 25183;
    private const uint ActionTripleTriad = 3357;
    private const uint ActionOrnament = 20086;
    private const uint ActionBarding = 1013;
    private const uint ActionUnlockLink = 2633; // emotes, hairstyles and other unlock links
    private const uint ActionGlasses = 37312;

    private const byte ItemRewardTypeArtifactGear = 6;
    private const byte ItemRewardTypeBeastRankBonus = 7;

    /// <summary>ItemAction types that unlock a collectible without a RewardKind of their own: portrait framer's kits and Bozja field notes.</summary>
    internal static readonly HashSet<uint> UnlockItemActions = [29459, 19743];
    private const byte UnlockTypeQuest = 1;

    private readonly GameSheets g;
    private readonly Dictionary<(uint Quest, RewardKind Kind, uint RewardId), UniqueRewardEntry> entries = new();

    private readonly Dictionary<ushort, uint> emoteByUnlockLink = new();
    private readonly Dictionary<ushort, uint> hairstyleByUnlockLink = new();
    private readonly Dictionary<uint, string> aetherCurrentZone = new();
    private readonly HashSet<uint> gilShopItems = new();
    private readonly HashSet<uint> vendorItems = new(); // gil shop items sold through a menu that is not a quest-reward reacquisition menu
    private readonly HashSet<uint> specialShopItems = new();
    private readonly HashSet<uint> recipeResults = new();
    private readonly HashSet<uint> gatheringItems = new();
    private readonly HashSet<uint> achievementItems = new();

    /// <summary>Quests that hand out at least one reward signal, for the "unclassified" section of the report.</summary>
    public Dictionary<uint, List<string>> RewardSignals { get; } = new();

    /// <summary>Plain item rewards the exclusivity rule (or the unnamed-item rule) refused, for the review report.</summary>
    public List<DroppedItem> Dropped { get; } = new();

    /// <summary>Achievements that name quests but no single quest earns (relic weapons, all-of-N sets), for the review report.</summary>
    public List<SkippedAchievement> SkippedAchievements { get; } = new();

    /// <summary>
    /// When true (the default), a plain untradable item is only shipped as a unique Item/OptionalItem when nothing else hands
    /// it out (no special shop, recipe or gathering node; no gil shop other than a Calamity Salvager) and it is not in a
    /// consumable-like ItemUICategory (see <see cref="NonExclusiveCategories"/>). Collectible kinds are never affected.
    /// </summary>
    public bool StrictItemExclusivity { get; set; } = true;

    /// <summary>
    /// ItemUICategory names (English) whose items are consumed, spent or re-sold and therefore never a quest-only
    /// collectible: potions, food, crafting materials, currencies, tickets, vouchers, Fantasia, coffers.
    /// </summary>
    internal static readonly HashSet<string> NonExclusiveCategories = new(StringComparer.Ordinal)
    {
        "Medicine", "Meal", "Ingredient", "Reagent", "Dye", "Crystal", "Catalyst", "Currency", "Other", "Miscellany",
        "Seasonal Miscellany", "Materia", "Demimateria", "Part", "Lumber", "Stone", "Metal", "Cloth", "Leather", "Bone",
        "Gardening",
    };

    /// <summary>
    /// Gil shop menus through which a Calamity Salvager re-sells quest rewards to characters that already completed the
    /// quest ("Purchase Quest Rewards I/II", "Purchase Lv. 50 Arms &amp; Tools", "Purchase Blue Mage Arms &amp; Gear", ...).
    /// Being listed there does not make an item obtainable by anyone else. Every other gil shop menu is a real vendor.
    /// </summary>
    internal static bool IsReacquisitionMenu(string shopName)
        => shopName.StartsWith("Purchase Quest Rewards", StringComparison.Ordinal)
           || shopName.EndsWith("Arms & Gear", StringComparison.Ordinal)
           || shopName.EndsWith("Arms & Tools", StringComparison.Ordinal);

    public UniqueRewardGenerator(GameSheets sheets)
    {
        g = sheets;
    }

    public IReadOnlyCollection<UniqueRewardEntry> Entries => entries.Values;

    public void Run()
    {
        BuildIndexes();
        ScanQuestRewards();
        ScanReverseLinks();
    }

    /// <summary>Applies a curated entry; curated always wins over a static entry with the same key.</summary>
    public void AddCurated(UniqueRewardEntry entry)
    {
        entries[(entry.QuestRowId, entry.Kind, entry.RewardId)] = entry;
    }

    /// <summary>
    /// Adds <paramref name="name"/> to <see cref="UniqueRewardEntry.OtherSources"/> of every entry delivered as item
    /// <paramref name="itemId"/> or granting the collectible (<paramref name="kind"/>, <paramref name="rewardId"/>);
    /// the second match catches rewards a quest grants directly, such as an emote with no item. Returns how many
    /// entries were marked.
    /// </summary>
    public int MarkOtherSource(uint itemId, RewardKind kind, uint rewardId, string name) => MarkOtherSource(itemId, kind, rewardId, name, null);

    /// <summary>
    /// <see cref="MarkOtherSource(uint, RewardKind, uint, string)"/> that also stores <paramref name="note"/> (when not
    /// blank) under <paramref name="name"/> in <see cref="UniqueRewardEntry.OtherSourceNotes"/>, such as the duties a
    /// dungeon drop comes from. Pass <paramref name="rewardId"/> 0 to match by item only.
    /// </summary>
    public int MarkOtherSource(uint itemId, RewardKind kind, uint rewardId, string name, string? note)
    {
        var marked = 0;
        foreach (var key in entries.Keys.ToList())
        {
            var entry = entries[key];
            var byItem = itemId != 0 && entry.ItemId == itemId;
            var byReward = rewardId != 0 && entry.Kind == kind && entry.RewardId == rewardId;
            if (!byItem && !byReward)
                continue;
            entries[key] = entry.WithOtherSource(name, note);
            marked++;
        }
        return marked;
    }

    /// <summary>
    /// The collectible an item unlocks through its ItemAction, with the same rules <see cref="ClassifyItem"/> applies:
    /// mount, minion, orchestrion roll, Triple Triad card, ornament, barding, emote or hairstyle. False for anything else.
    /// </summary>
    public bool TryResolveCollectible(Item item, out RewardKind kind, out uint rewardId)
    {
        kind = default;
        rewardId = 0;
        var action = item.ItemAction.RowId == 0 ? null : g.ItemActions.GetRowOrDefault(item.ItemAction.RowId);
        var type = action?.Action.RowId ?? 0;
        var data0 = action is { } a && a.Data.Count > 0 ? a.Data[0] : (ushort)0;
        switch (type)
        {
            case ActionMount:
                (kind, rewardId) = (RewardKind.Mount, data0);
                break;
            case ActionMinion:
                (kind, rewardId) = (RewardKind.Minion, data0);
                break;
            case ActionOrchestrion:
                (kind, rewardId) = (RewardKind.Orchestrion, item.AdditionalData.Is<Orchestrion>() ? item.AdditionalData.RowId : 0u);
                break;
            case ActionTripleTriad:
                (kind, rewardId) = (RewardKind.TripleTriadCard, data0);
                break;
            case ActionOrnament:
                (kind, rewardId) = (RewardKind.Ornament, data0);
                break;
            case ActionBarding:
                (kind, rewardId) = (RewardKind.Barding, data0);
                break;
            case ActionUnlockLink when emoteByUnlockLink.TryGetValue(data0, out var emoteId):
                (kind, rewardId) = (RewardKind.Emote, emoteId);
                break;
            case ActionUnlockLink when hairstyleByUnlockLink.ContainsKey(data0):
                (kind, rewardId) = (RewardKind.Hairstyle, data0);
                break;
        }
        return rewardId != 0;
    }

    // ----------------------------------------------------------------------------------------------------------
    // Indexes

    private void BuildIndexes()
    {
        foreach (var emote in g.Emotes)
        {
            if (emote.UnlockLink != 0 && emote.UnlockLink <= ushort.MaxValue)
                emoteByUnlockLink.TryAdd((ushort)emote.UnlockLink, emote.RowId);
        }

        foreach (var cmc in g.CharaMakeCustomizes)
        {
            if (cmc.UnlockLink != 0)
                hairstyleByUnlockLink.TryAdd(cmc.UnlockLink, cmc.RowId);
        }

        foreach (var set in g.AetherCurrentSets)
        {
            var zone = set.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText().Trim();
            if (string.IsNullOrEmpty(zone))
                continue;
            foreach (var current in set.AetherCurrents)
            {
                if (current.RowId != 0)
                    aetherCurrentZone.TryAdd(current.RowId, zone);
            }
        }

        foreach (var row in g.GilShopItems.Flatten())
        {
            if (row.Item.RowId == 0)
                continue;
            gilShopItems.Add(row.Item.RowId);
            if (!IsReacquisitionMenu(Text(g.GilShops.GetRowOrDefault(row.RowId)?.Name)))
                vendorItems.Add(row.Item.RowId);
        }

        foreach (var shop in g.SpecialShops)
        {
            foreach (var slot in shop.Item)
            {
                foreach (var receive in slot.ReceiveItems)
                {
                    if (receive.Item.RowId != 0)
                        specialShopItems.Add(receive.Item.RowId);
                }
            }
        }

        foreach (var recipe in g.Recipes)
        {
            if (recipe.ItemResult.RowId != 0)
                recipeResults.Add(recipe.ItemResult.RowId);
        }

        foreach (var gi in g.GatheringItems)
        {
            if (gi.Item.RowId != 0)
                gatheringItems.Add(gi.Item.RowId);
        }

        foreach (var ach in g.Achievements)
        {
            if (ach.Item.RowId != 0)
                achievementItems.Add(ach.Item.RowId);
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // Forward scan: Quest sheet reward slots

    private void ScanQuestRewards()
    {
        foreach (var quest in g.Quests)
        {
            if (quest.RowId == 0)
                continue;

            switch (quest.ItemRewardType)
            {
                case ItemRewardTypeArtifactGear:
                    ScanClassJobRewards(quest, RewardKind.ArtifactGear, "Quest.Reward;QuestClassJobReward;ItemRewardType=6");
                    break;
                case ItemRewardTypeBeastRankBonus:
                    // Reward[0] is a BeastRankBonus row (tribal quests): one item whose quantity scales with reputation rank.
                    if (quest.Reward[0].RowId != 0 && quest.Reward[0].GetValueOrDefault<BeastRankBonus>() is { } bonus
                        && bonus.Item.RowId != 0 && bonus.Item.ValueNullable is { } bonusItem)
                    {
                        NoteSignal(quest.RowId, $"BeastRankBonus item {DescribeItem(bonusItem)}");
                        ClassifyItem(quest.RowId, bonusItem, "Quest.Reward;BeastRankBonus;ItemRewardType=7");
                    }
                    break;
                default:
                    foreach (var reward in quest.Reward)
                    {
                        if (reward.RowId == 0 || !reward.Is<Item>())
                            continue;
                        var item = reward.GetValueOrDefault<Item>();
                        if (item is null)
                            continue;
                        NoteSignal(quest.RowId, $"Reward item {DescribeItem(item.Value)}");
                        ClassifyItem(quest.RowId, item.Value, "Quest.Reward");
                    }
                    break;
            }

            foreach (var optional in quest.OptionalItemReward)
            {
                var item = optional.ValueNullable;
                if (optional.RowId == 0 || item is null)
                    continue;
                NoteSignal(quest.RowId, $"Optional item {DescribeItem(item.Value)}");
                ClassifyItem(quest.RowId, item.Value, "Quest.OptionalItemReward", optionalSlot: true);
            }

            if (quest.EmoteReward.RowId != 0)
            {
                NoteSignal(quest.RowId, $"EmoteReward {quest.EmoteReward.RowId}");
                Add(quest.RowId, RewardKind.Emote, quest.EmoteReward.RowId, 0,
                    Text(quest.EmoteReward.ValueNullable?.Name), "Quest.EmoteReward");
            }

            if (quest.ActionReward.RowId != 0)
            {
                NoteSignal(quest.RowId, $"ActionReward {quest.ActionReward.RowId}");
                Add(quest.RowId, RewardKind.Action, quest.ActionReward.RowId, 0,
                    Text(quest.ActionReward.ValueNullable?.Name), "Quest.ActionReward");
            }

            foreach (var ga in quest.GeneralActionReward)
            {
                if (ga.RowId == 0)
                    continue;
                NoteSignal(quest.RowId, $"GeneralActionReward {ga.RowId}");
                Add(quest.RowId, RewardKind.GeneralAction, ga.RowId, 0, Text(ga.ValueNullable?.Name), "Quest.GeneralActionReward");
            }

            if (quest.InstanceContentUnlock.RowId != 0)
            {
                NoteSignal(quest.RowId, $"InstanceContentUnlock {quest.InstanceContentUnlock.RowId}");
                AddInstanceUnlock(quest.RowId, quest.InstanceContentUnlock.RowId, "Quest.InstanceContentUnlock");
            }

            if (quest.ClassJobUnlock.RowId != 0)
            {
                NoteSignal(quest.RowId, $"ClassJobUnlock {quest.ClassJobUnlock.RowId}");
                Add(quest.RowId, RewardKind.ClassJob, quest.ClassJobUnlock.RowId, 0,
                    Text(quest.ClassJobUnlock.ValueNullable?.Name), "Quest.ClassJobUnlock");
            }

            if (quest.OtherReward.RowId != 0)
            {
                NoteSignal(quest.RowId, $"OtherReward {quest.OtherReward.RowId}");
                Add(quest.RowId, RewardKind.Other, quest.OtherReward.RowId, 0,
                    Text(quest.OtherReward.ValueNullable?.Name), "Quest.OtherReward");
            }
        }
    }

    /// <summary>ItemRewardType 6: Reward[0] points at a QuestClassJobReward row whose subrows list items per job category.</summary>
    private void ScanClassJobRewards(Quest quest, RewardKind kind, string source)
    {
        var rowId = quest.Reward[0].RowId;
        if (rowId == 0)
            return;
        var subrows = g.QuestClassJobRewards.GetRowOrDefault(rowId);
        if (subrows is null)
            return;

        foreach (var sub in subrows.Value)
        {
            foreach (var reward in sub.RewardItem)
            {
                var item = reward.ValueNullable;
                if (reward.RowId == 0 || item is null)
                    continue;
                NoteSignal(quest.RowId, $"ClassJob reward item {DescribeItem(item.Value)}");
                if (Text(item.Value.Name).Length == 0)
                {
                    Dropped.Add(new DroppedItem(quest.RowId, item.Value.RowId, string.Empty, "unnamed item row"));
                    continue;
                }
                var others = OtherSourcesOf(item.Value);
                Add(quest.RowId, kind, item.Value.RowId, item.Value.RowId, Text(item.Value.Name), source + Suffix(others), others);
            }
        }
    }

    /// <summary>Resolves an item reward to a collectible kind via ItemAction, or to a plain untradable Item.</summary>
    private void ClassifyItem(uint questRowId, Item item, string source, bool optionalSlot = false)
    {
        var action = item.ItemAction.RowId == 0 ? null : g.ItemActions.GetRowOrDefault(item.ItemAction.RowId);
        var type = action?.Action.RowId ?? 0;
        var data0 = action is { } a && a.Data.Count > 0 ? a.Data[0] : (ushort)0;
        var itemName = Text(item.Name);
        var others = OtherSourcesOf(item);
        var exclusivity = Suffix(others);
        if (itemName.Length == 0)
        {
            Dropped.Add(new DroppedItem(questRowId, item.RowId, string.Empty, "unnamed item row"));
            return;
        }

        switch (type)
        {
            case ActionMount:
                Add(questRowId, RewardKind.Mount, data0, item.RowId, NameOr(g.Mounts.GetRowOrDefault(data0)?.Singular, itemName), $"{source};ItemAction={type}{exclusivity}", others);
                return;
            case ActionMinion:
                Add(questRowId, RewardKind.Minion, data0, item.RowId, NameOr(g.Companions.GetRowOrDefault(data0)?.Singular, itemName), $"{source};ItemAction={type}{exclusivity}", others);
                return;
            case ActionOrchestrion:
            {
                // Orchestrion rolls keep ItemAction.Data empty; the Orchestrion row is linked from Item.AdditionalData.
                var orchestrionId = item.AdditionalData.Is<Orchestrion>() ? item.AdditionalData.RowId : 0u;
                if (orchestrionId == 0)
                {
                    Dropped.Add(new DroppedItem(questRowId, item.RowId, itemName, "orchestrion roll without an Orchestrion link"));
                    return;
                }
                Add(questRowId, RewardKind.Orchestrion, orchestrionId, item.RowId, NameOr(g.Orchestrions.GetRowOrDefault(orchestrionId)?.Name, itemName), $"{source};ItemAction={type};AdditionalData={orchestrionId}{exclusivity}", others);
                return;
            }
            case ActionTripleTriad:
                Add(questRowId, RewardKind.TripleTriadCard, data0, item.RowId, NameOr(g.TripleTriadCards.GetRowOrDefault(data0)?.Name, itemName), $"{source};ItemAction={type}{exclusivity}", others);
                return;
            case ActionOrnament:
                Add(questRowId, RewardKind.Ornament, data0, item.RowId, NameOr(g.Ornaments.GetRowOrDefault(data0)?.Singular, itemName), $"{source};ItemAction={type}{exclusivity}", others);
                return;
            case ActionBarding:
                Add(questRowId, RewardKind.Barding, data0, item.RowId, NameOr(g.BuddyEquips.GetRowOrDefault(data0)?.Name, itemName), $"{source};ItemAction={type}{exclusivity}", others);
                return;
            case ActionUnlockLink:
                if (emoteByUnlockLink.TryGetValue(data0, out var emoteId))
                {
                    Add(questRowId, RewardKind.Emote, emoteId, item.RowId, NameOr(g.Emotes.GetRowOrDefault(emoteId)?.Name, itemName), $"{source};ItemAction={type};unlockLink={data0}{exclusivity}", others);
                    return;
                }
                if (hairstyleByUnlockLink.ContainsKey(data0))
                {
                    // rewardId is the unlock link id: that is what UIState.IsUnlockLinkUnlocked checks at runtime.
                    Add(questRowId, RewardKind.Hairstyle, data0, item.RowId, itemName, $"{source};ItemAction={type};unlockLink={data0}{exclusivity}", others);
                    return;
                }
                // Neither an emote nor a hairstyle: fall through to the generic untradable-item rule with the link recorded.
                source = $"{source};ItemAction={type};unlockLink={data0}";
                break;
            case ActionGlasses:
                // Facewear has no RewardKind of its own in V1; it is shipped as an untradable Item.
                Add(questRowId, RewardKind.Item, item.RowId, item.RowId, itemName, $"{source};ItemAction={type};glasses{exclusivity}", others);
                return;
        }

        if (item.IsUntradable && item.ItemSearchCategory.RowId == 0)
        {
            if (StrictItemExclusivity && NonExclusiveReason(item, type) is { } reason)
            {
                Dropped.Add(new DroppedItem(questRowId, item.RowId, itemName, reason));
                return;
            }
            var kind = optionalSlot ? RewardKind.OptionalItem : RewardKind.Item;
            var actionTag = type == 0 || source.Contains("ItemAction=") ? string.Empty : $";ItemAction={type}";
            Add(questRowId, kind, item.RowId, item.RowId, itemName, $"{source}{actionTag};untradable{exclusivity}", others);
        }
    }

    /// <summary>Why a plain untradable item is not a quest-only collectible, or null when it looks quest-exclusive.</summary>
    private string? NonExclusiveReason(Item item, uint itemActionType)
    {
        if (UnlockItemActions.Contains(itemActionType))
            return null; // framer's kit, field notes: an unlock, whatever category the item sits in
        var category = Text(item.ItemUICategory.ValueNullable?.Name);
        if (NonExclusiveCategories.Contains(category))
            return $"ItemUICategory {category}";
        if (specialShopItems.Contains(item.RowId)) return "sold by a special shop";
        if (recipeResults.Contains(item.RowId)) return "crafted by a recipe";
        if (gatheringItems.Contains(item.RowId)) return "gathered";
        if (vendorItems.Contains(item.RowId)) return "sold by a gil shop that is not a quest-reward reacquisition menu";
        return null;
    }

    /// <summary>
    /// Every other place the item can come from, as <see cref="OtherSource"/> names; empty when the item looks
    /// quest-exclusive. Shipped structured on the entry (<see cref="UniqueRewardEntry.OtherSources"/>); the
    /// <c>;otherSource=</c> suffix in the source text is provenance only.
    /// </summary>
    private string[] OtherSourcesOf(Item item)
    {
        var other = new List<string>(4);
        if (!item.IsUntradable) other.Add(OtherSource.Tradable);
        if (item.ItemSearchCategory.RowId != 0) other.Add(OtherSource.Marketable);
        if (gilShopItems.Contains(item.RowId)) other.Add(OtherSource.GilShopItem);
        if (specialShopItems.Contains(item.RowId)) other.Add(OtherSource.SpecialShop);
        if (recipeResults.Contains(item.RowId)) other.Add(OtherSource.Recipe);
        if (gatheringItems.Contains(item.RowId)) other.Add(OtherSource.GatheringItem);
        if (achievementItems.Contains(item.RowId)) other.Add(OtherSource.Achievement);
        return other.Count == 0 ? [] : other.ToArray();
    }

    /// <summary>Source-text suffix for <see cref="OtherSourcesOf"/>; empty when there is nothing to name.</summary>
    private static string Suffix(string[] others) => others.Length == 0 ? string.Empty : ";otherSource=" + string.Join(",", others);

    private void AddInstanceUnlock(uint questRowId, uint instanceContentId, string source)
    {
        var instance = g.InstanceContents.GetRowOrDefault(instanceContentId);
        var cfc = instance?.ContentFinderCondition.ValueNullable;
        if (cfc is { RowId: not 0 } && Text(cfc.Value.Name).Length > 0)
        {
            Add(questRowId, RewardKind.DutyUnlock, cfc.Value.RowId, 0, Text(cfc.Value.Name), $"{source};InstanceContent={instanceContentId}");
        }
        else if (cfc is { RowId: not 0 })
        {
            // A ContentFinderCondition row without a name (row 121, reached from InstanceContent 40001 by the Grand Company
            // quests "A Pup No Longer") is a placeholder with nothing to show; it must not become an entry.
            NoteSignal(questRowId, $"InstanceContentUnlock {instanceContentId} -> unnamed ContentFinderCondition {cfc.Value.RowId}; skipped");
        }
        else
        {
            Add(questRowId, RewardKind.Instance, instanceContentId, 0, $"Instance content {instanceContentId}", source);
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // Reverse links: other sheets that point at a quest

    private void ScanReverseLinks()
    {
        foreach (var action in g.Actions)
        {
            if (action.UnlockLink.RowId == 0 || !action.UnlockLink.Is<Quest>())
                continue;
            Add(action.UnlockLink.RowId, RewardKind.Action, action.RowId, 0, Text(action.Name), "Action.UnlockLink");
        }

        foreach (var trait in g.Traits)
        {
            if (trait.Quest.RowId == 0)
                continue;
            Add(trait.Quest.RowId, RewardKind.Trait, trait.RowId, 0, Text(trait.Name), "Trait.Quest");
        }

        foreach (var job in g.ClassJobs)
        {
            if (job.UnlockQuest.RowId == 0)
                continue;
            Add(job.UnlockQuest.RowId, RewardKind.ClassJob, job.RowId, 0, Text(job.Name), "ClassJob.UnlockQuest");
        }

        // The awarding quest, not always the one AetherCurrent.Quest lists (five currents name the wrong quest); the
        // resolver is shared with the Flight view (Tsukimichi.GameData.AetherCurrentQuests).
        foreach (var current in g.AetherCurrents)
        {
            if (Tsukimichi.GameData.AetherCurrentQuests.Resolve(current, g.Quests) is not { } awarding)
                continue;
            var name = aetherCurrentZone.TryGetValue(current.RowId, out var zone) ? $"Aether Current ({zone})" : "Aether Current";
            var source = awarding.Source switch
            {
                Tsukimichi.GameData.AetherCurrentQuestSource.PreviousQuest => $"Quest.OtherReward (AetherCurrent.Quest lists {awarding.ListedQuestRowId})",
                Tsukimichi.GameData.AetherCurrentQuestSource.Override => $"AetherCurrentQuests.Overrides (AetherCurrent.Quest lists {awarding.ListedQuestRowId})",
                _ => "AetherCurrent.Quest",
            };
            Add(awarding.QuestRowId, RewardKind.AetherCurrent, current.RowId, 0, name, source);
        }

        foreach (var aoz in g.AozActionTransients)
        {
            if (aoz.RequiredForQuest.RowId == 0)
                continue;
            // AozActionTransient and AozAction share row ids (spell number); the name lives on the linked Action.
            var spell = g.AozActions.GetRowOrDefault(aoz.RowId)?.Action.ValueNullable;
            Add(aoz.RequiredForQuest.RowId, RewardKind.BlueMageSpell, aoz.RowId, 0, NameOr(spell?.Name, $"Blue mage spell #{aoz.Number}"), "AozActionTransient.RequiredForQuest");
        }

        // Only the quests that earn the achievement by themselves (Tsukimichi.GameData.AchievementQuests): relic weapon
        // achievements (type 24) need the job too, and an all-of-N set (type 6) goes to its last quest or nowhere.
        foreach (var ach in g.Achievements)
        {
            var listed = Tsukimichi.GameData.AchievementQuests.QuestsOf(ach);
            if (listed.Count == 0)
                continue;
            var credited = Tsukimichi.GameData.AchievementQuests.CreditedQuests(ach, g.Quests);
            if (credited.Count == 0)
            {
                SkippedAchievements.Add(new SkippedAchievement(ach.RowId, ach.Type, Text(ach.Name), listed.ToArray()));
                continue;
            }

            var title = ach.Title.RowId != 0 ? ach.Title.ValueNullable : null;
            foreach (var questId in credited)
            {
                var field = ach.Key.RowId == questId ? "Achievement.Key" : "Achievement.Data";
                if (title is { } t)
                    Add(questId, RewardKind.Title, t.RowId, 0, Text(t.Masculine), $"{field};achievement={ach.RowId};type={ach.Type}");
                else
                    Add(questId, RewardKind.Achievement, ach.RowId, 0, Text(ach.Name), $"{field};type={ach.Type}");
            }
        }

        foreach (var cfc in g.ContentFinderConditions)
        {
            if (cfc.UnlockType != UnlockTypeQuest || cfc.UnlockCriteria.RowId == 0 || !cfc.UnlockCriteria.Is<Quest>() || Text(cfc.Name).Length == 0)
                continue;
            Add(cfc.UnlockCriteria.RowId, RewardKind.DutyUnlock, cfc.RowId, 0, Text(cfc.Name), "ContentFinderCondition.UnlockCriteria");
        }

        DropDuplicateAetherCurrents();
    }

    /// <summary>
    /// A quest that awards an aether current carries <c>Quest.OtherReward</c> "Aether Current" as well; once the
    /// AetherCurrent entry sits on that quest, the Other entry is the same reward counted twice, so it goes.
    /// </summary>
    private void DropDuplicateAetherCurrents()
    {
        var currentQuests = entries.Values.Where(e => e.Kind == RewardKind.AetherCurrent).Select(e => e.QuestRowId).ToHashSet();
        foreach (var key in entries.Keys.ToList())
        {
            if (key.Kind == RewardKind.Other
                && key.RewardId == Tsukimichi.GameData.AetherCurrentQuests.OtherRewardAetherCurrent
                && currentQuests.Contains(key.Quest))
            {
                entries.Remove(key);
            }
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // Helpers

    private void Add(uint questRowId, RewardKind kind, uint rewardId, uint itemId, string name, string source, string[]? otherSources = null)
    {
        var key = (questRowId, kind, rewardId);
        if (entries.ContainsKey(key))
            return; // first writer wins; forward Quest fields run before reverse links
        entries[key] = new UniqueRewardEntry(questRowId, kind, rewardId, itemId, name, Confidence.Static, source) { OtherSources = otherSources ?? [] };
    }

    private void NoteSignal(uint questRowId, string signal)
    {
        if (!RewardSignals.TryGetValue(questRowId, out var list))
            RewardSignals[questRowId] = list = new List<string>();
        list.Add(signal);
    }

    private string DescribeItem(Item item)
    {
        var action = item.ItemAction.RowId == 0 ? null : g.ItemActions.GetRowOrDefault(item.ItemAction.RowId);
        var type = action?.Action.RowId ?? 0;
        var flags = new List<string>(3);
        if (type != 0) flags.Add($"ItemAction={type}");
        if (item.IsUntradable) flags.Add("untradable");
        if (item.ItemSearchCategory.RowId != 0) flags.Add("marketable");
        return $"{item.RowId} {Text(item.Name)}" + (flags.Count == 0 ? string.Empty : $" [{string.Join(", ", flags)}]");
    }

    /// <summary>Plain text of a sheet string with Square Enix private-use glyphs (U+E000-U+F8FF) removed and whitespace trimmed.</summary>
    internal static string Text(ReadOnlySeString? s)
    {
        if (s is null)
            return string.Empty;
        var raw = s.Value.ExtractText();
        if (!raw.Any(c => c >= '' && c <= ''))
            return raw.Trim();
        return new string(raw.Where(c => c < '' || c > '').ToArray()).Trim();
    }

    private static string NameOr(ReadOnlySeString? s, string fallback)
    {
        var text = Text(s);
        return text.Length == 0 ? fallback : text;
    }
}

/// <summary>A plain item reward the generator refused, with the rule that refused it.</summary>
internal sealed record DroppedItem(uint QuestRowId, uint ItemId, string Name, string Reason);

/// <summary>An achievement that names quests but is credited to none of them, with its type and the quests it lists.</summary>
internal sealed record SkippedAchievement(uint AchievementId, byte Type, string Name, uint[] Quests);
