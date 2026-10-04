using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// "How you'll clear it" (feature plan v7 C7): the clear badges from a duty's sheet facts, the equipped item level,
/// and the item-level wall on both sides of Patch 8.0 (the current job's gear before; the best job's from Evercold on).
/// </summary>
public class DutyClearTests
{
    private const byte Dragoon = 22;
    private const byte Warrior = 21;

    private static DutyRunInfo Duty(bool support = false, bool trust = false, bool finder = true, int players = 4, ushort itemLevel = 0) =>
        new(1, 1, 1, DutyRunInfo.Dungeons, "the Test Halls", support, trust) { InDutyFinder = finder, Players = players, ItemLevelRequired = itemLevel };

    [Fact]
    public void A_solo_duty_is_solo_and_nothing_else()
    {
        var ways = DutyClear.Ways(Duty(support: true, finder: false, players: 1));
        Assert.Equal(DutyClearWays.Solo, ways);
        Assert.True(DutyClear.WithoutOthers(ways));
    }

    [Fact]
    public void Duty_Support_and_Trust_come_with_the_Duty_Finder()
    {
        var ways = DutyClear.Ways(Duty(support: true, trust: true));
        Assert.Equal(DutyClearWays.DutySupport | DutyClearWays.Trust | DutyClearWays.DutyFinder, ways);
        Assert.True(DutyClear.WithoutOthers(ways));
        Assert.True(DutyClear.WithoutOthers(Duty(trust: true)));
    }

    [Fact]
    public void A_duty_without_NPCs_needs_other_players()
    {
        Assert.Equal(DutyClearWays.DutyFinder, DutyClear.Ways(Duty()));
        Assert.False(DutyClear.WithoutOthers(Duty()));

        // The Duty Finder does not match it (an Ultimate raid): the player forms the party.
        var ultimate = Duty(finder: false, players: 8);
        Assert.Equal(DutyClearWays.PartyOnly, DutyClear.Ways(ultimate));
        Assert.False(DutyClear.WithoutOthers(ultimate));
    }

    [Fact]
    public void The_badges_list_the_ways_that_need_nobody_first()
    {
        Assert.Equal(
            [DutyClearWays.Solo, DutyClearWays.DutySupport, DutyClearWays.Trust, DutyClearWays.DutyFinder, DutyClearWays.PartyOnly],
            DutyClear.Order);
    }

    [Fact]
    public void The_equipped_item_level_counts_a_two_handed_weapon_twice()
    {
        // Main hand 700, no off hand, ten gear slots at 690 (the waist slot empty), a soul crystal that never counts.
        var slots = new ushort[14];
        slots[EquippedItemLevel.MainHand] = 700;
        for (var i = 2; i <= 12; i++)
        {
            slots[i] = i == EquippedItemLevel.Waist ? (ushort)0 : (ushort)690;
        }

        slots[EquippedItemLevel.SoulCrystal] = 999;
        Assert.Equal((700 + 700 + (10 * 690)) / 12, EquippedItemLevel.Average(slots));

        // A shield in the off hand counts once, as itself.
        slots[EquippedItemLevel.OffHand] = 680;
        Assert.Equal((700 + 680 + (10 * 690)) / 12, EquippedItemLevel.Average(slots));

        // No weapon: nothing to judge by.
        Assert.Equal(0, EquippedItemLevel.Average(new ushort[14]));
        Assert.Equal(0, EquippedItemLevel.Average([]));
    }

