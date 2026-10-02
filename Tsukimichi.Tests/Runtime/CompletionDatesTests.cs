using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
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
    public void Another_characters_stored_file_is_never_merged()
    {
        var other = CompletionDates.Begin(null, Capture(Install, Fixture.B)) with { ContentId = 2 };

        var mine = CompletionDates.Begin(other, Capture(Login, Fixture.A, Fixture.B));

        Assert.Equal(Login, mine.CompletionDatesSinceUtc);
        Assert.Empty(mine.CompletedUtc);
    }
}
