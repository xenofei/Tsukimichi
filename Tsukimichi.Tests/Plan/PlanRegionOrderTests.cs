using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Unique;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// My blues zone order (1.6.0): within a level band, zones are walked region by region (the map's region), so the
/// plan stops criss-crossing the map; across bands, and without regions, the order stays by level.
/// </summary>
public class PlanRegionOrderTests
{
    private static QuestRecord Blue(uint rowId, byte level, uint territory, uint map) =>
        Quest(rowId) with
        {
            Level = level,
            Journal = new JournalRef(3, "Side", 60, "Sidequests", 1, "Genre", (int)rowId),
            Issuer = new Issuer(1, "Giver", territory, map, 0f, 0f, 0f),
        };

    private static readonly Dictionary<uint, string> Regions = new()
    {
        [1] = "La Noscea",
        [2] = "Thanalan",
        [3] = "La Noscea",
        [4] = "Thanalan",
        [5] = "La Noscea",
    };

    private static UnlockPlan Plan(Func<uint, string>? regionOf, byte lastExpansion = 0)
    {
        var catalog = Catalog(
            Blue(A, 15, 101, 1),
            Blue(B, 16, 102, 2),
            Blue(C, 17, 103, 3),
            Blue(D, 18, 104, 4),
            Blue(E, 25, 105, 5) with { Expansion = lastExpansion });
        var tags = UnlockTags.Build(catalog, new HashSet<uint> { A, B, C, D, E }, UniqueRewardCatalog.Empty, PlanDuties.Empty);
        return UnlockPlan.Build(tags, new Dictionary<uint, QuestEvaluation>(), BlockerNames.Default with { Catalog = catalog }, regionOf);
    }

    [Fact]
    public void Without_regions_zones_go_by_their_lowest_level()
    {
        var zones = Assert.Single(Plan(null).Expansions).Zones;
        Assert.Equal([101u, 102u, 103u, 104u, 105u], zones.Select(z => z.TerritoryId));
    }

    [Fact]
    public void Within_a_band_a_regions_zones_follow_each_other()
    {
        var plan = Plan(map => Regions.GetValueOrDefault(map, string.Empty));
        var zones = Assert.Single(plan.Expansions).Zones;

        // Band 10–19: La Noscea (15, 17), then Thanalan (16, 18); band 20–29 after it, whatever its region.
        Assert.Equal([101u, 103u, 102u, 104u, 105u], zones.Select(z => z.TerritoryId));
        Assert.Equal([A, C, B, D, E], plan.Entries.Select(e => e.Quest.RowId));
    }

    [Fact]
    public void A_filtered_plan_keeps_the_region_order()
    {
        // The last quest belongs to the next expansion, so the filter regroups what it keeps.
        var plan = Plan(map => Regions.GetValueOrDefault(map, string.Empty), lastExpansion: 1).Filter(new PlanFilter(UnlockKinds.AllMask, ReadyOnly: false, MaxExpansion: 0));
        Assert.Equal([101u, 103u, 102u, 104u], Assert.Single(plan.Expansions).Zones.Select(z => z.TerritoryId));
    }

    [Fact]
    public void Zones_without_a_region_stand_alone()
    {
        var plan = Plan(map => map == 3 ? "La Noscea" : string.Empty);
        Assert.Equal([101u, 102u, 103u, 104u, 105u], Assert.Single(plan.Expansions).Zones.Select(z => z.TerritoryId));
    }
}
