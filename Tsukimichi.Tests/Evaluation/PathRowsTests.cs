using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Evaluation;

public class PathRowsTests
{
    private const QuestState Done = QuestState.Completed;
    private const QuestState Ready = QuestState.Ready;
    private const QuestState Blocked = QuestState.Blocked;

    /// <summary>A path of consecutive row ids from 1, one expansion per step as given (0 when omitted).</summary>
    private static (List<PathStep> Path, Func<uint, byte> Expansion) Path(QuestState[] states, byte[]? expansions = null)
    {
        var path = new List<PathStep>();
        for (var i = 0; i < states.Length; i++)
        {
            path.Add(new PathStep((uint)(i + 1), states[i], states.Length - 1 - i));
        }

        return (path, id => expansions is null ? (byte)0 : expansions[id - 1]);
    }

    private static string Shape(IReadOnlyList<PathRow> rows) => string.Join(" ", rows.Select(r => r.Kind switch
    {
        PathRowKind.Band => $"[{r.Expansion}]",
        PathRowKind.FoldedRun => $"fold{r.Count}@{r.PathIndex}",
        PathRowKind.Alternative => $"alt{r.RowId}",
        PathRowKind.MoreAlternatives => $"more{r.Count}",
        _ => r.IsTarget ? $"T{r.RowId}" : $"s{r.RowId}",
    }));

    [Fact]
    public void Completed_runs_fold_but_the_step_before_the_first_undone_one_stays()
    {
        var (path, expansion) = Path([Done, Done, Done, Done, Ready, Blocked]);

        var rows = PathRows.Build(path, expansion);

        // Steps 1-3 fold; step 4 is the moon the gold thread arrives from, so it is listed.
        Assert.Equal("[0] fold3@0 s4 s5 T6", Shape(rows));
        Assert.Equal(4, PathRows.TargetIndex(rows));
        Assert.Equal(0, rows[1].RunIndex);
    }

    [Fact]
    public void A_single_completed_step_or_a_pair_split_by_the_rule_is_never_folded()
    {
        var (path, expansion) = Path([Done, Done, Ready, Done, Blocked]);

        var rows = PathRows.Build(path, expansion);

        // Steps 1 and 2: 2 arrives at the undone 3, so only 1 is left, and one step does not fold.
        // Step 4 is done and arrives at the undone target.
        Assert.Equal("[0] s1 s2 s3 s4 T5", Shape(rows));
    }

    [Fact]
    public void A_completed_target_never_folds_and_runs_before_it_do()
    {
        var (path, expansion) = Path([Done, Done, Done, Done]);

        var rows = PathRows.Build(path, expansion);

        Assert.Equal("[0] fold3@0 T4", Shape(rows));
        Assert.Equal(QuestState.Completed, rows[^1].State);
    }

    [Fact]
    public void Each_expansion_gets_a_band_and_runs_do_not_cross_bands()
    {
        var (path, expansion) = Path([Done, Done, Done, Done, Done, Ready, Blocked], [0, 0, 0, 1, 1, 1, 2]);

        var rows = PathRows.Build(path, expansion);

        // A Realm Reborn's three fold; in Heavensward 5 arrives at the undone 6, which leaves 4 alone, and one step never folds.
        Assert.Equal("[0] fold3@0 [1] s4 s5 s6 [2] T7", Shape(rows));
        Assert.All(rows.Where(r => r.Kind == PathRowKind.Band), r => Assert.Equal(-1, r.PathIndex));
        Assert.Equal([0, 0, 1, 1, 1, 1, 2, 2], rows.Select(r => (int)r.Expansion));
    }

    [Fact]
    public void The_arrival_rule_holds_across_a_band_boundary()
    {
        var (path, expansion) = Path([Done, Done, Done, Ready], [0, 0, 0, 1]);

        var rows = PathRows.Build(path, expansion);

        Assert.Equal("[0] fold2@0 s3 [1] T4", Shape(rows));
    }

    [Fact]
    public void Folded_runs_and_steps_account_for_every_step_once()
    {
        var states = new[] { Done, Done, Done, Ready, Done, Done, Done, Done, Blocked, Done, Blocked };
        var (path, expansion) = Path(states);

        var rows = PathRows.Build(path, expansion);

        var covered = rows.Where(r => r.Kind == PathRowKind.Step).Select(r => r.PathIndex)
            .Concat(rows.Where(r => r.Kind == PathRowKind.FoldedRun).SelectMany(r => Enumerable.Range(r.PathIndex, r.Count)))
            .OrderBy(i => i);
        Assert.Equal(Enumerable.Range(0, states.Length), covered);
        Assert.Equal([0, 1], rows.Where(r => r.Kind == PathRowKind.FoldedRun).Select(r => r.RunIndex));
    }

    [Fact]
    public void Alternatives_sit_just_above_their_join_with_the_rest_counted()
    {
        var (path, expansion) = Path([Done, Ready, Blocked]);
        var joins = new[]
        {
            new JoinAlternatives(3, [new PathAlternative(40, Blocked, 3), new PathAlternative(41, Ready, 1)], 2)
            {
                Overflow = [new PathAlternative(43, Blocked, 4), new PathAlternative(44, Blocked, 5)],
            },
            new JoinAlternatives(99, [new PathAlternative(42, Ready, 1)], 0),
        };

        var rows = PathRows.Build(path, expansion, joins);

        Assert.Equal("[0] s1 s2 alt40 alt41 more2 T3", Shape(rows));
        var alternative = rows[3];
        Assert.Equal(PathRowKind.Alternative, alternative.Kind);
        Assert.Equal(3u, alternative.JoinRowId);
        Assert.Equal(3, alternative.RemainingCount);
        Assert.Equal(Blocked, alternative.State);
        Assert.Equal(2, rows[5].Count);
        Assert.Equal([43u, 44u], rows[5].OverflowRowIds);
    }

    [Fact]
    public void A_completed_join_lists_no_alternatives()
    {
        var (path, expansion) = Path([Done, Done, Ready]);
        var joins = new[] { new JoinAlternatives(2, [new PathAlternative(40, Ready, 1)], 0) };

        var rows = PathRows.Build(path, expansion, joins);

        Assert.Equal("[0] s1 s2 T3", Shape(rows));
    }

    [Fact]
    public void A_path_of_one_is_the_target_alone_and_an_empty_path_has_no_rows()
    {
        var (path, expansion) = Path([Ready], [3]);

        var rows = PathRows.Build(path, expansion);

        var target = Assert.Single(rows);
        Assert.True(target.IsTarget);
        Assert.Equal(3, target.Expansion);
        Assert.Equal(0, PathRows.TargetIndex(rows));
        Assert.Empty(PathRows.Build([], expansion));
        Assert.Equal(-1, PathRows.TargetIndex([]));
    }

    [Fact]
    public void Logical_heights_follow_the_proposal()
    {
        Assert.Equal(20f, PathRows.LogicalHeight(new PathRow(PathRowKind.Band)));
        Assert.Equal(24f, PathRows.LogicalHeight(new PathRow(PathRowKind.Step)));
        Assert.Equal(32f, PathRows.LogicalHeight(new PathRow(PathRowKind.Step) { IsTarget = true }));
        Assert.Equal(24f, PathRows.LogicalHeight(new PathRow(PathRowKind.FoldedRun)));
        Assert.Equal(22f, PathRows.LogicalHeight(new PathRow(PathRowKind.Alternative)));
        Assert.Equal(18f, PathRows.LogicalHeight(new PathRow(PathRowKind.MoreAlternatives)));
    }
}
