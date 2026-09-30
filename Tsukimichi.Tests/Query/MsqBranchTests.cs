using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.MsqBranchFixture;

namespace Tsukimichi.Tests.Query;

/// <summary>The branching main scenario (P14) over the synthetic Evercold fixture: graph, positions, routes, join, spoilers.</summary>
public class MsqBranchTests
{
    private static readonly QuestCatalog AllJoin = Build(JoinKind.All);
    private static readonly QuestCatalog AnyJoin = Build(JoinKind.Any);

    private static MsqPosition Position(QuestCatalog catalog, params uint[] completed)
    {
        var position = MsqProgress.Compute(catalog, Evaluate(catalog, completed));
        Assert.NotNull(position);
        return position;
    }

    private static uint[] Ids(IEnumerable<QuestRecord> quests) => quests.Select(q => q.RowId).ToArray();

    private static (int Done, int Total, MsqRouteStatus Status)[] Counts(MsqPosition position) =>
        position.Routes.Select(r => (r.Done, r.Total, r.Status)).ToArray();

    // The graph.

    [Fact]
    public void The_fixture_has_one_routed_region_with_three_routes_named_by_their_first_quests()
    {
        var graph = MsqGraph.For(AllJoin);

        var branch = Assert.Single(graph.Branches);
        Assert.True(branch.IsRouted);
        Assert.Equal(L2, branch.Start.RowId);
        Assert.Equal(J, branch.Join.RowId);
        Assert.Equal(JoinKind.All, branch.JoinKind);
        Assert.Equal(["The Ember Road", "The Glacier Road", "The Aurora Road"], branch.Routes.Select(r => r.Name));
        Assert.Equal([RouteA, RouteB, RouteC], branch.Routes.Select(r => Ids(r.Quests)));
        Assert.Equal([0, 1, 2], branch.Routes.Select(r => r.Index));
        Assert.Equal(JoinKind.Any, Assert.Single(MsqGraph.For(AnyJoin).Branches).JoinKind);
    }

    [Fact]
    public void Route_and_region_lookups_cover_route_quests_and_the_join_only()
    {
        var graph = MsqGraph.For(AllJoin);

        Assert.Equal("The Glacier Road", graph.RouteOf(B4)?.Name);
        Assert.Null(graph.RouteOf(J));
        Assert.Equal(J, graph.RoutedBranchOf(J)?.Join.RowId);
        Assert.Equal(J, graph.RoutedBranchOf(C1)?.Join.RowId);
        Assert.Null(graph.RoutedBranchOf(L2));
        Assert.Null(graph.RoutedBranchOf(P1));
        Assert.Equal([A1, B1, C1], Ids(graph.Successors(L2)));
        Assert.Equal([L2], Ids(graph.Predecessors(A1)));
    }

    [Fact]
    public void Same_shape_before_evercold_is_detected_but_not_routed()
    {
        var dawntrail = Build(JoinKind.All, expansion: 5);

        Assert.False(Assert.Single(MsqGraph.For(dawntrail).Branches).IsRouted);
        var position = MsqProgress.Compute(dawntrail, Evaluate(dawntrail, L1, L2, B1));
        Assert.False(position?.IsBranched);
        Assert.Equal(A1, position?.Next?.RowId);
    }

    [Fact]
    public void Two_quests_sharing_a_previous_quest_set_form_a_region()
    {
        // X and Y both need S1 and S2 (the rule "same MSQ prerequisite set"), then meet at Z.
        var catalog = QuestCatalog.Build(
        [
            Msq(A1, "S1", 40, Evercold),
            Msq(A2, "S2", 40, Evercold, A1),
            Msq(B1, "X", 40, Evercold) with { PreviousQuests = new Prereq([A1, A2], JoinKind.All) },
            Msq(C1, "Y", 40, Evercold) with { PreviousQuests = new Prereq([A1, A2], JoinKind.All) },
            Msq(J, "Z", 41, Evercold) with { PreviousQuests = new Prereq([B1, C1], JoinKind.All) },
        ]);

        var branch = Assert.Single(MsqGraph.For(catalog).Branches);
        Assert.Equal(A2, branch.Start.RowId);
        Assert.Equal(["X", "Y"], branch.Routes.Select(r => r.Name));
    }

