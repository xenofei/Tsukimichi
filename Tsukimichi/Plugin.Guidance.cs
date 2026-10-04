using System.Linq;
using Tsukimichi.Commands;
using Tsukimichi.Core.Text;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.21.0 guidance wiring (plan v7 P2 and P8): the current step's objective text for <see cref="GameLinks"/>,
/// <c>/tsuki msq</c>, <c>/tsuki next</c> and <c>/tsuki go</c>, and "Say what's next in chat".
/// </summary>
public sealed partial class Plugin
{
    /// <summary>Wires the guidance commands and the chat line once the panes, the route service and Next stops exist.</summary>
    private void WireGuidance(
        TsukimichiCommand command,
        UiState ui,
        GameLinks links,
        Game.ActiveRouteService routes,
        QueryRunner runner,
        NextStopsSource nextStops,
        Game.ChatNotifier notifier)
    {
        links.QuestText = QuestText;
        // Each input for the one character the line is about (the logged-in one, else the viewed one): its own route,
        // pins and Next stops, never the viewed character's while another is logged in.
        var guidance = new GuidanceCommand(Session, ui, links)
        {
            RouteNext = id => routes.NextStopQuest(routes.RouteOf(id)),
            Pins = id => id == Session.ViewedContentId ? runner.PinnedInOrder : runner.PinsOf(id),
            Closest = id => nextStops.StopsFor(id).SelectMany(static stop => stop.Quests).Select(static q => q.Quest.RowId),
        };
        guidanceCommand = guidance;
        command.Msq = guidance.Msq;
        command.Next = guidance.Next;
        command.Go = guidance.Go;

        // "/tsuki go west" stays the search for Go West, Craftsman, by the names the logged-in character's shield shows.
        command.BeginsQuestName = text => Session.Bundle is { } bundle
            && CommandLine.BeginsQuestName(bundle.Catalog, Session.LiveContentId is not null ? Session.LiveSpoilers : Session.Spoilers, text);
        notifier.NextLine = guidance.NextLine;
        notifier.StepDoneLine = guidance.StepDoneLine;
    }
}
