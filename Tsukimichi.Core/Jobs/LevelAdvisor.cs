using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Jobs;

/// <summary>
/// The quests one level of a job opens: every quest in <see cref="Quests"/> needs exactly <see cref="Level"/> and
/// nothing else the character lacks. Main scenario quests first, then unlock quests, then the rest, each in journal order.
/// </summary>
/// <param name="Level">The acceptance level the quests need (<see cref="QuestRecord.Level"/>).</param>
/// <param name="MainScenario">How many of them are main scenario quests.</param>
/// <param name="Unlocks">How many are unlock quests (feature quests: a duty, a system, a mount).</param>
public sealed record LevelStep(byte Level, IReadOnlyList<QuestRecord> Quests, int MainScenario, int Unlocks)
{
    public int Count => Quests.Count;
}

/// <summary>
/// What levelling one job opens (R6 C): its level now and, by the level each needs, the Blocked quests whose only
/// unmet requirements are the level and the class or job, and which this job (or the class it grew from, which shares
/// its level) can take. Empty <see cref="Steps"/> when levelling it opens nothing.
/// </summary>
/// <param name="Job">The ClassJob row id the advice speaks for (a job row of the Characters dashboard).</param>
/// <param name="Level">The job's level now.</param>
public sealed record JobLevelAdvice(byte Job, byte Level, IReadOnlyList<LevelStep> Steps)
{
    /// <summary>The nearest level that opens anything; null when nothing does.</summary>
    public LevelStep? Next => Steps.Count > 0 ? Steps[0] : null;

    /// <summary>Every quest levelling this job opens, at every level.</summary>
    public int Total => Steps.Sum(static s => s.Count);
}

/// <summary>The next main scenario quest waits for a level: on <paramref name="Job"/>, now at <paramref name="JobLevel"/>, it needs <paramref name="Level"/>.</summary>
public sealed record MsqLevelGate(QuestRecord Quest, byte Level, byte Job, byte JobLevel);

/// <summary>
/// The level advisor (feature plan v5 "Planning extras", R6 C): which quests levelling a job opens, grouped by the
/// level they need ("Levelling DRG 52→56 opens 7 quests (2 unlock quests, MSQ)"), and whether the next main scenario
/// quest waits for a level. Reads the evaluator's results only, so it agrees with the Journal: a quest counts when it
/// is Blocked on the current job and every unmet requirement is <see cref="RequirementKind.Level"/> or
/// <see cref="RequirementKind.ClassJob"/>; repeatables and quests that leave the totals (a path not taken, a spare
/// alternative) never do. Pure.
/// </summary>
public static class LevelAdvisor
{
    /// <summary>
    /// The advice for each of <paramref name="jobs"/> the character has levelled, in the order given. A job counts the
    /// quests it or its base class (<see cref="EvalContext.ParentJob"/>, which shares the job's level) can take.
    /// </summary>
    /// <param name="featureQuestIds">The unlock quests (<see cref="FeaturePresets"/>), for the steps' unlock counts.</param>
    public static IReadOnlyList<JobLevelAdvice> Compute(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        CharacterSnapshot snapshot,
        EvalContext ctx,
        IEnumerable<byte> jobs,
        IReadOnlySet<uint> featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(featureQuestIds);

        var gated = LevelGated(catalog, states);
        var list = new List<JobLevelAdvice>();
        foreach (var job in jobs)
        {
            var level = RequirementEvaluator.LevelOf(snapshot, job);
            if (level == 0)
            {
                continue;
            }

            var parent = ctx.ParentJob?.Invoke(job) ?? job;
            var byLevel = new SortedDictionary<byte, List<QuestRecord>>();
            foreach (var quest in gated)
            {
                if (quest.Level <= level
                    || (snapshot.LevelCap != 0 && quest.Level > snapshot.LevelCap)
                    || !(RequirementEvaluator.AdmitsJob(quest, snapshot, ctx, job)
                         || (parent != job && parent != 0 && RequirementEvaluator.AdmitsJob(quest, snapshot, ctx, parent))))
                {
                    continue;
                }

                if (!byLevel.TryGetValue(quest.Level, out var at))
                {
                    byLevel[quest.Level] = at = [];
                }

                at.Add(quest);
            }

            var steps = new List<LevelStep>(byLevel.Count);
            foreach (var (needed, quests) in byLevel)
            {
                quests.Sort((a, b) => Order(a, featureQuestIds).CompareTo(Order(b, featureQuestIds)) is var byKind and not 0
                    ? byKind
                    : CompareJournal(a, b));
                steps.Add(new LevelStep(
                    needed,
                    quests,
                    quests.Count(FeaturePresets.IsMainScenario),
                    quests.Count(q => !FeaturePresets.IsMainScenario(q) && featureQuestIds.Contains(q.RowId))));
            }

            list.Add(new JobLevelAdvice(job, level, steps));
        }

        return list;
    }

