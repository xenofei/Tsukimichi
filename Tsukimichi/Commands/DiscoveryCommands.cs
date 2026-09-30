using System.Globalization;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki zone</c> and <c>/tsuki which</c>: chat lists of quests the viewed character can start here, or that the
/// targeted NPC hands out. Lookups live in <see cref="QuestDiscovery"/>; nothing in the game is changed.
/// </summary>
public sealed class DiscoveryCommands(SessionState session, IClientState clientState, ITargetManager targets, GameLinks links)
{
    /// <summary>Chat links printed before "and N more".</summary>
    public const int MaxChatMatches = 10;

    /// <summary>Quests whose giver stands in the current territory and that are Ready (on this or another job), by level then name.</summary>
    public void Zone()
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        if (session.ViewedSnapshot is null)
        {
            links.PrintText(Strings.ZoneNoCharacter);
            return;
        }

        var matches = QuestDiscovery.StartableInZone(bundle.Catalog, session.States, clientState.TerritoryType);
        if (matches.Count == 0)
        {
            links.PrintText(Strings.ZoneNoQuests);
            return;
        }

        for (var i = 0; i < matches.Count && i < MaxChatMatches; i++)
        {
            var quest = matches[i];
            session.States.TryGetValue(quest.RowId, out var evaluation);
            links.PrintQuestLink(quest, BlockerText.StatusText(evaluation, quest, session.LiveNames, session.States));
        }

        if (matches.Count > MaxChatMatches)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, matches.Count - MaxChatMatches));
        }
    }

    /// <summary>Quests the targeted NPC issues, each with its state for the viewed character; capped like <see cref="Zone"/>.</summary>
    public void Which()
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        // An event NPC's base id is its ENpcResident row, which is what Quest.IssuerStart names.
        var target = targets.Target;
        if (target is null || target.ObjectKind != ObjectKind.EventNpc || target.BaseId == 0)
        {
            links.PrintText(Strings.WhichNoTarget);
            return;
        }

        var matches = QuestDiscovery.IssuedBy(bundle.Catalog, target.BaseId);
        if (matches.Count == 0)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.WhichNoQuestsFormat, target.Name.TextValue));
            return;
        }

        for (var i = 0; i < matches.Count && i < MaxChatMatches; i++)
        {
            var quest = matches[i];
            session.States.TryGetValue(quest.RowId, out var evaluation);
            links.PrintQuestLink(quest, BlockerText.StatusText(evaluation, quest, session.LiveNames, session.States));
        }

        if (matches.Count > MaxChatMatches)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, matches.Count - MaxChatMatches));
        }
    }
}
