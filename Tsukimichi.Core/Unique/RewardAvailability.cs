using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// "Can I still get it?" (feature plan v5, decision 4): how a Moonlit reward can be had today, from its quest. In order
/// of preference: a reward several quests give takes the best of them.
/// </summary>
public enum RewardAvailability : byte
{
    /// <summary>Nothing time-limited about it: the quest is in the game for good (whatever its gates).</summary>
    GetNow,

    /// <summary>A seasonal event the server runs right now gives it.</summary>
    EventRunning,

    /// <summary>
    /// A seasonal edition that has not run yet as far as the plugin can tell (no curated end, no completed run, its year
    /// not over, and neither its curated start nor last year's dates put its start behind us): never read as gone before
    /// it has even started.
    /// </summary>
    UpcomingEvent,

    /// <summary>
    /// This year's edition of a seasonal event that is not running now, when the plugin cannot tell whether it is over
    /// or still to come: its curated start has passed, or today falls in or just after last year's dates, and no end is
    /// curated. Neither upcoming nor gone.
    /// </summary>
    EventNotRunning,

    /// <summary>A collaboration event (Yo-kai Watch, Final Fantasy XVI, Fall Guys…): the game reruns these under the same id, so it may return.</summary>
    CollabMayReturn,

    /// <summary>The event (or the quest) is over, but the FFXIV Online Store sells the reward.</summary>
    PastEventOnStore,

    /// <summary>The event is over, or the game removed the quest, and nothing else sells it.</summary>
    GoneForGood,
}

/// <summary>A reward's availability and, for a running event with an announced end, when it ends (UTC).</summary>
public readonly record struct RewardAvailabilityInfo(RewardAvailability Kind, DateTime? EndsUtc = null)
{
    /// <summary>Whether the reward can no longer be had at all.</summary>
    public bool IsGone => Kind == RewardAvailability.GoneForGood;
}

/// <summary>
/// What every availability verdict of one character view shares: the curated festival entries, each festival's
/// edition year (<see cref="SeasonalNow.EditionYears"/>), which festivals the server runs, and the clock.
/// </summary>
public sealed class AvailabilityContext
{
    public AvailabilityContext(
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        IReadOnlyDictionary<ushort, int> editionYears,
        Func<ushort, bool> isRunning,
        DateTime nowUtc,
        IReadOnlyDictionary<ushort, EditionWindow>? editionWindows = null)
    {
        Festivals = festivals ?? throw new ArgumentNullException(nameof(festivals));
        EditionYears = editionYears ?? throw new ArgumentNullException(nameof(editionYears));
        IsRunning = isRunning ?? throw new ArgumentNullException(nameof(isRunning));
        NowUtc = nowUtc;
        EditionWindows = editionWindows ?? NoWindows;
    }

    private static readonly IReadOnlyDictionary<ushort, EditionWindow> NoWindows = new Dictionary<ushort, EditionWindow>();

    /// <summary>No festival known, none running: every reward of a live quest reads <see cref="RewardAvailability.GetNow"/>.</summary>
    public static AvailabilityContext None { get; } = new(
        new Dictionary<ushort, FestivalInfo>(),
        new Dictionary<ushort, int>(),
        static _ => false,
        DateTime.UnixEpoch);

    public IReadOnlyDictionary<ushort, FestivalInfo> Festivals { get; }

    public IReadOnlyDictionary<ushort, int> EditionYears { get; }

    /// <summary>The likely window of the undated editions (<see cref="SeasonalNow.EditionWindows"/>); empty when not known.</summary>
    public IReadOnlyDictionary<ushort, EditionWindow> EditionWindows { get; }

    public Func<ushort, bool> IsRunning { get; }

    public DateTime NowUtc { get; }

    /// <summary>The context for <paramref name="catalog"/> with the server's running festivals (<see cref="ServerFestivals.For"/>).</summary>
    public static AvailabilityContext For(QuestCatalog catalog, IReadOnlyDictionary<ushort, FestivalInfo> festivals, ServerFestivals running, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(festivals);
        ArgumentNullException.ThrowIfNull(running);
        var years = SeasonalNow.EditionYears(catalog, festivals);
        return new AvailabilityContext(festivals, years, running.Contains, nowUtc, SeasonalNow.EditionWindows(catalog, festivals, years));
    }
}

