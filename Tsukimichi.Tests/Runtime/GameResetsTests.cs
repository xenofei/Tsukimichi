using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Runtime;

public class GameResetsTests
{
    private static DateTime Utc(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Theory]
    // 2026-10-01 is a Thursday.
    [InlineData(10, 1, 14, 59, 9, 30, 15)]
    [InlineData(10, 1, 15, 0, 10, 1, 15)]
    [InlineData(10, 1, 23, 0, 10, 1, 15)]
    [InlineData(10, 2, 0, 30, 10, 1, 15)]
    [InlineData(1, 1, 3, 0, 12, 31, 15)]
    public void The_daily_reset_is_at_15_UTC(int month, int day, int hour, int minute, int lastMonth, int lastDay, int lastHour)
    {
        var now = Utc(month, day, hour, minute);
        var last = GameResets.LastDaily(now);
        var expected = lastMonth == 12 && month == 1 ? new DateTime(2025, 12, lastDay, lastHour, 0, 0, DateTimeKind.Utc) : Utc(lastMonth, lastDay, lastHour);

        Assert.Equal(expected, last);
        Assert.Equal(expected.AddDays(1), GameResets.NextDaily(now));
        Assert.True(last <= now && now < GameResets.NextDaily(now));
    }

    [Theory]
    // Tuesdays: 2026-09-29 and 2026-10-06.
    [InlineData(9, 29, 7, 59, 9, 22)]
    [InlineData(9, 29, 8, 0, 9, 29)]
    [InlineData(10, 1, 12, 0, 9, 29)]
    [InlineData(10, 5, 23, 0, 9, 29)]
    [InlineData(10, 6, 8, 0, 10, 6)]
    public void The_weekly_reset_is_on_Tuesday_at_8_UTC(int month, int day, int hour, int minute, int lastMonth, int lastDay)
    {
        var now = Utc(month, day, hour, minute);
        var last = GameResets.LastWeekly(now);

        Assert.Equal(Utc(lastMonth, lastDay, 8), last);
        Assert.Equal(DayOfWeek.Tuesday, last.DayOfWeek);
        Assert.Equal(last.AddDays(7), GameResets.NextWeekly(now));
    }