    [Fact]
    public void A_region_inside_a_route_is_part_of_that_route()
    {
        // Route B holds its own little fork: B1 → (B2 | B3) → B4.
        var catalog = QuestCatalog.Build(Build().All.Select(q => q.RowId switch
        {
            B3 => q with { PreviousQuests = new Prereq([B1], JoinKind.All) },
            B4 => q with { PreviousQuests = new Prereq([B2, B3], JoinKind.All) },
            _ => q,
        }));

        var graph = MsqGraph.For(catalog);

        Assert.Equal(2, graph.Branches.Count);
        var outer = Assert.Single(graph.Branches, b => b.IsRouted);
        Assert.Equal(J, outer.Join.RowId);
        Assert.Equal(5, outer.Routes[1].Quests.Count);
        Assert.Equal(outer, graph.RoutedBranchOf(B2));
    }

    // Positions at each stage.

    [Fact]
    public void Before_the_fork_the_position_is_the_single_next_quest()
    {
        var position = Position(AllJoin, L1);

        Assert.False(position.IsBranched);
        Assert.Null(position.Branch);
        Assert.Equal(L2, position.Next?.RowId);
        Assert.Equal(QuestState.Ready, position.State);
        Assert.Equal([L2], Ids(position.Positions));
        Assert.Equal((1, Story.Length), (position.Done, position.Total));
    }

    [Fact]
    public void After_the_fork_every_route_is_a_position()
    {
        var position = Position(AllJoin, L1, L2);

        Assert.True(position.IsBranched);
        Assert.Equal(J, position.Branch?.Join.RowId);
        Assert.Equal([A1, B1, C1], Ids(position.Positions));
        Assert.Equal(A1, position.Next?.RowId);
        Assert.Equal(QuestState.Ready, position.State);
        Assert.Equal([(0, 3, MsqRouteStatus.NotStarted), (0, 5, MsqRouteStatus.NotStarted), (0, 2, MsqRouteStatus.NotStarted)], Counts(position));
        Assert.Equal(3, position.RoutesToJoin);
        Assert.Equal((2, Story.Length), (position.Done, position.Total));
    }

    [Fact]
    public void Route_progress_counts_follow_each_route()
    {
        var evaluations = Evaluate(AllJoin, L1, L2, A1, A2, C1, C2);
        evaluations[B1] = evaluations[B1] with { State = QuestState.Accepted };

        var position = MsqProgress.Compute(AllJoin, evaluations)!;

        Assert.Equal([(2, 3, MsqRouteStatus.InProgress), (0, 5, MsqRouteStatus.InProgress), (2, 2, MsqRouteStatus.Done)], Counts(position));
        Assert.Equal([A3, B1], Ids(position.Positions));
        Assert.Equal([QuestState.Ready, QuestState.Accepted, QuestState.Completed], position.Routes.Select(r => r.State));
        Assert.Null(position.Routes[2].Next);
        Assert.Equal(2, position.RoutesToJoin);
        Assert.Equal(6, position.Done);
    }

    [Fact]
    public void The_primary_position_is_the_first_route_not_done()
    {
        var position = Position(AllJoin, Done([L1, L2], RouteA, [B1, B2]));

        Assert.Equal(B3, position.Next?.RowId);
        Assert.Equal([B3, C1], Ids(position.Positions));
        Assert.Equal(MsqRouteStatus.Done, position.Routes[0].Status);
    }

