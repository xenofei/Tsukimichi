using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

/// <summary>The plausibility guard (1.5.0 "Trust"): which mid-session captures are held back instead of saved.</summary>
public sealed class CapturePlausibilityTests
{
    private const uint First = 65600;

    /// <summary>Row ids of 600 one-off quests (65600…66199).</summary>
    private static readonly uint[] OneOff = Enumerable.Range(0, 600).Select(i => First + (uint)i).ToArray();

    /// <summary>100 seasonal quests (festival 7) and 100 repeatable ones after them.</summary>
    private static readonly uint[] Seasonal = Enumerable.Range(600, 100).Select(i => First + (uint)i).ToArray();
    private static readonly uint[] Repeatable = Enumerable.Range(700, 100).Select(i => First + (uint)i).ToArray();

    private static readonly QuestCatalog Catalog = Fixture.Catalog(
    [
        .. OneOff.Select(id => Fixture.Quest(id)),
        .. Seasonal.Select(id => Fixture.Quest(id) with { Festival = 7 }),
        .. Repeatable.Select(id => Fixture.Quest(id) with { IsRepeatable = true }),
    ]);

    private static CharacterSnapshot With(IEnumerable<uint> completed, params AcceptedQuest[] journal) =>
        Fixture.Snapshot([.. completed]) with { Accepted = journal };

    [Fact]
    public void Ordinary_progress_is_plausible()
    {
        var last = With(OneOff.Take(300));
        var now = With(OneOff.Take(301));

        var result = CapturePlausibility.Check(last, now, Catalog);

        Assert.True(result.Plausible);
        Assert.Null(result.LogNote);
    }

    [Fact]
    public void A_handful_of_lost_bits_is_plausible()
    {
        var last = With(OneOff.Take(300));
        var now = With(OneOff.Take(300).Skip(5));

        Assert.True(CapturePlausibility.Check(last, now, Catalog).Plausible);
    }

    [Fact]
    public void Losing_more_than_fifty_completed_quests_is_implausible()
    {
        var last = With(OneOff); // 600 completed: 51 is under 10 % but over the absolute cap
        var now = With(OneOff.Skip(51));

        var result = CapturePlausibility.Check(last, now, Catalog);

        Assert.Equal(PlausibilityVerdict.LostCompletions, result.Verdict);
        Assert.Equal(51, result.Lost);
        Assert.Equal(600, result.Completed);
        Assert.Contains("51 of 600", result.LogNote);
    }

    [Fact]
    public void Fifty_lost_of_six_hundred_is_still_plausible()
    {
        Assert.True(CapturePlausibility.Check(With(OneOff), With(OneOff.Skip(50)), Catalog).Plausible);
    }

    [Fact]
    public void A_small_character_is_judged_by_the_share()
    {
        // 40 completed: losing 10 is 25 %.
        var last = With(OneOff.Take(40));
        Assert.Equal(PlausibilityVerdict.LostCompletions, CapturePlausibility.Check(last, With(OneOff.Take(30)), Catalog).Verdict);

        // Losing 9 stays under the share rule's floor.
        Assert.True(CapturePlausibility.Check(last, With(OneOff.Take(31)), Catalog).Plausible);
    }

    [Fact]
    public void Seasonal_and_repeatable_quests_clearing_together_are_plausible()
    {
        // The yearly festival reset and the repeatable reset clear 200 bits at once; the one-off progress is intact.
        var last = With([.. OneOff.Take(100), .. Seasonal, .. Repeatable]);
        var now = With(OneOff.Take(100));

        var result = CapturePlausibility.Check(last, now, Catalog);

        Assert.True(result.Plausible);
        Assert.Equal(0, result.Lost);
        Assert.Equal(100, result.Completed);
    }

