using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Travel;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

public class AetheryteIndexTests
{
    private const uint Gridania = 132;
    private const uint OldGridania = 133;
    private const uint Bentbranch = 148;

    private static AetheryteIndex Index() => AetheryteIndex.From(
        [
            new AetheryteInfo(2, Gridania, "New Gridania", 30f, 30f),
            new AetheryteInfo(3, Bentbranch, "Bentbranch Meadows", 0f, 0f),
            new AetheryteInfo(4, Bentbranch, "The Hawthorne Hut", 300f, -200f),
        ],
        [new KeyValuePair<uint, uint>(OldGridania, 2)]);

    [Fact]
    public void Nearest_picks_the_closest_aetheryte_in_the_territory()
    {
        var index = Index();

        Assert.Equal(4u, index.Nearest(Bentbranch, 250f, -150f)!.RowId);
        Assert.Equal(3u, index.Nearest(Bentbranch, 10f, 10f)!.RowId);
        Assert.Equal(2, index.InTerritory(Bentbranch).Count);
    }

    [Fact]
    public void Nearest_falls_back_to_the_territory_default_and_null_otherwise()
    {
        var index = Index();

        Assert.Equal(2u, index.Nearest(OldGridania, 0f, 0f)!.RowId);
        Assert.Null(index.Nearest(999, 0f, 0f));
        Assert.Empty(index.InTerritory(999));
        Assert.Null(AetheryteIndex.Empty.Nearest(Gridania, 0f, 0f));
    }

    [Fact]
    public void The_giver_aetheryte_is_the_nearest_one_whatever_is_attuned()
    {
        // Stops group by this (Next stops, the route window): it must not move as aetherytes are attuned, so it reads
        // the sheets only. Teleport's target (the nearest attuned) is TravelPlanner.NearestAttuned's.
        var index = Index();
        var giver = Tsukimichi.Tests.Evaluation.Fixture.Quest(1) with { Issuer = new Core.Model.Issuer(1, "NPC", Bentbranch, 1, 250f, 0f, -150f) };

        Assert.Equal(4u, index.NearestToGiver(giver)!.RowId);
        Assert.Null(TravelPlanner.NearestAttuned(index.NodesInTerritory(Bentbranch), null, 250f, -150f, _ => false));
        Assert.Equal(3u, TravelPlanner.NearestAttuned(index.NodesInTerritory(Bentbranch), null, 250f, -150f, id => id == 3)!.Value.RowId);

        // A city sub-zone without its own aetheryte groups under its city's; no giver place, no aetheryte.
        Assert.Equal(2u, index.NearestToGiver(giver with { Issuer = giver.Issuer! with { TerritoryId = OldGridania } })!.RowId);
        Assert.Null(index.NearestToGiver(giver with { Issuer = null }));
        Assert.Null(index.NearestToGiver(giver with { Issuer = giver.Issuer! with { TerritoryId = 0 } }));
    }

    [Fact]
    public void ToRaw_inverts_the_map_coordinate_scale()
    {
        // Pixel 1024 is the raw origin on an unscaled, unoffset map; a 200 % map halves the raw span.
        Assert.Equal(0f, AetheryteIndex.ToRaw(1024f, 0, 100));
        Assert.Equal(100f, AetheryteIndex.ToRaw(1124f, 0, 100));
        Assert.Equal(50f, AetheryteIndex.ToRaw(1124f, 0, 200));
        Assert.Equal(-30f, AetheryteIndex.ToRaw(1024f, 30, 100));
        Assert.Equal(100f, AetheryteIndex.ToRaw(1124f, 0, 0));
    }

    [Fact]
    public void Prefer_page_picks_the_own_map_page_regardless_of_sheet_order()
    {
        (uint Page, short X, short Y)[] markers = [(299, 10, 20), (276, 30, 40), (300, 50, 60)];
        Assert.Equal((276u, (short)30, (short)40), AetheryteIndex.PreferPage(markers, 276));
        Assert.Equal((300u, (short)50, (short)60), AetheryteIndex.PreferPage(markers, 300));

        // No marker on the preferred page (or no own map at all): the first page serves.
        Assert.Equal((299u, (short)10, (short)20), AetheryteIndex.PreferPage(markers, 1));
        Assert.Equal((299u, (short)10, (short)20), AetheryteIndex.PreferPage(markers, 0));
        Assert.Throws<ArgumentException>(() => AetheryteIndex.PreferPage([], 276));
    }

