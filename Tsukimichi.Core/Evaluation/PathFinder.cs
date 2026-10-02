using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>One quest on the path to a target.</summary>
/// <param name="Depth">Distance from the target along the chosen path; the target itself is 0.</param>
public sealed record PathStep(uint RowId, QuestState State, int Depth)
{
    public bool Done => State == QuestState.Completed;
}

/// <summary>A previous quest of an Any join that the path does not take.</summary>
/// <param name="RowId">The alternative prerequisite.</param>
/// <param name="State">Its state for the character.</param>
/// <param name="RemainingCount">Quests on its own path, itself included, that are not completed.</param>
public sealed record PathAlternative(uint RowId, QuestState State, int RemainingCount);

/// <summary>The alternatives into one Any join: the listed ones (at most <see cref="PathFinder.MaxAlternatives"/>) and how many more there are.</summary>
public sealed record JoinAlternatives(uint JoinRowId, IReadOnlyList<PathAlternative> Alternatives, int More)
{
    /// <summary>The <see cref="More"/> alternatives that are not listed, in the same order (the "and N more" tooltip names them).</summary>
    public IReadOnlyList<PathAlternative> Overflow { get; init; } = [];
}

/// <summary>
/// Walks a quest's previous quests to the first step. Through an Any join it takes the branch with the fewest
/// incomplete quests (ties to the lowest row id); cycles are cut and ids missing from the catalog are skipped. A
/// branch on a path the character did not take (<see cref="QuestEvaluation.IsOtherPath"/>: another city's start,
/// another class's track) is never taken while another is open, and is never offered as an alternative.
/// </summary>
public static class PathFinder
{
    /// <summary>The path from its first step to <paramref name="targetRowId"/>, each quest once. Empty when the target is unknown.</summary>
    public static IReadOnlyList<PathStep> PathTo(uint targetRowId, QuestCatalog c, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(states);

        if (!c.ByRowId.ContainsKey(targetRowId))
        {
            return [];
        }

        var walk = new Walk(c, states);
        var steps = walk.SubPath(targetRowId);
        var path = new PathStep[steps.Count];
        for (var i = 0; i < steps.Count; i++)
        {
            var (rowId, depth) = steps[i];
            path[i] = new PathStep(rowId, walk.StateOf(rowId), depth);
        }

        return path;
    }

    /// <summary>How many quests on the path, including the target, are not completed.</summary>
    public static int RemainingCount(uint targetRowId, QuestCatalog c, IReadOnlyDictionary<uint, QuestEvaluation> states) =>
        PathTo(targetRowId, c, states).Count(step => !step.Done);

    /// <summary>Most alternatives listed per Any join; the rest are counted in <see cref="JoinAlternatives.More"/>.</summary>
    public const int MaxAlternatives = 3;

    /// <summary>
    /// The roads not taken on <paramref name="path"/> (path-section proposal §4.9): for every step whose quest joins
    /// two or more catalogued previous quests with Any, the prerequisites the path does not walk, each with how many
    /// of its own path's quests are still to do (<see cref="RemainingCount"/>). At most <paramref name="max"/> per
    /// join, fewest remaining first (ties to the lowest row id), the rest counted; a prerequisite whose own path
    /// leads back through the join is left out. Joins with nothing left over are omitted. In path order.
    /// </summary>
    public static IReadOnlyList<JoinAlternatives> Alternatives(IReadOnlyList<PathStep> path, QuestCatalog c, IReadOnlyDictionary<uint, QuestEvaluation> states, int max = MaxAlternatives)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentOutOfRangeException.ThrowIfNegative(max);

