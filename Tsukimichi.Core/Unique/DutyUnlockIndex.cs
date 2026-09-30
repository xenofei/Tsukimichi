using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// Which quests unlock a duty, keyed by <c>ContentFinderCondition</c> row id (what the Duty Finder selects): the Duty
/// Finder unlock hint (P13) asks it for the duty the player picked. Built from <c>curated/duty_unlocks.json</c> first,
/// in file order, then from every <see cref="RewardKind.DutyUnlock"/> entry of the reward data (<c>unique_quests.json</c>
/// through the merged <see cref="UniqueRewardCatalog"/>: <c>Quest.InstanceContentUnlock</c> and
/// <c>ContentFinderCondition.UnlockCriteria</c>) in catalog order; a quest listed by both appears once, at its curated
/// place. Immutable and allocation-free to query.
/// </summary>
public sealed class DutyUnlockIndex
{
    private static readonly uint[] NoQuests = [];

    /// <summary>The index over no data: every duty resolves to no quest.</summary>
    public static readonly DutyUnlockIndex Empty = new(FrozenDictionary<uint, uint[]>.Empty, CuratedData.Empty, UniqueRewardCatalog.Empty);

    private readonly FrozenDictionary<uint, uint[]> byCondition;

    private DutyUnlockIndex(FrozenDictionary<uint, uint[]> byCondition, CuratedData curated, UniqueRewardCatalog rewards)
    {
        this.byCondition = byCondition;
        Curated = curated;
        Rewards = rewards;
    }

    /// <summary>The curated overlay this index was built from.</summary>
    public CuratedData Curated { get; }

    /// <summary>The reward catalog this index was built from.</summary>
    public UniqueRewardCatalog Rewards { get; }

    /// <summary>Number of duties with at least one known unlocking quest.</summary>
    public int Count => byCondition.Count;

    /// <summary>
    /// Builds the index. Reward entries are read from <see cref="UniqueRewardCatalog.All"/> and
    /// <see cref="UniqueRewardCatalog.Hidden"/> alike: a quest the player marked "not unique" in Moonlit still unlocks
    /// its duty.
    /// </summary>
    public static DutyUnlockIndex Build(CuratedData curated, UniqueRewardCatalog rewards)
    {
        ArgumentNullException.ThrowIfNull(curated);
        ArgumentNullException.ThrowIfNull(rewards);

        var map = new Dictionary<uint, List<uint>>();
        foreach (var (questRowId, unlock) in curated.DutyUnlocks)
        {
            foreach (var condition in unlock.ContentFinderConditionIds)
            {
                Add(map, condition, questRowId);
            }
        }

        AddEntries(map, rewards.All);
        AddEntries(map, rewards.Hidden);

        if (map.Count == 0)
        {
            return new DutyUnlockIndex(FrozenDictionary<uint, uint[]>.Empty, curated, rewards);
        }

        return new DutyUnlockIndex(map.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray()), curated, rewards);
    }

    /// <summary>Whether this index was built from exactly these inputs (by reference), so a caller can tell when to rebuild.</summary>
    public bool Matches(CuratedData curated, UniqueRewardCatalog rewards) => ReferenceEquals(Curated, curated) && ReferenceEquals(Rewards, rewards);

    /// <summary>Quest row ids that unlock the duty, curated first; empty for zero, an unknown id or a duty no data covers.</summary>
    public IReadOnlyList<uint> QuestsFor(uint contentFinderConditionId) =>
        contentFinderConditionId == 0 ? NoQuests : byCondition.GetValueOrDefault(contentFinderConditionId) ?? NoQuests;

    /// <summary>
    /// The quests that unlock the duty as <paramref name="catalog"/> knows them, in <see cref="QuestsFor"/> order, leaving
    /// out ids the catalog lacks and quests the game removed (<see cref="QuestRecord.IsRemoved"/>: a retired row, or a
    /// pre-6.1 row the journal no longer lists, which its rework replaced). Empty when none is left. Allocates; call it
    /// when the selected duty changes, not per frame.
    /// </summary>
    public IReadOnlyList<QuestRecord> Resolve(uint contentFinderConditionId, QuestCatalog? catalog)
    {
        var ids = QuestsFor(contentFinderConditionId);
        if (ids.Count == 0 || catalog is null)
        {
            return [];
        }

        var result = new List<QuestRecord>(ids.Count);
        foreach (var id in ids)
        {
            if (catalog.GetByRowId(id) is { IsRemoved: false } quest)
            {
                result.Add(quest);
            }
        }

        return result;
    }

    private static void AddEntries(Dictionary<uint, List<uint>> map, IReadOnlyList<UniqueRewardEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Kind == RewardKind.DutyUnlock)
            {
                Add(map, entry.RewardId, entry.QuestRowId);
            }
        }
    }

    private static void Add(Dictionary<uint, List<uint>> map, uint condition, uint questRowId)
    {
        if (condition == 0 || questRowId == 0)
        {
            return;
        }

        if (!map.TryGetValue(condition, out var quests))
        {
            quests = [];
            map[condition] = quests;
        }

        if (!quests.Contains(questRowId))
        {
            quests.Add(questRowId);
        }
    }
}

/// <summary>
/// Hands out a <see cref="DutyUnlockIndex"/> that follows its two live inputs: the curated overlay and the merged reward
/// catalog (rebuilt after a Moonlit override change). <see cref="Current"/> rebuilds only when either reference changed,
/// so a per-frame caller pays two reference comparisons. Not thread-safe; the plugin reads it on the framework thread.
/// </summary>
public sealed class DutyUnlockIndexSource
{
    private readonly Func<CuratedData> curated;
    private readonly Func<UniqueRewardCatalog> rewards;
    private DutyUnlockIndex current = DutyUnlockIndex.Empty;

    public DutyUnlockIndexSource(Func<CuratedData> curated, Func<UniqueRewardCatalog> rewards)
    {
        this.curated = curated ?? throw new ArgumentNullException(nameof(curated));
        this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
    }

    /// <summary>The index for the inputs as they are now.</summary>
    public DutyUnlockIndex Current
    {
        get
        {
            var c = curated();
            var r = rewards();
            if (!current.Matches(c, r))
            {
                current = DutyUnlockIndex.Build(c, r);
            }

            return current;
        }
    }
}
