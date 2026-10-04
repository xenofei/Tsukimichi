using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;

namespace Tsukimichi.Core.Jobs;

/// <summary>What the EXP line warns about (feature plan v7, C8).</summary>
public enum ExpWarning : byte
{
    /// <summary>The current job takes the quest and gets as much as any job would (or close to it).</summary>
    None,

    /// <summary>The quest needs a job or job category the current job is not: it is handed in on another job.</summary>
    NeedsJob,

    /// <summary>The current job is at the level cap, so the EXP is lost; another job would get it.</summary>
    Capped,

    /// <summary>Under Quest Sync another job gets more than twice as much.</summary>
    LessThanBest,
}

/// <summary>One job's share: its ClassJob row id, its level and the EXP it would get (0 at the level cap).</summary>
public readonly record struct JobExp(byte Job, int Level, ulong Exp);

/// <summary>
/// Which job gets a quest's EXP and whether that is the right one (feature plan v7, C8).
/// </summary>
/// <param name="Reward">The quest's EXP reward (<see cref="QuestExp.For"/>).</param>
/// <param name="Current">The current job and what it gets if the quest is handed in now.</param>
/// <param name="CurrentTakes">The current job may take and hand in the quest (its class/job rules).</param>
/// <param name="Best">The job that gets the most of the character's jobs that may hand it in; null when none other than the current one can.</param>
/// <param name="Warning">What the line warns about.</param>
public sealed record ExpAdvice(ExpReward Reward, JobExp Current, bool CurrentTakes, JobExp? Best, ExpWarning Warning)
{
    /// <summary>Whether <see cref="Best"/> is another job worth naming.</summary>
    public bool NamesOther => Best is { } best && best.Job != Current.Job && Warning != ExpWarning.None;

    /// <summary>
    /// Another of the character's jobs at the level cap that could hand the quest in and would get nothing (spec-1.19
    /// C8: "your SGE is capped, 0"); null when none is. The game keeps no record of which job was used last, so the
    /// capped job named is the one the character keeps best geared (the highest gearset item level), then the lowest
    /// row id.
    /// </summary>
    public JobExp? Capped { get; init; }
}

/// <summary>
/// The game gives a quest's EXP to the job the character is on when it is handed in (feature plan v7, C8). A class or
/// job quest, a role quest and any quest of a job category is handed in on a job it admits
/// (<see cref="RequirementEvaluator.AdmitsJob"/>); any other quest on any job. A job at the character's level cap gets
/// nothing; under Quest Sync the amount follows the job's level within the quest's range
/// (<see cref="QuestExp.ForLevel"/>). The best job is the one that gets the most, then the current job (no switch),
/// then the highest level. Limited jobs (Blue Mage, Beastmaster) are never suggested. Pure.
/// </summary>
public static class ExpAdvisor
{
    /// <summary>
    /// The advice for <paramref name="quest"/> on <paramref name="snapshot"/>'s character; null when the quest gives no
    /// known amount of EXP (<see cref="ExpReward.HasAmount"/>) or the character has no current job.
    /// </summary>
    /// <param name="isLimited">Whether a ClassJob row is a limited job; null treats none as limited.</param>
    public static ExpAdvice? Advise(QuestRecord quest, CharacterSnapshot snapshot, QuestExpTable table, EvalContext context, Func<byte, bool>? isLimited = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(context);

        var reward = QuestExp.For(quest, table);
        if (!reward.HasAmount || snapshot.CurrentJob == 0)
        {
            return null;
        }

        var currentLevel = RequirementEvaluator.LevelOf(snapshot, snapshot.CurrentJob);
        var current = new JobExp(snapshot.CurrentJob, currentLevel, QuestExp.ForLevel(quest, table, currentLevel, snapshot.LevelCap) ?? 0);
        var currentTakes = RequirementEvaluator.AdmitsJob(quest, snapshot, context, snapshot.CurrentJob);
        var currentLine = Line(context, snapshot.CurrentJob);

        JobExp? best = currentTakes ? current : null;
        foreach (var (job, rawLevel) in snapshot.JobLevels)
        {
            var level = Math.Clamp((int)rawLevel, 0, byte.MaxValue);
            if (job == 0 || level <= 0 || level < quest.Level || (isLimited?.Invoke(job) ?? false)
                || (currentTakes && Line(context, job) == currentLine)
                || !RequirementEvaluator.AdmitsJob(quest, snapshot, context, job)
                || HasJobOver(quest, snapshot, context, job))
            {
                continue;
            }

            var share = new JobExp(job, level, QuestExp.ForLevel(quest, table, level, snapshot.LevelCap) ?? 0);
            if (best is not { } known || Better(share, known, snapshot.CurrentJob))
            {
                best = share;
            }
        }

        var warning = !currentTakes
            ? ExpWarning.NeedsJob
            : best is not { } top || top.Job == current.Job ? ExpWarning.None
            : current.Exp == 0 && top.Exp > 0 ? ExpWarning.Capped
            : top.Exp > 2 * current.Exp ? ExpWarning.LessThanBest
            : ExpWarning.None;
        return new ExpAdvice(reward, current, currentTakes, best, warning) { Capped = CappedOther(quest, snapshot, context, isLimited) };
    }

