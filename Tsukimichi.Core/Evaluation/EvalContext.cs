using System.Collections.Frozen;
using Tsukimichi.Core.Model;

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

    /// <summary>Quest ids offered by allied societies today. Empty means unknown and the offer is not checked.</summary>
    public IReadOnlySet<ushort> TodaysDailyOffer { get; init; } = FrozenSet<ushort>.Empty;

    /// <summary>
    /// Whether an inactive festival already ran for this character, from curated data. Composed with the default
    /// heuristic (any quest of that festival completed): a festival is past when either says so. Null uses the
    /// heuristic alone. See <see cref="WithFestivalEnds"/> for the curated end-date form.
    /// </summary>
    public Func<ushort, bool>? FestivalIsPast { get; init; }

    /// <summary>Category membership; null admits every job and takes other-job candidates from the snapshot's job levels.</summary>
    public IClassJobCategoryLookup? ClassJobs { get; init; }

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
    byte? Sequence);
