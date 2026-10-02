using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Runtime;

public class DailyOfferBookTests
{
    private const byte Amaljaa = 1;
    private const byte Dwarves = 14;
    private const uint Fibubb = 1005550;   // rank 1 giver
    private const uint Yadovv = 1005551;   // rank 2 giver
    private const uint Bajaal = 1005552;   // rank 3 giver
    private const uint Dwarf = 1033712;

    private static readonly DateTime Morning = new(2026, 10, 1, 16, 0, 0, DateTimeKind.Utc);

    /// <summary>The game's daily seed the answers are computed with.</summary>
    private const byte Seed = 42;

    private static QuestRecord Daily(uint rowId, byte tribe, byte rank, uint giver) => Quest(rowId) with
    {
        BeastTribe = tribe,
        BeastRank = rank,
        IsRepeatable = true,
        RepeatInterval = 1,
        DailyPool = 2,
        Issuer = new Issuer(giver, "Giver", 1, 0, 0, 0, 0),
    };

    private static readonly QuestCatalog Catalog = Tsukimichi.Tests.Evaluation.Fixture.Catalog(
        Daily(66001, Amaljaa, 1, Fibubb),
        Daily(66002, Amaljaa, 1, Fibubb),
        Daily(66003, Amaljaa, 2, Yadovv),
        Daily(66004, Amaljaa, 2, Yadovv),
        Daily(66005, Amaljaa, 3, Bajaal),
        Daily(69501, Dwarves, 3, Dwarf),
        Daily(69502, Dwarves, 3, Dwarf),
        Daily(69503, Dwarves, 4, Dwarf));

    private static ushort Id(uint rowId) => QuestRecord.ToQuestId(rowId);

    private static CharacterSnapshot Character(byte amaljaaRank, byte dwarfRank, bool rankedUp = false) => Snapshot() with
    {
        ContentId = 7,
        Tribes = new Dictionary<byte, TribeStanding>
        {
            [Amaljaa] = new(amaljaaRank, 0),
            [Dwarves] = new(dwarfRank, 0, rankedUp),
        },
    };

    [Fact]
    public void Nothing_read_means_nothing_known()
    {
        Assert.True(new DailyOfferBook().Offer(Catalog, Character(2, 4), Morning).IsEmpty);
    }

    [Fact]
    public void A_society_is_known_once_every_giver_at_or_below_its_rank_is_read()
    {
        var book = new DailyOfferBook();
        var character = Character(2, 4);
        book.Record(7, Morning, Seed, Fibubb, Amaljaa, character.Tribes[Amaljaa], [Id(66001)]);
        book.Record(7, Morning, Seed, Dwarf, Dwarves, character.Tribes[Dwarves], [Id(69501), Id(69503)]);

        // Yadovv (rank 2) is not read yet: the Amalj'aa stay unknown. The rank-3 giver offers nothing at rank 2.
        var partial = book.Offer(Catalog, character, Morning);
        Assert.Equal([Dwarves], partial.Tribes);
        Assert.Equal([Id(69501), Id(69503)], partial.Quests.Order());

        book.Record(7, Morning, Seed, Yadovv, Amaljaa, character.Tribes[Amaljaa], [Id(66003)]);
        var full = book.Offer(Catalog, character, Morning);
        Assert.Equal([Amaljaa, Dwarves], full.Tribes.Order());
        Assert.Equal([Id(66001), Id(66003), Id(69501), Id(69503)], full.Quests.Order());
        Assert.True(book.Knows(7, Morning, Seed, Yadovv, character.Tribes[Amaljaa]));
    }

    [Fact]
    public void A_society_with_no_giver_read_stays_unknown_even_when_no_giver_offers_at_its_rank()
    {
        var book = new DailyOfferBook();
        var character = Character(0, 4);
        book.Record(7, Morning, Seed, Dwarf, Dwarves, character.Tribes[Dwarves], [Id(69501)]);

        Assert.DoesNotContain(Amaljaa, book.Offer(Catalog, character, Morning).Tribes);
    }

