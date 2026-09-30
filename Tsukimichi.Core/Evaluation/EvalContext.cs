using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// ClassJobCategory membership comes from game data the Core project cannot read; the plugin supplies it.
/// </summary>
public interface IClassJobCategoryLookup
{
    bool Admits(uint categoryId, byte classJobId);

    IEnumerable<byte> JobsIn(uint categoryId);
}

/// <summary>
/// Inputs to evaluation that are not part of the catalog or the character snapshot: curated data, today's
/// offers, plugin-side lookups and display names. Everything has a safe default so the evaluator runs with <see cref="Default"/>.
/// </summary>
public sealed record EvalContext
{
    public static readonly EvalContext Default = new();

    /// <summary>Whether a quest (by row id) is gated by an achievement, from curated data.</summary>
    public Func<uint, bool> IsAchievementGated { get; init; } = static _ => false;

    /// <summary>
    /// Quest ids the allied societies offer today, when a source for it exists. Null means the offer is unknown and
    /// no daily is held back for it: the client never stores the day's offer (its 12-slot daily array holds the
    /// dailies already accepted), so the plugin leaves this null and only a future offer source sets it.
    /// </summary>
    public IReadOnlySet<ushort>? TodaysDailyOffer { get; init; }

    /// <summary>
    /// Whether an inactive festival already ran for this character. Composed with the default heuristic (any quest of
    /// that festival completed): a festival is past when either says so. Null uses the heuristic alone. See
    /// <see cref="WithFestivalEnds"/> for the end-date form. <see cref="CuratedFestivalPast"/> is asked first and, when
    /// it answers, decides.
    /// </summary>
    public Func<ushort, bool>? FestivalIsPast { get; init; }

    /// <summary>
    /// The curated verdict on an inactive festival, checked before <see cref="FestivalIsPast"/> and the completed-quest
    /// heuristic: true when its curated end has passed, false when a curated entry says it is not past (a rerun
    /// collaboration, or an end still ahead), which the heuristic may not overrule; null when curated data has no say
    /// and the other sources decide. See <see cref="WithCuratedFestivals"/>.
    /// </summary>
    public Func<ushort, bool?>? CuratedFestivalPast { get; init; }

    /// <summary>
    /// The events running on the server (<see cref="Evaluation.ServerFestivals.For"/>), read by the seasonal requirement
    /// in place of the snapshot's own flags; null uses the snapshot's. Set it when evaluating a stored character: its
    /// flags are the ones from when it was saved, and festivals run server-wide.
    /// </summary>
    public ServerFestivals? ServerFestivals { get; init; }

    /// <summary>Category membership; null admits every job and takes other-job candidates from the snapshot's job levels.</summary>
    public IClassJobCategoryLookup? ClassJobs { get; init; }

    /// <summary>
    /// The base class a job grew out of (<c>ClassJob.ClassJobParent</c>): Dragoon to Lancer, Paladin to Gladiator;
    /// a class, or a job without one, maps to itself. Null when the plugin has no sheet to answer from, in which case
    /// a class-pinned quest admits only that class.
    /// </summary>
    public Func<byte, byte>? ParentJob { get; init; }

    /// <summary>Whether the character owns a mount; null means not checked.</summary>
    public bool? HasMount { get; init; }

    /// <summary>Whether the character owns a house; null means not checked.</summary>
    public bool? HasHouse { get; init; }

    /// <summary>Allied society rank name for requirement details; defaults to <see cref="TribeRanks.Name"/>. The plugin supplies sheet names for other languages.</summary>
    public Func<byte, string> TribeRankName { get; init; } = TribeRanks.Name;

    /// <summary>Grand Company name for requirement details; defaults to <see cref="GrandCompanies.Name"/>.</summary>
    public Func<byte, string> GrandCompanyName { get; init; } = GrandCompanies.Name;

    /// <summary>Expansion name for requirement details; defaults to <see cref="Expansions.Name"/>.</summary>
    public Func<byte, string> ExpansionName { get; init; } = Expansions.Name;

    /// <summary>Custom delivery client name by SatisfactionNpc row id for requirement details ("M'naago"); empty leaves the client out of the clause.</summary>
    public Func<byte, string> SatisfactionNpcName { get; init; } = static _ => string.Empty;

