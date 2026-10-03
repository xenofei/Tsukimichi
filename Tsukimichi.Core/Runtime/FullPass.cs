using System.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>What <see cref="FullPass.Judge"/> found when a full pass landed.</summary>
public enum FullPassVerdict
{
    /// <summary>Nothing the pass was computed from moved: commit it.</summary>
    Commit,

    /// <summary>The catalog was replaced (a retry or a filing flip); the next poll starts a first pass.</summary>
    CatalogChanged,

    /// <summary>Another character is logged in now; the next poll starts over for that one.</summary>
    CharacterChanged,

    /// <summary>
    /// The capture the pass was diffed against is no longer the committed one; the next poll captures again and
    /// diffs against what is committed now.
    /// </summary>
    BaseChanged,
}

/// <summary>A full pass finished on a worker: the evaluations of the capture, its events and the resolve's wall time.</summary>
public sealed record FullPassResult(
    Dictionary<uint, QuestEvaluation> States,
    List<QuestEvent> Events,
    double ResolveMs);

/// <summary>
/// A re-evaluation of the whole catalog after a capture changed something the reverse index cannot narrow down to a
/// few quests: a job or level change, a duty clear, an allowance or rank, today's allied society offer, a new mount, or many quests
/// at once. It took 14–17 ms, a dropped frame on every gearset change, so the poller runs it on a worker: it decides on
/// the framework thread whether a diff needs one (<see cref="Needed"/>), runs <see cref="Run"/> over the immutable
/// capture, and on a later frame commits the result only when <see cref="Judge"/> finds nothing it was computed from
/// moved. Until then the session keeps showing the previous evaluations. Pure; <see cref="Run"/> is safe off-thread.
/// </summary>
public static class FullPass
{
    /// <summary>Above this many changed quests a full resolve is cheaper than walking dependents.</summary>
    public const int ChangedQuestThreshold = 200;

    /// <summary>
    /// Whether a diff needs the whole catalog resolved. A level change touches every level-gated quest, which the
    /// reverse index cannot enumerate by job, and the other inputs (current job, duties, allowances, ranks) touch
    /// quests it cannot enumerate at all; a new daily offer changes every allied society daily, and a new mount every
    /// quest that needs one owned (<see cref="SnapshotDiff.MountsChanged"/>: the last mount of a collection opens its gate).
    /// </summary>
    public static bool Needed(SnapshotDiff diff, bool offerChanged, int threshold = ChangedQuestThreshold)
    {
        ArgumentNullException.ThrowIfNull(diff);
        return diff.OtherChanged
            || diff.MountsChanged
            || offerChanged
            || diff.ChangedJobs.Count > 0
            || diff.ChangedQuestIds.Count > threshold;
    }

    /// <summary>
    /// The worker's part: every quest resolved for <paramref name="snapshot"/>, then the events of the change from
    /// <paramref name="last"/> with the previous evaluations, exactly as the poller derives them on the framework thread.
    /// </summary>
    /// <param name="previous">The evaluations committed with <paramref name="last"/>; read only.</param>
    public static FullPassResult Run(
        QuestCatalog catalog,
        CharacterSnapshot last,
        CharacterSnapshot snapshot,
        SnapshotDiff diff,
        EvalContext context,
        IReadOnlyDictionary<uint, QuestEvaluation> previous,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(last);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(previous);

        var started = Stopwatch.GetTimestamp();
        var states = StateResolver.ResolveAll(catalog, snapshot, context);
        var resolveMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var events = QuestEvents.Derive(diff, last, snapshot, catalog, previous, states, nowUtc);
        return new FullPassResult(states, events, resolveMs);
    }

    /// <summary>
    /// Whether a finished pass may be committed: the catalog it resolved against is still the session's, the character
    /// it captured is still the one logged in, and the capture it was diffed against is still the committed one.
    /// Checked in that order; the first that moved is the verdict.
    /// </summary>
    /// <param name="passCatalog">The catalog instance the pass resolved against.</param>
    /// <param name="currentCatalog">The session's catalog instance now; null while there is none.</param>
    /// <param name="passContentId">The character the pass captured.</param>
    /// <param name="liveContentId">The character logged in now; null or 0 when nobody is.</param>
    /// <param name="passBase">The committed capture the pass was diffed against.</param>
    /// <param name="currentBase">The committed capture now; null after a reset.</param>
    public static FullPassVerdict Judge(
        object passCatalog,
        object? currentCatalog,
        ulong passContentId,
        ulong? liveContentId,
        CharacterSnapshot passBase,
        CharacterSnapshot? currentBase)
    {
        ArgumentNullException.ThrowIfNull(passCatalog);
        ArgumentNullException.ThrowIfNull(passBase);

        if (!ReferenceEquals(passCatalog, currentCatalog))
        {
            return FullPassVerdict.CatalogChanged;
        }

        if (liveContentId != passContentId)
        {
            return FullPassVerdict.CharacterChanged;
        }

        return ReferenceEquals(passBase, currentBase) ? FullPassVerdict.Commit : FullPassVerdict.BaseChanged;
    }
}
