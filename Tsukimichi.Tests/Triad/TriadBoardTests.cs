using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Sources;
using Tsukimichi.Core.Triad;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Triad;

/// <summary>
/// The Triple Triad card (plan v7, 1.21.0 P6): which opponents play you (any and all joins), which wait on a quest and
/// which quest the card names, what the beaten and card records do, the spoiler shield over opponents, the chips, and
/// the route over the unlock quests and the opponents.
/// </summary>
public sealed class TriadBoardTests
{
    private const uint CaughtInTheAct = 100;
    private const uint Criminal = 101;
    private const uint AllTheLittleAngels = 102;
    private const uint Maelstrom = 110;
    private const uint TwinAdder = 111;
    private const uint Flames = 112;
    private const uint Honoring = 120;
    private const uint Engineering = 121;
    private const uint Endwalker = 130;

    private static readonly QuestCatalog Catalog = Fixture.Catalog(
        Fixture.Quest(CaughtInTheAct, "Caught in the Act") with { Level = 54 },
        Fixture.Quest(Criminal, "Criminal Phrenology") with { Level = 62 },
        Fixture.Quest(AllTheLittleAngels, "All the Little Angels") with { Level = 61 },
        Fixture.Quest(Maelstrom, "Till Sea Swallows All") with { Level = 20 },
        Fixture.Quest(TwinAdder, "Wood's Will Be Done") with { Level = 20 },
        Fixture.Quest(Flames, "For Coin and Country") with { Level = 20 },
        Fixture.Quest(Honoring, "Honoring the Past") with { Level = 55 },
        Fixture.Quest(Engineering, "An Engineering Enterprise") with { Level = 54 },
        Fixture.Quest(Endwalker, "Endwalker") with { Level = 90 });

    private static WorldSpot At(string zone, uint territory = 1) => new(territory, zone, 10f, 20f, 7.8f, 10.8f);

    private static readonly TriadOpponent Memeroon = new(2293762, 1, "Memeroon", At("Upper La Noscea"), Prereq.None, [37, 14]);
    private static readonly TriadOpponent Elaisse = new(2293806, 2, "Elaisse", At("The Pillars"), new Prereq([CaughtInTheAct], JoinKind.All), [102, 142]);
    private static readonly TriadOpponent Gyoei = new(2293829, 3, "Gyoei", At("Yanxia"), new Prereq([Criminal], JoinKind.All), [183, 248]);
    private static readonly TriadOpponent Swift = new(2293788, 4, "Swift", At("Ul'dah - Steps of Nald"), new Prereq([TwinAdder, Maelstrom, Flames], JoinKind.Any), [67, 59, 40]);
    private static readonly TriadOpponent Laniaitte = new(2293808, 5, "Laniaitte", At("The Sea of Clouds"), new Prereq([Engineering, Honoring], JoinKind.All), [103, 104]);
    private static readonly TriadOpponent Celia = new(2293879, 6, "Celia", At("Old Sharlayan"), new Prereq([Endwalker], JoinKind.All), [338, 341]);
    private static readonly TriadOpponent Tournament = new(2293863, 7, "Prideful Stag", null, Prereq.None, []);

