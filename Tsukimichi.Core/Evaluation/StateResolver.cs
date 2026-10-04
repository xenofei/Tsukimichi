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

        s = AsOfCycle(s, c, ctx);
        return ResolveCore(q, s, c, ctx, id => FestivalIsPast(id, s, c, ctx), PathIndex.For(c).Resolve(s));
    }

    /// <summary>Resolves every quest in the catalog, keyed by row id.</summary>
    public static Dictionary<uint, QuestEvaluation> ResolveAll(QuestCatalog c, CharacterSnapshot s, EvalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(ctx);

        s = AsOfCycle(s, c, ctx);
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
        s = AsOfCycle(s, c, ctx);
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
            && (s.IsCompleted(q.QuestId) || s.IsDoneThisCycle(q)))
        {
            evaluation = evaluation with { RepeatableDoneBefore = true };
        }

        return WithGameAnswer(q, s, ctx, evaluation);
    }

    /// <summary>
    /// The game's answer over Tsukimichi's (feature plan v7 C1): the player's "Go with the game" turns a quest Tsukimichi
    /// reads Blocked, Locked out or Not checked Ready; the game's own offer turns a Not checked one Ready. Never a
    /// completed quest, one in the journal or a repeatable one (whose offer moves with the cycle). Tsukimichi's own
    /// answer is kept in <see cref="QuestEvaluation.Own"/>, so the disagreement can still be told.
    /// </summary>
    private static QuestEvaluation WithGameAnswer(QuestRecord q, CharacterSnapshot s, EvalContext ctx, QuestEvaluation evaluation)
    {
        if (q.IsRepeatable || evaluation.State is not (QuestState.Blocked or QuestState.Foreclosed or QuestState.Unknown))
        {
            return evaluation;
        }

        var answer = ctx.GoWithGame(s.ContentId, q.RowId) ? GameAnswer.Override
            : evaluation.State == QuestState.Unknown && ctx.GameOffered(s.ContentId, q.RowId) ? GameAnswer.Offered
            : GameAnswer.None;
        return answer == GameAnswer.None
            ? evaluation
            : new QuestEvaluation(QuestState.Ready, evaluation.Requirements, null, null, null)
            {
                IsSpareAlternative = evaluation.IsSpareAlternative,
                ChoiceOf = evaluation.ChoiceOf,
                ByGame = answer,
                Own = evaluation,
            };
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

        // 5. Repeatable already done this cycle: only the client's cycle data says so, the allied society daily slots
        //    or the repeat flag the quest carries (QuestRecord.RepeatFlag: Gift of Joy, One Man's Relic, the Komra
        //    weeklies, which share one flag and so read done together). The completion bit of a repeatable that
        //    resets stays set after the first time, so it means "done before", not "done today" (ResolveCore records
        //    it as RepeatableDoneBefore).
        if (q.IsRepeatable && s.IsDoneThisCycle(q))
        {
            return new(QuestState.DoneThisCycle, requirements, null, null, null);
        }

        // 6. Achievement-gated and the client has not loaded achievements yet.
        if (!s.AchievementsLoaded && ctx.IsAchievementGated(q.RowId))
        {
            return new(QuestState.Unknown, requirements, FirstOfKind(requirements, RequirementKind.Achievement), null, null);
        }

        // 7. Requirements on the current job, then on other jobs. A game gate Tsukimichi cannot read (game_gates.json),
        //    and mounts or a house nobody read (IsNotChecked), are never judged: they turn what would read Ready (on
        //    this job or another) into Not checked, and leave a
        //    quest Blocked by something else Blocked by that. A gear gate judged from the capture counts like any other,
        //    except one whose weapon the character carries but has not equipped, which goes with a level or job gate:
        //    equipping that weapon is switching to a job that wears it, so the quest is Ready on such a job when one
        //    meets the level, and Blocked by the weapon to equip when none does.
        RequirementResult? firstUnmet = null;
        RequirementResult? gameGate = null;
        RequirementResult? carried = null;
        var onlyJobGates = true;
        var jobGateUnmet = false;
        foreach (var r in requirements)
        {
            if (r.Met)
            {
                continue;
            }

            if (IsNotChecked(r.Req))
            {
                gameGate ??= r;
                continue;
            }

            firstUnmet ??= r;
            if (IsCarriedGate(r))
            {
                carried ??= r;
            }
            else if (r.Req.Kind is RequirementKind.ClassJob or RequirementKind.Level)
            {
                jobGateUnmet = true;
            }
            else
            {
                onlyJobGates = false;
            }
        }

        if (firstUnmet is null)
        {
            return gameGate is null
                ? new(QuestState.Ready, requirements, null, null, null)
                : new(QuestState.Unknown, requirements, gameGate, null, null);
        }

        if (onlyJobGates && jobGateUnmet)
        {
            if (FindReadyJob(q, s, ctx, carried is null ? null : WearsCarried(q, s, c, ctx)) is { } job)
            {
                return gameGate is null
                    ? new(QuestState.ReadyOnOtherJob, requirements, null, job, null)
                    : new(QuestState.Unknown, requirements, gameGate, null, null);
            }

            if (carried is not null)
            {
                return new(QuestState.Blocked, requirements, carried, null, null);
            }
        }

        return new(QuestState.Blocked, requirements, firstUnmet, null, null);
    }

    /// <summary>
    /// A requirement nobody could judge, which turns Ready into Not checked and never blocks on its own: a game gate
    /// Tsukimichi cannot read, mounts the capture did not read, a house (1.11.0, C2: these read a silent Ready before).
    /// </summary>
    internal static bool IsNotChecked(Requirement requirement) =>
        requirement is GameGateRequirement { IsNotChecked: true } or MountRequirement { HasMount: null } or HouseRequirement { HasHouse: null };

    /// <summary>
    /// An unmet gear gate whose weapons must be equipped and that the character carries (in the Armoury Chest or the
    /// inventory): <see cref="GameGateRequirement.Matching"/> names them.
    /// </summary>
    internal static bool IsCarriedGate(RequirementResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return !r.Met && r.Req is GameGateRequirement { Checked: GateHold.Equipped, Matching.Length: > 0 };
    }

    /// <summary>
    /// Whether a job can wear one of the weapon groups of <paramref name="q"/>'s gear gate that the character carries:
    /// every item of the group equippable by the job (<see cref="EvalContext.ItemJobCategory"/> through
    /// <see cref="EvalContext.ClassJobs"/>). A weapon of unknown category, or no category lookup, admits no job.
    /// </summary>
    private static Func<byte, bool> WearsCarried(QuestRecord q, CharacterSnapshot s, QuestCatalog c, EvalContext ctx)
    {
        var carried = new List<uint[]>();
        if (c.GameGateOf(q.RowId)?.Items is { } items && s.GateItems is { } capture)
        {
            foreach (var group in items.Groups)
            {
                if (group.Length > 0 && Array.TrueForAll(group, capture.Held.Contains))
                {
                    carried.Add(group);
                }
            }
        }

        return job => ctx.ClassJobs is { } lookup
            && carried.Exists(group => Array.TrueForAll(group, id => ctx.ItemJobCategory(id) is var category and not 0 && lookup.Admits(category, job)));
    }

    /// <summary>
    /// The job other than the current one that the quest admits at the required level, and the one the pane says it is
    /// ready on: of those, one of the current job's class line (Lancer and Dragoon), then of its role, then the
    /// highest level, then a job over its own base class (Paladin before Gladiator, which share a level), then the
    /// lowest job id. Only reached when every other requirement is already met on the current job, and those do not
    /// depend on the job, so admission plus level is the whole check; with <paramref name="wears"/> (a gear gate whose
    /// weapon is carried, not equipped) only a job that can wear that weapon counts.
    /// </summary>
    private static byte? FindReadyJob(QuestRecord q, CharacterSnapshot s, EvalContext ctx, Func<byte, bool>? wears = null)
    {
        byte? best = null;
        foreach (var job in CandidateJobs(q, s, ctx))
        {
            if (job != s.CurrentJob
                && RequirementEvaluator.AdmitsJob(q, s, ctx, job)
                && RequirementEvaluator.LevelOf(s, job) >= q.Level
                && (wears is null || wears(job))
                && (best is not { } current || CompareJobs(job, current, s, ctx, levelFirst: false) < 0))
            {
                best = job;
            }
        }

        return best;
    }

    /// <summary>
    /// The job the quest's level is best measured on when the current job cannot take it: of the jobs the quest admits
    /// that the character has levelled, the highest level, then the current job's class line and role, then a job over
    /// its base class, then the lowest id. Null when the character has no such job.
    /// </summary>
    internal static byte? BestAdmittedJob(QuestRecord q, CharacterSnapshot s, EvalContext ctx)
    {
        byte? best = null;
        foreach (var job in CandidateJobs(q, s, ctx))
        {
            if (job != s.CurrentJob
                && RequirementEvaluator.LevelOf(s, job) > 0
                && RequirementEvaluator.AdmitsJob(q, s, ctx, job)
                && (best is not { } current || CompareJobs(job, current, s, ctx, levelFirst: true) < 0))
            {
                best = job;
            }
        }

        return best;
    }

    /// <summary>Negative when <paramref name="a"/> is the job to name before <paramref name="b"/> (<see cref="FindReadyJob"/>'s order).</summary>
    private static int CompareJobs(byte a, byte b, CharacterSnapshot s, EvalContext ctx, bool levelFirst)
    {
        if (a == b)
        {
            return 0;
        }

        var byLevel = RequirementEvaluator.LevelOf(s, b).CompareTo(RequirementEvaluator.LevelOf(s, a));
        if (levelFirst && byLevel != 0)
        {
            return byLevel;
        }

        if (ctx.ParentJob is { } parentOf)
        {
            var line = parentOf(s.CurrentJob);
            var byLine = (parentOf(b) == line).CompareTo(parentOf(a) == line);
            if (byLine != 0)
            {
                return byLine;
            }
        }

        if (ctx.JobRole is { } roleOf && roleOf(s.CurrentJob) is var role and not 0)
        {
            var byRole = (roleOf(b) == role).CompareTo(roleOf(a) == role);
            if (byRole != 0)
            {
                return byRole;
            }
        }

        if (byLevel != 0)
        {
            return byLevel;
        }

        // A job over the base class it grew out of: Paladin and Gladiator share a level, and Paladin is what is played.
        if (ctx.ParentJob is { } parent)
        {
            if (parent(a) == b)
            {
                return -1;
            }

            if (parent(b) == a)
            {
                return 1;
            }

            var byJob = (parent(b) != b).CompareTo(parent(a) != a);
            if (byJob != 0)
            {
                return byJob;
            }
        }

        return a.CompareTo(b);
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

    /// <summary>
    /// The snapshot as it stands now when the context says it is a stored one (<see cref="EvalContext.CycleClock"/>):
    /// cycle data from before the last reset reads as cleared (<see cref="Runtime.GameResets.AsOf"/>). The same
    /// instance otherwise, and whenever nothing predates a reset.
    /// </summary>
    internal static CharacterSnapshot AsOfCycle(CharacterSnapshot s, QuestCatalog c, EvalContext ctx) =>
        ctx.CycleClock is { } clock ? Runtime.GameResets.AsOf(s, c, clock()) : s;

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
