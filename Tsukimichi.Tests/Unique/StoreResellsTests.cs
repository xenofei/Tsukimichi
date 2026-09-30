using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Unique;

public class StoreResellsTests
{
    private static readonly UniqueRewardEntry[] Entries =
    [
        new(68546, RewardKind.Mount, 99, 22437, "Starlight bear", Confidence.Static, "s") { OtherSources = [OtherSource.OnlineStore] },
        new(67079, RewardKind.Emote, 109, 0, "Bomb Dance", Confidence.Static, "s") { OtherSources = [OtherSource.OnlineStore] },
        new(66038, RewardKind.Emote, 114, 0, "Most Gentlemanly", Confidence.Static, "s"),
        new(70858, RewardKind.TripleTriadCard, 444, 40000, "King Elmer III", Confidence.Static, "s") { OtherSources = [OtherSource.SpecialShop] },
    ];

    [Fact]
    public void Matches_by_item_or_by_collectible_but_only_for_online_store_entries()
    {
        var resells = StoreResells.Build(Entries);

        Assert.Equal(2, resells.Count);
        Assert.True(resells.Contains(new RewardRef(RewardKind.Item, 22437, 22437, 1, "Starlight Bear Horn", 0)), "the quest's item");
        Assert.True(resells.Contains(new RewardRef(RewardKind.Mount, 99, 0, 1, "Starlight Bear", 0)), "the mount itself");
        Assert.True(resells.Contains(new RewardRef(RewardKind.Emote, 109, 0, 1, "Bomb Dance", 0)), "an emote the quest grants directly");
        Assert.False(resells.Contains(new RewardRef(RewardKind.Emote, 114, 0, 1, "Most Gentlemanly", 0)));
        Assert.False(resells.Contains(new RewardRef(RewardKind.TripleTriadCard, 444, 40000, 1, "King Elmer III", 0)), "SpecialShop is not the store");
        Assert.False(resells.Contains(new RewardRef(RewardKind.Other, 0, 0, 1, "gil", 0)), "zero ids never match");
    }

    [Fact]
    public void Empty_when_nothing_is_sold_on_the_store()
    {
        Assert.Same(StoreResells.Empty, StoreResells.Build([Entries[2], Entries[3]]));
        Assert.Equal(0, StoreResells.Empty.Count);
        Assert.False(StoreResells.Empty.Contains(RewardKind.Mount, 99, 22437));
        Assert.Throws<ArgumentNullException>(() => StoreResells.Build(null!));
    }
}
