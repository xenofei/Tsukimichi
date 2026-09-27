using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>One quest on the path to a target.</summary>
/// <param name="Depth">Distance from the target along the chosen path; the target itself is 0.</param>
public sealed record PathStep(uint RowId, QuestState State, int Depth)
{
    public bool Done => State == QuestState.Completed;
}

/// <summary>
/// Walks a quest's previous quests to the first step. Through an Any join it takes the branch with the fewest
/// incomplete quests (ties to the lowest row id); cycles are cut and ids missing from the catalog are skipped.
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

    private sealed class Walk(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        private readonly Dictionary<uint, List<(uint RowId, int Depth)>> memo = [];
        private readonly HashSet<uint> inProgress = [];

        public QuestState StateOf(uint rowId) =>
            states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

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
            var prereqs = quest.PreviousQuests.QuestIds.Where(catalog.ByRowId.ContainsKey);

            if (quest.PreviousQuests.Join == JoinKind.Any)
            {
                List<(uint RowId, int Depth)>? best = null;
                var bestCost = int.MaxValue;
                foreach (var prereq in prereqs.OrderBy(id => id))
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