    [Fact]
    public void Routes_can_be_done_in_any_order_with_the_same_outcome()
    {
        uint[][] routes = [RouteA, RouteB, RouteC];
        int[][] orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
        foreach (var order in orders)
        {
            var completed = new List<uint> { L1, L2 };
            for (var step = 0; step < order.Length; step++)
            {
                // Halfway along the route this step works on, then with it done.
                var route = routes[order[step]];
                var half = MsqProgress.Compute(AllJoin, Evaluate(AllJoin, [.. completed, route[0]]))!;
                Assert.True(half.IsBranched);
                Assert.Equal(MsqRouteStatus.InProgress, half.Routes[order[step]].Status);
                Assert.Equal(1, half.Routes[order[step]].Done);

                completed.AddRange(route);
                var position = MsqProgress.Compute(AllJoin, Evaluate(AllJoin, [.. completed]))!;
                var doneRoutes = order.Take(step + 1).ToHashSet();
                if (step < order.Length - 1)
                {
                    Assert.True(position.IsBranched);
                    Assert.DoesNotContain(J, Ids(position.Positions));
                    Assert.Equal(order.Length - step - 1, position.RoutesToJoin);
                    for (var r = 0; r < routes.Length; r++)
                    {
                        Assert.Equal(doneRoutes.Contains(r) ? MsqRouteStatus.Done : MsqRouteStatus.NotStarted, position.Routes[r].Status);
                        Assert.Equal(doneRoutes.Contains(r) ? routes[r].Length : 0, position.Routes[r].Done);
                    }

                    // The primary position is the first route not done, whatever order the others were done in.
                    var firstOpen = Enumerable.Range(0, routes.Length).First(r => !doneRoutes.Contains(r));
                    Assert.Equal(routes[firstOpen][0], position.Next?.RowId);
                }
                else
                {
                    Assert.False(position.IsBranched);
                    Assert.Equal(J, position.Next?.RowId);
                    Assert.Equal(QuestState.Ready, position.State);
                    Assert.Equal(12, position.Done);
                }
            }
        }
    }

    [Fact]
    public void The_reconvergence_quest_stays_blocked_until_every_route_is_done()
    {
        var almost = Done([L1, L2], RouteA, RouteB, [C1]);
        var evaluations = Evaluate(AllJoin, almost);

        Assert.Equal(QuestState.Blocked, evaluations[J].State);
        var position = MsqProgress.Compute(AllJoin, evaluations)!;
        Assert.True(position.IsBranched);
        Assert.Equal([C2], Ids(position.Positions));
        Assert.Equal(C2, position.Next?.RowId);
        Assert.Equal(1, position.RoutesToJoin);

        var joined = Evaluate(AllJoin, [.. almost, C2]);
        Assert.Equal(QuestState.Ready, joined[J].State);
        var after = MsqProgress.Compute(AllJoin, joined)!;
        Assert.False(after.IsBranched);
        Assert.Equal(J, after.Next?.RowId);
        Assert.Equal(0, after.RoutesToJoin);
    }

    [Fact]
    public void After_the_join_the_story_is_a_single_line_again()
    {
        var position = Position(AllJoin, Done([L1, L2], RouteA, RouteB, RouteC, [J]));

        Assert.False(position.IsBranched);
        Assert.Equal(P1, position.Next?.RowId);
        Assert.Equal((13, Story.Length), (position.Done, position.Total));

        var complete = Position(AllJoin, Story);
        Assert.True(complete.IsComplete);
        Assert.Empty(complete.Positions);
        Assert.Equal((Story.Length, Story.Length), (complete.Done, complete.Total));
    }

    [Fact]
    public void A_route_locked_out_neither_counts_nor_blocks_the_join()
    {
        var evaluations = Evaluate(AllJoin, Done([L1, L2], RouteA, RouteB));
        foreach (var id in RouteC)
        {
            evaluations[id] = evaluations[id] with { State = QuestState.Foreclosed };
        }

        var position = MsqProgress.Compute(AllJoin, evaluations)!;

        Assert.False(position.IsBranched);
        Assert.Equal(J, position.Next?.RowId);
        Assert.Equal(Story.Length - RouteC.Length, position.Total);
    }