/// <summary>
/// Classifies a Moonlit reward's availability (feature plan v5, decision 4) from its quest, the quest's evaluation for
/// the viewed character, the entry's other sources and the curated festivals. Pure.
/// <list type="bullet">
/// <item>A seasonal quest (<see cref="QuestRecord.Festival"/>): <see cref="RewardAvailability.EventRunning"/> while the
/// server runs its festival (with the curated end when one may be shown, <see cref="SeasonalNow.AnnouncedEnd"/>);
/// otherwise a collaboration (<see cref="FestivalInfo.IsRerun"/>) <see cref="RewardAvailability.CollabMayReturn"/>, never
/// gone; an edition that has not run yet <see cref="RewardAvailability.UpcomingEvent"/>; this year's edition when it
/// cannot be told whether it ran <see cref="RewardAvailability.EventNotRunning"/>; a past one
/// <see cref="RewardAvailability.PastEventOnStore"/> when the Online Store sells the reward, else
/// <see cref="RewardAvailability.GoneForGood"/>.</item>
/// <item>A quest the game removed (<see cref="QuestRecord.IsRetired"/>): on the store, or gone for good.</item>
/// <item>Anything else: <see cref="RewardAvailability.GetNow"/>.</item>
/// </list>
/// An edition is past when its curated end has passed, when the evaluation already locks the quest out (the resolver's
/// own verdict: a curated end or a completed run of it), or when its edition year is over; a curated start still ahead
/// keeps it upcoming, and an edition with no curated entry and no year is taken as past, since the game opens a new
/// festival id for every edition. This year's edition with no curated end reads by last year's dates
/// (<see cref="AvailabilityContext.EditionWindows"/>): upcoming before their start, past a month after their end (events
/// shift by days from year to year), not running in between; without them, not running once a curated start has passed,
/// else upcoming.
/// </summary>
public static class RewardAvailabilities
{
    /// <summary>The availability of <paramref name="entry"/>'s reward through its quest for the viewed character.</summary>
    /// <param name="quest">The entry's quest; null (not in the catalog) reads <see cref="RewardAvailability.GetNow"/>.</param>
    /// <param name="evaluation">The quest's evaluation for the viewed character; null when there is none.</param>
    public static RewardAvailabilityInfo Classify(UniqueRewardEntry entry, QuestRecord? quest, QuestEvaluation? evaluation, AvailabilityContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);
        if (quest is null)
        {
            return new RewardAvailabilityInfo(RewardAvailability.GetNow);
        }

        var store = entry.SoldOnOnlineStore;
        if (quest.Festival != 0)
        {
            var festival = quest.Festival;
            context.Festivals.TryGetValue(festival, out var info);
            if (context.IsRunning(festival))
            {
                return new RewardAvailabilityInfo(RewardAvailability.EventRunning, SeasonalNow.AnnouncedEnd(info, context.NowUtc));
            }

            if (info is { IsRerun: true })
            {
                return new RewardAvailabilityInfo(RewardAvailability.CollabMayReturn);
            }

            switch (EditionOf(festival, info, evaluation, context))
            {
                case Edition.Upcoming:
                    return new RewardAvailabilityInfo(RewardAvailability.UpcomingEvent);
                case Edition.Unclear:
                    return new RewardAvailabilityInfo(RewardAvailability.EventNotRunning);
            }

            return new RewardAvailabilityInfo(store ? RewardAvailability.PastEventOnStore : RewardAvailability.GoneForGood);
        }

        if (quest.IsRetired)
        {
            return new RewardAvailabilityInfo(store ? RewardAvailability.PastEventOnStore : RewardAvailability.GoneForGood);
        }

        return new RewardAvailabilityInfo(RewardAvailability.GetNow);
    }

    /// <summary>Lower is better: the order <see cref="RewardAvailability"/> lists them in.</summary>
    public static int Rank(RewardAvailability availability) => (int)availability;

    /// <summary>The export's value: <c>getNow</c>, <c>eventRunning</c>, <c>upcomingEvent</c>, <c>eventNotRunning</c>, <c>collabMayReturn</c>, <c>pastEventOnStore</c> or <c>goneForGood</c>.</summary>
    public static string ExportName(RewardAvailability availability) => availability switch
    {
        RewardAvailability.GetNow => "getNow",
        RewardAvailability.EventRunning => "eventRunning",
        RewardAvailability.UpcomingEvent => "upcomingEvent",
        RewardAvailability.EventNotRunning => "eventNotRunning",
        RewardAvailability.CollabMayReturn => "collabMayReturn",
        RewardAvailability.PastEventOnStore => "pastEventOnStore",
        RewardAvailability.GoneForGood => "goneForGood",
        _ => availability.ToString(),
    };

    /// <summary>How long after last year's end this year's edition still reads as not running rather than past.</summary>
    private static readonly TimeSpan WindowSlack = TimeSpan.FromDays(30);

    private enum Edition
    {
        Past,
        Upcoming,
        Unclear,
    }

    /// <summary>Where an edition that is not running stands (see the class remarks).</summary>
    private static Edition EditionOf(ushort festival, FestivalInfo? info, QuestEvaluation? evaluation, AvailabilityContext context)
    {
        var now = context.NowUtc;
        if (info?.End is { } end)
        {
            return end < now ? Edition.Past : Edition.Upcoming;
        }

        if (info?.Start is { } start && start > now)
        {
            return Edition.Upcoming;
        }

        if (evaluation is { State: QuestState.Foreclosed })
        {
            return Edition.Past;
        }

        if (!context.EditionYears.TryGetValue(festival, out var year))
        {
            return Edition.Past;
        }

        if (year != now.Year)
        {
            return year < now.Year ? Edition.Past : Edition.Upcoming;
        }

        // This year's edition, not running, no curated end: last year's dates, else the curated start.
        if (context.EditionWindows.TryGetValue(festival, out var window))
        {
            if (now < window.Start)
            {
                return Edition.Upcoming;
            }

            return now > window.End + WindowSlack ? Edition.Past : Edition.Unclear;
        }

        return info?.Start is not null ? Edition.Unclear : Edition.Upcoming;
    }
}
