using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// No unlock repeats a reward, over the installed game: for every quest of the catalog, no row any unlock surface draws
/// (<see cref="QuestUnlocks.For"/>, the table's Unlocks column, the detail pane's Unlocks section, the tooltips) is a
/// thing the quest's own Rewards already show (the Rewards column and tiles, both drawn from
/// <see cref="QuestRecord.Rewards"/>), and no quest's rows name one thing twice. The matching here is written out on
/// its own rather than through <see cref="UnlockRewards"/>, so the two keep each other honest.
/// </summary>
public sealed class UnlockRewardDuplicateTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint ConfederateConsternation = 68016;
    private const uint EasternBow = 154;
    private const uint CloseToHomeLancer = 65621;
    private const uint Lancer = 4;
    private const uint IntoTheAery = 67170;
    private const uint Manacutter = 55;
    private const uint ActingThePart = 66538;
    private const uint HalloHalatali = 66233;
    private const uint DivineIntervention = 67133;
    private const uint ATripToTheMoon = 69932;

    private QuestUnlocks Index => fixture.Unlocks;

    [GameDataFact]
    public void No_shown_unlock_repeats_a_reward_of_its_quest()
    {
        var before = Duplicates(Index.IncludingRewards);
        var after = Duplicates(Index.For);
        output.WriteLine($"quests whose unlocks repeat a reward: {before.Count} counting every row, {after.Count} shown");
        foreach (var (kind, count) in before.SelectMany(q => q.Value).GroupBy(d => d.Kind).Select(g => (g.Key, g.Count())).OrderByDescending(k => k.Item2))
        {
            output.WriteLine($"   {count,5} {kind}");
        }

        Assert.True(before.Count > 300, $"the rule's floor fell: {before.Count} quests repeat a reward before it");
        Assert.True(after.Count == 0, "shown unlocks that repeat a reward:\n" + string.Join('\n', after.SelectMany(q => q.Value).Take(40).Select(d => d.Text)));
    }

    [GameDataFact]
    public void No_quest_shows_one_unlock_twice()
    {
        var twice = new List<string>();
        foreach (var quest in fixture.Catalog.All)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<(UnlockTarget, uint)>();
            foreach (var entry in Index.For(quest.RowId))
            {
                if (entry.Target == UnlockTarget.NextQuest)
                {
                    continue;
                }

                var byId = entry.TargetId == 0 || ids.Add((entry.Target, entry.TargetId));
                var byName = keys.Add($"{entry.Group}|{Key(entry.Name)}");
                if (!byId || !byName)
                {
                    twice.Add($"{quest.RowId} {quest.Name}: {entry.Target} {entry.TargetId} '{entry.Name}'");
                }
            }
        }

        Assert.True(twice.Count == 0, "unlocks shown twice:\n" + string.Join('\n', twice.Take(40)));
    }

    [GameDataFact]
    public void Rewards_stay_in_Rewards_and_the_rest_stays_in_Unlocks()
    {
        foreach (var rowId in new[] { ConfederateConsternation, CloseToHomeLancer, IntoTheAery, ActingThePart, HalloHalatali, DivineIntervention, ATripToTheMoon })
        {
            Dump(rowId);
        }

        // Confederate Consternation rewards the Eastern Bow emote: a Rewards tile, no unlock row; Onokoro stays.
        var confederate = fixture.Catalog.GetByRowId(ConfederateConsternation)!;
        Assert.Contains(confederate.Rewards, r => r.Kind == RewardKind.Emote && r.Id == EasternBow);
        Assert.Contains(Index.IncludingRewards(ConfederateConsternation), e => e.Target == UnlockTarget.Emote && e.TargetId == EasternBow && e.InRewards);
        Assert.DoesNotContain(Index.For(ConfederateConsternation), e => e.Target == UnlockTarget.Emote);
        Assert.Contains(Index.For(ConfederateConsternation), e => e.Target == UnlockTarget.Aetheryte && e.Name == "Onokoro");

        // A class quest rewards its class: the tile wears the job icon, and Unlocks says nothing of it.
        var lancer = Assert.Single(fixture.Catalog.GetByRowId(CloseToHomeLancer)!.Rewards, r => r.Kind == RewardKind.ClassJob);
        Assert.Equal(Lancer, lancer.Id);
        Assert.Equal(QuestUnlocks.JobIconBase + Lancer, lancer.Icon);
        Assert.Contains(Index.IncludingRewards(CloseToHomeLancer), e => e.Target == UnlockTarget.Job && e.InRewards);
        Assert.DoesNotContain(Index.For(CloseToHomeLancer), e => e.Target == UnlockTarget.Job);

        // Into the Aery rewards the Manacutter's key and an aether current; the Aery itself is no reward and stays.
        var aery = Index.For(IntoTheAery);
        Assert.DoesNotContain(aery, e => e.Target == UnlockTarget.Mount);
        Assert.Contains(Index.IncludingRewards(IntoTheAery), e => e.Target == UnlockTarget.Mount && e.TargetId == Manacutter && e.InRewards);
        Assert.DoesNotContain(aery, e => e.Target is UnlockTarget.Flying or UnlockTarget.System);
        Assert.Contains(aery, e => e.Target == UnlockTarget.Dungeon && e.Name == "The Aery");

        // An emote reward.
        Assert.Contains(fixture.Catalog.GetByRowId(ActingThePart)!.Rewards, r => r.Kind == RewardKind.Emote && r.Name == "Imperial Salute");
        Assert.DoesNotContain(Index.For(ActingThePart), e => e.Target == UnlockTarget.Emote);

        // A duty the quest's reward opens: a tile with the dungeon icon, no Duties row.
        var halatali = Assert.Single(fixture.Catalog.GetByRowId(HalloHalatali)!.Rewards, r => r.Kind == RewardKind.Instance);
        Assert.NotEqual(0u, halatali.Icon);
        Assert.Contains(Index.IncludingRewards(HalloHalatali), e => e.Group == UnlockGroup.Duty && e.Name == "Halatali" && e.InRewards);
        Assert.DoesNotContain(Index.For(HalloHalatali), e => e.Group == UnlockGroup.Duty);

        // The Aether Compass reward is neither a feature row nor an action row.
        Assert.DoesNotContain(Index.For(DivineIntervention), e => e.Name.Contains("Aether Compass", StringComparison.OrdinalIgnoreCase));

        // A world map and its own place name are one row.
        var moon = Index.For(ATripToTheMoon).Where(e => e.Target == UnlockTarget.WorldMap).Select(e => e.Name).ToList();
        Assert.Equal(moon.Distinct().Count(), moon.Count);
    }

    /// <summary>Per quest, the shown rows <paramref name="rows"/> holds that are one of the quest's rewards.</summary>
    private Dictionary<uint, List<(string Kind, string Text)>> Duplicates(Func<uint, IReadOnlyList<UnlockEntry>> rows)
    {
        var found = new Dictionary<uint, List<(string, string)>>();
        foreach (var quest in fixture.Catalog.All)
        {
            foreach (var entry in rows(quest.RowId))
            {
                if (entry.Group is UnlockGroup.Area or UnlockGroup.Aetheryte or UnlockGroup.NextQuest)
                {
                    continue;
                }

                foreach (var reward in quest.Rewards)
                {
                    if (!IsReward(reward, entry))
                    {
                        continue;
                    }

                    if (!found.TryGetValue(quest.RowId, out var list))
                    {
                        list = [];
                        found[quest.RowId] = list;
                    }

                    list.Add(($"{entry.Target} <- {reward.Kind}", $"{quest.RowId} {quest.Name}: {entry.Target} '{entry.Name}' <- {reward.Kind} '{reward.Name}'"));
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>The same thing: one item, one sheet row of one kind, the quest's aether current, or one name.</summary>
    private static bool IsReward(RewardRef reward, UnlockEntry entry)
    {
        if (reward.Kind == RewardKind.Other && reward.ItemId != 0)
        {
            return false;
        }

        if (reward.ItemId != 0 && reward.ItemId == entry.ItemId)
        {
            return true;
        }

        if (reward.Id != 0 && reward.Id == entry.TargetId && entry.Reward == reward.Kind)
        {
            return true;
        }

        if (reward.Kind == RewardKind.Other && reward.Icon == QuestUnlocks.AetherCurrentIcon && entry.Target == UnlockTarget.Flying && entry.Icon == QuestUnlocks.AetherCurrentIcon)
        {
            return true;
        }

        return reward.Name.Length > 0 && Key(reward.Name) == Key(entry.Name);
    }

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
        var quest = fixture.Catalog.GetByRowId(rowId);
        output.WriteLine($"## {rowId} {quest?.Name}");
        foreach (var reward in quest?.Rewards ?? [])
        {
            output.WriteLine($"   reward {reward.Kind,-14} {reward.Id,7} {reward.Name} icon {reward.Icon}");
        }

        foreach (var entry in Index.IncludingRewards(rowId))
        {
            output.WriteLine($"   unlock {entry.Target,-14} {entry.TargetId,7} {entry.Name}{(entry.InRewards ? " [in Rewards, hidden]" : string.Empty)}");
        }
    }
}
