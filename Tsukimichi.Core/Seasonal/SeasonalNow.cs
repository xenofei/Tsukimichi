using System.Globalization;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Seasonal;

/// <summary>One quest of a running seasonal event with its state for the character.</summary>
public sealed record SeasonalQuest(QuestRecord Quest, QuestState State);

/// <summary>
/// A seasonal event the game reports as running, with its quests (removed ones left out) in display order: what can be
/// done now first. <paramref name="AnnouncedEndUtc"/> is set only when <c>curated/festivals.json</c> has an end for
/// this Festival id that is still ahead and an https evidence URL; otherwise the event reads "running now" and no date
/// is shown (a date the plugin cannot attribute is never shown, product review §3.4).
/// </summary>
/// <param name="FestivalId">The <c>Festival</c> row id (<see cref="QuestRecord.Festival"/>).</param>
/// <param name="Name">Display name without the edition year ("Moonfire Faire").</param>
/// <param name="ReadyCount">Quests whose state is <see cref="QuestState.Ready"/>.</param>
/// <param name="EndEvidence">The announcement the end was read from, when <paramref name="AnnouncedEndUtc"/> is set.</param>
public sealed record RunningFestival(
    ushort FestivalId,
    string Name,
    IReadOnlyList<SeasonalQuest> Quests,
    int ReadyCount,
    DateTime? AnnouncedEndUtc,
    string? EndEvidence);

/// <summary>One event edition in a character's seasonal history: the quests of it the character completed.</summary>
public sealed record SeasonalHistoryFestival(ushort FestivalId, string Name, IReadOnlyList<QuestRecord> Quests);

/// <summary>A year of seasonal history; <paramref name="Year"/> is null for editions whose year is not known.</summary>
public sealed record SeasonalHistoryYear(int? Year, IReadOnlyList<SeasonalHistoryFestival> Festivals)
{
    /// <summary>Completed quests across the year's editions.</summary>
    public int QuestCount => Festivals.Sum(f => f.Quests.Count);
}

/// <summary>
/// "Seasonal now" (P11): which seasonal events run for a character and what of them is left to do, and the
/// character's seasonal history by year. Pure over the catalog, a snapshot's running festivals, the evaluations and
/// the curated festival windows; the Todo overlay, the Characters dashboard and the login notice all read it.
/// <para>
/// Running events come from the game's festival flags (<see cref="CharacterSnapshot.ActiveFestivals"/>), the same flags
/// the evaluator's <see cref="SeasonalRequirement.Active"/> is built from; never from a date. Festivals run
/// server-wide, so for a stored character the caller passes <see cref="ServerFestivals.For"/>: the live character's
/// flags while someone is logged in, else the stored flags less those a passed curated end shows to be stale. An
/// event's end is shown only from curated data with evidence.
/// </para>
/// <para>
/// Edition year (<see cref="EditionYears"/>): the game's <c>Festival</c> sheet carries no name or year, and a journal
/// genre ("Moonfire Faire Events") none either. So a year is the one in the curated edition name ("Moonfire Faire
/// (2014)", from the Lodestone announcement); an edition without a curated entry is counted from the nearest curated
/// edition of the same journal genre, one edition per year, since the game takes a new Festival id for each
/// edition. When the edition before and the one after disagree (a year whose edition sits in a combined genre such as
/// "Little Ladies' &amp; Hatching-tide Events"), or the genre has no curated edition (collaboration events, which
/// rerun under one id), the year is not known.
/// </para>
/// </summary>
public static class SeasonalNow
{
    /// <summary>"Moonfire Faire (2014)": group 1 is the edition year.</summary>
    private static readonly Regex EditionSuffix = new(@"\s*\((\d{4})\)\s*$", RegexOptions.CultureInvariant);

    private const string EventsSuffix = " Events";

    // Display vocabulary (English in Core, as the todo hints are).
    private const string EndsFormat = "Ends {0} (Lodestone)";
    private const string NamedEndsFormat = "{0}: ends {1} (Lodestone)";
    private const string AnnouncedFormat = "announced to end {0} (Lodestone)";
    public const string RunningNow = "running now";
    private const string NoticeFormat = "{0} is running: {1} {2} ready";
    private const string NoticeEndFormat = " (ends {0})";

