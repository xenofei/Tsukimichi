using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Unlocks;

/// <summary>
/// Find by unlock (plan v7, 1.19.0 K3) without game files: the kinds a quest opens, the finds across the catalog (flying
/// in a zone is one find over every quest current), the search over them under the spoiler shield and Sprout mode's
/// reach, the row a menu routes from, the Unlocks filter and the table search, and Route to unlock.
/// </summary>
public class UnlockFindTests
{
    internal const uint JewelOfThavnair = 70_101;
    internal const uint SecondCurrent = 70_102;
    internal const uint BoundForKugane = 70_103;
    internal const uint CamelQuest = 70_104;
    internal const uint OtherWayToKugane = 70_105;
    internal const uint Plain = 70_106;
    internal const uint Kugane = 628;
    private const uint Camel = 55;

    internal static QuestCatalog Quests() => Catalog(
        Quest(JewelOfThavnair, "The Jewel of Thavnair") with { Expansion = 4 },
        Quest(SecondCurrent, "A Second Current") with { Expansion = 4 },
        Quest(BoundForKugane, "Not without Incident") with { EventIconType = UnlockAreas.MainScenarioIconType, Expansion = 2 },
        Quest(CamelQuest, "A Camel of One's Own") with { Rewards = [new RewardRef(RewardKind.Mount, Camel, 0, 1, "Thavnair Camel", 4001)] },
        Quest(OtherWayToKugane, "The Man from Ul'dah") with { EventIconType = UnlockAreas.MainScenarioIconType, Expansion = 2 },
        Quest(Plain, "Nothing Opens Here"));

    private static UnlockLinks Links() => new()
    {
        Zones = [new UnlockZone(Kugane, "Kugane", "Hingashi", 371, 2, 111, 628)],
        Warps = [new UnlockWarp(BoundForKugane, Kugane), new UnlockWarp(OtherWayToKugane, Kugane)],
        Touches = [new UnlockTouch(BoundForKugane, Kugane, 0f, 0f), new UnlockTouch(OtherWayToKugane, Kugane, 0f, 0f)],
        AreaIcon = 7,
    };

    private static UniqueRewardEntry Current(uint quest) =>
        new(quest, RewardKind.AetherCurrent, 0, 0, "Aether Current (Thavnair)", Confidence.Static, "AetherCurrent");

    internal static QuestUnlocks Build() => QuestUnlocks.Build(
        Quests(),
        UniqueRewardCatalog.Build(new UniqueRewardsData("test", default, [Current(JewelOfThavnair), Current(SecondCurrent)]), new Dictionary<uint, UniqueOverride>(), CuratedData.Empty),
        PlanDuties.Empty,
        Links());

    [Fact]
    public void Each_quest_carries_the_kinds_it_opens_its_rewards_included()
    {
        var unlocks = Build();

        Assert.Equal(UnlockFindKinds.Bit(UnlockFindKind.Flying), unlocks.KindMask(JewelOfThavnair));
        Assert.Equal(UnlockFindKinds.Bit(UnlockFindKind.Area), unlocks.KindMask(BoundForKugane));
        // The mount is a Rewards tile, never an Unlocks row, and still a kind the quest opens.
        Assert.Equal(UnlockFindKinds.Bit(UnlockFindKind.Mount), unlocks.KindMask(CamelQuest));
        Assert.Equal(0, unlocks.KindMask(Plain));
        Assert.Equal(0, QuestUnlocks.Empty.KindMask(JewelOfThavnair));
    }

    [Fact]
    public void Flying_in_a_zone_is_one_find_that_needs_every_quest_current()
    {
        var unlocks = Build();

        var flying = Assert.Single(unlocks.Finds, f => f.Target == UnlockTarget.Flying);
        Assert.Equal("Flying in Thavnair", flying.Label);
        Assert.Equal([JewelOfThavnair, SecondCurrent], flying.Quests.ToArray());
        Assert.True(flying.NeedsAll);
        Assert.Equal(UnlockFindKind.Flying, flying.Kind);

        var kugane = Assert.Single(unlocks.Finds, f => f.Target == UnlockTarget.Zone);
        Assert.Equal("Kugane", kugane.Label);
        Assert.Equal([BoundForKugane, OtherWayToKugane], kugane.Quests.ToArray());
        Assert.False(kugane.NeedsAll);

        // A label that already says it is not said twice.
        Assert.Equal("Flying in Heavensward", UnlockFindKinds.Label(UnlockTarget.Flying, "Flying in Heavensward"));
    }

    [Fact]
    public void The_search_matches_every_term_and_puts_a_label_that_starts_with_it_first()
    {
        var unlocks = Build();

        var flying = Assert.Single(unlocks.Find(SearchIndex.Normalize("flying thav")));
        Assert.Equal("Flying in Thavnair", flying.Find.Label);
        Assert.Equal(JewelOfThavnair, flying.Via);

        // The camel's name starts with the term, so it leads though the flying row comes first in display order.
        Assert.Equal(["Thavnair Camel", "Flying in Thavnair"], unlocks.Find("thavnair").Select(m => m.Find.Label).ToArray());
        Assert.Single(unlocks.Find("thavnair", max: 1));
        Assert.Empty(unlocks.Find(string.Empty));
        Assert.Empty(unlocks.Find("sirensong"));
    }

