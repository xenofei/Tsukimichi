using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Route;

/// <summary>
/// Next stops (1.6.0, R6 B): the Ready set (route stop, pins, pinned expansion's blues, other Ready quests near the
/// character's level) grouped by the aetheryte nearest each giver, the current zone first, then the stop with more
/// quests (unlock quests weighted), then the lower level.
/// </summary>
public class StopPlannerTests
{
    private const uint Here = 132;
    private const uint Elsewhere = 140;

    private static readonly StopPlace Camp = new(1, "Camp Dragonhead");
    private static readonly StopPlace Falcon = new(2, "Falcon's Nest");
    private static readonly StopPlace Gridania = new(3, "New Gridania");

    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static QuestRecord Side(uint rowId, byte level, uint territory = Elsewhere) =>
        Quest(rowId) with
        {
            Level = level,
            Journal = new JournalRef(3, "Side", 60, "Sidequests", 1, "Genre", (int)rowId),
            Issuer = new Issuer(1, "Giver", territory, 1, 0f, 0f, 0f),
        };

    private static Dictionary<uint, QuestEvaluation> Ready(QuestCatalog catalog, params uint[] ready) =>
        catalog.All.ToDictionary(q => q.RowId, q => Eval(ready.Contains(q.RowId) ? QuestState.Ready : QuestState.Blocked));

    private static StopInputs Inputs(
        QuestCatalog catalog,
        Dictionary<uint, QuestEvaluation> states,
        Dictionary<uint, StopPlace> places,
        uint[]? pins = null,
        uint? routeStop = null,
        uint[]? blues = null,
        uint[]? unlocks = null,
        short level = 0,
        uint territory = 0) =>
        new(catalog, states, q => places.GetValueOrDefault(q.RowId), pins ?? [], routeStop, blues ?? [], new HashSet<uint>(unlocks ?? []), level, StopPlanner.DefaultLevelRange, territory);

    [Fact]
    public void Ready_quests_sharing_an_aetheryte_are_one_stop()
    {
        var catalog = Catalog(Side(A, 50), Side(B, 50), Side(C, 50));
        var places = new Dictionary<uint, StopPlace> { [A] = Camp, [B] = Camp, [C] = Falcon };

        var stops = StopPlanner.Plan(Inputs(catalog, Ready(catalog, A, B, C), places, pins: [A, B, C]));

        Assert.Equal(2, stops.Count);
        Assert.Equal(Camp, stops[0].Place);
        Assert.Equal([A, B], stops[0].Quests.Select(q => q.Quest.RowId));
        Assert.Equal("2 quests", stops[0].CountText);
        Assert.Equal(Falcon, stops[1].Place);
    }

    [Fact]
    public void Only_Ready_quests_count_and_a_quest_with_nowhere_to_teleport_is_left_out()
    {
        var catalog = Catalog(Side(A, 50), Side(B, 50), Side(C, 50));
        var places = new Dictionary<uint, StopPlace> { [A] = Camp, [B] = Camp };

        var stops = StopPlanner.Plan(Inputs(catalog, Ready(catalog, A, C), places, pins: [A, B, C]));

        var stop = Assert.Single(stops);
        Assert.Equal([A], stop.Quests.Select(q => q.Quest.RowId));
    }

    [Fact]
    public void The_current_zone_comes_first_then_more_quests_with_unlocks_weighted_then_lower_level()
    {
        // Falcon: three quests. Camp: two, one an unlock (scores 3), lower level. Gridania: one, but in this zone.
        var catalog = Catalog(
            Side(A, 50), Side(B, 52),
            Side(C, 55), Side(D, 55), Side(E, 55),
            Side(Target, 58, territory: Here));
        var places = new Dictionary<uint, StopPlace> { [A] = Camp, [B] = Camp, [C] = Falcon, [D] = Falcon, [E] = Falcon, [Target] = Gridania };
        var all = new[] { A, B, C, D, E, Target };

        var stops = StopPlanner.Plan(Inputs(catalog, Ready(catalog, all), places, pins: all, unlocks: [B], territory: Here));

        Assert.Equal([Gridania, Camp, Falcon], stops.Select(s => s.Place));
        Assert.True(stops[0].IsHere);
        Assert.Equal(3, stops[1].Score);
        Assert.Equal("2 quests · 1 unlock", stops[1].CountText);

        // Without a zone, Camp and Falcon tie on score and Camp's lower level wins.
        var anywhere = StopPlanner.Plan(Inputs(catalog, Ready(catalog, all), places, pins: all, unlocks: [B]));
        Assert.Equal([Camp, Falcon, Gridania], anywhere.Select(s => s.Place));
    }

    [Fact]
    public void Other_Ready_quests_join_only_near_the_characters_level_and_never_main_scenario_ones()
    {
        var msq = Quest(D) with { Level = 50, Issuer = new Issuer(1, "Giver", Elsewhere, 1, 0f, 0f, 0f) };
        var catalog = Catalog(Side(A, 50), Side(B, 30), msq);
        var places = new Dictionary<uint, StopPlace> { [A] = Camp, [B] = Camp, [D] = Camp };

        var stops = StopPlanner.Plan(Inputs(catalog, Ready(catalog, A, B, D), places, level: 52));

        var stop = Assert.Single(stops);
        var quest = Assert.Single(stop.Quests);
        Assert.Equal(A, quest.Quest.RowId);
        Assert.Equal(StopReason.Side, quest.Reason);
    }

    [Fact]
    public void A_quest_from_several_sources_keeps_the_strongest_reason_and_leads_its_stop()
    {
        var catalog = Catalog(Side(A, 40), Side(B, 50), Side(C, 45));
        var places = new Dictionary<uint, StopPlace> { [A] = Camp, [B] = Camp, [C] = Camp };

        var stops = StopPlanner.Plan(Inputs(catalog, Ready(catalog, A, B, C), places, pins: [A, B], routeStop: B, blues: [C], level: 50, unlocks: [C]));

        var stop = Assert.Single(stops);
        Assert.Equal([B, A, C], stop.Quests.Select(q => q.Quest.RowId));
        Assert.Equal([StopReason.Route, StopReason.Pin, StopReason.Blue], stop.Quests.Select(q => q.Reason));
    }
}
