using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Where a character stands in the main scenario: one quest on a linear stretch, or one entry per route inside a
/// routed branch region (<see cref="MsqGraph"/>).
/// </summary>
/// <param name="Next">
/// The primary position: on a linear stretch the first main scenario quest, in journal order, that is not completed;
/// inside a branch region the first route's next quest (<see cref="Routes"/>). Null once every quest is done.
/// </param>
/// <param name="State">Resolved state of <paramref name="Next"/>; <see cref="QuestState.Completed"/> when there is no next quest.</param>
/// <param name="Done">Completed main scenario quests.</param>
/// <param name="Total">Main scenario quests the character can still do or has done; foreclosed branches (the other two Grand Company choices, say), out-of-season quests and the optional routes of a met Any join are left out.</param>
public sealed record MsqPosition(QuestRecord? Next, QuestState State, int Done, int Total)
{
    public bool IsComplete => Next is null;

    /// <summary>The routed branch region the character is inside; null on a linear stretch.</summary>
    public MsqBranch? Branch { get; init; }

    /// <summary>
    /// Inside a branch region, every route's progress in route order (done and not started ones included); empty on
    /// a linear stretch.
    /// </summary>
    public IReadOnlyList<MsqRouteProgress> Routes { get; init; } = NoRoutes;

    /// <summary>
    /// Inside a branch region, how many more routes must be done before the reconvergence quest
    /// (<see cref="MsqBranch.Join"/>) opens: every open route for an All join, one for an Any join. 0 on a linear stretch.
    /// </summary>
    public int RoutesToJoin { get; init; }

    /// <summary>Whether the position is reported route by route.</summary>
    public bool IsBranched => Routes.Count > 0;

    /// <summary>
    /// Every position: <see cref="Next"/> alone on a linear stretch, each unfinished route's next quest in route
    /// order inside a branch region, nothing once the story is complete.
    /// </summary>
    public IReadOnlyList<QuestRecord> Positions
    {
        get
        {
            if (!IsBranched)
            {
                return Next is { } next ? [next] : [];
            }

            var list = new List<QuestRecord>(Routes.Count);
            foreach (var route in Routes)
            {
                if (route.Next is { } routeNext)
                {
                    list.Add(routeNext);
                }
            }

            return list;
        }
    }

    private static readonly IReadOnlyList<MsqRouteProgress> NoRoutes = [];
}

/// <summary>
/// Finds the main scenario position (<see cref="MsqGraph.Position(IReadOnlyDictionary{uint, QuestState})"/>): sections
/// 0 (A Realm Reborn through Endwalker) and 1 (Dawntrail) walked in journal order, stopping at the first quest that is
/// neither completed nor foreclosed, reported route by route inside a routed branch region. The journal's own "hide"
/// state plays no part, so a new player who lost the MSQ under the five-quest display finds it here. Pure; the graph is
/// cached with the catalog and the caller memoizes per session version.
/// </summary>
public static class MsqProgress
{
    /// <summary>JournalSection ids holding main scenario quests, in play order.</summary>
    public static readonly IReadOnlyList<uint> MainScenarioSections = [0, 1];

    /// <summary>Position from evaluator output. Returns null when the catalog has no main scenario quests at all.</summary>
    public static MsqPosition? Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return MsqGraph.For(catalog).Position(evaluations);
    }

    /// <summary>Position from a plain state map; missing rows read as <see cref="QuestState.Unknown"/>, which counts as not done.</summary>
    public static MsqPosition? Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return MsqGraph.For(catalog).Position(states);
    }
}
