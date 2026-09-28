using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Unique;

public class RewardLookupTests
{
    private const uint MountQuest = 66001;
    private const uint GearQuest = 66002;
    private const uint HiddenQuest = 66003;
    private const uint SharedItemQuest = 66004;

    private const uint WhistleItem = 20000;
    private const uint GearItem = 21000;
    private const uint SharedItem = 22000;
    private const uint KeyItem = 2_001_234;

    private static UniqueRewardEntry Entry(uint quest, RewardKind kind, uint rewardId, uint itemId, string name) =>
        new(quest, kind, rewardId, itemId, name, Confidence.Static, "test");

    // Item-kind entries carry their item id as the reward id, as the shipped file does; the catalog merges entries by
    // (quest, kind, reward id), so two items of one quest must differ there.
    private static readonly UniqueRewardsData Shipped = new("1.0", default, [
        Entry(MountQuest, RewardKind.Mount, 10, WhistleItem, "Company Chocobo"),
        Entry(MountQuest, RewardKind.Emote, 20, 0, "Salute"),
        Entry(GearQuest, RewardKind.ArtifactGear, GearItem, GearItem, "Helm of Light"),
        Entry(GearQuest, RewardKind.Item, SharedItem, SharedItem, "Token"),
        Entry(HiddenQuest, RewardKind.Item, 23000, 23000, "Hidden Trinket"),
        Entry(SharedItemQuest, RewardKind.Item, SharedItem, SharedItem, "Token"),
        Entry(SharedItemQuest, RewardKind.Item, KeyItem, KeyItem, "Key of Ages"),
    ]);

    private static UniqueRewardCatalog Catalog(IReadOnlyDictionary<uint, UniqueOverride>? overrides = null) =>
        UniqueRewardCatalog.Build(Shipped, overrides ?? new Dictionary<uint, UniqueOverride>(), CuratedData.Empty);

    private static QuestCatalog Quests() => QuestCatalog.Build([
        new QuestRecord { RowId = MountQuest, QuestId = QuestRecord.ToQuestId(MountQuest), Name = "My Little Chocobo" },
        new QuestRecord { RowId = GearQuest, QuestId = QuestRecord.ToQuestId(GearQuest), Name = "The Ultimate Weapon" },
    ]);

    [Fact]
    public void ByItem_returns_the_entries_for_a_base_item_id_in_catalog_order()
    {
        var lookup = new RewardLookup(Catalog(), Quests());

        Assert.Equal(["Company Chocobo"], lookup.ByItem(WhistleItem).Select(e => e.RewardName));
        Assert.Equal([GearQuest, SharedItemQuest], lookup.ByItem(SharedItem).Select(e => e.QuestRowId));
        Assert.Equal(5, lookup.ItemCount);
    }

    [Fact]
    public void ByItem_normalises_hq_and_collectable_ids_and_keeps_event_item_ids()
    {
        var lookup = new RewardLookup(Catalog(), null);

        Assert.Equal(lookup.ByItem(WhistleItem), lookup.ByItem(WhistleItem + RewardLookup.HqOffset));
        Assert.Equal(lookup.ByItem(GearItem), lookup.ByItem(GearItem + RewardLookup.CollectableOffset));
        Assert.Equal(["Key of Ages"], lookup.ByItem(KeyItem).Select(e => e.RewardName));
        Assert.Equal(KeyItem, RewardLookup.NormalizeItemId(KeyItem));
        Assert.Equal(WhistleItem, RewardLookup.NormalizeItemId(WhistleItem + RewardLookup.HqOffset));
        Assert.Equal(GearItem, RewardLookup.NormalizeItemId(GearItem + RewardLookup.CollectableOffset));
        Assert.Equal(0u, RewardLookup.NormalizeItemId(0));
        Assert.Equal(0u, RewardLookup.NormalizeItemId(ulong.MaxValue));
    }

    [Fact]
    public void ByItem_is_empty_for_unknown_ids_zero_and_entries_without_an_item()
    {
        var lookup = new RewardLookup(Catalog(), null);

        Assert.Empty(lookup.ByItem(99999));
        Assert.Empty(lookup.ByItem(0));
        Assert.Empty(lookup.ByItem(RewardLookup.HqOffset));
        Assert.Same(lookup.ByItem(99999), lookup.ByItem(0));
        Assert.DoesNotContain(lookup.ByItem(WhistleItem), e => e.Kind == RewardKind.Emote);
    }

    [Fact]
    public void ByItem_omits_quests_the_user_marked_not_unique()
    {
        var overrides = new Dictionary<uint, UniqueOverride> { [HiddenQuest] = new(false, null) };
        var lookup = new RewardLookup(Catalog(overrides), null);

        Assert.Empty(lookup.ByItem(23000));
        Assert.NotEmpty(lookup.ByItem(WhistleItem));
    }

    [Fact]
    public void QuestFor_resolves_through_the_quest_catalog_and_is_null_without_one()
    {
        var withQuests = new RewardLookup(Catalog(), Quests());
        var withoutQuests = new RewardLookup(Catalog(), null);
        var whistle = withQuests.ByItem(WhistleItem)[0];
        var token = withQuests.ByItem(SharedItem)[1];

        Assert.Equal("My Little Chocobo", withQuests.QuestFor(whistle)?.Name);
        Assert.Null(withQuests.QuestFor(token));
        Assert.Null(withoutQuests.QuestFor(whistle));
    }

    [Fact]
    public void Empty_lookup_resolves_nothing()
    {
        Assert.Equal(0, RewardLookup.Empty.ItemCount);
        Assert.Empty(RewardLookup.Empty.ByItem(WhistleItem));
        Assert.Null(RewardLookup.Empty.Quests);
        Assert.True(RewardLookup.Empty.Matches(UniqueRewardCatalog.Empty, null));
    }

    [Fact]
    public void Source_rebuilds_only_when_a_catalog_reference_changes()
    {
        var rewards = Catalog();
        QuestCatalog? quests = null;
        var source = new RewardLookupSource(() => rewards, () => quests);

        var first = source.Current;
        Assert.Same(first, source.Current);
        Assert.Same(rewards, first.Rewards);
        Assert.Null(first.Quests);

        quests = Quests();
        var second = source.Current;
        Assert.NotSame(first, second);
        Assert.Same(quests, second.Quests);
        Assert.Same(second, source.Current);

        rewards = Catalog();
        var third = source.Current;
        Assert.NotSame(second, third);
        Assert.Same(rewards, third.Rewards);
        Assert.Same(quests, third.Quests);
    }
}
