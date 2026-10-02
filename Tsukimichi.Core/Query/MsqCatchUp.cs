using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Query;

/// <summary>
/// One duty on the main scenario path: the Duty Finder entry and the instance it links, either id 0 when unknown. Two
/// duties are the same when either id matches (<see cref="Same"/>): a duty one quest needs cleared (an InstanceContent
/// id) is the one an earlier quest unlocks (a ContentFinderCondition id) once the plugin can map one to the other.
/// </summary>
public readonly record struct CatchUpDuty(uint ContentFinderConditionId, uint InstanceContentId)
{
    /// <summary>Whether both stand for the same duty.</summary>
    public bool Same(CatchUpDuty other) =>
        (ContentFinderConditionId != 0 && ContentFinderConditionId == other.ContentFinderConditionId)
        || (InstanceContentId != 0 && InstanceContentId == other.InstanceContentId);
}

/// <summary>
/// The duties a main scenario quest opens, and how an instance maps to its Duty Finder entry, for the catch-up's duty
/// count: the curated <c>duty_unlocks.json</c> and the <see cref="RewardKind.DutyUnlock"/> entries of the reward data,
/// read as <see cref="DutyUnlockIndex"/> reads them. Core cannot read the sheets, so the instance mapping comes from
/// the plugin (the duty index); without it a required duty and an unlocked one never merge.
/// </summary>
/// <param name="UnlocksOf">Quest row id to the ContentFinderCondition ids it unlocks.</param>
/// <param name="ConditionOf">InstanceContent id to its ContentFinderCondition id; 0 when unknown. Null knows none.</param>
public sealed record CatchUpDutySource(Func<uint, IReadOnlyList<uint>> UnlocksOf, Func<uint, uint>? ConditionOf = null)
{
    /// <summary>Builds the source from the curated overlay and the merged reward catalog (shown and hidden entries alike).</summary>
    public static CatchUpDutySource From(CuratedData curated, UniqueRewardCatalog rewards, Func<uint, uint>? conditionOf = null)
    {
        ArgumentNullException.ThrowIfNull(curated);
        ArgumentNullException.ThrowIfNull(rewards);
        return new CatchUpDutySource(
            rowId =>
            {
                var list = new List<uint>();
                if (curated.DutyUnlocks.TryGetValue(rowId, out var unlock))
                {
                    list.AddRange(unlock.ContentFinderConditionIds.Where(static id => id != 0));
                }

                foreach (var entry in rewards.ForQuest(rowId))
                {
                    if (entry.Kind == RewardKind.DutyUnlock && entry.RewardId != 0 && !list.Contains(entry.RewardId))
                    {
                        list.Add(entry.RewardId);
                    }
                }

                return list;
            },
            conditionOf);
    }
}

/// <summary>
/// The main scenario quests one expansion still holds for the character: how many, their level span (the levels the
/// journal prints) and the duties on the way.
/// </summary>
/// <param name="Expansion">The ExVersion row id.</param>
/// <param name="Quests">Main scenario quests left.</param>
/// <param name="MinLevel">Lowest level among them.</param>
/// <param name="MaxLevel">Highest level among them.</param>
/// <param name="Duties">The duties those quests ask to have cleared that the character has not, and the ones they unlock, in story order.</param>
public sealed record CatchUpExpansion(byte Expansion, int Quests, byte MinLevel, byte MaxLevel, IReadOnlyList<CatchUpDuty> Duties);

/// <summary>
/// "To reach the latest story: 143 quests, Lv 90–100, 6 duties" (feature plan v5 "Planning extras", R6 F): the main
/// scenario quests left on the character's path, per expansion. Counts only; no hours. See <see cref="MsqCatchUp.Compute"/>.
/// </summary>
public sealed record MsqCatchUpSummary(IReadOnlyList<CatchUpExpansion> Expansions)
{
    /// <summary>Main scenario quests left in every expansion.</summary>
    public int Quests => Expansions.Sum(static e => e.Quests);

    /// <summary>Lowest level left; 0 when nothing is.</summary>
    public byte MinLevel => Expansions.Count == 0 ? (byte)0 : Expansions.Min(static e => e.MinLevel);

    /// <summary>Highest level left; 0 when nothing is.</summary>
    public byte MaxLevel => Expansions.Count == 0 ? (byte)0 : Expansions.Max(static e => e.MaxLevel);

    /// <summary>Distinct duties left across every expansion.</summary>
    public int Duties
    {
        get
        {
            var seen = new List<CatchUpDuty>();
            foreach (var duty in Expansions.SelectMany(static e => e.Duties))
            {
                if (!seen.Exists(d => d.Same(duty)))
                {
                    seen.Add(duty);
                }
            }

            return seen.Count;
        }
    }

