using System.Globalization;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki recap [quest name]</c> (feature plan v5, collector extras): opens the story recap ("Previously…") on the
/// last main scenario quests the viewed character completed, or, with a quest name, on the story chain that quest
/// belongs to, once the character has completed some quest of it. Name resolution is <see cref="ReportCommand.FindByName"/> through the viewed character's spoiler
/// shield; a miss, a quest on no chain or a story not started yet says so in chat.
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

        if (session.Chains.ForQuest(quest.RowId) is not { } chain)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.RecapNoChainFormat, session.Spoilers.DisplayName(quest)));
            return;
        }

        // Only a story the character has begun has something to recap (the window would open on an empty page).
        if (session.ViewedSnapshot is { } snapshot && !StoryRecap.HasStarted(chain, rowId => snapshot.IsCompleted(QuestRecord.ToQuestId(rowId))))
        {
            links.PrintText(Strings.RecapNotStarted);
            return;
        }

        ui.OpenRecap(new RecapRequest(quest.RowId));
    }
}
