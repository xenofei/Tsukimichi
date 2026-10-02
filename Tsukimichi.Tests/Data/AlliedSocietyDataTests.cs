using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// What the 1.5.0 allied society and repeatable gates read from the sheets: which quests need their rank's reputation
/// maxed (Quest.BeastReputationValue 65535), the reputation that maxes each rank, and the twelve repeat-flag quests.
/// </summary>
internal static class AlliedSocietyData
{
    /// <summary>The Qitari stela choices: rank-gated story quests that ask for no reputation (BeastReputationValue 0).</summary>
    public static readonly uint[] Stelae = [69336, 69337, 69338, 69339, 69340, 69341];

    /// <summary>The repeat-flag quests: row id, QuestRepeatFlag row, repeat interval (1 daily, 2 weekly).</summary>
    public static readonly (uint RowId, byte Flag, byte Interval)[] RepeatFlagged =
    [
        (65783, 3, 1),  // The Gift of Joy (The Lavender Beds)
        (65784, 4, 1),  // The Gift of Joy (Mist)
        (65785, 5, 1),  // The Gift of Joy (The Goblet)
        (66677, 2, 1),  // Morbid Motivation
        (67914, 10, 1), // A Starlight Story
        (67916, 7, 2),  // Seeking Inspiration
        (67917, 8, 1),  // Cut from a Different Cloth
        (69564, 11, 2), // One Man's Relic
        (69586, 12, 2), // All That Grinds Is Not Gloom
        (69587, 12, 2), // The Merchant of Komra
        (69588, 12, 2), // How to Catch an Automaton
        (69631, 13, 2), // A Ruined Opportunity
    ];

    public static void AssertRankUps(QuestCatalog catalog)
    {
        var tanks = catalog.GetByRowId(69434u);
        Assert.NotNull(tanks);
        Assert.EndsWith("I Heard You Like Tanks", tanks.Name);
        Assert.Equal(14, tanks.BeastTribe);
        Assert.Equal(4, tanks.BeastRank);
        Assert.True(tanks.BeastReputationMaxed);
        Assert.Equal(0, tanks.BeastValue);

        // Every allied society story quest past its opener needs its rank maxed, A Realm Reborn's societies included
        // ("Ranger Rescue", Amalj'aa at Neutral); the Qitari stela choices do not, and no daily does.
        var story = catalog.All.Where(q => q.BeastTribe != 0 && !q.IsRepeatable && q.BeastRank > 0 && !q.IsRemoved).ToList();
        Assert.True(story.Count >= 100, $"{story.Count} rank-gated story quests");
        Assert.All(story.Where(q => !Stelae.Contains(q.RowId)), q => Assert.True(q.BeastReputationMaxed, $"{q.RowId} {q.Name}"));
        Assert.All(story.Where(q => Stelae.Contains(q.RowId)), q => Assert.False(q.BeastReputationMaxed, $"{q.RowId} {q.Name}"));
        Assert.DoesNotContain(catalog.All, q => q.IsRepeatable && q.BeastReputationMaxed);
        Assert.True(catalog.GetByRowId(66755u)!.BeastReputationMaxed, "Ranger Rescue (Amalj'aa, Neutral)");

        // Each society's rank-up quests climb one rank at a time: the last asks for Sworn, or Honored for the Ixal.
        foreach (var tribe in story.Select(q => q.BeastTribe).Distinct())
        {
            var ranks = story.Where(q => q.BeastTribe == tribe && q.BeastReputationMaxed).Select(q => (int)q.BeastRank).Distinct().Order().ToList();
            Assert.True(ranks.Count >= 3, $"society {tribe}: ranks {string.Join(",", ranks)}");
        }
    }

    public static void AssertRepeatFlags(QuestCatalog catalog)
    {
        var flagged = catalog.All.Where(q => q.RepeatFlag != 0).OrderBy(q => q.RowId).Select(q => (q.RowId, q.RepeatFlag, q.RepeatInterval)).ToArray();
        Assert.Equal(RepeatFlagged, flagged);
        Assert.All(catalog.All.Where(q => q.RepeatFlag != 0), q => Assert.True(q.IsRepeatable && q.BeastTribe == 0, q.Name));
        Assert.Equal(6, RepeatFlagged.Count(q => q.Interval == 2));
    }
}

/// <summary>The same checks against the live sheets.</summary>
public class AlliedSocietyDataTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Rank_up_quests_need_their_rank_maxed() => AlliedSocietyData.AssertRankUps(fixture.Bundle.Catalog);

    [GameDataFact]
    public void Twelve_quests_carry_a_repeat_flag() => AlliedSocietyData.AssertRepeatFlags(fixture.Bundle.Catalog);

    [GameDataFact]
    public void Rank_maximums_are_the_sheet_s()
    {
        var sheet = fixture.Game.GetExcelSheet<BeastReputationRank>()!;
        foreach (var row in sheet)
        {
            Assert.Equal(row.RequiredReputation, TribeRanks.MaxReputation((byte)row.RowId));
        }

        Assert.Equal(9, sheet.Count);
        Assert.Equal(16, fixture.Game.GetExcelSheet<QuestRepeatFlag>()!.Count);
    }

    [GameDataFact]
    public void Every_daily_has_a_giver_so_each_society_s_offer_can_be_read_in_full()
    {
        var catalog = fixture.Bundle.Catalog;
        var dailies = catalog.All.Where(q => q.IsAlliedSocietyDaily && q.DailyPool != 0 && !q.IsRemoved).ToList();
        Assert.True(dailies.Count > 500, $"{dailies.Count} dailies");
        Assert.All(dailies, q => Assert.True(q.Issuer is { NpcId: not 0 }, $"{q.RowId} {q.Name}"));
        Assert.All(dailies, q => Assert.Contains(DailyOfferBook.DailiesOf(catalog, q.Issuer!.NpcId), d => d.RowId == q.RowId));
        // A giver serves one society.
        Assert.All(dailies.GroupBy(q => q.Issuer!.NpcId), g => Assert.Single(g.Select(q => q.BeastTribe).Distinct()));
    }
}

/// <summary>The same checks against the frozen catalog, on every machine.</summary>
public class AlliedSocietyFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    [Fact]
    public void Rank_up_quests_need_their_rank_maxed() => AlliedSocietyData.AssertRankUps(fixture.Bundle.Catalog);

    [Fact]
    public void Twelve_quests_carry_a_repeat_flag() => AlliedSocietyData.AssertRepeatFlags(fixture.Bundle.Catalog);

    [Fact]
    public void A_dwarf_rank_up_reads_blocked_until_trusted_is_maxed()
    {
        var catalog = fixture.Bundle.Catalog;
        var tanks = catalog.GetByRowId(69434u)!;
        var done = tanks.PreviousQuests.QuestIds;
        var snapshot = Tsukimichi.Tests.Evaluation.Fixture.Snapshot(done) with
        {
            JobLevels = Tsukimichi.Tests.Evaluation.Fixture.Levels((1, 100)),
            Tribes = new Dictionary<byte, TribeStanding> { [14] = new(4, 0) },
        };

        var blocked = StateResolver.Resolve(tanks, snapshot, catalog, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal("Trusted 0/720 reputation", blocked.NextStep!.Detail);

        var maxed = snapshot with { Tribes = new Dictionary<byte, TribeStanding> { [14] = new(4, 720) } };
        Assert.DoesNotContain(StateResolver.Resolve(tanks, maxed, catalog, EvalContext.Default).Requirements, r => r.Req.Kind == RequirementKind.TribeReputation && !r.Met);
    }
}
