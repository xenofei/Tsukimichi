using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Builds the base <see cref="EvalContext"/> the resolver expects for one catalog: category membership from the
/// bundle, curated festival end dates, and the achievement-gated quests when a source for them exists. Built once
/// per catalog, never per poll: the festival-end map and the hook it feeds are allocated here and shared by every
/// evaluation until the catalog changes. The daily offer stays unknown (see <see cref="EvalContext.TodaysDailyOffer"/>).
/// </summary>
public static class EvalContextBuilder
{
    /// <param name="festivals">Curated festival windows (<see cref="CuratedData.Festivals"/>); an entry without an end date, or a missing entry, leaves that festival to the resolver's completed-quest heuristic.</param>
    /// <param name="classJobs">ClassJobCategory membership from the game data; null admits every job.</param>
    /// <param name="nowUtc">Clock, read on every festival check so a long-lived context stays current.</param>
    /// <param name="achievementGatedRowIds">Quest row ids gated by an achievement; null or empty gates nothing.</param>
    /// <param name="jobParents">ClassJob row id to its <c>ClassJobParent</c> row id, from the sheet; null leaves <see cref="EvalContext.ParentJob"/> unset.</param>
    /// <param name="satisfactionNpcName">Custom delivery client name by SatisfactionNpc row id, from the sheet; null leaves the client out of the requirement detail.</param>
    public static EvalContext Build(
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        IClassJobCategoryLookup? classJobs,
        Func<DateTime> nowUtc,
        IReadOnlySet<uint>? achievementGatedRowIds = null,
        IReadOnlyDictionary<byte, byte>? jobParents = null,
        Func<byte, string>? satisfactionNpcName = null)
    {
        ArgumentNullException.ThrowIfNull(festivals);
        ArgumentNullException.ThrowIfNull(nowUtc);

        var context = EvalContext.Default with { ClassJobs = classJobs };
        if (satisfactionNpcName is not null)
        {
            context = context with { SatisfactionNpcName = satisfactionNpcName };
        }

        if (achievementGatedRowIds is { Count: > 0 } gated)
        {
            context = context with { IsAchievementGated = gated.Contains };
        }

        if (jobParents is { Count: > 0 } parents)
        {
            context = context with { ParentJob = job => parents.TryGetValue(job, out var parent) && parent != 0 ? parent : job };
        }

        var ends = FestivalEnds(festivals);
        return ends.Count == 0 ? context : context.WithFestivalEnds(ends, nowUtc);
    }

    /// <summary>
    /// Festival id to curated end (UTC) for every entry that has one. Entries without an end are left out: "unknown"
    /// must not read as "never ends", and <see cref="EvalContext.WithFestivalEnds"/> treats a missing id the same way.
    /// </summary>
    public static Dictionary<ushort, DateTime?> FestivalEnds(IReadOnlyDictionary<ushort, FestivalInfo> festivals)
    {
        ArgumentNullException.ThrowIfNull(festivals);

        var ends = new Dictionary<ushort, DateTime?>();
        foreach (var (id, info) in festivals)
        {
            if (info.End is { } end)
            {
                ends[id] = end;
            }
        }

        return ends;
    }
}