    [Fact]
    public void The_rule_follows_the_newest_expansion_in_the_data()
    {
        Assert.Same(ItemLevelRule.PerJob, ItemLevelRule.ForExpansion(5));
        Assert.Same(ItemLevelRule.Shared, ItemLevelRule.ForExpansion(ItemLevelRule.SharedFromExpansion));
        Assert.Equal(ItemLevelBasis.CurrentJob, ItemLevelRule.PerJob.Basis);
        Assert.Equal(ItemLevelBasis.BestJob, ItemLevelRule.Shared.Basis);

        // Patch 7.x data: Dawntrail's quests are the newest.
        var dawntrail = Fixture.Catalog(Fixture.Quest(70000) with { Expansion = 5 }, Fixture.Quest(65600) with { Expansion = 0 });
        Assert.Same(ItemLevelRule.PerJob, ItemLevelRule.For(dawntrail));

        // Patch 8.0 data: Evercold's quests are in it.
        var evercold = Fixture.Catalog(Fixture.Quest(70000) with { Expansion = 5 }, Fixture.Quest(71000) with { Expansion = 6 });
        Assert.Same(ItemLevelRule.Shared, ItemLevelRule.For(evercold));

        // A removed row never decides.
        var removed = Fixture.Catalog(Fixture.Quest(70000) with { Expansion = 5 }, Fixture.Quest(71000) with { Expansion = 6, IsRetired = true });
        Assert.Same(ItemLevelRule.PerJob, ItemLevelRule.For(removed));
    }

    private static CharacterSnapshot Geared(ushort equipped, params (byte Job, ushort Level)[] gearsets) =>
        Fixture.Snapshot() with
        {
            CurrentJob = Dragoon,
            ItemLevel = equipped,
            JobItemLevels = gearsets.ToDictionary(g => g.Job, g => g.Level),
        };

    [Fact]
    public void Before_Evercold_the_wall_reads_the_job_you_queue_on()
    {
        var snapshot = Geared(677, (Dragoon, 677), (Warrior, 692));
        var wall = ItemLevelWall.For(690, snapshot, ItemLevelRule.PerJob);
        Assert.NotNull(wall);
        Assert.False(wall.Met);
        Assert.Equal(ItemLevelBasis.CurrentJob, wall.Basis);
        Assert.Equal(677, wall.Have);
        Assert.Equal(13, wall.Short);

        // The Warrior gearset would pass: switching is the answer.
        Assert.True(wall.OtherGearsetPasses);
        Assert.Equal(Warrior, wall.BestJob);
        Assert.Equal(692, wall.BestItemLevel);

        // A gearset that falls short too is not offered.
        var weak = ItemLevelWall.For(700, snapshot, ItemLevelRule.PerJob);
        Assert.NotNull(weak);
        Assert.False(weak.OtherGearsetPasses);
    }

    [Fact]
    public void From_Evercold_the_wall_reads_the_best_job()
    {
        var snapshot = Geared(677, (Dragoon, 677), (Warrior, 692));
        var wall = ItemLevelWall.For(690, snapshot, ItemLevelRule.Shared);
        Assert.NotNull(wall);
        Assert.True(wall.Met);
        Assert.Equal(ItemLevelBasis.BestJob, wall.Basis);
        Assert.Equal(692, wall.Have);
        Assert.Equal(Warrior, wall.BestJob);
        Assert.False(wall.OtherGearsetPasses);
        Assert.Equal(0, wall.Short);
    }

    [Fact]
    public void A_tie_keeps_the_current_job()
    {
        var wall = ItemLevelWall.For(690, Geared(692, (Warrior, 692)), ItemLevelRule.Shared);
        Assert.NotNull(wall);
        Assert.Equal(Dragoon, wall.BestJob);
    }

    [Fact]
    public void No_wall_without_a_requirement_or_an_item_level()
    {
        var snapshot = Geared(677, (Dragoon, 677));
        Assert.Null(ItemLevelWall.For(0, snapshot, ItemLevelRule.PerJob));
        Assert.Null(ItemLevelWall.For(690, null, ItemLevelRule.PerJob));
        Assert.Null(ItemLevelWall.For(690, Fixture.Snapshot(), ItemLevelRule.PerJob));
        Assert.Null(ItemLevelWall.For(690, Fixture.Snapshot(), ItemLevelRule.Shared));

        // Gear not read this capture (hooks paused) but the job's gearset known: that gearset stands in.
        var stored = Geared(0, (Dragoon, 680));
        var wall = ItemLevelWall.For(690, stored, ItemLevelRule.PerJob);
        Assert.NotNull(wall);
        Assert.Equal(680, wall.Have);

        // A duty row carries its own requirement.
        Assert.Equal(690, ItemLevelWall.For(Duty(itemLevel: 690), snapshot, ItemLevelRule.PerJob)!.Required);
    }
}
