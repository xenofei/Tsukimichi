using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Plan;

/// <summary>
/// How soon an unlock quest is worth doing (feature plan v7 P4, spec-1.21 "Tiers"): My blues' Do first sort shows one
/// card per tier, in this order. Every plan quest gets exactly one (<see cref="UnlockTiers.Classify"/>).
/// </summary>
public enum UnlockTier : byte
{
    /// <summary>The main scenario asks for it later: <c>story_required.json</c>, and the story's duty and previous-quest gates.</summary>
    StoryNeedsIt,

    /// <summary>Opens a dungeon, trial, raid, field operation or flying, or is a quest of the job the character plays.</summary>
    OpensContent,

    /// <summary>Opens a game feature (glamour, retainers, the Gold Saucer), or nothing more specific is known.</summary>
    Systems,

    /// <summary>Opens only Extreme, Savage, Unreal, Ultimate or Chaotic content.</summary>
    HighEnd,

    /// <summary>A job, class or allied society the character does not play now.</summary>
    AnotherJob,
}

/// <summary>What <see cref="UnlockTiers.Classify"/> knows about the character beyond the quest's own data.</summary>
public sealed record UnlockTierContext
{
    public static readonly UnlockTierContext Empty = new();

    /// <summary>The side quests the main scenario needs (<see cref="Query.StoryRequirements.SideQuests"/>).</summary>
    public IReadOnlySet<uint> StoryRequired { get; init; } = new HashSet<uint>();

    /// <summary>
    /// Whether the character can take the quest on the job it plays now (its class or job category admits it); null
    /// knows no character (browse mode), so every job quest reads as another job's.
    /// </summary>
    public Func<QuestRecord, bool>? AdmitsCurrentJob { get; init; }

    /// <summary>Whether the character plays the ClassJob now: its current job or that job's base class. Null plays none.</summary>
    public Func<uint, bool>? PlaysJob { get; init; }

    /// <summary>Whether the character has standing with the allied society (BeastTribe id). Null knows none.</summary>
    public Func<byte, bool>? KnowsSociety { get; init; }

    /// <summary>
    /// The context for a character playing <paramref name="currentJob"/>: it admits the quests of that job and of its
    /// base class; it plays that job, its base class and the job its class becomes (a Gladiator's Paladin); it knows a
    /// society it has a rank with.
    /// </summary>
    /// <param name="parents">ClassJob row id to its base class (a class maps to itself), as the sheet has it.</param>
    /// <param name="jobs">ClassJobCategory membership.</param>
    /// <param name="tribes">The character's allied society standings.</param>
    /// <param name="storyRequired">The side quests the main scenario needs.</param>
    public static UnlockTierContext For(
        byte currentJob,
        IReadOnlyDictionary<byte, byte> parents,
        Evaluation.IClassJobCategoryLookup jobs,
        IReadOnlyDictionary<byte, TribeStanding> tribes,
        IReadOnlySet<uint> storyRequired)
    {
        ArgumentNullException.ThrowIfNull(parents);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(storyRequired);
        var parent = parents.GetValueOrDefault(currentJob);
        return new UnlockTierContext
        {
            StoryRequired = storyRequired,
            AdmitsCurrentJob = quest => quest.ClassJobRequired != 0
                ? quest.ClassJobRequired == currentJob || (parent != 0 && quest.ClassJobRequired == parent)
                : quest.ClassJobCategory == 0 || jobs.Admits(quest.ClassJobCategory, currentJob),
            PlaysJob = id => id == currentJob || (parent != 0 && id == parent) || (id <= byte.MaxValue && parents.GetValueOrDefault((byte)id) == currentJob),
            KnowsSociety = tribe => tribes.TryGetValue(tribe, out var standing) && standing.Rank > 0,
        };
    }
}

