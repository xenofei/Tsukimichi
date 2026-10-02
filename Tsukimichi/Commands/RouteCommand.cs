using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki route [quest name]</c> (1.7.0): opens the unlock route window on the named quest (the selected one when no
/// name is given) for the viewed character, as the detail pane's Route to this does. Name resolution is
/// <see cref="ReportCommand.FindByName"/> through the viewed character's spoiler shield, since the route window shows
/// that character; a miss or no selection says so in chat.
/// </summary>
public sealed class RouteCommand(SessionState session, UiState ui, GameLinks links)
{
    public void Run(string name)
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        var text = (name ?? string.Empty).Trim();
        QuestRecord? quest;
        if (text.Length == 0)
        {
            quest = ui.SelectedRowId is { } rowId ? bundle.Catalog.GetByRowId(rowId) : null;
            if (quest is null)
            {
                links.PrintText(Strings.RouteCommandNoSelection);
                return;
            }
        }
        else
        {
            quest = ReportCommand.FindByName(bundle.Catalog, text, session.Spoilers);
            if (quest is null)
            {
                links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ReportNoMatchFormat, text));
                return;
            }
        }

        ui.OpenRoute(Core.Route.RouteTarget.ForQuest(quest.RowId, session.Spoilers.DisplayName(quest)));
    }
}
