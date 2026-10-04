using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Journal;

/// <summary>
/// Journal slots and Make room (feature plan v7, C9; spec-1.19 "The journal count and Make room"): what is left of the
/// 30 slots, how the status bar shows it, when a Ready quest reads "journal full", and the Make room groups.
/// </summary>
public class MakeRoomTests
{
    private const uint Msq = 65610;
    private const uint Daily = 65611;
    private const uint Weekly = 65612;
    private const uint Seasonal = 65613;
    private const uint Started = 65614;
    private const uint NoGiver = 65615;
    private const uint Tribal = 65616;
    private const uint SoloDuty = 65617;
    private const uint Raid = 65618;
    private const uint Supply = 65619;
    private const uint Gatherer = 65620;
    private const uint Unknown = 65999;

    private static readonly Issuer Giver = new(1000, "Wymond", 132, 2, 0f, 0f, 0f);

    private static readonly HandInItem Ore = new() { ItemId = 5114, Name = "Mythrite Ore", Amount = 3 };
    private static readonly HandInItem MinerOre = new() { ItemId = 5115, Name = "Cobalt Ore", ClassJobCategories = [17] };
    private static readonly HandInItem BotanistLog = new() { ItemId = 5390, Name = "Spruce Log", ClassJobCategories = [18] };

    private static readonly DutyRunInfo Aery = new(39, 39, 1, DutyRunInfo.Dungeons, "The Aery", OffersDutySupport: true, OffersTrust: false) { Players = 4 };
    private static readonly DutyRunInfo Battle = new(40, 40, 2, 0, "A Lance's Oath", OffersDutySupport: false, OffersTrust: false) { Players = 1, InDutyFinder = false };
    private static readonly DutyRunInfo Jeuno = new(41, 41, 3, DutyRunInfo.Raids, "Jeuno: The First Walk", OffersDutySupport: false, OffersTrust: false) { Players = 24 };

