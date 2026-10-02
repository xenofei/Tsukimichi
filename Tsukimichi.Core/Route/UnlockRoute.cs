using System.Globalization;
using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Route;

/// <summary>
/// A main scenario journal category the route runs through ("Seventh Astral Era", "Heavensward"): the MSQ milestone
/// crossed once its last quest on the route (<paramref name="LastRowId"/>) is done.
/// </summary>
/// <param name="CategoryId">JournalCategory row id.</param>
/// <param name="Name">The category's name without the journal's "Main Scenario Quests" suffix.</param>
/// <param name="LastRowId">The route's last quest in the category.</param>
public sealed record RouteMilestone(uint CategoryId, string Name, uint LastRowId);

/// <summary>One quest still to do on an unlock route, in the order to do it.</summary>
/// <param name="RowId">Quest sheet row id.</param>
/// <param name="State">Its state for the character the route was built for.</param>
/// <param name="Level">The acceptance level (<see cref="QuestRecord.Level"/>): what the gate and the order use.</param>
/// <param name="DisplayLevel">The level the journal prints (<see cref="QuestRecord.DisplayLevel"/>).</param>
public sealed record RouteStep(uint RowId, QuestState State, byte Level, byte DisplayLevel)
{
    /// <summary>
    /// The level gate: this step's <see cref="Level"/> when it is above every earlier step's (the route asks for a new
    /// level here, the first step always included); 0 when an earlier step already needed as much.
    /// </summary>
    public byte LevelGate { get; init; }

    /// <summary>The character already has <see cref="Level"/> on a job the quest admits (true when levels were not given).</summary>
    public bool LevelMet { get; init; } = true;

    /// <summary>A main scenario quest (journal sections 0 and 1).</summary>
    public bool IsMainScenario { get; init; }

    /// <summary>The next MSQ milestone crossed at or after this step; null when no main scenario quest follows.</summary>
    public RouteMilestone? Milestone { get; init; }

    /// <summary>The quest's status line (<see cref="BlockerText.StatusText"/>), "Blocked · after MSQ: …" and the like.</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>For a quest that takes any one of several previous quests: the ones the route does not take, fewest quests left first.</summary>
    public IReadOnlyList<PathAlternative> Alternatives { get; init; } = [];

    /// <summary>The route's last step, the quest that unlocks the target.</summary>
    public bool IsTarget { get; init; }
}

/// <summary>How many quests a route holds, the levels it spans and the MSQ milestones it crosses.</summary>
/// <param name="MinLevel">Lowest <see cref="RouteStep.DisplayLevel"/> on the route (0 for an empty route).</param>
/// <param name="MaxLevel">Highest <see cref="RouteStep.DisplayLevel"/> on the route.</param>
public sealed record RouteSummary(int Count, byte MinLevel, byte MaxLevel, IReadOnlyList<RouteMilestone> Milestones)
{
    public static readonly RouteSummary Empty = new(0, 0, 0, []);

    /// <summary>"12 quests · Lv 50–60 · MSQ: Heavensward, Dragonsong"; "Nothing left to do" for an empty route.</summary>
    public string Text
    {
        get
        {
            if (Count == 0)
            {
                return CoreText.T("Core.Route.NothingLeft", "Nothing left to do");
            }

            var sb = new StringBuilder();
            sb.Append(string.Format(
                CultureInfo.CurrentCulture,
                Count == 1 ? CoreText.T("Core.Route.CountOne", "{0} quest") : CoreText.T("Core.Route.Count", "{0} quests"),
                Count));
            sb.Append(" · ").Append(string.Format(
                CultureInfo.CurrentCulture,
                MaxLevel != MinLevel ? CoreText.T("Core.Route.LevelRange", "Lv {0}–{1}") : CoreText.T("Core.Route.Level", "Lv {0}"),
                MinLevel,
                MaxLevel));

            if (Milestones.Count > 0)
            {
                sb.Append(" · ").Append(CoreText.T("Core.Route.MsqLabel", "MSQ:")).Append(' ');
                for (var i = 0; i < Milestones.Count; i++)
                {
                    sb.Append(i == 0 ? string.Empty : ", ").Append(Milestones[i].Name);
                }
            }

            return sb.ToString();
        }
    }
}

/// <summary>What a route came to.</summary>
public enum RouteOutcome : byte
{
    /// <summary>Quests are left to do (<see cref="UnlockRoute.Steps"/>).</summary>
    Route,

    /// <summary>The target's quest is completed: the route is empty.</summary>
    AlreadyDone,

