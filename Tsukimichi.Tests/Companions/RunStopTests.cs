using System.Numerics;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// "Why it stopped" (plan v7, 1.18.0, A2; spec-1.18): the eight reasons' cues and safe fixes
/// (<see cref="RunStopClassifier"/>), how a run's end and a travel give-up map to a reason, the next quest, and the dock
/// rules (<see cref="StopDock"/>): one card, calm cards never push out one that needs you, the same stop refreshes,
/// calm cards fade after 30 s on screen, a card without words is refused.
/// </summary>
public class RunStopTests
{
    private static StopCard Card(StopReason reason, uint quest = 0, string title = "Title") =>
        new(reason, StopHandOff.Questionable) { Title = title, QuestRowId = quest };

    [Fact]
    public void Each_reason_has_its_cue_and_only_copper_cards_stay()
    {
        Assert.Equal(StopCue.Gold, RunStopClassifier.Cue(StopReason.Finished));
        Assert.Equal(StopCue.Silver, RunStopClassifier.Cue(StopReason.Player));
        foreach (var reason in RunStopClassifier.All.Skip(2))
        {
            Assert.Equal(StopCue.Copper, RunStopClassifier.Cue(reason));
            Assert.True(RunStopClassifier.NeedsYou(reason));
        }

        Assert.False(RunStopClassifier.NeedsYou(StopReason.Finished));
        Assert.False(RunStopClassifier.NeedsYou(StopReason.Player));
        Assert.Equal(8, RunStopClassifier.All.Length);
        Assert.Equal(Enum.GetValues<StopReason>().Length, RunStopClassifier.All.Distinct().Count());
    }

    [Fact]
    public void The_fixes_follow_the_spec_and_never_more_than_two()
    {
        Assert.Equal((StopFix.StartNext, StopFix.None), RunStopClassifier.Fixes(StopReason.Finished));
        Assert.Equal((StopFix.StartAgain, StopFix.None), RunStopClassifier.Fixes(StopReason.Player));
        Assert.Equal((StopFix.ShowDuty, StopFix.KeepGoingAfterDuty), RunStopClassifier.Fixes(StopReason.DutyGuard));
        Assert.Equal((StopFix.TryAgain, StopFix.None), RunStopClassifier.Fixes(StopReason.KnockedOut));
        Assert.Equal((StopFix.ReloadAndRetry, StopFix.FlagSpot), RunStopClassifier.Fixes(StopReason.Stuck));
        Assert.Equal((StopFix.ReloadAndRetry, StopFix.TeleportCloser), RunStopClassifier.Fixes(StopReason.NoPath));
        Assert.Equal((StopFix.OpenSetup, StopFix.None), RunStopClassifier.Fixes(StopReason.MissingPlugin));
        Assert.Equal((StopFix.OpenJournal, StopFix.None), RunStopClassifier.Fixes(StopReason.Error));
        Assert.True(RunStopClassifier.ReportIsPrimary(StopReason.Error));
        Assert.All(RunStopClassifier.All.Where(r => r != StopReason.Error), r => Assert.False(RunStopClassifier.ReportIsPrimary(r)));
    }

    [Theory]
    [InlineData(QuestionableRunEnd.SingleQuestDone, StopReason.Finished)]
    [InlineData(QuestionableRunEnd.AfterQuests, StopReason.Finished)]
    [InlineData(QuestionableRunEnd.AtTime, StopReason.Finished)]
    [InlineData(QuestionableRunEnd.AfterCurrent, StopReason.Finished)]
    [InlineData(QuestionableRunEnd.StoppedFromTsukimichi, StopReason.Player)]
    [InlineData(QuestionableRunEnd.BeforeDutyWithPlayers, StopReason.DutyGuard)]
    [InlineData(QuestionableRunEnd.SingleQuestNotDone, StopReason.Error)]
    [InlineData(QuestionableRunEnd.Ended, StopReason.Error)]
    public void A_receipt_maps_to_its_reason(QuestionableRunEnd end, StopReason expected) =>
        Assert.Equal(expected, RunStopClassifier.FromReceipt(end, null));

    [Fact]
    public void A_run_that_ends_just_after_a_knock_out_or_stall_says_so_not_error()
    {
        Assert.Equal(StopReason.KnockedOut, RunStopClassifier.FromReceipt(QuestionableRunEnd.Ended, StopReason.KnockedOut));
        Assert.Equal(StopReason.Stuck, RunStopClassifier.FromReceipt(QuestionableRunEnd.SingleQuestNotDone, StopReason.Stuck));

        // A run that finished, or that the player stopped, keeps its own reason whatever happened before.
        Assert.Equal(StopReason.Finished, RunStopClassifier.FromReceipt(QuestionableRunEnd.SingleQuestDone, StopReason.KnockedOut));
        Assert.Equal(StopReason.Player, RunStopClassifier.FromReceipt(QuestionableRunEnd.StoppedFromTsukimichi, StopReason.Stuck));
    }