    [Fact]
    public void The_join_is_met_once_the_reconvergence_quest_opens_even_with_an_optional_quest_left_on_a_route()
    {
        // Route B holds its own Any fork: B3 needs B2 or B2alt, and B2alt (after B1) lies on route B. The player
        // takes B2 and skips B2alt, so route B never reads done; the game opens J all the same.
        const uint B2alt = 70_026;
        var fixture = Build(JoinKind.All);
        var b2 = fixture.GetByRowId(B2)!;
        var catalog = QuestCatalog.Build(fixture.All
            .Select(q => q.RowId == B3 ? q with { PreviousQuests = new Prereq([B2, B2alt], JoinKind.Any) } : q)
            .Append(Msq(B2alt, "Around the Crevasse", 40, Evercold, B1) with { Journal = b2.Journal }));
        var graph = MsqGraph.For(catalog);
        Assert.Contains(B2alt, Ids(graph.RouteOf(B2)!.Quests));
        Assert.Same(graph.RouteOf(B2), graph.RouteOf(B2alt));

        // Before route C is done J is blocked and the region is reported route by route, B2alt on route B's line.
        var open = Done([L1, L2], RouteA, RouteB, [C1]);
        var inside = MsqProgress.Compute(catalog, Evaluate(catalog, open))!;
        Assert.True(inside.IsBranched);
        Assert.Equal([B2alt, C2], Ids(inside.Positions));

        // Every route the game needs is done: J reads Ready, and it is the position, B2alt a leftover.
        var evaluations = Evaluate(catalog, [.. open, C2]);
        Assert.Equal(QuestState.Ready, evaluations[J].State);
        Assert.Equal(QuestState.Ready, evaluations[B2alt].State);
        foreach (var state in new[] { QuestState.Ready, QuestState.ReadyOnOtherJob, QuestState.Accepted })
        {
            evaluations[J] = evaluations[J] with { State = state };
            var position = MsqProgress.Compute(catalog, evaluations)!;

            Assert.False(position.IsBranched);
            Assert.Equal(J, position.Next?.RowId);
            Assert.Equal(state, position.State);
            Assert.Equal([J], Ids(position.Positions));
            Assert.Equal(Story.Length, position.Total);

            // The shield no longer hides J behind the skipped quest.
            var mask = SpoilerMask.Build(catalog, evaluations, SpoilerOptions.Default with { Ahead = 0 });
            Assert.False(mask.IsMasked(J));
        }

        // Past J, the skipped quest never becomes the position.
        Assert.Equal(P1, MsqProgress.Compute(catalog, Evaluate(catalog, [.. open, C2, J]))!.Next?.RowId);
    }

    // The Any join.

    [Fact]
    public void Any_join_opens_after_one_route_and_drops_the_others()
    {
        var inside = Position(AnyJoin, L1, L2, A1);
        Assert.True(inside.IsBranched);
        Assert.Equal(1, inside.RoutesToJoin);
        Assert.Equal([A2, B1, C1], Ids(inside.Positions));

        // The shortest route done: J opens, and the unfinished quests of A and B are optional.
        var opened = Position(AnyJoin, L1, L2, A1, C1, C2);
        Assert.False(opened.IsBranched);
        Assert.Equal(J, opened.Next?.RowId);
        Assert.Equal(QuestState.Ready, opened.State);
        Assert.Equal(5, opened.Done);
        Assert.Equal(Story.Length - (RouteA.Length - 1) - RouteB.Length, opened.Total);

        // Past the join, a route left half done never becomes the position.
        var past = Position(AnyJoin, L1, L2, A1, C1, C2, J);
        Assert.Equal(P1, past.Next?.RowId);
    }

    [Fact]
    public void Any_join_reports_every_route_while_none_is_done()
    {
        var position = Position(AnyJoin, Done([L1, L2], [A1, A2], [B1, B2, B3, B4]));

        Assert.Equal([A3, B5, C1], Ids(position.Positions));
        Assert.Equal([(2, 3, MsqRouteStatus.InProgress), (4, 5, MsqRouteStatus.InProgress), (0, 2, MsqRouteStatus.NotStarted)], Counts(position));
        Assert.Equal(QuestState.Blocked, Evaluate(AnyJoin, Done([L1, L2], [A1, A2], [B1, B2, B3, B4]))[J].State);
    }

