using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Seasonal;

/// <summary>A running event that ends within the warning window (<see cref="EventWarnings.EndingSoon"/>).</summary>
/// <param name="Festival">The event, as <see cref="SeasonalNow"/> lists it running.</param>
/// <param name="EndUtc">When it ends.</param>
/// <param name="DaysLeft">Calendar days left in the player's time zone: 0 on the last day.</param>
/// <param name="InJournal">Its quests in the journal, which the game takes away when it ends.</param>
/// <param name="Left">Its quests the character can still take (Ready, here or on another job).</param>
/// <param name="RewardsMissing">Rewards of its quests the character does not have (0 when unknown).</param>
public sealed record EndingSoonEvent(RunningFestival Festival, DateTime EndUtc, int DaysLeft, int InJournal, int Left, int RewardsMissing)
{
    /// <summary>The event ends today.</summary>
    public bool LastDay => DaysLeft == 0;
}

/// <summary>What an ending-soon chat line says is left of the event (<see cref="EventWarnings.ChatLine"/>).</summary>
public enum EndingSoonLeft : byte
{
    /// <summary>Quests in the journal, which the game takes away when the event ends.</summary>
    InJournal,

    /// <summary>Quests the character can still take.</summary>
    ToTake,

    /// <summary>Only rewards the character lacks (no quest in the journal or to take now).</summary>
    Rewards,
}

/// <summary>
/// The parts of one ending-soon chat line: what is left and how many, and the quest to link after it; null
/// <paramref name="Link"/> prints the line without one (only rewards are left), never no line.
/// </summary>
public readonly record struct EndingSoonLine(EndingSoonLeft Left, int Count, QuestRecord? Link);

/// <summary>
/// Seasonal events' ending-soon warnings (feature plan v7, 1.19.0, C10). Forgetting an event until too late, and
/// losing an accepted event quest when the event ends (the game takes it out of the journal), are the most common
/// seasonal losses. A running event whose end is known (<see cref="SeasonalNow.ResolveEnd"/>: the Lodestone, a dated
/// rerun or the player's own date, never a guess) and falls within <c>warnDays</c> calendar days warns, as long as
/// something of it is left: a quest in the journal, one to take, or a reward the character lacks. Pure.
/// </summary>
public static class EventWarnings
{
    /// <summary>The default warning window, days before the end (the coordinator's decision: 3).</summary>
    public const int DefaultWarnDays = 3;

    /// <summary>The longest window the setting offers.</summary>
    public const int MaxWarnDays = 7;

    /// <summary>
    /// Calendar days from <paramref name="nowUtc"/> to <paramref name="endUtc"/> in <paramref name="zone"/>: 0 when the
    /// end falls today, 1 tomorrow. Never negative.
    /// </summary>
    public static int DaysLeft(DateTime endUtc, DateTime nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(Utc(endUtc), zone).Date;
        var now = TimeZoneInfo.ConvertTimeFromUtc(Utc(nowUtc), zone).Date;
        return Math.Max(0, (end - now).Days);
    }

    /// <summary>
    /// The events of <paramref name="running"/> that end within <paramref name="warnDays"/> days (0 turns warnings
    /// off), soonest first, each with what is left of it. <paramref name="rewardsMissing"/> counts the rewards a quest
    /// gives that the character lacks; null leaves rewards out.
    /// </summary>
    public static IReadOnlyList<EndingSoonEvent> EndingSoon(
        IReadOnlyList<RunningFestival> running,
        DateTime nowUtc,
        int warnDays,
        TimeZoneInfo zone,
        Func<QuestRecord, int>? rewardsMissing = null)
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(zone);
        if (warnDays <= 0 || running.Count == 0)
        {
            return [];
        }

        List<EndingSoonEvent>? soon = null;
        foreach (var festival in running)
        {
            if (festival.AnnouncedEndUtc is not { } end || Utc(end) <= Utc(nowUtc))
            {
                continue;
            }

            var days = DaysLeft(end, nowUtc, zone);
            if (days > warnDays)
            {
                continue;
            }

            int inJournal = 0, left = 0, missing = 0;
            foreach (var quest in festival.Quests)
            {
                if (quest.IsSpareAlternative)
                {
                    continue;
                }

                if (quest.State == QuestState.Accepted)
                {
                    inJournal++;
                }
                else if (quest.State is QuestState.Ready or QuestState.ReadyOnOtherJob)
                {
                    left++;
                }

                if (rewardsMissing is not null && quest.State is not (QuestState.Completed or QuestState.DoneThisCycle))
                {
                    missing += rewardsMissing(quest.Quest);
                }
            }

            if (inJournal + left + missing == 0)
            {
                continue;
            }

            (soon ??= []).Add(new EndingSoonEvent(festival, end, days, inJournal, left, missing));
        }

        if (soon is null)
        {
            return [];
        }

        soon.Sort(static (a, b) => a.EndUtc.CompareTo(b.EndUtc));
        return soon;
    }

    /// <summary>
    /// The chat line for <paramref name="warning"/>: the journal's quests when any (linking the first), else the quests
    /// to take (linking the first), else the rewards the character lacks, with no link. Every warning has a line, so the
    /// once-per-login line is never used up by a warning that printed nothing.
    /// </summary>
    public static EndingSoonLine ChatLine(EndingSoonEvent warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        QuestRecord? accepted = null, actionable = null;
        foreach (var quest in warning.Festival.Quests)
        {
            if (quest.IsSpareAlternative)
            {
                continue;
            }

            if (quest.State == QuestState.Accepted)
            {
                accepted = quest.Quest;
                break;
            }

            if (actionable is null && quest.IsActionable)
            {
                actionable = quest.Quest;
            }
        }

        return warning.InJournal > 0 ? new EndingSoonLine(EndingSoonLeft.InJournal, warning.InJournal, accepted ?? actionable)
            : warning.Left > 0 ? new EndingSoonLine(EndingSoonLeft.ToTake, warning.Left, actionable)
            : new EndingSoonLine(EndingSoonLeft.Rewards, warning.RewardsMissing, null);
    }

    /// <summary>
    /// The days left for a quest's "Ends in 2 days" chip: the quest is in the journal and its event ends soon
    /// (<paramref name="soon"/>); null otherwise.
    /// </summary>
    public static int? ChipDays(QuestRecord quest, QuestState state, IReadOnlyList<EndingSoonEvent> soon)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(soon);
        if (state != QuestState.Accepted || quest.Festival == 0)
        {
            return null;
        }

        foreach (var warning in soon)
        {
            if (warning.Festival.FestivalId == quest.Festival)
            {
                return warning.DaysLeft;
            }
        }

        return null;
    }

    /// <summary>
    /// Puts the quests of an ending-soon event that are in the journal first, keeping the order otherwise (a stable
    /// sort): "Event quests already in the journal sort first in Todo and Tonight".
    /// </summary>
    public static List<T> JournalFirst<T>(IEnumerable<T> items, Func<T, QuestRecord> quest, Func<T, QuestState> state, IReadOnlyList<EndingSoonEvent> soon)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(soon);
        var first = new List<T>();
        var rest = new List<T>();
        foreach (var item in items)
        {
            (ChipDays(quest(item), state(item), soon) is not null ? first : rest).Add(item);
        }

        first.AddRange(rest);
        return first;
    }

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}