    [Fact]
    public void Ids_the_catalog_does_not_name_are_not_counted()
    {
        Assert.True(CapturePlausibility.MayClear(QuestRecord.ToQuestId(70000), Catalog));
        Assert.True(CapturePlausibility.MayClear(QuestRecord.ToQuestId(Seasonal[0]), Catalog));
        Assert.True(CapturePlausibility.MayClear(QuestRecord.ToQuestId(Repeatable[0]), Catalog));
        Assert.False(CapturePlausibility.MayClear(QuestRecord.ToQuestId(OneOff[0]), Catalog));

        var unknown = Enumerable.Range(0, 200).Select(i => 70000u + (uint)i).ToArray();
        Assert.True(CapturePlausibility.Check(With([.. OneOff.Take(10), .. unknown]), With(OneOff.Take(10)), Catalog).Plausible);
    }

    [Fact]
    public void A_capture_that_reads_as_an_empty_character_is_implausible()
    {
        var last = With(OneOff.Take(3), Fixture.Accepted(OneOff[10]));
        var empty = Fixture.Snapshot() with { CompletedBits = new byte[8192] };

        var result = CapturePlausibility.Check(last, empty, Catalog);

        Assert.Equal(PlausibilityVerdict.EmptyCapture, result.Verdict);
        Assert.NotNull(result.LogNote);
    }

    [Fact]
    public void A_journal_of_several_quests_emptying_at_once_is_implausible()
    {
        var last = With(OneOff.Take(5), Fixture.Accepted(OneOff[10]), Fixture.Accepted(OneOff[11]), Fixture.Accepted(OneOff[12]));
        var now = With(OneOff.Take(5));

        var result = CapturePlausibility.Check(last, now, Catalog);

        Assert.Equal(PlausibilityVerdict.EmptiedJournal, result.Verdict);
        Assert.Equal(3, result.JournalBefore);
        Assert.Equal(3, result.JournalLeft);
    }

    [Fact]
    public void Turning_in_the_last_quests_of_the_journal_is_plausible()
    {
        // Three quests leave the journal and all three are completed now.
        var last = With(OneOff.Take(5), Fixture.Accepted(OneOff[10]), Fixture.Accepted(OneOff[11]), Fixture.Accepted(OneOff[12]));
        var now = With([.. OneOff.Take(5), OneOff[10], OneOff[11], OneOff[12]]);

        Assert.True(CapturePlausibility.Check(last, now, Catalog).Plausible);
    }

    [Fact]
    public void Abandoning_the_one_or_two_quests_left_is_plausible()
    {
        var last = With(OneOff.Take(5), Fixture.Accepted(OneOff[10]), Fixture.Accepted(OneOff[11]));

        Assert.True(CapturePlausibility.Check(last, With(OneOff.Take(5)), Catalog).Plausible);
    }

    [Fact]
    public void Another_character_or_no_previous_capture_is_always_plausible()
    {
        var empty = Fixture.Snapshot() with { CompletedBits = new byte[8192] };

        Assert.True(CapturePlausibility.Check(null, empty, Catalog).Plausible);
        Assert.True(CapturePlausibility.Check(With(OneOff) with { ContentId = 2 }, empty, Catalog).Plausible);
    }

    [Fact]
    public void Festival_quests_leaving_the_journal_when_the_festival_ends_are_plausible()
    {
        // The game takes a festival's uncompleted quests out of the journal when it ends; so do repeatables move on.
        var last = With(OneOff.Take(5), Fixture.Accepted(Seasonal[0]), Fixture.Accepted(Seasonal[1]), Fixture.Accepted(Seasonal[2]), Fixture.Accepted(Repeatable[0]));

        var result = CapturePlausibility.Check(last, With(OneOff.Take(5)), Catalog);

        Assert.True(result.Plausible);
        Assert.Equal(0, result.JournalBefore);

        // Three one-off quests vanishing beside them still count.
        var mixed = last with { Accepted = [.. last.Accepted, Fixture.Accepted(OneOff[10]), Fixture.Accepted(OneOff[11]), Fixture.Accepted(OneOff[12])] };
        Assert.Equal(PlausibilityVerdict.EmptiedJournal, CapturePlausibility.Check(mixed, With(OneOff.Take(5)), Catalog).Verdict);
    }

