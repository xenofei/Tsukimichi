using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Todo;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Route;

/// <summary>
/// The followed route (1.6.0, R6 A, C3 C): what the overlay shows of it, its next stop, how it advances and finishes
/// as quests are turned in, its storage in the configuration, and merging consecutive steps at one aetheryte.
/// </summary>
public class ActiveRouteTests
{
    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static Dictionary<uint, QuestEvaluation> States(QuestCatalog catalog, params (uint RowId, QuestState State)[] set)
    {
        var states = catalog.All.ToDictionary(q => q.RowId, _ => Eval(QuestState.Blocked));
        foreach (var (rowId, state) in set)
        {
            states[rowId] = Eval(state);
        }

        return states;
    }

    private static QuestRecord Side(uint rowId, byte level, params uint[] prereqs) =>
        Quest(rowId) with
        {
            Level = level,
            PreviousQuests = new Prereq(prereqs, JoinKind.All),
            Journal = new JournalRef(3, "Side", 60, "Sidequests", 1, "Genre", (int)rowId),
        };

    /// <summary>A chain A (Lv 1) → B (Lv 5) → C (Lv 10) → D (Lv 20) → Target (Lv 30).</summary>
    private static QuestCatalog Chain() => Catalog(
        Side(A, 1),
        Side(B, 5, A),
        Side(C, 10, B),
        Side(D, 20, C),
        Side(Target, 30, D));

    private static UnlockRoute Build(QuestCatalog catalog, Dictionary<uint, QuestEvaluation> states, byte level = 0) =>
        UnlockRoute.Build(RouteTarget.ForQuest(Target, "target"), catalog, states, null, level == 0 ? null : _ => level);

    [Fact]
    public void The_overlay_shows_the_next_three_steps_and_counts_the_rest()
    {
        var catalog = Chain();
        var glance = ActiveRoute.Glance(Build(catalog, States(catalog)));

        Assert.Equal([A, B, C], glance.Next.Select(s => s.RowId));
        Assert.Equal(2, glance.More);
        Assert.Null(glance.Gate);
        Assert.Equal(string.Empty, glance.GateText);
    }

    [Fact]
    public void The_gate_line_names_the_first_shown_step_above_the_characters_level()
    {
        var catalog = Chain();
        var glance = ActiveRoute.Glance(Build(catalog, States(catalog), level: 7));

        Assert.NotNull(glance.Gate);
        Assert.Equal(C, glance.Gate!.RowId);
        Assert.Equal("Level 10 needed from step 3 (you are 7)", glance.GateText);
    }

    [Fact]
    public void Completed_steps_drop_off_so_the_route_advances()
    {
        var catalog = Chain();
        var glance = ActiveRoute.Glance(Build(catalog, States(catalog, (A, QuestState.Completed), (B, QuestState.Completed))));

        Assert.Equal([C, D, Target], glance.Next.Select(s => s.RowId));
        Assert.Equal(0, glance.More);
    }

    [Fact]
    public void The_next_stop_skips_a_quest_already_in_the_journal()
    {
        var catalog = Chain();
        Assert.Equal(A, ActiveRoute.NextStop(Build(catalog, States(catalog, (A, QuestState.Ready))))!.RowId);
        Assert.Equal(B, ActiveRoute.NextStop(Build(catalog, States(catalog, (A, QuestState.Accepted))))!.RowId);
    }