    /// <summary>
    /// The capped job <see cref="ExpAdvice.Capped"/> names: another job (not the current one's level line) at the
    /// character's level cap that may hand the quest in; the best geared, then the lowest row id. Null without a known
    /// cap.
    /// </summary>
    private static JobExp? CappedOther(QuestRecord quest, CharacterSnapshot snapshot, EvalContext context, Func<byte, bool>? isLimited)
    {
        if (snapshot.LevelCap == 0)
        {
            return null;
        }

        var currentLine = Line(context, snapshot.CurrentJob);
        JobExp? capped = null;
        ushort cappedGear = 0;
        foreach (var (job, rawLevel) in snapshot.JobLevels.OrderBy(static kv => kv.Key))
        {
            if (job == 0 || rawLevel < snapshot.LevelCap || (isLimited?.Invoke(job) ?? false)
                || Line(context, job) == currentLine
                || !RequirementEvaluator.AdmitsJob(quest, snapshot, context, job)
                || HasJobOver(quest, snapshot, context, job))
            {
                continue;
            }

            var gear = snapshot.JobItemLevels.GetValueOrDefault(job);
            if (capped is null || gear > cappedGear)
            {
                capped = new JobExp(job, rawLevel, 0);
                cappedGear = gear;
            }
        }

        return capped;
    }

    /// <summary>Whether <paramref name="a"/> ranks before <paramref name="b"/>: more EXP, then the current job, then the higher level, then the lower row id.</summary>
    private static bool Better(JobExp a, JobExp b, byte currentJob)
    {
        if (a.Exp != b.Exp)
        {
            return a.Exp > b.Exp;
        }

        if ((a.Job == currentJob) != (b.Job == currentJob))
        {
            return a.Job == currentJob;
        }

        return a.Level != b.Level ? a.Level > b.Level : a.Job < b.Job;
    }

    /// <summary>The level line a ClassJob belongs to: its base class (a job and its class share a level), else itself.</summary>
    private static byte Line(EvalContext context, byte job) => context.ParentJob is { } parentOf ? parentOf(job) : job;

    /// <summary>
    /// A base class whose job the character also has and the quest also admits: the job is the one to name (Paladin,
    /// not Gladiator). False without a parent lookup.
    /// </summary>
    private static bool HasJobOver(QuestRecord quest, CharacterSnapshot snapshot, EvalContext context, byte job)
    {
        if (context.ParentJob is not { } parentOf || parentOf(job) != job)
        {
            return false;
        }

        foreach (var (other, level) in snapshot.JobLevels)
        {
            if (other != job && level > 0 && parentOf(other) == job && RequirementEvaluator.AdmitsJob(quest, snapshot, context, other))
            {
                return true;
            }
        }

        return false;
    }
}