    private static readonly TriadOpponents Index = new([Memeroon, Elaisse, Gyoei, Swift, Laniaitte, Celia, Tournament]);

    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static Dictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] states)
    {
        var map = new Dictionary<uint, QuestEvaluation>();
        foreach (var quest in Catalog.All)
        {
            map[quest.RowId] = Eval(QuestState.Blocked);
        }

        foreach (var (rowId, state) in states)
        {
            map[rowId] = Eval(state);
        }

        return map;
    }

    private static TriadRecordCapture Records(uint[] beaten, uint[] cards) => new(Index.Fingerprint, beaten.Order().ToArray(), cards.Order().ToArray());

    [Fact]
    public void The_index_reverse_maps_quests_to_the_opponents_they_open()
    {
        Assert.Equal(7, Index.Count);
        Assert.Equal(5, Index.QuestGatedCount);
        Assert.Equal([Elaisse], Index.OpenedBy(CaughtInTheAct));
        Assert.Equal([Swift], Index.OpenedBy(Flames));
        Assert.Equal([Laniaitte], Index.OpenedBy(Honoring));
        Assert.Empty(Index.OpenedBy(AllTheLittleAngels));
        Assert.Equal(Index.WatchedCards.Order(), Index.WatchedCards);
        Assert.Equal(Index.WatchedCards.Distinct().Count(), Index.WatchedCards.Count);
    }

    [Fact]
    public void An_any_join_opens_with_one_quest_and_an_all_join_with_every_quest()
    {
        Assert.True(TriadBoard.IsOpen(Prereq.None, States()));
        Assert.True(TriadBoard.IsOpen(Swift.Gate, States((Flames, QuestState.Completed))));
        Assert.False(TriadBoard.IsOpen(Swift.Gate, States((Flames, QuestState.Ready))));
        Assert.False(TriadBoard.IsOpen(Laniaitte.Gate, States((Engineering, QuestState.Completed))));
        Assert.True(TriadBoard.IsOpen(Laniaitte.Gate, States((Engineering, QuestState.Completed), (Honoring, QuestState.Completed))));
    }

    [Fact]
    public void Locked_opponents_name_the_quest_closest_to_done_with_Ready_first()
    {
        var board = TriadBoard.Build(Index, Catalog, States(
            (Maelstrom, QuestState.Completed),
            (CaughtInTheAct, QuestState.Ready),
            (Engineering, QuestState.Completed),
            (Honoring, QuestState.Accepted)), Records([], []));

        // Swift is open (one Grand Company quest done); Memeroon has no gate; the tournament row has no place.
        Assert.Equal(["Memeroon", "Swift"], board.PlaysYou.Select(r => r.Opponent.Name).Order());
        Assert.DoesNotContain(board.PlaysYou.Concat(board.Locked), r => r.Opponent == Tournament);

        // Elaisse (Ready) then Laniaitte (the quest left is in the journal) then Gyoei and Celia (Blocked, by level).
        Assert.Equal(["Elaisse", "Laniaitte", "Gyoei", "Celia"], board.Locked.Select(r => r.Opponent.Name));
        Assert.Equal(CaughtInTheAct, board.Locked[0].Quest?.RowId);
        Assert.Equal(Honoring, board.Locked[1].Quest?.RowId);
        Assert.Equal(4, board.ToUnlock);
    }

    [Fact]
    public void An_any_join_names_the_alternative_on_the_characters_path()
    {
        var states = States((Maelstrom, QuestState.Foreclosed), (TwinAdder, QuestState.Foreclosed), (Flames, QuestState.Blocked));
        Assert.Equal(Flames, TriadBoard.ShownQuest(Swift.Gate, Catalog, states)?.RowId);

        // Every alternative on another path: Swift can never play this character and leaves the card.
        var gone = States((Maelstrom, QuestState.Foreclosed), (TwinAdder, QuestState.Foreclosed), (Flames, QuestState.Foreclosed));
        var board = TriadBoard.Build(Index, Catalog, gone, null);
        Assert.DoesNotContain(board.PlaysYou.Concat(board.Locked), r => r.Opponent == Swift);
    }

    [Fact]
    public void Records_say_beaten_and_cards_left_and_a_finished_opponent_leaves_the_list()
    {
        var states = States((Maelstrom, QuestState.Completed));
        var board = TriadBoard.Build(Index, Catalog, states, Records([Memeroon.ResidentId], [37, 14, 67]));

        // Memeroon: beaten, every card owned: counted in Done only. Swift: not beaten, two cards left.
        var swift = Assert.Single(board.PlaysYou);
        Assert.Equal(Swift, swift.Opponent);
        Assert.False(swift.Beaten);
        Assert.Equal(2, swift.CardsLeft);
        Assert.Equal(1, board.Done);
        Assert.True(board.HasRecords);
        Assert.Equal(1 + 4, board.WithCardsLeft);

        // Beaten but a card left: still listed, after the opponents not beaten.
        var partly = TriadBoard.Build(Index, Catalog, states, Records([Memeroon.ResidentId], [37]));
        Assert.Equal(["Swift", "Memeroon"], partly.PlaysYou.Select(r => r.Opponent.Name));
        Assert.Equal(1, partly.PlaysYou[1].CardsLeft);
    }

    [Fact]
    public void Records_read_against_another_list_or_none_leave_beaten_and_cards_unknown()
    {
        var states = States();
        var stale = TriadBoard.Build(Index, Catalog, states, new TriadRecordCapture(Index.Fingerprint + 1, [Memeroon.ResidentId], [37, 14]));
        var none = TriadBoard.Build(Index, Catalog, states, null);
        foreach (var board in new[] { stale, none })
        {
            Assert.False(board.HasRecords);
            var row = Assert.Single(board.PlaysYou);
            Assert.Null(row.Beaten);
            Assert.Equal(-1, row.CardsLeft);
            Assert.Equal(0, board.Done);
        }
    }

    [Fact]
    public void An_opponent_past_the_story_point_is_masked_and_listed_last_among_the_locked()
    {
        // Endwalker is masked: Celia is past the story point. An all join masks with any quest masked.
        var masked = new HashSet<uint> { Endwalker, Honoring };
        var board = TriadBoard.Build(Index, Catalog, States((CaughtInTheAct, QuestState.Ready)), null, masked.Contains, zone => zone == "Yanxia");
        // Named first (Ready, then Blocked), then the masked ones by the level of their quest.
        Assert.Equal(["Elaisse", "Swift", "Laniaitte", "Gyoei", "Celia"], board.Locked.Select(r => r.Opponent.Name));
        Assert.Equal([false, false, true, true, true], board.Locked.Select(r => r.Masked));
        Assert.Equal(["Elaisse", "Swift"], board.Named.Select(r => r.Opponent.Name));

        // An any join is masked only when every alternative is.
        Assert.False(TriadBoard.IsMasked(Swift.Gate, new HashSet<uint> { Maelstrom, TwinAdder }.Contains));
        Assert.True(TriadBoard.IsMasked(Swift.Gate, new HashSet<uint> { Maelstrom, TwinAdder, Flames }.Contains));
        Assert.False(TriadBoard.IsMasked(Prereq.None, _ => true));

        // An open opponent standing in a masked zone cannot be reached yet: it waits with the locked ones, masked.
        var zoneAhead = TriadBoard.Build(Index, Catalog, States(), null, zoneMasked: zone => zone == "Upper La Noscea");
        Assert.Empty(zoneAhead.PlaysYou);
        Assert.Contains(zoneAhead.Locked, r => r.Opponent == Memeroon && r.Masked && r.Quest is null);
    }

    [Fact]
    public void Chips_keep_a_row_when_any_chip_it_matches_is_on()
    {
        var board = TriadBoard.Build(Index, Catalog, States((Maelstrom, QuestState.Completed)), Records([], [37, 14, 67, 59, 40, 102]));
        var memeroon = board.PlaysYou.Single(r => r.Opponent == Memeroon);
        var elaisse = board.Locked.Single(r => r.Opponent == Elaisse);
        Assert.Equal(0, memeroon.CardsLeft);
        Assert.Equal(1, elaisse.CardsLeft);

        Assert.True(TriadChips.All.Keeps(memeroon));
        Assert.False(new TriadChips(PlaysYou: false, Locked: true, CardsLeft: true).Keeps(memeroon));
        Assert.True(new TriadChips(PlaysYou: false, Locked: false, CardsLeft: true).Keeps(elaisse));
        Assert.False(new TriadChips(PlaysYou: true, Locked: false, CardsLeft: false).Keeps(elaisse));
    }

    [Fact]
    public void The_route_honours_any_and_all_joins_and_ends_at_the_opponents()
    {
        var target = RouteTarget.ForTriad([Swift, Laniaitte, Elaisse], "3 Triple Triad opponents");
        Assert.Equal(RouteTargetKind.TriadNpc, target.Kind);
        Assert.Equal(RouteTarget.TriadCardIcon, target.Icon);

        // Swift: one part, the three Grand Company quests as variants. Laniaitte: one part per quest, one label.
        Assert.Equal(4, target.Parts.Count);
        Assert.Equal([TwinAdder, Maelstrom, Flames], target.Parts[0].QuestRowIds);
        Assert.Equal(["Laniaitte", "Laniaitte"], target.Parts.Skip(1).Take(2).Select(p => p.Label));
        Assert.Equal(["Swift", "Laniaitte", "Elaisse"], target.TriadStops.Select(s => s.Name));
        Assert.Equal("The Pillars", target.TriadStops[2].Zone);

        // The route takes one Grand Company quest (the cheapest), both of Laniaitte's and Elaisse's.
        var states = States((Maelstrom, QuestState.Ready));
        var route = UnlockRoute.Build(target, Catalog, states);
        var steps = route.Steps.Select(s => s.RowId).ToArray();
        Assert.Single(steps, id => id is Maelstrom or TwinAdder or Flames);
        Assert.Contains(Engineering, steps);
        Assert.Contains(Honoring, steps);
        Assert.Contains(CaughtInTheAct, steps);

        // The followed route keeps its stops.
        var saved = SavedRoute.From(target, 1).ToTarget();
        Assert.Equal(target.TriadStops, saved.TriadStops);
        Assert.Equal(target.QuestRowIds, saved.QuestRowIds);
    }

    [Fact]
    public void A_triad_record_change_is_saved_without_resolving_a_quest()
    {
        var a = Fixture.Snapshot() with { TriadRecords = new TriadRecordCapture(5, [1, 2], [37]) };
        Assert.True(SnapshotDiff.Compute(a, a with { TriadRecords = new TriadRecordCapture(5, [1, 2], [37]) }).IsEmpty);
        var diff = SnapshotDiff.Compute(a, a with { TriadRecords = new TriadRecordCapture(5, [1, 2], [14, 37]) });
        Assert.True(diff.RecordsChanged);
        Assert.Empty(diff.ChangedQuestIds);
    }
}
