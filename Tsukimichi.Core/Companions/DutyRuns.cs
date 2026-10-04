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
    public const uint Guildhests = 3;
    public const uint Trials = 4;
    public const uint Raids = 5;
    public const uint UltimateRaids = 28;
    public const uint ChaoticAllianceRaid = 37;

    /// <summary>The class or job level the Duty Finder asks for (<c>ContentFinderCondition.ClassJobLevelRequired</c>); 0 when unknown.</summary>
    public byte LevelRequired { get; init; }

    /// <summary>The average item level the Duty Finder asks for (<c>ContentFinderCondition.ItemLevelRequired</c>); 0 for none.</summary>
    public ushort ItemLevelRequired { get; init; }

    /// <summary>
    /// Whether the Duty Finder matches players for it (<c>ContentFinderCondition.IsInDutyFinder</c>). False for the
    /// Ultimate raids, the current Savage tier and the Chaotic raid, which a full party enters together, and for solo
    /// quest battles. True by default, so an entry built without the column reads as before.
    /// </summary>
    public bool InDutyFinder { get; init; } = true;

    /// <summary>Players the duty seats (<c>ContentMemberType</c> members per party times parties); 0 when unknown, 1 for a solo duty.</summary>
    public int Players { get; init; }

    /// <summary>The Duty Roulettes that draw from it (the <c>ContentFinderCondition</c> roulette columns).</summary>
    public DutyRoulettes Roulettes { get; init; }

    /// <summary>The Duty Finder's own order within its category (<c>ContentFinderCondition.SortKey</c>).</summary>
    public ushort SortKey { get; init; }
}

/// <summary>
/// The Duty Roulettes, as the <c>ContentFinderCondition</c> sheet's roulette columns name them; one duty can sit in
/// several. Each maps to one <c>ContentRoulette</c> row (<see cref="RouletteInfo.Id"/>).
/// </summary>
[Flags]
public enum DutyRoulettes : ushort
{
    None = 0,
    Leveling = 1 << 0,
    HighLevel = 1 << 1,
    MainScenario = 1 << 2,
    Guildhests = 1 << 3,
    Expert = 1 << 4,
    Trials = 1 << 5,
    LevelCap = 1 << 6,
    Mentor = 1 << 7,
    AllianceRaids = 1 << 8,
    NormalRaids = 1 << 9,
}

/// <summary>
/// One Duty Roulette as the <c>ContentRoulette</c> sheet describes it: its name, the duties it draws from (the
/// duties whose <see cref="DutyRunInfo.Roulettes"/> carry <paramref name="Flag"/>) and what opening it takes.
/// </summary>
/// <param name="Id">The <c>ContentRoulette</c> row id.</param>
/// <param name="Name">The roulette's name ("Duty Roulette: Level Cap Dungeons").</param>
/// <param name="Flag">Its column on the <c>ContentFinderCondition</c> sheet.</param>
/// <param name="RequiresEveryDuty">
/// The sheet's open rule asks for every duty in it (<c>ContentRouletteOpenRule.HasDutyRequirements</c>: Expert, Level
/// Cap, Mentor); otherwise a few of its duties open it (<see cref="DutyBoard.MinimumUnlocked"/>).
/// </param>
/// <param name="RequiredLevel">The level the roulette asks for (<c>ContentRoulette.RequiredLevel</c>).</param>
/// <param name="RequiredExpansion">The ExVersion row the account must own (<c>ContentRoulette.RequiredExVersion</c>).</param>
/// <param name="ItemLevelRequired">The average item level it asks for to queue.</param>
/// <param name="SortKey">The Duty Finder's order of the roulettes.</param>
public sealed record RouletteInfo(
    uint Id,
    string Name,
    DutyRoulettes Flag,
    bool RequiresEveryDuty,
    byte RequiredLevel,
    byte RequiredExpansion,
    ushort ItemLevelRequired,
    byte SortKey)
{
    /// <summary>The roulette's short name ("Level Cap Dungeons", <c>ContentRoulette.Category</c>); <see cref="Name"/> when the sheet has none.</summary>
    public string ShortName
    {
        get => string.IsNullOrEmpty(shortName) ? Name : shortName;
        init => shortName = value;
    }

    private readonly string? shortName;
}

/// <summary>Every <see cref="DutyRunInfo"/> by ContentFinderCondition id and by InstanceContent id, and the Duty Roulettes. Immutable.</summary>
public sealed class DutyRunIndex
{
    public static readonly DutyRunIndex Empty = new(FrozenDictionary<uint, DutyRunInfo>.Empty, FrozenDictionary<uint, DutyRunInfo>.Empty, [], []);

    private readonly FrozenDictionary<uint, DutyRunInfo> byCondition;
    private readonly FrozenDictionary<uint, DutyRunInfo> byInstance;

    private DutyRunIndex(FrozenDictionary<uint, DutyRunInfo> byCondition, FrozenDictionary<uint, DutyRunInfo> byInstance, DutyRunInfo[] all, RouletteInfo[] roulettes)
    {
        this.byCondition = byCondition;
        this.byInstance = byInstance;
        All = all;
        Roulettes = roulettes;
    }

    public int Count => byCondition.Count;

    /// <summary>Every entry, in the order given (the sheet's).</summary>
    public IReadOnlyList<DutyRunInfo> All { get; }

    /// <summary>The Duty Roulettes in the Duty Finder's order; empty when none was read.</summary>
    public IReadOnlyList<RouletteInfo> Roulettes { get; }

    /// <summary>The first entry per instance wins (an instance some Duty Finder entries share keeps its first).</summary>
    public static DutyRunIndex From(IEnumerable<DutyRunInfo> duties, IEnumerable<RouletteInfo>? roulettes = null)
    {
        ArgumentNullException.ThrowIfNull(duties);
        var byCondition = new Dictionary<uint, DutyRunInfo>();
        var byInstance = new Dictionary<uint, DutyRunInfo>();
        var all = new List<DutyRunInfo>();
        foreach (var duty in duties)
        {
            if (duty.ContentFinderConditionId == 0 || !byCondition.TryAdd(duty.ContentFinderConditionId, duty))
            {
                continue;
            }

            all.Add(duty);
            if (duty.InstanceContentId != 0)
            {
                byInstance.TryAdd(duty.InstanceContentId, duty);
            }
        }

        var ordered = roulettes is null ? [] : roulettes.OrderBy(static r => r.SortKey).ThenBy(static r => r.Id).ToArray();
        return new DutyRunIndex(byCondition.ToFrozenDictionary(), byInstance.ToFrozenDictionary(), [.. all], ordered);
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
