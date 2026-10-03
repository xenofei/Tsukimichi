using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Unlocks;

/// <summary>
/// The first-visit rule (feature plan v6 K1, unlocks spec §2.2) over a synthetic three-city main scenario: each city's
/// own opener opens its zone, an envoy opens the other cities it reaches and never reopens its own, the 40-yalm radius
/// edge, return warps and a cycle in the data.
/// </summary>
public class UnlockAreasTests
{
    // Zones.
    private const uint Gridania = 132;
    private const uint Limsa = 128;
    private const uint Uldah = 130;
    private const uint Shroud = 148;
    private const uint Thanalan = 140;
    private const uint LavenderBeds = 340;

    // Quests: a start per city, a second step, an envoy each, the shared quest after them.
    private const uint G1 = 65_601;
    private const uint G2 = 65_602;
    private const uint G3 = 65_603;
    private const uint L1 = 65_611;
    private const uint L2 = 65_612;
    private const uint L3 = 65_613;
    private const uint U1 = 65_621;
    private const uint Shared = 65_631;
    private const uint Housing = 65_641;
    private const uint HousingOrphan = 65_642;

    private static QuestRecord Msq(uint rowId, byte level, params uint[] previous) =>
        Quest(rowId) with { EventIconType = UnlockAreas.MainScenarioIconType, Level = level, PreviousQuests = new Prereq(previous, JoinKind.All) };

    private static QuestCatalog Story() => Catalog(
        Msq(G1, 1),
        Msq(G2, 5, G1),
        Msq(G3, 15, G2),
        Msq(L1, 1),
        Msq(L2, 5, L1),
        Msq(L3, 15, L2),
        Msq(U1, 1),
        Msq(Shared, 17, G3, L3) with { PreviousQuests = new Prereq([G3, L3], JoinKind.Any) },
        Quest(Housing) with { Level = 20, PreviousQuests = new Prereq([G2], JoinKind.All), Issuer = new Issuer(1, "Usher", Shroud, 1, 0f, 0f, 0f) },
        Quest(HousingOrphan) with { Level = 20, Issuer = new Issuer(1, "Usher", Shroud, 1, 0f, 0f, 0f) });

    private static UnlockLinks Links() => new()
    {
        Zones =
        [
            new UnlockZone(Gridania, "New Gridania", "The Black Shroud", 2, 0, 2),
            new UnlockZone(Limsa, "Limsa Lominsa Upper Decks", "La Noscea", 11, 0, 8),
            new UnlockZone(Uldah, "Ul'dah - Steps of Nald", "Thanalan", 13, 0, 9),
            new UnlockZone(Shroud, "Central Shroud", "The Black Shroud", 4, 0, 3),
            new UnlockZone(Thanalan, "Western Thanalan", "Thanalan", 20, 0, 17),
            new UnlockZone(LavenderBeds, "The Lavender Beds", "The Black Shroud", 72, 0, 0),
        ],
        Aetherytes =
        [
            new UnlockAetheryte(3, Shroud, "Bentbranch Meadows", 0f, 0f, true),
            new UnlockAetheryte(4, Shroud, "The Hawthorne Hut", 300f, 0f, true),
        ],
        Touches =
        [
            new UnlockTouch(G1, Gridania, 0f, 0f),
            new UnlockTouch(G2, Gridania, 0f, 0f),
            new UnlockTouch(G2, Shroud, 39f, 0f),       // within 40 y of Bentbranch
            new UnlockTouch(G2, Shroud, 300f, 41f),     // 41 y from the Hawthorne Hut: too far
            new UnlockTouch(G3, Limsa, 0f, 0f),         // the envoy reaches Limsa and Ul'dah
            new UnlockTouch(G3, Uldah, 0f, 0f),
            new UnlockTouch(L1, Limsa, 0f, 0f),
            new UnlockTouch(L2, Limsa, 0f, 0f),
            new UnlockTouch(L3, Limsa, 0f, 0f),         // the Lominsan envoy: Limsa again, Gridania new
            new UnlockTouch(L3, Gridania, 0f, 0f),
            new UnlockTouch(U1, Uldah, 0f, 0f),
            new UnlockTouch(Shared, Gridania, 0f, 0f),
            new UnlockTouch(Shared, Thanalan, 0f, 0f),
        ],
        Warps =
        [
            new UnlockWarp(Housing, LavenderBeds),
            new UnlockWarp(Housing, Gridania),          // the way home
            new UnlockWarp(HousingOrphan, LavenderBeds),
            new UnlockWarp(HousingOrphan, Gridania),    // no prerequisites, but the story reached Gridania at level 1
        ],
    };

    [Fact]
    public void Each_city_start_opens_its_own_city()
    {
        var result = UnlockAreas.Derive(Story(), Links());

        Assert.Equal([Gridania], result.Zones[G1]);
        Assert.Equal([Limsa], result.Zones[L1]);
        Assert.Equal([Uldah], result.Zones[U1]);
        Assert.False(result.Zones.ContainsKey(L2));
    }

    [Fact]
    public void An_envoy_opens_the_other_cities_and_never_reopens_its_own()
    {
        var result = UnlockAreas.Derive(Story(), Links());

        Assert.Equal([Limsa, Uldah], result.Zones[G3].Order());
        Assert.Equal([Gridania], result.Zones[L3]);
    }

    [Fact]
    public void A_quest_after_an_any_join_opens_only_what_no_route_reached()
    {
        var result = UnlockAreas.Derive(Story(), Links());

        Assert.Equal([Thanalan], result.Zones[Shared]);
    }

    [Fact]
    public void An_aetheryte_opens_within_forty_yalms_and_not_beyond()
    {
        var result = UnlockAreas.Derive(Story(), Links());

        Assert.Equal([3u], result.Aetherytes[G2]);
        Assert.Equal([4u], UnlockAreas.Derive(Story(), Links(), radius: 42f).Aetherytes[G2].Except([3u]));
    }

    [Fact]
    public void A_return_warp_is_no_unlock()
    {
        var result = UnlockAreas.Derive(Story(), Links());

        Assert.Equal([LavenderBeds], result.Warps[Housing]);
        Assert.Equal([LavenderBeds], result.Warps[HousingOrphan]);
    }

    [Fact]
    public void A_cycle_in_the_data_ends()
    {
        var a = Msq(66_001, 1, 66_002);
        var b = Msq(66_002, 1, 66_001);
        var links = new UnlockLinks
        {
            Zones = [new UnlockZone(Gridania, "New Gridania", string.Empty, 2, 0, 2)],
            Touches = [new UnlockTouch(a.RowId, Gridania, 0f, 0f), new UnlockTouch(b.RowId, Gridania, 0f, 0f)],
        };

        var result = UnlockAreas.Derive(Catalog(a, b), links);

        // Each is the other's ancestor, so neither is first; what matters is that the walk ends.
        Assert.Empty(result.Zones);
    }

    [Fact]
    public void Feature_quests_open_no_zone_by_first_visit()
    {
        var side = Quest(66_100) with { EventIconType = 8 };
        var links = new UnlockLinks
        {
            Zones = [new UnlockZone(Gridania, "New Gridania", string.Empty, 2, 0, 2)],
            Touches = [new UnlockTouch(side.RowId, Gridania, 0f, 0f)],
        };

        Assert.Empty(UnlockAreas.Derive(Catalog(side), links).Zones);
        Assert.Same(UnlockAreas.Result.Empty, UnlockAreas.Derive(Catalog(side), UnlockLinks.Empty));
    }
}
