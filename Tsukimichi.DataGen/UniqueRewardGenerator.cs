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
    private const byte ItemRewardTypeClassJobItems = 7;
    private const byte UnlockTypeQuest = 1;

    private readonly GameSheets g;
    private readonly Dictionary<(uint Quest, RewardKind Kind, uint RewardId), UniqueRewardEntry> entries = new();

    private readonly Dictionary<ushort, uint> emoteByUnlockLink = new();
    private readonly Dictionary<ushort, uint> hairstyleByUnlockLink = new();
    private readonly Dictionary<uint, string> aetherCurrentZone = new();
    private readonly HashSet<uint> gilShopItems = new();
    private readonly HashSet<uint> specialShopItems = new();
    private readonly HashSet<uint> recipeResults = new();
    private readonly HashSet<uint> gatheringItems = new();
    private readonly HashSet<uint> achievementItems = new();

    /// <summary>Quests that hand out at least one reward signal, for the "unclassified" section of the report.</summary>
    public Dictionary<uint, List<string>> RewardSignals { get; } = new();

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
            if (row.Item.RowId != 0)
                gilShopItems.Add(row.Item.RowId);
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
                case ItemRewardTypeClassJobItems:
                    ScanClassJobRewards(quest, null, "Quest.Reward;QuestClassJobReward;ItemRewardType=7");
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

    /// <summary>ItemRewardType 6/7: Reward[0] points at a QuestClassJobReward row whose subrows list items per job category.</summary>
    private void ScanClassJobRewards(Quest quest, RewardKind? forcedKind, string source)
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
                if (forcedKind is { } kind)
                    Add(quest.RowId, kind, item.Value.RowId, item.Value.RowId, Text(item.Value.Name), source + Exclusivity(item.Value));
                else
                    ClassifyItem(quest.RowId, item.Value, source);
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
        var exclusivity = Exclusivity(item);

        switch (type)
        {
            case ActionMount:
                Add(questRowId, RewardKind.Mount, data0, item.RowId, NameOr(g.Mounts.GetRowOrDefault(data0)?.Singular, itemName), $"{source};ItemAction={type}{exclusivity}");
                return;
            case ActionMinion:
                Add(questRowId, RewardKind.Minion, data0, item.RowId, NameOr(g.Companions.GetRowOrDefault(data0)?.Singular, itemName), $"{source};ItemAction={type}{exclusivity}");
                return;
            case ActionOrchestrion:
                Add(questRowId, RewardKind.Orchestrion, data0, item.RowId, NameOr(g.Orchestrions.GetRowOrDefault(data0)?.Name, itemName), $"{source};ItemAction={type}{exclusivity}");
                return;
            case ActionTripleTriad:
                Add(questRowId, RewardKind.TripleTriadCard, data0, item.RowId, NameOr(g.TripleTriadCards.GetRowOrDefault(data0)?.Name, itemName), $"{source};ItemAction={type}{exclusivity}");
                return;
            case ActionOrnament:
                Add(questRowId, RewardKind.Ornament, data0, item.RowId, NameOr(g.Ornaments.GetRowOrDefault(data0)?.Singular, itemName), $"{source};ItemAction={type}{exclusivity}");
                return;
            case ActionBarding:
                Add(questRowId, RewardKind.Barding, data0, item.RowId, NameOr(g.BuddyEquips.GetRowOrDefault(data0)?.Name, itemName), $"{source};ItemAction={type}{exclusivity}");
                return;
            case ActionUnlockLink:
                if (emoteByUnlockLink.TryGetValue(data0, out var emoteId))
                {
                    Add(questRowId, RewardKind.Emote, emoteId, item.RowId, NameOr(g.Emotes.GetRowOrDefault(emoteId)?.Name, itemName), $"{source};ItemAction={type};unlockLink={data0}{exclusivity}");
                    return;
                }
                if (hairstyleByUnlockLink.ContainsKey(data0))
                {
                    // rewardId is the unlock link id: that is what UIState.IsUnlockLinkUnlocked checks at runtime.
                    Add(questRowId, RewardKind.Hairstyle, data0, item.RowId, itemName, $"{source};ItemAction={type};unlockLink={data0}{exclusivity}");
                    return;
                }
                // Neither an emote nor a hairstyle: fall through to the generic untradable-item rule with the link recorded.
                source = $"{source};ItemAction={type};unlockLink={data0}";
                break;
            case ActionGlasses:
                // Facewear has no RewardKind of its own in V1; it is shipped as an untradable Item.
                Add(questRowId, RewardKind.Item, item.RowId, item.RowId, itemName, $"{source};ItemAction={type};glasses{exclusivity}");
                return;
        }

        if (item.IsUntradable && item.ItemSearchCategory.RowId == 0)
        {
            var kind = optionalSlot ? RewardKind.OptionalItem : RewardKind.Item;
            Add(questRowId, kind, item.RowId, item.RowId, itemName, $"{source};untradable{exclusivity}");
        }
    }

    /// <summary>Source suffix naming every other place the item can come from. Empty when the item looks quest-exclusive.</summary>
    private string Exclusivity(Item item)
    {
        var other = new List<string>(4);
        if (!item.IsUntradable) other.Add("Tradable");
        if (item.ItemSearchCategory.RowId != 0) other.Add("Marketable");
        if (gilShopItems.Contains(item.RowId)) other.Add("GilShopItem");
        if (specialShopItems.Contains(item.RowId)) other.Add("SpecialShop");
        if (recipeResults.Contains(item.RowId)) other.Add("Recipe");
        if (gatheringItems.Contains(item.RowId)) other.Add("GatheringItem");
        if (achievementItems.Contains(item.RowId)) other.Add("Achievement");
        return other.Count == 0 ? string.Empty : ";otherSource=" + string.Join(",", other);
    }

    private void AddInstanceUnlock(uint questRowId, uint instanceContentId, string source)
    {
        var instance = g.InstanceContents.GetRowOrDefault(instanceContentId);
        var cfc = instance?.ContentFinderCondition.ValueNullable;
        if (cfc is { RowId: not 0 })
        {
            Add(questRowId, RewardKind.DutyUnlock, cfc.Value.RowId, 0, Text(cfc.Value.Name), $"{source};InstanceContent={instanceContentId}");
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

        foreach (var current in g.AetherCurrents)
        {
            if (current.Quest.RowId == 0)
                continue;
            var name = aetherCurrentZone.TryGetValue(current.RowId, out var zone) ? $"Aether Current ({zone})" : "Aether Current";
            Add(current.Quest.RowId, RewardKind.AetherCurrent, current.RowId, 0, name, "AetherCurrent.Quest");
        }

        foreach (var aoz in g.AozActionTransients)
        {
            if (aoz.RequiredForQuest.RowId == 0)
                continue;
            // AozActionTransient and AozAction share row ids (spell number); the name lives on the linked Action.
            var spell = g.AozActions.GetRowOrDefault(aoz.RowId)?.Action.ValueNullable;
            Add(aoz.RequiredForQuest.RowId, RewardKind.BlueMageSpell, aoz.RowId, 0, NameOr(spell?.Name, $"Blue mage spell #{aoz.Number}"), "AozActionTransient.RequiredForQuest");
        }

        foreach (var ach in g.Achievements)
        {
            var quests = new List<(uint Quest, string Field)>();
            if (ach.Key.RowId != 0 && ach.Key.Is<Quest>())
                quests.Add((ach.Key.RowId, "Achievement.Key"));
            foreach (var data in ach.Data)
            {
                if (data.RowId != 0 && data.Is<Quest>())
                    quests.Add((data.RowId, "Achievement.Data"));
            }
            if (quests.Count == 0)
                continue;

            var title = ach.Title.RowId != 0 ? ach.Title.ValueNullable : null;
            foreach (var (questId, field) in quests.DistinctBy(q => q.Quest))
            {
                if (title is { } t)
                    Add(questId, RewardKind.Title, t.RowId, 0, Text(t.Masculine), $"{field};achievement={ach.RowId};type={ach.Type}");
                else
                    Add(questId, RewardKind.Achievement, ach.RowId, 0, Text(ach.Name), $"{field};type={ach.Type}");
            }
        }

        foreach (var cfc in g.ContentFinderConditions)
        {
            if (cfc.UnlockType != UnlockTypeQuest || cfc.UnlockCriteria.RowId == 0 || !cfc.UnlockCriteria.Is<Quest>())
                continue;
            Add(cfc.UnlockCriteria.RowId, RewardKind.DutyUnlock, cfc.RowId, 0, Text(cfc.Name), "ContentFinderCondition.UnlockCriteria");
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // Helpers

    private void Add(uint questRowId, RewardKind kind, uint rewardId, uint itemId, string name, string source)
    {
        var key = (questRowId, kind, rewardId);
        if (entries.ContainsKey(key))
            return; // first writer wins; forward Quest fields run before reverse links
        entries[key] = new UniqueRewardEntry(questRowId, kind, rewardId, itemId, name, Confidence.Static, source);
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