/// <summary>The tier words, their order and the classification (spec-1.21 P4).</summary>
public static class UnlockTiers
{
    /// <summary>Every tier, in Do first order.</summary>
    public static readonly UnlockTier[] All =
    [
        UnlockTier.StoryNeedsIt, UnlockTier.OpensContent, UnlockTier.Systems, UnlockTier.HighEnd, UnlockTier.AnotherJob,
    ];

    /// <summary>The tier word: "Story needs it", "Opens content", ….</summary>
    public static string Name(UnlockTier tier) => tier switch
    {
        UnlockTier.StoryNeedsIt => CoreText.T("Core.Tier.StoryNeedsIt", "Story needs it"),
        UnlockTier.OpensContent => CoreText.T("Core.Tier.OpensContent", "Opens content"),
        UnlockTier.Systems => CoreText.T("Core.Tier.Systems", "Systems"),
        UnlockTier.HighEnd => CoreText.T("Core.Tier.HighEnd", "High-end"),
        _ => CoreText.T("Core.Tier.AnotherJob", "Another job or society"),
    };

    /// <summary>
    /// The quest's tier, the first rule that applies:
    /// <list type="number">
    /// <item>the main scenario needs it (<see cref="UnlockTierContext.StoryRequired"/>): Story needs it;</item>
    /// <item>it is for another job: its class or job category does not admit the job played now, or it hands out a
    /// class or job (<see cref="RewardKind.ClassJob"/>) the character does not play; or it is an allied society's
    /// quest and the character has no standing with that society: Another job or society;</item>
    /// <item>every duty it opens is high-end (<see cref="PlanUnlock.HighEnd"/>): High-end;</item>
    /// <item>it opens duty content or flying, or it is a job quest of the job played now: Opens content;</item>
    /// <item>anything else (a system, a feature, Other): Systems.</item>
    /// </list>
    /// </summary>
    public static UnlockTier Classify(QuestRecord quest, IReadOnlyList<PlanUnlock> unlocks, UnlockTierContext context)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(unlocks);
        ArgumentNullException.ThrowIfNull(context);
        if (context.StoryRequired.Contains(quest.RowId))
        {
            return UnlockTier.StoryNeedsIt;
        }

        if (IsAnotherJob(quest, unlocks, context))
        {
            return UnlockTier.AnotherJob;
        }

        var duties = 0;
        var highEnd = 0;
        var content = false;
        var job = false;
        foreach (var unlock in unlocks)
        {
            if (unlock.Kind <= UnlockKind.FieldOperation)
            {
                duties++;
                highEnd += unlock.HighEnd ? 1 : 0;
                content |= !unlock.HighEnd;
            }
            else if (unlock.Kind == UnlockKind.Flying)
            {
                content = true;
            }
            else if (unlock.Kind == UnlockKind.Job)
            {
                job = true;
            }
        }

        if (duties > 0 && highEnd == duties && !content)
        {
            return UnlockTier.HighEnd;
        }

        return content || job ? UnlockTier.OpensContent : UnlockTier.Systems;
    }

    private static bool IsAnotherJob(QuestRecord quest, IReadOnlyList<PlanUnlock> unlocks, UnlockTierContext context)
    {
        var restricted = quest.ClassJobRequired != 0 || quest.ClassJobCategory != 0;
        var isJobQuest = false;
        foreach (var unlock in unlocks)
        {
            isJobQuest |= unlock.Kind == UnlockKind.Job;
        }

        if (isJobQuest && restricted && context.AdmitsCurrentJob is null)
        {
            return true;
        }

        if (restricted && context.AdmitsCurrentJob is { } admitsJob && !admitsJob(quest))
        {
            return true;
        }

        foreach (var reward in quest.Rewards)
        {
            if (reward.Kind == RewardKind.ClassJob && reward.Id != 0 && context.PlaysJob?.Invoke(reward.Id) != true)
            {
                return true;
            }
        }

        return quest.BeastTribe != 0 && context.KnowsSociety?.Invoke(quest.BeastTribe) != true;
    }
}