    [Fact]
    public void A_held_back_loss_that_reads_the_same_for_a_few_minutes_is_taken_in()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var last = With(OneOff);
        var lossy = With(OneOff.Skip(100));
        var result = CapturePlausibility.Check(last, lossy, Catalog);
        Assert.False(result.Plausible);
        var held = new HeldBackCaptures();

        Assert.False(held.Observe(last, lossy, result, t0));
        Assert.False(held.Observe(last, lossy, result, t0.AddSeconds(30)));
        Assert.Equal(2, held.Count);
        Assert.Equal(t0, held.SinceUtc);

        // Long enough, but only from the third capture on.
        Assert.False(held.Observe(last, lossy, result, t0.AddMinutes(1)));
        Assert.True(held.Observe(last, lossy, result, t0 + HeldBackCaptures.AcceptAfter));

        // Progress made meanwhile does not change the loss itself.
        var playing = new HeldBackCaptures();
        var moreProgress = With([.. OneOff.Skip(100), .. Seasonal.Take(1)]);
        Assert.False(playing.Observe(last, lossy, result, t0));
        Assert.False(playing.Observe(last, moreProgress, CapturePlausibility.Check(last, moreProgress, Catalog), t0.AddMinutes(1)));
        Assert.True(playing.Observe(last, lossy, result, t0.AddMinutes(2)));
    }

    [Fact]
    public void An_accepted_loss_stays_accepted_until_it_is_committed()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var last = With(OneOff);
        var lossy = With(OneOff.Skip(100));
        var result = CapturePlausibility.Check(last, lossy, Catalog);
        var held = new HeldBackCaptures();
        held.Observe(last, lossy, result, t0);
        held.Observe(last, lossy, result, t0.AddMinutes(1));
        Assert.True(held.Observe(last, lossy, result, t0 + HeldBackCaptures.AcceptAfter));
        Assert.True(held.Accepted);
        Assert.Equal(t0, held.SinceUtc);

        // The deferred pass that was to commit it was dropped at logout: the logout's own capture of the same loss is
        // taken in at once, not held back for another few minutes.
        Assert.True(held.Observe(last, lossy, result, t0 + HeldBackCaptures.AcceptAfter + TimeSpan.FromSeconds(1)));

        // Committed: the watch ends, and a later loss starts from scratch.
        held.Reset();
        Assert.False(held.Accepted);
        Assert.False(held.Observe(last, lossy, result, t0.AddMinutes(10)));

        // A different loss after an acceptance is a new watch.
        var other = With(OneOff.Skip(200));
        held.Observe(last, lossy, result, t0.AddMinutes(11));
        held.Observe(last, lossy, result, t0.AddMinutes(13));
        Assert.True(held.Accepted);
        Assert.False(held.Observe(last, other, CapturePlausibility.Check(last, other, Catalog), t0.AddMinutes(14)));
        Assert.False(held.Accepted);
    }

    [Fact]
    public void A_loss_that_changes_or_heals_starts_the_watch_over()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var last = With(OneOff);
        var lossy = With(OneOff.Skip(100));
        var other = With(OneOff.Skip(200));
        var held = new HeldBackCaptures();

        held.Observe(last, lossy, CapturePlausibility.Check(last, lossy, Catalog), t0);
        held.Observe(last, lossy, CapturePlausibility.Check(last, lossy, Catalog), t0.AddMinutes(1));

        // Another loss: a bad read, still moving.
        Assert.False(held.Observe(last, other, CapturePlausibility.Check(last, other, Catalog), t0.AddMinutes(3)));
        Assert.Equal(1, held.Count);
        Assert.Equal(t0.AddMinutes(3), held.SinceUtc);

        // A plausible capture ends the watch.
        Assert.False(held.Observe(last, last, CapturePlausibility.Check(last, last, Catalog), t0.AddMinutes(4)));
        Assert.Equal(0, held.Count);
        Assert.Null(held.SinceUtc);
    }

    [Fact]
    public void An_empty_capture_is_never_taken_in()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var last = With(OneOff.Take(3), Fixture.Accepted(OneOff[10]));
        var empty = Fixture.Snapshot() with { CompletedBits = new byte[8192] };
        var result = CapturePlausibility.Check(last, empty, Catalog);
        var held = new HeldBackCaptures();

        for (var minute = 0; minute < 30; minute++)
        {
            Assert.False(held.Observe(last, empty, result, t0.AddMinutes(minute)));
        }
    }

    [Fact]
    public void A_shorter_mask_counts_the_missing_bytes_as_cleared()
    {
        var last = With(OneOff);
        var now = last with { CompletedBits = last.CompletedBits[..^20] };

        Assert.Equal(PlausibilityVerdict.LostCompletions, CapturePlausibility.Check(last, now, Catalog).Verdict);
    }

    /// <summary>The first 200 one-off quests stand for the New Game+ chapters (main scenario, job quests).</summary>
    private static readonly HashSet<uint> Replayable = [.. OneOff.Take(200)];

    [Fact]
    public void A_small_loss_of_replayable_quests_is_a_New_Game_plus_replay()
    {
        // Three quests of a chapter read as not completed: under every threshold, so it used to be saved at once.
        var last = With(OneOff.Take(300));
        var now = With(OneOff.Take(300).Skip(3));

        Assert.True(CapturePlausibility.Check(last, now, Catalog).Plausible);

        var result = CapturePlausibility.Check(last, now, Catalog, Replayable);
        Assert.Equal(PlausibilityVerdict.NewGamePlusReplay, result.Verdict);
        Assert.Equal(3, result.Lost);
        Assert.Contains("New Game+", result.LogNote, StringComparison.Ordinal);
    }

    [Fact]
    public void A_whole_replayed_chapter_is_a_New_Game_plus_replay_not_a_lost_reading()
    {
        var last = With(OneOff);
        var now = With(OneOff.Skip(150));

        Assert.Equal(PlausibilityVerdict.LostCompletions, CapturePlausibility.Check(last, now, Catalog).Verdict);
        Assert.Equal(PlausibilityVerdict.NewGamePlusReplay, CapturePlausibility.Check(last, now, Catalog, Replayable).Verdict);
    }

    [Fact]
    public void A_New_Game_plus_replay_is_never_taken_in()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var last = With(OneOff);
        var replay = With(OneOff.Skip(32));
        var result = CapturePlausibility.Check(last, replay, Catalog, Replayable);
        var held = new HeldBackCaptures();

        for (var minute = 0; minute < 60; minute++)
        {
            Assert.False(held.Observe(last, replay, result, t0.AddMinutes(minute)));
        }

        Assert.False(held.Accepted);
    }

    [Fact]
    public void A_loss_beyond_the_replayable_quests_keeps_the_usual_rules()
    {
        // One quest no New Game+ chapter lists is among the lost: not a replay, so the thresholds decide as before.
        var last = With(OneOff.Take(300));
        var mixed = With(OneOff.Take(300).Where((_, i) => i is not (0 or 1 or 2 or 250)));
        Assert.True(CapturePlausibility.Check(last, mixed, Catalog, Replayable).Plausible);

        var big = With(OneOff.Skip(100).Take(100));
        Assert.Equal(PlausibilityVerdict.LostCompletions, CapturePlausibility.Check(With(OneOff), big, Catalog, Replayable).Verdict);
    }

    [Fact]
    public void Seasonal_bits_clearing_beside_a_replay_do_not_hide_it()
    {
        // A festival's quests reset while a chapter is replayed: they are left out of the counts, so the replay shows.
        var last = With([.. OneOff.Take(300), .. Seasonal.Take(20)]);
        var now = With(OneOff.Take(300).Skip(5));

        Assert.Equal(PlausibilityVerdict.NewGamePlusReplay, CapturePlausibility.Check(last, now, Catalog, Replayable).Verdict);
    }

    [Fact]
    public void A_New_Game_plus_replay_commits_new_progress_and_keeps_the_replayed_quests()
    {
        // Mid-chapter: three replayed quests read as not completed, while the character also levelled and completed a
        // quest no chapter lists. Before, the whole capture was held back on every poll for as long as the replay lasted.
        var last = With(OneOff.Take(300)) with { JobLevels = Fixture.Levels((Fixture.Gladiator, 50)) };
        var now = With(OneOff.Take(301).Skip(3)) with { JobLevels = Fixture.Levels((Fixture.Gladiator, 51)) };
        Assert.Equal(PlausibilityVerdict.NewGamePlusReplay, CapturePlausibility.Check(last, now, Catalog, Replayable).Verdict);

        var (capture, result) = CapturePlausibility.Judge(last, now, Catalog, Replayable);

        Assert.True(result.Plausible);
        Assert.All(OneOff.Take(301), id => Assert.True(capture.IsCompleted(QuestRecord.ToQuestId(id))));
        Assert.Equal((short)51, capture.JobLevels[Fixture.Gladiator]);
        var diff = SnapshotDiff.Compute(last, capture);
        Assert.Equal([QuestRecord.ToQuestId(OneOff[300])], diff.ChangedQuestIds);
        Assert.Equal([Fixture.Gladiator], diff.ChangedJobs);
    }

    [Fact]
    public void A_New_Game_plus_replay_keeps_only_the_replayed_quests()
    {
        var last = With([.. OneOff.Take(300), .. Seasonal.Take(20)]);
        var now = With(OneOff.Take(300).Skip(5));

        var (capture, result) = CapturePlausibility.Judge(last, now, Catalog, Replayable);

        Assert.True(result.Plausible);
        Assert.All(OneOff.Take(5), id => Assert.True(capture.IsCompleted(QuestRecord.ToQuestId(id))));
        // The festival's quests stay cleared as the capture read them: only the replayed chapter's are kept.
        Assert.Equal(Seasonal.Take(20).Select(QuestRecord.ToQuestId), SnapshotDiff.Compute(last, capture).ChangedQuestIds);
    }

    [Fact]
    public void A_login_mid_chapter_goes_live_with_the_stored_progress()
    {
        // The first capture of a session, judged against the stored snapshot, lacks a whole replayed chapter.
        var stored = With(OneOff);
        var login = With(OneOff.Skip(150));

        var (capture, result) = CapturePlausibility.Judge(stored, login, Catalog, Replayable);

        Assert.True(result.Plausible);
        Assert.Same(stored.CompletedBits, capture.CompletedBits);
        Assert.True(SnapshotDiff.Compute(stored, capture).IsEmpty);
    }

    [Fact]
    public void Judge_leaves_any_other_capture_as_it_is()
    {
        var last = With(OneOff.Take(300));
        var progress = With(OneOff.Take(301));
        var (same, plausible) = CapturePlausibility.Judge(last, progress, Catalog, Replayable);
        Assert.Same(progress, same);
        Assert.True(plausible.Plausible);

        // A loss beyond the replayable quests is held back as before, with the capture untouched.
        var big = With(OneOff.Skip(100).Take(100));
        var (untouched, lost) = CapturePlausibility.Judge(With(OneOff), big, Catalog, Replayable);
        Assert.Same(big, untouched);
        Assert.Equal(PlausibilityVerdict.LostCompletions, lost.Verdict);
    }

    [Fact]
    public void Ordinary_progress_stays_plausible_with_the_replayable_quests_known()
    {
        var last = With(OneOff.Take(300));
        Assert.True(CapturePlausibility.Check(last, With(OneOff.Take(301)), Catalog, Replayable).Plausible);
        Assert.True(CapturePlausibility.Check(last, last, Catalog, Replayable).Plausible);
    }
}
