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
/// offers and plugin-side lookups. Everything has a safe default so the evaluator runs with <see cref="Default"/>.
/// </summary>
public sealed record EvalContext
{
    public static readonly EvalContext Default = new();

    /// <summary>Whether a quest (by row id) is gated by an achievement, from curated data.</summary>
    public Func<uint, bool> IsAchievementGated { get; init; } = static _ => false;

    /// <summary>Quest ids offered by allied societies today. Empty means unknown and the offer is not checked.</summary>
    public IReadOnlySet<ushort> TodaysDailyOffer { get; init; } = new HashSet<ushort>();

    /// <summary>
    /// Whether an inactive festival already ran for this character. Null uses the default heuristic:
    /// any quest of that festival completed by the character.
    /// </summary>
    public Func<ushort, bool>? FestivalIsPast { get; init; }

    /// <summary>Category membership; null admits every job and takes other-job candidates from the snapshot's job levels.</summary>
    public IClassJobCategoryLookup? ClassJobs { get; init; }

    /// <summary>Whether the character owns a mount; null means not checked.</summary>
    public bool? HasMount { get; init; }

    /// <summary>Whether the character owns a house; null means not checked.</summary>
    public bool? HasHouse { get; init; }
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