    /// <summary>
    /// The events <paramref name="snapshot"/> was captured with as running: right for the live character only. For a
    /// character that may be a stored one, pass <see cref="ServerFestivals.For"/> to the other overload.
    /// </summary>
    public static IReadOnlyList<RunningFestival> Running(
        QuestCatalog catalog,
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlyDictionary<ushort, FestivalInfo> curated,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Running(catalog, ServerFestivals.Of(snapshot), states, curated, nowUtc);
    }

    /// <summary>
    /// The events running on the server (<paramref name="running"/>, see <see cref="ServerFestivals.For"/>), by Festival
    /// id, each with its listed quests and their states from <paramref name="states"/> (a quest without an evaluation
    /// reads <see cref="QuestState.Unknown"/>). A running id with no listed quest in the catalog is left out.
    /// </summary>
    public static IReadOnlyList<RunningFestival> Running(
        QuestCatalog catalog,
        ServerFestivals running,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlyDictionary<ushort, FestivalInfo> curated,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(curated);

        if (running.Ids.Count == 0)
        {
            return [];
        }

        var active = new HashSet<ushort>(running.Ids);
        active.Remove(0);
        var quests = new Dictionary<ushort, List<SeasonalQuest>>();
        foreach (var quest in catalog.All)
        {
            if (quest.Festival == 0 || quest.IsRemoved || !active.Contains(quest.Festival))
            {
                continue;
            }

            if (!quests.TryGetValue(quest.Festival, out var list))
            {
                quests[quest.Festival] = list = [];
            }

            list.Add(new SeasonalQuest(quest, states.TryGetValue(quest.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown));
        }

        var result = new List<RunningFestival>(quests.Count);
        foreach (var id in quests.Keys.Order())
        {
            var list = quests[id];
            list.Sort(static (a, b) =>
            {
                var byRank = Rank(a.State).CompareTo(Rank(b.State));
                if (byRank != 0)
                {
                    return byRank;
                }

                var bySort = a.Quest.Journal.SortKey.CompareTo(b.Quest.Journal.SortKey);
                return bySort != 0 ? bySort : a.Quest.RowId.CompareTo(b.Quest.RowId);
            });

            var ready = list.Count(q => q.State == QuestState.Ready);
            curated.TryGetValue(id, out var info);
            var end = AnnouncedEnd(info, nowUtc);
            result.Add(new RunningFestival(id, DisplayName(id, info, list.Select(q => q.Quest)), list, ready, end, end is null ? null : info!.Evidence));
        }

        return result;
    }

    /// <summary>
    /// True for the states the Todo overlay lists for a running event: startable now (here or on another job) or in
    /// the journal.
    /// </summary>
    public static bool IsActionable(QuestState state) =>
        state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted;

    /// <summary>
    /// The curated end of a festival when it may be shown: the entry has an end that has not passed and an https
    /// evidence URL. Null otherwise, which the surfaces render as "running now".
    /// </summary>
    public static DateTime? AnnouncedEnd(FestivalInfo? info, DateTime nowUtc)
    {
        if (info?.End is not { } end || end < nowUtc || !IsHttps(info.Evidence))
        {
            return null;
        }

        return end;
    }

    /// <summary>The Festival's display name: the curated name without its edition year, else the name of its journal genre.</summary>
    public static string Name(ushort festivalId, QuestCatalog catalog, IReadOnlyDictionary<ushort, FestivalInfo> curated)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(curated);
        curated.TryGetValue(festivalId, out var info);
        return DisplayName(festivalId, info, catalog.All.Where(q => q.Festival == festivalId));
    }

