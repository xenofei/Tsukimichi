using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Which quests count as feature ("blue") quests. The mapped <see cref="QuestRecord"/> carries no flag for the blue
/// journal icon, so the set is derived: a quest is a feature quest when curated data lists it (the feature list or a
/// system or duty unlock entry), when the shipped unique-reward data records an unlock for it, or when its own
/// rewards unlock something: a duty, a class or job, an action, a general action, a trait, an aether current, a blue
/// magic spell, a system unlock, or a named <c>Quest.OtherReward</c>. Main scenario quests and repeatables are never
/// feature quests, whatever they reward.
/// </summary>
public static class FeaturePresets
{
    /// <summary>Journal sections holding main scenario quests: 0 (A Realm Reborn through Endwalker) and 1 (Dawntrail onward).</summary>
    public static bool IsMainScenario(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return !quest.IsUnlisted && quest.Journal.SectionId is 0 or 1;
    }

    /// <summary>Whether a reward of this kind marks the quest that gives it as a feature quest.</summary>
    public static bool IsUnlockKind(RewardKind kind) => kind is RewardKind.Instance
        or RewardKind.DutyUnlock
        or RewardKind.ClassJob
        or RewardKind.SystemUnlock
        or RewardKind.Action
        or RewardKind.GeneralAction
        or RewardKind.Trait
        or RewardKind.AetherCurrent
        or RewardKind.BlueMageSpell;

    public static bool IsFeatureQuest(QuestRecord quest, CuratedData curated) => IsFeatureQuest(quest, curated, null);

    /// <param name="unlockQuests">Row ids of quests the unique-reward data records an unlock kind for; see <see cref="UnlockQuests"/>.</param>
    public static bool IsFeatureQuest(QuestRecord quest, CuratedData curated, IReadOnlySet<uint>? unlockQuests)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(curated);

        if (quest.IsRepeatable || IsMainScenario(quest))
        {
            return false;
        }

        if (curated.FeatureQuests.Contains(quest.RowId)
            || curated.SystemUnlocks.ContainsKey(quest.RowId)
            || curated.DutyUnlocks.ContainsKey(quest.RowId)
            || (unlockQuests is not null && unlockQuests.Contains(quest.RowId)))
        {
            return true;
        }

        var rewards = quest.Rewards;
        for (var i = 0; i < rewards.Count; i++)
        {
            if (IsFeatureReward(rewards[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Quests the unique-reward data credits with an unlock (duty, aether current, system, trait, spell, job, action).</summary>
    public static HashSet<uint> UnlockQuests(IEnumerable<UniqueRewardEntry> uniqueRewards)
    {
        ArgumentNullException.ThrowIfNull(uniqueRewards);
        var ids = new HashSet<uint>();
        foreach (var entry in uniqueRewards)
        {
            if (IsUnlockKind(entry.Kind))
            {
                ids.Add(entry.QuestRowId);
            }
        }

        return ids;
    }

    /// <summary>Row ids of every feature quest in the catalog. Computed once per catalog; the result is immutable.</summary>
    /// <param name="uniqueRewards">The shipped unique-reward entries; null derives from curated data and quest rewards alone.</param>
    public static FrozenSet<uint> Derive(QuestCatalog catalog, CuratedData curated, IEnumerable<UniqueRewardEntry>? uniqueRewards = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(curated);

        var unlockQuests = uniqueRewards is null ? null : UnlockQuests(uniqueRewards);
        var ids = new HashSet<uint>();
        foreach (var quest in catalog.All)
        {
            if (IsFeatureQuest(quest, curated, unlockQuests))
            {
                ids.Add(quest.RowId);
            }
        }

        return ids.ToFrozenSet();
    }

    /// <summary>
    /// <see cref="RewardKind.Other"/> is shared by three sources in the catalog: <c>Quest.OtherReward</c> (named, no
    /// item), currency rewards (carry the currency's item id) and unresolved reward slots (unnamed); only the first
    /// one marks a feature quest.
    /// </summary>
    private static bool IsFeatureReward(RewardRef reward) =>
        IsUnlockKind(reward.Kind) || (reward.Kind == RewardKind.Other && reward.ItemId == 0 && reward.Name.Length > 0);
}
