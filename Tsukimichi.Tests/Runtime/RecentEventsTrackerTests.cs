using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// Bug-hunt G3: a character switch without a logout gap kept the previous character's recent events, so Recent
/// activity showed them and NoticeTracker (whose announced set resets on the content-id change) re-announced them.
/// </summary>
public class RecentEventsTrackerTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private const ulong Michiru = 1001;
    private const ulong Alt = 1002;

    private static QuestEvent Available(uint rowId) => new(QuestEventKind.NewlyAvailable, rowId, Now);

    private static QuestEvent Completed(uint rowId) => new(QuestEventKind.Completed, rowId, Now);

    private static uint[] Rows(IEnumerable<QuestEvent> events) => events.Select(e => e.RowId).ToArray();

    [Fact]
    public void Events_recorded_for_one_character_are_gone_once_another_content_id_is_seen()
    {
        var tracker = new RecentEventsTracker();
        tracker.Add(Michiru, [Available(1), Completed(2)]);
        Assert.Equal([1u, 2u], Rows(tracker.Events));

        Assert.True(tracker.Follow(Alt));

        Assert.Empty(tracker.Events);
        Assert.Equal(Alt, tracker.ContentId);
    }

    [Fact]
    public void Recording_for_another_character_drops_the_previous_events_first()
    {
        var tracker = new RecentEventsTracker();
        tracker.Add(Michiru, [Available(1)]);

        Assert.True(tracker.Add(Alt, [Completed(7)]));

        Assert.Equal([7u], Rows(tracker.Events));
        Assert.Equal(Alt, tracker.ContentId);
    }

    [Fact]
    public void Switching_characters_stops_the_notice_scan_from_re_announcing_the_previous_ones()
    {
        var tracker = new RecentEventsTracker();
        var notices = new NoticeTracker();
        tracker.Add(Michiru, [Available(1), Available(2)]);
        Assert.Equal([2u, 1u], notices.Scan(tracker.Events, Michiru));

        // Login for another character arrives without a logout in between: the poller follows the new content id
        // before it has any events of its own.
        tracker.Follow(Alt);
        Assert.Empty(notices.Scan(tracker.Events, Alt));

        tracker.Add(Alt, [Available(3)]);
        Assert.Equal([3u], notices.Scan(tracker.Events, Alt));
    }

    [Fact]
    public void Same_character_keeps_its_events_and_reports_no_change()
    {
        var tracker = new RecentEventsTracker();
        tracker.Add(Michiru, [Available(1)]);

        Assert.False(tracker.Follow(Michiru));
        Assert.False(tracker.Add(Michiru, []));

        Assert.Equal([1u], Rows(tracker.Events));
    }

    [Fact]
    public void Batches_go_newest_first_in_their_own_order_and_the_list_is_capped()
    {
        var tracker = new RecentEventsTracker(capacity: 3);
        tracker.Add(Michiru, [Available(1), Available(2)]);
        tracker.Add(Michiru, [Available(3), Available(4)]);

        Assert.Equal([3u, 4u, 1u], Rows(tracker.Events));
        Assert.Equal(3, tracker.Capacity);
    }

    [Fact]
    public void Clear_forgets_the_character_and_the_events_and_the_list_instance_is_stable()
    {
        var tracker = new RecentEventsTracker();
        var events = tracker.Events;
        tracker.Add(Michiru, [Available(1)]);

        tracker.Clear();

        Assert.Empty(tracker.Events);
        Assert.Null(tracker.ContentId);
        Assert.Same(events, tracker.Events);
        Assert.False(tracker.Follow(Michiru));
    }

    private static QuestEvent Swapped(uint rowId) => Available(rowId) with { GearOnly = true };

    [Fact]
    public void A_gear_only_opening_of_a_quest_already_announced_this_session_is_dropped()
    {
        // Review 1.10: each swap back to the relic weapon re-fired NewlyAvailable for its step.
        var tracker = new RecentEventsTracker();
        Assert.True(tracker.Add(Michiru, [Swapped(1)]));
        Assert.False(tracker.Add(Michiru, [Swapped(1)]));
        Assert.True(tracker.Add(Michiru, [Completed(2), Swapped(1)]));
        Assert.Equal([2u, 1u], Rows(tracker.Events));

        // Announced another way first (the step before completed with the weapon on): a later swap is no news either.
        tracker.Add(Michiru, [Available(3)]);
        tracker.Add(Michiru, [Swapped(3)]);
        Assert.Single(tracker.Events, e => e.RowId == 3);

        // An opening that is not a weapon swap is news each time (a daily open again after the reset).
        tracker.Add(Michiru, [Available(4)]);
        tracker.Add(Michiru, [Available(4)]);
        Assert.Equal(2, tracker.Events.Count(e => e.RowId == 4));
    }

    [Fact]
    public void The_once_per_session_rule_starts_over_for_another_character_and_after_a_logout()
    {
        var tracker = new RecentEventsTracker(capacity: 1);
        tracker.Add(Michiru, [Swapped(1)]);
        tracker.Add(Michiru, [Completed(2)]);
        Assert.Equal([2u], Rows(tracker.Events));

        // Fallen off the capped list, still announced this session.
        Assert.False(tracker.Add(Michiru, [Swapped(1)]));

        tracker.Add(Alt, [Swapped(1)]);
        Assert.Equal([1u], Rows(tracker.Events));

        tracker.Clear();
        Assert.True(tracker.Add(Alt, [Swapped(1)]));
    }

    [Fact]
    public void Capacity_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecentEventsTracker(0));
    }
}