    [Fact]
    public void From_with_nothing_is_empty()
    {
        Assert.Same(AetheryteIndex.Empty, AetheryteIndex.From([]));
        Assert.Empty(AetheryteIndex.Empty.All);
        Assert.Empty(AetheryteIndex.Empty.Shards);
        Assert.Empty(AetheryteIndex.Empty.ShardNodesInGroup(2));
        Assert.Null(AetheryteIndex.Empty.GroupAetheryte(2));
    }

    [Fact]
    public void Shards_are_kept_by_network_beside_the_aetherytes()
    {
        var index = AetheryteIndex.From(
            [
                new AetheryteInfo(2, Gridania, "New Gridania", 35f, 28f, Group: 2),
                new AetheryteInfo(3, Bentbranch, "Bentbranch Meadows", 0f, 0f),
            ],
            [new KeyValuePair<uint, uint>(OldGridania, 2)],
            [
                new AetheryteInfo(25, Gridania, "Archers' Guild", 166f, 88f, Group: 2),
                new AetheryteInfo(28, OldGridania, "Conjurers' Guild", -145f, -12f, Group: 2),
                // A row without a network and a row reusing an aetheryte's id are not shards.
                new AetheryteInfo(99, OldGridania, "Nowhere", 0f, 0f),
                new AetheryteInfo(3, OldGridania, "Duplicate", 0f, 0f, Group: 2),
            ]);

        Assert.Equal([25u, 28u], index.Shards.Select(s => s.RowId));
        Assert.Equal([25u, 28u], index.ShardsInGroup(2).Select(s => s.RowId));
        Assert.Equal([25u, 28u], index.ShardNodesInGroup(2).Select(s => s.RowId));
        Assert.Empty(index.ShardsInGroup(0));
        Assert.Empty(index.ShardsInGroup(7));

        Assert.Equal("Conjurers' Guild", index.Find(28)?.Name);
        Assert.Equal("Bentbranch Meadows", index.Find(3)?.Name);
        Assert.Null(index.Find(99));
        Assert.Equal(2u, index.GroupAetheryte(2)?.RowId);
        Assert.Null(index.GroupAetheryte(0));
        Assert.Equal(2u, index.TerritoryDefault(OldGridania)?.RowId);
        Assert.Null(index.TerritoryDefault(Bentbranch));

        // Shards never become teleport destinations.
        Assert.Empty(index.InTerritory(OldGridania));
        Assert.Equal(new TravelNode(2, Gridania, 35f, 28f, 2), Assert.Single(index.NodesInTerritory(Gridania)));
    }
}

