using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// One split between Rewards and Unlocks (<see cref="RewardSplit"/>), over the installed game: no Rewards surface (the
/// table's Rewards column, the detail pane's Rewards tiles) draws an unlock-class thing, nothing is on both surfaces,
/// and every entry of <see cref="QuestRecord.Rewards"/> lands on exactly one of them (a currency stays in Rewards).
/// The surfaces are rebuilt here as the UI draws them, and the matching of a reward to its unlock row is written out on
/// its own rather than through <see cref="UnlockRewards"/>, so the two keep each other honest.
/// </summary>
public sealed class RewardUnlockSplitTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const int MaxRewardIcons = 4;
    private const uint IntoACopperHell = 66196;
    private const uint IntoTheAery = 67170;
    private const uint HalloHalatali = 66233;
    private const uint Halatali = 7;
    private const uint PaladinsPledge = 66591;
    private const uint SpiritsWithin = 29;
    private const uint Paladin = 19;
    private const uint DivineIntervention = 67133;
    private const uint CloseToHomeLancer = 65621;
    private const uint Lancer = 4;

    private QuestUnlocks Index => fixture.Unlocks;

    private QuestCatalog Catalog => fixture.Catalog;

    [GameDataFact]
    public void No_Rewards_surface_draws_an_unlock()
    {
        // 1.12.2 drew every reward as a tile; the split draws only what the character keeps.
        var before = new Dictionary<string, int>(StringComparer.Ordinal);
        var beforeQuests = 0;
        var afterQuests = 0;
        foreach (var quest in Catalog.All)
        {
            var unlockClass = quest.Rewards.Where(r => !KeptByHand(r)).ToList();
            beforeQuests += unlockClass.Count > 0 ? 1 : 0;
            foreach (var reward in unlockClass)
            {
                var key = reward.Kind == RewardKind.Other ? $"Other '{reward.Name}'" : reward.Kind.ToString();
                before[key] = before.GetValueOrDefault(key) + 1;
            }

            var drawn = Tiles(quest).Concat(Column(quest)).ToList();
            if (drawn.Any(r => !KeptByHand(r) || RewardSplit.Of(r) != RewardOrUnlock.Reward))
            {
                afterQuests++;
            }
        }

        output.WriteLine($"quests whose Rewards drew an unlock-class entry: {beforeQuests} in 1.12.2, {afterQuests} now");
        foreach (var (kind, count) in before.OrderByDescending(k => k.Value))
        {
            output.WriteLine($"   {count,5} {kind}");
        }

        Assert.True(beforeQuests > 300, $"the measure's floor fell: {beforeQuests} quests rewarded an unlock-class entry");
        Assert.Equal(0, afterQuests);
    }

    [GameDataFact]
    public void Every_reward_lands_in_exactly_one_section()
    {
        var misfiled = new List<string>();
        var currencies = 0;
        var unnamed = 0;
        foreach (var quest in Catalog.All)
        {
            var unlocks = Index.For(quest.RowId);
            var tiles = Tiles(quest).ToList();
            foreach (var reward in quest.Rewards)
            {
                // A slot with neither name nor icon (an unresolved Other, a solo instance with no duty: A Pup No Longer)
                // names nothing either section could draw.
                if (reward.Name.Length == 0 && reward.Icon == 0 && reward.ItemId == 0)
                {
                    unnamed++;
                    continue;
                }

                currencies += reward.Kind == RewardKind.Other && reward.ItemId != 0 ? 1 : 0;
                var inRewards = tiles.Contains(reward);
                var inUnlocks = unlocks.Any(row => Names(reward, row));
                if (inRewards == inUnlocks)
                {
                    misfiled.Add($"{quest.RowId} {quest.Name}: {reward.Kind} {reward.Id} '{reward.Name}' {(inRewards ? "in both" : "in neither")}");
                }
                else if (inRewards != KeptByHand(reward))
                {
                    misfiled.Add($"{quest.RowId} {quest.Name}: {reward.Kind} {reward.Id} '{reward.Name}' in the wrong section");
                }
            }
        }

        output.WriteLine($"currency rewards (kept in Rewards): {currencies}; slots that name nothing: {unnamed}");
        Assert.True(currencies > 0, "no currency reward was read");
        Assert.True(misfiled.Count == 0, "rewards not in exactly one section:\n" + string.Join('\n', misfiled.Take(40)));
    }

    [GameDataFact]
    public void Nothing_is_in_both_sections()
    {
        var both = new List<string>();
        var extras = 0;
        foreach (var quest in Catalog.All)
        {
            var unlocks = Index.For(quest.RowId);
            foreach (var row in unlocks)
            {
                // An Unlocks row is never a thing kept: an emote, a collectable, a title belongs to Rewards.
                if (RewardSplit.Of(row.Target) == RewardOrUnlock.Reward || row.InRewards)
                {
                    both.Add($"{quest.RowId} {quest.Name}: Unlocks draws {row.Target} '{row.Name}'");
                }
            }

            foreach (var reward in Tiles(quest))
            {
                if (unlocks.FirstOrDefault(row => Names(reward, row) || UnlockRewards.Same(reward, row)) is { } row)
                {
                    both.Add($"{quest.RowId} {quest.Name}: {reward.Kind} '{reward.Name}' is a tile and the {row.Target} row '{row.Name}'");
                }
            }

            foreach (var extra in Index.ExtraRewards(quest.RowId))
            {
                extras++;
                Assert.Equal(RewardOrUnlock.Reward, RewardSplit.Of(extra.Target));
                Assert.NotNull(extra.Reward);
                Assert.DoesNotContain(quest.Rewards, r => UnlockRewards.Same(r, extra));
            }
        }

        output.WriteLine($"reward-class rows no reward slot carries, drawn as tiles: {extras}");
        Assert.True(both.Count == 0, "in both sections:\n" + string.Join('\n', both.Take(40)));
    }

    [GameDataFact]
    public void Every_named_other_reward_is_sorted_on_purpose()
    {
        // A QuestRewardOther row a patch adds fails here until RewardSplit lists it as kept or as access.
        var seen = Catalog.All
            .SelectMany(q => q.Rewards)
            .Where(r => r.Kind == RewardKind.Other && r.ItemId == 0 && r.Name.Length > 0)
            .GroupBy(r => r.Id)
            .ToList();
        foreach (var group in seen.OrderBy(g => g.Key))
        {
            output.WriteLine($"   QuestRewardOther {group.Key,3} '{group.First().Name}' x{group.Count()}: {RewardSplit.Of(group.First())}");
        }

        Assert.All(seen, g => Assert.True(RewardSplit.KeptOtherRewards.Contains(g.Key) || RewardSplit.OpenedOtherRewards.Contains(g.Key), $"QuestRewardOther {g.Key} '{g.First().Name}' is not sorted"));
        Assert.Empty(RewardSplit.KeptOtherRewards.Intersect(RewardSplit.OpenedOtherRewards));
    }

    [GameDataFact]
    public void Actions_in_Unlocks_wear_their_icons()
    {
        var bare = new List<string>();
        var actions = 0;
        foreach (var quest in Catalog.All)
        {
            foreach (var row in Index.For(quest.RowId))
            {
                if (row.Group != UnlockGroup.ActionEmote)
                {
                    continue;
                }

                actions++;
                if (row.Icon == 0)
                {
                    bare.Add($"{quest.RowId} {quest.Name}: {row.Target} {row.TargetId} '{row.Name}'");
                }
            }
        }

        output.WriteLine($"action rows: {actions}, without an icon: {bare.Count}");
        Assert.True(actions > 300, $"action rows: {actions}");
        Assert.True(bare.Count == 0, "action rows without an icon:\n" + string.Join('\n', bare.Take(40)));
    }

    [GameDataFact]
    public void The_samples_read_as_the_split_says()
    {
        // No main scenario quest carries its dungeon in a reward slot; the 48 Instance rewards are side, primal and raid quests.
        Assert.DoesNotContain(Catalog.All, q => FeaturePresets.IsMainScenario(q) && q.Rewards.Any(r => r.Kind == RewardKind.Instance));
        var msq = Catalog.GetByRowId(IntoACopperHell)!;
        var current = Catalog.GetByRowId(IntoTheAery)!;
        foreach (var rowId in new[] { IntoACopperHell, HalloHalatali, IntoTheAery, PaladinsPledge, DivineIntervention, CloseToHomeLancer })
        {
            Dump(rowId);
        }

        // An MSQ quest that opens a dungeon: the duty is an Unlocks row with its icon, and never a Rewards tile.
        Assert.True(FeaturePresets.IsMainScenario(msq));
        Assert.Contains(Index.For(IntoACopperHell), e => e.Target == UnlockTarget.Dungeon && e.Name == "Copperbell Mines" && e.Icon != 0);
        Assert.DoesNotContain(Tiles(msq), r => r.Kind is RewardKind.Instance or RewardKind.DutyUnlock);

        // A side quest whose reward slot opens a dungeon (a 1.12.2 tile): now only its Unlocks row.
        var halatali = Catalog.GetByRowId(HalloHalatali)!;
        Assert.Contains(halatali.Rewards, r => r.Kind == RewardKind.Instance && r.Id == Halatali);
        Assert.DoesNotContain(Tiles(halatali), r => r.Kind == RewardKind.Instance);
        Assert.Contains(Index.For(HalloHalatali), e => e.Target == UnlockTarget.Dungeon && e.Name == "Halatali" && e.Icon != 0);

        // A quest that grants an aether current: flying in its zone is an Unlocks row; the tile is gone, the mount's
        // whistle stays a tile, and the Aery stays an Unlocks row.
        Assert.Contains(current.Rewards, r => r.Kind == RewardKind.Other && r.Id == UnlockRewards.AetherCurrentOtherReward);
        Assert.DoesNotContain(Tiles(current), r => r.Kind == RewardKind.Other && r.Id == UnlockRewards.AetherCurrentOtherReward);
        Assert.Contains(Tiles(current), r => r.Name == "Manacutter Key");
        Assert.Contains(Index.For(IntoTheAery), e => e.Target == UnlockTarget.Dungeon && e.Name == "The Aery");
        var flying = Assert.Single(Index.For(current.RowId), e => e.Target == UnlockTarget.Flying);
        Assert.Equal(RewardKind.AetherCurrent, flying.Reward);
        Assert.Equal(QuestUnlocks.AetherCurrentIcon, flying.Icon);

        // A job quest that teaches an action: the job and the action are Unlocks rows with their icons; the soul
        // crystal it hands over is a Rewards tile.
        var pledge = Catalog.GetByRowId(PaladinsPledge)!;
        Assert.Contains(Index.For(PaladinsPledge), e => e.Target == UnlockTarget.Action && e.TargetId == SpiritsWithin && e.Icon != 0);
        Assert.Contains(Index.For(PaladinsPledge), e => e.Target == UnlockTarget.Job && e.TargetId == Paladin && e.Icon == QuestUnlocks.JobIconBase + Paladin);
        Assert.Equal(["Soul of the Paladin"], Tiles(pledge).Select(r => r.Name).ToArray());

        // A class quest's class: an Unlocks row, and no Rewards tile left.
        Assert.Contains(Index.For(CloseToHomeLancer), e => e.Target == UnlockTarget.Job && e.TargetId == Lancer);
        Assert.Empty(Tiles(Catalog.GetByRowId(CloseToHomeLancer)!));

        // Divine Intervention: the Aether Compass is one Unlocks row with an icon; the Black Chocobo whistle stays a tile.
        var compass = Assert.Single(Index.For(DivineIntervention), e => e.Name.Contains("Aether Compass", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(0u, compass.Icon);
        Assert.Contains(Tiles(Catalog.GetByRowId(DivineIntervention)!), r => r.Name == "Black Chocobo Whistle");
        Assert.DoesNotContain(Index.For(DivineIntervention), e => e.Target == UnlockTarget.Mount);
    }

    /// <summary>The detail pane's Rewards tiles: the quest's kept rewards, then the reward-class rows no slot carries.</summary>
    private IEnumerable<RewardRef> Tiles(QuestRecord quest)
    {
        foreach (var reward in quest.Rewards)
        {
            if (RewardSplit.IsReward(reward))
            {
                yield return reward;
            }
        }

        foreach (var extra in Index.ExtraRewards(quest.RowId))
        {
            yield return new RewardRef(extra.Reward!.Value, extra.TargetId, extra.ItemId, 1, extra.Name, extra.Icon);
        }
    }

    /// <summary>The table's Rewards column: the first four kept rewards that have an icon.</summary>
    private static IEnumerable<RewardRef> Column(QuestRecord quest) =>
        quest.Rewards.Where(r => r.Icon != 0 && RewardSplit.IsReward(r)).Take(MaxRewardIcons);

    /// <summary>The decision written out by hand: what the character receives and keeps, else access.</summary>
    private static bool KeptByHand(RewardRef reward) => reward.Kind switch
    {
        RewardKind.Instance or RewardKind.DutyUnlock or RewardKind.AetherCurrent or RewardKind.ClassJob or RewardKind.SystemUnlock
            or RewardKind.Action or RewardKind.GeneralAction or RewardKind.Trait or RewardKind.BlueMageSpell => false,
        RewardKind.Other when reward.ItemId == 0 && reward.Name.Length > 0 => reward.Name.StartsWith("Soul of the ", StringComparison.Ordinal),
        _ => true,
    };

    /// <summary>Whether the Unlocks row is the reward's thing, by kind.</summary>
    private static bool Names(RewardRef reward, UnlockEntry row) => reward.Kind switch
    {
        RewardKind.Instance or RewardKind.DutyUnlock => row.Group == UnlockGroup.Duty && Key(row.Name) == Key(reward.Name),
        RewardKind.ClassJob => row.Target == UnlockTarget.Job && row.TargetId == reward.Id,
        RewardKind.Action => row.Target == UnlockTarget.Action && row.TargetId == reward.Id,
        RewardKind.GeneralAction => row.Target == UnlockTarget.GeneralAction && row.TargetId == reward.Id,
        RewardKind.Emote => row.Target == UnlockTarget.Emote && row.TargetId == reward.Id,
        RewardKind.Other when reward.ItemId != 0 => false,
        RewardKind.Other when reward.Id == UnlockRewards.AetherCurrentOtherReward => row.Target == UnlockTarget.Flying && row.Reward == RewardKind.AetherCurrent,
        RewardKind.Other => reward.Name.Length > 0 && Key(row.Name) == Key(reward.Name),
        _ => (reward.ItemId != 0 && row.ItemId == reward.ItemId) || (reward.Name.Length > 0 && Key(row.Name) == Key(reward.Name)),
    };

    private static string Key(string name)
    {
        var key = name.Trim();
        var floors = key.IndexOf(" (Floors ", StringComparison.Ordinal);
        if (floors > 0)
        {
            key = key[..floors];
        }

        return (key.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? key[4..] : key).ToLowerInvariant();
    }

    private void Dump(uint rowId)
    {
        var quest = Catalog.GetByRowId(rowId)!;
        output.WriteLine($"## {rowId} {quest.Name}");
        output.WriteLine($"   1.12.2 Rewards: {string.Join(" · ", quest.Rewards.Select(r => $"{r.Kind} '{r.Name}'"))}");
        output.WriteLine($"   now Rewards:    {string.Join(" · ", Tiles(quest).Select(r => $"{r.Kind} '{r.Name}'"))}");
        output.WriteLine($"   now Unlocks:    {string.Join(" · ", Index.For(rowId).Where(e => e.Target != UnlockTarget.NextQuest).Select(e => $"{e.Target} '{e.Name}' icon {e.Icon}"))}");
    }
}
