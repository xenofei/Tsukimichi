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

        return ResolveCore(q, s, c, ctx, id => FestivalIsPast(id, s, c, ctx), PathIndex.For(c).Resolve(s));
    }

    /// <summary>Resolves every quest in the catalog, keyed by row id.</summary>
    public static Dictionary<uint, QuestEvaluation> ResolveAll(QuestCatalog c, CharacterSnapshot s, EvalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(ctx);

        var festivalIsPast = MemoizedFestivalIsPast(s, c, ctx);
        var paths = PathIndex.For(c).Resolve(s);
        var results = new Dictionary<uint, QuestEvaluation>(c.Count);
        foreach (var quest in c.All)
        {
            results[quest.RowId] = ResolveCore(quest, s, c, ctx, festivalIsPast, paths);
        }

        return results;
    }

    /// <summary>
    /// Re-resolves only the rows a change can affect: the changed quests, quests that list them as previous quests or
    /// locks, quests sharing a changed quest's festival, and, when given, quests at changed levels or of changed festivals.
    /// Untouched rows keep their previous <see cref="QuestEvaluation"/> instance. A change to a choice group's anchor
    /// quest or any quest a group tags (<see cref="PathIndex.IsAnchor"/>: a start city's first quest, a "Close to Home",
    /// a set member, a quest on one city's line) can move the character's path choices, so it resolves everything, as
    /// <see cref="ResolveAll"/> does.
    /// </summary>
    /// <param name="previousResults">Result of an earlier <see cref="ResolveAll"/> or this method, keyed by row id.</param>
    /// <param name="changedRowIds">Quest sheet <b>row ids</b> (65536 + n) whose completion changed. Ids the catalog does not know are ignored.
    /// Use <see cref="ResolveDependentsByQuestId"/> when the diff comes from the completion bitmask, which is indexed by quest id.</param>
    /// <param name="changedLevels">Job levels that changed; every quest at exactly those levels is re-resolved.</param>
    /// <param name="changedFestivals">Festivals that started or ended; every quest of those festivals is re-resolved.</param>
    public static Dictionary<uint, QuestEvaluation> ResolveDependents(
        IReadOnlyDictionary<uint, QuestEvaluation> previousResults,
        IEnumerable<uint> changedRowIds,
        ReversePrereqIndex index,
        QuestCatalog c,
        CharacterSnapshot s,
        EvalContext ctx,
        IEnumerable<byte>? changedLevels = null,
        IEnumerable<ushort>? changedFestivals = null)
    {
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(changedRowIds);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(ctx);

        var changedRows = changedRowIds as IReadOnlyCollection<uint> ?? changedRowIds.ToList();
        var paths = PathIndex.For(c);
        foreach (var rowId in changedRows)
        {
            if (paths.IsAnchor(rowId))
            {
                return ResolveAll(c, s, ctx);
            }
        }

        var affected = new HashSet<uint>();
        foreach (var rowId in changedRows)
        {
            if (c.GetByRowId(rowId) is not { } changed)
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
        var choice = paths.Resolve(s);
        foreach (var rowId in affected)
        {
            results[rowId] = ResolveCore(c.ByRowId[rowId], s, c, ctx, festivalIsPast, choice);
        }

        return results;
    }

    /// <summary>
    /// <see cref="ResolveDependents"/> for a diff expressed in runtime <b>quest ids</b> (low 16 bits), as a completion
    /// bitmask compare produces. Each id is mapped through <see cref="QuestCatalog.ByQuestId"/>; unknown ids are ignored.
    /// </summary>
    public static Dictionary<uint, QuestEvaluation> ResolveDependentsByQuestId(
        IReadOnlyDictionary<uint, QuestEvaluation> previousResults,
        IEnumerable<ushort> changedQuestIds,
        ReversePrereqIndex index,
        QuestCatalog c,
        CharacterSnapshot s,
        EvalContext ctx,
        IEnumerable<byte>? changedLevels = null,
        IEnumerable<ushort>? changedFestivals = null)
    {
        ArgumentNullException.ThrowIfNull(changedQuestIds);
        ArgumentNullException.ThrowIfNull(c);

        var rowIds = new List<uint>();
        foreach (var questId in changedQuestIds)
        {
            if (c.TryGetByQuestId(questId, out var quest))
            {
                rowIds.Add(quest.RowId);
            }
        }

        return ResolveDependents(previousResults, rowIds, index, c, s, ctx, changedLevels, changedFestivals);
    }

    private static bool IsAccepted(CharacterSnapshot s, ushort questId)
    {
        foreach (var accepted in s.Accepted)
        {
            if (accepted.QuestId == questId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The rules, then the path tags of a choice not made yet (<see cref="PathChoice"/>): a spare alternative leaves the
    /// totals and an option quest says how many options are open (a completed quest or one in the journal carries
    /// neither), then <see cref="QuestEvaluation.RepeatableDoneBefore"/> on a repeatable the character has completed at
    /// least once (its completion bit stays set after the first time, and the day's turn-in is in the cycle data). Not on
    /// one that leaves the totals (locked out, out of season, a spare alternative): it would count as done while out of
    /// the total.
    /// </summary>
    private static QuestEvaluation ResolveCore(QuestRecord q, CharacterSnapshot s, QuestCatalog c, EvalContext ctx, Func<ushort, bool> festivalIsPast, PathChoice paths)
    {
        var evaluation = ResolveRules(q, s, c, ctx, festivalIsPast, paths);
        if (evaluation.State is not (QuestState.Completed or QuestState.Accepted))
        {
            var spare = paths.IsSpare(q.RowId);
            var of = paths.ChoiceCount(q.RowId);
            if (spare || of > 0)
            {
                evaluation = evaluation with { IsSpareAlternative = spare, ChoiceOf = of };
            }
        }

        if (q.IsRepeatable && evaluation.State != QuestState.Completed && !evaluation.LeavesTotals
            && (s.IsCompleted(q.QuestId) || s.DailyDone.ContainsKey(q.QuestId)))
        {
            evaluation = evaluation with { RepeatableDoneBefore = true };
        }

        return evaluation;
    }

    private static QuestEvaluation ResolveRules(QuestRecord q, CharacterSnapshot s, QuestCatalog c, EvalContext ctx, Func<ushort, bool> festivalIsPast, PathChoice paths)
    {
        var completed = s.IsCompleted(q.QuestId);
        var requirements = RequirementEvaluator.EvaluateForJob(q, s, c, ctx, s.CurrentJob, paths);

        // 1. Completed. A repeatable whose flag never resets (RepeatInterval 0) is final too; the ones that cycle
        //    belong to rule 5.
        if (completed && (!q.IsRepeatable || q.RepeatInterval == 0))
        {
            return new(QuestState.Completed, requirements, null, null, null);
        }

        // 2. Removed from the game: locked out for good. A completed retired quest already read Completed under rule 1,
        //    which is how a character that cleared the old A Realm Reborn story before 5.3 keeps that history.
        if (q.IsRetired)
        {
            return new(QuestState.Foreclosed, requirements, FirstOfKind(requirements, RequirementKind.Retired), null, null);
        }

        // 2. A path the character did not take: another city's start, another starting class, another Grand Company,
        //    another choice of a set (PathIndex). Never a completed quest (rule 1) or one in the journal.
        if (FirstOfKind(requirements, RequirementKind.OtherPath) is { } otherPath)
        {
            return new(QuestState.Foreclosed, requirements, otherPath, null, null);
        }

        // 2. A completed lock forecloses, a Grand Company's own version of a quest included: once one company's is
        //    done, switching companies does not open another's.
        if (q.QuestLocks.Length > 0
            && q.QuestLocks.Any(id => s.IsCompleted(QuestRecord.ToQuestId(id))))
        {
            return new(QuestState.Foreclosed, requirements, FirstOfKind(requirements, RequirementKind.Foreclosure), null, null);
        }

        // 3. Inactive festival: foreclosed if the character already saw a run of it, otherwise blocked as seasonal.
        //    A running event whose reported phase lies outside the quest's window (a chapter not open yet, or over)
        //    is blocked as seasonal too; the requirement carries which. A chapter quest the character already holds
        //    stays In journal (rule 4) while its event runs: the game keeps it there after the chapter moves on.
        if (q.Festival != 0 && FirstOfKind(requirements, RequirementKind.Seasonal) is { Met: false } seasonal
            && !(seasonal.Req is SeasonalRequirement { Active: true } && IsAccepted(s, q.QuestId)))
        {
            var running = seasonal.Req is SeasonalRequirement { Active: true };
            var state = !running && festivalIsPast(q.Festival) ? QuestState.Foreclosed : QuestState.Blocked;
            return new(state, requirements, seasonal, null, null);
        }

        // 4. In the journal.
        foreach (var accepted in s.Accepted)
        {
            if (accepted.QuestId == q.QuestId)
            {
                return new(QuestState.Accepted, requirements, null, null, accepted.Sequence);
            }
        }

        // 5. Repeatable already done this cycle: only the client's cycle data (the allied society daily slots) says so.
        //    The completion bit of a repeatable that resets stays set after the first time, so it means "done before",
        //    not "done today" (ResolveCore records it as RepeatableDoneBefore).
        if (q.IsRepeatable && s.DailyDone.ContainsKey(q.QuestId))
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

        if (onlyJobGates && FindReadyJob(q, s, ctx) is { } job)
        {
            return new(QuestState.ReadyOnOtherJob, requirements, null, job, null);
        }

        return new(QuestState.Blocked, requirements, firstUnmet, null, null);
    }

    /// <summary>
    /// Highest-level job other than the current one that the quest admits at the required level; ties go to the lowest
    /// job id. Only reached when every other requirement is already met on the current job, and those do not depend on
    /// the job, so admission plus level is the whole check.
    /// </summary>
    private static byte? FindReadyJob(QuestRecord q, CharacterSnapshot s, EvalContext ctx)
    {
        var candidates = CandidateJobs(q, s, ctx)
            .Where(job => job != s.CurrentJob)
            .Distinct()
            .OrderByDescending(job => RequirementEvaluator.LevelOf(s, job))
            .ThenBy(job => job);

        foreach (var job in candidates)
        {
            if (RequirementEvaluator.AdmitsJob(q, s, ctx, job) && RequirementEvaluator.LevelOf(s, job) >= q.Level)
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

    /// <summary>
    /// A festival is past when the curated verdict says so, and a curated "not past" (a rerun collaboration, an end
    /// still ahead) is final too. Without a curated verdict it is past when the hook says so or when the character
    /// completed any quest of it (a run of it already happened for them). The hooks are checked first because the
    /// heuristic walks the catalog.
    /// </summary>
    private static bool FestivalIsPast(ushort festival, CharacterSnapshot s, QuestCatalog c, EvalContext ctx)
    {
        if (ctx.CuratedFestivalPast?.Invoke(festival) is { } curated)
        {
            return curated;
        }

        if (ctx.FestivalIsPast is { } hook && hook(festival))
        {
            return true;
        }

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
        var cache = new Dictionary<ushort, bool>();
        return id =>
        {
            if (!cache.TryGetValue(id, out var past))
            {
                past = FestivalIsPast(id, s, c, ctx);
                cache[id] = past;
            }

            return past;
        };
    }
}
