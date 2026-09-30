using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Route;

/// <summary>What an unlock route leads to; the route itself is always to a quest (<see cref="RouteTarget.QuestRowIds"/>).</summary>
public enum RouteTargetKind : byte
{
    /// <summary>A quest picked directly (the detail pane's "Route to this").</summary>
    Quest,

    /// <summary>A job, through its unlock quest (<see cref="LadderJob.UnlockQuestRowId"/>).</summary>
    Job,

    /// <summary>A duty: the quest whose reward opens it (an Instance or DutyUnlock reward, sheet or curated).</summary>
    Duty,

    /// <summary>A system (retainers, the Gold Saucer, flying), from curated <c>system_unlocks.json</c>.</summary>
    System,

    /// <summary>A Moonlit reward: the quest that grants it.</summary>
    Reward,
}

/// <summary>
/// The thing a player wants and the quest or quests that give it (feature plan v3 P6). Several quests stand for one
/// target where the game has variants (the three Grand Company enrollments, the starting-city envoys that each hand
/// out the Wind-up Airship, two row ids of one reworked quest); <see cref="UnlockRoute.Build"/> takes the variant
/// with the fewest quests left and lists the others. Empty when nothing is known to unlock the target.
/// </summary>
/// <param name="Kind">What the target is.</param>
/// <param name="Label">The target's name as the route card titles it ("Blue Mage", "Retainers"); for a quest target the caller passes the name through the spoiler shield.</param>
/// <param name="QuestRowIds">Quest sheet row ids any one of which unlocks the target, distinct, in the order given.</param>
public sealed record RouteTarget(RouteTargetKind Kind, string Label, IReadOnlyList<uint> QuestRowIds)
{
    /// <summary>A quest picked directly.</summary>
    public static RouteTarget ForQuest(uint rowId, string label) => new(RouteTargetKind.Quest, label ?? string.Empty, [rowId]);

    /// <summary>A job through its unlock quest; a base class (no unlock quest) has nothing to route to.</summary>
    public static RouteTarget ForJob(LadderJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return ForJob(job.Name, job.UnlockQuestRowId);
    }

    /// <summary>A job by name and unlock quest row id (0 when the job has none, which gives an empty target).</summary>
    public static RouteTarget ForJob(string name, uint unlockQuestRowId) =>
        new(RouteTargetKind.Job, name ?? string.Empty, unlockQuestRowId == 0 ? [] : [unlockQuestRowId]);

    /// <summary>
    /// A Moonlit entry: the quest granting it, plus every other entry of <paramref name="all"/> handing out the same
    /// reward (same kind, reward id and item; for a system unlock, which has no id, the same curated label). A reward
    /// without an id of any other kind (a quest you marked unique yourself) stands alone. A duty or system unlock entry
    /// makes a <see cref="RouteTargetKind.Duty"/> or <see cref="RouteTargetKind.System"/> target.
    /// </summary>
    /// <param name="label">The name the route is titled with; null uses <see cref="UniqueRewardEntry.RewardName"/>.</param>
    public static RouteTarget ForReward(UniqueRewardEntry entry, IEnumerable<UniqueRewardEntry>? all = null, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var quests = new List<uint> { entry.QuestRowId };
        if (all is not null)
        {
            foreach (var other in all)
            {
                if (other.Kind == entry.Kind && SameReward(entry, other) && !quests.Contains(other.QuestRowId))
                {
                    quests.Add(other.QuestRowId);
                }
            }
        }

        var kind = entry.Kind switch
        {
            RewardKind.DutyUnlock or RewardKind.Instance => RouteTargetKind.Duty,
            RewardKind.SystemUnlock => RouteTargetKind.System,
            _ => RouteTargetKind.Reward,
        };
        return new RouteTarget(kind, label ?? entry.RewardName, quests);
    }

    /// <summary>
    /// A duty by the id its reward carries: the quests whose sheet rewards list it (<paramref name="kind"/>
    /// <see cref="RewardKind.Instance"/> with an InstanceContent id, or <see cref="RewardKind.DutyUnlock"/> with a
    /// ContentFinderCondition id), then the entries of <paramref name="entries"/> (the curated duty unlocks) naming it.
    /// </summary>
    public static RouteTarget ForDuty(QuestCatalog catalog, RewardKind kind, uint dutyId, string label, IEnumerable<UniqueRewardEntry>? entries = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var quests = new List<uint>();
        foreach (var quest in catalog.All)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.Kind == kind && reward.Id == dutyId && !quests.Contains(quest.RowId))
                {
                    quests.Add(quest.RowId);
                }
            }
        }

        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                if (entry.Kind == kind && entry.RewardId == dutyId && !quests.Contains(entry.QuestRowId))
                {
                    quests.Add(entry.QuestRowId);
                }
            }
        }

        return new RouteTarget(RouteTargetKind.Duty, label ?? string.Empty, quests);
    }

    /// <summary>A system by its curated label ("Retainers"): every system-unlock entry of <paramref name="entries"/> with that label.</summary>
    public static RouteTarget ForSystem(IEnumerable<UniqueRewardEntry> entries, string label)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var quests = new List<uint>();
        foreach (var entry in entries)
        {
            if (entry.Kind == RewardKind.SystemUnlock && string.Equals(entry.RewardName, label, StringComparison.Ordinal) && !quests.Contains(entry.QuestRowId))
            {
                quests.Add(entry.QuestRowId);
            }
        }

        return new RouteTarget(RouteTargetKind.System, label ?? string.Empty, quests);
    }

    private static bool SameReward(UniqueRewardEntry a, UniqueRewardEntry b)
    {
        if (a.RewardId != 0 || b.RewardId != 0)
        {
            return a.RewardId == b.RewardId && a.ItemId == b.ItemId;
        }

        return a.Kind == RewardKind.SystemUnlock && string.Equals(a.RewardName, b.RewardName, StringComparison.Ordinal);
    }
}
