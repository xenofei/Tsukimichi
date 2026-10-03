using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// The one rule that keeps a reward out of Unlocks: an unlock the quest's own Rewards already show (the table's Rewards
/// column, the detail pane's Rewards tiles, both drawn from <see cref="QuestRecord.Rewards"/>) is not shown again on
/// any unlock surface. <see cref="QuestUnlocks"/> sets <see cref="UnlockEntry.InRewards"/> with it once per catalog,
/// <see cref="QuestUnlocks.For"/> and <see cref="UnlockView.Visible"/> leave those rows out, and a surface that lists
/// other rewards beside the unlocks (Moonlit's rows, the game panels' Moonlit lines) asks <see cref="Same(UniqueRewardEntry, UnlockEntry)"/>.
/// <para>A reward and an unlock row are the same thing when, in this order:</para>
/// <list type="number">
/// <item>they name the same item (a mount's whistle, a minion, an orchestrion roll, an emote's book);</item>
/// <item>they are the same sheet row of the same kind (an emote, an action, a general action, a class or job);</item>
/// <item>the reward is the quest's aether current (<c>QuestRewardOther</c> 2) and the row is flying a current opens;</item>
/// <item>failing ids, they carry the same name (<see cref="PlanDuties.NameKey"/>: case and a leading "the" aside) and
/// the row is something a quest can hand over (a duty, a feature, a job, an action, an emote, a collectable): an
/// instance reward and its duty, a named <c>Quest.OtherReward</c> and its feature ("Wondrous Tails"), a general action
/// and the curated feature it switches on ("Desynthesis"). Areas, aetherytes and next quests are never a reward.</item>
/// </list>
/// </summary>
public static class UnlockRewards
{
    /// <summary>The <c>QuestRewardOther</c> row "Aether Current": the reward tile that stands for flying in the zone.</summary>
    public const uint AetherCurrentOtherReward = 2;

    /// <summary>Whether any of <paramref name="rewards"/> (a quest's own) is the row's thing.</summary>
    public static bool Shown(IReadOnlyList<RewardRef> rewards, UnlockEntry entry)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(entry);
        foreach (var reward in rewards)
        {
            if (Same(reward, entry))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the quest's reward is the row's thing.</summary>
    public static bool Same(RewardRef reward, UnlockEntry entry)
    {
        ArgumentNullException.ThrowIfNull(reward);
        ArgumentNullException.ThrowIfNull(entry);

        // A currency (an item id under Other) or an unresolved slot (no name) stands for nothing a row names.
        if (reward.Kind == RewardKind.Other && reward.ItemId != 0)
        {
            return false;
        }

        if (reward.Kind == RewardKind.Other && reward.Id == AetherCurrentOtherReward && entry.Reward == RewardKind.AetherCurrent)
        {
            return true;
        }

        return Same(reward.Kind, reward.Id, reward.ItemId, reward.Name, entry);
    }

    /// <summary>Whether a unique-reward entry (a Moonlit row, a panel's Moonlit line) is the row's thing.</summary>
    public static bool Same(UniqueRewardEntry reward, UnlockEntry entry)
    {
        ArgumentNullException.ThrowIfNull(reward);
        ArgumentNullException.ThrowIfNull(entry);
        if (reward.Kind == RewardKind.AetherCurrent)
        {
            return entry.Target == UnlockTarget.Flying && SameName(CurrentZone(reward.RewardName), entry.Name);
        }

        return Same(reward.Kind, reward.RewardId, reward.ItemId, reward.RewardName, entry);
    }

    private static bool Same(RewardKind kind, uint id, uint itemId, string name, UnlockEntry entry)
    {
        if (entry.Group is UnlockGroup.Area or UnlockGroup.Aetheryte or UnlockGroup.NextQuest)
        {
            return false;
        }

        if (itemId != 0 && entry.ItemId == itemId)
        {
            return true;
        }

        if (id != 0 && entry.TargetId == id && entry.Reward == kind)
        {
            return true;
        }

        return SameName(name, entry.Name);
    }

    /// <summary>
    /// Whether two names name one thing as the rule reads them: case, a leading "the" and a deep dungeon's floor set
    /// aside ("the Palace of the Dead (Floors 1-10)" is "Palace of the Dead"). Two empty names are not the same.
    /// </summary>
    public static bool SameName(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var key = PlanDuties.NameKey(UnlockTags.ContentName(a));
        return key.Length > 0 && string.Equals(key, PlanDuties.NameKey(UnlockTags.ContentName(b)), StringComparison.Ordinal);
    }

    /// <summary>"Aether Current (Coerthas Western Highlands)" names the zone flying opens in.</summary>
    internal static string CurrentZone(string name)
    {
        var open = name.IndexOf('(', StringComparison.Ordinal);
        var close = name.LastIndexOf(')');
        return open >= 0 && close > open + 1 ? name[(open + 1)..close].Trim() : name;
    }
}
