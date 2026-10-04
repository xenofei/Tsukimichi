using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The ending-soon chat line (feature plan v7, 1.19.0, C10): "A Nocturne for Heroes ends in 2 days: 1 quest in your
/// journal · [quest]", once per event per login and again on its last day, for the logged-in character, while
/// <see cref="Configuration.ChatNoticeSeasonalEnding"/> is on (off by default: notices stay off until the player turns
/// them on). The warnings are <see cref="EventWarnings"/>'s, so the line says what Tonight's card says.
/// </summary>
public sealed partial class ChatNotifier
{
    /// <summary>The ending-soon warnings; set by the plugin. Null prints no line.</summary>
    public EventWarningSource? EventWarnings { get; set; }

    // (character, festival, last day) already announced this login.
    private readonly HashSet<(ulong Character, ushort Festival, bool LastDay)> endingAnnounced = [];
    private ulong endingCharacter;

    private void AnnounceEndingSoon()
    {
        if (!config.ChatNoticeSeasonalEnding || EventWarnings is not { } source || session.LiveContentId is not { } live || live == 0)
        {
            return;
        }

        if (live != endingCharacter)
        {
            endingCharacter = live;
            endingAnnounced.Clear();
        }

        // The warnings are the viewed character's: speak only while the window views the one logged in.
        if (session.ViewedContentId != live)
        {
            return;
        }

        foreach (var warning in source.Current)
        {
            if (!endingAnnounced.Add((live, warning.Festival.FestivalId, warning.LastDay)))
            {
                continue;
            }

            QuestRecord? first = null;
            foreach (var quest in warning.Festival.Quests)
            {
                if (quest.State == QuestState.Accepted && !quest.IsSpareAlternative)
                {
                    first = quest.Quest;
                    break;
                }

                if (first is null && quest.IsActionable)
                {
                    first = quest.Quest;
                }
            }

            var left = warning.InJournal > 0
                ? warning.InJournal == 1 ? Strings.EventCardJournalOne : string.Format(CultureInfo.CurrentCulture, Strings.EventCardJournalFormat, warning.InJournal)
                : warning.Left == 1 ? Strings.EventCardLeftOne : string.Format(CultureInfo.CurrentCulture, Strings.EventCardLeftFormat, warning.Left);
            var text = string.Format(CultureInfo.CurrentCulture, Strings.EventChatFormat, EventWarningSource.Title(warning), left);
            if (first is not null)
            {
                Print(text + Strings.SeasonalChatNextPrefix, first, string.Empty);
            }
        }
    }
}
