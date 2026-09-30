using System.Text.RegularExpressions;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Unique;

/// <summary>The crediting rule on plain ids (<see cref="AchievementQuests"/>).</summary>
public class AchievementQuestsTests
{
    // 1 -> 2 -> 3 is a chain; 4 stands alone.
    private static readonly Dictionary<uint, uint[]> Previous = new()
    {
        [2] = [1],
        [3] = [2],
    };

    private static IEnumerable<uint> Prev(uint id) => Previous.GetValueOrDefault(id) ?? [];

    [Fact]
    public void A_relic_weapon_achievement_is_credited_to_no_quest()
    {
        Assert.Empty(AchievementQuests.CreditedQuests(AchievementQuests.TypeRelicWeapon, [3], Prev));
    }

    [Fact]
    public void A_single_quest_achievement_goes_to_its_quest()
    {
        Assert.Equal([4u], AchievementQuests.CreditedQuests(AchievementQuests.TypeAllQuests, [4, 0], Prev));
    }

    [Fact]
    public void An_all_of_set_goes_to_the_quest_that_needs_all_the_others_or_nowhere()
    {
        Assert.Equal([3u], AchievementQuests.CreditedQuests(AchievementQuests.TypeAllQuests, [1, 3, 2], Prev));
        Assert.Empty(AchievementQuests.CreditedQuests(AchievementQuests.TypeAllQuests, [3, 4], Prev));
        Assert.Empty(AchievementQuests.CreditedQuests(AchievementQuests.TypeAllQuests, [2, 4], Prev));
    }

    [Fact]
    public void An_any_of_set_goes_to_every_quest()
    {
        Assert.Equal([3u, 4u], AchievementQuests.CreditedQuests(AchievementQuests.TypeAnyQuest, [3, 4, 3], Prev));
    }

    [Fact]
    public void Earned_from_quests_needs_all_for_type_6_and_one_for_type_9()
    {
        var done = new HashSet<uint> { 1 };
        Assert.False(AchievementQuests.EarnedFromQuests(AchievementQuests.TypeAllQuests, [1, 2, 3], done.Contains));
        Assert.True(AchievementQuests.EarnedFromQuests(AchievementQuests.TypeAllQuests, [1], done.Contains));
        Assert.True(AchievementQuests.EarnedFromQuests(AchievementQuests.TypeAnyQuest, [2, 1], done.Contains));
        Assert.False(AchievementQuests.EarnedFromQuests(AchievementQuests.TypeAnyQuest, [2, 3], done.Contains));
        Assert.Null(AchievementQuests.EarnedFromQuests(AchievementQuests.TypeRelicWeapon, [1], done.Contains));
        Assert.Null(AchievementQuests.EarnedFromQuests(AchievementQuests.TypeAllQuests, [], done.Contains));
    }
}

/// <summary>The shipped Moonlit titles, achievements and aether currents (tagging audit finding 6).</summary>
public class MoonlitAchievementsShippedTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    private static readonly Regex TypeTag = new(@"(?:^|;)type=(\d+)", RegexOptions.Compiled);
    private static readonly Regex AchievementTag = new(@"(?:^|;)achievement=(\d+)", RegexOptions.Compiled);

    private static IReadOnlyList<UniqueRewardEntry> Entries() =>
        UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json")).Entries;

    private static IEnumerable<UniqueRewardEntry> AchievementEntries() =>
        Entries().Where(e => e.Kind is RewardKind.Title or RewardKind.Achievement);

    [Fact]
    public void No_relic_weapon_achievement_is_credited_to_a_quest()
    {
        var entries = AchievementEntries().ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            var type = TypeTag.Match(e.Source);
            Assert.True(type.Success, $"{e.QuestRowId} {e.RewardName}: no type in '{e.Source}'");
            Assert.NotEqual(AchievementQuests.TypeRelicWeapon.ToString(), type.Groups[1].Value);
        });
    }

    [Fact]
    public void An_aether_current_is_counted_once_per_quest()
    {
        var entries = Entries();
        var currentQuests = entries.Where(e => e.Kind == RewardKind.AetherCurrent).Select(e => e.QuestRowId).ToHashSet();
        Assert.NotEmpty(currentQuests);
        Assert.DoesNotContain(entries, e =>
            e.Kind == RewardKind.Other && e.RewardId == AetherCurrentQuests.OtherRewardAetherCurrent && currentQuests.Contains(e.QuestRowId));
        Assert.DoesNotContain(entries, e => e.Kind == RewardKind.Other && e.RewardName == "Aether Current");
    }

    [Fact]
    public void Each_title_and_achievement_is_shipped_once_per_quest()
    {
        var duplicates = AchievementEntries()
            .GroupBy(e => (e.QuestRowId, e.Kind, e.RewardId))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}")
            .ToList();
        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Against the sheet: every entry's achievement names its quest, is no relic weapon achievement, and an all-of-N
    /// achievement sits only on a quest that has every other quest of the set as a prerequisite (none today: the
    /// five such sets, "Tales of War" and the rest, are credited to no quest).
    /// </summary>
    [GameDataFact]
    public void No_all_of_N_achievement_is_credited_to_a_single_quest()
    {
        var achievements = fixture.Game.Excel.GetSheet<Achievement>(Language.English);
        var quests = fixture.Game.Excel.GetSheet<Quest>(Language.English);
        var offenders = new List<string>();
        foreach (var e in AchievementEntries())
        {
            var id = e.Kind == RewardKind.Achievement ? e.RewardId : uint.Parse(AchievementTag.Match(e.Source).Groups[1].Value);
            var row = achievements.GetRow(id);
            var named = AchievementQuests.QuestsOf(row);
            if (!named.Contains(e.QuestRowId))
            {
                offenders.Add($"{e.QuestRowId} {e.RewardName}: achievement {id} does not name the quest");
            }

            if (row.Type == AchievementQuests.TypeRelicWeapon)
            {
                offenders.Add($"{e.QuestRowId} {e.RewardName}: achievement {id} is a relic weapon achievement");
            }

            if (row.Type == AchievementQuests.TypeAllQuests && named.Count > 1
                && !AchievementQuests.CreditedQuests(row, quests).SequenceEqual([e.QuestRowId]))
            {
                offenders.Add($"{e.QuestRowId} {e.RewardName}: achievement {id} needs all of {string.Join(", ", named)}");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));

        // The five sets the audit found (Seeker of Bounty and the rest) are not on any quest.
        var shippedTitles = AchievementEntries().Where(e => e.Kind == RewardKind.Title).Select(e => e.RewardId).ToHashSet();
        foreach (var setId in new uint[] { 314, 315, 316, 317, 1165 })
        {
            var row = achievements.GetRow(setId);
            Assert.Equal(AchievementQuests.TypeAllQuests, row.Type);
            Assert.Empty(AchievementQuests.CreditedQuests(row, quests));
            Assert.DoesNotContain(row.Title.RowId, shippedTitles);
        }
    }
}