    [Fact]
    public void Both_state_overloads_agree_inside_a_region()
    {
        var evaluations = Evaluate(AllJoin, L1, L2, A1, B1, B2);
        var fromEvaluations = MsqProgress.Compute(AllJoin, evaluations)!;
        var fromStates = MsqProgress.Compute(AllJoin, evaluations.ToDictionary(p => p.Key, p => p.Value.State))!;

        Assert.Equal(fromEvaluations.Next, fromStates.Next);
        Assert.Equal(Ids(fromEvaluations.Positions), Ids(fromStates.Positions));
        Assert.Equal(Counts(fromEvaluations), Counts(fromStates));
    }

    // The spoiler shield, "N ahead" per route.

    private static uint[] Visible(QuestCatalog catalog, int ahead, params uint[] completed)
    {
        var mask = SpoilerMask.Build(catalog, States(catalog, completed), SpoilerOptions.Default with { Ahead = ahead });
        return Story.Where(id => !mask.IsMasked(id)).ToArray();
    }

    [Fact]
    public void Spoiler_mask_counts_ahead_along_each_route()
    {
        // Each route's next quest and the one after it; C's two are its whole route. J lies 3 + 5 + 2 quests ahead.
        Assert.Equal([L1, L2, A1, A2, B1, B2, C1, C2], Visible(AllJoin, 1, L1, L2));

        // Ahead 0: the positions alone.
        Assert.Equal([L1, L2, A1, B1, C1], Visible(AllJoin, 0, L1, L2));
    }

    [Fact]
    public void Spoiler_mask_reveals_the_join_once_few_enough_quests_are_left_before_it()
    {
        var late = Done([L1, L2], RouteA, [B1, B2, B3], RouteC);

        // B4 and B5 left: J is 2 ahead, P1 3.
        Assert.Equal(Story.Where(id => id is not (J or P1 or P2)).ToArray(), Visible(AllJoin, 1, late));
        Assert.Equal(Story.Where(id => id is not (P1 or P2)).ToArray(), Visible(AllJoin, 2, late));
        Assert.Equal(Story.Where(id => id is not P2).ToArray(), Visible(AllJoin, 3, late));
    }

    [Fact]
    public void Spoiler_mask_does_not_count_quests_done_out_of_order_toward_the_join()
    {
        // Route B done out of order (B2 skipped for now, B3 to B5 completed) and route C not started: B2 is the one
        // quest left on B, C1 and C2 on C, so J is 3 ahead, not 6.
        var states = States(AllJoin, Done([L1, L2], RouteA, [B1, B3, B4, B5]));
        Assert.Equal(QuestState.Completed, states[B4]);
        Assert.Equal(QuestState.Blocked, states[J]);

        var three = SpoilerMask.Build(AllJoin, states, SpoilerOptions.Default with { Ahead = 3 });
        Assert.False(three.IsMasked(J));
        Assert.True(three.IsMasked(P1));

        var two = SpoilerMask.Build(AllJoin, states, SpoilerOptions.Default with { Ahead = 2 });
        Assert.True(two.IsMasked(J));
    }

    [Fact]
    public void Spoiler_mask_counts_the_shortest_route_to_an_any_join()
    {
        // C1 done: C2 is the one quest left before J on the shortest route, so J is 1 ahead and P1 2.
        Assert.Equal([L1, L2, A1, A2, B1, B2, C1, C2, J], Visible(AnyJoin, 1, L1, L2, C1));
        Assert.Equal([L1, L2, A1, A2, A3, B1, B2, B3, C1, C2, J, P1], Visible(AnyJoin, 2, L1, L2, C1));

        // An All join needs A and B too: J is 3 + 5 + 1 ahead.
        Assert.Equal([L1, L2, A1, A2, B1, B2, C1, C2], Visible(AllJoin, 1, L1, L2, C1));
    }

    [Fact]
    public void Spoiler_reach_is_the_primary_positions_expansion()
    {
        var mask = SpoilerMask.Build(AllJoin, States(AllJoin, L1, L2), SpoilerOptions.Default);

        Assert.Equal(Evercold, mask.ReachExpansion);
    }
}
