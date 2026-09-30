using System.Globalization;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki why [quest name]</c>: prints to chat why the named quest (the selected one when no name is given) is
/// not offered: the quest link with its state and decisive blocker, one line per requirement in the diagnostic
/// block's words, and the curated quirk note when the quest has one. A Ready quest says whom to talk to, with the
/// giver's zone and coordinates as a map link. Name resolution is <see cref="ReportCommand.FindByName"/>; the lines
/// are composed by <see cref="WhyText"/>.
/// </summary>
public sealed class WhyCommand(SessionState session, UiState ui, GameLinks links)
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
                links.PrintText(Strings.WhyNoSelection);
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

        session.States.TryGetValue(quest.RowId, out var evaluation);
        var states = session.States.Count > 0 ? session.States : null;
        session.Curated.Quirks.TryGetValue(quest.RowId, out var quirk);

        // The giver's zone and coordinates ride on the chat line as a map link, so the headline itself leaves them
        // out; without a map they are named in plain text.
        var linkGiver = evaluation is { State: QuestState.Ready or QuestState.ReadyOnOtherJob } && links.MapLink(quest) is not null;
        string? place = null;
        (float X, float Y)? coordinates = null;
        if (!linkGiver && quest.Issuer is { } issuer)
        {
            place = links.Map(issuer.MapId)?.PlaceName;
            coordinates = links.MapCoordinates(quest) is { } c ? (c.X, c.Y) : null;
        }

        var lines = WhyText.Lines(evaluation, quest, session.Names, states, place, coordinates, quirk?.Note);
        links.PrintHeadline(quest, lines[0], linkGiver);
        for (var i = 1; i < lines.Count; i++)
        {
            links.PrintText(lines[i]);
        }
    }
}
