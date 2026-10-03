using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unlocks;

/// <summary>Which of the two quest sections a thing belongs to (<see cref="RewardSplit"/>).</summary>
public enum RewardOrUnlock : byte
{
    /// <summary>Something the character receives and keeps: drawn by the Rewards column and the Rewards tiles.</summary>
    Reward,

    /// <summary>Access or an ability the character gains: drawn by the Unlocks column, section and lines.</summary>
    Unlock,
}

/// <summary>
/// The one split between Rewards and Unlocks (owner request after 1.12.2: "Those need to be explicitly in one
/// section"). Every surface asks it, so nothing is drawn in both and nothing falls between them:
/// <list type="bullet">
/// <item><b>Rewards</b>: what the character receives and keeps: items, optional items, gear, gil and currencies, and
/// the collectables and glamour (mounts, minions, emotes, hairstyles, orchestrion rolls, Triple Triad cards, bardings,
/// fashion accessories, titles, achievements), and the seven soul crystals the 2.x job quests hand over as
/// <c>Quest.OtherReward</c>. Emotes stay here: the game and Moonlit count them as collectables.</item>
/// <item><b>Unlocks</b>: access and abilities: areas, aetherytes, duties of every kind (an instance or a duty
/// unlock), flying (an aether current), features and systems (a system unlock, a named <c>Quest.OtherReward</c> such
/// as Wondrous Tails or Spearfishing), jobs and classes, and actions (job actions, general actions, traits, blue
/// magic).</item>
/// </list>
/// The table's Rewards column and the detail pane's Rewards tiles draw only <see cref="RewardOrUnlock.Reward"/>
/// entries of <see cref="QuestRecord.Rewards"/>; the unlock index (<see cref="QuestUnlocks"/>) always draws the
/// <see cref="RewardOrUnlock.Unlock"/> ones and never a reward-class row (<see cref="UnlockEntry.InRewards"/>).
/// </summary>
public static class RewardSplit
{
    /// <summary>
    /// The <c>QuestRewardOther</c> rows that are things kept rather than access: the soul crystals of the 2.x jobs
    /// (Soul of the Paladin … Soul of the Black Mage), each the job quest's crystal, whose job the Unlocks job row names.
    /// Every other named row (Aether Current, Collectable Action, Specialist Action, Aether Compass, Yo-kai Medallium,
    /// Wondrous Tails, Spearfishing) opens a feature or an ability.
    /// </summary>
    public static readonly FrozenSet<uint> KeptOtherRewards = new uint[] { 10, 11, 12, 13, 14, 15, 16 }.ToFrozenSet();

    /// <summary>
    /// The <c>QuestRewardOther</c> rows a quest hands over as access, as the game data held them in 2026.10; the
    /// game-data tests fail on a row in neither this set nor <see cref="KeptOtherRewards"/>, so a new one is sorted on
    /// purpose. An unlisted row reads as access (<see cref="Of(RewardRef)"/>).
    /// </summary>
    public static readonly FrozenSet<uint> OpenedOtherRewards = new uint[] { UnlockRewards.AetherCurrentOtherReward, 3, 4, 5, 6, 7, 8 }.ToFrozenSet();

