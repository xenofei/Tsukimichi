using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Runtime;

public class NoticeTrackerTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static QuestEvent Available(uint rowId) => new(QuestEventKind.NewlyAvailable, rowId, Now);

    private static QuestEvent Completed(uint rowId) => new(QuestEventKind.Completed, rowId, Now);

    /// <summary>Mimics SessionState.AddEvents: a batch is inserted at the front, keeping its own order.</summary>
    private static void Add(List<QuestEvent> recent, params QuestEvent[] batch)
    {
        for (var i = batch.Length - 1; i >= 0; i--)
        {
            recent.Insert(0, batch[i]);
        }
    }

    private static HashSet<uint> Set(IEnumerable<uint> ids) => [.. ids];

    [Fact]
    public void First_scan_reports_every_newly_available_event_and_nothing_else()
    {
        var recent = new List<QuestEvent>();
        Add(recent, Available(1), Completed(2), Available(3));

        var tracker = new NoticeTracker();
        Assert.Equal(Set([1u, 3u]), Set(tracker.Scan(recent, 7)));
    }

    [Fact]
    public void Later_scans_report_only_events_added_since_earlier_polls_first()
    {
        var recent = new List<QuestEvent>();
        var tracker = new NoticeTracker();
        Add(recent, Available(1));
        Assert.Equal([1u], tracker.Scan(recent, 7));

        Assert.Empty(tracker.Scan(recent, 7));

        Add(recent, Available(2));
        Add(recent, Available(3), Available(4));
        var scanned = tracker.Scan(recent, 7);
        Assert.Equal(2u, scanned[0]);
        Assert.Equal(Set([2u, 3u, 4u]), Set(scanned));
        Assert.Empty(tracker.Scan(recent, 7));
    }

    [Fact]
    public void A_cleared_list_then_new_events_are_all_new()
    {
        var recent = new List<QuestEvent>();
        var tracker = new NoticeTracker();
        Add(recent, Available(1));
        tracker.Scan(recent, 7);

        recent.Clear();
        Assert.Empty(tracker.Scan(recent, 7));

        Add(recent, Available(4));
        Assert.Equal([4u], tracker.Scan(recent, 7));
    }

    [Fact]
    public void Events_pushed_past_the_cap_still_count_once()
    {
        var recent = new List<QuestEvent>();
        var tracker = new NoticeTracker();
        Add(recent, Available(1));
        tracker.Scan(recent, 7);

        // The event last seen was trimmed away: everything present is new, nothing is repeated later.
        recent.Clear();
        Add(recent, Available(2), Available(3));
        Assert.Equal(Set([2u, 3u]), Set(tracker.Scan(recent, 7)));
        Assert.Empty(tracker.Scan(recent, 7));
    }

    [Fact]
    public void Announced_set_is_per_login_session()
    {
        var tracker = new NoticeTracker();
        var recent = new List<QuestEvent>();
        Add(recent, Available(1));
        tracker.Scan(recent, 7);

        Assert.True(tracker.MarkNotified(1));
        Assert.False(tracker.MarkNotified(1));
        Assert.True(tracker.WasNotified(1));

        // Same character, same session: still announced.
        tracker.Scan(recent, 7);
        Assert.True(tracker.WasNotified(1));

        // Logout clears the events; logging the same character in again is a new session.
        recent.Clear();
        tracker.Scan(recent, null);
        Assert.False(tracker.WasNotified(1));

        Add(recent, Available(1));
        Assert.Equal([1u], tracker.Scan(recent, 7));

        // Another character is a new session too.
        tracker.MarkNotified(1);
        tracker.Scan(recent, 8);
        Assert.False(tracker.WasNotified(1));
        Assert.Empty(tracker.Notified);
    }

    [Fact]
    public void Qualifies_needs_a_pin_or_a_feature_quest_and_gates_the_main_scenario()
    {
        var side = Quest(1, "Side", section: 2);
        var msq = Quest(2, "Main", section: 0);

        Assert.False(NoticeTracker.Qualifies(side, pinned: false, feature: false, includeMainScenario: true));
        Assert.True(NoticeTracker.Qualifies(side, pinned: true, feature: false, includeMainScenario: false));
        Assert.True(NoticeTracker.Qualifies(side, pinned: false, feature: true, includeMainScenario: false));

        Assert.False(NoticeTracker.Qualifies(msq, pinned: true, feature: false, includeMainScenario: false));
        Assert.True(NoticeTracker.Qualifies(msq, pinned: true, feature: false, includeMainScenario: true));
        Assert.False(NoticeTracker.Qualifies(msq, pinned: false, feature: false, includeMainScenario: true));
    }
}
