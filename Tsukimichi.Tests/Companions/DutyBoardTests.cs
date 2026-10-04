using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// The Duties board (feature plan v7 N4): why each roulette is closed (a roulette that asks for every duty, one that
/// asks for a few, the level and the expansion), the duties unlocked but never cleared, and what the capture reads.
/// </summary>
public class DutyBoardTests
{
    private static readonly RouletteInfo Leveling = new(1, "Duty Roulette: Leveling", DutyRoulettes.Leveling, false, 16, 0, 0, 4) { ShortName = "Leveling" };
    private static readonly RouletteInfo LevelCap = new(8, "Duty Roulette: Level Cap Dungeons", DutyRoulettes.LevelCap, true, 100, 5, 720, 2) { ShortName = "Level Cap Dungeons" };
    private static readonly RouletteInfo MainScenario = new(3, "Duty Roulette: Main Scenario", DutyRoulettes.MainScenario, false, 50, 0, 42, 6);
    private static readonly RouletteInfo Alliance = new(15, "Duty Roulette: Alliance Raids", DutyRoulettes.AllianceRaids, false, 50, 0, 70, 8);
    private static readonly RouletteInfo Mentor = new(9, "Duty Roulette: Mentor", DutyRoulettes.Mentor, true, 100, 5, 755, 10);

    private static DutyRunInfo Duty(uint id, uint type, DutyRoulettes roulettes, byte level, string? name = null) =>
        new(id, id + 1000, id + 2000, type, name ?? $"Duty {id}", false, false) { Roulettes = roulettes, LevelRequired = level, SortKey = (ushort)id };

    // Leveling: three dungeons; Level Cap: two of them; Main Scenario: three duties; one alliance raid; a trial in no roulette.
    private static readonly DutyRunInfo Sastasha = Duty(4, DutyRunInfo.Dungeons, DutyRoulettes.Leveling | DutyRoulettes.Mentor, 15, "Sastasha");
    private static readonly DutyRunInfo TamTara = Duty(2, DutyRunInfo.Dungeons, DutyRoulettes.Leveling | DutyRoulettes.Mentor, 16, "the Tam-Tara Deepcroft");
    private static readonly DutyRunInfo Copperbell = Duty(3, DutyRunInfo.Dungeons, DutyRoulettes.Leveling, 17, "Copperbell Mines");
    private static readonly DutyRunInfo CapA = Duty(100, DutyRunInfo.Dungeons, DutyRoulettes.LevelCap, 100, "Cap A");
    private static readonly DutyRunInfo CapB = Duty(101, DutyRunInfo.Dungeons, DutyRoulettes.LevelCap, 100, "Cap B");
    private static readonly DutyRunInfo Castrum = Duty(15, DutyRunInfo.Dungeons, DutyRoulettes.MainScenario, 50);
    private static readonly DutyRunInfo Praetorium = Duty(16, DutyRunInfo.Dungeons, DutyRoulettes.MainScenario, 50);
    private static readonly DutyRunInfo Porta = Duty(17, DutyRunInfo.Trials, DutyRoulettes.MainScenario, 50);
    private static readonly DutyRunInfo Labyrinth = Duty(92, DutyRunInfo.Raids, DutyRoulettes.AllianceRaids, 50);
    private static readonly DutyRunInfo Ifrit = Duty(56, DutyRunInfo.Trials, DutyRoulettes.None, 20, "the Bowl of Embers");
    private static readonly DutyRunInfo Ultimate = Duty(280, DutyRunInfo.UltimateRaids, DutyRoulettes.None, 70, "the Unending Coil of Bahamut (Ultimate)");
    private static readonly DutyRunInfo Hunt = Duty(900, 9, DutyRoulettes.None, 1, "a treasure hunt");

    private static readonly DutyRunIndex Index = DutyRunIndex.From(
        [Sastasha, TamTara, Copperbell, CapA, CapB, Castrum, Praetorium, Porta, Labyrinth, Ifrit, Ultimate, Hunt],
        [LevelCap, Leveling, MainScenario, Alliance, Mentor]);

    private static CharacterSnapshot With(short level, byte expansion, uint[] unlocked, uint[] cleared) =>
        Fixture.Snapshot() with
        {
            JobLevels = Fixture.Levels((Fixture.Gladiator, level)),
            MaxExpansion = expansion,
            DutyRecords = new DutyRecordCapture(0, unlocked.Select(d => d + 1000).Order().ToArray(), cleared.Select(d => d + 1000).Order().ToArray()),
        };

    private static RouletteLine Line(DutyBoardModel board, RouletteInfo roulette) => Assert.Single(board.Roulettes, r => r.Roulette == roulette);

    [Fact]
    public void Roulettes_keep_the_Duty_Finder_order_and_leave_Mentor_out()
    {
        Assert.Equal([LevelCap, Leveling, MainScenario, Alliance, Mentor], Index.Roulettes);
        var board = DutyBoard.Build(Index, With(100, 5, [], []));
        Assert.Equal([LevelCap, Leveling, MainScenario, Alliance], board.Roulettes.Select(r => r.Roulette));
    }