    [Fact]
    public void Only_runs_Tsukimichi_started_or_a_stop_condition_ended_get_a_card()
    {
        Assert.True(RunStopClassifier.CardFor(QuestionableRunEnd.Ended, QuestionableRunOrigin.SingleQuest));
        Assert.True(RunStopClassifier.CardFor(QuestionableRunEnd.Ended, QuestionableRunOrigin.KeepGoing));
        Assert.True(RunStopClassifier.CardFor(QuestionableRunEnd.AfterQuests, QuestionableRunOrigin.Elsewhere));
        Assert.False(RunStopClassifier.CardFor(QuestionableRunEnd.Ended, QuestionableRunOrigin.Elsewhere));
        Assert.False(RunStopClassifier.CardFor(QuestionableRunEnd.StoppedFromTsukimichi, QuestionableRunOrigin.Elsewhere));
    }

    [Theory]
    [InlineData(GoToGiverFailure.Stuck, StopReason.Stuck)]
    [InlineData(GoToGiverFailure.WalkStoppedShort, StopReason.NoPath)]
    [InlineData(GoToGiverFailure.PathNotReady, StopReason.NoPath)]
    [InlineData(GoToGiverFailure.WalkDidNotStart, StopReason.NoPath)]
    [InlineData(GoToGiverFailure.TeleportTimedOut, StopReason.Error)]
    public void A_travel_give_up_maps_to_its_reason(GoToGiverFailure failure, StopReason expected) =>
        Assert.Equal(expected, RunStopClassifier.FromTravel(failure));

    [Fact]
    public void Every_reason_has_a_title_key()
    {
        Assert.All(RunStopClassifier.All, r => Assert.StartsWith("StopCardTitle", RunStopClassifier.TitleKey(r), StringComparison.Ordinal));
        Assert.Equal(RunStopClassifier.All.Length, RunStopClassifier.All.Select(RunStopClassifier.TitleKey).Distinct().Count());
    }

    [Fact]
    public void The_next_quest_is_the_first_ready_one_the_finished_quest_leads_to()
    {
        static QuestRecord Quest(uint rowId, params uint[] previous) => new()
        {
            RowId = rowId,
            QuestId = QuestRecord.ToQuestId(rowId),
            InternalId = $"Test_{rowId}",
            Name = $"Quest {rowId}",
            PreviousQuests = previous.Length == 0 ? Prereq.None : new Prereq(previous, JoinKind.All),
            Level = 1,
        };

        var catalog = QuestCatalog.Build([Quest(65600), Quest(65601, 65600), Quest(65602, 65600), Quest(65603, 65599)]);
        Assert.Equal(65601u, RunStopClassifier.NextQuest(catalog, 65600, _ => true));
        Assert.Equal(65602u, RunStopClassifier.NextQuest(catalog, 65600, id => id == 65602));
        Assert.Equal(0u, RunStopClassifier.NextQuest(catalog, 65600, _ => false));
        Assert.Equal(0u, RunStopClassifier.NextQuest(catalog, 0, _ => true));
    }

    [Fact]
    public void A_card_without_words_is_refused()
    {
        var dock = new StopDock();
        Assert.Throws<ArgumentException>(() => dock.Raise(Card(StopReason.Stuck, title: " "), 0));
        Assert.Null(dock.Current);
    }

    [Fact]
    public void A_calm_card_never_pushes_out_one_that_needs_you()
    {
        var dock = new StopDock();
        Assert.True(dock.Raise(Card(StopReason.KnockedOut, 1), 0));
        Assert.False(dock.Raise(Card(StopReason.Finished, 2), 1));
        Assert.Equal(StopReason.KnockedOut, dock.Current!.Reason);

        // Another card that needs you does replace it: the newest trouble is the one to read.
        Assert.True(dock.Raise(Card(StopReason.Stuck, 3), 2));
        Assert.Equal(StopReason.Stuck, dock.Current!.Reason);
    }

