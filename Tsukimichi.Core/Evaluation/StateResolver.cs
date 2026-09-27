using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Resolves a quest's <see cref="QuestState"/> for one character. Rules run in the order of spec section 5;
/// the first decisive rule wins. Pure: no game access, safe to run off-thread.
/// </summary>
public static class StateResolver
{
    public static QuestEvaluation Resolve(QuestRecord q, CharacterSnapshot s, QuestCatalog c, EvalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(ctx);

        return ResolveCore(q, s, c, ctx, ctx.FestivalIsPast ?? (id => DefaultFestivalIsPast(id, s, c)));
    }

    /// <summary>Resolves every quest in the catalog.</summary>
    public static Dictionary<uint, QuestEvaluation> ResolveAll(QuestCatalog c, CharacterSnapshot s, EvalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(ctx);

        var festivalIsPast = MemoizedFestivalIsPast(s, c, ctx);
        var results = new Dictionary<uint, QuestEvaluation>(c.Count);
        foreach (var quest in c.All)
        {
            results[quest.RowId] = ResolveCore(quest, s, c, ctx, festivalIsPast);
        }

        return results;
    }

    /// <summary>
    /// Re-resolves only the rows a change can affect: the changed quests, quests that list them as previous quests or
    /// locks, quests sharing a changed quest's festival, and, when given, quests at changed levels or of changed festivals.
    /// Untouched rows keep their previous <see cref="QuestEvaluation"/> instance.
    /// </summary>
    public static Dictionary<uint, QuestEvaluation> ResolveDependents(
        IReadOnlyDictionary<uint, QuestEvaluation> previousResults,
        IEnumerable<uint> changedQuestIds,
        ReversePrereqIndex index,
        QuestCatalog c,
        CharacterSnapshot s,
        EvalContext ctx,
        IEnumerable<byte>? changedLevels = null,
        IEnumerable<ushort>? changedFestivals = null)
    {
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(changedQuestIds);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(ctx);

        var affected = new HashSet<uint>();
        foreach (var rowId in changedQuestIds)
        {
            if (c.Get(rowId) is not { } changed)
            {
                continue;
            }

            affected.Add(rowId);
            affected.UnionWith(index.Dependents(rowId));
            if (changed.Festival != 0)
            {
                affected.UnionWith(index.ByFestival(changed.Festival));
            }
        }

        foreach (var level in changedLevels ?? [])
        {
            affected.UnionWith(index.ByLevel(level));
        }

        foreach (var festival in changedFestivals ?? [])
        {
            affected.UnionWith(index.ByFestival(festival));
        }

        var results = new Dictionary<uint, QuestEvaluation>(previousResults);
        if (affected.Count == 0)
        {
            return results;
        }

        var festivalIsPast = MemoizedFestivalIsPast(s, c, ctx);
        foreach (var rowId in affected)
        {
            results[rowId] = ResolveCore(c.ByRowId[rowId], s, c, ctx, festivalIsPast);
        }

        return results;
    }