public class AetheryteIndexGameDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Builds_from_the_sheets_and_finds_the_giver_of_close_to_home()
    {
        var index = AetheryteIndex.Build(fixture.Game.Excel, Lumina.Data.Language.English);
        output.WriteLine($"{index.All.Count} teleportable aetherytes");
        Assert.InRange(index.All.Count, 90, 400);
        Assert.All(index.All, a => Assert.False(string.IsNullOrWhiteSpace(a.Name), $"aetheryte {a.RowId} has no name"));

        // Close to Home starts in New Gridania (territory 132) a stone's throw from the aetheryte plaza (row 2).
        var quest = fixture.Bundle.Catalog.GetByRowId(65621u);
        Assert.NotNull(quest?.Issuer);
        var nearest = index.Nearest(quest.Issuer.TerritoryId, quest.Issuer.X, quest.Issuer.Z);
        Assert.NotNull(nearest);
        output.WriteLine($"Close to Home: territory {quest.Issuer.TerritoryId} -> aetheryte {nearest.RowId} {nearest.Name}");
        Assert.Equal(2u, nearest.RowId);
        Assert.InRange(MathF.Sqrt((nearest.X - quest.Issuer.X) * (nearest.X - quest.Issuer.X) + (nearest.Z - quest.Issuer.Z) * (nearest.Z - quest.Issuer.Z)), 0f, 150f);

        // Old Gridania (133) holds no aetheryte of its own; the TerritoryType row points at Gridania's.
        Assert.Empty(index.InTerritory(133));
        Assert.Equal(2u, index.Nearest(133, 0f, 0f)!.RowId);
    }

    [GameDataFact]
    public void City_shards_are_placed_in_their_own_city()
    {
        var index = AetheryteIndex.Build(fixture.Game.Excel, Language.English);
        output.WriteLine($"{index.Shards.Count} aethernet shards");
        Assert.InRange(index.Shards.Count, 60, 250);
        Assert.All(index.Shards, s =>
        {
            Assert.NotEqual(0u, s.Group);
            Assert.False(string.IsNullOrWhiteSpace(s.Name), $"shard {s.RowId} has no name");
            Assert.NotNull(index.GroupAetheryte(s.Group));
        });

        // Every shard in its city aetheryte's own territory stands within the city, not across the map.
        foreach (var shard in index.Shards)
        {
            var city = index.GroupAetheryte(shard.Group)!;
            if (city.TerritoryId == shard.TerritoryId)
            {
                Assert.InRange(TravelPlanner.Distance(shard.X, shard.Z, city.X, city.Z), 0f, 700f);
            }
        }

        // Gridania: the Conjurers' Guild shard (28) stands in Old Gridania (133) in network 2, whose aetheryte is
        // New Gridania's (2). A giver beside it is reached by teleport to New Gridania and a hop there.
        var conjurers = index.Find(28);
        Assert.NotNull(conjurers);
        Assert.Equal(133u, conjurers.TerritoryId);
        Assert.Equal(2u, conjurers.Group);
        var gridania = index.GroupAetheryte(2);
        Assert.Equal(2u, gridania?.RowId);
        Assert.Equal(2u, index.Nearest(133, conjurers.X, conjurers.Z)?.RowId);
        var hop = TravelPlanner.ChooseShard(gridania!.Node, index.ShardNodesInGroup(2), 133, conjurers.X + 5f, conjurers.Z + 5f, _ => true);
        Assert.Equal(28u, hop?.RowId);

        // Kugane's shards (network 7) are in Kugane (628).
        Assert.Contains(index.ShardsInGroup(7), s => s.TerritoryId == 628);
    }

    [GameDataFact]
    public void Multi_map_city_aetheryte_takes_the_marker_from_its_own_map_page()
    {
        var excel = fixture.Game.Excel;
        var index = AetheryteIndex.Build(excel, Language.English);

        // Kugane (aetheryte 111) stands in territory 628 and carries a type-3 marker on its own map page (Map 370,
        // range 280) and again on the page of the instanced Kugane map (Map 411, range 300, territory 665). The
        // index must resolve it to its own territory with the own page's marker converted by the own map's row.
        var row = excel.GetSheet<Aetheryte>(Language.English).GetRow(111u);
        Assert.True(row.IsAetheryte);
        Assert.Equal(628u, row.Territory.RowId);
        var map = row.Map.Value;

        var pages = new List<uint>();
        (short X, short Y)? own = null;
        foreach (var page in excel.GetSubrowSheet<MapMarker>(Language.English))
        {
            foreach (var marker in page)
            {
                if (marker.DataType == AetheryteIndex.AetheryteMarkerType && marker.DataKey.RowId == 111u)
                {
                    pages.Add(page.RowId);
                    if (page.RowId == map.MapMarkerRange)
                    {
                        own = (marker.X, marker.Y);
                    }
                }
            }
        }

        output.WriteLine($"Kugane: map {row.Map.RowId} (marker range {map.MapMarkerRange}), marker pages [{string.Join(", ", pages)}]");
        Assert.NotNull(own);
        Assert.Contains(pages, page => page != map.MapMarkerRange);

        var info = Assert.Single(index.InTerritory(628), a => a.RowId == 111u);
        Assert.Equal("Kugane", info.Name);
        Assert.Equal(AetheryteIndex.ToRaw(own.Value.X, map.OffsetX, map.SizeFactor), info.X);
        Assert.Equal(AetheryteIndex.ToRaw(own.Value.Y, map.OffsetY, map.SizeFactor), info.Z);
        Assert.DoesNotContain(index.InTerritory(665), a => a.RowId == 111u);
    }
}