    /// <summary>The target's quest, or a quest the route cannot avoid, is locked out or removed from the game; the steps are still listed.</summary>
    LockedOut,

    /// <summary>No quest in the catalog unlocks the target.</summary>
    NoQuest,
}

/// <summary>
/// The unlock route for alts (feature plan v3 P6): for a target and one character's quest states, the ordered list of
/// quests still to do.
/// <para>
/// <b>Which quests.</b> The target's prerequisite closure minus what is completed: walking back from the target
/// through <see cref="QuestCatalog.PrerequisitesOf"/> (the previous quests and the accept conditions that name a
/// quest), a completed quest ends the walk (its own prerequisites no longer matter), an All join needs every previous
/// quest, and an Any join is met by one completed previous quest or else takes the branch with the fewest quests left
/// to do (the other branches are listed on the step as <see cref="RouteStep.Alternatives"/> with their counts); a
/// branch through a locked-out or removed quest is taken only when every branch has one. A target several quests
/// unlock (<see cref="RouteTarget.QuestRowIds"/>) is chosen the same way.
/// </para>
/// <para>
/// <b>Ordering rule.</b> Every quest comes after every quest it needs (a topological order of the closure); among the
/// quests whose prerequisites are all placed, the one with the lowest acceptance level (<see cref="QuestRecord.Level"/>)
/// goes first, then the earlier in journal order (<see cref="JournalRef.SortKey"/>), then the lower row id. The
/// target is therefore always last. A cycle in the data is broken by placing what is left in the same order.
/// </para>
/// <para>
/// <b>Main scenario routes.</b> Inside a routed branch region of the main scenario (<see cref="MsqGraph"/>, Evercold
/// on) each route is done in one go: a route quest is ordered by its route's first quest's level and then by route,
/// so the route begun is finished before the next one starts, routes in their journal order. The MSQ milestone of a
/// category that ends at or after the reconvergence quest therefore falls after every route. Before Evercold no
/// region is routed and the order is exactly the rule above.
/// </para>
/// </summary>
public sealed class UnlockRoute
{
    private const string MainScenarioSuffix = " Main Scenario Quests";
    private const string MainScenarioPrefixJa = "メインクエスト：";

    private UnlockRoute(RouteTarget target, uint targetRowId, RouteOutcome outcome, IReadOnlyList<RouteStep> steps, RouteSummary summary, IReadOnlyList<PathAlternative> targetAlternatives)
    {
        Target = target;
        TargetRowId = targetRowId;
        Outcome = outcome;
        Steps = steps;
        Summary = summary;
        TargetAlternatives = targetAlternatives;
    }

    public RouteTarget Target { get; }

    /// <summary>The quest the route leads to (of <see cref="RouteTarget.QuestRowIds"/>, the completed one or the cheapest); 0 for <see cref="RouteOutcome.NoQuest"/>.</summary>
    public uint TargetRowId { get; }

    public RouteOutcome Outcome { get; }

    /// <summary>The quests still to do, in order; the target last. Empty when the target is done or unknown.</summary>
    public IReadOnlyList<RouteStep> Steps { get; }

    public RouteSummary Summary { get; }

    /// <summary>The target's other quests (<see cref="RouteTarget.QuestRowIds"/> the route does not use), fewest quests left first.</summary>
    public IReadOnlyList<PathAlternative> TargetAlternatives { get; }

    /// <summary>
    /// Builds the route to <paramref name="target"/> for the character whose states are <paramref name="states"/>
    /// (the live character's or a stored alt's). See the class summary for which quests and in what order.
    /// </summary>
    /// <param name="names">Names for the step status lines (the plugin's route quest names through the spoiler shield); null prints catalog names.</param>
    /// <param name="levelOf">The character's best level on a job the quest admits (<see cref="RouteLevels.For"/>); null counts every level gate as met.</param>
    public static UnlockRoute Build(
        RouteTarget target,
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        BlockerNames? names = null,
        Func<QuestRecord, byte>? levelOf = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);

        var planner = new Planner(catalog, states);
        var candidates = new List<uint>();
        foreach (var id in target.QuestRowIds)
        {
            if (catalog.ByRowId.ContainsKey(id) && !candidates.Contains(id))
            {
                candidates.Add(id);
            }
        }

        if (candidates.Count == 0)
        {
            return new UnlockRoute(target, 0, RouteOutcome.NoQuest, [], RouteSummary.Empty, []);
        }

