using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Finds the completions that just happened, for the waxing moon (feature plan v6 U8a): each frame it is handed the
/// session's recent events (newest first, the same list throughout, <see cref="RecentEventsTracker"/>) and reports the
/// <see cref="QuestEventKind.Completed"/> events that arrived since the last frame. The first look at a character only
/// notes where its events stand, so opening the plugin, logging in or switching characters never replays old
/// completions. Allocation-free.
/// </summary>
public sealed class CompletionCues
{
    private bool primed;
    private ulong? contentId;
    private QuestEvent? newest;

    /// <summary>
    /// Adds to <paramref name="into"/> the row ids completed since the last call for <paramref name="character"/>
    /// (the live character; null while logged out) and returns how many. With <paramref name="shown"/> false (another
    /// character is on screen) the events are noted but none is reported, so they do not play later either.
    /// </summary>
    public int Take(ulong? character, IReadOnlyList<QuestEvent> newestFirst, bool shown, List<uint> into)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        ArgumentNullException.ThrowIfNull(into);

        var first = newestFirst.Count > 0 ? newestFirst[0] : null;
        if (!primed || character != contentId)
        {
            primed = true;
            contentId = character;
            newest = first;
            return 0;
        }

        var found = 0;
        for (var i = 0; i < newestFirst.Count; i++)
        {
            var e = newestFirst[i];
            if (ReferenceEquals(e, newest) || (newest is not null && e.TimeUtc < newest.TimeUtc))
            {
                break;
            }

            if (shown && e.Kind == QuestEventKind.Completed)
            {
                into.Add(e.RowId);
                found++;
            }
        }

        newest = first;
        return found;
    }
}
