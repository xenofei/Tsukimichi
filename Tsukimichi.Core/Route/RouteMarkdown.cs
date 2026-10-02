using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Localization;

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
        var target = Escape(route.Target.Label.Length > 0 ? route.Target.Label : CoreText.T("Core.Route.QuestFallback", "quest"));
        sb.Append("**").Append(string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Route.Title", "Route to {0}"), target)).Append("**");
        switch (route.Outcome)
        {
            case RouteOutcome.AlreadyDone:
                return sb.Append(" · ").Append(CoreText.T("Core.Route.AlreadyUnlocked", "already unlocked")).ToString();
            case RouteOutcome.NoQuest:
                return sb.Append(" · ").Append(CoreText.T("Core.Route.NoQuest", "no quest known to unlock it")).ToString();
        }

        sb.Append(" · ").Append(route.Summary.Text);
        if (route.Outcome == RouteOutcome.LockedOut)
        {
            sb.Append(" · ").Append(CoreText.T("Core.Route.IncludesLockedOut", "includes a quest that is locked out"));
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
                sb.Append('\n').Append('*').Append(milestone is null
                    ? CoreText.T("Core.Route.AfterMainScenario", "After the main scenario")
                    : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Route.Milestone", "Main scenario: {0}"), Escape(milestone.Name))).Append("*\n");
                first = false;
            }

            var name = catalog.GetByRowId(step.RowId) is { } quest ? questName(quest) : QuestId(step.RowId);
            sb.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(". ")
                .Append(string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Route.Level", "Lv {0}"), step.DisplayLevel))
                .Append(" · ").Append(Escape(name));
            if (step.IsMainScenario)
            {
                sb.Append(' ').Append(CoreText.T("Core.Route.MsqMark", "(MSQ)"));
            }

            if (step.TargetLabel.Length > 0)
            {
                // A route to several targets names the milestones the step reaches ("Dragoon quests").
                sb.Append(" — ").Append(Escape(step.TargetLabel));
            }
            else if (step.IsTarget)
            {
                sb.Append(" — ").Append(CoreText.T("Core.Route.TargetMark", "target"));
            }

            sb.Append('\n');
            foreach (var alternative in step.Alternatives)
            {
                var other = catalog.GetByRowId(alternative.RowId) is { } q ? questName(q) : QuestId(alternative.RowId);
                var format = alternative.RemainingCount == 1
                    ? CoreText.T("Core.Route.OrInsteadOne", "or instead: {0} ({1} quest)")
                    : CoreText.T("Core.Route.OrInstead", "or instead: {0} ({1} quests)");
                sb.Append("   - ").Append(string.Format(CultureInfo.CurrentCulture, format, Escape(other), alternative.RemainingCount)).Append('\n');
            }
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static string QuestId(uint rowId) =>
        string.Format(CultureInfo.InvariantCulture, CoreText.T("Core.Route.QuestId", "quest {0}"), rowId);

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
