using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableRunGuard"/> (feature plan v7 A4, A6): a run is followed from the first status that reads
/// running, a start Tsukimichi asked for names its origin, the stop conditions fire once when met, and the run ends
/// with a receipt once Questionable has been idle for the grace period.
/// </summary>
public class QuestionableRunGuardTests
{
    private const uint QuestA = 0x10000 + 428;
    private const uint QuestB = 0x10000 + 429;
    private const uint QuestC = 0x10000 + 430;

    private static readonly DateTime T0 = new(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc);

    private static QuestionableGuardAction Observe(QuestionableRunGuard guard, bool running, double seconds, uint? current = null, Func<uint, bool>? isCompleted = null) =>
        guard.Observe(running, current, T0.AddSeconds(seconds), isCompleted, out _);

    private static QuestionableRunReceipt End(QuestionableRunGuard guard, double seconds, Func<uint, bool>? isCompleted = null)
    {
        Assert.Equal(QuestionableGuardAction.None, guard.Observe(false, null, T0.AddSeconds(seconds), isCompleted, out _));
        var action = guard.Observe(false, null, T0.AddSeconds(seconds + QuestionableRunGuard.EndGraceSeconds), isCompleted, out var receipt);
        Assert.Equal(QuestionableGuardAction.Ended, action);
        Assert.NotNull(receipt);
        return receipt;
    }

    [Fact]
    public void Nothing_is_followed_or_polled_while_idle()
    {
        var guard = new QuestionableRunGuard();

        Assert.Equal(QuestionableGuardAction.None, Observe(guard, false, 0));
        Assert.False(guard.Tracking);
        Assert.False(guard.NeedsPolling);
    }

    [Fact]
    public void A_run_seen_running_without_a_start_of_ours_was_started_elsewhere()
    {
        var guard = new QuestionableRunGuard();

        Observe(guard, true, 10, QuestA);

        Assert.True(guard.Tracking);
        Assert.True(guard.NeedsPolling);
        Assert.Equal(QuestionableRunOrigin.Elsewhere, guard.Origin);
        Assert.Equal(T0.AddSeconds(10), guard.StartedUtc);
        Assert.Equal(QuestA, guard.CurrentRowId);
    }

