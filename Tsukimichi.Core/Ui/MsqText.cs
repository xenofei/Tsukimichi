using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// How a branched main scenario position (<see cref="MsqPosition.Routes"/>) reads on every surface: the status bar
/// pill ("route The Ember Road 3/9 · route The Glacier Road —"), the dashboard line ("route The Ember Road 3 of 9 ·
/// route The Glacier Road not started · route The Aurora Road done"), one tooltip line per route and the line that
/// says when the routes meet again. Route names are their first quests' names, passed through
/// <c>name</c> (the spoiler shield's display name); a locked-out route is left out everywhere.
/// </summary>
public static class MsqText
{
    public const string RoutePrefix = "route ";
    public const string NotStartedShort = "—";
    public const string DoneShort = "done";
    public const string NotStarted = "not started";
    public const string Done = "done";
    public const string Separator = " · ";

    /// <summary>"3/9", "—" before the route is started, "done" once it is.</summary>
    public static string Short(MsqRouteProgress route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return route.Status switch
        {
            MsqRouteStatus.NotStarted => NotStartedShort,
            MsqRouteStatus.Done => DoneShort,
            _ => string.Format(CultureInfo.InvariantCulture, "{0}/{1}", route.Done, route.Total),
        };
    }

    /// <summary>"3 of 9", "not started", "done".</summary>
    public static string Long(MsqRouteProgress route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return route.Status switch
        {
            MsqRouteStatus.NotStarted => NotStarted,
            MsqRouteStatus.Done => Done,
            _ => string.Format(CultureInfo.CurrentCulture, "{0:N0} of {1:N0}", route.Done, route.Total),
        };
    }

    /// <summary>"route The Ember Road": the route named by its first quest.</summary>
    public static string RouteName(MsqRoute route, Func<QuestRecord, string> name)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(name);
        return RoutePrefix + name(route.First);
    }

    /// <summary>The pill's routes: "route A 3/9 · route B — · route C done". Empty on a linear stretch.</summary>
    public static string Compact(MsqPosition position, Func<QuestRecord, string> name) => Join(position, name, compact: true);

    /// <summary>The dashboard's routes: "route A 3 of 9 · route B not started · route C done". Empty on a linear stretch.</summary>
    public static string Spelled(MsqPosition position, Func<QuestRecord, string> name) => Join(position, name, compact: false);

    /// <summary>
    /// One tooltip line per route: "route A: 3 of 9 · next: Ashes Underfoot (Ready)", "route B: not started · next:
    /// The Glacier Road (Ready)", "route C: done".
    /// </summary>
    public static IReadOnlyList<string> Lines(MsqPosition position, Func<QuestRecord, string> name)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(name);
        var lines = new List<string>(position.Routes.Count);
        foreach (var route in position.Routes)
        {
            if (route.Status == MsqRouteStatus.LockedOut)
            {
                continue;
            }

            var line = RouteName(route.Route, name) + ": " + Long(route);
            if (route.Next is { } next)
            {
                line += Separator + "next: " + name(next) + " (" + StateNames.Name(route.State, next) + ")";
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// "The routes meet again at Where the Roads Meet once all are done" (All join) or "… once one is done" (Any
    /// join), with how many are still needed; empty on a linear stretch.
    /// </summary>
    public static string JoinLine(MsqPosition position, Func<QuestRecord, string> name)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(name);
        if (position.Branch is not { } branch)
        {
            return string.Empty;
        }

        var need = branch.JoinKind == JoinKind.Any ? "once one is done" : "once all are done";
        var left = position.RoutesToJoin == 1 ? "1 route to go" : string.Format(CultureInfo.CurrentCulture, "{0:N0} routes to go", position.RoutesToJoin);
        return "The routes meet again at " + name(branch.Join) + " " + need + " (" + left + ")";
    }

    /// <summary>
    /// A route's label for a row that names its next quest (the Todo overlay, the Tonight card): "route A · 3 of 9",
    /// "route B · not started".
    /// </summary>
    public static string RowPrefix(MsqRouteProgress route, Func<QuestRecord, string> name) =>
        RouteName(route.Route, name) + Separator + Long(route);

    private static string Join(MsqPosition position, Func<QuestRecord, string> name, bool compact)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(name);
        var sb = new StringBuilder();
        foreach (var route in position.Routes)
        {
            if (route.Status == MsqRouteStatus.LockedOut)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(Separator);
            }

            sb.Append(RouteName(route.Route, name)).Append(' ').Append(compact ? Short(route) : Long(route));
        }

        return sb.ToString();
    }
}
