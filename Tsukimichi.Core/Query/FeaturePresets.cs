using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Which quests count as feature ("blue") quests. A quest is a feature quest when the game draws it with the blue
/// journal icon (<see cref="QuestRecord.EventIconType"/> equal to <see cref="FeatureEventIconType"/>, or the
/// quasi-quest type <see cref="QuasiQuestEventIconType"/> that shares the icon), when curated data lists it (the
/// feature list or a system or duty unlock entry), when the shipped unique-reward data records an unlock for it, or
/// when its own rewards unlock something: a duty, a class or job, an action, a general action, a trait, an aether
/// current, a blue magic spell, a system unlock, or a named <c>Quest.OtherReward</c>. Main scenario quests,
/// repeatables and retired quests are never feature quests, whatever they reward or show.
/// </summary>
public static class FeaturePresets
{
    /// <summary><c>Quest.EventIconType</c> row of the blue "+" feature quest icon.</summary>
    public const byte FeatureEventIconType = 8;

    /// <summary>
    /// <c>Quest.EventIconType</c> row of the quasi-quest: the same blue "+" map icons as <see cref="FeatureEventIconType"/>,
    /// on quests one dialogue accepts and completes (class intros, Leves of…, Sights of…, Gold Saucer, Eureka entry).
    /// </summary>
    public const byte QuasiQuestEventIconType = 10;

    /// <summary>Whether the game draws the quest with the blue "+" icon, as a feature quest or a quasi-quest.</summary>
    public static bool HasFeatureIcon(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.EventIconType is FeatureEventIconType or QuasiQuestEventIconType;
    }

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

        if (quest.IsRetired || quest.IsRepeatable || IsMainScenario(quest))
        {
            return false;
        }

        if (HasFeatureIcon(quest)
            || curated.FeatureQuests.Contains(quest.RowId)
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

    /// <summary>Quests that grant an aether current: the unique-reward data credits them with one, or a quest reward is one.</summary>
    public static HashSet<uint> AetherCurrentQuests(QuestCatalog catalog, IEnumerable<UniqueRewardEntry> uniqueRewards)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(uniqueRewards);
        var ids = new HashSet<uint>();
        foreach (var entry in uniqueRewards)
        {
            if (entry.Kind == RewardKind.AetherCurrent)
            {
                ids.Add(entry.QuestRowId);
            }
        }

        foreach (var quest in catalog.All)
        {
            for (var i = 0; i < quest.Rewards.Count; i++)
            {
                if (quest.Rewards[i].Kind == RewardKind.AetherCurrent)
                {
                    ids.Add(quest.RowId);
                    break;
                }
            }
        }

        return ids;
    }

    /// <summary>Quests the unique-reward data credits with an unlock other than an aether current (a duty, system, job, action, trait, spell).</summary>
    public static HashSet<uint> NonCurrentUnlockQuests(IEnumerable<UniqueRewardEntry> uniqueRewards)
    {
        ArgumentNullException.ThrowIfNull(uniqueRewards);
        var ids = new HashSet<uint>();
        foreach (var entry in uniqueRewards)
        {
            if (IsUnlockKind(entry.Kind) && entry.Kind != RewardKind.AetherCurrent)
            {
                ids.Add(entry.QuestRowId);
            }
        }

        return ids;
    }

    /// <summary>
    /// Whether every unlock a quest carries is an aether current (or it carries none beyond the blue icon): no curated
    /// system or duty unlock, no unique-reward unlock of another kind (<paramref name="nonCurrentUnlockQuests"/>), no
    /// reward of another unlock kind, and a named <c>Quest.OtherReward</c> only when it is the quest's aether current
    /// (<paramref name="aetherCurrentQuests"/> lists the quest). The zone story lines since Shadowbringers open with
    /// such a quest and continue with blue quests that unlock nothing of their own.
    /// </summary>
    public static bool UnlocksOnlyAetherCurrents(QuestRecord quest, CuratedData curated, IReadOnlySet<uint> aetherCurrentQuests, IReadOnlySet<uint> nonCurrentUnlockQuests)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(curated);
        ArgumentNullException.ThrowIfNull(aetherCurrentQuests);
        ArgumentNullException.ThrowIfNull(nonCurrentUnlockQuests);

        if (curated.SystemUnlocks.ContainsKey(quest.RowId)
            || curated.DutyUnlocks.ContainsKey(quest.RowId)
            || nonCurrentUnlockQuests.Contains(quest.RowId))
        {
            return false;
        }

        var rewards = quest.Rewards;
        for (var i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (IsUnlockKind(reward.Kind) && reward.Kind != RewardKind.AetherCurrent)
            {
                return false;
            }

            if (IsFeatureReward(reward) && reward.Kind == RewardKind.Other && !aetherCurrentQuests.Contains(quest.RowId))
            {
                return false;
            }
        }

        return true;
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
