using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Config;
using Tsukimichi.Core.Chains;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The finale chat line (feature plan v7 N8; spec-1.21 N8): "Finale Ready · Bard Quests: [A Harmony from the Heavens]"
/// once per finale per character, ever (recorded with the character's noticed ids, <see cref="LooseEnds.NoticeId"/>),
/// for the logged-in character's loose ends whose last quest it can take now, while
/// <see cref="Configuration.ChatNoticeStorylineFinale"/> is on (off by default). Nothing is recorded while it is off, so
/// turning it on later still speaks of a finale that is Ready then. A finale past the story point is never named.
/// </summary>
public sealed partial class ChatNotifier
{
    /// <summary>The loose ends; set by the plugin. Null prints no line.</summary>
    public LooseEndsSource? LooseEnds { get; set; }

    private void AnnounceFinales()
    {
        if (!config.ChatNoticeStorylineFinale
            || LooseEnds is not { } source
            || CharacterSettings is not { } book
            || session.LiveContentId is not { } contentId
            || contentId == 0)
        {
            return;
        }

        List<LooseEnd>? fresh = null;
        HashSet<string>? noticed = null;
        var spoilers = session.LiveSpoilers;
        foreach (var end in source.Live)
        {
            if (!end.IsReadyFinale || spoilers.IsAhead(end.Next.RowId))
            {
                continue;
            }

            noticed ??= new HashSet<string>(book.Noticed(contentId), System.StringComparer.Ordinal);
            if (!noticed.Contains(Core.Chains.LooseEnds.NoticeId(end.Next.RowId)))
            {
                (fresh ??= []).Add(end);
            }
        }

        if (fresh is null)
        {
            return;
        }

        var marks = new List<Core.Storage.CharacterSettingChange>(fresh.Count);
        foreach (var end in fresh)
        {
            marks.Add(Core.Storage.CharacterSettingChange.Noticed(contentId, Core.Chains.LooseEnds.NoticeId(end.Next.RowId)));
        }

        book.Edit(marks);
        foreach (var end in fresh)
        {
            Print(string.Format(CultureInfo.CurrentCulture, Strings.LooseEndsChatFormat, source.NameOf(end.Line, spoilers)), end.Next, string.Empty);
        }
    }
}
