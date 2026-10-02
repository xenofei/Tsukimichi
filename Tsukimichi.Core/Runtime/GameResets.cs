using System.Globalization;
using System.Runtime.CompilerServices;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The game's daily and weekly resets, and what they clear. The daily reset is at 15:00 UTC: the allied society
/// dailies and their allowances, the daily repeat flags and the "ranked up today" mark start over. The weekly reset is
/// on Tuesday at 08:00 UTC: the weekly repeat flags (One Man's Relic, Seeking Inspiration, the Komra weeklies) start
/// over. Pure; every method takes the clock as an argument.
/// </summary>
public static class GameResets
{
    /// <summary>Hour of the daily reset, UTC.</summary>
    public const int DailyHourUtc = 15;

    /// <summary>Hour of the weekly reset, UTC, on <see cref="WeeklyDay"/>.</summary>
    public const int WeeklyHourUtc = 8;

    /// <summary>Day of the weekly reset (UTC).</summary>
    public const DayOfWeek WeeklyDay = DayOfWeek.Tuesday;

    /// <summary>Allied society quest allowances after the daily reset (<c>QuestManager.GetBeastTribeAllowance</c>).</summary>
    public const byte FullTribeAllowance = 12;

    /// <summary><see cref="QuestRecord.RepeatInterval"/> of a quest that resets daily.</summary>
    public const byte DailyInterval = 1;

    /// <summary><see cref="QuestRecord.RepeatInterval"/> of a quest that resets weekly.</summary>
    public const byte WeeklyInterval = 2;

    // Per catalog: the repeat interval of each QuestRepeatFlag row (index = flag), 0 where no quest carries the flag.
    private static readonly ConditionalWeakTable<QuestCatalog, byte[]> FlagIntervals = [];

    /// <summary>The most recent daily reset at or before <paramref name="nowUtc"/>.</summary>
    public static DateTime LastDaily(DateTime nowUtc)
    {
        var now = AsUtc(nowUtc);
        var today = new DateTime(now.Year, now.Month, now.Day, DailyHourUtc, 0, 0, DateTimeKind.Utc);
        return today <= now ? today : today.AddDays(-1);
    }

    /// <summary>The first daily reset after <paramref name="nowUtc"/>.</summary>
    public static DateTime NextDaily(DateTime nowUtc) => LastDaily(nowUtc).AddDays(1);

    /// <summary>The most recent weekly reset at or before <paramref name="nowUtc"/>.</summary>
    public static DateTime LastWeekly(DateTime nowUtc)
    {
        var now = AsUtc(nowUtc);
        var daysBack = ((int)now.DayOfWeek - (int)WeeklyDay + 7) % 7;
        var day = now.Date.AddDays(-daysBack);
        var reset = new DateTime(day.Year, day.Month, day.Day, WeeklyHourUtc, 0, 0, DateTimeKind.Utc);
        return reset <= now ? reset : reset.AddDays(-7);
    }

    /// <summary>The first weekly reset after <paramref name="nowUtc"/>.</summary>
    public static DateTime NextWeekly(DateTime nowUtc) => LastWeekly(nowUtc).AddDays(7);

    /// <summary>The next reset of a quest with <paramref name="repeatInterval"/>: daily, weekly, or null for any other interval.</summary>
    public static DateTime? Next(byte repeatInterval, DateTime nowUtc) => repeatInterval switch
    {
        DailyInterval => NextDaily(nowUtc),
        WeeklyInterval => NextWeekly(nowUtc),
        _ => null,
    };

    /// <summary>
    /// Which reset cycle <paramref name="nowUtc"/> lies in, as the pair of last resets; two clocks in the same pair
    /// see the same cleared data. A watcher compares it to the one it saw last to notice a reset passing.
    /// </summary>
    public static (DateTime Daily, DateTime Weekly) Cycle(DateTime nowUtc) => (LastDaily(nowUtc), LastWeekly(nowUtc));

    /// <summary>
    /// "resets in 3 h" for a done repeatable of <paramref name="repeatInterval"/>; null for an interval that is neither
    /// daily nor weekly.
    /// </summary>
    public static string? ResetsIn(byte repeatInterval, DateTime nowUtc) =>
        Next(repeatInterval, nowUtc) is { } next ? ResetsIn(next - AsUtc(nowUtc)) : null;

