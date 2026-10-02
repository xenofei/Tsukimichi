using System.Globalization;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Route;

/// <summary>
/// What the Todo overlay's route section shows (1.6.0, R6 A): the next open steps, how many more follow, and the step
/// whose level gate the character has not met yet among them (the "level 52 needed" line); null when none is.
/// </summary>
public sealed record RouteGlance(IReadOnlyList<RouteStep> Next, int More, RouteStep? Gate)
{
    public static readonly RouteGlance Empty = new([], 0, null);

    /// <summary>"Level 52 needed from step 2 (you are 48)"; empty when no shown step waits on a level.</summary>
    public string GateText
    {
        get
        {
            if (Gate is not { } gate)
            {
                return string.Empty;
            }

            var number = 1;
            for (var i = 0; i < Next.Count; i++)
            {
                if (ReferenceEquals(Next[i], gate))
                {
                    number = i + 1;
                    break;
                }
            }

            return gate.CharacterLevel > 0
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Route.GateLine", "Level {0} needed from step {1} (you are {2})"), gate.Level, number, gate.CharacterLevel)
                : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Route.GateLineNoLevel", "Level {0} needed from step {1}"), gate.Level, number);
        }
    }
}

/// <summary>
/// The route a player follows (1.6.0, R6 A, C3 C): what the overlay shows of it and which step's giver is the next
/// stop. A route never lists a completed quest, so rebuilding it from new states is what advances it.
/// </summary>
public static class ActiveRoute
{
    /// <summary>Steps the overlay shows; the rest are one "N more" line.</summary>
    public const int Shown = 3;

    /// <summary>The first <paramref name="take"/> steps, the count of the rest and the first of them still below its level gate.</summary>
    public static RouteGlance Glance(UnlockRoute route, int take = Shown)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.Steps.Count == 0 || take <= 0)
        {
            return route.Steps.Count == 0 ? RouteGlance.Empty : new RouteGlance([], route.Steps.Count, null);
        }

        var count = Math.Min(take, route.Steps.Count);
        var next = new RouteStep[count];
        RouteStep? gate = null;
        for (var i = 0; i < count; i++)
        {
            next[i] = route.Steps[i];
            if (gate is null && !next[i].LevelMet)
            {
                gate = next[i];
            }
        }

        return new RouteGlance(next, route.Steps.Count - count, gate);
    }

    /// <summary>
    /// The step whose giver is the next place to go: the first step not in the journal yet (an accepted quest's giver
    /// is behind the player), or the first step when every one is accepted; null for an empty route.
    /// </summary>
    public static RouteStep? NextStop(UnlockRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        foreach (var step in route.Steps)
        {
            if (step.State != QuestState.Accepted)
            {
                return step;
            }
        }

        return route.Steps.Count > 0 ? route.Steps[0] : null;
    }

    /// <summary>"Route complete: everything for Dragoon." for the chat line printed when a followed route runs out.</summary>
    public static string CompletedLine(string label) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Route.Completed", "Route complete: {0}."), label ?? string.Empty);
}

/// <summary>What changed on a followed route since the last look (<see cref="RouteFollower.Update"/>).</summary>
public enum RouteProgressKind : byte
{
    /// <summary>Nothing to act on.</summary>
    Unchanged,

    /// <summary>The first look at the route since it was followed (or since the plugin loaded): nothing is flagged.</summary>
    Started,

    /// <summary>A step was completed and the next stop is another quest: the flag may move to it.</summary>
    Advanced,

    /// <summary>The route ran out: everything on it is done.</summary>
    Finished,

    /// <summary>
    /// No quest leads to the target any more (the catalog changed), or every part left is locked out; the route is
    /// dropped quietly.
    /// </summary>
    Lost,
}

/// <summary>The outcome of one look at a followed route; <paramref name="NextStop"/> is the quest to flag on <see cref="RouteProgressKind.Advanced"/>.</summary>
public readonly record struct RouteProgress(RouteProgressKind Kind, uint? NextStop);

/// <summary>
/// Follows one route as it is rebuilt from new states (1.6.0): says when a step was completed and the next stop moved
/// (so the map flag can follow, C3 C), and when the route ran out. Only a completion moves the stop: accepting the
/// head quest changes the next stop too, but the flag waits until a quest is turned in. Pure; one per followed route.
/// </summary>
public sealed class RouteFollower
{
    private bool started;
    private bool finished;
    private int count;
    private uint? stop;

    /// <summary>Looks at the route as rebuilt now.</summary>
    public RouteProgress Update(UnlockRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (finished)
        {
            return new RouteProgress(RouteProgressKind.Unchanged, null);
        }

        // No quest leads there any more, or what is left of it is locked out (a route to several targets whose
        // remaining parts are all foreclosed): never "complete", the route is dropped quietly.
        if (route.Outcome == RouteOutcome.NoQuest || (route.Outcome == RouteOutcome.LockedOut && route.Steps.Count == 0))
        {
            finished = true;
            return new RouteProgress(RouteProgressKind.Lost, null);
        }

        if (route.Outcome == RouteOutcome.AlreadyDone || route.Steps.Count == 0)
        {
            finished = true;
            return new RouteProgress(RouteProgressKind.Finished, null);
        }

        var next = ActiveRoute.NextStop(route)?.RowId;
        if (!started)
        {
            started = true;
            count = route.Steps.Count;
            stop = next;
            return new RouteProgress(RouteProgressKind.Started, next);
        }

        var completed = route.Steps.Count < count;
        count = route.Steps.Count;
        if (completed && next is { } moved && moved != stop)
        {
            stop = moved;
            return new RouteProgress(RouteProgressKind.Advanced, moved);
        }

        return new RouteProgress(RouteProgressKind.Unchanged, null);
    }
}

/// <summary>Consecutive route steps whose givers share one aetheryte: one stop ("3 quests near Camp Dragonhead").</summary>
/// <param name="Start">Index of the stop's first step in <see cref="UnlockRoute.Steps"/>.</param>
/// <param name="Count">Steps in the stop (1 for a step alone).</param>
/// <param name="AetheryteId">The aetheryte nearest every giver of the stop; 0 for a step with none.</param>
public readonly record struct RouteStop(int Start, int Count, uint AetheryteId);

/// <summary>Merges a route's steps into stops (1.6.0, R6 A) without ever reordering them.</summary>
public static class RouteStops
{
    /// <summary>
    /// The steps in order, runs of consecutive steps with the same non-zero aetheryte merged: the route's order (its
    /// dependencies) is kept, so two visits to one place with something elsewhere between stay two stops.
    /// </summary>
    /// <param name="aetheryteOf">The aetheryte nearest a quest's giver by row id; 0 when it has none.</param>
    public static IReadOnlyList<RouteStop> Group(IReadOnlyList<RouteStep> steps, Func<uint, uint> aetheryteOf)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(aetheryteOf);
        var stops = new List<RouteStop>();
        var i = 0;
        while (i < steps.Count)
        {
            var place = aetheryteOf(steps[i].RowId);
            var end = i + 1;
            while (place != 0 && end < steps.Count && aetheryteOf(steps[end].RowId) == place)
            {
                end++;
            }

            stops.Add(new RouteStop(i, end - i, place));
            i = end;
        }

        return stops;
    }
}
