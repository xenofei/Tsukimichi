using System.Runtime.CompilerServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// One route of a branch region: the main scenario quests between the region's start and its reconvergence quest
/// that follow one of the start's successors, in play order (every quest after the quests it needs, journal order
/// between equals). Named after its first quest ("route In Search of Alphinaud").
/// </summary>
/// <param name="Index">The route's place in the region, 0 first; routes are ordered by their first quest's journal position.</param>
/// <param name="First">The route's first quest: its name is the route's name.</param>
/// <param name="Quests">Every quest on the route in play order, <paramref name="First"/> first; the reconvergence quest is not on it.</param>
public sealed record MsqRoute(int Index, QuestRecord First, IReadOnlyList<QuestRecord> Quests)
{
    /// <summary>The route's name: its first quest's name (print it through the spoiler shield).</summary>
    public string Name => First.Name;
}

/// <summary>
/// A branch region of the main scenario: a start quest with two or more main scenario successors whose lines run in
/// parallel and meet again at <see cref="Join"/>.
/// </summary>
/// <param name="Start">The quest the routes branch from (the last shared quest before them).</param>
/// <param name="Join">The reconvergence quest: the first quest every route leads to.</param>
/// <param name="JoinKind">
/// How <see cref="Join"/> combines its previous quests: <see cref="JoinKind.All"/> needs every route done,
/// <see cref="JoinKind.Any"/> one of them (the rest become optional).
/// </param>
/// <param name="Routes">The parallel routes, in <see cref="MsqRoute.Index"/> order.</param>
/// <param name="IsRouted">
/// Whether the position reports this region route by route. False for a region before
/// <see cref="MsqGraph.RoutedFromExpansion"/> (those are walked in journal order as before) and for a region nested
/// inside a route of a larger routed region.
/// </param>
public sealed record MsqBranch(QuestRecord Start, QuestRecord Join, JoinKind JoinKind, IReadOnlyList<MsqRoute> Routes, bool IsRouted)
{
    /// <summary>Whether <paramref name="rowId"/> is one of the routes' quests (the join is not).</summary>
    public bool HasOnRoute(uint rowId)
    {
        foreach (var route in Routes)
        {
            foreach (var quest in route.Quests)
            {
                if (quest.RowId == rowId)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>Where a character stands on one route of a branch region.</summary>
public enum MsqRouteStatus : byte
{
    /// <summary>No quest on the route is completed or in the journal.</summary>
    NotStarted,

    /// <summary>Some quests on the route are done or accepted, some are left.</summary>
    InProgress,

    /// <summary>Every quest on the route the character can do is completed.</summary>
    Done,

    /// <summary>Every quest on the route is locked out for the character (a branch not taken): it neither counts nor blocks.</summary>
    LockedOut,
}

/// <summary>One route's progress for one character.</summary>
/// <param name="Route">The route.</param>
/// <param name="Next">The route's first quest in play order that is not completed; null when the route is done or locked out.</param>
/// <param name="State">The state of <paramref name="Next"/>; <see cref="QuestState.Completed"/> when there is none.</param>
/// <param name="Done">Completed quests on the route.</param>
/// <param name="Total">Quests on the route the character can do or has done (locked-out ones left out).</param>
/// <param name="Status">Not started, in progress, done or locked out.</param>
public sealed record MsqRouteProgress(MsqRoute Route, QuestRecord? Next, QuestState State, int Done, int Total, MsqRouteStatus Status);

/// <summary>
/// The main scenario as a graph (feature plan v3 P14): its quests (journal sections 0 and 1, removed rows left out)
/// joined by their <see cref="QuestRecord.PreviousQuests"/> among main scenario quests, the branch regions found in
/// it, and the character's position read from it. Built once per catalog and cached with it.
/// <para>
/// <b>Branch regions.</b> A quest with two or more main scenario successors starts a region when those successors
/// (a successor reachable from another is a shortcut edge, not a route) all lead to a common later quest; the first
/// such quest in play order is the reconvergence quest. Each successor opens a route: every quest reachable from it
/// that leads to the reconvergence quest. Routes that share a quest before the reconvergence merge into one. Two or
/// more quests with the same previous-quest set are covered too: each quest of the set has them all as successors.
/// A region found from several start quests is kept once.
/// </para>
/// <para>
/// <b>Routed regions.</b> Only a region whose routes start in <see cref="RoutedFromExpansion"/> or later (Evercold,
/// 8.0, with its three-route main scenario) is reported route by route. The earlier regions (Shadowbringers'
/// Alphinaud and Alisaie lines, Dawntrail's Kozama'uka and Urqopacha lines, the Grand Company choice and a dozen
/// two- or three-quest errands) are detected but walked in journal order as they always were, so today's data
/// yields exactly the single position it always did. A region inside a route of a larger routed region is part of
/// that route, not reported on its own.
/// </para>
/// <para>
/// <b>Position.</b> The main scenario is walked in journal order, as <see cref="MsqProgress"/> always walked it: the
/// first quest neither completed nor on a branch the character did not take is the position. When that quest lies
/// in a routed region (on a route, or the reconvergence quest itself) and the region's join is not met yet, the
/// position becomes one entry per route (<see cref="MsqPosition.Routes"/>), and the primary quest
/// (<see cref="MsqPosition.Next"/>) is the first route's next quest. The reconvergence quest is never a position
/// while its join is unmet. The join is met once the reconvergence quest itself is open, in the journal or completed
/// (the evaluator has judged its previous quests as the game does), or else once the routes it needs are done: every
/// route that is not locked out for an All join, one of them for an Any join. Once the join is met, the unfinished
/// quests of the routes are optional: they leave the position and the totals, like a branch not taken.
/// </para>
/// </summary>
public sealed class MsqGraph
{
    /// <summary>
    /// The first expansion whose branch regions are reported route by route: 6, Evercold (8.0). Dawntrail is 5.
    /// </summary>
    public const byte RoutedFromExpansion = 6;

    private static readonly ConditionalWeakTable<QuestCatalog, MsqGraph> Cache = [];
    private static readonly IReadOnlyList<QuestRecord> NoQuests = [];

    private readonly Dictionary<uint, int> storyIndex;
    private readonly Dictionary<uint, List<QuestRecord>> successors;
    private readonly Dictionary<uint, List<QuestRecord>> predecessors;

    /// <summary>Row id to the routed region it belongs to (a route quest or the reconvergence quest).</summary>
    private readonly Dictionary<uint, MsqBranch> routedRegion = [];

    /// <summary>Row id to the route it lies on, for routed regions.</summary>
    private readonly Dictionary<uint, MsqRoute> routedRoute = [];

    private MsqGraph(QuestCatalog catalog, byte routedFromExpansion)
    {
        var story = new List<QuestRecord>();
        foreach (var section in MsqProgress.MainScenarioSections)
        {
            if (catalog.BySection.GetValueOrDefault(section) is not { } quests)
            {
                continue;
            }

            foreach (var quest in quests)
            {
                if (!quest.IsRemoved)
                {
                    story.Add(quest);
                }
            }
        }

        Story = story;
        storyIndex = new Dictionary<uint, int>(story.Count);
        for (var i = 0; i < story.Count; i++)
        {
            storyIndex.TryAdd(story[i].RowId, i);
        }

        successors = new Dictionary<uint, List<QuestRecord>>(story.Count);
        predecessors = new Dictionary<uint, List<QuestRecord>>(story.Count);
        foreach (var quest in story)
        {
            var before = new List<QuestRecord>();
            foreach (var id in quest.PreviousQuests.QuestIds)
            {
                if (id == quest.RowId || !storyIndex.TryGetValue(id, out var at) || before.Contains(story[at]))
                {
                    continue;
                }

                before.Add(story[at]);
                if (!successors.TryGetValue(id, out var after))
                {
                    successors[id] = after = [];
                }

                after.Add(quest);
            }

            predecessors[quest.RowId] = before;
        }

        var topo = TopologicalOrder();
        foreach (var list in successors.Values)
        {
            list.Sort((a, b) => topo[a.RowId].CompareTo(topo[b.RowId]));
        }

        Branches = FindBranches(topo, routedFromExpansion);
        foreach (var branch in Branches)
        {
            if (!branch.IsRouted)
            {
                continue;
            }

            routedRegion.TryAdd(branch.Join.RowId, branch);
            foreach (var route in branch.Routes)
            {
                foreach (var quest in route.Quests)
                {
                    routedRegion[quest.RowId] = branch;
                    routedRoute[quest.RowId] = route;
                }
            }
        }
    }

    /// <summary>Every live main scenario quest in journal order: section 0, then section 1.</summary>
    public IReadOnlyList<QuestRecord> Story { get; }

    /// <summary>Every branch region found, routed or not, in the order of their start quests.</summary>
    public IReadOnlyList<MsqBranch> Branches { get; }

    /// <summary>The graph for a catalog, built on first use and cached with the catalog.</summary>
    public static MsqGraph For(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, static c => new MsqGraph(c, RoutedFromExpansion));
    }

    /// <summary>
    /// A fresh graph (not cached) with its own <paramref name="routedFromExpansion"/>: 0 reports every region route
    /// by route, which is how the design note previews 8.0 behaviour on today's Shadowbringers and Dawntrail lines.
    /// </summary>
    public static MsqGraph Build(QuestCatalog catalog, byte routedFromExpansion = RoutedFromExpansion)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new MsqGraph(catalog, routedFromExpansion);
    }

    /// <summary>The main scenario quests that need <paramref name="rowId"/>, in play order.</summary>
    public IReadOnlyList<QuestRecord> Successors(uint rowId) => successors.TryGetValue(rowId, out var list) ? list : NoQuests;

    /// <summary>The main scenario quests <paramref name="rowId"/> needs (its previous quests that are main scenario quests).</summary>
    public IReadOnlyList<QuestRecord> Predecessors(uint rowId) => predecessors.TryGetValue(rowId, out var list) ? list : NoQuests;

    /// <summary>The routed region a quest belongs to, as a route quest or as the reconvergence quest; null otherwise.</summary>
    public MsqBranch? RoutedBranchOf(uint rowId) => routedRegion.GetValueOrDefault(rowId);

    /// <summary>The route of a routed region the quest lies on; null for any other quest (the reconvergence quest included).</summary>
    public MsqRoute? RouteOf(uint rowId) => routedRoute.GetValueOrDefault(rowId);

    /// <summary>Position from evaluator output. Null when the catalog has no main scenario quests at all.</summary>
    public MsqPosition? Position(IReadOnlyDictionary<uint, QuestEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        return Position(new EvaluationSource(evaluations));
    }

    /// <summary>Position from a plain state map; missing rows read as <see cref="QuestState.Unknown"/>, which counts as not done.</summary>
    public MsqPosition? Position(IReadOnlyDictionary<uint, QuestState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        return Position(new StateMapSource(states));
    }

    /// <summary>The position over any state source; see the class summary.</summary>
    internal MsqPosition? Position<TSource>(TSource source)
        where TSource : struct, IStateSource
    {
        if (Story.Count == 0)
        {
            return null;
        }

        // Routed regions' join state, worked out once per call (none exist before 8.0 data).
        Dictionary<MsqBranch, bool>? joinMet = null;
        bool JoinMet(MsqBranch branch)
        {
            joinMet ??= [];
            if (!joinMet.TryGetValue(branch, out var met))
            {
                joinMet[branch] = met = IsJoinMet(branch, source);
            }

            return met;
        }

        QuestRecord? first = null;
        var firstState = QuestState.Completed;
        var done = 0;
        var total = 0;
        foreach (var quest in Story)
        {
            if (source.LeavesTotals(quest.RowId))
            {
                // A branch the character did not take (or a quest out of season); neither pending nor countable.
                continue;
            }

            var state = source.StateOf(quest.RowId);
            if (state != QuestState.Completed
                && routedRoute.ContainsKey(quest.RowId)
                && routedRegion[quest.RowId] is var region
                && JoinMet(region))
            {
                // An optional leftover of a met join (another route of an Any join, or a quest the reconvergence
                // quest did not need): the story has moved on without it.
                continue;
            }

            total++;
            if (state == QuestState.Completed)
            {
                done++;
            }
            else if (first is null)
            {
                first = quest;
                firstState = state;
            }
        }

        if (first is null || routedRegion.GetValueOrDefault(first.RowId) is not { } branch || JoinMet(branch))
        {
            return new MsqPosition(first, firstState, done, total);
        }

        var routes = new MsqRouteProgress[branch.Routes.Count];
        MsqRouteProgress? primary = null;
        for (var i = 0; i < routes.Length; i++)
        {
            routes[i] = Progress(branch.Routes[i], source);
            primary ??= routes[i].Next is not null ? routes[i] : null;
        }

        var next = primary?.Next ?? first;
        var nextState = primary?.Next is not null ? primary.State : firstState;
        return new MsqPosition(next, nextState, done, total)
        {
            Branch = branch,
            Routes = routes,
            RoutesToJoin = RoutesToJoin(branch, routes),
        };
    }

    /// <summary>One route's progress; see <see cref="MsqRouteProgress"/>.</summary>
    internal static MsqRouteProgress Progress<TSource>(MsqRoute route, TSource source)
        where TSource : struct, IStateSource
    {
        QuestRecord? next = null;
        var nextState = QuestState.Completed;
        var done = 0;
        var total = 0;
        var started = false;
        foreach (var quest in route.Quests)
        {
            if (source.LeavesTotals(quest.RowId))
            {
                continue;
            }

            var state = source.StateOf(quest.RowId);
            total++;
            if (state == QuestState.Completed)
            {
                done++;
                started = true;
            }
            else
            {
                started |= state == QuestState.Accepted;
                if (next is null)
                {
                    next = quest;
                    nextState = state;
                }
            }
        }

        var status = total == 0 ? MsqRouteStatus.LockedOut
            : done == total ? MsqRouteStatus.Done
            : started ? MsqRouteStatus.InProgress
            : MsqRouteStatus.NotStarted;
        return new MsqRouteProgress(route, next, nextState, done, total, status);
    }

    /// <summary>How many more routes must be done before the reconvergence quest opens (0 once it has).</summary>
    private static int RoutesToJoin(MsqBranch branch, IReadOnlyList<MsqRouteProgress> routes)
    {
        var open = 0;
        var anyDone = false;
        foreach (var route in routes)
        {
            anyDone |= route.Status == MsqRouteStatus.Done;
            open += route.Status is MsqRouteStatus.NotStarted or MsqRouteStatus.InProgress ? 1 : 0;
        }

        return branch.JoinKind == JoinKind.Any ? (anyDone || open == 0 ? 0 : 1) : open;
    }

    /// <summary>
    /// The region's join is met: the reconvergence quest is completed, in the journal or open (Ready, or ready on
    /// another job), or the routes it needs are done (every route not locked out for an All join, one route for an
    /// Any join).
    /// <para>
    /// The reconvergence quest's own state comes first: the evaluator has already judged its previous quests the way
    /// the game does, while a route here is every quest that leads to it, so a route holding a quest the game does not
    /// need (one side of an Any fork inside the route) never reads done.
    /// </para>
    /// </summary>
    private static bool IsJoinMet<TSource>(MsqBranch branch, TSource source)
        where TSource : struct, IStateSource
    {
        if (source.StateOf(branch.Join.RowId) is QuestState.Completed or QuestState.Accepted or QuestState.Ready or QuestState.ReadyOnOtherJob)
        {
            return true;
        }

        var anyDone = false;
        var allDone = true;
        foreach (var route in branch.Routes)
        {
            switch (Progress(route, source).Status)
            {
                case MsqRouteStatus.Done:
                    anyDone = true;
                    break;
                case MsqRouteStatus.LockedOut:
                    break;
                default:
                    allDone = false;
                    break;
            }
        }

        return branch.JoinKind == JoinKind.Any ? anyDone : allDone;
    }

    /// <summary>
    /// Every story quest's place in a topological order of the graph (Kahn's algorithm, journal order between
    /// equals). Today's data needs no reordering (no quest needs a later one); a cycle is broken by appending what is
    /// left in journal order.
    /// </summary>
    private Dictionary<uint, int> TopologicalOrder()
    {
        var pending = new Dictionary<uint, int>(Story.Count);
        var ready = new PriorityQueue<QuestRecord, int>();
        foreach (var quest in Story)
        {
            var count = predecessors[quest.RowId].Count;
            pending[quest.RowId] = count;
            if (count == 0)
            {
                ready.Enqueue(quest, storyIndex[quest.RowId]);
            }
        }

        var order = new Dictionary<uint, int>(Story.Count);
        while (order.Count < Story.Count)
        {
            if (ready.Count == 0)
            {
                foreach (var quest in Story)
                {
                    if (!order.ContainsKey(quest.RowId))
                    {
                        ready.Enqueue(quest, storyIndex[quest.RowId]);
                        break;
                    }
                }
            }

            var next = ready.Dequeue();
            if (!order.TryAdd(next.RowId, order.Count))
            {
                continue;
            }

            foreach (var after in Successors(next.RowId))
            {
                if (--pending[after.RowId] == 0 && !order.ContainsKey(after.RowId))
                {
                    ready.Enqueue(after, storyIndex[after.RowId]);
                }
            }
        }

        return order;
    }

    private HashSet<uint> Reach(uint from, Dictionary<uint, List<QuestRecord>> edges)
    {
        var seen = new HashSet<uint> { from };
        var stack = new Stack<uint>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            if (!edges.TryGetValue(stack.Pop(), out var list))
            {
                continue;
            }

            foreach (var quest in list)
            {
                if (seen.Add(quest.RowId))
                {
                    stack.Push(quest.RowId);
                }
            }
        }

        return seen;
    }

    private List<MsqBranch> FindBranches(Dictionary<uint, int> topo, byte routedFromExpansion)
    {
        var found = new List<MsqBranch>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var descendants = new Dictionary<uint, HashSet<uint>>();
        HashSet<uint> Descendants(uint rowId)
        {
            if (!descendants.TryGetValue(rowId, out var set))
            {
                descendants[rowId] = set = Reach(rowId, successors);
            }

            return set;
        }

        var starts = Story.OrderBy(q => topo[q.RowId]).ToList();
        foreach (var start in starts)
        {
            var after = Successors(start.RowId);
            if (after.Count < 2)
            {
                continue;
            }

            // A successor another successor already leads to is a shortcut edge, not a route of its own.
            var heads = new List<QuestRecord>();
            foreach (var candidate in after)
            {
                var shortcut = false;
                foreach (var other in after)
                {
                    if (other.RowId != candidate.RowId && Descendants(other.RowId).Contains(candidate.RowId))
                    {
                        shortcut = true;
                        break;
                    }
                }

                if (!shortcut)
                {
                    heads.Add(candidate);
                }
            }

            if (heads.Count < 2)
            {
                continue;
            }

            // The reconvergence quest: the earliest quest (in play order) every head leads to.
            QuestRecord? join = null;
            foreach (var candidate in Descendants(heads[0].RowId))
            {
                var everywhere = true;
                for (var i = 1; i < heads.Count && everywhere; i++)
                {
                    everywhere = Descendants(heads[i].RowId).Contains(candidate);
                }

                if (everywhere && (join is null || topo[candidate] < topo[join.RowId]))
                {
                    join = Story[storyIndex[candidate]];
                }
            }

            if (join is null)
            {
                continue;
            }

            var leadsToJoin = Reach(join.RowId, predecessors);
            var groups = new List<HashSet<uint>>();
            foreach (var head in heads)
            {
                var route = new HashSet<uint>();
                foreach (var id in Descendants(head.RowId))
                {
                    if (id != join.RowId && leadsToJoin.Contains(id))
                    {
                        route.Add(id);
                    }
                }

                // Routes that share a quest before the reconvergence are one route.
                for (var g = groups.Count - 1; g >= 0; g--)
                {
                    if (groups[g].Overlaps(route))
                    {
                        route.UnionWith(groups[g]);
                        groups.RemoveAt(g);
                    }
                }

                groups.Add(route);
            }

            if (groups.Count < 2)
            {
                continue;
            }

            var routes = groups
                .Select(g => g.Select(id => Story[storyIndex[id]]).OrderBy(q => topo[q.RowId]).ToArray())
                .OrderBy(quests => storyIndex[quests[0].RowId])
                .Select((quests, index) => new MsqRoute(index, quests[0], quests))
                .ToArray();
            var key = join.RowId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + string.Join(',', routes.Select(r => r.First.RowId));
            if (!keys.Add(key))
            {
                continue;
            }

            var routed = routes.All(r => r.First.Expansion >= routedFromExpansion);
            found.Add(new MsqBranch(start, join, join.PreviousQuests.Join, routes, routed));
        }

        // A routed region inside a route of a larger routed region belongs to that route.
        var bySize = found.Where(b => b.IsRouted).OrderByDescending(b => b.Routes.Sum(r => r.Quests.Count)).ToList();
        var claimed = new HashSet<uint>();
        var nested = new HashSet<MsqBranch>(ReferenceEqualityComparer.Instance);
        foreach (var branch in bySize)
        {
            var quests = branch.Routes.SelectMany(r => r.Quests).Select(q => q.RowId).ToList();
            if (quests.Any(claimed.Contains) || claimed.Contains(branch.Join.RowId))
            {
                nested.Add(branch);
                continue;
            }

            claimed.UnionWith(quests);
        }

        return found.Select(b => nested.Contains(b) ? b with { IsRouted = false } : b).ToList();
    }
}