    /// <summary>
    /// "resets in 45 min" under an hour, "resets in 3 h" under a day (rounded to the nearest hour), "resets in 2 d 4 h"
    /// beyond. A time already past reads as one minute.
    /// </summary>
    public static string ResetsIn(TimeSpan left)
    {
        if (left < TimeSpan.FromMinutes(59.5))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
            return F("Core.Reset.InMinutes", "resets in {0} min", minutes);
        }

        var hours = (int)Math.Round(left.TotalHours, MidpointRounding.AwayFromZero);
        if (hours < 24)
        {
            return F("Core.Reset.InHours", "resets in {0} h", hours);
        }

        return hours % 24 == 0
            ? F("Core.Reset.InDays", "resets in {0} d", hours / 24)
            : F("Core.Reset.InDaysHours", "resets in {0} d {1} h", hours / 24, hours % 24);
    }

    /// <summary>
    /// A stored snapshot as it stands at <paramref name="nowUtc"/>: when it was taken before the last daily reset, the
    /// allied society dailies done (<see cref="CharacterSnapshot.DailyDone"/>), the daily repeat flags and the
    /// ranked-up-today marks are cleared and the allowances are full (<see cref="FullTribeAllowance"/>); when taken
    /// before the last weekly reset, the weekly repeat flags are cleared too. A flag no quest of
    /// <paramref name="catalog"/> carries is kept. Returns the same instance when nothing predates a reset; the
    /// snapshot itself, and the file it came from, are never changed.
    /// </summary>
    public static CharacterSnapshot AsOf(CharacterSnapshot snapshot, QuestCatalog catalog, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);

        var taken = AsUtc(snapshot.TakenUtc);
        var dailyStale = taken < LastDaily(nowUtc);
        var weeklyStale = taken < LastWeekly(nowUtc);
        if (!dailyStale && !weeklyStale)
        {
            return snapshot;
        }

        var flags = snapshot.RepeatFlags;
        if (flags.Count > 0)
        {
            var intervals = FlagIntervals.GetValue(catalog, BuildFlagIntervals);
            var kept = new List<byte>(flags.Count);
            foreach (var flag in flags)
            {
                var interval = flag < intervals.Length ? intervals[flag] : (byte)0;
                var cleared = (interval == DailyInterval && dailyStale) || (interval == WeeklyInterval && weeklyStale);
                if (!cleared)
                {
                    kept.Add(flag);
                }
            }

            if (kept.Count != flags.Count)
            {
                flags = kept;
            }
        }

        if (!dailyStale)
        {
            return ReferenceEquals(flags, snapshot.RepeatFlags) ? snapshot : snapshot with { RepeatFlags = flags };
        }

        var tribes = snapshot.Tribes;
        foreach (var standing in snapshot.Tribes.Values)
        {
            if (standing.RankedUpToday)
            {
                var fresh = new Dictionary<byte, TribeStanding>(snapshot.Tribes.Count);
                foreach (var (tribe, value) in snapshot.Tribes)
                {
                    fresh[tribe] = value with { RankedUpToday = false };
                }

                tribes = fresh;
                break;
            }
        }

        return snapshot with
        {
            DailyDone = snapshot.DailyDone.Count == 0 ? snapshot.DailyDone : new Dictionary<ushort, byte>(),
            RepeatFlags = flags,
            TribeAllowance = FullTribeAllowance,
            Tribes = tribes,
        };
    }

    private static byte[] BuildFlagIntervals(QuestCatalog catalog)
    {
        var intervals = new byte[16];
        foreach (var quest in catalog.All)
        {
            if (quest.RepeatFlag != 0)
            {
                if (quest.RepeatFlag >= intervals.Length)
                {
                    Array.Resize(ref intervals, quest.RepeatFlag + 1);
                }

                intervals[quest.RepeatFlag] = quest.RepeatInterval;
            }
        }

        return intervals;
    }

    /// <summary>A clock reading as UTC: a local time is converted, an unspecified one (a file's) is taken as UTC.</summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };

    private static string F(string key, string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);
}
