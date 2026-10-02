using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Todo;
using Tsukimichi.Core.Ui;
using static Tsukimichi.Tests.Query.MsqBranchFixture;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// What the consumers of the main scenario position make of a branched one (P14), over the synthetic Evercold
/// fixture: the status bar and dashboard wording, the Todo overlay and Tonight rows, the IPC gates and the unlock
/// route's per-route order. A linear position is covered by each consumer's own tests, unchanged.
/// </summary>
public class MsqBranchConsumerTests
{
    private static readonly QuestCatalog Catalog = Build();

    private static string Name(QuestRecord quest) => quest.Name;

    private static MsqPosition Position(params uint[] completed) => MsqProgress.Compute(Catalog, Evaluate(Catalog, completed))!;

    // Wording (MsqText): the pill, the dashboard, the tooltip.

    [Fact]
    public void Pill_reads_each_route_with_its_short_progress()
    {
        var position = Position(Done([L1, L2], [A1, A2], RouteC));

        Assert.Equal("route The Ember Road 2/3 · route The Glacier Road — · route The Aurora Road done", MsqText.Compact(position, Name));
    }

    [Fact]
    public void Dashboard_spells_each_routes_progress_out()
    {
        var position = Position(Done([L1, L2], [A1, A2], RouteC));

        Assert.Equal("route The Ember Road 2 of 3 · route The Glacier Road not started · route The Aurora Road done", MsqText.Spelled(position, Name));
    }

    [Fact]
    public void Tooltip_lists_every_route_with_its_next_quest_and_when_they_meet()
    {
        var position = Position(Done([L1, L2], [A1, A2], RouteC));

        Assert.Equal(
            [
                "route The Ember Road: 2 of 3 · next: A Hearth Relit (Ready)",
                "route The Glacier Road: not started · next: The Glacier Road (Ready)",
                "route The Aurora Road: done",
            ],
            MsqText.Lines(position, Name));
        Assert.Equal("The routes meet again at Where the Roads Meet once all are done (2 routes to go)", MsqText.JoinLine(position, Name));

        var any = MsqProgress.Compute(Build(JoinKind.Any), Evaluate(Build(JoinKind.Any), L1, L2))!;
        Assert.Equal("The routes meet again at Where the Roads Meet once one is done (1 route to go)", MsqText.JoinLine(any, Name));
    }

    [Fact]
    public void Route_names_go_through_the_callers_name_lookup()
    {
        var position = Position(L1, L2);
        var mask = SpoilerMask.Build(Catalog, States(Catalog, L1, L2), SpoilerOptions.Default with { Ahead = 0 });

        // Every route's first quest is a position, so the shield never hides a route's name; the join's name is hidden.
        Assert.Equal("route The Ember Road — · route The Glacier Road — · route The Aurora Road —", MsqText.Compact(position, mask.DisplayName));
        Assert.StartsWith("The routes meet again at Main scenario quest (Lv 41)", MsqText.JoinLine(position, mask.DisplayName));
    }

    [Fact]
    public void A_linear_position_has_no_route_wording()
    {
        var position = Position(L1);

        Assert.Equal(string.Empty, MsqText.Compact(position, Name));
        Assert.Equal(string.Empty, MsqText.JoinLine(position, Name));
        Assert.Empty(MsqText.Lines(position, Name));
    }

    // The Todo overlay and the Tonight card (one TodoList model).

    private static TodoSectionModel? MsqSection(params uint[] completed) => MsqSection(Catalog, Evaluate(Catalog, completed));

    private static TodoSectionModel? MsqSection(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> evaluations)
    {
        var inputs = new TodoInputs(
            catalog,
            evaluations,
            Array.Empty<uint>(),
            new HashSet<uint>(),
            0,
            Evaluation.Fixture.Gladiator,
            new Dictionary<byte, short> { [Evaluation.Fixture.Gladiator] = 50 },
            JobLadder.Empty,
            new Dictionary<uint, string>(),
            ShowPins: false,
            ShowNearbyFeature: false,
            ShowMsq: true,
            ShowJobQuests: false);
        return TodoList.Build(inputs).Sections.SingleOrDefault(s => s.Section == TodoSection.Msq);
    }

    [Fact]
    public void Todo_lists_one_main_scenario_row_per_open_route()
    {
        var section = MsqSection(Done([L1, L2], [A1], RouteC))!;

        Assert.Equal([A2, B1], section.Rows.Select(r => r.RowId));
        Assert.All(section.Rows, r => Assert.Equal(TodoRowKind.Msq, r.Kind));
        Assert.StartsWith("route The Ember Road · 1 of 3 · ", section.Rows[0].Hint);
        Assert.StartsWith("route The Glacier Road · not started · ", section.Rows[1].Hint);
    }

