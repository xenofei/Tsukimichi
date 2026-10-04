using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Jobs;

/// <summary>One of the character's gearsets as the game lists it: its 0-based id, its ClassJob, its item level and name.</summary>
public readonly record struct GearsetInfo(int Id, byte Job, short ItemLevel, string Name);

/// <summary>
/// "Switch gearset" (feature plan v7, C8): offered only for a quest that needs a specific job or job category the
/// current job is not (a class or job quest, a role quest, a crafter's or gatherer's quest), never to chase EXP on a
/// quest any job can do. Picks the gearset to switch to. Pure.
/// </summary>
public static class GearsetChoice
{
    /// <summary>
    /// Whether the quest pins a job or a job category and the current job cannot take it, so switching is the way to
    /// do it. False for a quest any job can take, and for one already done.
    /// </summary>
    public static bool Needed(QuestRecord quest, CharacterSnapshot snapshot, EvalContext context, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (state is QuestState.Completed or QuestState.Foreclosed or QuestState.DoneThisCycle
            || (quest.ClassJobRequired == 0 && quest.ClassJobCategory == 0))
        {
            return false;
        }

        return !RequirementEvaluator.AdmitsJob(quest, snapshot, context, snapshot.CurrentJob);
    }

    /// <summary>
    /// The gearset to switch to for <paramref name="quest"/>: of the gearsets whose job takes it, those of
    /// <paramref name="preferredJob"/> first (the job the quest reads Ready on), then the highest item level, then the
    /// lowest id. Null when no gearset's job takes the quest.
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
            if (gearset.Job == 0 || !RequirementEvaluator.AdmitsJob(quest, snapshot, context, gearset.Job))
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

    private static bool Better(GearsetInfo a, GearsetInfo b, byte? preferredJob)
    {
        if (preferredJob is { } job && (a.Job == job) != (b.Job == job))
        {
            return a.Job == job;
        }

        return a.ItemLevel != b.ItemLevel ? a.ItemLevel > b.ItemLevel : a.Id < b.Id;
    }
}