    /// <summary>Whether the character has caught up with the latest story.</summary>
    public bool IsComplete => Expansions.Count == 0;
}

/// <summary>
/// The main scenario catch-up summary. Walks the story as <see cref="MsqGraph"/> does (sections 0 and 1 in journal
/// order, removed quests left out) and counts every quest that is not completed, leaving out those that leave the
/// totals (another city's or Grand Company's quests, a spare alternative) and, inside a routed branch region whose
/// reconvergence quest is already open or done, the optional leftovers of the other routes, so the count equals the
/// position's <see cref="MsqPosition.Total"/> less its <see cref="MsqPosition.Done"/>. A quest's duties are the
/// <see cref="QuestRecord.InstanceContentRequired"/> rows the character has not cleared
/// (<see cref="CharacterSnapshot.UnlockedInstances"/>, as the evaluator reads them), the instance it opens
/// (<see cref="RewardKind.Instance"/>) and, with a <see cref="CatchUpDutySource"/>, the duties it unlocks: the story's
/// dungeons and trials, not done while the quest that opens them is not. Pure.
/// </summary>
public static class MsqCatchUp
{
    /// <summary>The summary; null when the catalog has no main scenario quests.</summary>
    public static MsqCatchUpSummary? Compute(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        CharacterSnapshot? snapshot,
        CatchUpDutySource? duties = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        var graph = MsqGraph.For(catalog);
        if (graph.Story.Count == 0)
        {
            return null;
        }

        var cleared = snapshot is null ? new HashSet<uint>() : new HashSet<uint>(snapshot.UnlockedInstances);
        var parts = new SortedDictionary<byte, (int Quests, byte Min, byte Max, List<CatchUpDuty> Duties)>();
        foreach (var quest in graph.Story)
        {
            var evaluation = states.GetValueOrDefault(quest.RowId);
            if (evaluation is { LeavesTotals: true } || evaluation?.State == QuestState.Completed)
            {
                continue;
            }

            if (graph.RouteOf(quest.RowId) is not null
                && graph.RoutedBranchOf(quest.RowId) is { } branch
                && states.GetValueOrDefault(branch.Join.RowId)?.State is QuestState.Completed or QuestState.Accepted or QuestState.Ready or QuestState.ReadyOnOtherJob)
            {
                // The story moved on past this region without this route's quest.
                continue;
            }

            var level = quest.DisplayLevel;
            if (!parts.TryGetValue(quest.Expansion, out var part))
            {
                part = (0, level, level, []);
            }

            part.Quests++;
            part.Min = Math.Min(part.Min, level);
            part.Max = Math.Max(part.Max, level);
            foreach (var instance in quest.InstanceContentRequired)
            {
                if (instance != 0 && !cleared.Contains(instance))
                {
                    Add(part.Duties, new CatchUpDuty(duties?.ConditionOf?.Invoke(instance) ?? 0, instance));
                }
            }

            foreach (var reward in quest.Rewards)
            {
                if (reward.Kind == RewardKind.Instance && reward.Id != 0)
                {
                    Add(part.Duties, new CatchUpDuty(duties?.ConditionOf?.Invoke(reward.Id) ?? 0, reward.Id));
                }
            }

            if (duties is not null)
            {
                foreach (var condition in duties.UnlocksOf(quest.RowId))
                {
                    Add(part.Duties, new CatchUpDuty(condition, 0));
                }
            }

            parts[quest.Expansion] = part;
        }

        var list = new List<CatchUpExpansion>(parts.Count);
        foreach (var (expansion, part) in parts)
        {
            list.Add(new CatchUpExpansion(expansion, part.Quests, part.Min, part.Max, part.Duties));
        }

        return new MsqCatchUpSummary(list);
    }

    private static void Add(List<CatchUpDuty> duties, CatchUpDuty duty)
    {
        if (duty.ContentFinderConditionId == 0 && duty.InstanceContentId == 0)
        {
            return;
        }

        for (var i = 0; i < duties.Count; i++)
        {
            if (duties[i].Same(duty))
            {
                // Keep the fuller identity: an unlocked entry later named by its instance, or the reverse.
                duties[i] = new CatchUpDuty(
                    duties[i].ContentFinderConditionId != 0 ? duties[i].ContentFinderConditionId : duty.ContentFinderConditionId,
                    duties[i].InstanceContentId != 0 ? duties[i].InstanceContentId : duty.InstanceContentId);
                return;
            }
        }

        duties.Add(duty);
    }
}