        foreach (var id in candidates)
        {
            if (planner.Done(id))
            {
                return new UnlockRoute(target, id, RouteOutcome.AlreadyDone, [], RouteSummary.Empty, []);
            }
        }

        // The target quest: the candidate with the fewest quests left, a locked-out one only when all are.
        var ranked = new List<(uint RowId, int Cost, int Count)>(candidates.Count);
        foreach (var id in candidates)
        {
            var closure = planner.Collect(id);
            ranked.Add((id, planner.Cost(closure), closure.Count));
        }

        ranked.Sort(static (a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.RowId.CompareTo(b.RowId));
        var targetRowId = ranked[0].RowId;
        var others = new PathAlternative[ranked.Count - 1];
        for (var i = 1; i < ranked.Count; i++)
        {
            others[i - 1] = new PathAlternative(ranked[i].RowId, planner.StateOf(ranked[i].RowId), ranked[i].Count);
        }

        var order = planner.Order(targetRowId);
        var steps = BuildSteps(order, targetRowId, catalog, states, planner, names ?? new BlockerNames { Catalog = catalog }, levelOf);
        var lockedOut = false;
        foreach (var id in order)
        {
            lockedOut |= planner.Dead(id);
        }

        return new UnlockRoute(target, targetRowId, lockedOut ? RouteOutcome.LockedOut : RouteOutcome.Route, steps.Steps, steps.Summary, others);
    }

    private static (RouteStep[] Steps, RouteSummary Summary) BuildSteps(
        List<uint> order,
        uint targetRowId,
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        Planner planner,
        BlockerNames names,
        Func<QuestRecord, byte>? levelOf)
    {
        // Milestones: each main scenario category in the order the route finishes with it.
        var lastInCategory = new Dictionary<uint, int>();
        for (var i = 0; i < order.Count; i++)
        {
            var quest = catalog.ByRowId[order[i]];
            if (FeaturePresets.IsMainScenario(quest))
            {
                lastInCategory[quest.Journal.CategoryId] = i;
            }
        }

        var milestoneAt = new Dictionary<int, RouteMilestone>();
        foreach (var (category, index) in lastInCategory)
        {
            var quest = catalog.ByRowId[order[index]];
            milestoneAt[index] = new RouteMilestone(category, MilestoneName(quest.Journal), quest.RowId);
        }

        var nextMilestone = new RouteMilestone?[order.Count];
        RouteMilestone? next = null;
        for (var i = order.Count - 1; i >= 0; i--)
        {
            if (milestoneAt.TryGetValue(i, out var milestone))
            {
                next = milestone;
            }

            nextMilestone[i] = next;
        }

        var steps = new RouteStep[order.Count];
        byte highest = 0;
        byte minLevel = byte.MaxValue;
        byte maxLevel = 0;
        var milestones = new List<RouteMilestone>(milestoneAt.Count);
        for (var i = 0; i < order.Count; i++)
        {
            var quest = catalog.ByRowId[order[i]];
            var gate = i == 0 || quest.Level > highest ? quest.Level : (byte)0;
            highest = Math.Max(highest, quest.Level);
            minLevel = Math.Min(minLevel, quest.DisplayLevel);
            maxLevel = Math.Max(maxLevel, quest.DisplayLevel);
            if (milestoneAt.TryGetValue(i, out var milestone))
            {
                milestones.Add(milestone);
            }

            steps[i] = new RouteStep(quest.RowId, planner.StateOf(quest.RowId), quest.Level, quest.DisplayLevel)
            {
                LevelGate = gate,
                LevelMet = levelOf is null || levelOf(quest) >= quest.Level,
                IsMainScenario = FeaturePresets.IsMainScenario(quest),
                Milestone = nextMilestone[i],
                StatusText = BlockerText.StatusText(states.GetValueOrDefault(quest.RowId), quest, names, states),
                Alternatives = planner.AlternativesOf(quest),
                IsTarget = quest.RowId == targetRowId,
            };
        }

        var summary = order.Count == 0 ? RouteSummary.Empty : new RouteSummary(order.Count, minLevel, maxLevel, milestones);
        return (steps, summary);
    }

    /// <summary>
    /// The category name without the journal's "Main Scenario Quests": "Seventh Astral Era", "Post-Shadowbringers II";
    /// a name that is nothing but those words, or lacks them, as it is.
    /// </summary>
    public static string MilestoneName(JournalRef journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var name = journal.CategoryName;
        var at = name.IndexOf(MainScenarioSuffix, StringComparison.Ordinal);
        if (at > 0)
        {
            return name.Remove(at, MainScenarioSuffix.Length);
        }

        // The Japanese journal opens the category with its label instead ("メインクエスト：黄金のレガシー").
        return name.StartsWith(MainScenarioPrefixJa, StringComparison.Ordinal) && name.Length > MainScenarioPrefixJa.Length
            ? name[MainScenarioPrefixJa.Length..]
            : name;
    }

