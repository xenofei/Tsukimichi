using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Seasonal;

/// <summary>
/// Seasonal events ending soon and dated reruns (1.19.0, C10): where an end comes from (the edition's Lodestone date, a
/// collaboration's dated run under way, the player's own date; never a guess), the ending-soon warning only with a
/// known end and something left, the "Ends in 2 days" chip and the journal-first order, and the calendar's "usually
/// August · last ran 2026".
/// </summary>
public sealed class EventWarningsTests
{
    private const ushort Festival = 84;
    private const string Lodestone = "https://na.finalfantasyxiv.com/lodestone/topics/detail/abc";
    private const string Wiki = "https://ffxiv.consolegameswiki.com/wiki/A_Nocturne_for_Heroes";

    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RunEnd = new(2026, 10, 13, 14, 59, 0, DateTimeKind.Utc);

    private static readonly FestivalInfo Rerun = new("A Nocturne for Heroes", null, null, false, Lodestone)
    {
        Runs =
        [
            new FestivalRun(new DateTime(2024, 2, 28, 8, 0, 0, DateTimeKind.Utc), new DateTime(2024, 3, 13, 14, 59, 0, DateTimeKind.Utc), Lodestone),
            new FestivalRun(new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc), RunEnd, Lodestone),
        ],
    };

    private static readonly QuestRecord First = Fixture.Quest(Fixture.A) with { Festival = Festival };
    private static readonly QuestRecord Second = Fixture.Quest(Fixture.B) with { Festival = Festival };
    private static readonly QuestCatalog Catalog = QuestCatalog.Build([First, Second]);

    private static CharacterSnapshot Character() => Fixture.Snapshot() with { ActiveFestivals = [Festival] };

    private static Dictionary<uint, QuestEvaluation> States(QuestState first, QuestState second) => new()
    {
        [Fixture.A] = new QuestEvaluation(first, [], null, null, null),
        [Fixture.B] = new QuestEvaluation(second, [], null, null, null),
    };

    private static RunningFestival Running(FestivalInfo? info, QuestState first, QuestState second, IReadOnlyDictionary<ushort, DateTime>? entered = null, DateTime? now = null)
    {
        var curated = info is null ? new Dictionary<ushort, FestivalInfo>() : new Dictionary<ushort, FestivalInfo> { [Festival] = info };
        return Assert.Single(SeasonalNow.Running(Catalog, Character(), States(first, second), curated, now ?? Now, entered));
    }

    [Fact]
    public void A_collaboration_takes_its_end_from_the_dated_run_under_way()
    {
        var festival = Running(Rerun, QuestState.Accepted, QuestState.Ready);
        Assert.Equal(RunEnd, festival.AnnouncedEndUtc);
        Assert.Equal(FestivalEndSource.Rerun, festival.EndSource);
        Assert.Equal("announced to end Oct 13 (Lodestone)", SeasonalNow.Status(festival, Now));
        Assert.Equal(1, festival.InJournal);

        // Between runs: no end, and the entry stays undated, so nothing reads Locked out.
        var between = Running(Rerun, QuestState.Ready, QuestState.Ready, now: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Null(between.AnnouncedEndUtc);
        Assert.True(Rerun.IsRerun);
        Assert.Equal(RunEnd, SeasonalNow.AnnouncedEnd(Rerun, Now));
    }

    [Fact]
    public void A_run_from_the_wiki_names_its_source()
    {
        var wiki = Rerun with { Runs = [new FestivalRun(Now.AddDays(-5), RunEnd, Wiki)] };
        var festival = Running(wiki, QuestState.Ready, QuestState.Ready);
        Assert.Equal("ends Oct 13 (wiki)", SeasonalNow.Status(festival, Now));
        Assert.Equal("Ends Oct 13 (wiki)", SeasonalNow.EndsLine(festival, named: false, Now));
    }

    [Fact]
    public void The_player_s_date_fills_in_only_when_nothing_curated_does_and_only_while_it_is_ahead()
    {
        var entered = new Dictionary<ushort, DateTime> { [Festival] = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc) };
        var named = new FestivalInfo("A Nocturne for Heroes", null, null, false, Lodestone);

        var own = Running(named, QuestState.Ready, QuestState.Ready, entered);
        Assert.Equal(FestivalEndSource.Entered, own.EndSource);
        Assert.Equal("ends Oct 20 · you entered this", SeasonalNow.Status(own, Now));
        Assert.Equal("Ends Oct 20 (you entered)", SeasonalNow.EndsLine(own, named: false, Now));

        // A curated run wins over the player's date.
        Assert.Equal(FestivalEndSource.Rerun, Running(Rerun, QuestState.Ready, QuestState.Ready, entered).EndSource);

        // A passed date is no end at all.
        var passed = new Dictionary<ushort, DateTime> { [Festival] = Now.AddDays(-1) };
        Assert.Equal(FestivalEndSource.None, Running(named, QuestState.Ready, QuestState.Ready, passed).EndSource);
    }

    [Fact]
    public void Days_left_are_calendar_days_in_the_player_s_zone()
    {
        var utc = TimeZoneInfo.Utc;
        Assert.Equal(3, EventWarnings.DaysLeft(RunEnd, Now, utc));
        Assert.Equal(0, EventWarnings.DaysLeft(RunEnd, new DateTime(2026, 10, 13, 1, 0, 0, DateTimeKind.Utc), utc));

        // At UTC+12, 11:00 UTC on the 10th is still the 10th and 14:59 UTC on the 13th is already the 14th.
        var east = TimeZoneInfo.CreateCustomTimeZone("Plus12", TimeSpan.FromHours(12), "Plus12", "Plus12");
        Assert.Equal(3, EventWarnings.DaysLeft(RunEnd, Now.AddHours(-1), utc));
        Assert.Equal(4, EventWarnings.DaysLeft(RunEnd, Now.AddHours(-1), east));
        Assert.Equal(0, EventWarnings.DaysLeft(Now.AddDays(-1), Now, utc));
    }

    [Fact]
    public void An_event_warns_within_the_window_when_its_end_is_known_and_something_is_left()
    {
        var utc = TimeZoneInfo.Utc;
        var festival = Running(Rerun, QuestState.Accepted, QuestState.Ready);
        var soon = Assert.Single(EventWarnings.EndingSoon([festival], Now, 3, utc, _ => 2));
        Assert.Equal((3, 1, 1, 4), (soon.DaysLeft, soon.InJournal, soon.Left, soon.RewardsMissing));
        Assert.False(soon.LastDay);

        // Outside the window, warnings off, no end known, or nothing left: no warning.
        Assert.Empty(EventWarnings.EndingSoon([festival], Now, 2, utc));
        Assert.Empty(EventWarnings.EndingSoon([festival], Now, 0, utc));
        Assert.Empty(EventWarnings.EndingSoon([Running(null, QuestState.Accepted, QuestState.Ready)], Now, 3, utc));
        Assert.Empty(EventWarnings.EndingSoon([Running(Rerun, QuestState.Completed, QuestState.Completed)], Now, 3, utc, _ => 0));

        // The rewards of a completed quest are not counted as missing.
        var done = Running(Rerun, QuestState.Completed, QuestState.Ready);
        Assert.Equal(2, Assert.Single(EventWarnings.EndingSoon([done], Now, 3, utc, _ => 2)).RewardsMissing);
    }

    [Fact]
    public void The_chip_and_the_order_put_the_ending_event_s_journal_quests_first()
    {
        var festival = Running(Rerun, QuestState.Ready, QuestState.Accepted);
        var soon = EventWarnings.EndingSoon([festival], Now, 3, TimeZoneInfo.Utc);

        Assert.Equal(3, EventWarnings.ChipDays(Second, QuestState.Accepted, soon));
        Assert.Null(EventWarnings.ChipDays(First, QuestState.Ready, soon));
        Assert.Null(EventWarnings.ChipDays(Fixture.Quest(Fixture.C), QuestState.Accepted, soon));

        var ordered = EventWarnings.JournalFirst(festival.Quests, static q => q.Quest, static q => q.State, soon);
        Assert.Equal([Fixture.B, Fixture.A], ordered.Select(q => q.Quest.RowId));
    }

    [Fact]
    public void The_calendar_groups_editions_by_event_with_the_usual_month_and_the_last_run()
    {
        var curated = new Dictionary<ushort, FestivalInfo>
        {
            [1] = new("Moonfire Faire (2024)", new DateTime(2024, 8, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8, 28, 23, 59, 59, DateTimeKind.Utc), false, Lodestone),
            [2] = new("Moonfire Faire (2025)", new DateTime(2025, 8, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 8, 26, 23, 59, 59, DateTimeKind.Utc), false, Lodestone),
            [3] = new("Moonfire Faire (2026)", new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 20, 23, 59, 59, DateTimeKind.Utc), false, Lodestone),
            [4] = new("All Saints' Wake (2026)", null, null, false, Wiki),
            [Festival] = Rerun,
        };

        var lines = SeasonalCalendar.Events(curated, Now);
        Assert.Equal(["A Nocturne for Heroes", "Moonfire Faire"], lines.Select(l => l.Name));

        var moonfire = lines[1];
        Assert.Equal(8, moonfire.UsualMonth);
        Assert.Equal(2026, moonfire.LastStart?.Start.Year);
        Assert.True(moonfire.EndedRecently);
        Assert.Equal(3, moonfire.Windows.Count);

        var nocturne = lines[0];
        Assert.Equal(2, nocturne.Windows.Count);
        Assert.False(nocturne.EndedRecently);
    }

    [Fact]
    public void The_usual_month_breaks_a_tie_toward_the_newest()
    {
        var march = new FestivalRun(new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 3, 10, 0, 0, 0, DateTimeKind.Utc), Lodestone);
        var april = new FestivalRun(new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 4, 10, 0, 0, 0, DateTimeKind.Utc), Lodestone);
        Assert.Equal(4, SeasonalCalendar.UsualMonth([march, april]));
        Assert.Null(SeasonalCalendar.UsualMonth([]));
    }
}