    [Fact]
    public void The_follower_moves_the_stop_on_a_turn_in_not_on_an_accept_and_finishes_once()
    {
        var catalog = Chain();
        var follower = new RouteFollower();

        var start = follower.Update(Build(catalog, States(catalog, (A, QuestState.Ready))));
        Assert.Equal(RouteProgressKind.Started, start.Kind);
        Assert.Equal(A, start.NextStop);

        // Accepting A moves the next stop to B, but nothing was turned in: the flag stays.
        Assert.Equal(RouteProgressKind.Unchanged, follower.Update(Build(catalog, States(catalog, (A, QuestState.Accepted)))).Kind);

        // A turned in: the flag moves to B.
        var advanced = follower.Update(Build(catalog, States(catalog, (A, QuestState.Completed), (B, QuestState.Ready))));
        Assert.Equal(RouteProgressKind.Advanced, advanced.Kind);
        Assert.Equal(B, advanced.NextStop);

        // A new session version with the same states changes nothing.
        Assert.Equal(RouteProgressKind.Unchanged, follower.Update(Build(catalog, States(catalog, (A, QuestState.Completed), (B, QuestState.Ready)))).Kind);

        var done = States(catalog, (A, QuestState.Completed), (B, QuestState.Completed), (C, QuestState.Completed), (D, QuestState.Completed), (Target, QuestState.Completed));
        Assert.Equal(RouteProgressKind.Finished, follower.Update(Build(catalog, done)).Kind);
        Assert.Equal(RouteProgressKind.Unchanged, follower.Update(Build(catalog, done)).Kind);
    }

    [Fact]
    public void A_route_no_quest_leads_to_any_more_is_lost()
    {
        var follower = new RouteFollower();
        var route = UnlockRoute.Build(RouteTarget.ForJob("Gladiator", 0), Chain(), States(Chain()));
        Assert.Equal(RouteProgressKind.Lost, follower.Update(route).Kind);
    }

    [Fact]
    public void Consecutive_steps_at_one_aetheryte_merge_into_one_stop_and_the_order_never_changes()
    {
        var catalog = Chain();
        var route = Build(catalog, States(catalog));

        // A and B near aetheryte 8, C near 9, D near 8 again, the target has none.
        var place = new Dictionary<uint, uint> { [A] = 8, [B] = 8, [C] = 9, [D] = 8 };
        var stops = RouteStops.Group(route.Steps, rowId => place.GetValueOrDefault(rowId));

        Assert.Equal(
            [new RouteStop(0, 2, 8), new RouteStop(2, 1, 9), new RouteStop(3, 1, 8), new RouteStop(4, 1, 0)],
            stops);
        Assert.Equal(route.Steps.Count, stops.Sum(s => s.Count));
    }

    [Fact]
    public void Steps_without_an_aetheryte_are_never_merged()
    {
        var catalog = Chain();
        var stops = RouteStops.Group(Build(catalog, States(catalog)).Steps, _ => 0u);
        Assert.All(stops, s => Assert.Equal(1, s.Count));
    }

    private static TodoInputs Todo(QuestCatalog catalog, Dictionary<uint, QuestEvaluation> states) => new(
        catalog,
        states,
        [],
        new HashSet<uint>(),
        0,
        0,
        new Dictionary<byte, short>(),
        JobLadder.Empty,
        new Dictionary<uint, string>(),
        ShowPins: false,
        ShowNearbyFeature: false,
        ShowMsq: false,
        ShowJobQuests: false,
        ShowSeasonal: false,
        ShowPlan: false);

    [Fact]
    public void The_overlays_route_section_is_titled_by_the_target_with_three_steps_and_the_gate_line()
    {
        var catalog = Chain();
        var states = States(catalog);
        var model = TodoList.Build(Todo(catalog, states) with { Route = Build(catalog, states, level: 7) });

        var section = Assert.Single(model.Sections);
        Assert.Equal(TodoSection.Route, section.Section);
        Assert.Equal("Route: target", section.Title);
        Assert.Equal([A, B, C], section.Rows.Select(r => r.RowId));
        Assert.All(section.Rows, r => Assert.Equal(TodoRowKind.Route, r.Kind));
        Assert.Equal(2, section.More);
        Assert.Equal(["Level 10 needed from step 3 (you are 7)"], section.Notes);

        // Turned off, or nothing left: no section.
        Assert.Empty(TodoList.Build(Todo(catalog, states) with { Route = Build(catalog, states), ShowRoute = false }).Sections);
        var done = States(catalog, (A, QuestState.Completed), (B, QuestState.Completed), (C, QuestState.Completed), (D, QuestState.Completed), (Target, QuestState.Completed));
        Assert.Empty(TodoList.Build(Todo(catalog, done) with { Route = Build(catalog, done) }).Sections);
    }

