using Lumina.Data;
using Lumina.Data.Files;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Find by unlock and Route to unlock (plan v7, 1.19.0 K3) over the installed game: the search finds Kugane, the
/// Sirensong Sea and flying in Thavnair; flying in Thavnair routes through every quest current of the zone; the game's
/// layouts place every one of its field currents near an aetheryte; and a quest the shield masks is never found by what
/// it opens.
/// </summary>
public sealed class FindByUnlockGameDataTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint NotWithoutIncident = 68005;
    private const uint Thavnair = 957;

    private QuestUnlocks Index => fixture.Unlocks;

    [GameDataFact]
    public void The_search_finds_a_city_a_dungeon_and_flying_in_a_zone()
    {
        var kugane = Assert.Single(Index.Find("kugane"), m => m.Find.Target == UnlockTarget.Zone);
        Assert.Contains(NotWithoutIncident, kugane.Find.Quests);
        Assert.Equal(UnlockFindKind.Area, kugane.Find.Kind);

        Assert.Contains(Index.Find("sirensong"), m => m.Find.Kind == UnlockFindKind.Duty && m.Find.Label == "The Sirensong Sea");

        var flying = Assert.Single(Index.Find(SearchIndex.Normalize("flying thavnair")));
        Assert.Equal("Flying in Thavnair", flying.Find.Label);
        Assert.True(flying.Find.NeedsAll);
        output.WriteLine($"Flying in Thavnair: {string.Join(", ", flying.Find.Quests)}");
    }

    [GameDataFact]
    public void Flying_in_thavnair_routes_through_every_quest_current_of_the_zone()
    {
        var flight = FlightIndex.Build(fixture.Game.Excel, Language.English);
        var find = Index.Finds.Single(f => f.Target == UnlockTarget.Flying && f.Name == "Thavnair");

        var zone = flight.ZoneOfQuests(find.Quests);
        Assert.NotNull(zone);
        Assert.Equal(Thavnair, zone.TerritoryId);
        Assert.Equal(zone.QuestCurrents.Select(c => c.QuestRowId).Order().ToArray(), find.Quests.Order().ToArray());

        var route = UnlockRoute.Build(RouteTarget.ForUnlock(find, zone.TerritoryId), fixture.Catalog, new Dictionary<uint, QuestEvaluation>());
        Assert.Equal(RouteOutcome.Route, route.Outcome);
        foreach (var quest in find.Quests)
        {
            Assert.Contains(route.Steps, s => s.RowId == quest && s.IsTarget);
        }
    }

    [GameDataFact]
    public void The_layouts_place_every_field_current_of_thavnair_near_an_aetheryte()
    {
        var flight = FlightIndex.Build(fixture.Game.Excel, Language.English);
        var aetherytes = AetheryteIndex.Build(fixture.Game.Excel, Language.English);
        var zone = flight.ZoneFor(Thavnair)!;

        var places = AetherCurrentPlaces.Read(fixture.Game.Excel, path => fixture.Game.GetFile<LgbFile>(path), zone, Language.English);

        Assert.Equal(zone.FieldCurrentIds.ToArray(), places.Select(p => p.AetherCurrentId).ToArray());
        foreach (var place in places)
        {
            var nearest = aetherytes.Nearest(place.TerritoryId, place.X, place.Z);
            Assert.NotNull(nearest);
            output.WriteLine($"{place.AetherCurrentId}: {nearest.Name} ({place.X:0}, {place.Z:0})");
        }
    }

    [GameDataFact]
    public void A_masked_quest_is_never_found_by_what_it_opens()
    {
        // A character at the very start of the main scenario: Stormblood's quests are masked.
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in fixture.Catalog.All)
        {
            states[quest.RowId] = QuestState.Blocked;
        }

        var mask = SpoilerMask.Build(fixture.Catalog, states, SpoilerOptions.Default);
        Assert.True(mask.IsMasked(NotWithoutIncident));

        var search = SearchIndex.For(fixture.Catalog);
        Assert.True(search.Matches(NotWithoutIncident, "kugane", null, Index));
        Assert.False(search.Matches(NotWithoutIncident, "kugane", mask, Index));
        Assert.DoesNotContain(Index.Find("kugane", rowId => !mask.IsMasked(rowId)), m => m.Via == NotWithoutIncident);
    }
}
