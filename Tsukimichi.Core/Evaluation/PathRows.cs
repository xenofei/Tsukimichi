using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>What one line of the detail pane's Path chart is.</summary>
public enum PathRowKind : byte
{
    /// <summary>An expansion's band header (the sky band's caption and its four-point star on the thread).</summary>
    Band,

    /// <summary>One quest on the path; the target is the last one (<see cref="PathRow.IsTarget"/>).</summary>
    Step,

    /// <summary>A run of at least <see cref="PathRows.MinFoldedRun"/> completed steps folded into one bead.</summary>
    FoldedRun,

    /// <summary>A previous quest of the next step's Any join that the path does not take (a ghost node).</summary>
    Alternative,

    /// <summary>"and N more" under a join's listed alternatives.</summary>
    MoreAlternatives,
}

/// <summary>One line of the Path chart, built once per path (<see cref="PathRows.Build"/>) so drawing allocates nothing.</summary>
/// <param name="Kind">What the line is.</param>
public sealed record PathRow(PathRowKind Kind)
{
    /// <summary>The expansion the line belongs to (its band).</summary>
    public byte Expansion { get; init; }

    /// <summary>The quest of a <see cref="PathRowKind.Step"/> or <see cref="PathRowKind.Alternative"/>; 0 otherwise.</summary>
    public uint RowId { get; init; }

    /// <summary>The state of <see cref="RowId"/> (Completed for a folded run).</summary>
    public QuestState State { get; init; }

    /// <summary>The step is the path's target (its last step).</summary>
    public bool IsTarget { get; init; }

    /// <summary>A step's index into the path; a folded run's first index; -1 otherwise.</summary>
    public int PathIndex { get; init; } = -1;

    /// <summary>Steps a folded run holds, or alternatives a <see cref="PathRowKind.MoreAlternatives"/> line stands for.</summary>
    public int Count { get; init; }

    /// <summary>A folded run's ordinal on the path (0, 1, …), the key its expanded state is remembered by; -1 otherwise.</summary>
    public int RunIndex { get; init; } = -1;

    /// <summary>An alternative's quests still to do (<see cref="PathAlternative.RemainingCount"/>).</summary>
    public int RemainingCount { get; init; }

    /// <summary>The Any join an alternative (or its "and N more") leads into.</summary>
    public uint JoinRowId { get; init; }

    /// <summary>The quests a <see cref="PathRowKind.MoreAlternatives"/> line stands for (<see cref="JoinAlternatives.Overflow"/>); empty otherwise.</summary>
    public IReadOnlyList<uint> OverflowRowIds { get; init; } = [];
}

/// <summary>
/// Turns a path (<see cref="PathFinder.PathTo"/>) into the lines of the detail pane's star chart (path-section
/// proposal §4): an expansion band header wherever the expansion changes, completed stretches folded, each Any join's
/// alternatives (<see cref="PathFinder.Alternatives"/>) just above it, the target last. Folding: a run of at least
/// <see cref="MinFoldedRun"/> consecutive completed steps of one expansion becomes one <see cref="PathRowKind.FoldedRun"/>,
/// except that the target never folds and the completed step immediately before a step that is not completed never
/// folds (it is the moon the gold thread visibly arrives from). Alternatives are listed only for a join that is not
/// completed; a completed join's roads not taken no longer matter. A path of one quest is the target alone, with no
/// band header (§4.11).
/// </summary>
public static class PathRows
{
    /// <summary>Fewest completed steps that fold; a lone completed step is always listed.</summary>
    public const int MinFoldedRun = 2;

    // Logical row heights at scale 1 (path-section proposal §4.1).
    public const float BandHeight = 20f;
    public const float StepHeight = 24f;
    public const float BeadHeight = 24f;
    public const float AlternativeHeight = 22f;
    public const float MoreHeight = 18f;
    public const float TargetHeight = 32f;

    /// <summary>A folded run's steps once expanded, each (compact rows, §4.8).</summary>
    public const float ExpandedStepHeight = 22f;

    // The Unlocks next tail under the target (§4.10): the junction row, one row per unlock, "and N more", the dots.
    public const float UnlocksHeaderHeight = 20f;
    public const float UnlockHeight = 22f;
    public const float UnlocksMoreHeight = 18f;
    public const float TailDotsHeight = 10f;

    /// <summary>Logical height of the Unlocks next tail with <paramref name="shown"/> unlocks listed.</summary>
    public static float UnlocksTailHeight(int shown, bool more) =>
        UnlocksHeaderHeight + (Math.Max(0, shown) * UnlockHeight) + (more ? UnlocksMoreHeight : 0f) + TailDotsHeight;