    [Fact]
    public void The_overlays_next_stops_section_lists_a_row_per_stop_on_its_first_quest()
    {
        var catalog = Chain();
        var stops = new[]
        {
            new Stop(new StopPlace(1, "Camp Dragonhead"), false, [new StopQuest(catalog.ByRowId[B], StopReason.Pin, true), new StopQuest(catalog.ByRowId[A], StopReason.Side, false)]),
            new Stop(new StopPlace(2, "Falcon's Nest"), false, [new StopQuest(catalog.ByRowId[C], StopReason.Side, false)]),
        };

        var model = TodoList.Build(Todo(catalog, States(catalog)) with { Stops = stops, ShowNextStops = true });

        var section = Assert.Single(model.Sections);
        Assert.Equal(TodoSection.NextStops, section.Section);
        Assert.Equal([B, C], section.Rows.Select(r => r.RowId));
        Assert.Equal(["Camp Dragonhead", "Falcon's Nest"], section.Rows.Select(r => r.Name));
        Assert.Equal("2 quests · 1 unlock", section.Rows[0].Hint);
        Assert.Equal("1 quest", section.Rows[1].Hint);

        // Off by default.
        Assert.Empty(TodoList.Build(Todo(catalog, States(catalog)) with { Stops = stops }).Sections);
    }

    [Fact]
    public void A_stored_route_comes_back_as_the_same_target()
    {
        var target = RouteTarget.Union(RouteTargetKind.Pins, "all your pins", [RouteTarget.ForQuest(A, string.Empty), RouteTarget.ForQuest(B, string.Empty)]);
        var saved = SavedRoute.From(target, 42);

        Assert.Equal(42ul, saved.OwnerContentId);
        Assert.True(saved.Matches(target));
        var back = saved.ToTarget();
        Assert.True(saved.Matches(back));
        Assert.Equal([A, B], back.QuestRowIds);
        Assert.Equal(2, back.Parts.Count);
        Assert.False(saved.Matches(RouteTarget.ForQuest(A, "all your pins")));

        var single = SavedRoute.From(RouteTarget.ForJob("Paladin", Target), 7);
        Assert.True(single.Matches(single.ToTarget()));
        Assert.False(single.ToTarget().IsUnion);
    }

    [Fact]
    public void A_stored_route_keeps_its_header_icon()
    {
        var part = RouteTarget.ForQuest(A, "part") with { Icon = 61802 };
        var union = RouteTarget.Union(RouteTargetKind.Blues, "A Realm Reborn blues", [part, RouteTarget.ForQuest(B, string.Empty)]) with { Icon = 61875 };
        var back = SavedRoute.From(union, 1).ToTarget();
        Assert.Equal(61875u, back.Icon);
        Assert.Equal(61802u, back.Parts[0].Icon);
        Assert.Equal(0u, back.Parts[1].Icon);

        var single = SavedRoute.From(RouteTarget.ForJob("Paladin", Target) with { Icon = 62119 }, 7).ToTarget();
        Assert.Equal(62119u, single.Icon);

        // A route stored before 1.15 has no icon: it reads as 0.
        var old = System.Text.Json.JsonSerializer.Deserialize<SavedRoute>("""{"OwnerContentId":7,"Kind":1,"Label":"Paladin","QuestRowIds":[5],"Parts":[{"Kind":0,"Label":"","QuestRowIds":[5]}]}""")!;
        Assert.Equal(0u, old.Icon);
        Assert.Equal(0u, old.Parts[0].Icon);
        Assert.Equal(0u, old.ToTarget().Icon);
    }

    [Fact]
    public void A_stored_route_with_missing_lists_still_reads()
    {
        var saved = new SavedRoute { Kind = RouteTargetKind.Quest, Label = "x", QuestRowIds = null!, Parts = null! };
        var target = saved.ToTarget();
        Assert.Empty(target.QuestRowIds);
        Assert.False(target.IsUnion);
    }
}