    /// <summary>A side quest: the fixture's default journal section (1) is the main scenario's.</summary>
    private static QuestRecord SideQuest(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Side Quests", 1, "Category", 89, "Genre", (int)rowId), Issuer = Giver };

    private static QuestCatalog JournalCatalog() => Catalog(
        SideQuest(A, "Fresh") with { Level = 30 },
        SideQuest(B, "Low") with { Level = 5 },
        SideQuest(C, "Last Step") with { StepCount = 4 },
        SideQuest(D, "Delivery") with { StepCount = 2, HandInItems = [Ore] },
        Quest(Msq, "Story") with { Issuer = Giver, Journal = new JournalRef(1, "Main Scenario", 1, "Category", 1, "Genre", 1) },
        SideQuest(Daily, "Tribal daily") with { IsRepeatable = true, BeastTribe = 1 },
        SideQuest(Weekly, "Weekly") with { IsRepeatable = true },
        SideQuest(Seasonal, "Festive") with { Festival = 3 },
        SideQuest(Started, "Halfway") with { StepCount = 5 },
        SideQuest(NoGiver, "Nobody") with { Issuer = null },
        SideQuest(Tribal, "Society story") with { BeastTribe = 2 },
        SideQuest(SoloDuty, "Sworn Upon a Lance") with { StepCount = 3 },
        SideQuest(Raid, "An Otherworldly Encounter") with { StepCount = 3 },
        SideQuest(Supply, "Sellspade") with { StepCount = 3, HandInItems = [Ore] },
        SideQuest(Gatherer, "Gatherer's Due") with { StepCount = 3, HandInItems = [MinerOre, BotanistLog] });

    private static CharacterSnapshot With(params (uint RowId, byte Sequence)[] journal) =>
        Snapshot() with { Accepted = journal.Select(j => Accepted(j.RowId, j.Sequence)).ToArray() };

    private static DutyRunInfo? Duties(QuestRecord quest) => quest.RowId switch
    {
        SoloDuty => Battle,
        Raid => Jeuno,
        Started => Aery,
        _ => null,
    };

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

    [Theory]
    [InlineData(0, JournalBar.Hidden)]
    [InlineData(24, JournalBar.Hidden)]
    [InlineData(25, JournalBar.Quiet)]
    [InlineData(26, JournalBar.Quiet)]
    [InlineData(27, JournalBar.Near)]
    [InlineData(29, JournalBar.Near)]
    [InlineData(30, JournalBar.Full)]
    public void The_status_bar_shows_the_count_from_25_in_text_from_27_and_full_at_30(int used, JournalBar bar) =>
        Assert.Equal(bar, new JournalSlots(used, JournalSlots.GameCap).Bar);

    [Fact]
    public void Only_a_ready_quest_reads_journal_full_and_only_while_the_journal_is_full()
    {
        var full = new JournalSlots(30, 30);
        Assert.True(full.KeepsOut(QuestState.Ready));
        Assert.True(full.KeepsOut(QuestState.ReadyOnOtherJob));
        Assert.All(
            [QuestState.Accepted, QuestState.Blocked, QuestState.Unknown, QuestState.Completed, QuestState.Foreclosed, QuestState.DoneThisCycle],
            state => Assert.False(full.KeepsOut(state)));
        Assert.False(new JournalSlots(29, 30).KeepsOut(QuestState.Ready));
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
    public void Make_room_groups_the_journal_by_what_finishing_takes_then_safe_to_drop_and_leaves_out_what_it_cannot_say()
    {
        var catalog = JournalCatalog();
        var snapshot = With(
            (A, 1), (B, 0), (C, MakeRoom.LastStep), (D, MakeRoom.LastStep), (Msq, 2), (Daily, 0), (Weekly, 1),
            (Seasonal, 1), (Started, 3), (NoGiver, 1), (Tribal, 1), (SoloDuty, 2), (Raid, 2), (Supply, 2), (Unknown, 1));

        // Three of the ore in the bags for the delivery at its last step, none for the other.
        var entries = MakeRoom.Plan(snapshot, catalog, Duties, item => item.ItemId == Ore.ItemId ? 3 : 0);

        Assert.Equal(
            [
                (C, RoomGroup.FinishNow),
                (D, RoomGroup.FinishNow),
                (Started, RoomGroup.NeedsDuty),
                (SoloDuty, RoomGroup.NeedsDuty),
                (Raid, RoomGroup.NeedsGroup),
                (B, RoomGroup.SafeToDrop),
                (A, RoomGroup.SafeToDrop),
            ],
            entries.Select(e => (e.Quest.RowId, e.Group)));

        Assert.False(Entry(C).IsDelivery);
        Assert.True(Entry(D).IsDelivery);
        Assert.Same(Battle, Entry(SoloDuty).Duty);
        Assert.Same(Aery, Entry(Started).Duty);
        Assert.Same(Jeuno, Entry(Raid).Duty);

        // The daily keeps no slot; the main scenario, a repeatable, a seasonal or society quest, one started without a
        // known duty, one with no giver to take it from again and one Tsukimichi doesn't know have nothing to say.
        Assert.DoesNotContain(entries, e => e.Quest.RowId is Msq or Daily or Weekly or Seasonal or NoGiver or Tribal or Unknown);

        RoomEntry Entry(uint rowId) => entries.Single(e => e.Quest.RowId == rowId);
    }

    [Fact]
    public void An_item_the_character_lacks_puts_the_quest_under_needs_an_item_with_the_count()
    {
        var catalog = JournalCatalog();
        var snapshot = With((Supply, 2), (D, MakeRoom.LastStep));

        var entries = MakeRoom.Plan(snapshot, catalog, held: _ => 1);

        Assert.All(entries, e => Assert.Equal(RoomGroup.NeedsItem, e.Group));
        Assert.All(entries, e => Assert.Equal((Ore, 1), (e.Item, e.Held)));

        // A stored character's items can't be counted: the delivery is finished at its last step, the other says nothing.
        var stored = Assert.Single(MakeRoom.Plan(snapshot, catalog, held: _ => null));
        Assert.Equal((D, RoomGroup.FinishNow), (stored.Quest.RowId, stored.Group));
        Assert.Equal(stored, Assert.Single(MakeRoom.Plan(snapshot, catalog)));
    }

    [Fact]
    public void Items_for_some_jobs_only_count_together_since_one_of_them_is_the_character_s()
    {
        var catalog = JournalCatalog();
        var snapshot = With((Gatherer, 2));

        // A miner holding the ore lacks the botanist's log, which isn't theirs to hand in.
        Assert.Empty(MakeRoom.Plan(snapshot, catalog, held: item => item.ItemId == MinerOre.ItemId ? 1 : 0));

        var lacking = Assert.Single(MakeRoom.Plan(snapshot, catalog, held: _ => 0));
        Assert.Equal((RoomGroup.NeedsItem, MinerOre, 0), (lacking.Group, lacking.Item, lacking.Held));
    }

    [Fact]
    public void Safe_to_drop_is_strictly_the_first_step_of_a_quest_that_can_be_taken_again_for_nothing()
    {
        var catalog = JournalCatalog();
        bool Safe(uint rowId, byte sequence) => MakeRoom.IsSafeToDrop(catalog.GetByRowId(rowId)!, Accepted(rowId, sequence));

        Assert.True(Safe(A, 0));
        Assert.True(Safe(A, 1));
        Assert.False(Safe(A, 2));
        Assert.False(Safe(Msq, 1));
        Assert.False(Safe(Weekly, 1));
        Assert.False(Safe(Daily, 0));
        Assert.False(Safe(Tribal, 1));
        Assert.False(Safe(Seasonal, 1));
        Assert.False(Safe(NoGiver, 1));
    }

    [Fact]
    public void A_quest_on_step_1_that_needs_a_duty_shows_with_its_duty_and_still_wears_safe_to_drop()
    {
        var catalog = JournalCatalog();
        var entry = Assert.Single(MakeRoom.Plan(With((SoloDuty, 1)), catalog, Duties));
        Assert.Equal((RoomGroup.NeedsDuty, true), (entry.Group, entry.SafeToDrop));
    }

    [Fact]
    public void A_main_scenario_quest_at_its_last_step_is_finished_now_since_handing_it_in_frees_its_slot()
    {
        var catalog = JournalCatalog();
        var entry = Assert.Single(MakeRoom.Plan(With((Msq, MakeRoom.LastStep)), catalog));
        Assert.Equal((RoomGroup.FinishNow, false), (entry.Group, entry.SafeToDrop));
    }
}
