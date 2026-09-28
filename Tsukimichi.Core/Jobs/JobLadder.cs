using System.Collections.Frozen;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Jobs;

/// <summary>
/// A class or job as the ladder needs it; the plugin maps the ClassJob sheet row (<c>ClassJobInfo</c>) to this.
/// <see cref="ParentRowId"/> equals <see cref="RowId"/> for classes and for jobs without a class.
/// <see cref="Role"/> is the sheet's role byte (1 tank, 2 melee, 3 ranged or caster, 4 healer, 0 none).
/// </summary>
public sealed record LadderJob(
    uint RowId,
    string Name,
    uint ParentRowId,
    uint UnlockQuestRowId,
    byte Role,
    bool IsCrafter,
    bool IsGatherer);

/// <summary>Role quest lines, in display order. The sheet's role 3 is split by Disciples of Magic membership.</summary>
public enum JobRole : byte
{
    Tank = 1,
    Healer = 2,
    Melee = 3,
    PhysicalRanged = 4,
    MagicalRanged = 5,
}

/// <summary>One job's quest line: its own quests, preceded by its base class's when it grew out of one.</summary>
public sealed record JobLadderEntry(LadderJob Job, IReadOnlyList<uint> QuestRowIds);

/// <summary>Where a character stands on a ladder.</summary>
/// <param name="Done">Completed quests.</param>
/// <param name="Total">Quests that count: foreclosed ones (the other start of a class) are left out.</param>
/// <param name="NextRowId">First quest in ladder order that is neither completed nor foreclosed; null once finished.</param>
/// <param name="NextLevel">The next quest's level; 0 when finished.</param>
/// <param name="IsReadyNow">The next quest is Ready, Ready on another job or already accepted.</param>
/// <param name="LevelReached">The job's level is at least <see cref="NextLevel"/> (true when finished).</param>
public readonly record struct LadderProgress(int Done, int Total, uint? NextRowId, byte NextLevel, bool IsReadyNow, bool LevelReached)
{
    public bool IsComplete => NextRowId is null;

    public float Fraction => Total == 0 ? 0f : (float)Done / Total;
}

/// <summary>A quest that became available because <paramref name="Job"/> reached <paramref name="Level"/>.</summary>
public readonly record struct JobNudge(LadderJob Job, short Level, uint RowId);

/// <summary>
/// Per-job quest ladders (V2-11), built once per catalog. A job's ladder is every listed quest of the "Class &amp; Job
/// Quests" journal section that names the job in <see cref="QuestRecord.ClassJobRequired"/> or whose
/// <see cref="QuestRecord.ClassJobCategory"/> admits that job alone, preceded by the same for its base class and by
/// the job's unlock quest (which the sheet opens to every combat job, e.g. Dark Knight's "Our End"); each run in
/// level, then journal order. Role quests (genres or categories named "Role Quests") form one ladder per <see cref="JobRole"/>,
/// each quest under every role its category admits, so a Shadowbringers physical DPS quest sits on both the melee
/// and the physical ranged ladder and the all-role capstones on all five.
/// </summary>
public sealed class JobLadder
{
    /// <summary>JournalSection id of "Class &amp; Job Quests".</summary>
    public const uint ClassJobSectionId = 6;

    /// <summary>ClassJobCategory 31 is "Disciples of Magic": tells casters from physical ranged jobs, which share role 3.</summary>
    public const uint DisciplesOfMagicCategory = 31;

    /// <summary>Role quest genres and their category carry this in their name.</summary>
    public const string RoleQuestsMarker = "Role Quests";

    private static readonly IReadOnlyList<uint> NoQuests = [];

    public static readonly JobLadder Empty = new(QuestCatalog.Empty, [], [], FrozenDictionary<uint, JobLadderEntry>.Empty, FrozenDictionary<JobRole, IReadOnlyList<uint>>.Empty, FrozenDictionary<uint, JobRole>.Empty);