    /// <summary>
    /// The quests the advice is made of: Blocked, not repeatable, not removed, not leaving the totals, with at least
    /// one unmet requirement and every unmet one a level or a class/job gate. In journal order.
    /// </summary>
    public static IReadOnlyList<QuestRecord> LevelGated(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        var list = new List<QuestRecord>();
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || quest.IsRepeatable || !states.TryGetValue(quest.RowId, out var evaluation) || IsLevelGated(evaluation) is false)
            {
                continue;
            }

            list.Add(quest);
        }

        return list;
    }

    /// <summary>
    /// The next main scenario quest (the first route's, inside a branch region) when it waits only for a level: the job
    /// it is measured on is the current one when that job can take it, else the character's best job that can.
    /// Null when the story is complete, or the next quest is open or waits for something else too.
    /// </summary>
    public static MsqLevelGate? MsqGate(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, CharacterSnapshot snapshot, EvalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ctx);
        if (MsqProgress.Compute(catalog, states) is not { Next: { } next }
            || !states.TryGetValue(next.RowId, out var evaluation)
            || !IsLevelGated(evaluation))
        {
            return null;
        }

        var job = RequirementEvaluator.AdmitsJob(next, snapshot, ctx, snapshot.CurrentJob) && RequirementEvaluator.LevelOf(snapshot, snapshot.CurrentJob) > 0
            ? snapshot.CurrentJob
            : StateResolver.BestAdmittedJob(next, snapshot, ctx);
        if (job is not { } measured)
        {
            return null;
        }

        var level = RequirementEvaluator.LevelOf(snapshot, measured);
        return level >= next.Level ? null : new MsqLevelGate(next, next.Level, measured, level);
    }

    /// <summary>Blocked, in the totals, and held back by nothing but the level and the class or job.</summary>
    private static bool IsLevelGated(QuestEvaluation evaluation)
    {
        if (evaluation.State != QuestState.Blocked || evaluation.LeavesTotals)
        {
            return false;
        }

        var unmet = false;
        foreach (var result in evaluation.Requirements)
        {
            if (result.Met)
            {
                continue;
            }

            if (result.Req.Kind is not (RequirementKind.Level or RequirementKind.ClassJob))
            {
                return false;
            }

            unmet = true;
        }

        // The class/job gate alone is a level gate too: the current job cannot take it and no job that can is high
        // enough (else it would read Ready on another job); each job's own level is compared by the caller.
        return unmet;
    }

    private static int Order(QuestRecord quest, IReadOnlySet<uint> featureQuestIds) =>
        FeaturePresets.IsMainScenario(quest) ? 0 : featureQuestIds.Contains(quest.RowId) ? 1 : 2;

    private static int CompareJournal(QuestRecord a, QuestRecord b)
    {
        var bySort = a.Journal.SortKey.CompareTo(b.Journal.SortKey);
        return bySort != 0 ? bySort : a.RowId.CompareTo(b.RowId);
    }
}