    [Fact]
    public void A_local_or_unspecified_clock_reads_as_UTC()
    {
        var utc = Utc(10, 1, 16);
        Assert.Equal(GameResets.LastDaily(utc), GameResets.LastDaily(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
        Assert.Equal(GameResets.LastDaily(utc), GameResets.LastDaily(utc.ToLocalTime()));
    }

    [Theory]
    [InlineData(0, 10, "resets in 10 min")]
    [InlineData(0, 0.2, "resets in 1 min")]
    [InlineData(0, 59, "resets in 59 min")]
    [InlineData(1, 0, "resets in 1 h")]
    [InlineData(2, 40, "resets in 3 h")]
    [InlineData(23, 10, "resets in 23 h")]
    [InlineData(48, 0, "resets in 2 d")]
    [InlineData(100, 0, "resets in 4 d 4 h")]
    public void Resets_in_reads_minutes_hours_then_days(int hours, double minutes, string expected) =>
        Assert.Equal(expected, GameResets.ResetsIn(TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Resets_in_by_interval_counts_to_the_next_daily_or_weekly_reset()
    {
        var now = Utc(10, 1, 12); // Thursday noon: daily in 3 h, weekly on Tuesday 08:00 (4 d 20 h).
        Assert.Equal("resets in 3 h", GameResets.ResetsIn(GameResets.DailyInterval, now));
        Assert.Equal("resets in 4 d 20 h", GameResets.ResetsIn(GameResets.WeeklyInterval, now));
        Assert.Null(GameResets.ResetsIn(0, now));
    }

    private static readonly QuestRecord DailyFlagged = Quest(A) with { IsRepeatable = true, RepeatInterval = 1, RepeatFlag = 3 };
    private static readonly QuestRecord WeeklyFlagged = Quest(B) with { IsRepeatable = true, RepeatInterval = 2, RepeatFlag = 12 };
    private static readonly QuestCatalog FlagCatalog = Catalog(DailyFlagged, WeeklyFlagged);

    private static CharacterSnapshot StoredAt(DateTime taken) => Snapshot() with
    {
        TakenUtc = taken,
        DailyDone = new Dictionary<ushort, byte> { [700] = 1 },
        RepeatFlags = [3, 12, 9],
        TribeAllowance = 4,
        Tribes = new Dictionary<byte, TribeStanding> { [14] = new(4, 300, RankedUpToday: true), [15] = new(2, 10) },
    };

    [Fact]
    public void A_snapshot_from_this_cycle_is_returned_as_it_is()
    {
        var stored = StoredAt(Utc(10, 1, 15, 30));
        Assert.Same(stored, GameResets.AsOf(stored, FlagCatalog, Utc(10, 1, 20)));
    }

    [Fact]
    public void After_the_daily_reset_the_dailies_flags_and_allowances_start_over()
    {
        var stored = StoredAt(Utc(10, 1, 14));
        var now = Utc(10, 1, 16);

        var asOf = GameResets.AsOf(stored, FlagCatalog, now);

        Assert.Empty(asOf.DailyDone);
        Assert.Equal(GameResets.FullTribeAllowance, asOf.TribeAllowance);
        // The daily flag clears; the weekly one (reset Tuesday) and one no quest carries stay.
        Assert.Equal([12, 9], asOf.RepeatFlags);
        Assert.False(asOf.Tribes[14].RankedUpToday);
        Assert.Equal(new TribeStanding(4, 300), asOf.Tribes[14]);
        Assert.Equal(new TribeStanding(2, 10), asOf.Tribes[15]);
        // The stored snapshot itself is untouched.
        Assert.Equal(4, stored.TribeAllowance);
        Assert.Single(stored.DailyDone);
        Assert.Equal(stored.TakenUtc, asOf.TakenUtc);
    }

    [Fact]
    public void After_the_weekly_reset_the_weekly_flags_clear_too()
    {
        var stored = StoredAt(Utc(9, 28, 12)); // Monday
        var asOf = GameResets.AsOf(stored, FlagCatalog, Utc(9, 29, 9)); // Tuesday after 08:00

        Assert.Equal([9], asOf.RepeatFlags);
    }

    [Fact]
    public void A_weekly_reset_alone_keeps_the_daily_data()
    {
        // Taken Tuesday 07:00, read Tuesday 09:00: the weekly reset passed, the daily one (15:00) not yet.
        var stored = StoredAt(Utc(9, 29, 7));
        var asOf = GameResets.AsOf(stored, FlagCatalog, Utc(9, 29, 9));

        Assert.Equal([3, 9], asOf.RepeatFlags);
        Assert.Single(asOf.DailyDone);
        Assert.Equal(4, asOf.TribeAllowance);
        Assert.True(asOf.Tribes[14].RankedUpToday);
    }

    [Fact]
    public void Stored_context_resolves_yesterdays_daily_as_open_and_the_live_one_as_done()
    {
        var daily = Quest(A) with { BeastTribe = 14, BeastRank = 3, IsRepeatable = true, RepeatInterval = 1, DailyPool = 2 };
        var flagged = DailyFlagged with { RowId = B, QuestId = QuestRecord.ToQuestId(B), Name = "Gift" };
        var catalog = Catalog(daily, flagged);
        // The daily was turned in before: its completion bit is set as well as today's slot.
        var stored = Snapshot(A) with
        {
            TakenUtc = Utc(10, 1, 14),
            DailyDone = new Dictionary<ushort, byte> { [daily.QuestId] = 1 },
            RepeatFlags = [3],
            TribeAllowance = 0,
            Tribes = new Dictionary<byte, TribeStanding> { [14] = new(4, 0) },
        };

        var live = StateResolver.ResolveAll(catalog, stored, EvalContext.Default);
        Assert.Equal(QuestState.DoneThisCycle, live[A].State);
        Assert.Equal(QuestState.DoneThisCycle, live[B].State);

        var before = new EvalContext { CycleClock = () => Utc(10, 1, 14, 30) };
        Assert.Equal(QuestState.DoneThisCycle, StateResolver.ResolveAll(catalog, stored, before)[A].State);

        var after = new EvalContext { CycleClock = () => Utc(10, 1, 15, 5) };
        var states = StateResolver.ResolveAll(catalog, stored, after);
        Assert.Equal(QuestState.Ready, states[A].State);
        Assert.Equal(QuestState.Ready, states[B].State);
        Assert.Equal("12 allowances left", Only(states[A].Requirements, RequirementKind.TribeAllowance).Detail);
        // Done before still counts: the daily was turned in once.
        Assert.True(states[A].RepeatableDoneBefore);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(daily, stored, catalog, after).State);
    }

    [Fact]
    public void Cycle_changes_when_either_reset_passes()
    {
        Assert.Equal(GameResets.Cycle(Utc(10, 1, 15, 1)), GameResets.Cycle(Utc(10, 2, 14, 59)));
        Assert.NotEqual(GameResets.Cycle(Utc(10, 1, 14, 59)), GameResets.Cycle(Utc(10, 1, 15)));
        Assert.NotEqual(GameResets.Cycle(Utc(10, 6, 7, 59)), GameResets.Cycle(Utc(10, 6, 8)));
    }
}