    /// <param name="path">The path, first step first, the target last.</param>
    /// <param name="expansionOf">The expansion of a step's quest.</param>
    /// <param name="alternatives">The path's Any-join alternatives; null or empty draws none.</param>
    public static IReadOnlyList<PathRow> Build(IReadOnlyList<PathStep> path, Func<uint, byte> expansionOf, IReadOnlyList<JoinAlternatives>? alternatives = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expansionOf);

        var rows = new List<PathRow>(path.Count + 8);
        var last = path.Count - 1;
        if (last < 0)
        {
            return rows;
        }

        if (last == 0)
        {
            rows.Add(StepRow(path, 0, expansionOf(path[0].RowId), isTarget: true));
            return rows;
        }

        var runStart = -1;
        var runCount = 0;
        var runIndex = 0;
        var band = -1;
        byte expansion = 0;

        void Flush()
        {
            if (runCount >= MinFoldedRun)
            {
                rows.Add(new PathRow(PathRowKind.FoldedRun)
                {
                    Expansion = expansion,
                    State = QuestState.Completed,
                    PathIndex = runStart,
                    Count = runCount,
                    RunIndex = runIndex++,
                });
            }
            else
            {
                for (var i = runStart; i >= 0 && i < runStart + runCount; i++)
                {
                    rows.Add(StepRow(path, i, expansion, isTarget: false));
                }
            }

            runStart = -1;
            runCount = 0;
        }

        for (var i = 0; i <= last; i++)
        {
            var step = path[i];
            var stepExpansion = expansionOf(step.RowId);
            if (band < 0 || stepExpansion != expansion)
            {
                Flush();
                expansion = stepExpansion;
                band = rows.Count;
                rows.Add(new PathRow(PathRowKind.Band) { Expansion = expansion });
            }

            var isTarget = i == last;
            var arrivesAtUndone = i < last && !path[i + 1].Done;
            if (step.Done && !isTarget && !arrivesAtUndone)
            {
                if (runCount == 0)
                {
                    runStart = i;
                }

                runCount++;
                continue;
            }

            Flush();
            if (!step.Done && alternatives is { Count: > 0 })
            {
                AddAlternatives(rows, alternatives, step.RowId, expansion);
            }

            rows.Add(StepRow(path, i, expansion, isTarget));
        }

        Flush();
        return rows;
    }

    /// <summary>Index of the target's row in <paramref name="rows"/>, or -1.</summary>
    public static int TargetIndex(IReadOnlyList<PathRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].IsTarget)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Where the target sits in the chart's view once it is scrolled to (§4.12): 60 % down.</summary>
    public const float TargetViewFraction = 0.6f;

    /// <summary>
    /// The scroll offset that puts content y <paramref name="y"/> at <paramref name="fraction"/> of a
    /// <paramref name="viewHeight"/> view over <paramref name="contentHeight"/> of content, clamped to the scrollable
    /// range (0 when everything fits).
    /// </summary>
    public static float ScrollFor(float y, float contentHeight, float viewHeight, float fraction = TargetViewFraction)
    {
        var max = MathF.Max(0f, contentHeight - viewHeight);
        var scroll = y - (viewHeight * fraction);
        return float.IsFinite(scroll) ? Math.Clamp(scroll, 0f, max) : 0f;
    }

    /// <summary>A row's logical height with its folded run collapsed (a target step is taller).</summary>
    public static float LogicalHeight(PathRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Kind switch
        {
            PathRowKind.Band => BandHeight,
            PathRowKind.Step => row.IsTarget ? TargetHeight : StepHeight,
            PathRowKind.FoldedRun => BeadHeight,
            PathRowKind.Alternative => AlternativeHeight,
            _ => MoreHeight,
        };
    }

    private static PathRow StepRow(IReadOnlyList<PathStep> path, int index, byte expansion, bool isTarget) => new(PathRowKind.Step)
    {
        Expansion = expansion,
        RowId = path[index].RowId,
        State = path[index].State,
        IsTarget = isTarget,
        PathIndex = index,
    };

    private static void AddAlternatives(List<PathRow> rows, IReadOnlyList<JoinAlternatives> alternatives, uint joinRowId, byte expansion)
    {
        foreach (var join in alternatives)
        {
            if (join.JoinRowId != joinRowId)
            {
                continue;
            }

            foreach (var alternative in join.Alternatives)
            {
                rows.Add(new PathRow(PathRowKind.Alternative)
                {
                    Expansion = expansion,
                    RowId = alternative.RowId,
                    State = alternative.State,
                    RemainingCount = alternative.RemainingCount,
                    JoinRowId = joinRowId,
                });
            }

            if (join.More > 0)
            {
                var overflow = new uint[join.Overflow.Count];
                for (var i = 0; i < overflow.Length; i++)
                {
                    overflow[i] = join.Overflow[i].RowId;
                }

                rows.Add(new PathRow(PathRowKind.MoreAlternatives) { Expansion = expansion, Count = join.More, JoinRowId = joinRowId, OverflowRowIds = overflow });
            }

            return;
        }
    }
}