    /// <summary>The closure walk, the Any-join choices (memoized so every walk agrees) and the ordering.</summary>
    private sealed class Planner(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        /// <summary>Added to a branch's cost when it runs through a locked-out or removed quest.</summary>
        private const int DeadPenalty = 1_000_000;

        private readonly Dictionary<uint, Choice> choices = [];
        private readonly HashSet<uint> choosing = [];
        private readonly MsqGraph msq = MsqGraph.For(catalog);

        public QuestState StateOf(uint rowId) =>
            states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

        public bool Done(uint rowId) => StateOf(rowId) == QuestState.Completed;

        public bool IsOtherPath(uint rowId) => states.TryGetValue(rowId, out var evaluation) && evaluation.IsOtherPath;

        public bool Dead(uint rowId) =>
            StateOf(rowId) == QuestState.Foreclosed || (catalog.ByRowId.TryGetValue(rowId, out var quest) && quest.IsRetired);

        public int Cost(HashSet<uint> closure)
        {
            foreach (var id in closure)
            {
                if (Dead(id))
                {
                    return closure.Count + DeadPenalty;
                }
            }

            return closure.Count;
        }

        public IReadOnlyList<PathAlternative> AlternativesOf(QuestRecord quest) =>
            quest.PreviousQuests.Join == JoinKind.Any && choices.TryGetValue(quest.RowId, out var choice) ? choice.Others : [];

        /// <summary>The quests still to do for <paramref name="root"/>, itself included (empty when it is done).</summary>
        public HashSet<uint> Collect(uint root)
        {
            var set = new HashSet<uint>();
            if (Done(root))
            {
                return set;
            }

            var stack = new Stack<uint>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!set.Add(id))
                {
                    continue;
                }

                foreach (var need in Needs(catalog.ByRowId[id]))
                {
                    if (!set.Contains(need))
                    {
                        stack.Push(need);
                    }
                }
            }

