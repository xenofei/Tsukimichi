using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Text.SeStringHandling;
using Tsukimichi.Core.Seasonal;
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

            // Every warning prints, linking a quest when one is in the journal or can be taken; a warning with only
            // rewards left prints without a link rather than using up its once-per-login line silently.
            var line = Core.Seasonal.EventWarnings.ChatLine(warning);
            var culture = CultureInfo.CurrentCulture;
            var left = line.Left switch
            {
                EndingSoonLeft.InJournal => line.Count == 1 ? Strings.EventCardJournalOne : string.Format(culture, Strings.EventCardJournalFormat, line.Count),
                EndingSoonLeft.ToTake => line.Count == 1 ? Strings.EventCardLeftOne : string.Format(culture, Strings.EventCardLeftFormat, line.Count),
                _ => line.Count == 1 ? Strings.EventCardRewardsOne : string.Format(culture, Strings.EventCardRewardsFormat, line.Count),
            };
            var text = string.Format(culture, Strings.EventChatFormat, EventWarningSource.Title(warning), left);
            if (line.Link is { } quest)
            {
                Print(text + Strings.SeasonalChatNextPrefix, quest, string.Empty);
            }
            else
            {
                chat.Print(new SeStringBuilder().AddText(text).Build(), Strings.ChatTag);
            }
        }
    }
}