    private readonly QuestCatalog catalog;
    private readonly IReadOnlyList<LadderJob> allJobs;
    private readonly FrozenDictionary<uint, JobLadderEntry> byJob;
    private readonly FrozenDictionary<JobRole, IReadOnlyList<uint>> roleLadders;
    private readonly FrozenDictionary<uint, JobRole> roleOf;

    private JobLadder(
        QuestCatalog catalog,
        IReadOnlyList<LadderJob> allJobs,
        IReadOnlyList<JobLadderEntry> jobs,
        FrozenDictionary<uint, JobLadderEntry> byJob,
        FrozenDictionary<JobRole, IReadOnlyList<uint>> roleLadders,
        FrozenDictionary<uint, JobRole> roleOf)
    {
        this.catalog = catalog;
        this.allJobs = allJobs;
        Jobs = jobs;
        this.byJob = byJob;
        this.roleLadders = roleLadders;
        this.roleOf = roleOf;
    }

    /// <summary>Every job with at least one quest, in the order the jobs were given (sheet row order).</summary>
    public IReadOnlyList<JobLadderEntry> Jobs { get; }

    /// <summary>The ladder of a class or job by ClassJob row id; null when it has no quests.</summary>
    public JobLadderEntry? ForJob(uint jobId) => byJob.GetValueOrDefault(jobId);

    /// <summary>Role quests a job of <paramref name="role"/> can take, in level then journal order; empty for a role without any.</summary>
    public IReadOnlyList<uint> RoleLadder(JobRole role) => roleLadders.GetValueOrDefault(role, NoQuests);

    /// <summary>The role of a combat class or job; null for crafters, gatherers and unknown ids.</summary>
    public JobRole? RoleOf(uint jobId) => roleOf.TryGetValue(jobId, out var role) ? role : null;

    public static JobLadder Build(QuestCatalog catalog, IReadOnlyList<LadderJob> jobs, IClassJobCategoryLookup categories)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(categories);

        var roles = new Dictionary<uint, JobRole>();
        foreach (var job in jobs)
        {
            if (RoleOf(job, categories) is { } role)
            {
                roles[job.RowId] = role;
            }
        }

        var own = new Dictionary<uint, List<QuestRecord>>();
        var byRole = new Dictionary<JobRole, List<QuestRecord>>();
        if (catalog.BySection.TryGetValue(ClassJobSectionId, out var section))
        {
            foreach (var quest in section)
            {
                if (quest.IsUnlisted)
                {
                    continue;
                }

                if (IsRoleQuest(quest))
                {
                    AddToRoles(byRole, roles, quest, categories);
                    continue;
                }

                if (quest.ClassJobRequired != 0)
                {
                    Add(own, quest.ClassJobRequired, quest);
                }

                if (SingleJob(categories, quest.ClassJobCategory) is { } single && single != quest.ClassJobRequired)
                {
                    Add(own, single, quest);
                }
            }
        }

        var entries = new List<JobLadderEntry>();
        var byJob = new Dictionary<uint, JobLadderEntry>();
        foreach (var job in jobs)
        {
            if (job.RowId == 0 || job.Name.Length == 0)
            {
                continue;
            }

            // Three runs, each in level then journal order: the class's quests, the unlock quest (its sheet level is
            // the character level it asks for, e.g. 50 for "Our End", so it cannot sort by level among the job's
            // own quests, all of which follow it), then the job's own quests.
            var quests = new List<(int Run, QuestRecord Quest)>();
            if (job.ParentRowId != 0 && job.ParentRowId != job.RowId && own.TryGetValue(job.ParentRowId, out var inherited))
            {
                foreach (var quest in inherited)
                {
                    quests.Add((0, quest));
                }
            }

            if (job.UnlockQuestRowId != 0 && catalog.TryGetByRowId(job.UnlockQuestRowId, out var unlock))
            {
                quests.Add((1, unlock));
            }

            if (own.TryGetValue(job.RowId, out var mine))
            {
                foreach (var quest in mine)
                {
                    quests.Add((2, quest));
                }
            }

            if (quests.Count == 0)
            {
                continue;
            }

            var entry = new JobLadderEntry(job, Order(quests));
            entries.Add(entry);
            byJob[job.RowId] = entry;
        }