    private static QuestEvaluation ResolveCore(QuestRecord q, CharacterSnapshot s, QuestCatalog c, EvalContext ctx, Func<ushort, bool> festivalIsPast)
    {
        var completed = s.IsCompleted(q.QuestId);
        var requirements = RequirementEvaluator.Evaluate(q, s, c, ctx);

        // 1. Completed, unless repeatable (rule 5 owns those).
        if (completed && !q.IsRepeatable)
        {
            return new(QuestState.Completed, requirements, null, null, null);
        }

        // 2. A completed lock forecloses, except for a Grand Company quest the character could still switch to.
        if (q.QuestLocks.Length > 0
            && !RequirementEvaluator.IsSwitchableGrandCompanyQuest(q, s)
            && q.QuestLocks.Any(id => s.IsCompleted(QuestRecord.ToQuestId(id))))
        {
            return new(QuestState.Foreclosed, requirements, FirstOfKind(requirements, RequirementKind.Foreclosure), null, null);
        }

        // 3. Inactive festival: foreclosed if the character already saw a run of it, otherwise blocked as seasonal.
        if (q.Festival != 0 && !s.ActiveFestivals.Contains(q.Festival))
        {
            var state = festivalIsPast(q.Festival) ? QuestState.Foreclosed : QuestState.Blocked;
            return new(state, requirements, FirstOfKind(requirements, RequirementKind.Seasonal), null, null);
        }

        // 4. In the journal.
        foreach (var accepted in s.Accepted)
        {
            if (accepted.QuestId == q.QuestId)
            {
                return new(QuestState.Accepted, requirements, null, null, accepted.Sequence);
            }
        }

        // 5. Repeatable already done this cycle: daily flag, or the completion bit on a repeatable.
        if (q.IsRepeatable && (completed || s.DailyDone.ContainsKey(q.QuestId)))
        {
            return new(QuestState.DoneThisCycle, requirements, null, null, null);
        }

        // 6. Achievement-gated and the client has not loaded achievements yet.
        if (!s.AchievementsLoaded && ctx.IsAchievementGated(q.RowId))
        {
            return new(QuestState.Unknown, requirements, FirstOfKind(requirements, RequirementKind.Achievement), null, null);
        }

        // 7. Requirements on the current job, then on other jobs.
        RequirementResult? firstUnmet = null;
        var onlyJobGates = true;
        foreach (var r in requirements)
        {
            if (r.Met)
            {
                continue;
            }

            firstUnmet ??= r;
            if (r.Req.Kind is not (RequirementKind.ClassJob or RequirementKind.Level))
            {
                onlyJobGates = false;
            }
        }

        if (firstUnmet is null)
        {
            return new(QuestState.Ready, requirements, null, null, null);
        }

        if (onlyJobGates && FindReadyJob(q, s, c, ctx) is { } job)
        {
            return new(QuestState.ReadyOnOtherJob, requirements, null, job, null);
        }

        return new(QuestState.Blocked, requirements, firstUnmet, null, null);
    }

    /// <summary>Highest-level job other than the current one on which every requirement is met; ties go to the lowest job id.</summary>
    private static byte? FindReadyJob(QuestRecord q, CharacterSnapshot s, QuestCatalog c, EvalContext ctx)
    {
        var candidates = CandidateJobs(q, s, ctx)
            .Where(job => job != s.CurrentJob)
            .Distinct()
            .OrderByDescending(job => RequirementEvaluator.LevelOf(s, job))
            .ThenBy(job => job);

        foreach (var job in candidates)
        {
            if (RequirementEvaluator.EvaluateForJob(q, s, c, ctx, job).All(r => r.Met))
            {
                return job;
            }
        }

        return null;
    }

    private static IEnumerable<byte> CandidateJobs(QuestRecord q, CharacterSnapshot s, EvalContext ctx)
    {
        if (q.ClassJobRequired != 0)
        {
            return q.ClassJobRequired <= byte.MaxValue ? [(byte)q.ClassJobRequired] : [];
        }

        if (q.ClassJobCategory != 0 && ctx.ClassJobs is { } lookup)
        {
            return lookup.JobsIn(q.ClassJobCategory);
        }

        return s.JobLevels.Keys;
    }

    private static RequirementResult? FirstOfKind(IReadOnlyList<RequirementResult> results, RequirementKind kind)
    {
        foreach (var r in results)
        {
            if (r.Req.Kind == kind)
            {
                return r;
            }
        }

        return null;
    }

    /// <summary>Default heuristic: the character completed any quest of that festival, so a run of it already happened for them.</summary>
    private static bool DefaultFestivalIsPast(ushort festival, CharacterSnapshot s, QuestCatalog c)
    {
        foreach (var quest in c.All)
        {
            if (quest.Festival == festival && s.IsCompleted(quest.QuestId))
            {
                return true;
            }
        }

        return false;
    }

    private static Func<ushort, bool> MemoizedFestivalIsPast(CharacterSnapshot s, QuestCatalog c, EvalContext ctx)
    {
        if (ctx.FestivalIsPast is { } hook)
        {
            return hook;
        }

        var cache = new Dictionary<ushort, bool>();
        return id =>
        {
            if (!cache.TryGetValue(id, out var past))
            {
                past = DefaultFestivalIsPast(id, s, c);
                cache[id] = past;
            }

            return past;
        };
    }
}