        List<JoinAlternatives>? joins = null;
        HashSet<uint>? onPath = null;
        Walk? walk = null;
        foreach (var step in path)
        {
            if (!c.ByRowId.TryGetValue(step.RowId, out var quest) || quest.PreviousQuests.Join != JoinKind.Any)
            {
                continue;
            }

            var prereqs = c.PrerequisitesOf(quest).QuestIds;
            var catalogued = 0;
            foreach (var id in prereqs.Distinct())
            {
                if (id != step.RowId && c.ByRowId.ContainsKey(id))
                {
                    catalogued++;
                }
            }

            if (catalogued < 2)
            {
                continue;
            }

            onPath ??= path.Select(p => p.RowId).ToHashSet();
            walk ??= new Walk(c, states);
            List<PathAlternative>? found = null;
            foreach (var id in prereqs.Distinct())
            {
                if (id == step.RowId || onPath.Contains(id) || !c.ByRowId.ContainsKey(id) || walk.IsOtherPath(id))
                {
                    continue;
                }

                var branch = walk.SubPath(id);
                if (branch.Count == 0 || branch.Exists(s => s.RowId == step.RowId))
                {
                    continue;
                }

                var remaining = branch.Count(s => walk.StateOf(s.RowId) != QuestState.Completed);
                (found ??= []).Add(new PathAlternative(id, walk.StateOf(id), remaining));
            }

            if (found is null)
            {
                continue;
            }

            found.Sort(static (a, b) => a.RemainingCount != b.RemainingCount ? a.RemainingCount.CompareTo(b.RemainingCount) : a.RowId.CompareTo(b.RowId));
            var shown = Math.Min(max, found.Count);
            (joins ??= []).Add(new JoinAlternatives(step.RowId, found.GetRange(0, shown), found.Count - shown) { Overflow = found.GetRange(shown, found.Count - shown) });
        }

        return joins ?? (IReadOnlyList<JoinAlternatives>)[];
    }

    private sealed class Walk(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        private readonly Dictionary<uint, List<(uint RowId, int Depth)>> memo = [];
        private readonly HashSet<uint> inProgress = [];

        public QuestState StateOf(uint rowId) =>
            states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

        public bool IsOtherPath(uint rowId) =>
            states.TryGetValue(rowId, out var evaluation) && evaluation.IsOtherPath;

        /// <summary>Post-order path ending at <paramref name="rowId"/>, depths relative to it. Empty when re-entered (cycle).</summary>
        public List<(uint RowId, int Depth)> SubPath(uint rowId)
        {
            if (memo.TryGetValue(rowId, out var known))
            {
                return known;
            }

            if (!inProgress.Add(rowId))
            {
                return [];
            }

            var quest = catalog.ByRowId[rowId];
            var steps = new List<(uint RowId, int Depth)>();
            var seen = new HashSet<uint>();
            var prereqs = catalog.PrerequisitesOf(quest).QuestIds.Where(catalog.ByRowId.ContainsKey);

            if (quest.PreviousQuests.Join == JoinKind.Any)
            {
                List<(uint RowId, int Depth)>? best = null;
                var bestCost = int.MaxValue;
                var ordered = prereqs.OrderBy(id => id).ToList();
                if (ordered.Exists(id => !IsOtherPath(id)))
                {
                    // Another city's or class's line is never the way there while the character's own is open.
                    ordered.RemoveAll(IsOtherPath);
                }

                foreach (var prereq in ordered)
                {
                    var branch = SubPath(prereq);
                    if (branch.Count == 0)
                    {
                        continue;
                    }

                    var cost = branch.Count(step => StateOf(step.RowId) != QuestState.Completed);
                    if (cost < bestCost)
                    {
                        best = branch;
                        bestCost = cost;
                    }
                }

                if (best is not null)
                {
                    Append(steps, seen, best);
                }
            }
            else
            {
                foreach (var prereq in prereqs)
                {
                    Append(steps, seen, SubPath(prereq));
                }
            }

            steps.Add((rowId, 0));
            inProgress.Remove(rowId);
            memo[rowId] = steps;
            return steps;
        }

        private static void Append(List<(uint RowId, int Depth)> steps, HashSet<uint> seen, List<(uint RowId, int Depth)> branch)
        {
            foreach (var (rowId, depth) in branch)
            {
                if (seen.Add(rowId))
                {
                    steps.Add((rowId, depth + 1));
                }
            }
        }
    }
}
