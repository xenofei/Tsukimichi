using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

public class PathFinderTests
{
    private static IReadOnlyDictionary<uint, QuestEvaluation> States(QuestCatalog catalog, params uint[] completed) =>
        StateResolver.ResolveAll(catalog, Snapshot(completed), EvalContext.Default);

    [Fact]
    public void Linear_chain_runs_from_first_step_to_target()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(Target) with { PreviousQuests = new Prereq([B], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog, A));

        Assert.Equal([A, B, Target], path.Select(p => p.RowId));
        Assert.Equal([2, 1, 0], path.Select(p => p.Depth));
        Assert.Equal([true, false, false], path.Select(p => p.Done));
        Assert.Equal(QuestState.Completed, path[0].State);
        Assert.Equal(QuestState.Ready, path[1].State);
        Assert.Equal(2, PathFinder.RemainingCount(Target, catalog, States(catalog, A)));
    }

    [Fact]
    public void All_join_includes_every_branch_once()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(C) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(Target) with { PreviousQuests = new Prereq([B, C], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([A, B, C, Target], path.Select(p => p.RowId));
        Assert.Equal(4, PathFinder.RemainingCount(Target, catalog, States(catalog)));
    }

    [Fact]
    public void Any_join_takes_the_branch_with_fewest_incomplete_quests()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(C) with { PreviousQuests = new Prereq([B], JoinKind.All) },
            Quest(D),
            Quest(Target) with { PreviousQuests = new Prereq([C, D], JoinKind.Any) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([D, Target], path.Select(p => p.RowId));
    }

    [Fact]
    public void Any_join_prefers_the_branch_that_is_already_done()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(C) with { PreviousQuests = new Prereq([B], JoinKind.All) },
            Quest(D),
            Quest(Target) with { PreviousQuests = new Prereq([C, D], JoinKind.Any) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog, A, B, C));

        Assert.Equal([A, B, C, Target], path.Select(p => p.RowId));
        Assert.Equal(1, PathFinder.RemainingCount(Target, catalog, States(catalog, A, B, C)));
    }

    [Fact]
    public void Any_join_ties_go_to_the_lowest_row_id()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(B),
            Quest(Target) with { PreviousQuests = new Prereq([B, A], JoinKind.Any) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([A, Target], path.Select(p => p.RowId));
    }

    [Fact]
    public void Cycles_are_cut()
    {
        var catalog = Catalog(
            Quest(A) with { PreviousQuests = new Prereq([B], JoinKind.All) },
            Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([B, A, Target], path.Select(p => p.RowId));
    }

    [Fact]
    public void Any_join_where_every_branch_cycles_back_keeps_only_the_branch_heads()
    {
        // A and B each lead straight back to the target, so at their level no branch survives; each is then a
        // one-quest branch of equal cost and the tie goes to the lowest row id.
        var catalog = Catalog(
            Quest(A) with { PreviousQuests = new Prereq([Target], JoinKind.Any) },
            Quest(B) with { PreviousQuests = new Prereq([Target], JoinKind.Any) },
            Quest(Target) with { PreviousQuests = new Prereq([B, A], JoinKind.Any) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([A, Target], path.Select(p => p.RowId));
        Assert.Equal([1, 0], path.Select(p => p.Depth));
    }

    [Fact]
    public void Any_join_that_only_names_itself_is_a_single_step()
    {
        var catalog = Catalog(Quest(Target) with { PreviousQuests = new Prereq([Target], JoinKind.Any) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([Target], path.Select(p => p.RowId));
        Assert.Equal(1, PathFinder.RemainingCount(Target, catalog, States(catalog)));
    }

    [Fact]
    public void Self_reference_is_cut()
    {
        var catalog = Catalog(Quest(Target) with { PreviousQuests = new Prereq([Target], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([Target], path.Select(p => p.RowId));
    }

    [Fact]
    public void Missing_ids_are_skipped()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(Target) with { PreviousQuests = new Prereq([12345, A], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([A, Target], path.Select(p => p.RowId));
        Assert.Empty(PathFinder.PathTo(99999, catalog, States(catalog)));
    }

    [Fact]
    public void Steps_without_a_state_are_Unknown()
    {
        var catalog = Catalog(Quest(A), Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, new Dictionary<uint, QuestEvaluation>());

        Assert.All(path, p => Assert.Equal(QuestState.Unknown, p.State));
        Assert.All(path, p => Assert.False(p.Done));
    }

    [Fact]
    public void Diamond_dependencies_appear_once_with_their_first_depth()
    {
        var catalog = Catalog(
            Quest(A),
            Quest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Quest(C) with { PreviousQuests = new Prereq([A, B], JoinKind.All) },
            Quest(Target) with { PreviousQuests = new Prereq([A, C], JoinKind.All) });

        var path = PathFinder.PathTo(Target, catalog, States(catalog));

        Assert.Equal([A, B, C, Target], path.Select(p => p.RowId));
        Assert.Equal(1, path[0].Depth);
    }
}
