using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The bookkeeping behind chat notices: which of the session's recent events are new since the last look, and which
/// quests were already announced this login session. The recent-event list is newest first and capped, so the scan
/// walks from the top until it meets the event it stopped at last time; when that event is gone (a cleared list
/// after logout, or more events than the cap since the last scan) everything present counts as new. A change of
/// live character starts a fresh session: the announced set is dropped.
/// </summary>
public sealed class NoticeTracker
{
    private readonly HashSet<uint> notified = [];
    private readonly HashSet<uint> jobNudged = [];
    private QuestEvent? lastSeen;
    private ulong? sessionContentId;
    private bool started;

    /// <summary>Row ids announced this session.</summary>
    public IReadOnlySet<uint> Notified => notified;

    /// <summary>Row ids announced as a level-up nudge this session; a separate set, since a job quest may also be a feature quest.</summary>
    public IReadOnlySet<uint> JobNudged => jobNudged;

    /// <summary>
    /// Row ids of <see cref="QuestEventKind.NewlyAvailable"/> events added since the previous call, earlier polls
    /// before later ones (events of one poll share a timestamp, so their order among themselves is not significant).
    /// <paramref name="liveContentId"/> is the logged-in character (null when logged out); a different value than
    /// last time resets the announced set.
    /// </summary>
    public List<uint> Scan(IReadOnlyList<QuestEvent> recentNewestFirst, ulong? liveContentId)
    {
        ArgumentNullException.ThrowIfNull(recentNewestFirst);

        if (!started || liveContentId != sessionContentId)
        {
            started = true;
            sessionContentId = liveContentId;
            notified.Clear();
            jobNudged.Clear();
            lastSeen = null;
        }

        var result = new List<uint>();
        if (recentNewestFirst.Count == 0)
        {
            lastSeen = null;
            return result;
        }

        if (ReferenceEquals(recentNewestFirst[0], lastSeen))
        {
            return result;
        }

        var end = recentNewestFirst.Count;
        for (var i = 0; i < recentNewestFirst.Count; i++)
        {
            if (ReferenceEquals(recentNewestFirst[i], lastSeen))
            {
                end = i;
                break;
            }
        }

        lastSeen = recentNewestFirst[0];
        for (var i = end - 1; i >= 0; i--)
        {
            var e = recentNewestFirst[i];
            if (e.Kind == QuestEventKind.NewlyAvailable)
            {
                result.Add(e.RowId);
            }
        }

        return result;
    }

    public bool WasNotified(uint rowId) => notified.Contains(rowId);

    /// <summary>Records an announcement; false when the quest was already announced this session.</summary>
    public bool MarkNotified(uint rowId) => notified.Add(rowId);

    public bool WasJobNudged(uint rowId) => jobNudged.Contains(rowId);

    /// <summary>Records a level-up nudge; false when the quest was already nudged this session.</summary>
    public bool MarkJobNudged(uint rowId) => jobNudged.Add(rowId);

    /// <summary>
    /// Whether a newly available quest deserves a line: it must be pinned or a feature quest, and a main scenario
    /// quest only when the user asked for those.
    /// </summary>
    public static bool Qualifies(QuestRecord quest, bool pinned, bool feature, bool includeMainScenario)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return (pinned || feature) && (includeMainScenario || !FeaturePresets.IsMainScenario(quest));
    }
}