    /// <summary>"Aug 28", or "Jan 14, 2027" when the end falls in another year than <paramref name="nowUtc"/>; the UTC date.</summary>
    public static string DateText(DateTime endUtc, DateTime nowUtc) =>
        endUtc.ToString(endUtc.Year == nowUtc.Year ? "MMM d" : "MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// The Todo overlay's line under its "Event quests running now" header: "Ends Aug 28 (Lodestone)", or with the
    /// event's name first when several events have quests listed. Null without an announced end.
    /// </summary>
    public static string? EndsLine(RunningFestival festival, bool named, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(festival);
        if (festival.AnnouncedEndUtc is not { } end)
        {
            return null;
        }

        var date = DateText(end, nowUtc);
        return named
            ? string.Format(CultureInfo.InvariantCulture, NamedEndsFormat, festival.Name, date)
            : string.Format(CultureInfo.InvariantCulture, EndsFormat, date);
    }

    /// <summary>The dashboard's status for a running event: "announced to end Aug 28 (Lodestone)" or "running now".</summary>
    public static string Status(RunningFestival festival, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(festival);
        return festival.AnnouncedEndUtc is { } end
            ? string.Format(CultureInfo.InvariantCulture, AnnouncedFormat, DateText(end, nowUtc))
            : RunningNow;
    }

    /// <summary>The login notice: "Moonfire Faire is running: 2 quests ready (ends Aug 28)"; the end only when announced.</summary>
    public static string NoticeText(RunningFestival festival, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(festival);
        var text = string.Format(CultureInfo.InvariantCulture, NoticeFormat, festival.Name, festival.ReadyCount, festival.ReadyCount == 1 ? "quest" : "quests");
        return festival.AnnouncedEndUtc is { } end
            ? text + string.Format(CultureInfo.InvariantCulture, NoticeEndFormat, DateText(end, nowUtc))
            : text;
    }

    /// <summary>The edition year in a curated name ("Moonfire Faire (2014)" is 2014); null when the name carries none.</summary>
    public static int? YearInName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var match = EditionSuffix.Match(name);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Edition year per Festival id of the catalog, for the ids whose year is known (see the class remarks): the curated
    /// name's year, else counted along the festival's journal genre from the nearest curated edition, one per year,
    /// when every genre it sits in and both directions agree.
    /// </summary>
    public static IReadOnlyDictionary<ushort, int> EditionYears(QuestCatalog catalog, IReadOnlyDictionary<ushort, FestivalInfo> curated)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(curated);

        var genresOf = new Dictionary<ushort, SortedSet<uint>>();
        var festivalsOf = new Dictionary<uint, SortedSet<ushort>>();
        foreach (var quest in catalog.All)
        {
            if (quest.Festival == 0)
            {
                continue;
            }

            if (!genresOf.TryGetValue(quest.Festival, out var genres))
            {
                genresOf[quest.Festival] = genres = [];
            }

            var genre = quest.Journal.GenreId;
            if (genre == 0)
            {
                continue;
            }

            genres.Add(genre);
            if (!festivalsOf.TryGetValue(genre, out var festivals))
            {
                festivalsOf[genre] = festivals = [];
            }

            festivals.Add(quest.Festival);
        }

        var anchors = new Dictionary<ushort, int>();
        foreach (var id in genresOf.Keys)
        {
            if (curated.TryGetValue(id, out var info) && YearInName(info.Name) is { } year)
            {
                anchors[id] = year;
            }
        }

        var years = new Dictionary<ushort, int>(anchors);
        var sequences = festivalsOf.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        foreach (var (id, genres) in genresOf)
        {
            if (anchors.ContainsKey(id))
            {
                continue;
            }

            int? year = null;
            var known = genres.Count > 0;
            foreach (var genre in genres)
            {
                var counted = CountAlong(sequences[genre], id, anchors, out var conflict);
                if (conflict || (counted is { } y && year is { } previous && previous != y))
                {
                    known = false;
                    break;
                }

                year ??= counted;
            }

            if (known && year is { } derived)
            {
                years[id] = derived;
            }
        }

        return years;
    }

    /// <summary>
    /// The completed seasonal quests of <paramref name="snapshot"/> grouped by edition year, newest year first and the
    /// editions of unknown year last; within a year by Festival id (the order the game added them), quests in journal
    /// order. Removed quests the character completed count: it is history.
    /// </summary>
    public static IReadOnlyList<SeasonalHistoryYear> History(
        QuestCatalog catalog,
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<ushort, FestivalInfo> curated)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(curated);