        var roleLadders = new Dictionary<JobRole, IReadOnlyList<uint>>();
        foreach (var (role, quests) in byRole)
        {
            var run = new List<(int Run, QuestRecord Quest)>(quests.Count);
            foreach (var quest in quests)
            {
                run.Add((0, quest));
            }

            roleLadders[role] = Order(run);
        }

        return new JobLadder(catalog, jobs, entries, byJob.ToFrozenDictionary(), roleLadders.ToFrozenDictionary(), roles.ToFrozenDictionary());
    }

    /// <summary>
    /// The role of a class or job from its sheet role byte; casters are told from physical ranged jobs by Disciples
    /// of Magic membership. Null for crafters, gatherers and roleless rows.
    /// </summary>
    public static JobRole? RoleOf(LadderJob job, IClassJobCategoryLookup categories)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(categories);
        if (job.IsCrafter || job.IsGatherer)
        {
            return null;
        }

        return job.Role switch
        {
            1 => JobRole.Tank,
            4 => JobRole.Healer,
            2 => JobRole.Melee,
            3 => job.RowId <= byte.MaxValue && categories.Admits(DisciplesOfMagicCategory, (byte)job.RowId) ? JobRole.MagicalRanged : JobRole.PhysicalRanged,
            _ => null,
        };
    }

    /// <summary>A role quest by its journal genre or category name (English sheets; other languages get no role ladders).</summary>
    public static bool IsRoleQuest(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.Journal.GenreName.Contains(RoleQuestsMarker, StringComparison.OrdinalIgnoreCase)
               || quest.Journal.CategoryName.Contains(RoleQuestsMarker, StringComparison.OrdinalIgnoreCase);
    }

    public LadderProgress Progress(JobLadderEntry entry, IReadOnlyDictionary<uint, QuestEvaluation> states, short jobLevel)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Progress(entry.QuestRowIds, states, jobLevel);
    }

    /// <summary>
    /// Counts completed quests and finds the first still to do. A quest without an evaluation is not done; a foreclosed
    /// one (the other start of a class, locked once its twin is taken) is skipped and not counted.
    /// </summary>
    public LadderProgress Progress(IReadOnlyList<uint> rowIds, IReadOnlyDictionary<uint, QuestEvaluation> states, short jobLevel)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        ArgumentNullException.ThrowIfNull(states);

        var done = 0;
        var total = 0;
        uint? next = null;
        byte nextLevel = 0;
        var ready = false;
        foreach (var rowId in rowIds)
        {
            var state = states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            if (state == QuestState.Foreclosed)
            {
                continue;
            }

            total++;
            if (state == QuestState.Completed)
            {
                done++;
            }
            else if (next is null)
            {
                next = rowId;
                nextLevel = catalog.GetByRowId(rowId)?.Level ?? 0;
                ready = state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted;
            }
        }

        return new LadderProgress(done, total, next, nextLevel, ready, next is null || jobLevel >= nextLevel);
    }

    /// <summary>
    /// The quests to announce after a level-up: for every job whose level in <paramref name="after"/> is above the one in
    /// <paramref name="before"/> (jobs absent from <paramref name="before"/> are a first capture, not a level-up), the
    /// next quest of its ladder and of its role's ladder when that quest is open now (Ready or Ready on another job;
    /// an accepted quest is already in the journal) and it is this level-up that opened it: its level is above the
    /// previous level and at most the new one. A quest that was already available before the level-up, or that needs
    /// a level still to come, is not announced. A class whose job is unlocked is skipped so the job's name is the one
    /// printed; each quest is listed once.
    /// </summary>
    public IReadOnlyList<JobNudge> LevelUpNudges(
        IReadOnlyDictionary<byte, short> before,
        IReadOnlyDictionary<byte, short> after,
        IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(states);

        var result = new List<JobNudge>();
        var seen = new HashSet<uint>();
        foreach (var (jobId, level) in after.OrderBy(kv => kv.Key))
        {
            if (!before.TryGetValue(jobId, out var previous) || level <= previous || ForJob(jobId) is not { } entry || HasUnlockedJob(entry.Job, states))
            {
                continue;
            }

            Consider(entry.QuestRowIds);
            if (RoleOf(jobId) is { } role)
            {
                Consider(RoleLadder(role));
            }

            void Consider(IReadOnlyList<uint> rowIds)
            {
                var progress = Progress(rowIds, states, level);
                if (progress.NextRowId is not { } next
                    || progress.NextLevel <= previous
                    || progress.NextLevel > level
                    || !states.TryGetValue(next, out var evaluation)
                    || evaluation.State is not (QuestState.Ready or QuestState.ReadyOnOtherJob))
                {
                    return;
                }

                if (seen.Add(next))
                {
                    result.Add(new JobNudge(entry.Job, level, next));
                }
            }
        }

        return result;
    }

    /// <summary>True for a class one of whose jobs has its unlock quest completed.</summary>
    private bool HasUnlockedJob(LadderJob job, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        foreach (var other in allJobs)
        {
            if (other.RowId != job.RowId
                && other.ParentRowId == job.RowId
                && other.UnlockQuestRowId != 0
                && states.TryGetValue(other.UnlockQuestRowId, out var unlock)
                && unlock.State == QuestState.Completed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The one job a category admits, or null when it admits none or several.</summary>
    private static uint? SingleJob(IClassJobCategoryLookup categories, uint categoryId)
    {
        if (categoryId == 0)
        {
            return null;
        }

        uint? single = null;
        foreach (var job in categories.JobsIn(categoryId))
        {
            if (single is not null)
            {
                return null;
            }

            single = job;
        }

        return single;
    }

    private static void AddToRoles(Dictionary<JobRole, List<QuestRecord>> byRole, Dictionary<uint, JobRole> roles, QuestRecord quest, IClassJobCategoryLookup categories)
    {
        if (quest.ClassJobCategory == 0)
        {
            return;
        }

        var added = new HashSet<JobRole>();
        foreach (var job in categories.JobsIn(quest.ClassJobCategory))
        {
            if (roles.TryGetValue(job, out var role) && added.Add(role))
            {
                Add(byRole, role, quest);
            }
        }
    }

    private static void Add<TKey>(Dictionary<TKey, List<QuestRecord>> map, TKey key, QuestRecord quest)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(quest);
    }

    /// <summary>Distinct row ids (first run wins) by run, then level, then journal order, then row id.</summary>
    private static uint[] Order(List<(int Run, QuestRecord Quest)> quests)
    {
        var distinct = new Dictionary<uint, (int Run, QuestRecord Quest)>(quests.Count);
        foreach (var entry in quests)
        {
            distinct.TryAdd(entry.Quest.RowId, entry);
        }

        var ordered = new List<(int Run, QuestRecord Quest)>(distinct.Values);
        ordered.Sort(static (a, b) =>
        {
            var byRun = a.Run.CompareTo(b.Run);
            if (byRun != 0)
            {
                return byRun;
            }

            var byLevel = a.Quest.Level.CompareTo(b.Quest.Level);
            if (byLevel != 0)
            {
                return byLevel;
            }

            var byJournal = a.Quest.Journal.SortKey.CompareTo(b.Quest.Journal.SortKey);
            return byJournal != 0 ? byJournal : a.Quest.RowId.CompareTo(b.Quest.RowId);
        });

        var result = new uint[ordered.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = ordered[i].Quest.RowId;
        }

        return result;
    }
}