    [Fact]
    public void A_start_of_ours_names_the_origin_and_the_start_time()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestA, T0);
        Assert.True(guard.NeedsPolling);

        Observe(guard, true, 2, QuestA);

        Assert.Equal(QuestionableRunOrigin.SingleQuest, guard.Origin);
        Assert.Equal(QuestA, guard.SingleRowId);
        Assert.Equal(T0, guard.StartedUtc);
    }

    [Fact]
    public void A_start_that_never_runs_is_forgotten_after_the_start_window()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.KeepGoing, QuestA, T0);

        Observe(guard, false, QuestionableRunGuard.StartWindowSeconds + 1);
        Assert.False(guard.NeedsPolling);

        Observe(guard, true, 60, QuestA);
        Assert.Equal(QuestionableRunOrigin.Elsewhere, guard.Origin);
    }

    [Fact]
    public void A_single_quest_run_whose_quest_is_done_ends_as_done()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestA, T0);
        Observe(guard, true, 1, QuestA);
        guard.NoteCompleted(QuestA);

        var receipt = End(guard, 600);

        Assert.Equal(QuestionableRunEnd.SingleQuestDone, receipt.End);
        Assert.Equal(QuestA, receipt.QuestRowId);
        Assert.Equal([QuestA], receipt.Completed);
        Assert.Equal(TimeSpan.FromSeconds(600), receipt.Duration);
        Assert.False(guard.Tracking);
        Assert.False(guard.NeedsPolling);
    }

    [Fact]
    public void A_single_quest_completion_noted_late_is_read_from_the_states()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestA, T0);
        Observe(guard, true, 1, QuestA);

        var receipt = End(guard, 300, rowId => rowId == QuestA);

        Assert.Equal(QuestionableRunEnd.SingleQuestDone, receipt.End);
        Assert.Equal([QuestA], receipt.Completed);
    }

    [Fact]
    public void A_single_quest_run_that_ends_early_says_so()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestA, T0);
        Observe(guard, true, 1, QuestA);

        var receipt = End(guard, 120, _ => false);

        Assert.Equal(QuestionableRunEnd.SingleQuestNotDone, receipt.End);
        Assert.Empty(receipt.Completed);
    }

    [Fact]
    public void A_short_idle_blip_does_not_end_the_run()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);

        Assert.Equal(QuestionableGuardAction.None, Observe(guard, false, 10));
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, false, 10 + QuestionableRunGuard.EndGraceSeconds - 0.5));
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 14, QuestB));
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, false, 20));

        Assert.True(guard.Tracking);
    }

    [Fact]
    public void A_start_during_the_last_runs_grace_ends_that_run_and_begins_a_new_one()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestA, T0);
        Observe(guard, true, 1, QuestA);
        guard.NoteCompleted(QuestA);
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, false, 100));

        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestB, T0.AddSeconds(101));
        var action = guard.Observe(true, QuestB, T0.AddSeconds(102), null, out var receipt);

        Assert.Equal(QuestionableGuardAction.Ended, action);
        Assert.NotNull(receipt);
        Assert.Equal(QuestionableRunEnd.SingleQuestDone, receipt.End);
        Assert.Equal(QuestA, receipt.QuestRowId);
        Assert.Equal(T0.AddSeconds(100), receipt.EndedUtc);
        Assert.True(guard.Tracking);
        Assert.Equal(QuestB, guard.SingleRowId);
        Assert.Equal(T0.AddSeconds(101), guard.StartedUtc);
    }

    [Fact]
    public void A_start_noted_while_a_run_reads_running_is_ignored()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);

        guard.NoteStarted(QuestionableRunOrigin.SingleQuest, QuestB, T0.AddSeconds(1));
        Observe(guard, true, 2, QuestA);

        Assert.Equal(QuestionableRunOrigin.Elsewhere, guard.Origin);
        Assert.Equal(QuestionableRunEnd.Ended, End(guard, 10).End);
    }

    [Fact]
    public void Stop_after_N_quests_fires_once_when_N_more_are_done()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        guard.NoteCompleted(QuestA);
        Assert.True(guard.Arm(QuestionableStopCondition.AfterQuests(2)));
        Assert.Equal(2, guard.QuestsLeft);

        guard.NoteCompleted(QuestB);
        Assert.Equal(1, guard.QuestsLeft);
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 60, QuestC));

        guard.NoteCompleted(QuestC);
        Assert.Equal(0, guard.QuestsLeft);
        Assert.Equal(QuestionableGuardAction.Stop, Observe(guard, true, 120, QuestC));
        Assert.True(guard.Stopping);
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 121, QuestC));

        var receipt = End(guard, 130);
        Assert.Equal(QuestionableRunEnd.AfterQuests, receipt.End);
        Assert.Equal(2, receipt.LimitQuests);
        Assert.Equal([QuestA, QuestB, QuestC], receipt.Completed);
    }

    [Fact]
    public void A_quest_completed_twice_counts_once()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        guard.Arm(QuestionableStopCondition.AfterQuests(2));

        guard.NoteCompleted(QuestA);
        guard.NoteCompleted(QuestA);

        Assert.Equal(1, guard.QuestsLeft);
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 5, QuestB));
    }

    [Fact]
    public void Stop_at_a_time_fires_at_that_time()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        Assert.True(guard.Arm(QuestionableStopCondition.AtTime(T0.AddMinutes(30))));

        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, (30 * 60) - 1, QuestA));
        Assert.Equal(QuestionableGuardAction.Stop, Observe(guard, true, 30 * 60, QuestA));

        var receipt = End(guard, (30 * 60) + 2);
        Assert.Equal(QuestionableRunEnd.AtTime, receipt.End);
        Assert.Equal(T0.AddMinutes(30), receipt.LimitAtUtc);
    }

    [Fact]
    public void Stop_after_this_quest_waits_for_that_quest()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        Assert.True(guard.Arm(QuestionableStopCondition.AfterCurrent(QuestA)));

        guard.NoteCompleted(QuestB);
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 10, QuestA));

        guard.NoteCompleted(QuestA);
        Assert.Equal(QuestionableGuardAction.Stop, Observe(guard, true, 20, QuestC));

        var receipt = End(guard, 25);
        Assert.Equal(QuestionableRunEnd.AfterCurrent, receipt.End);
        Assert.Equal(QuestA, receipt.QuestRowId);
    }

    [Fact]
    public void Stop_after_this_quest_without_a_known_quest_stops_after_the_next_one_done()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0);
        guard.NoteCompleted(QuestA);
        Assert.True(guard.Arm(QuestionableStopCondition.AfterCurrent(null)));
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 1));

        guard.NoteCompleted(QuestB);

        Assert.Equal(QuestionableGuardAction.Stop, Observe(guard, true, 2));
    }

    [Fact]
    public void A_refused_stop_drops_the_condition_and_the_run_goes_on()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        guard.Arm(QuestionableStopCondition.AtTime(T0));
        Assert.Equal(QuestionableGuardAction.Stop, Observe(guard, true, 1, QuestA));

        guard.StopFailed();

        Assert.Equal(QuestionableStopRule.None, guard.Condition.Rule);
        Assert.False(guard.Stopping);
        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 2, QuestA));
        Assert.Equal(QuestionableRunEnd.Ended, End(guard, 10).End);
    }

    [Fact]
    public void A_condition_needs_a_run_and_ends_with_it()
    {
        var guard = new QuestionableRunGuard();
        Assert.False(guard.Arm(QuestionableStopCondition.AfterQuests(3)));

        Observe(guard, true, 0, QuestA);
        Assert.True(guard.Arm(QuestionableStopCondition.AfterQuests(3)));
        End(guard, 10);

        Assert.Equal(QuestionableStopRule.None, guard.Condition.Rule);
        Observe(guard, true, 100, QuestA);
        Assert.Equal(QuestionableStopRule.None, guard.Condition.Rule);
    }

    [Fact]
    public void Disarm_clears_the_condition()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        guard.Arm(QuestionableStopCondition.AtTime(T0.AddSeconds(5)));

        guard.Disarm();

        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 10, QuestA));
    }

    [Fact]
    public void Tsukimichis_stop_is_the_reason_and_wins_over_a_condition_met_later()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteStarted(QuestionableRunOrigin.KeepGoing, QuestA, T0);
        Observe(guard, true, 1, QuestA);
        guard.Arm(QuestionableStopCondition.AfterQuests(1));

        guard.NoteStopAsked();
        guard.NoteCompleted(QuestA);

        Assert.Equal(QuestionableGuardAction.None, Observe(guard, true, 2, QuestA));
        var receipt = End(guard, 3);
        Assert.Equal(QuestionableRunEnd.StoppedFromTsukimichi, receipt.End);
        Assert.Equal(QuestionableRunOrigin.KeepGoing, receipt.Origin);
        Assert.Equal(0, receipt.LimitQuests);
    }

    [Fact]
    public void A_run_that_ends_by_itself_reads_ended()
    {
        var guard = new QuestionableRunGuard();
        Observe(guard, true, 0, QuestA);
        guard.NoteCompleted(QuestA);

        var receipt = End(guard, 3600 + 720);

        Assert.Equal(QuestionableRunEnd.Ended, receipt.End);
        Assert.Equal(QuestionableRunOrigin.Elsewhere, receipt.Origin);
        Assert.Equal(TimeSpan.FromMinutes(72), receipt.Duration);
    }

    [Fact]
    public void Completions_outside_a_run_are_not_counted()
    {
        var guard = new QuestionableRunGuard();
        guard.NoteCompleted(QuestA);

        Observe(guard, true, 0, QuestB);

        Assert.Empty(guard.Completed);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(500, QuestionableRunGuard.MaxQuests)]
    public void The_quest_count_is_clamped(int asked, int expected)
    {
        Assert.Equal(expected, QuestionableStopCondition.AfterQuests(asked).Quests);
    }

    [Theory]
    [InlineData("21:30", 21, 30)]
    [InlineData("9:05", 9, 5)]
    [InlineData(" 07.45 ", 7, 45)]
    [InlineData("2130", 21, 30)]
    [InlineData("930", 9, 30)]
    [InlineData("0:00", 0, 0)]
    public void Clock_times_are_read(string text, int hour, int minute)
    {
        Assert.True(QuestionableRunGuard.TryParseClock(text, out var h, out var m));
        Assert.Equal(hour, h);
        Assert.Equal(minute, m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("12:5")]
    [InlineData("noon")]
    [InlineData("123:00")]
    [InlineData("-1:30")]
    [InlineData("12")]
    public void Anything_else_is_not_a_clock_time(string? text)
    {
        Assert.False(QuestionableRunGuard.TryParseClock(text, out _, out _));
    }

    [Fact]
    public void The_next_time_is_today_when_ahead_and_tomorrow_otherwise()
    {
        var now = new DateTime(2026, 10, 3, 20, 15, 0, DateTimeKind.Local);

        Assert.Equal(new DateTime(2026, 10, 3, 21, 30, 0, DateTimeKind.Local), QuestionableRunGuard.NextAt(now, 21, 30));
        Assert.Equal(new DateTime(2026, 10, 4, 8, 0, 0, DateTimeKind.Local), QuestionableRunGuard.NextAt(now, 8, 0));
        Assert.Equal(new DateTime(2026, 10, 4, 20, 15, 0, DateTimeKind.Local), QuestionableRunGuard.NextAt(now, 20, 15));
    }
}
