using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// The allied society board (1.9.0, R6 E): unlocked societies only, rank and reputation against the rank-up thresholds,
/// the dailies done and offered today (unknown while the offer is), the main giver for the zone and Teleport, the
/// allowances and the next daily reset, with a stored character read as of now.
/// </summary>
public sealed class AlliedSocietyBoardTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static QuestRecord Daily(uint rowId, byte tribe, byte rank, uint giver) => Fixture.Quest(rowId) with
    {
        IsRepeatable = true,
        RepeatInterval = GameResets.DailyInterval,
        DailyPool = 1,
        BeastTribe = tribe,
        BeastRank = rank,
        Issuer = new Issuer(giver, $"Giver {giver}", 400, 30 + giver, 0f, 0f, 0f),
    };

    // Society 1: two dailies from giver 100 at rank 1, three from giver 101 at rank 3 (above the character's rank 2).
    // Society 3: one daily. Society 2: a daily, but the character has not unlocked the society.
    private static readonly QuestRecord[] Dailies =
    [
        Daily(66100, 1, 1, 100), Daily(66101, 1, 1, 100),
        Daily(66102, 1, 3, 101), Daily(66103, 1, 3, 101), Daily(66104, 1, 3, 101),
        Daily(66110, 2, 1, 110),
        Daily(66120, 3, 1, 120),
    ];

    private static readonly QuestCatalog Catalog = Fixture.Catalog(Dailies);

    private static CharacterSnapshot Character(DateTime taken) => Fixture.Snapshot() with
    {
        TakenUtc = taken,
        Tribes = new Dictionary<byte, TribeStanding>
        {
            [1] = new(2, 200),
            [2] = new(0, 0),
            [3] = new(4, 720, RankedUpToday: true),
            [4] = new(8, 0),
        },
        DailyDone = new Dictionary<ushort, byte> { [QuestRecord.ToQuestId(66100)] = 1, [QuestRecord.ToQuestId(66120)] = 1 },
        TribeAllowance = 10,
    };

    [Fact]
    public void Lists_the_unlocked_societies_with_rank_and_reputation()
    {
        var board = AlliedSocietyBoard.Build(Catalog, Character(Now.AddMinutes(-5)), null, null, Now);
        Assert.Equal([1, 3, 4], board.Rows.Select(r => (int)r.Tribe));

        var first = board.Rows[0];
        Assert.Equal((2, 200, (ushort?)360), (first.Rank, first.Reputation, first.RankMax));
        Assert.False(first.Maxed);
        Assert.Equal(200f / 360f, first.Fraction, 4);

        var third = board.Rows[1];
        Assert.True(third.Maxed);
        Assert.True(third.RankedUpToday);
        Assert.Equal(1f, third.Fraction);

        var last = board.Rows[2];
        Assert.True(last.AtLastRank);
        Assert.False(last.Maxed);
        Assert.Null(last.Giver);
    }

    [Fact]
    public void Counts_the_dailies_done_and_leaves_the_offer_unknown_until_read()
    {
        var unknown = AlliedSocietyBoard.Build(Catalog, Character(Now.AddMinutes(-5)), null, null, Now);
        Assert.Equal((1, (int?)null), (unknown.Rows[0].DoneToday, unknown.Rows[0].OfferedToday));
        Assert.Equal((1, (int?)null), (unknown.Rows[1].DoneToday, unknown.Rows[1].OfferedToday));

        // An offer read for society 1 only: society 3's stays unknown.
        var offered = new HashSet<ushort> { QuestRecord.ToQuestId(66100), QuestRecord.ToQuestId(66101), QuestRecord.ToQuestId(66102) };
        var known = AlliedSocietyBoard.Build(Catalog, Character(Now.AddMinutes(-5)), offered, new HashSet<byte> { 1 }, Now);
        Assert.Equal((1, (int?)3), (known.Rows[0].DoneToday, known.Rows[0].OfferedToday));
        Assert.Null(known.Rows[1].OfferedToday);

        // An offer that speaks for every society (null tribes) knows society 3 offers none of these.
        var every = AlliedSocietyBoard.Build(Catalog, Character(Now.AddMinutes(-5)), offered, null, Now);
        Assert.Equal(0, every.Rows[1].OfferedToday);
    }

    [Fact]
    public void The_giver_is_the_main_one_open_at_the_character_s_rank()
    {
        var board = AlliedSocietyBoard.Build(Catalog, Character(Now.AddMinutes(-5)), null, null, Now);
        Assert.Equal(66100u, board.Rows[0].Giver?.RowId);
        Assert.Equal(66120u, board.Rows[1].Giver?.RowId);

        // At rank 3 giver 101 has more dailies open; today's offer, when read, decides first.
        var ranked = Character(Now.AddMinutes(-5)) with
        {
            Tribes = new Dictionary<byte, TribeStanding> { [1] = new(3, 0) },
        };
        Assert.Equal(66102u, AlliedSocietyBoard.Build(Catalog, ranked, null, null, Now).Rows[0].Giver?.RowId);
        var offered = new HashSet<ushort> { QuestRecord.ToQuestId(66100), QuestRecord.ToQuestId(66101) };
        Assert.Equal(66100u, AlliedSocietyBoard.Build(Catalog, ranked, offered, new HashSet<byte> { 1 }, Now).Rows[0].Giver?.RowId);
    }

    [Fact]
    public void Allowances_and_the_next_reset_and_a_save_from_before_the_reset_reads_as_a_fresh_day()
    {
        var today = AlliedSocietyBoard.Build(Catalog, Character(Now.AddMinutes(-5)), null, null, Now);
        Assert.Equal(10, today.AllowancesLeft);
        Assert.Equal(new DateTime(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc), today.NextReset);

        // Saved yesterday before the 15:00 UTC reset: nothing done today, the allowances full, the rank-up mark gone.
        var stored = AlliedSocietyBoard.Build(Catalog, Character(Now.AddDays(-1)), null, null, Now);
        Assert.Equal(GameResets.FullTribeAllowance, stored.AllowancesLeft);
        Assert.All(stored.Rows, r => Assert.Equal(0, r.DoneToday));
        Assert.False(stored.Rows[1].RankedUpToday);
    }

    [Fact]
    public void A_character_with_no_society_has_an_empty_board()
    {
        var board = AlliedSocietyBoard.Build(Catalog, Fixture.Snapshot() with { TakenUtc = Now }, null, null, Now);
        Assert.Empty(board.Rows);
    }
}