    /// <summary>
    /// The section a reward kind belongs to. <see cref="RewardKind.Other"/> reads as a reward here (a currency, an
    /// unresolved slot); <see cref="Of(RewardRef)"/> sorts a named <c>Quest.OtherReward</c> on its row. Every kind is
    /// listed: a kind added to <see cref="RewardKind"/> without a line here throws, and the tests name it.
    /// </summary>
    public static RewardOrUnlock Of(RewardKind kind) => kind switch
    {
        RewardKind.Item => RewardOrUnlock.Reward,
        RewardKind.OptionalItem => RewardOrUnlock.Reward,
        RewardKind.ArtifactGear => RewardOrUnlock.Reward,
        RewardKind.Other => RewardOrUnlock.Reward,
        RewardKind.Emote => RewardOrUnlock.Reward,
        RewardKind.Mount => RewardOrUnlock.Reward,
        RewardKind.Minion => RewardOrUnlock.Reward,
        RewardKind.Orchestrion => RewardOrUnlock.Reward,
        RewardKind.TripleTriadCard => RewardOrUnlock.Reward,
        RewardKind.Ornament => RewardOrUnlock.Reward,
        RewardKind.Barding => RewardOrUnlock.Reward,
        RewardKind.Hairstyle => RewardOrUnlock.Reward,
        RewardKind.Achievement => RewardOrUnlock.Reward,
        RewardKind.Title => RewardOrUnlock.Reward,
        RewardKind.Action => RewardOrUnlock.Unlock,
        RewardKind.GeneralAction => RewardOrUnlock.Unlock,
        RewardKind.Trait => RewardOrUnlock.Unlock,
        RewardKind.BlueMageSpell => RewardOrUnlock.Unlock,
        RewardKind.Instance => RewardOrUnlock.Unlock,
        RewardKind.DutyUnlock => RewardOrUnlock.Unlock,
        RewardKind.ClassJob => RewardOrUnlock.Unlock,
        RewardKind.AetherCurrent => RewardOrUnlock.Unlock,
        RewardKind.SystemUnlock => RewardOrUnlock.Unlock,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "RewardSplit has no line for this reward kind"),
    };

    /// <summary>
    /// The section one of a quest's own rewards belongs to: its kind's, except that a named <c>Quest.OtherReward</c>
    /// (no item id, a name) is access unless it is one of the <see cref="KeptOtherRewards"/>. A currency (an item id
    /// under Other) and an unresolved slot (no name) stay rewards.
    /// </summary>
    public static RewardOrUnlock Of(RewardRef reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        if (reward.Kind == RewardKind.Other && reward.ItemId == 0 && reward.Name.Length > 0)
        {
            return KeptOtherRewards.Contains(reward.Id) ? RewardOrUnlock.Reward : RewardOrUnlock.Unlock;
        }

        return Of(reward.Kind);
    }

    /// <summary>The section a unique-reward entry (a Moonlit row) belongs to: its kind's.</summary>
    public static RewardOrUnlock Of(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Of(entry.Kind);
    }

    /// <summary>
    /// The section an unlock row's target belongs to: emotes and the collectables are rewards; areas, aetherytes,
    /// duties, jobs, flying, features, actions and next quests are unlocks. Every target is listed, as for
    /// <see cref="Of(RewardKind)"/>.
    /// </summary>
    public static RewardOrUnlock Of(UnlockTarget target) => target switch
    {
        UnlockTarget.Zone => RewardOrUnlock.Unlock,
        UnlockTarget.WorldMap => RewardOrUnlock.Unlock,
        UnlockTarget.Aetheryte => RewardOrUnlock.Unlock,
        UnlockTarget.AethernetShard => RewardOrUnlock.Unlock,
        UnlockTarget.Dungeon => RewardOrUnlock.Unlock,
        UnlockTarget.Trial => RewardOrUnlock.Unlock,
        UnlockTarget.NormalRaid => RewardOrUnlock.Unlock,
        UnlockTarget.AllianceRaid => RewardOrUnlock.Unlock,
        UnlockTarget.FieldOperation => RewardOrUnlock.Unlock,
        UnlockTarget.OtherDuty => RewardOrUnlock.Unlock,
        UnlockTarget.Job => RewardOrUnlock.Unlock,
        UnlockTarget.Flying => RewardOrUnlock.Unlock,
        UnlockTarget.System => RewardOrUnlock.Unlock,
        UnlockTarget.Action => RewardOrUnlock.Unlock,
        UnlockTarget.Trait => RewardOrUnlock.Unlock,
        UnlockTarget.GeneralAction => RewardOrUnlock.Unlock,
        UnlockTarget.BlueMageSpell => RewardOrUnlock.Unlock,
        UnlockTarget.NextQuest => RewardOrUnlock.Unlock,
        UnlockTarget.Emote => RewardOrUnlock.Reward,
        UnlockTarget.Mount => RewardOrUnlock.Reward,
        UnlockTarget.Minion => RewardOrUnlock.Reward,
        UnlockTarget.Orchestrion => RewardOrUnlock.Reward,
        UnlockTarget.Card => RewardOrUnlock.Reward,
        UnlockTarget.Hairstyle => RewardOrUnlock.Reward,
        UnlockTarget.Barding => RewardOrUnlock.Reward,
        UnlockTarget.Ornament => RewardOrUnlock.Reward,
        UnlockTarget.Title => RewardOrUnlock.Reward,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "RewardSplit has no line for this unlock target"),
    };

    /// <summary>Whether the Rewards column and tiles draw this reward of the quest's own list.</summary>
    public static bool IsReward(RewardRef reward) => Of(reward) == RewardOrUnlock.Reward;

    /// <summary>Whether the reward belongs to Unlocks (the index draws it there) rather than to the Rewards tiles.</summary>
    public static bool IsUnlock(RewardRef reward) => Of(reward) == RewardOrUnlock.Unlock;
}