    [Fact]
    public void Level_Cap_needs_every_dungeon_and_names_each_one_still_locked()
    {
        var board = DutyBoard.Build(Index, With(100, 5, [100], [100]), condition => condition == 101 ? [Fixture.Quest(70500, "Unlock Cap B")] : []);
        var line = Line(board, LevelCap);
        Assert.Equal(RouletteLock.NeedsDuties, line.Lock);
        Assert.True(line.NeedsEvery);
        Assert.Equal(1, line.Unlocked);
        Assert.Equal(2, line.Needed);
        Assert.Equal(1, line.Left);
        var missing = Assert.Single(line.Missing);
        Assert.Same(CapB, missing.Duty);
        Assert.Equal("Unlock Cap B", Assert.Single(missing.UnlockQuests).Name);

        var open = DutyBoard.Build(Index, With(100, 5, [100, 101], []));
        Assert.Equal(RouletteLock.Open, Line(open, LevelCap).Lock);
        Assert.Empty(Line(open, LevelCap).Missing);
        Assert.DoesNotContain(Line(open, LevelCap), open.WithSomethingLeft);
    }

    [Fact]
    public void Leveling_opens_with_two_dungeons_and_lists_every_one_left()
    {
        var one = Line(DutyBoard.Build(Index, With(20, 5, [4], [4])), Leveling);
        Assert.Equal(RouletteLock.NeedsDuties, one.Lock);
        Assert.False(one.NeedsEvery);
        Assert.Equal(2, one.Needed);
        Assert.Equal(1, one.Left);
        Assert.Equal([TamTara, Copperbell], one.Missing.Select(m => m.Duty));

        // Open, with one dungeon still not unlocked: what is left in it.
        var open = DutyBoard.Build(Index, With(20, 5, [4, 3], []));
        Assert.Equal(RouletteLock.Open, Line(open, Leveling).Lock);
        Assert.Same(TamTara, Assert.Single(Line(open, Leveling).Missing).Duty);
        Assert.Contains(Line(open, Leveling), open.WithSomethingLeft);
        Assert.DoesNotContain(Line(open, Leveling), open.Locked);
    }

    [Fact]
    public void Main_Scenario_needs_all_three_and_a_raid_roulette_one()
    {
        var board = DutyBoard.Build(Index, With(50, 5, [15, 16, 92], []));
        var msq = Line(board, MainScenario);
        Assert.Equal(RouletteLock.NeedsDuties, msq.Lock);
        Assert.Equal(3, msq.Needed);
        Assert.True(msq.NeedsEvery);
        Assert.Same(Porta, Assert.Single(msq.Missing).Duty);
        Assert.Equal(RouletteLock.Open, Line(board, Alliance).Lock);
    }

    [Fact]
    public void Level_and_expansion_come_before_duties()
    {
        var low = DutyBoard.Build(Index, With(15, 5, [], []));
        Assert.Equal(RouletteLock.NeedsLevel, Line(low, Leveling).Lock);
        Assert.Equal(3, Line(low, Leveling).Missing.Count);

        var trial = DutyBoard.Build(Index, With(100, 4, [], []));
        Assert.Equal(RouletteLock.NeedsExpansion, Line(trial, LevelCap).Lock);

        // An entitlement the capture did not read (0) never blocks.
        Assert.Equal(RouletteLock.NeedsDuties, Line(DutyBoard.Build(Index, With(100, 0, [], [])), LevelCap).Lock);
    }

    [Fact]
    public void A_cleared_duty_counts_as_unlocked()
    {
        var board = DutyBoard.Build(Index, With(20, 5, [], [4, 2]));
        Assert.Equal(RouletteLock.Open, Line(board, Leveling).Lock);
    }

    [Fact]
    public void Never_cleared_lists_unlocked_duties_per_category_lowest_first()
    {
        var board = DutyBoard.Build(Index, With(100, 5, [4, 2, 3, 56, 280, 900], [2]));
        Assert.Equal([DutyRunInfo.Dungeons, DutyRunInfo.Trials, DutyRunInfo.Raids], board.NeverCleared.Select(g => g.ContentType));
        Assert.Equal([Sastasha, Copperbell], board.NeverCleared[0].Duties);
        Assert.Equal([Ifrit], board.NeverCleared[1].Duties);

        // The Ultimate raid files with the raids; a treasure hunt is not on the list.
        Assert.Equal([Ultimate], board.NeverCleared[2].Duties);

        var allClear = DutyBoard.Build(Index, With(100, 5, [4], [4]));
        Assert.Empty(allClear.NeverCleared);
    }

    [Fact]
    public void Without_records_the_board_is_empty()
    {
        Assert.Same(DutyBoardModel.Empty, DutyBoard.Build(Index, Fixture.Snapshot()));
        Assert.Same(DutyBoardModel.Empty, DutyBoard.Build(Index, null));
        Assert.Same(DutyBoardModel.Empty, DutyBoard.Build(DutyRunIndex.Empty, With(100, 5, [], [])));
    }

    [Fact]
    public void The_capture_reads_roulette_duties_and_the_never_cleared_categories()
    {
        var watched = DutyBoard.Watched(Index);
        Assert.Equal(watched.Order(), watched);
        Assert.Contains(Sastasha.InstanceContentId, watched);
        Assert.Contains(Ifrit.InstanceContentId, watched);
        Assert.Contains(Ultimate.InstanceContentId, watched);
        Assert.DoesNotContain(Hunt.InstanceContentId, watched);
    }

    [Fact]
    public void Minimum_unlocked_follows_the_wiki_numbers()
    {
        Assert.Equal(2, DutyBoard.MinimumUnlocked(Leveling, 40));
        Assert.Equal(40, DutyBoard.MinimumUnlocked(LevelCap, 40));
        Assert.Equal(3, DutyBoard.MinimumUnlocked(MainScenario, 3));
        Assert.Equal(1, DutyBoard.MinimumUnlocked(Alliance, 18));
        Assert.Equal(1, DutyBoard.MinimumUnlocked(Leveling, 1));
    }
}