    [Fact]
    public void The_same_stop_raised_again_refreshes_the_card_and_keeps_what_only_the_first_knew()
    {
        var dock = new StopDock();
        var guard = Card(StopReason.DutyGuard, 7) with { Why = "The next step is The Vault.", DutyId = 33, DutyName = "The Vault" };
        dock.Raise(guard, 5);
        var version = dock.Version;
        dock.Raise(Card(StopReason.DutyGuard, 7) with { Why = "Stopped before a duty with other players.", Context = "Quest · step 3" }, 9);
        Assert.Equal(5, dock.RaisedAt);
        Assert.True(dock.Version > version);
        Assert.Equal("The next step is The Vault.", dock.Current!.Why);
        Assert.Equal(33u, dock.Current.DutyId);
        Assert.Equal("Quest · step 3", dock.Current.Context);
    }

    [Fact]
    public void A_calm_card_fades_after_thirty_seconds_on_screen_and_only_on_screen()
    {
        var dock = new StopDock();
        dock.Raise(Card(StopReason.Finished, 1), 0);
        var t = 0.0;

        // Not shown: its clock stands still.
        for (var i = 0; i < 600; i++)
        {
            t += 0.1;
            dock.Tick(t, shown: false, paused: false, reduceMotion: false);
        }

        Assert.Equal(StopDock.FadeAfterSeconds, dock.Remaining, 3);

        // Hovered: still.
        for (var i = 0; i < 100; i++)
        {
            t += 0.1;
            dock.Tick(t, shown: true, paused: true, reduceMotion: false);
        }

        Assert.Equal(StopDock.FadeAfterSeconds, dock.Remaining, 3);

        for (var i = 0; i < 305 && !dock.Leaving; i++)
        {
            t += 0.1;
            dock.Tick(t, shown: true, paused: false, reduceMotion: false);
        }

        Assert.True(dock.Leaving);
        t += StopDock.LeaveSeconds;
        dock.Tick(t, shown: true, paused: false, reduceMotion: false);
        Assert.Null(dock.Current);
    }

    [Fact]
    public void A_card_that_needs_you_stays_until_dismissed_or_another_hand_off_starts()
    {
        var dock = new StopDock();
        dock.Raise(Card(StopReason.Stuck, 1), 0);
        var t = 0.0;
        for (var i = 0; i < 1000; i++)
        {
            t += 0.1;
            dock.Tick(t, shown: true, paused: false, reduceMotion: false);
        }

        Assert.NotNull(dock.Current);
        Assert.Equal(double.PositiveInfinity, dock.Remaining);

        dock.HandOffStarted(t);
        Assert.True(dock.Leaving);
        dock.Tick(t + 0.2, true, false, false);
        Assert.Null(dock.Current);

        dock.Raise(Card(StopReason.Error, 2), t);
        dock.Dismiss(t + 1);
        dock.Tick(t + 1.01, true, false, reduceMotion: true);
        Assert.Null(dock.Current);
    }

    [Fact]
    public void It_rises_in_over_rise_and_fades_out_over_leave_and_is_still_under_reduce_motion()
    {
        var dock = new StopDock();
        dock.Raise(Card(StopReason.Player, 1), 10);
        Assert.Equal(0f, dock.Alpha(10, false));
        Assert.InRange(dock.Alpha(10.08, false), 0.4f, 0.6f);
        Assert.Equal(1f, dock.Alpha(10.2, false));
        Assert.False(dock.Interactive(10.05, false));
        Assert.True(dock.Interactive(10.2, false));
        Assert.Equal(1f, dock.Alpha(10, true));
        dock.Dismiss(11);
        Assert.InRange(dock.Alpha(11.06, false), 0.4f, 0.6f);
        Assert.False(dock.Interactive(11.0, false));
    }

    [Fact]
    public void Selecting_another_quest_folds_the_card_to_the_status_bar()
    {
        var dock = new StopDock();
        dock.Raise(Card(StopReason.Stuck, 100), 0);
        dock.NoteSelection(50);
        dock.NoteSelection(50);
        Assert.False(dock.Folded);

        // The card's own quest does not fold it; another does.
        dock.NoteSelection(100);
        Assert.False(dock.Folded);
        dock.NoteSelection(200);
        Assert.True(dock.Folded);
        dock.Unfold();
        Assert.False(dock.Folded);

        // A new card opens unfolded.
        dock.NoteSelection(300);
        Assert.True(dock.Folded);
        dock.Raise(Card(StopReason.Error, 9), 1);
        Assert.False(dock.Folded);
    }

    [Fact]
    public void A_card_keeps_where_it_stood()
    {
        var card = Card(StopReason.Stuck) with { Position = new Vector3(1, 2, 3), TerritoryId = 155 };
        Assert.Equal(new Vector3(1, 2, 3), card.Position);
        Assert.Equal(StopCue.Copper, card.Cue);
        Assert.True(card.NeedsYou);
    }
}