        var done = new Dictionary<ushort, List<QuestRecord>>();
        var all = new Dictionary<ushort, List<QuestRecord>>();
        foreach (var quest in catalog.All)
        {
            if (quest.Festival == 0)
            {
                continue;
            }

            if (!all.TryGetValue(quest.Festival, out var members))
            {
                all[quest.Festival] = members = [];
            }

            members.Add(quest);
            if (!snapshot.IsCompleted(quest.QuestId))
            {
                continue;
            }

            if (!done.TryGetValue(quest.Festival, out var list))
            {
                done[quest.Festival] = list = [];
            }

            list.Add(quest);
        }

        if (done.Count == 0)
        {
            return [];
        }

        var years = EditionYears(catalog, curated);
        var byYear = new Dictionary<int, List<SeasonalHistoryFestival>>();
        var unknown = new List<SeasonalHistoryFestival>();
        foreach (var id in done.Keys.Order())
        {
            var quests = done[id];
            quests.Sort(static (a, b) =>
            {
                var bySort = a.Journal.SortKey.CompareTo(b.Journal.SortKey);
                return bySort != 0 ? bySort : a.RowId.CompareTo(b.RowId);
            });

            curated.TryGetValue(id, out var info);
            var entry = new SeasonalHistoryFestival(id, DisplayName(id, info, all[id]), quests);
            if (years.TryGetValue(id, out var year))
            {
                if (!byYear.TryGetValue(year, out var list))
                {
                    byYear[year] = list = [];
                }

                list.Add(entry);
            }
            else
            {
                unknown.Add(entry);
            }
        }

        var result = new List<SeasonalHistoryYear>(byYear.Count + 1);
        foreach (var year in byYear.Keys.OrderDescending())
        {
            result.Add(new SeasonalHistoryYear(year, byYear[year]));
        }

        if (unknown.Count > 0)
        {
            result.Add(new SeasonalHistoryYear(null, unknown));
        }

        return result;
    }

    /// <summary>
    /// The year of <paramref name="id"/> counted along one genre's editions (ascending ids) from the nearest anchored
    /// edition before it and after it; <paramref name="conflict"/> when both exist and disagree.
    /// </summary>
    private static int? CountAlong(List<ushort> sequence, ushort id, Dictionary<ushort, int> anchors, out bool conflict)
    {
        conflict = false;
        var index = sequence.IndexOf(id);
        int? fromBelow = null;
        for (var j = index - 1; j >= 0; j--)
        {
            if (anchors.TryGetValue(sequence[j], out var year))
            {
                fromBelow = year + (index - j);
                break;
            }
        }

        int? fromAbove = null;
        for (var k = index + 1; k < sequence.Count; k++)
        {
            if (anchors.TryGetValue(sequence[k], out var year))
            {
                fromAbove = year - (k - index);
                break;
            }
        }

        if (fromBelow is { } below && fromAbove is { } above && below != above)
        {
            conflict = true;
            return null;
        }

        return fromBelow ?? fromAbove;
    }

    /// <summary>The curated name without "(2014)", else the most common journal genre of the quests without " Events".</summary>
    private static string DisplayName(ushort id, FestivalInfo? info, IEnumerable<QuestRecord> quests)
    {
        if (info is { Name.Length: > 0 } named)
        {
            return EditionSuffix.Replace(named.Name, string.Empty);
        }

        var genre = quests
            .Where(q => q.Journal.GenreName.Length > 0)
            .GroupBy(q => q.Journal.GenreName, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(q => q.Journal.GenreId))
            .Select(g => g.Key)
            .FirstOrDefault();
        if (genre is null)
        {
            return "Seasonal event " + id.ToString(CultureInfo.InvariantCulture);
        }

        return genre.EndsWith(EventsSuffix, StringComparison.Ordinal) && genre.Length > EventsSuffix.Length
            ? genre[..^EventsSuffix.Length]
            : genre;
    }

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>What can be done now first, then in the journal, blocked, unknown, and what is done or gone last.</summary>
    private static int Rank(QuestState state) => state switch
    {
        QuestState.Ready => 0,
        QuestState.ReadyOnOtherJob => 1,
        QuestState.Accepted => 2,
        QuestState.Blocked => 3,
        QuestState.Unknown => 4,
        QuestState.DoneThisCycle => 5,
        QuestState.Completed => 6,
        _ => 7,
    };
}
