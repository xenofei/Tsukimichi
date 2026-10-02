using System.Globalization;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki recap [quest name]</c> (feature plan v5, collector extras): opens the story recap ("Previously…") on the
/// last main scenario quests the viewed character completed, or, with a quest name, on the story chain that quest
/// belongs to. Name resolution is <see cref="ReportCommand.FindByName"/> through the viewed character's spoiler
/// shield; a miss, or a quest on no chain, says so in chat.
/// </summary>
public sealed class RecapCommand(SessionState session, UiState ui, GameLinks links)
{
    public void Run(string name)
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            ui.OpenRecap(RecapRequest.MainScenario);
            return;
        }

        if (ReportCommand.FindByName(bundle.Catalog, text, session.Spoilers) is not { } quest)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ReportNoMatchFormat, text));
            return;
        }

        if (session.Chains.ForQuest(quest.RowId) is null)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.RecapNoChainFormat, session.Spoilers.DisplayName(quest)));
            return;
        }

        ui.OpenRecap(new RecapRequest(quest.RowId));
    }
}
