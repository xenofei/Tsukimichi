using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

/// <summary>The login line "Moonfire Faire is running: 2 quests ready": once per event per login, only with a Ready quest.</summary>
public class NoticeTrackerSeasonalTests
{
    private static RunningFestival Festival(ushort id, params QuestState[] states) =>
        new(id, "Event " + id, states.Select((s, i) => new SeasonalQuest(Fixture.Quest((uint)(65600 + i)), s)).ToList(), states.Count(s => s == QuestState.Ready), null, null);

    [Fact]
    public void An_event_with_a_ready_quest_is_announced_once_per_login()
    {
        var tracker = new NoticeTracker();
        tracker.ScanEvents([], 7);
        var moonfire = Festival(174, QuestState.Ready, QuestState.Ready, QuestState.Blocked);

        Assert.Equal([moonfire], tracker.TakeSeasonalNotices([moonfire]));
        Assert.Empty(tracker.TakeSeasonalNotices([moonfire]));
        Assert.True(tracker.WasSeasonalNoticed(174));

        // Logout (no live character) and login again: a fresh session announces it again.
        tracker.ScanEvents([], null);
        tracker.ScanEvents([], 7);
        Assert.Empty(tracker.SeasonalNoticed);
        Assert.Equal([moonfire], tracker.TakeSeasonalNotices([moonfire]));
    }

    [Fact]
    public void An_event_without_a_ready_quest_waits_until_one_is_ready()
    {
        var tracker = new NoticeTracker();
        tracker.ScanEvents([], 7);
        var accepted = Festival(84, QuestState.Accepted, QuestState.Blocked);

        Assert.Empty(tracker.TakeSeasonalNotices([accepted]));
        Assert.False(tracker.WasSeasonalNoticed(84));

        var ready = Festival(84, QuestState.Ready);
        var other = Festival(174, QuestState.Completed);
        Assert.Equal([ready], tracker.TakeSeasonalNotices([other, ready]));
        Assert.True(tracker.MarkSeasonalNoticed(174));
        Assert.False(tracker.MarkSeasonalNoticed(174));
    }
}
