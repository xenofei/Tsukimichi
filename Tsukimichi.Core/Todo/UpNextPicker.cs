using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Todo;

/// <summary>Which rule of Up next's order chose its quest (spec-1.21 P1, "The pick").</summary>
public enum UpNextRule : byte
{
    /// <summary>The next stop of the route the character follows.</summary>
    Route,

    /// <summary>The character's alt goal (1.21.0 N11): its first quest the character can do now.</summary>
    Goal,

    /// <summary>The next main scenario quest, Ready or in the journal.</summary>
    MainScenario,

    /// <summary>The first pinned quest that is Ready.</summary>
    Pinned,

    /// <summary>The closest Ready quest of Next stops.</summary>
    ClosestStop,

    /// <summary>The next story quest waits for a level: the level gate.</summary>
    LevelGate,
}

/// <summary>Up next's one quest and the rule that chose it.</summary>
public readonly record struct UpNextPick(uint RowId, UpNextRule Rule);

/// <summary>
/// What Up next shows at the top of Tonight (plan v7, 1.21.0 P1; spec-1.21 P1 and decision 1), and what
/// <c>/tsuki next</c> and "Say what's next in chat" speak about: the first rule in <see cref="Order"/> that has a quest
/// for the character. The followed route's next stop and the goal's quest count while they are Ready or in the journal;
/// the main scenario's next quest likewise; a pin and a Next stop only while Ready; the level gate is the next story
/// quest itself, which waits for a level. Every quest it picks is one the character can act on now (or the level gate's
/// story quest), so it is never past the story point; the reason line's words carry the shield where a target lies
/// further on. Pure.
/// </summary>
public static class UpNextPicker
{
    /// <summary>The order, as the reason line's hover lists it (settled 3 October 2026: the goal comes after a route you chose).</summary>
    public static readonly IReadOnlyList<UpNextRule> Order =
    [
        UpNextRule.Route,
        UpNextRule.Goal,
        UpNextRule.MainScenario,
        UpNextRule.Pinned,
        UpNextRule.ClosestStop,
        UpNextRule.LevelGate,
    ];

    /// <param name="states">The character's quest states.</param>
    /// <param name="routeNext">The followed route's next stop; null when no route is followed.</param>
    /// <param name="goal">The goal's quests left, the ones the character can do now first; empty without a goal.</param>
    /// <param name="msqNext">The next main scenario quest; null once caught up.</param>
    /// <param name="pins">The pinned quests, in pin order.</param>
    /// <param name="closest">Next stops' quests, closest stop first.</param>
    /// <param name="levelGate">The next story quest when it waits only for a level; null otherwise.</param>
    public static UpNextPick? Pick(
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        uint? routeNext,
        IEnumerable<uint> goal,
        uint? msqNext,
        IEnumerable<uint> pins,
        IEnumerable<uint> closest,
        uint? levelGate)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(closest);
        if (routeNext is { } route && Actionable(states, route))
        {
            return new UpNextPick(route, UpNextRule.Route);
        }

        foreach (var rowId in goal)
        {
            if (Actionable(states, rowId))
            {
                return new UpNextPick(rowId, UpNextRule.Goal);
            }
        }

        if (msqNext is { } msq && Actionable(states, msq))
        {
            return new UpNextPick(msq, UpNextRule.MainScenario);
        }

        foreach (var pin in pins)
        {
            if (Is(states, pin, QuestState.Ready))
            {
                return new UpNextPick(pin, UpNextRule.Pinned);
            }
        }

        foreach (var stop in closest)
        {
            if (Is(states, stop, QuestState.Ready))
            {
                return new UpNextPick(stop, UpNextRule.ClosestStop);
            }
        }

        return levelGate is { } gate ? new UpNextPick(gate, UpNextRule.LevelGate) : null;
    }

    /// <summary>
    /// The Ready quests in Tonight's order (1.22.0, IPC <c>GetReadyTonight</c>): Up next's <see cref="Order"/> over every
    /// quest (the followed route's next stop, the goal's quests, the next main scenario quest, the pins in pin order,
    /// Next stops' quests closest first), then every other Ready quest in <paramref name="rest"/>'s order. Each quest
    /// once, Ready ones only, at most <paramref name="max"/>; empty for a <paramref name="max"/> of 0 or less.
    /// </summary>
    /// <param name="rest">Every quest, in the journal's order (the catalog's).</param>
    public static IReadOnlyList<uint> ReadyInOrder(
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        uint? routeNext,
        IEnumerable<uint> goal,
        uint? msqNext,
        IEnumerable<uint> pins,
        IEnumerable<uint> closest,
        IEnumerable<uint> rest,
        int max)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(closest);
        ArgumentNullException.ThrowIfNull(rest);
        var picked = new List<uint>(Math.Clamp(max, 0, 64));
        if (max <= 0)
        {
            return picked;
        }

        var seen = new HashSet<uint>();
        bool Add(uint rowId)
        {
            if (Is(states, rowId, QuestState.Ready) && seen.Add(rowId))
            {
                picked.Add(rowId);
            }

            return picked.Count >= max;
        }

        if (routeNext is { } route && Add(route))
        {
            return picked;
        }

        foreach (var source in (IEnumerable<uint>[])[goal, msqNext is { } msq ? [msq] : [], pins, closest, rest])
        {
            foreach (var rowId in source)
            {
                if (Add(rowId))
                {
                    return picked;
                }
            }
        }

        return picked;
    }

    /// <summary>Ready or in the journal: what a route stop, a goal quest and the main scenario need to be picked.</summary>
    public static bool Actionable(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        Is(states, rowId, QuestState.Ready) || Is(states, rowId, QuestState.Accepted);

    private static bool Is(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId, QuestState state) =>
        states.TryGetValue(rowId, out var evaluation) && evaluation.State == state;
}
