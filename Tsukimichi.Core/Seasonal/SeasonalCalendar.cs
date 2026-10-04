using System.Text.RegularExpressions;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Seasonal;

/// <summary>
/// One event of the Characters dashboard's Seasonal events list (feature plan v7, 1.19.0, C10): its editions and runs
/// with dates, and what they say about when it comes.
/// </summary>
/// <param name="Name">The event's name without an edition year ("Moonfire Faire").</param>
/// <param name="Windows">Every dated edition or run, newest first (UTC; each with the announcement it was read from).</param>
/// <param name="UsualMonth">The month most of its dated windows start in (1–12); null with none dated.</param>
/// <param name="LastStart">The newest window that has started by now; null when none has.</param>
/// <param name="EndedRecently">That window ended within <see cref="SeasonalCalendar.RecentDays"/> days.</param>
public sealed record SeasonalEventLine(string Name, IReadOnlyList<FestivalRun> Windows, int? UsualMonth, FestivalRun? LastStart, bool EndedRecently);

/// <summary>
/// The dated history of every seasonal and collaboration event in <c>curated/festivals.json</c>, grouped by event: the
/// editions' Lodestone windows (one Festival id each) and the collaborations' dated runs (<see cref="FestivalInfo.Runs"/>,
/// one Festival id for all). It answers "usually August · last ran 2026" and lists the runs on hover. Running events
/// are the game's to say (<see cref="SeasonalNow"/>);
/// this never claims one runs. Pure.
/// </summary>
public static class SeasonalCalendar
{
    /// <summary>A window that ended within this many days reads "ended".</summary>
    public const int RecentDays = 30;

    private static readonly Regex EditionSuffix = new(@"\s*\((\d{4})\)\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The events with at least one dated window, by name; those whose newest window started latest first. Entries
    /// with no date and no run (an announced edition without its window yet) are left out.
    /// </summary>
    public static IReadOnlyList<SeasonalEventLine> Events(IReadOnlyDictionary<ushort, FestivalInfo> curated, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(curated);
        var byName = new Dictionary<string, List<FestivalRun>>(StringComparer.Ordinal);
        foreach (var info in curated.Values)
        {
            if (info.Name.Length == 0)
            {
                continue;
            }

            var name = EditionSuffix.Replace(info.Name, string.Empty);
            if (!byName.TryGetValue(name, out var windows))
            {
                byName[name] = windows = [];
            }

            if (info is { Start: { } start, End: { } end } && info.Evidence is { } evidence)
            {
                windows.Add(new FestivalRun(start, end, evidence));
            }

            windows.AddRange(info.Runs);
        }

        var lines = new List<SeasonalEventLine>(byName.Count);
        foreach (var (name, windows) in byName)
        {
            if (windows.Count == 0)
            {
                continue;
            }

            windows.Sort(static (a, b) => b.Start.CompareTo(a.Start));
            FestivalRun? last = null;
            foreach (var window in windows)
            {
                if (window.Start <= nowUtc)
                {
                    last = window;
                    break;
                }
            }

            var ended = last is { } l && l.End < nowUtc && (nowUtc - l.End).TotalDays <= RecentDays;
            lines.Add(new SeasonalEventLine(name, windows, UsualMonth(windows), last, ended));
        }

        lines.Sort(static (a, b) =>
        {
            var byStart = (b.LastStart?.Start ?? DateTime.MinValue).CompareTo(a.LastStart?.Start ?? DateTime.MinValue);
            return byStart != 0 ? byStart : string.CompareOrdinal(a.Name, b.Name);
        });
        return lines;
    }

    /// <summary>The month most windows start in; on a tie, the month of the newest of them.</summary>
    public static int? UsualMonth(IReadOnlyList<FestivalRun> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (windows.Count == 0)
        {
            return null;
        }

        var counts = new int[13];
        foreach (var window in windows)
        {
            counts[window.Start.Month]++;
        }

        var best = 0;
        DateTime newest = DateTime.MinValue;
        for (var month = 1; month <= 12; month++)
        {
            if (counts[month] == 0)
            {
                continue;
            }

            var latest = windows.Where(w => w.Start.Month == month).Max(static w => w.Start);
            if (best == 0 || counts[month] > counts[best] || (counts[month] == counts[best] && latest > newest))
            {
                best = month;
                newest = latest;
            }
        }

        return best;
    }
}