    /// <summary>
    /// A context whose <see cref="FestivalIsPast"/> answers true for a festival whose curated end lies before
    /// <paramref name="nowUtc"/>. Festivals without an entry, or with a null end, are left to the other sources:
    /// any hook already on this context, then the default completed-quest heuristic in the resolver.
    /// </summary>
    /// <param name="ends">Festival id to end time (UTC), e.g. from curated <c>festivals.json</c>.</param>
    /// <param name="nowUtc">Clock, read on every check so a long-lived context stays current.</param>
    public EvalContext WithFestivalEnds(IReadOnlyDictionary<ushort, DateTime?> ends, Func<DateTime> nowUtc)
    {
        ArgumentNullException.ThrowIfNull(ends);
        ArgumentNullException.ThrowIfNull(nowUtc);

        var previous = FestivalIsPast;
        return this with
        {
            FestivalIsPast = id =>
                (ends.TryGetValue(id, out var end) && end is { } endUtc && endUtc < nowUtc())
                || (previous?.Invoke(id) ?? false),
        };
    }

    /// <summary>
    /// A context whose <see cref="CuratedFestivalPast"/> answers from curated <c>festivals.json</c> entries: an entry
    /// with an end is past once that end lies before <paramref name="nowUtc"/> and not past before it; an undated entry
    /// whose name carries no edition year (<see cref="FestivalInfo.IsRerun"/>, a collaboration the game reruns under
    /// the same id) is never past, so a character who did part of it is not locked out between runs. An undated
    /// edition (a name with its year, "The Rising (2024)") and a festival without an entry are left to the other
    /// sources: <see cref="FestivalIsPast"/>, then the resolver's completed-quest heuristic.
    /// </summary>
    /// <param name="festivals">Festival id to curated entry.</param>
    /// <param name="nowUtc">Clock, read on every check so a long-lived context stays current.</param>
    public EvalContext WithCuratedFestivals(IReadOnlyDictionary<ushort, FestivalInfo> festivals, Func<DateTime> nowUtc)
    {
        ArgumentNullException.ThrowIfNull(festivals);
        ArgumentNullException.ThrowIfNull(nowUtc);

        return this with
        {
            CuratedFestivalPast = id =>
            {
                if (!festivals.TryGetValue(id, out var info))
                {
                    return null;
                }

                if (info.End is { } end)
                {
                    return end < nowUtc();
                }

                return info.IsRerun ? false : null;
            },
        };
    }
}

/// <summary>Outcome of resolving one quest for one character.</summary>
/// <param name="Requirements">Every applicable gate in display order, evaluated on the current job.</param>
/// <param name="NextStep">First unmet requirement; null when nothing blocks.</param>
/// <param name="ReadyOnJob">The job the quest is ready on when <see cref="State"/> is <see cref="QuestState.ReadyOnOtherJob"/>.</param>
/// <param name="Sequence">Journal step when <see cref="State"/> is <see cref="QuestState.Accepted"/>.</param>
public sealed record QuestEvaluation(
    QuestState State,
    IReadOnlyList<RequirementResult> Requirements,
    RequirementResult? NextStep,
    byte? ReadyOnJob,
    byte? Sequence)
{
    /// <summary>
    /// Blocked only because its seasonal event is not running (resolver rule 3). Nothing the character does changes
    /// it until the event returns, so it is treated like a foreclosed quest by every done/total count.
    /// </summary>
    public bool IsOutOfSeason => State == QuestState.Blocked && NextStep is { Req.Kind: RequirementKind.Seasonal };

    /// <summary>
    /// The quest leaves every done/total count (tree nodes, dashboard sections, tab badges, Compare's "neither done"):
    /// it is <see cref="QuestState.Foreclosed"/>, which the character can never do, or <see cref="IsOutOfSeason"/>,
    /// which they cannot do now. A section whose remainder is all of these reads as complete.
    /// </summary>
    public bool LeavesTotals => State == QuestState.Foreclosed || IsOutOfSeason;

    /// <summary>
    /// A repeatable the character has completed at least once (its completion bit is set, or it is done this cycle)
    /// while its <see cref="State"/> reads something else: Ready again the next day, Done this cycle, or blocked by a
    /// requirement that changed since. Always false on a quest that is not repeatable, and on a Completed one.
    /// </summary>
    public bool RepeatableDoneBefore { get; init; }

    /// <summary>
    /// What done/total counts as done: <see cref="QuestState.Completed"/>, or a repeatable completed at least once
    /// (<see cref="RepeatableDoneBefore"/>; only the allied society dailies enter the counts, see
    /// <see cref="QuestRecord.IsAlliedSocietyDaily"/>).
    /// </summary>
    public bool CountsAsDone => State == QuestState.Completed || RepeatableDoneBefore;
}
