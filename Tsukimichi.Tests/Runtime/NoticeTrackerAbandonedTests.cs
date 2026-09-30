using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>The "Abandoned: [quest]" chat line's bookkeeping: every new event is scanned, each quest announced once per session.</summary>
public class NoticeTrackerAbandonedTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ScanEvents_returns_every_new_event_oldest_first_and_Scan_keeps_only_newly_available()
    {
        var abandoned = new QuestEvent(QuestEventKind.Abandoned, 65600, Now);
        var available = new QuestEvent(QuestEventKind.NewlyAvailable, 65601, Now);
        var later = new QuestEvent(QuestEventKind.Abandoned, 65602, Now.AddSeconds(1));
        var tracker = new NoticeTracker();

        Assert.Equal([abandoned, available], tracker.ScanEvents([available, abandoned], 7));
        Assert.Empty(tracker.ScanEvents([available, abandoned], 7));
        Assert.Equal([later], tracker.ScanEvents([later, available, abandoned], 7));

        var other = new NoticeTracker();
        Assert.Equal([65601u], other.Scan([later, available, abandoned], 7));
    }

    [Fact]
    public void An_abandoned_quest_is_announced_once_per_session()
    {
        var tracker = new NoticeTracker();
        tracker.ScanEvents([], 7);

        Assert.True(tracker.MarkAbandonNoticed(65600));
        Assert.False(tracker.MarkAbandonNoticed(65600));
        Assert.Contains(65600u, tracker.AbandonNoticed);

        // Another character logs in: a fresh session.
        tracker.ScanEvents([], 8);
        Assert.Empty(tracker.AbandonNoticed);
        Assert.True(tracker.MarkAbandonNoticed(65600));
    }
}
