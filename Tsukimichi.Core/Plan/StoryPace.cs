using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Core.Plan;

/// <summary>
/// The pace line of Your story (feature plan v7 N9, spec-1.21 "The page"): how many evenings the rest of the story
/// takes at the character's recent pace. Always an estimate, and none at all below <see cref="StoryPace.MinDated"/>
/// dated story quests.
/// </summary>
/// <param name="Dated">Main scenario quests with a completion date the plugin saw (<see cref="CompletionDateKind.Seen"/>).</param>
/// <param name="Evenings">The evenings the pace is taken over: the most recent ones, at most <see cref="StoryPace.RecentEvenings"/>.</param>
/// <param name="PerEvening">The median number of story quests per evening over <paramref name="Evenings"/>; 0 when not shown.</param>
public sealed record StoryPaceEstimate(int Dated, int Evenings, double PerEvening)
{
    /// <summary>Whether there is enough data to show a pace (<see cref="StoryPace.MinDated"/> dated quests).</summary>
    public bool IsShown => Dated >= StoryPace.MinDated && PerEvening > 0;

    /// <summary>Evenings the <paramref name="questsLeft"/> take at this pace, rounded up; null when the pace is not shown.</summary>
    public int? EveningsFor(int questsLeft) =>
        !IsShown ? null : questsLeft <= 0 ? 0 : (int)Math.Ceiling(questsLeft / PerEvening);
}

/// <summary>
/// The recent pace (N9): an <b>evening</b> is a local day from 05:00 to 05:00 on which the character finished at least
/// one main scenario quest, and the pace is the median number of story quests per evening over the last
/// <see cref="RecentEvenings"/> such evenings. Only dates the plugin saw happen count: a quest found done at a login
/// (<see cref="CompletionDateKind.By"/>) could have been done any time since the capture before, and would heap days of
/// play onto one evening. Pure.
/// </summary>
public static class StoryPace
{
    /// <summary>Fewer dated story quests than this, and the line says so instead of guessing.</summary>
    public const int MinDated = 15;

    /// <summary>The evenings the median is taken over, most recent first.</summary>
    public const int RecentEvenings = 20;

    /// <summary>When an evening turns into the next: 05:00 local, so a session past midnight is one evening.</summary>
    public static readonly TimeSpan DayStart = TimeSpan.FromHours(5);

    /// <summary>The UTC times the plugin saw the character finish each main scenario quest of <paramref name="story"/>.</summary>
    public static List<DateTime> DatedStoryQuests(IEnumerable<QuestRecord> story, CharacterSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(story);
        var dates = new List<DateTime>();
        if (snapshot is null)
        {
            return dates;
        }

        foreach (var quest in story)
        {
            if (CompletionDates.For(snapshot, quest.QuestId) is { Kind: CompletionDateKind.Seen } date)
            {
                dates.Add(date.Utc);
            }
        }

        return dates;
    }

    /// <summary>The estimate from the completion times (UTC) of the character's dated story quests, evenings in <paramref name="zone"/>.</summary>
    public static StoryPaceEstimate Compute(IReadOnlyCollection<DateTime> completedUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(completedUtc);
        ArgumentNullException.ThrowIfNull(zone);
        if (completedUtc.Count < MinDated)
        {
            return new StoryPaceEstimate(completedUtc.Count, 0, 0);
        }

        var perEvening = new Dictionary<DateOnly, int>();
        foreach (var utc in completedUtc)
        {
            var key = Evening(utc, zone);
            perEvening[key] = perEvening.GetValueOrDefault(key) + 1;
        }

        var recent = perEvening.OrderByDescending(static kv => kv.Key).Take(RecentEvenings).Select(static kv => kv.Value).Order().ToArray();
        var mid = recent.Length / 2;
        var median = recent.Length % 2 == 1 ? recent[mid] : (recent[mid - 1] + recent[mid]) / 2.0;
        return new StoryPaceEstimate(completedUtc.Count, recent.Length, median);
    }

    /// <summary>The evening a UTC time falls in: its local date, a time before <see cref="DayStart"/> counting to the day before.</summary>
    public static DateOnly Evening(DateTime utc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        };
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(asUtc, zone) - DayStart);
    }
}