    [Fact]
    public void Todo_lists_the_single_next_quest_outside_a_region()
    {
        Assert.Equal([L2], MsqSection(L1)!.Rows.Select(r => r.RowId));
        Assert.Equal([J], MsqSection(Done([L1, L2], RouteA, RouteB, RouteC))!.Rows.Select(r => r.RowId));
        Assert.DoesNotContain("route", MsqSection(L1)!.Rows[0].Hint, StringComparison.Ordinal);
    }

    // IPC.

    [Fact]
    public void Ipc_position_is_the_first_routes_next_quest_and_positions_list_every_route()
    {
        var inside = new IpcView(Catalog, Evaluate(Catalog, Done([L1, L2], RouteA, [B1])), BlockerNames.Default);
        Assert.Equal(B2, inside.MsqNext());
        Assert.Equal([B2, C1], inside.MsqPositions());

        var linear = new IpcView(Catalog, Evaluate(Catalog, L1), BlockerNames.Default);
        Assert.Equal(L2, linear.MsqNext());
        Assert.Equal([L2], linear.MsqPositions());

        var done = new IpcView(Catalog, Evaluate(Catalog, Story), BlockerNames.Default);
        Assert.Equal(0u, done.MsqNext());
        Assert.Empty(done.MsqPositions());

        Assert.Empty(new IpcView(Catalog, null, BlockerNames.Default).MsqPositions());
        Assert.Empty(IpcView.Empty.MsqPositions());
    }

    [Fact]
    public void An_any_join_with_every_route_locked_out_reads_the_same_everywhere()
    {
        // Every quest of the three routes foreclosed: nothing is left to wait for, so J is the single position and
        // no surface reads "MSQ done" or an empty route list.
        var catalog = Build(JoinKind.Any);
        var evaluations = Evaluate(catalog, L1, L2);
        foreach (var id in Done(RouteA, RouteB, RouteC))
        {
            evaluations[id] = evaluations[id] with { State = QuestState.Foreclosed };
        }

        var position = MsqProgress.Compute(catalog, evaluations)!;
        Assert.False(position.IsBranched);
        Assert.False(position.IsComplete);
        Assert.Equal(J, position.Next?.RowId);
        Assert.Equal([J], position.Positions.Select(q => q.RowId));
        Assert.Equal((2, Story.Length - RouteA.Length - RouteB.Length - RouteC.Length), (position.Done, position.Total));

        // IPC: the first entry of GetMsqPositions is GetMsqPosition's answer.
        var view = new IpcView(catalog, evaluations, BlockerNames.Default);
        Assert.Equal(J, view.MsqNext());
        Assert.Equal([J], view.MsqPositions());

        // The pill has no route wording to print, and Todo and Tonight list J.
        Assert.Equal(string.Empty, MsqText.Compact(position, Name));
        Assert.Equal([J], MsqSection(catalog, evaluations)!.Rows.Select(r => r.RowId));
    }

    [Fact]
    public void Ipc_positions_are_a_fresh_array_per_call()
    {
        var view = new IpcView(Catalog, Evaluate(Catalog, L1, L2), BlockerNames.Default);

        var first = view.MsqPositions();
        first[0] = 0;

        Assert.Equal([A1, B1, C1], view.MsqPositions());
    }

    // The unlock route.

    [Fact]
    public void Unlock_route_walks_each_route_to_its_end_before_the_next()
    {
        // By level alone the routes would interleave (A1 A2 B1 B2 B3 C1, then the level 41 quests); by route they
        // do not.
        var route = UnlockRoute.Build(RouteTarget.ForQuest(P1, "Evercold"), Catalog, Evaluate(Catalog, L1, L2));

        Assert.Equal(RouteOutcome.Route, route.Outcome);
        Assert.Equal([A1, A2, A3, B1, B2, B3, B4, B5, C1, C2, J, P1], route.Steps.Select(s => s.RowId));
        Assert.All(route.Steps, s => Assert.True(s.IsMainScenario));
    }

    [Fact]
    public void Unlock_route_milestone_falls_after_every_route()
    {
        var route = UnlockRoute.Build(RouteTarget.ForQuest(P1, "Evercold"), Catalog, Evaluate(Catalog, L1, L2, B1));

        var milestone = Assert.Single(route.Summary.Milestones);
        Assert.Equal(P1, milestone.LastRowId);
        Assert.Equal("Evercold", milestone.Name);
        Assert.Equal([A1, A2, A3, B2, B3, B4, B5, C1, C2, J, P1], route.Steps.Select(s => s.RowId));
    }

    [Fact]
    public void Before_evercold_the_unlock_route_keeps_the_level_order()
    {
        var dawntrail = Build(JoinKind.All, expansion: 5);

        var route = UnlockRoute.Build(RouteTarget.ForQuest(P1, "Dawntrail"), dawntrail, Evaluate(dawntrail, L1, L2));

        Assert.Equal([A1, A2, B1, B2, B3, C1, A3, B4, B5, C2, J, P1], route.Steps.Select(s => s.RowId));
    }
}
