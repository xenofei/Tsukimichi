using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// Quest completion dates (decision 9): recording starts at the first capture and nothing before it is guessed; a quest
/// seen completing gets the capture's time, one found completed at a login gets "between the previous capture and
/// now", and a date once set is never moved.
/// </summary>
public sealed class CompletionDatesTests
{
    private static readonly DateTime Install = new(2026, 9, 12, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 9, 20, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Login = new(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);

    private static ushort Q(uint rowId) => QuestRecord.ToQuestId(rowId);

    private static CharacterSnapshot Capture(DateTime taken, params uint[] completed) => Fixture.Snapshot(completed) with { TakenUtc = taken };

    [Fact]
    public void The_first_capture_starts_recording_and_dates_nothing_already_complete()
    {
        var first = CompletionDates.Begin(null, Capture(Install, Fixture.A));

        Assert.Equal(Install, first.CompletionDatesSinceUtc);
        Assert.Empty(first.CompletedUtc);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Before, Install), CompletionDates.For(first, Q(Fixture.A)));
        Assert.Null(CompletionDates.For(first, Q(Fixture.B)));

        // A stored file from before 1.5 (no start date) is the same: recording starts now, nothing is backfilled.
        var fromOld = CompletionDates.Begin(Capture(Install.AddDays(-30)), Capture(Install, Fixture.A, Fixture.B));
        Assert.Equal(Install, fromOld.CompletionDatesSinceUtc);
        Assert.Empty(fromOld.CompletedUtc);
        Assert.Empty(fromOld.CompletedAfterUtc);
    }

    [Fact]
    public void A_quest_seen_completing_gets_that_captures_time_and_keeps_it()
    {
        var first = CompletionDates.Begin(null, Capture(Install, Fixture.A));
        var done = CompletionDates.Carry(first, Capture(Later, Fixture.A, Fixture.B));

        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Seen, Later), CompletionDates.For(done, Q(Fixture.B)));
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(done, Q(Fixture.A))!.Value.Kind);
        Assert.Equal(Install, done.CompletionDatesSinceUtc);

        // An unchanged mask shares the maps (the diff compares them by reference) and moves nothing.
        var again = Capture(Later.AddMinutes(1)) with { CompletedBits = done.CompletedBits };
        var same = CompletionDates.Carry(done, again);
        Assert.Same(done.CompletedUtc, same.CompletedUtc);
        Assert.Equal(Later, same.CompletedUtc[Q(Fixture.B)]);

        // A capture that briefly reads the quest as not done (or a seasonal bit the game clears) and then done again
        // keeps the first date.
        var cleared = CompletionDates.Carry(same, Capture(Later.AddMinutes(2), Fixture.A));
        Assert.Null(CompletionDates.For(cleared, Q(Fixture.B)));
        var back = CompletionDates.Carry(cleared, Capture(Later.AddMinutes(3), Fixture.A, Fixture.B));
        Assert.Equal(Later, back.CompletedUtc[Q(Fixture.B)]);
    }

    [Fact]
    public void A_quest_found_completed_at_login_is_dated_between_the_stored_capture_and_the_login()
    {
        var first = CompletionDates.Begin(null, Capture(Install, Fixture.A));
        var stored = CompletionDates.Carry(first, Capture(Later, Fixture.A, Fixture.B));

        // Fixture.C was completed while the plugin was not running.
        var login = CompletionDates.Begin(stored, Capture(Login, Fixture.A, Fixture.B, Fixture.C));

        Assert.Equal(Install, login.CompletionDatesSinceUtc);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.By, Login, Later), CompletionDates.For(login, Q(Fixture.C)));
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Seen, Later), CompletionDates.For(login, Q(Fixture.B)));
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(login, Q(Fixture.A))!.Value.Kind);
    }

    [Fact]
    public void A_quest_complete_before_recording_is_never_dated_when_its_bit_dips_and_returns()
    {
        var first = CompletionDates.Begin(null, Capture(Install, Fixture.A, Fixture.B));
        Assert.Equal(first.CompletedBits, first.CompletedBeforeBits);

        // A small dip (a client still loading, or a chapter the game clears for a replay) and back.
        var dip = CompletionDates.Carry(first, Capture(Later, Fixture.A));
        var back = CompletionDates.Carry(dip, Capture(Later.AddMinutes(1), Fixture.A, Fixture.B));

        Assert.Empty(back.CompletedUtc);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Before, Install), CompletionDates.For(back, Q(Fixture.B)));

        // The same at a login: the stored capture missed it, the login capture has it again.
        var login = CompletionDates.Begin(dip, Capture(Login, Fixture.A, Fixture.B));
        Assert.Empty(login.CompletedUtc);
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(login, Q(Fixture.B))!.Value.Kind);

        // A quest never complete before still gets its date.
        var done = CompletionDates.Carry(back, Capture(Later.AddMinutes(2), Fixture.A, Fixture.B, Fixture.C));
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Seen, Later.AddMinutes(2)), CompletionDates.For(done, Q(Fixture.C)));
    }

    [Fact]
    public void Inline_dates_without_a_before_set_treat_every_undated_completed_quest_as_before()
    {
        // A 1.5 preview's snapshot: dates inline, no record of the quests complete at the start.
        var legacy = Capture(Later, Fixture.A, Fixture.B) with
        {
            CompletionDatesSinceUtc = Install,
            CompletedUtc = new Dictionary<ushort, DateTime> { [Q(Fixture.B)] = Later },
        };

        var before = Capture(Install) with { CompletedBits = CompletionDates.LegacyBefore(legacy) };
        Assert.True(before.IsCompleted(Q(Fixture.A)));
        Assert.False(before.IsCompleted(Q(Fixture.B)));

        // A dips at the login and comes back a poll later: still "before", never dated.
        var login = CompletionDates.Begin(legacy, Capture(Login, Fixture.B));
        var back = CompletionDates.Carry(login, Capture(Login.AddMinutes(1), Fixture.A, Fixture.B));
        Assert.False(back.CompletedUtc.ContainsKey(Q(Fixture.A)));
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(back, Q(Fixture.A))!.Value.Kind);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Seen, Later), CompletionDates.For(back, Q(Fixture.B)));
    }

    [Fact]
    public void A_completed_quest_no_capture_could_place_has_no_date()
    {
        // The dates' basis is older than the snapshot (an older build played the character meanwhile).
        var first = CompletionDates.Begin(null, Capture(Install, Fixture.A));
        var stored = first with { CompletedBits = Fixture.Bits(Fixture.A, Fixture.B) };

        Assert.Null(CompletionDates.For(stored, Q(Fixture.B)));
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(stored, Q(Fixture.A))!.Value.Kind);
    }

    [Fact]
    public void The_first_pass_continues_from_the_dates_file_and_never_starts_over_on_an_unreadable_one()
    {
        var stored = CompletionDates.Carry(CompletionDates.Begin(null, Capture(Install, Fixture.A)), Capture(Later, Fixture.A, Fixture.B));
        var capture = Capture(Login, Fixture.A, Fixture.B, Fixture.C);
        var warnings = new List<string>();

        var loaded = CompletionDates.BeginFrom(SharedRead<CharacterSnapshot>.Of(stored), capture, warnings);
        Assert.Equal(Install, loaded.CompletionDatesSinceUtc);
        Assert.Equal(Later, loaded.CompletedUtc[Q(Fixture.B)]);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.By, Login, Later), CompletionDates.For(loaded, Q(Fixture.C)));

        var missing = CompletionDates.BeginFrom(SharedRead<CharacterSnapshot>.Missing, capture, warnings);
        Assert.Equal(Login, missing.CompletionDatesSinceUtc);
        Assert.Empty(warnings);

        // Locked or failing disk: the pass fails (and is retried) rather than save an empty record over the file.
        var unreadable = new SharedRead<CharacterSnapshot>(SharedLoad.Unreadable, null, "locked");
        Assert.Throws<IOException>(() => CompletionDates.BeginFrom(unreadable, capture, warnings));

        // Corrupt (the store quarantined it): recording starts over, with a warning.
        var invalid = CompletionDates.BeginFrom(new SharedRead<CharacterSnapshot>(SharedLoad.Invalid, null, "bad"), capture, warnings);
        Assert.Equal(Login, invalid.CompletionDatesSinceUtc);
        Assert.Single(warnings);

        // A newer build's file: nothing is recorded this session, so no save can replace it.
        var newer = CompletionDates.BeginFrom(new SharedRead<CharacterSnapshot>(SharedLoad.Newer, null, "newer"), capture, warnings);
        Assert.Null(newer.CompletionDatesSinceUtc);
        Assert.Equal(2, warnings.Count);
        var later = CompletionDates.Carry(newer, Capture(Login.AddMinutes(1), Fixture.A, Fixture.B, Fixture.C, Fixture.D));
        Assert.Null(later.CompletionDatesSinceUtc);
        Assert.Empty(later.CompletedUtc);
        Assert.Null(CompletionDates.For(later, Q(Fixture.D)));
    }

    [Fact]
    public void Resuming_from_this_sessions_last_capture_keeps_its_dates_or_its_lack_of_them()
    {
        var last = CompletionDates.Carry(CompletionDates.Begin(null, Capture(Install, Fixture.A)), Capture(Later, Fixture.A, Fixture.B));
        var resumed = CompletionDates.Resume(last, Capture(Login, Fixture.A, Fixture.B, Fixture.C));
        Assert.Equal(Install, resumed.CompletionDatesSinceUtc);
        Assert.Equal(Later, resumed.CompletedUtc[Q(Fixture.B)]);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.By, Login, Later), CompletionDates.For(resumed, Q(Fixture.C)));

        var withoutDates = Capture(Later, Fixture.A);
        Assert.Null(CompletionDates.Resume(withoutDates, Capture(Login, Fixture.A, Fixture.B)).CompletionDatesSinceUtc);
    }

    [Fact]
    public void Another_characters_stored_file_is_never_merged()
    {
        var other = CompletionDates.Begin(null, Capture(Install, Fixture.B)) with { ContentId = 2 };

        var mine = CompletionDates.Begin(other, Capture(Login, Fixture.A, Fixture.B));

        Assert.Equal(Login, mine.CompletionDatesSinceUtc);
        Assert.Empty(mine.CompletedUtc);
    }
}