    [Fact]
    public void What_a_masked_quest_opens_is_named_only_through_a_quest_the_shield_shows()
    {
        var unlocks = Build();

        // The first opener is hidden: the find is still listed, through the other one.
        var kugane = Assert.Single(unlocks.Find("kugane", rowId => rowId != BoundForKugane));
        Assert.Equal(OtherWayToKugane, kugane.Via);

        // Every opener hidden: never listed.
        Assert.Empty(unlocks.Find("kugane", rowId => rowId is not (BoundForKugane or OtherWayToKugane)));
    }

    [Fact]
    public void A_term_matches_what_the_quest_opens_within_reach()
    {
        var unlocks = Build();

        Assert.True(unlocks.MatchesTerm(JewelOfThavnair, "thav"));
        Assert.True(unlocks.MatchesTerm(JewelOfThavnair, "flying in"));
        Assert.True(unlocks.MatchesTerm(BoundForKugane, "kugane"));
        Assert.False(unlocks.MatchesTerm(Plain, "kugane"));

        // Sprout mode: Kugane belongs to Stormblood, past a character who has reached Heavensward.
        Assert.False(unlocks.MatchesTerm(BoundForKugane, "kugane", reach: 1));
        Assert.Empty(unlocks.Find("kugane", reach: 1));
    }

    [Fact]
    public void An_unlock_row_finds_its_find_and_a_row_no_chip_names_none()
    {
        var unlocks = Build();

        var row = Assert.Single(unlocks.For(BoundForKugane), e => e.Target == UnlockTarget.Zone);
        Assert.Equal([BoundForKugane, OtherWayToKugane], unlocks.FindFor(row)!.Quests.ToArray());
        Assert.Null(unlocks.FindFor(new UnlockEntry(UnlockTarget.NextQuest, Plain, "Nothing Opens Here", 0, UnlockSource.Sheet, 0)));
        Assert.Null(unlocks.FindFor(new UnlockEntry(UnlockTarget.Minion, 9, "Wind-up Moogle", 0, UnlockSource.Sheet, 0)));
    }

    [Fact]
    public void Route_to_flying_takes_every_quest_current_and_anything_else_one_quest()
    {
        var catalog = Quests();
        var unlocks = Build();
        var states = new Dictionary<uint, Core.Evaluation.QuestEvaluation>();

        var flying = RouteTarget.ForUnlock(unlocks.Finds.Single(f => f.Target == UnlockTarget.Flying), flyingTerritory: 957);
        Assert.Equal(RouteTargetKind.Unlock, flying.Kind);
        Assert.Equal("Flying in Thavnair", flying.Label);
        Assert.Equal(957u, flying.FlyingTerritory);
        Assert.True(flying.IsUnion);
        var route = UnlockRoute.Build(flying, catalog, states);
        Assert.Equal(RouteOutcome.Route, route.Outcome);
        Assert.Equal([JewelOfThavnair, SecondCurrent], route.Steps.Where(s => s.IsTarget).Select(s => s.RowId).Order().ToArray());

        var kugane = RouteTarget.ForUnlock(unlocks.Finds.Single(f => f.Target == UnlockTarget.Zone));
        Assert.False(kugane.IsUnion);
        Assert.Equal(0u, kugane.FlyingTerritory);
        var one = UnlockRoute.Build(kugane, catalog, states);
        Assert.Single(one.Steps);
        Assert.Single(one.TargetAlternatives);

        // A followed route keeps the zone its field currents are in.
        var saved = SavedRoute.From(flying, 1);
        Assert.Equal(957u, saved.ToTarget().FlyingTerritory);
        Assert.Equal(RouteTargetKind.Unlock, saved.ToTarget().Kind);
        Assert.Equal(2, saved.ToTarget().Parts.Count);
        Assert.True(saved.Matches(flying));
    }

    [Fact]
    public void Field_currents_left_group_by_their_aetheryte_and_unknown_ones_stay()
    {
        FieldCurrentStop[] all =
        [
            new(1, 10, "Yedlihmad"),
            new(2, 20, "The Great Work"),
            new(3, 0, string.Empty),
            new(4, 10, "Yedlihmad"),
            new(5, 20, "The Great Work"),
        ];

        // 2 is attuned; 5 cannot be told (a stored alt), so it stays.
        var left = FieldCurrentStops.Left(all, id => id switch { 2 => true, 5 => null, _ => false });
        Assert.Equal([1u, 4u, 5u, 3u], left.Select(s => s.AetherCurrentId).ToArray());
        Assert.Equal(10, FieldCurrentStops.Stops(6, 4));
        Assert.Equal(0, FieldCurrentStops.Stops(-1, 0));
    }
}
