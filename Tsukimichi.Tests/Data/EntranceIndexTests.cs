using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Travel;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Interiors and their doors against the game data (travel review, 1.10): which aetheryte Teleport and Go to giver
/// pick for a giver inside a building or a story area, and an audit of every quest giver's choice.
/// </summary>
public class EntranceIndexGameDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const uint ReturnFromTheVoid = 70134;
    private const uint ZerosDomain = 1077;
    private const uint Thavnair = 957;
    private const uint Yedlihmad = 169;
    private const uint TheGreatWork = 170;

    private AetheryteIndex? index;
    private EntranceIndex? entrances;

    private AetheryteIndex Index => index ??= AetheryteIndex.Build(fixture.Game.Excel, Language.English);

    private EntranceIndex Entrances => entrances ??= EntranceIndex.Create(fixture.Game.Excel, path => fixture.Game.GetFile<LgbFile>(path), Index, Language.English);

    private ArrivalChoice Arrive(Issuer issuer, Func<uint, bool> isAttuned, uint playerTerritory = 0) =>
        GiverTravel.Arrival(Index, issuer.TerritoryId, GiverTravel.Goal(issuer, Entrances, playerTerritory), isAttuned);

    [GameDataFact]
    public void Return_from_the_void_goes_to_yedlihmad_and_the_way_into_the_void()
    {
        // Varshahn stands in Zero's Domain, a story area of the Void with no aetheryte, no shard and no door of its own:
        // it is reached from Thavnair, through the NPC at the ruins who takes you to Weaver's Warding (Alzadaal's
        // Legacy), and on through the Fell Court of Troia.
        var quest = fixture.Bundle.Catalog.GetByRowId(ReturnFromTheVoid);
        Assert.NotNull(quest?.Issuer);
        var issuer = quest.Issuer;
        Assert.Equal(ZerosDomain, issuer.TerritoryId);
        Assert.Empty(Index.InTerritory(ZerosDomain));
        Assert.True(Entrances.IsInterior(ZerosDomain));

        var door = Entrances.For(ZerosDomain);
        Assert.NotNull(door);
        output.WriteLine($"Zero's Domain: way in from territory {door.Value.TerritoryId} at ({door.Value.X:F1}, {door.Value.Z:F1}), placed {door.Value.Placed}");
        Assert.Equal(Thavnair, door.Value.TerritoryId);
        Assert.True(door.Value.Placed);
        Assert.InRange(TravelPlanner.Distance(door.Value.X, door.Value.Z, -270.3f, 605.8f), 0f, 5f);

        // From anywhere outside, the goal is that NPC, and the aetheryte nearest it is Yedlihmad.
        var goal = GiverTravel.Goal(issuer, Entrances, 0);
        Assert.True(goal.AtEntrance);
        var arrival = Arrive(issuer, _ => true);
        Assert.Equal(Yedlihmad, arrival.Nearest?.RowId);
        Assert.Equal(Yedlihmad, arrival.Target?.RowId);
        Assert.False(arrival.Substituted);

        // Without Yedlihmad attuned, the next nearest attuned one in Thavnair, and the choice says so.
        var substitute = Arrive(issuer, id => id != Yedlihmad);
        Assert.Equal(TheGreatWork, substitute.Target?.RowId);
        Assert.True(substitute.Substituted);

        // Inside Zero's Domain already, the goal is Varshahn himself.
        Assert.False(GiverTravel.Goal(issuer, Entrances, ZerosDomain).AtEntrance);
    }

    [GameDataFact]
    public void The_travel_actions_are_the_rows_they_say()
    {
        var actions = fixture.Game.Excel.GetSheet<GeneralAction>(Language.English);
        Assert.Equal("Sprint", actions.GetRow(TravelActions.Sprint).Name.ExtractText());
        Assert.Equal("Mount Roulette", actions.GetRow(TravelActions.MountRoulette).Name.ExtractText());
        Assert.Equal("Dismount", actions.GetRow(TravelActions.Dismount).Name.ExtractText());
    }

    [GameDataFact]
    public void Common_interiors_lead_to_their_doors()
    {
        // The Waking Sands: its door in Western Thanalan (an object whose event is an array holding the warp), Horizon.
        AssertDoor(212, 140, -480.9f, -386.9f, 17);

        // The Rising Stones: the door beside the Seventh Heaven in Mor Dhona, Revenant's Toll.
        AssertDoor(351, 156, 21.1f, -631.3f, 24);

        // Fortemps Manor: the Pillars hold no aetheryte; Foundation's, the zone's own, is the fallback.
        var fortemps = AssertDoor(433, 419, 14.3f, -7.5f, null);
        var arrival = GiverTravel.Arrival(Index, 433, new TravelGoal(new TravelPlace(419, fortemps.X, fortemps.Y, fortemps.Z), true, fortemps), _ => true);
        Assert.Equal(70u, arrival.Target?.RowId);

        // Kienkan's door is in the Doman Enclave; its TerritoryType row names Namai in Yanxia, another zone.
        var kienkan = Entrances.For(744);
        Assert.Equal(759u, kienkan?.TerritoryId);
        var enclave = GiverTravel.Arrival(Index, 744, new TravelGoal(new TravelPlace(759, 0f, 0f, 0f), false, kienkan), _ => true);
        Assert.Equal(759u, Index.Find(enclave.Target!.Value.RowId)!.TerritoryId);

        // An open field with no aetheryte of its own: the Dravanian Hinterlands, over Idyllshire's zone line.
        var hinterlands = Entrances.For(399);
        Assert.Equal(478u, hinterlands?.TerritoryId);
        Assert.True(hinterlands?.Placed);

        // Cities, sub-zones with shards and the special zones are no interiors.
        Assert.False(Entrances.IsInterior(132));
        Assert.False(Entrances.IsInterior(133));
        Assert.False(Entrances.IsInterior(419));
        Assert.False(Entrances.IsInterior(TravelSpecials.FirmamentTerritory));
        Assert.Null(Entrances.For(132));
    }

    private InteriorEntrance AssertDoor(uint interior, uint outside, float x, float z, uint? aetheryte)
    {
        var door = Entrances.For(interior);
        Assert.NotNull(door);
        output.WriteLine($"{interior}: door in {door.Value.TerritoryId} at ({door.Value.X:F1}, {door.Value.Z:F1}), placed {door.Value.Placed}");
        Assert.Equal(outside, door.Value.TerritoryId);
        Assert.True(door.Value.Placed);
        Assert.InRange(TravelPlanner.Distance(door.Value.X, door.Value.Z, x, z), 0f, 10f);
        if (aetheryte is { } expected)
        {
            var goal = new TravelGoal(new TravelPlace(outside, door.Value.X, door.Value.Y, door.Value.Z), true, door);
            Assert.Equal(expected, GiverTravel.Arrival(Index, interior, goal, _ => true).Target?.RowId);
        }

        return door.Value;
    }

    /// <summary>Zones no teleport or door reaches: instanced field operations, and the Occult Crescent's second zone.</summary>
    private static readonly HashSet<uint> NoWayIn = [920, 975, 1346];

    [GameDataFact]
    public void Every_giver_gets_an_aetheryte_in_its_own_region_and_within_reach()
    {
        var territories = fixture.Game.Excel.GetSheet<TerritoryType>(Language.English);
        string Region(uint territory) => territories.GetRowOrDefault(territory)?.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? string.Empty;

        var problems = new List<string>();
        int givers = 0, msq = 0, viaDoor = 0, walkable = 0;
        foreach (var quest in fixture.Bundle.Catalog.All)
        {
            if (quest.Issuer is not { TerritoryId: > 0 } issuer || TravelSpecials.Classify(issuer.TerritoryId) != TravelSpecial.None)
            {
                continue;
            }

            givers++;
            var isMsq = FeaturePresets.IsMainScenario(quest);
            msq += isMsq ? 1 : 0;
            var goal = GiverTravel.Goal(issuer, Entrances, 0);
            viaDoor += goal.AtEntrance ? 1 : 0;
            walkable += goal.Placed ? 1 : 0;
            var arrival = Arrive(issuer, _ => true);
            if (arrival.Target is not { } target || Index.Find(target.RowId) is not { } aetheryte)
            {
                if (!NoWayIn.Contains(issuer.TerritoryId))
                {
                    problems.Add($"{quest.RowId} {quest.Name}: no aetheryte for territory {issuer.TerritoryId}");
                }

                continue;
            }

            // A target in another territory than the goal (a city sub-zone's city) must stay in the region.
            var goalRegion = Region(goal.Place.TerritoryId);
            var targetRegion = Region(aetheryte.TerritoryId);
            if (aetheryte.TerritoryId != goal.Place.TerritoryId && goalRegion.Length > 0 && goalRegion != "???" && goalRegion != targetRegion)
            {
                problems.Add($"{quest.RowId} {quest.Name}: {aetheryte.Name} ({targetRegion}) for a goal in {goalRegion}");
            }

            // The farthest giver from its zone's only aetheryte is in Azys Lla, about 1,740 from Helix.
            if (goal.Placed && aetheryte.TerritoryId == goal.Place.TerritoryId)
            {
                var distance = TravelPlanner.Distance(aetheryte.X, aetheryte.Z, goal.Place.X, goal.Place.Z);
                if (distance > 1800f)
                {
                    problems.Add($"{quest.RowId} {quest.Name}: {aetheryte.Name} is {distance:F0} from the {(goal.AtEntrance ? "door" : "giver")}{(isMsq ? " (MSQ)" : string.Empty)}");
                }
            }
        }

        output.WriteLine($"{givers} givers ({msq} MSQ), {viaDoor} behind a door, {walkable} with a place to walk to");
        foreach (var problem in problems)
        {
            output.WriteLine(problem);
        }

        Assert.Empty(problems);
    }
}
