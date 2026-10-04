using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Jobs;

/// <summary>One of the character's gearsets as the game lists it: its 0-based id, its ClassJob, its item level and name.</summary>
public readonly record struct GearsetInfo(int Id, byte Job, short ItemLevel, string Name);

/// <summary>
/// "Switch gearset" (feature plan v7, C8; spec-1.19 C8 and decision 6): offered only for a quest that requires one
/// specific class or job (a class or job quest, a crafter's or gatherer's quest) the current job is not, never to chase
/// EXP on a quest any job can do, nor for a role quest several jobs take. Picks the gearset to switch to. Pure.
/// </summary>
public static class GearsetChoice
{
    /// <summary>
    /// The one class or job the quest requires: its <see cref="QuestRecord.ClassJobRequired"/>, else the only job of its
    /// <see cref="QuestRecord.ClassJobCategory"/>; null for a quest any job takes or one a category of several jobs
    /// takes (a role quest).
    /// </summary>
    public static byte? PinnedJob(QuestRecord quest, EvalContext context)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(context);
        if (quest.ClassJobRequired != 0)
        {
            return quest.ClassJobRequired <= byte.MaxValue ? (byte)quest.ClassJobRequired : null;
        }

        if (quest.ClassJobCategory == 0 || context.ClassJobs is not { } categories)
        {
            return null;
        }

        uint? single = null;
        foreach (var job in categories.JobsIn(quest.ClassJobCategory))
        {
            if (single is not null)
            {
                return null;
            }

            single = job;
        }

        return single is { } only and > 0 and <= byte.MaxValue ? (byte)only : null;
    }

    /// <summary>
    /// Whether the quest requires one specific class or job (<see cref="PinnedJob"/>) and the current job cannot take
    /// it, so switching is the way to do it. False for a quest any job can take, a role quest, and one already done.
    /// </summary>
    public static bool Needed(QuestRecord quest, CharacterSnapshot snapshot, EvalContext context, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (state is QuestState.Completed or QuestState.Foreclosed or QuestState.DoneThisCycle || PinnedJob(quest, context) is null)
        {
            return false;
        }

        return !RequirementEvaluator.AdmitsJob(quest, snapshot, context, snapshot.CurrentJob);
    }

    /// <summary>
    /// The gearset Switch gearset equips for a job-locked quest (spec-1.19 C8: "the first gearset of that job"): the
    /// first (lowest id) gearset of <paramref name="job"/>, whatever its level (the Level requirement says what is
    /// missing), else the first of another job that takes the quest at its level (a class quest accepted on its job).
    /// Null when no saved gearset takes it ("No Culinarian gearset saved").
    /// </summary>
    public static GearsetInfo? First(IReadOnlyList<GearsetInfo> gearsets, byte job, QuestRecord quest, CharacterSnapshot snapshot, EvalContext context)
    {
        ArgumentNullException.ThrowIfNull(gearsets);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        GearsetInfo? other = null;
        foreach (var gearset in gearsets.OrderBy(static g => g.Id))
        {
            if (gearset.Job == job)
            {
                return gearset;
            }

            if (other is null && Takes(gearset, quest, snapshot, context))
            {
                other = gearset;
            }
        }

        return other;
    }

    /// <summary>
    /// The gearset to switch to for <paramref name="quest"/>: of the gearsets whose job takes it, those of
    /// <paramref name="preferredJob"/> first (the job the quest reads Ready on), then the highest item level, then the
    /// lowest id. A gearset whose job is known to be under the quest's level is left out. Null when no gearset's job
    /// takes the quest.
    /// </summary>
    public static GearsetInfo? Pick(IReadOnlyList<GearsetInfo> gearsets, QuestRecord quest, CharacterSnapshot snapshot, EvalContext context, byte? preferredJob = null)
    {
        ArgumentNullException.ThrowIfNull(gearsets);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        GearsetInfo? best = null;
        foreach (var gearset in gearsets)
        {
            if (!Takes(gearset, quest, snapshot, context))
            {
                continue;
            }

            if (best is not { } known || Better(gearset, known, preferredJob))
            {
                best = gearset;
            }
        }

        return best;
    }

    /// <summary>
    /// Whether the gearset's job may take the quest: its class or job rules admit it, and its level, when known, is the
    /// quest's level or more (a set too low to take it is never the one to switch to).
    /// </summary>
    private static bool Takes(GearsetInfo gearset, QuestRecord quest, CharacterSnapshot snapshot, EvalContext context)
    {
        if (gearset.Job == 0 || !RequirementEvaluator.AdmitsJob(quest, snapshot, context, gearset.Job))
        {
            return false;
        }

        var level = RequirementEvaluator.LevelOf(snapshot, gearset.Job);
        return level == 0 || level >= quest.Level;
    }

    private static bool Better(GearsetInfo a, GearsetInfo b, byte? preferredJob)
    {
        if (preferredJob is { } job && (a.Job == job) != (b.Job == job))
        {
            return a.Job == job;
        }

        return a.ItemLevel != b.ItemLevel ? a.ItemLevel > b.ItemLevel : a.Id < b.Id;
    }
}
