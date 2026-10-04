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

    [Fact]
    public void The_jobs_that_qualify_are_listed_best_first()
    {
        const byte Sage = 40, WhiteMage = 24, Bard = 23;
        var snapshot = Geared(677, (Dragoon, 677), (Warrior, 692), (Sage, 705), (WhiteMage, 692), (Bard, 650));
        var wall = ItemLevelWall.For(690, snapshot, ItemLevelRule.PerJob);
        Assert.NotNull(wall);

        // Sage first, then the two at i692 by job id; the bard falls short and the current job is never listed.
        Assert.Equal([(Sage, (ushort)705), (Warrior, (ushort)692), (WhiteMage, (ushort)692)], wall.Qualifying);
        Assert.Equal(Sage, wall.BestJob);
        Assert.True(wall.OtherGearsetPasses);
    }

    [Fact]
    public void Crafter_gatherer_and_limited_job_gearsets_never_pass_the_wall()
    {
        // Review fix: a Carpenter gearset at i710 and a Blue Mage one at i700 cannot queue, so neither passes for a
        // Paladin at i680, nor stands in for the 8.0 shared item level.
        const byte Paladin = 19, Carpenter = 8, Botanist = 17, BlueMage = 36;
        var snapshot = Fixture.Snapshot() with
        {
            CurrentJob = Paladin,
            ItemLevel = 680,
            JobItemLevels = new Dictionary<byte, ushort> { [Paladin] = 680, [Carpenter] = 710, [Botanist] = 705, [BlueMage] = 700 },
        };

        var wall = ItemLevelWall.For(690, snapshot, ItemLevelRule.PerJob);
        Assert.NotNull(wall);
        Assert.False(wall.Met);
        Assert.False(wall.OtherGearsetPasses);
        Assert.Equal(Paladin, wall.BestJob);
        Assert.Empty(wall.Qualifying);

        var shared = ItemLevelWall.For(690, snapshot, ItemLevelRule.Shared);
        Assert.NotNull(shared);
        Assert.False(shared.Met);
        Assert.Equal(680, shared.Have);

        // Standing on the Carpenter: the wall is judged by the best combat job, never the crafter's own gear.
        var crafting = snapshot with { CurrentJob = Carpenter, ItemLevel = 710 };
        var onCrafter = ItemLevelWall.For(690, crafting, ItemLevelRule.PerJob);
        Assert.NotNull(onCrafter);
        Assert.False(onCrafter.Met);
        Assert.Equal(Paladin, onCrafter.CurrentJob);
        Assert.Equal(680, onCrafter.Have);
        Assert.False(ItemLevelWall.For(690, crafting, ItemLevelRule.Shared)!.Met);

        // A crafter with no combat gearset known: nothing to judge.
        Assert.Null(ItemLevelWall.For(690, Fixture.Snapshot() with { CurrentJob = Carpenter, ItemLevel = 710, JobItemLevels = new Dictionary<byte, ushort> { [Carpenter] = 710 } }, ItemLevelRule.PerJob));
    }

    [Fact]
    public void Duty_jobs_are_the_disciples_of_war_and_magic_without_limited_jobs()
    {
        Assert.True(DutyJobs.ByRowId(19));
        Assert.True(DutyJobs.ByRowId(1));
        Assert.False(DutyJobs.ByRowId(0));
        Assert.False(DutyJobs.ByRowId(8));
        Assert.False(DutyJobs.ByRowId(18));
        Assert.False(DutyJobs.ByRowId(36));

        // From the sheet: a later limited job (row 43 here) is known by its flag; a row the sheet lacks falls back.
        var fromSheet = DutyJobs.From([(19u, false, false, false), (8u, true, false, false), (43u, false, false, true)]);
        Assert.True(fromSheet(19));
        Assert.False(fromSheet(8));
        Assert.False(fromSheet(43));
        Assert.False(fromSheet(17));
        Assert.True(fromSheet(21));
    }

    [Fact]
    public void Each_surface_wears_its_own_badges()
    {
        var support = Duty(support: true);
        var savage = Duty(players: 8) with { HighEnd = true };

        // The detail pane and the Duty Finder hint: every badge.
        Assert.Equal([new DutyBadge(DutyBadgeKind.SoloWithNpcs), new DutyBadge(DutyBadgeKind.Optional)], DutyBadgeRules.For(support, false, DutyBadgeSurface.Detail));
        Assert.Equal([new DutyBadge(DutyBadgeKind.Group, 8), new DutyBadge(DutyBadgeKind.HighEnd), new DutyBadge(DutyBadgeKind.StoryRequired)], DutyBadgeRules.For(savage, true, DutyBadgeSurface.DutyFinder));

        // My blues: Story-required when the story needs it, never Optional (what nearly every blue unlock is).
        Assert.Equal([new DutyBadge(DutyBadgeKind.SoloWithNpcs), new DutyBadge(DutyBadgeKind.StoryRequired)], DutyBadgeRules.For(support, true, DutyBadgeSurface.MyBlues));
        Assert.Equal([new DutyBadge(DutyBadgeKind.SoloWithNpcs)], DutyBadgeRules.For(support, false, DutyBadgeSurface.MyBlues));

        // A route step: the size and High-end; the step's MSQ mark speaks for the story.
        Assert.Equal([new DutyBadge(DutyBadgeKind.Group, 8), new DutyBadge(DutyBadgeKind.HighEnd)], DutyBadgeRules.For(savage, true, DutyBadgeSurface.Route));

        // The Duties board and the roulette hint: the size alone, or nothing when it is unknown.
        Assert.Equal([new DutyBadge(DutyBadgeKind.Group, 8)], DutyBadgeRules.For(savage, true, DutyBadgeSurface.Board));
        Assert.Empty(DutyBadgeRules.For(Duty(players: 0), true, DutyBadgeSurface.Board));
    }

    [Fact]
    public void The_badges_read_size_then_high_end_then_story()
    {
        // Duty Support: Solo with NPCs, whatever the size.
        Assert.Equal(
            [new DutyBadge(DutyBadgeKind.SoloWithNpcs), new DutyBadge(DutyBadgeKind.StoryRequired)],
            DutyBadgeRules.For(Duty(support: true), storyRequired: true));

        // Players only: a group of its size; an alliance raid seats 24.
        Assert.Equal([new DutyBadge(DutyBadgeKind.Group, 24), new DutyBadge(DutyBadgeKind.Optional)], DutyBadgeRules.For(Duty(players: 24), storyRequired: false));

        // High-end follows the size; the story badge is left out when unknown.
        var savage = Duty(players: 8) with { HighEnd = true };
        Assert.Equal([new DutyBadge(DutyBadgeKind.Group, 8), new DutyBadge(DutyBadgeKind.HighEnd)], DutyBadgeRules.For(savage, storyRequired: null));

        // A solo duty is Solo; an unknown size wears no size badge.
        Assert.Equal(new DutyBadge(DutyBadgeKind.Solo, 1), DutyBadgeRules.Size(Duty(players: 1)));
        Assert.Null(DutyBadgeRules.Size(Duty(players: 0)));
    }
}
