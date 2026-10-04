using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// What <c>/tsuki msq</c> says (plan v7, 1.21.0 P8): the next main scenario quest, the journal part it belongs to
/// (<see cref="JournalRef.GenreName"/>: "Dawntrail", "The New Dawn"), how many main scenario quests are left in that
/// part and to the latest story, and their level span. Counts say what is left, never done/total.
/// </summary>
public sealed record MsqLeft(QuestRecord Next, string Part, int LeftInPart, int LeftToLatest, int MinLevel, int MaxLevel)
{
    /// <summary>
    /// The summary for a character's states; null once the story is caught up (or the catalog has no main scenario).
    /// What is left follows <see cref="MsqGraph.QuestsLeft"/>, the position's own rule: a spare alternative (the Grand
    /// Companies' choices before one is made), a foreclosed quest or an optional leftover of a met join is not left.
    /// </summary>
    public static MsqLeft? For(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        if (MsqProgress.Compute(catalog, states) is not { Next: { } next })
        {
            return null;
        }

        var inPart = 0;
        var left = 0;
        var min = int.MaxValue;
        var max = 0;
        foreach (var quest in MsqGraph.For(catalog).QuestsLeft(states))
        {
            left++;
            if (quest.Journal.GenreId == next.Journal.GenreId)
            {
                inPart++;
            }

            min = Math.Min(min, quest.DisplayLevel);
            max = Math.Max(max, quest.DisplayLevel);
        }

        return new MsqLeft(next, next.Journal.GenreName, inPart, left, min == int.MaxValue ? next.DisplayLevel : min, Math.Max(max, next.DisplayLevel));
    }
}

/// <summary>Why <see cref="GuidancePick"/> chose its quest.</summary>
public enum GuidanceReason : byte
{
    /// <summary>The next stop of the route the character follows.</summary>
    Route,

    /// <summary>The next main scenario quest, Ready or in the journal.</summary>
    MainScenario,

    /// <summary>The first pinned quest that is Ready.</summary>
    Pinned,

    /// <summary>The closest Ready quest of Next stops.</summary>
    ClosestStop,
}

/// <summary>
/// The one quest <c>/tsuki next</c>, <c>/tsuki go</c> without a name and "Say what's next in chat" speak about (P8),
/// by the order of Up next (spec-1.21 P1, decision 1) as far as this release's data reaches: the followed route's next
/// stop, the next main scenario quest when it is Ready or in the journal, the first Ready pin, then the closest Ready
/// quest of Next stops. A route stop or pin counts only while it is Ready or in the journal. Pure.
/// </summary>
public static class GuidancePick
{
    /// <param name="states">The character's quest states.</param>
    /// <param name="routeNext">The followed route's next quest; null when no route is followed.</param>
    /// <param name="msqNext">The next main scenario quest (<see cref="MsqProgress"/>); null once caught up.</param>
    /// <param name="pins">The pinned quests, in pin order.</param>
    /// <param name="closest">Next stops' quests, closest first.</param>
    public static (uint RowId, GuidanceReason Reason)? Pick(
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        uint? routeNext,
        uint? msqNext,
        IEnumerable<uint> pins,
        IEnumerable<uint> closest)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(closest);

        // Up next's picker (1.21.0 P1) holds the order; without a goal or a level gate its rules map one to one.
        return Todo.UpNextPicker.Pick(states, routeNext, [], msqNext, pins, closest, levelGate: null) switch
        {
            { Rule: Todo.UpNextRule.Route } pick => (pick.RowId, GuidanceReason.Route),
            { Rule: Todo.UpNextRule.MainScenario } pick => (pick.RowId, GuidanceReason.MainScenario),
            { Rule: Todo.UpNextRule.Pinned } pick => (pick.RowId, GuidanceReason.Pinned),
            { Rule: Todo.UpNextRule.ClosestStop } pick => (pick.RowId, GuidanceReason.ClosestStop),
            _ => null,
        };
    }

    /// <summary>How many quests are Ready on another job (the "nothing is Ready on this job" line).</summary>
    public static int ReadyOnOtherJob(IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var count = 0;
        foreach (var evaluation in states.Values)
        {
            if (evaluation.State == QuestState.ReadyOnOtherJob)
            {
                count++;
            }
        }

        return count;
    }
}