    [Fact]
    public void Answers_expire_at_the_daily_reset_for_another_character_and_on_a_rank_change()
    {
        var book = new DailyOfferBook();
        var character = Character(2, 4);
        book.Record(7, Morning, Seed, Dwarf, Dwarves, character.Tribes[Dwarves], [Id(69501)]);
        Assert.Contains(Dwarves, book.Offer(Catalog, character, Morning).Tribes);

        // Ranked up (Respected, ranked up today): the answer at Trusted no longer holds.
        Assert.True(book.Offer(Catalog, Character(2, 5, rankedUp: true), Morning).IsEmpty);
        Assert.False(book.Knows(7, Morning, Seed, Dwarf, new TribeStanding(5, 0, true)));

        // The next daily reset.
        Assert.True(book.Offer(Catalog, character, Morning.AddDays(1)).IsEmpty);
        Assert.Equal(0, book.Count);

        // Another character.
        book.Record(7, Morning, Seed, Dwarf, Dwarves, character.Tribes[Dwarves], [Id(69501)]);
        Assert.True(book.Offer(Catalog, character with { ContentId = 8 }, Morning).IsEmpty);
    }

    [Fact]
    public void Answers_hold_only_for_the_daily_seed_they_were_computed_with()
    {
        var book = new DailyOfferBook();
        var character = Character(2, 4);
        var standing = character.Tribes[Dwarves];

        // Read just after the reset while the client still held yesterday's seed (or the clock ran ahead).
        book.Record(7, Morning, Seed, Dwarf, Dwarves, standing, [Id(69501)]);
        Assert.True(book.Knows(7, Morning, Seed, Dwarf, standing));
        Assert.Contains(Dwarves, book.Offer(Catalog, character, Morning).Tribes);

        // The server's seed for the day arrives: the old answer is not today's and must be read again.
        Assert.False(book.Knows(7, Morning, Seed + 1, Dwarf, standing));
        Assert.Equal(0, book.Count);
        Assert.True(book.Offer(Catalog, character, Morning).IsEmpty);

        book.Record(7, Morning, Seed + 1, Dwarf, Dwarves, standing, [Id(69502), Id(69503)]);
        Assert.Equal([Id(69502), Id(69503)], book.Offer(Catalog, character, Morning).Quests.Order());

        // A seed change seen while no giver is loaded drops the answers too.
        book.Observe(7, Morning, Seed);
        Assert.Equal(0, book.Count);
        Assert.True(book.Offer(Catalog, character, Morning).IsEmpty);

        // The same seed again keeps what is known.
        book.Record(7, Morning, Seed, Dwarf, Dwarves, standing, [Id(69501)]);
        book.Observe(7, Morning, Seed);
        Assert.Equal(1, book.Count);
    }

    [Fact]
    public void An_offer_missing_a_daily_taken_today_is_dropped_and_reported_once()
    {
        var book = new DailyOfferBook();
        var character = Character(2, 4) with { DailyDone = new Dictionary<ushort, byte> { [Id(69502)] = 1 } };
        book.Record(7, Morning, Seed, Dwarf, Dwarves, character.Tribes[Dwarves], [Id(69501), Id(69503)]);

        Assert.True(book.Offer(Catalog, character, Morning).IsEmpty);
        Assert.Equal([Dwarves], book.Mismatched);
        Assert.Empty(book.Mismatched);
        book.Offer(Catalog, character, Morning);
        Assert.Empty(book.Mismatched);

        // A daily in the journal counts the same; one that was offered is fine.
        var accepted = Character(2, 4) with { Accepted = [new AcceptedQuest(Id(69501), 0)] };
        Assert.Contains(Dwarves, book.Offer(Catalog, accepted, Morning).Tribes);
    }

    [Fact]
    public void Dailies_of_a_giver_come_from_the_catalog()
    {
        Assert.Equal([Id(66003), Id(66004)], DailyOfferBook.DailiesOf(Catalog, Yadovv).Select(q => q.QuestId));
        Assert.Empty(DailyOfferBook.DailiesOf(Catalog, 42));
    }

    [Fact]
    public void Offers_compare_by_content()
    {
        var a = new DailyOffer(new HashSet<ushort> { 1, 2 }, new HashSet<byte> { 3 });
        var b = new DailyOffer(new HashSet<ushort> { 2, 1 }, new HashSet<byte> { 3 });
        Assert.True(a.SameAs(b));
        Assert.False(a.SameAs(DailyOffer.None));
        Assert.False(a.SameAs(null));
        Assert.True(DailyOffer.None.SameAs(new DailyOffer(new HashSet<ushort>(), new HashSet<byte>())));
    }
}
