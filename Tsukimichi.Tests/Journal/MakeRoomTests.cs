using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Journal;

/// <summary>Journal slots and Make room (feature plan v7, C9): what is left of the 30 slots, and what freeing each one costs.</summary>
public class MakeRoomTests
{
    private const uint Msq = 65610;
    private const uint Daily = 65611;
    private const uint Weekly = 65612;
    private const uint Seasonal = 65613;
    private const uint Started = 65614;
    private const uint NoGiver = 65615;
    private const uint Unknown = 65999;

    private static readonly Issuer Giver = new(1000, "Wymond", 132, 2, 0f, 0f, 0f);

    /// <summary>A side quest: the fixture's default journal section (1) is the main scenario's.</summary>
    private static QuestRecord SideQuest(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Side Quests", 1, "Category", 89, "Genre", (int)rowId) };

    private static QuestCatalog JournalCatalog() => Catalog(
        SideQuest(A, "Fresh") with { Issuer = Giver, Level = 30 },
        SideQuest(B, "Low") with { Issuer = Giver, Level = 5 },
        SideQuest(C, "Last Step") with { Issuer = Giver, StepCount = 4 },
        Quest(Msq, "Story") with { Issuer = Giver, Journal = new JournalRef(1, "Main Scenario", 1, "Category", 1, "Genre", 1) },
        SideQuest(Daily, "Tribal") with { Issuer = Giver, IsRepeatable = true, BeastTribe = 1 },
        SideQuest(Weekly, "Weekly") with { Issuer = Giver, IsRepeatable = true },
        SideQuest(Seasonal, "Festive") with { Issuer = Giver, Festival = 3 },
        SideQuest(Started, "Halfway") with { Issuer = Giver, StepCount = 5 },
        SideQuest(NoGiver, "Nobody"));

    private static CharacterSnapshot With(params (uint RowId, byte Sequence)[] journal) =>
        Snapshot() with { Accepted = journal.Select(j => Accepted(j.RowId, j.Sequence)).ToArray() };

    [Fact]
    public void The_journal_says_what_is_left_never_a_tally()
    {
        Assert.Equal((27, JournalRoom.Room, "27 journal slots left"), Slots(new JournalSlots(3, 30)));
        Assert.Equal((3, JournalRoom.Near, "3 journal slots left"), Slots(new JournalSlots(27, 30)));
        Assert.Equal((1, JournalRoom.Near, "1 journal slot left"), Slots(new JournalSlots(29, 30)));
        Assert.Equal((0, JournalRoom.Full, "Journal full"), Slots(new JournalSlots(30, 30)));
        Assert.Equal((0, JournalRoom.Full, "Journal full"), Slots(new JournalSlots(31, 30)));

        static (int, JournalRoom, string) Slots(JournalSlots s) => (s.Left, s.Room, s.Text);
    }

    [Fact]
    public void The_client_s_own_count_wins_and_an_older_capture_counts_its_journal_less_the_allied_dailies()
    {
        var catalog = JournalCatalog();
        var older = With((A, 1), (Daily, 0), (Weekly, 1));
        Assert.Equal(new JournalSlots(2, JournalSlots.GameCap), JournalSlots.Of(older, catalog));
        Assert.Equal(new JournalSlots(29, 30), JournalSlots.Of(older with { JournalSlotsUsed = 29 }, catalog));
    }

    [Fact]
    public void Make_room_puts_hand_ins_first_then_safe_drops_lowest_level_first_then_the_rest_with_reasons()
    {
        var catalog = JournalCatalog();
        var snapshot = With(
            (A, 1), (B, 1), (C, MakeRoom.LastStep), (Msq, 2), (Daily, 0), (Weekly, 1),
            (Seasonal, 1), (Started, 3), (NoGiver, 1), (Unknown, 1));

        var rows = MakeRoom.Rank(snapshot, catalog);

        // The allied society daily keeps no journal slot, so it is not listed.
        Assert.DoesNotContain(rows, r => r.Entry.QuestId == QuestRecord.ToQuestId(Daily));
        Assert.Equal(
            [
                (C, RoomAdvice.HandIn),
                (B, RoomAdvice.SafeToDrop),
                (A, RoomAdvice.SafeToDrop),
                (Msq, RoomAdvice.Keep),
                (Weekly, RoomAdvice.Keep),
                (Seasonal, RoomAdvice.Keep),
                (Started, RoomAdvice.Keep),
                (NoGiver, RoomAdvice.Keep),
                (Unknown, RoomAdvice.Keep),
            ],
            rows.Select(r => (0x10000u | r.Entry.QuestId, r.Advice)));

        string Reason(uint rowId) => rows.Single(r => r.Entry.QuestId == QuestRecord.ToQuestId(rowId)).Reason;
        Assert.Equal("At its last step: hand it in to free the slot.", Reason(C));
        Assert.Equal("Not started: take it again from Wymond any time.", Reason(A));
        Assert.Equal("Main scenario: the game won't let you abandon it.", Reason(Msq));
        Assert.Equal("Repeatable: dropping it doesn't give back today's allowance.", Reason(Weekly));
        Assert.Equal("Seasonal: the event may end before you can take it again.", Reason(Seasonal));
        Assert.Equal("Started (step 3 of 5): you'd do those steps again.", Reason(Started));
        Assert.Equal("No known giver to take it from again.", Reason(NoGiver));
        Assert.StartsWith("Not in Tsukimichi's quest list", Reason(Unknown), StringComparison.Ordinal);

        Assert.Equal("step 4 of 4", rows[0].StepText);
        Assert.Equal(string.Empty, MakeRoom.Rank(With((A, 0)), catalog)[0].StepText);
    }

    [Fact]
    public void A_main_scenario_quest_at_its_last_step_reads_hand_in_since_handing_it_in_frees_its_slot()
    {
        var catalog = JournalCatalog();
        var row = Assert.Single(MakeRoom.Rank(With((Msq, MakeRoom.LastStep)), catalog));
        Assert.Equal(RoomAdvice.HandIn, row.Advice);
    }
}
