using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// Carried-over allied society dailies and stored alts' allowances (1.19.0, C5): a daily accepted before the daily
/// reset and still in the journal holds every allowance back until it is turned in, live or projected from a stored
/// character's last login; and the rank-up hint ("keep 3 allowances for the bonus dailies").
/// </summary>
public sealed class AlliedCarryoverTests
{
    // 2026-10-02 12:00 UTC: the last reset was 2026-10-01 15:00 UTC, the next 2026-10-02 15:00 UTC.
    private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastReset = new(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);

    private const uint MoogleDaily = 66100;
    private const uint SahaginDaily = 66110;
    private const uint SahaginRankUp = 66119;

    private static QuestRecord Daily(uint rowId, byte tribe) => Fixture.Quest(rowId) with
    {
        IsRepeatable = true,
        RepeatInterval = GameResets.DailyInterval,
        DailyPool = 1,
        BeastTribe = tribe,
        BeastRank = 1,
        Issuer = new Issuer(500 + rowId, "Giver", 400, 30, 0f, 0f, 0f),
    };

    private static readonly QuestCatalog Catalog = Fixture.Catalog(
        Daily(MoogleDaily, 8),
        Daily(SahaginDaily, 4),
        Fixture.Quest(SahaginRankUp) with { BeastTribe = 4, BeastRank = 3, BeastReputationMaxed = true },
        Fixture.Quest(66200));

    private static CharacterSnapshot Character(DateTime taken, params uint[] journal) => Fixture.Snapshot() with
    {
        TakenUtc = taken,
        Accepted = journal.Select(id => Fixture.Accepted(id, 0)).ToList(),
        Tribes = new Dictionary<byte, TribeStanding> { [8] = new(6, 100), [4] = new(3, 510) },
        TribeAllowance = 9,
    };

    private static ushort Id(uint rowId) => QuestRecord.ToQuestId(rowId);

    [Fact]
    public void A_daily_accepted_before_the_reset_carries_over_and_one_accepted_after_does_not()
    {
        var snapshot = Character(Now, MoogleDaily, SahaginDaily, 66200);
        var since = new Dictionary<ushort, DateTime>
        {
            [Id(MoogleDaily)] = LastReset.AddHours(-2),
            [Id(SahaginDaily)] = LastReset.AddHours(2),
            [Id(66200)] = LastReset.AddDays(-5),
        };

        var carried = Assert.Single(AlliedCarryover.Find(Catalog, snapshot, since, Now));
        Assert.Equal(MoogleDaily, carried.Quest.RowId);
        Assert.Equal(LastReset.AddHours(-2), carried.AcceptedUtc);

        // Without accepted times a live capture's dailies read as taken now: nothing is claimed.
        Assert.Empty(AlliedCarryover.Find(Catalog, snapshot, null, Now));
    }

    [Fact]
    public void A_stored_character_captured_before_the_reset_with_a_daily_in_its_journal_holds_it_over()
    {
        var stored = Character(LastReset.AddDays(-1), SahaginDaily);
        Assert.Equal(SahaginDaily, Assert.Single(AlliedCarryover.Find(Catalog, stored, null, Now)).Quest.RowId);

        // Projected as of now: 0 allowances, not 12.
        Assert.Equal(0, GameResets.AsOf(stored, Catalog, Now).TribeAllowance);
        Assert.Equal(GameResets.FullTribeAllowance, GameResets.AsOf(Character(LastReset.AddDays(-1)), Catalog, Now).TribeAllowance);
    }

    [Fact]
    public void The_board_reads_0_allowances_and_marks_the_society_holding_the_daily()
    {
        var since = new Dictionary<ushort, DateTime> { [Id(MoogleDaily)] = LastReset.AddHours(-1) };
        var board = AlliedSocietyBoard.Build(Catalog, Character(Now, MoogleDaily), null, null, Now, since);

        Assert.Equal(0, board.AllowancesLeft);
        Assert.Equal(MoogleDaily, Assert.Single(board.Carried).Quest.RowId);
        Assert.Equal(MoogleDaily, board.Rows.Single(r => r.Tribe == 8).Carried?.RowId);
        Assert.Null(board.Rows.Single(r => r.Tribe == 4).Carried);
        Assert.False(board.Projected);

        // Turned in: the game's count stands again.
        var clear = AlliedSocietyBoard.Build(Catalog, Character(Now), null, null, Now, since);
        Assert.Equal(9, clear.AllowancesLeft);
        Assert.Empty(clear.Carried);
    }

    [Fact]
    public void A_stored_board_says_it_is_projected_and_from_when()
    {
        var taken = LastReset.AddDays(-2);
        var board = AlliedSocietyBoard.Build(Catalog, Character(taken), null, null, Now);
        Assert.True(board.Projected);
        Assert.Equal(taken, board.TakenUtc);
        Assert.Equal(GameResets.FullTribeAllowance, board.AllowancesLeft);

        var holding = AlliedSocietyBoard.Build(Catalog, Character(taken, SahaginDaily), null, null, Now);
        Assert.Equal(0, holding.AllowancesLeft);
        Assert.Equal(SahaginDaily, holding.Rows.Single(r => r.Tribe == 4).Carried?.RowId);
    }

    [Fact]
    public void Rank_up_ready_when_the_reputation_is_full_and_the_rank_up_quest_waits()
    {
        // Sahagin rank 3 at 510 (full): its rank-up quest waits, and ranking up to rank 4 opens the bonus dailies.
        var board = AlliedSocietyBoard.Build(Catalog, Character(Now), null, null, Now);
        Assert.True(board.Rows.Single(r => r.Tribe == 4).RankUpBonus);
        Assert.False(board.Rows.Single(r => r.Tribe == 8).RankUpBonus);

        // Done already: nothing waits.
        var done = Character(Now) with { CompletedBits = Fixture.Bits(SahaginRankUp) };
        Assert.False(AlliedSocietyBoard.Build(Catalog, done, null, null, Now).Rows.Single(r => r.Tribe == 4).RankUpBonus);
    }

    [Fact]
    public void Ranking_up_to_Sworn_opens_bonus_dailies_for_the_Moogles_and_the_Namazu_only()
    {
        Assert.True(AlliedCarryover.RankUpOpensBonus(4, 3));
        Assert.False(AlliedCarryover.RankUpOpensBonus(4, 6));
        Assert.True(AlliedCarryover.RankUpOpensBonus(8, 6));
        Assert.True(AlliedCarryover.RankUpOpensBonus(11, 6));
    }
}
