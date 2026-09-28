using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

public class FlightIndexTests
{
    private static FlightZone Zone(uint territory, string name, byte expansion, int quests = 5, int field = 10)
    {
        var questCurrents = new FlightCurrent[quests];
        for (var i = 0; i < quests; i++)
        {
            questCurrents[i] = new FlightCurrent(territory * 100 + (uint)i, territory * 1000 + (uint)i);
        }

        var fieldIds = new uint[field];
        for (var i = 0; i < field; i++)
        {
            fieldIds[i] = territory * 100 + 50 + (uint)i;
        }

        return new FlightZone(territory, name, expansion, questCurrents, fieldIds);
    }

    [Fact]
    public void From_orders_by_expansion_then_name_and_answers_zone_for()
    {
        var index = FlightIndex.From(
        [
            Zone(1188, "Urqopacha", 5),
            Zone(397, "Coerthas Western Highlands", 1, field: 4),
            Zone(1187, "Kozama'uka", 5),
            Zone(398, "The Dravanian Forelands", 1, field: 4),
        ]);

        Assert.Equal(["Coerthas Western Highlands", "The Dravanian Forelands", "Kozama'uka", "Urqopacha"], index.Zones.Select(z => z.Name));
        Assert.Equal(1187u, index.ZoneFor(1187)!.TerritoryId);
        Assert.Null(index.ZoneFor(132));
        Assert.Equal(9, index.Zones[0].TotalCurrents);
        Assert.Equal(4, index.Zones[0].FieldCurrentCount);
        Assert.Equal(15, index.Zones[3].TotalCurrents);
    }

    [Fact]
    public void From_keeps_the_first_zone_per_territory_and_nothing_is_empty()
    {
        var index = FlightIndex.From([Zone(397, "First", 1), Zone(397, "Second", 1)], aetherCompassIcon: 405);
        Assert.Single(index.Zones);
        Assert.Equal("First", index.ZoneFor(397)!.Name);
        Assert.Equal(405u, index.AetherCompassIcon);

        Assert.Same(FlightIndex.Empty, FlightIndex.From([]));
        Assert.Empty(FlightIndex.Empty.Zones);
        Assert.Null(FlightIndex.Empty.ZoneFor(397));
        Assert.Equal(0u, FlightIndex.Empty.AetherCompassIcon);
    }
}

public class FlightIndexGameDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const byte ARealmReborn = 0;
    private const byte Heavensward = 1;
    private const byte Endwalker = 4;
    private const byte Dawntrail = 5;

    [GameDataFact]
    public void Builds_every_flying_zone_from_the_sheets()
    {
        var index = FlightIndex.Build(fixture.Game.Excel, Language.English);
        foreach (var zone in index.Zones)
        {
            output.WriteLine($"{zone.Expansion} {zone.TerritoryId,5} {zone.Name,-32} quests {zone.QuestCurrents.Count} field {zone.FieldCurrentCount}");
        }

        output.WriteLine($"{index.Zones.Count} zones; Aether Compass icon {index.AetherCompassIcon}");

        // 31 rows: Mor Dhona (the ARR flight current), six zones for each of Heavensward, Stormblood, Shadowbringers
        // and Endwalker, six for Dawntrail.
        Assert.Equal(31, index.Zones.Count);
        Assert.Equal(6, index.Zones.Count(z => z.Expansion == Heavensward));
        Assert.Equal(6, index.Zones.Count(z => z.Expansion == Endwalker));

        // A Realm Reborn: flight is opened by The Ultimate Weapon, recorded as one quest current on Mor Dhona (156).
        var arr = Assert.Single(index.Zones, z => z.Expansion == ARealmReborn);
        Assert.Equal(156u, arr.TerritoryId);
        Assert.Equal("Mor Dhona", arr.Name);
        Assert.Equal(0, arr.FieldCurrentCount);
        var ultimateWeapon = Assert.Single(arr.QuestCurrents);
        Assert.Equal("The Ultimate Weapon", fixture.Bundle.Catalog.GetByRowId(ultimateWeapon.QuestRowId)?.Name);

        // Heavensward to Endwalker: five quest currents and, since the 6.0 reduction, four field currents per zone.
        var reduced = index.Zones.Where(z => z.Expansion is >= Heavensward and <= Endwalker).ToList();
        Assert.Equal(24, reduced.Count);
        Assert.All(reduced, z => Assert.Equal(5, z.QuestCurrents.Count));
        Assert.All(reduced.Where(z => z.TerritoryId != 402), z => Assert.Equal(4, z.FieldCurrentCount));
        // Azys Lla (402) is quest-only.
        Assert.Equal(0, index.ZoneFor(402)!.FieldCurrentCount);
        Assert.Contains(reduced, z => z.Name == "Coerthas Western Highlands" && z.TerritoryId == 397);
        Assert.Contains(reduced, z => z.Name == "The Sea of Clouds" && z.TerritoryId == 401);

        // Dawntrail: six zones, each with five quest currents and ten field currents (15 in all).
        var dawntrail = index.Zones.Where(z => z.Expansion == Dawntrail).ToList();
        Assert.Equal(6, dawntrail.Count);
        Assert.All(dawntrail, z => Assert.Equal(5, z.QuestCurrents.Count));
        Assert.All(dawntrail, z => Assert.Equal(10, z.FieldCurrentCount));
        Assert.All(dawntrail, z => Assert.Equal(15, z.TotalCurrents));
        Assert.Contains(dawntrail, z => z.Name == "Urqopacha" && z.TerritoryId == 1187);
        Assert.Contains(dawntrail, z => z.Name == "Living Memory" && z.TerritoryId == 1192);

        // Ordered by expansion then name; every quest current names a catalog quest; no current is listed twice.
        for (var i = 1; i < index.Zones.Count; i++)
        {
            var a = index.Zones[i - 1];
            var b = index.Zones[i];
            Assert.True(a.Expansion < b.Expansion || (a.Expansion == b.Expansion && string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase) <= 0), $"{a.Name} before {b.Name}");
        }

        Assert.All(index.Zones.SelectMany(z => z.QuestCurrents), c => Assert.NotNull(fixture.Bundle.Catalog.GetByRowId(c.QuestRowId)));
        var allIds = index.Zones.SelectMany(z => z.QuestCurrents.Select(c => c.AetherCurrentId).Concat(z.FieldCurrentIds)).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
        Assert.Equal(303, allIds.Count);

        // Territories without flying are absent: New Gridania (132), Middle La Noscea (134), Sastasha (1036).
        Assert.Null(index.ZoneFor(132));
        Assert.Null(index.ZoneFor(134));
        Assert.Null(index.ZoneFor(1036));
    }

    [GameDataFact]
    public void The_aether_compass_is_the_action_row_the_index_names()
    {
        var actions = fixture.Game.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Language.English);
        var compass = actions.GetRow(FlightIndex.AetherCompassAction);
        output.WriteLine($"Action {compass.RowId}: '{compass.Name.ExtractText()}' icon {compass.Icon}");
        Assert.Equal("the Aether Compass", compass.Name.ExtractText());
        Assert.True(compass.Icon > 0);

        // Nothing in GeneralAction is the compass; the Action sheet holds it.
        Assert.DoesNotContain(fixture.Game.Excel.GetSheet<GeneralAction>(Language.English), a => a.Name.ExtractText().Contains("Compass", StringComparison.OrdinalIgnoreCase));

        var index = FlightIndex.Build(fixture.Game.Excel, Language.English);
        Assert.Equal((uint)compass.Icon, index.AetherCompassIcon);
    }
}
