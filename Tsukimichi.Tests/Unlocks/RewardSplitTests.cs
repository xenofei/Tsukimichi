using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Tests.Unlocks;

/// <summary>
/// The one split between Rewards and Unlocks (<see cref="RewardSplit"/>), over every reward kind and every unlock
/// target: a kind or target added without a line in the classifier, or in the tables here, fails.
/// </summary>
public class RewardSplitTests
{
    private const RewardOrUnlock R = RewardOrUnlock.Reward;
    private const RewardOrUnlock U = RewardOrUnlock.Unlock;

    /// <summary>The decision, written out: what is received and kept, and what opens access or an ability.</summary>
    private static readonly Dictionary<RewardKind, RewardOrUnlock> Kinds = new()
    {
        [RewardKind.Item] = R,
        [RewardKind.OptionalItem] = R,
        [RewardKind.ArtifactGear] = R,
        [RewardKind.Other] = R,
        [RewardKind.Emote] = R,
        [RewardKind.Mount] = R,
        [RewardKind.Minion] = R,
        [RewardKind.Orchestrion] = R,
        [RewardKind.TripleTriadCard] = R,
        [RewardKind.Ornament] = R,
        [RewardKind.Barding] = R,
        [RewardKind.Hairstyle] = R,
        [RewardKind.Achievement] = R,
        [RewardKind.Title] = R,
        [RewardKind.Action] = U,
        [RewardKind.GeneralAction] = U,
        [RewardKind.Trait] = U,
        [RewardKind.BlueMageSpell] = U,
        [RewardKind.Instance] = U,
        [RewardKind.DutyUnlock] = U,
        [RewardKind.ClassJob] = U,
        [RewardKind.AetherCurrent] = U,
        [RewardKind.SystemUnlock] = U,
    };

    private static readonly Dictionary<UnlockTarget, RewardOrUnlock> Targets = new()
    {
        [UnlockTarget.Zone] = U,
        [UnlockTarget.WorldMap] = U,
        [UnlockTarget.Aetheryte] = U,
        [UnlockTarget.AethernetShard] = U,
        [UnlockTarget.Dungeon] = U,
        [UnlockTarget.Trial] = U,
        [UnlockTarget.NormalRaid] = U,
        [UnlockTarget.AllianceRaid] = U,
        [UnlockTarget.FieldOperation] = U,
        [UnlockTarget.OtherDuty] = U,
        [UnlockTarget.Job] = U,
        [UnlockTarget.Flying] = U,
        [UnlockTarget.System] = U,
        [UnlockTarget.Action] = U,
        [UnlockTarget.Trait] = U,
        [UnlockTarget.GeneralAction] = U,
        [UnlockTarget.BlueMageSpell] = U,
        [UnlockTarget.NextQuest] = U,
        [UnlockTarget.Emote] = R,
        [UnlockTarget.Mount] = R,
        [UnlockTarget.Minion] = R,
        [UnlockTarget.Orchestrion] = R,
        [UnlockTarget.Card] = R,
        [UnlockTarget.Hairstyle] = R,
        [UnlockTarget.Barding] = R,
        [UnlockTarget.Ornament] = R,
        [UnlockTarget.Title] = R,
    };

    [Fact]
    public void Every_reward_kind_has_its_section()
    {
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            Assert.True(Kinds.TryGetValue(kind, out var expected), $"RewardKind.{kind} is not in the decision table");
            Assert.Equal(expected, RewardSplit.Of(kind));
            Assert.Equal(expected, RewardSplit.Of(new UniqueRewardEntry(1, kind, 1, 0, "x", Confidence.Static, "test")));
        }

        Assert.Equal(Enum.GetValues<RewardKind>().Length, Kinds.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => RewardSplit.Of((RewardKind)250));
    }

    [Fact]
    public void Every_unlock_target_has_its_section()
    {
        foreach (var target in Enum.GetValues<UnlockTarget>())
        {
            Assert.True(Targets.TryGetValue(target, out var expected), $"UnlockTarget.{target} is not in the decision table");
            Assert.Equal(expected, RewardSplit.Of(target));
        }

        Assert.Equal(Enum.GetValues<UnlockTarget>().Length, Targets.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => RewardSplit.Of((UnlockTarget)250));

        // A group is all of one section: the Unlocks section never draws a kept thing's group.
        foreach (var group in UnlockTargets.Groups)
        {
            var sections = Enum.GetValues<UnlockTarget>().Where(t => UnlockTargets.GroupOf(t) == group && t != UnlockTarget.Emote).Select(RewardSplit.Of).Distinct();
            Assert.Single(sections);
        }
    }

    [Fact]
    public void A_named_other_reward_is_access_unless_it_is_a_soul_crystal()
    {
        Assert.Equal(U, RewardSplit.Of(new RewardRef(RewardKind.Other, UnlockRewards.AetherCurrentOtherReward, 0, 1, "Aether Current", 60033)));
        Assert.Equal(U, RewardSplit.Of(new RewardRef(RewardKind.Other, 7, 0, 1, "Wondrous Tails", 25987)));
        Assert.Equal(U, RewardSplit.Of(new RewardRef(RewardKind.Other, 99, 0, 1, "A new feature", 0)));
        Assert.Equal(R, RewardSplit.Of(new RewardRef(RewardKind.Other, 10, 0, 1, "Soul of the Paladin", 26003)));

        // A currency (an item id under Other) and an unresolved slot (no name) stay rewards.
        Assert.Equal(R, RewardSplit.Of(new RewardRef(RewardKind.Other, 28, 28, 5, "Allagan Tomestone of Poetics", 65023)));
        Assert.Equal(R, RewardSplit.Of(new RewardRef(RewardKind.Other, 3, 0, 1, string.Empty, 0)));

        Assert.True(RewardSplit.IsReward(new RewardRef(RewardKind.Emote, 154, 0, 1, "Eastern Bow", 246315)));
        Assert.True(RewardSplit.IsUnlock(new RewardRef(RewardKind.Action, 29, 0, 1, "Spirits Within", 2503)));
        Assert.True(RewardSplit.IsUnlock(new RewardRef(RewardKind.Instance, 7, 0, 1, "Halatali", 61801)));
        Assert.Empty(RewardSplit.KeptOtherRewards.Intersect(RewardSplit.OpenedOtherRewards));
    }
}