            return set;
        }

        /// <summary>The closure of <paramref name="root"/> in route order (see <see cref="UnlockRoute"/>'s ordering rule).</summary>
        public List<uint> Order(uint root)
        {
            var needed = Collect(root);
            var pending = new Dictionary<uint, int>(needed.Count);
            var dependents = new Dictionary<uint, List<uint>>();
            foreach (var id in needed)
            {
                var count = 0;
                foreach (var need in Needs(catalog.ByRowId[id]))
                {
                    if (!needed.Contains(need))
                    {
                        continue;
                    }

                    count++;
                    if (!dependents.TryGetValue(need, out var list))
                    {
                        dependents[need] = list = [];
                    }

                    list.Add(id);
                }

                pending[id] = count;
            }

            var ready = new PriorityQueue<uint, (byte Level, int Route, int SortKey, uint RowId)>();
            foreach (var (id, count) in pending)
            {
                if (count == 0)
                {
                    ready.Enqueue(id, Key(id));
                }
            }

            var order = new List<uint>(needed.Count);
            var placed = new HashSet<uint>();
            while (ready.Count > 0)
            {
                var id = ready.Dequeue();
                order.Add(id);
                placed.Add(id);
                if (!dependents.TryGetValue(id, out var list))
                {
                    continue;
                }

                foreach (var dependent in list)
                {
                    if (--pending[dependent] == 0)
                    {
                        ready.Enqueue(dependent, Key(dependent));
                    }
                }
            }

            if (order.Count < needed.Count)
            {
                // A cycle in the data: what is left goes in the same order, prerequisites or not.
                var rest = needed.Where(id => !placed.Contains(id)).OrderBy(Key).ToList();
                order.AddRange(rest);
            }

            return order;
        }

        /// <summary>
        /// The ordering key: level, then journal order, then row id; a main scenario route quest takes its route's
        /// entry level and its route's place, so a route is walked to its end before the next begins.
        /// </summary>
        private (byte Level, int Route, int SortKey, uint RowId) Key(uint rowId)
        {
            var quest = catalog.ByRowId[rowId];
            return msq.RouteOf(rowId) is { } route
                ? (route.First.Level, route.Index + 1, quest.Journal.SortKey, rowId)
                : (quest.Level, 0, quest.Journal.SortKey, rowId);
        }

        /// <summary>
        /// The previous quests of <paramref name="quest"/> the route must still do: every one for All, the chosen branch
        /// for Any and the accept conditions it needs beside it (<see cref="Prereq.Required"/>).
        /// </summary>
        private IEnumerable<uint> Needs(QuestRecord quest)
        {
            if (quest.PreviousQuests.Join == JoinKind.Any)
            {
                if (Choose(quest) is { } chosen && !Done(chosen))
                {
                    yield return chosen;
                }

                foreach (var id in catalog.PrerequisitesOf(quest).Required)
                {
                    if (id != quest.RowId && catalog.ByRowId.ContainsKey(id) && !Done(id))
                    {
                        yield return id;
                    }
                }

                yield break;
            }

            foreach (var id in Prereqs(quest))
            {
                if (!Done(id))
                {
                    yield return id;
                }
            }
        }

        /// <summary>The catalogued prerequisites, accept conditions that name a quest included (<see cref="QuestCatalog.PrerequisitesOf"/>).</summary>
        private List<uint> Prereqs(QuestRecord quest)
        {
            var prerequisites = catalog.PrerequisitesOf(quest).QuestIds;
            var list = new List<uint>(prerequisites.Length);
            foreach (var id in prerequisites)
            {
                if (id != quest.RowId && catalog.ByRowId.ContainsKey(id) && !list.Contains(id))
                {
                    list.Add(id);
                }
            }

            return list;
        }

        /// <summary>
        /// The branch an Any join takes: null when a previous quest is already completed or none is catalogued;
        /// otherwise the one with the fewest quests left (a branch through a dead quest last), ties to the lower row id.
        /// </summary>
        private uint? Choose(QuestRecord quest)
        {
            if (choices.TryGetValue(quest.RowId, out var known))
            {
                return known.Chosen;
            }

            if (!choosing.Add(quest.RowId))
            {
                // Re-entered through a cycle: no edge this time, and nothing remembered.
                return null;
            }

            // Only the alternatives: an accept condition the join needs whatever the branch is no choice (Needs).
            var required = catalog.PrerequisitesOf(quest);
            var prereqs = Prereqs(quest);
            prereqs.RemoveAll(required.IsRequired);
            Choice choice;
            if (prereqs.Count == 0 || prereqs.Exists(Done))
            {
                choice = Choice.None;
            }
            else if (prereqs.Count == 1)
            {
                choice = new Choice(prereqs[0], []);
            }
            else
            {
                var ranked = new List<(uint RowId, int Cost, int Count)>(prereqs.Count);
                foreach (var id in prereqs)
                {
                    var closure = Collect(id);
                    ranked.Add((id, Cost(closure), closure.Count));
                }

                ranked.Sort(static (a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.RowId.CompareTo(b.RowId));
                // A branch on a path the character did not take (another city's start, another class's track) is
                // no alternative worth naming.
                var others = new List<PathAlternative>(ranked.Count - 1);
                for (var i = 1; i < ranked.Count; i++)
                {
                    if (!IsOtherPath(ranked[i].RowId))
                    {
                        others.Add(new PathAlternative(ranked[i].RowId, StateOf(ranked[i].RowId), ranked[i].Count));
                    }
                }

                choice = new Choice(ranked[0].RowId, others);
            }

            choosing.Remove(quest.RowId);
            choices[quest.RowId] = choice;
            return choice.Chosen;
        }

        private sealed record Choice(uint? Chosen, IReadOnlyList<PathAlternative> Others)
        {
            public static readonly Choice None = new(null, []);
        }
    }
}

/// <summary>The character's level for a quest, for <see cref="UnlockRoute.Build"/>'s level gates.</summary>
public static class RouteLevels
{
    /// <summary>
    /// The highest level <paramref name="snapshot"/> has on a job <paramref name="context"/> says the quest admits: the
    /// pinned job for a job quest, the category's jobs through <see cref="EvalContext.ClassJobs"/>, else any job.
    /// </summary>
    public static Func<QuestRecord, byte> For(CharacterSnapshot snapshot, EvalContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        return quest =>
        {
            byte best = 0;
            foreach (var (job, level) in snapshot.JobLevels)
            {
                if (level > best && RequirementEvaluator.AdmitsJob(quest, snapshot, context, job))
                {
                    best = (byte)Math.Clamp(level, (short)0, (short)byte.MaxValue);
                }
            }

            return best;
        };
    }
}
