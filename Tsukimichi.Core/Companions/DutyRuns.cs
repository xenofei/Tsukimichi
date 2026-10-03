using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Companions;

/// <summary>
/// One duty as AutoDuty sees it: the Duty Finder entry, the instance it links, the territory AutoDuty's gates take
/// (<c>AutoDuty.ContentHasPath(uint territoryType)</c>, <c>AutoDuty.Run(uint territoryType, …)</c>) and the ways it
/// can be queued. Read from the sheets by <c>Tsukimichi.GameData.DutyRunSheets</c>.
/// </summary>
/// <param name="ContentFinderConditionId">ContentFinderCondition row id (what <see cref="RewardKind.DutyUnlock"/> carries).</param>
/// <param name="InstanceContentId">The InstanceContent row the entry links (what <see cref="QuestRecord.InstanceContentRequired"/> holds); 0 when none.</param>
/// <param name="TerritoryTypeId">The duty's TerritoryType row id.</param>
/// <param name="ContentTypeId">The Duty Finder category (ContentType row: 2 dungeons, 4 trials, 5 raids).</param>
/// <param name="Name">The duty's name as the sheet spells it.</param>
/// <param name="OffersDutySupport">A DawnContent row names it with more than one party choice: the Duty Support window lists it.</param>
/// <param name="OffersTrust">A DawnContent row names it and it is from Shadowbringers on: Trust lists it.</param>
/// <param name="Icon">The duty's icon through <see cref="Unlocks.DutyArt"/>'s chain (its emblem, its category's tile, …); 0 for the stand-in.</param>
public sealed record DutyRunInfo(
    uint ContentFinderConditionId,
    uint InstanceContentId,
    uint TerritoryTypeId,
    uint ContentTypeId,
    string Name,
    bool OffersDutySupport,
    bool OffersTrust,
    uint Icon = 0)
{
    public const uint Dungeons = 2;
    public const uint Trials = 4;
    public const uint Raids = 5;
}

/// <summary>Every <see cref="DutyRunInfo"/> by ContentFinderCondition id and by InstanceContent id. Immutable.</summary>
public sealed class DutyRunIndex
{
    public static readonly DutyRunIndex Empty = new(FrozenDictionary<uint, DutyRunInfo>.Empty, FrozenDictionary<uint, DutyRunInfo>.Empty);

    private readonly FrozenDictionary<uint, DutyRunInfo> byCondition;
    private readonly FrozenDictionary<uint, DutyRunInfo> byInstance;

    private DutyRunIndex(FrozenDictionary<uint, DutyRunInfo> byCondition, FrozenDictionary<uint, DutyRunInfo> byInstance)
    {
        this.byCondition = byCondition;
        this.byInstance = byInstance;
    }

    public int Count => byCondition.Count;

    /// <summary>The first entry per instance wins (an instance some Duty Finder entries share keeps its first).</summary>
    public static DutyRunIndex From(IEnumerable<DutyRunInfo> duties)
    {
        ArgumentNullException.ThrowIfNull(duties);
        var byCondition = new Dictionary<uint, DutyRunInfo>();
        var byInstance = new Dictionary<uint, DutyRunInfo>();
        foreach (var duty in duties)
        {
            if (duty.ContentFinderConditionId == 0)
            {
                continue;
            }

            byCondition.TryAdd(duty.ContentFinderConditionId, duty);
            if (duty.InstanceContentId != 0)
            {
                byInstance.TryAdd(duty.InstanceContentId, duty);
            }
        }

        return new DutyRunIndex(byCondition.ToFrozenDictionary(), byInstance.ToFrozenDictionary());
    }

    public DutyRunInfo? ByCondition(uint contentFinderConditionId) => byCondition.GetValueOrDefault(contentFinderConditionId);

    public DutyRunInfo? ByInstance(uint instanceContentId) => byInstance.GetValueOrDefault(instanceContentId);
}

/// <summary>How a duty relates to a quest in the detail pane's Duties section.</summary>
public enum QuestDutyRelation
{
    /// <summary>The quest needs it cleared before it is offered (<see cref="QuestRecord.InstanceContentRequired"/>).</summary>
    Required,

    /// <summary>The quest adds it to the Duty Finder (a duty unlock reward, curated or from the sheets).</summary>
    Unlocks,
}

/// <summary>A duty the detail pane offers to run for a quest.</summary>
public sealed record QuestDuty(DutyRunInfo Duty, QuestDutyRelation Relation);

/// <summary>The duties a quest requires or unlocks, as the detail pane lists them.</summary>
public static class QuestDuties
{
    /// <summary>The section lists at most this many; a relic step naming more is rare.</summary>
    public const int Max = 4;

    /// <summary>
    /// Required duties first (in the quest's order), then unlocked ones: curated <c>duty_unlocks.json</c> first, then
    /// the quest's <see cref="RewardKind.DutyUnlock"/> entries in the reward catalog (shown and hidden alike). A duty the
    /// index does not know (no Duty Finder entry) is left out, as is a duty already listed. At most <see cref="Max"/>.
    /// Allocates; call it when the selection changes.
    /// </summary>
    public static IReadOnlyList<QuestDuty> For(QuestRecord quest, DutyRunIndex index, CuratedData? curated, IReadOnlyList<UniqueRewardEntry>? rewardEntries)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(index);
        if (index.Count == 0)
        {
            return [];
        }

        var result = new List<QuestDuty>();
        foreach (var instance in quest.InstanceContentRequired)
        {
            Add(result, index.ByInstance(instance), QuestDutyRelation.Required);
        }

        if (curated is not null && curated.DutyUnlocks.TryGetValue(quest.RowId, out var unlock))
        {
            foreach (var condition in unlock.ContentFinderConditionIds)
            {
                Add(result, index.ByCondition(condition), QuestDutyRelation.Unlocks);
            }
        }

        if (rewardEntries is not null)
        {
            foreach (var entry in rewardEntries)
            {
                if (entry.Kind == RewardKind.DutyUnlock && entry.QuestRowId == quest.RowId)
                {
                    Add(result, index.ByCondition(entry.RewardId), QuestDutyRelation.Unlocks);
                }
            }
        }

        return result;
    }

    private static void Add(List<QuestDuty> result, DutyRunInfo? duty, QuestDutyRelation relation)
    {
        if (duty is null || result.Count >= Max)
        {
            return;
        }

        foreach (var existing in result)
        {
            if (existing.Duty.ContentFinderConditionId == duty.ContentFinderConditionId)
            {
                return;
            }
        }

        result.Add(new QuestDuty(duty, relation));
    }
}
