using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Route;

/// <summary>
/// "Copy route": the route as a Markdown list to paste into a note, a spreadsheet cell or a Discord message. Quest
/// names come through <c>questName</c>, so the spoiler shield masks them as everywhere else; nothing identifies the
/// character (no name, world or content id), and states are left out since they belong to one character.
/// <code>
/// **Route to Blue Mage** · 3 quests · Lv 49–50 · MSQ: Seventh Umbral Era
///
/// *Main scenario: Seventh Umbral Era*
/// 1. Lv 49 · The Ultimate Weapon (MSQ)
/// 2. Lv 50 · Out of the Blue — target
/// </code>
/// </summary>
public static class RouteMarkdown
{
    /// <summary>The Markdown text of <paramref name="route"/>; one line saying so for an empty route.</summary>
    /// <param name="questName">A quest's printed name (the plugin passes the spoiler shield's).</param>
    public static string Write(UnlockRoute route, QuestCatalog catalog, Func<QuestRecord, string> questName)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(questName);

        var sb = new StringBuilder();
        sb.Append("**Route to ").Append(Escape(route.Target.Label.Length > 0 ? route.Target.Label : "quest")).Append("**");
        switch (route.Outcome)
        {
            case RouteOutcome.AlreadyDone:
                return sb.Append(" · already unlocked").ToString();
            case RouteOutcome.NoQuest:
                return sb.Append(" · no quest known to unlock it").ToString();
        }

        sb.Append(" · ").Append(route.Summary.Text);
        if (route.Outcome == RouteOutcome.LockedOut)
        {
            sb.Append(" · includes a quest that is locked out");
        }

        sb.Append('\n');
        RouteMilestone? milestone = null;
        var first = true;
        for (var i = 0; i < route.Steps.Count; i++)
        {
            var step = route.Steps[i];
            if (first || !ReferenceEquals(step.Milestone, milestone))
            {
                milestone = step.Milestone;
                sb.Append('\n').Append('*').Append(milestone is null ? "After the main scenario" : "Main scenario: " + Escape(milestone.Name)).Append("*\n");
                first = false;
            }

            var name = catalog.GetByRowId(step.RowId) is { } quest ? questName(quest) : "quest " + step.RowId.ToString(CultureInfo.InvariantCulture);
            sb.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(". Lv ").Append(step.DisplayLevel.ToString(CultureInfo.InvariantCulture))
                .Append(" · ").Append(Escape(name));
            if (step.IsMainScenario)
            {
                sb.Append(" (MSQ)");
            }

            if (step.IsTarget)
            {
                sb.Append(" — target");
            }

            sb.Append('\n');
            foreach (var alternative in step.Alternatives)
            {
                var other = catalog.GetByRowId(alternative.RowId) is { } q ? questName(q) : "quest " + alternative.RowId.ToString(CultureInfo.InvariantCulture);
                sb.Append("   - or instead: ").Append(Escape(other)).Append(" (")
                    .Append(alternative.RemainingCount.ToString(CultureInfo.InvariantCulture))
                    .Append(alternative.RemainingCount == 1 ? " quest)" : " quests)").Append('\n');
            }
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Backslashes the characters Markdown would read as emphasis, links or code in a quest name.</summary>
    private static string Escape(string text)
    {
        if (text.AsSpan().IndexOfAny("*_`[]\\<>#|") < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            if (c is '*' or '_' or '`' or '[' or ']' or '\\' or '<' or '>' or '#' or '|')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
