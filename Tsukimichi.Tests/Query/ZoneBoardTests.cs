using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The zones board (plan v7, 1.21.0 P7): zones grouped by expansion with what is left in each, the kind chips, the
/// sorts, the group that fits the job's level, and the spoiler shield over expansions and zones.
/// </summary>
public sealed class ZoneBoardTests
{
    private const byte Shadowbringers = 3;
    private const byte Endwalker = 4;
    private const byte Dawntrail = 5;

    private static readonly UnlockZone Lakeland = new(813, "Lakeland", "Norvrandt", 1, Shadowbringers, 0, 10);
    private static readonly UnlockZone Kholusia = new(814, "Kholusia", "Norvrandt", 2, Shadowbringers, 0, 11);
    private static readonly UnlockZone Labyrinthos = new(956, "Labyrinthos", "Ilsabard", 3, Endwalker, 0, 20);
    private static readonly UnlockZone Thavnair = new(957, "Thavnair", "Ilsabard", 4, Endwalker, 0, 21);
    private static readonly UnlockZone Garlemald = new(958, "Garlemald", "Ilsabard", 5, Endwalker, 0, 22);
    private static readonly UnlockZone Elpis = new(961, "Elpis", "Ilsabard", 6, Endwalker, 0, 24);
    private static readonly UnlockZone Urqopacha = new(1187, "Urqopacha", "Yok Tural", 7, Dawntrail, 0, 30);

    private static readonly UnlockZone[] Zones = [Lakeland, Kholusia, Labyrinthos, Thavnair, Garlemald, Elpis, Urqopacha];

    private static uint next = 1000;

    private static QuestRecord Q(UnlockZone zone, byte level, string? added = null) => Fixture.Quest(next++) with
    {
        Level = level,
        Issuer = new Issuer(1, "Giver", zone.TerritoryId, zone.MapId, 0f, 0f, 0f),
        AddedIn = added ?? string.Empty,
    };

    // Lakeland 70-72: one Ready, one done. Kholusia 71: done. Labyrinthos 80-82: two Ready (one a blue). Thavnair 80-89:
    // one Ready side story, one Blocked reward, one blue Blocked. Garlemald 83: Blocked only. Elpis 86: Ready, new.
    // Urqopacha 90: Ready.
    private static readonly QuestRecord LakelandReady = Q(Lakeland, 70);
    private static readonly QuestRecord LakelandDone = Q(Lakeland, 72);
    private static readonly QuestRecord KholusiaDone = Q(Kholusia, 71);
    private static readonly QuestRecord LabyrinthosBlue = Q(Labyrinthos, 80);
    private static readonly QuestRecord LabyrinthosReady = Q(Labyrinthos, 82);
    private static readonly QuestRecord ThavnairStory = Q(Thavnair, 80);
    private static readonly QuestRecord ThavnairReward = Q(Thavnair, 85);
    private static readonly QuestRecord ThavnairBlue = Q(Thavnair, 89);
    private static readonly QuestRecord GarlemaldBlocked = Q(Garlemald, 83);
    private static readonly QuestRecord ElpisNew = Q(Elpis, 86, "7.5");
    private static readonly QuestRecord UrqopachaReady = Q(Urqopacha, 90);
    private static readonly QuestRecord Repeatable = Q(Garlemald, 90) with { IsRepeatable = true };

    private static readonly QuestCatalog Catalog = Fixture.Catalog(
        LakelandReady, LakelandDone, KholusiaDone, LabyrinthosBlue, LabyrinthosReady, ThavnairStory, ThavnairReward,
        ThavnairBlue, GarlemaldBlocked, ElpisNew, UrqopachaReady, Repeatable);

    private static readonly Dictionary<uint, QuestEvaluation> States = new()
    {
        [LakelandReady.RowId] = Eval(QuestState.Ready),
        [LakelandDone.RowId] = Eval(QuestState.Completed),
        [KholusiaDone.RowId] = Eval(QuestState.Completed),
        [LabyrinthosBlue.RowId] = Eval(QuestState.Ready),
        [LabyrinthosReady.RowId] = Eval(QuestState.ReadyOnOtherJob),
        [ThavnairStory.RowId] = Eval(QuestState.Ready),
        [ThavnairReward.RowId] = Eval(QuestState.Blocked),
        [ThavnairBlue.RowId] = Eval(QuestState.Blocked),
        [GarlemaldBlocked.RowId] = Eval(QuestState.Blocked),
        [ElpisNew.RowId] = Eval(QuestState.Ready),
        [UrqopachaReady.RowId] = Eval(QuestState.Ready),
        [Repeatable.RowId] = Eval(QuestState.Ready),
    };

    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static ZoneQuestKinds KindsOf(QuestRecord quest) => new(
        Blue: quest == LabyrinthosBlue || quest == ThavnairBlue,
        SideStory: quest == ThavnairStory,
        Reward: quest == ThavnairReward);

    private static IReadOnlyList<ZoneExpansion> Board(ZoneKinds kinds = ZoneKinds.All, ZoneSort sort = ZoneSort.LevelFit, int level = 90, bool otherJob = true, byte reach = byte.MaxValue, Func<string, bool>? masked = null) =>
        ZoneBoard.Build(Zones, Catalog, States, KindsOf, kinds, sort, level, otherJob, reach, masked, q => q.AddedIn == "7.5");

    private static ZoneLine Line(IReadOnlyList<ZoneExpansion> board, UnlockZone zone) =>
        board.SelectMany(g => g.Zones.Concat(g.Empty)).Single(l => l.Zone == zone);

    [Fact]
    public void Zones_count_what_is_left_by_kind_with_their_level_span()
    {
        var board = Board();
        var thavnair = Line(board, Thavnair);
        Assert.Equal((80, 89), (thavnair.MinLevel, thavnair.MaxLevel));
        Assert.Equal((1, 1, 1, 1), (thavnair.Ready, thavnair.Blues, thavnair.SideStories, thavnair.Rewards));

        // Ready on another job counts with Nearby's setting on, not with it off.
        Assert.Equal(2, Line(board, Labyrinthos).Ready);
        Assert.Equal(1, Line(Board(otherJob: false), Labyrinthos).Ready);

        // The span counts done quests too; a repeatable counts nowhere.
        Assert.Equal((70, 72), (Line(board, Lakeland).MinLevel, Line(board, Lakeland).MaxLevel));
        Assert.Equal((83, 83), (Line(board, Garlemald).MinLevel, Line(board, Garlemald).MaxLevel));
        Assert.True(Line(board, Elpis).IsNew);
        Assert.False(Line(board, Thavnair).IsNew);
    }

    [Fact]
    public void Zones_with_nothing_left_of_the_chips_on_fold_away()
    {
        var board = Board();
        var endwalker = board.Single(g => g.Expansion == Endwalker);
        Assert.Equal([Garlemald], endwalker.Empty.Select(l => l.Zone));
        Assert.DoesNotContain(endwalker.Zones, l => l.Zone == Garlemald);
        Assert.Equal([Kholusia], board.Single(g => g.Expansion == Shadowbringers).Empty.Select(l => l.Zone));

        // Only Rewards on: Thavnair alone has something left in Endwalker.
        var rewards = Board(ZoneKinds.Rewards).Single(g => g.Expansion == Endwalker);
        Assert.Equal([Thavnair], rewards.Zones.Select(l => l.Zone));
        Assert.Equal(3, rewards.Empty.Count);
        Assert.Equal(4, rewards.ZoneCount);
    }

    [Fact]
    public void The_group_fitting_the_jobs_level_comes_first_then_the_nearest()
    {
        var at85 = Board(level: 85);
        Assert.Equal([Endwalker, Shadowbringers, Dawntrail], at85.Select(g => g.Expansion));
        Assert.True(at85[0].Fits);
        Assert.False(at85[1].Fits);

        var at71 = Board(level: 71);
        Assert.Equal([Shadowbringers, Endwalker, Dawntrail], at71.Select(g => g.Expansion));

        // Level 90 is past Endwalker's span here (80-89) and inside Dawntrail's (90).
        Assert.Equal(Dawntrail, Board(level: 90)[0].Expansion);
    }

    [Fact]
    public void Sorts_order_zones_by_level_fit_ready_count_or_the_games_order()
    {
        var fit = Board(sort: ZoneSort.LevelFit, level: 86).Single(g => g.Expansion == Endwalker).Zones.Select(l => l.Zone);
        Assert.Equal([Thavnair, Elpis, Labyrinthos], fit);

        var ready = Board(sort: ZoneSort.ReadyFirst).Single(g => g.Expansion == Endwalker).Zones.Select(l => l.Zone);
        Assert.Equal([Labyrinthos, Thavnair, Elpis], ready);

        var story = Board(sort: ZoneSort.StoryOrder).Single(g => g.Expansion == Endwalker).Zones.Select(l => l.Zone);
        Assert.Equal([Labyrinthos, Thavnair, Elpis], story);
        Assert.Equal(4, Board().Single(g => g.Expansion == Endwalker).Ready);
    }

    [Fact]
    public void A_set_aside_quest_counts_for_no_kind_but_keeps_its_level_in_the_span()
    {
        // Thavnair's Ready side story and its blue set aside (P4): its Ready, side story and blue leave the counts.
        var setAside = new HashSet<uint> { ThavnairStory.RowId, ThavnairBlue.RowId };
        var board = ZoneBoard.Build(Zones, Catalog, States, KindsOf, ZoneKinds.All, ZoneSort.LevelFit, 90, setAside: setAside);
        var thavnair = Line(board, Thavnair);
        Assert.Equal((0, 0, 0, 1), (thavnair.Ready, thavnair.Blues, thavnair.SideStories, thavnair.Rewards));
        Assert.Equal((80, 89), (thavnair.MinLevel, thavnair.MaxLevel));

        // With only the set-aside kinds on, Thavnair has nothing left and folds away.
        var stories = ZoneBoard.Build(Zones, Catalog, States, KindsOf, ZoneKinds.SideStories, ZoneSort.LevelFit, 90, setAside: setAside);
        Assert.Contains(stories.Single(g => g.Expansion == Endwalker).Empty, l => l.Zone == Thavnair);
        Assert.Equal(3, board.Single(g => g.Expansion == Endwalker).Ready);
    }

    [Fact]
    public void An_expansion_past_the_story_is_masked_whole_and_a_zone_ahead_is_masked_and_last()
    {
        var board = Board(level: 82, reach: Endwalker, masked: zone => zone is "Elpis" or "Urqopacha");
        var dawntrail = board[^1];
        Assert.Equal(Dawntrail, dawntrail.Expansion);
        Assert.True(dawntrail.Masked);
        Assert.False(dawntrail.Fits);

        var endwalker = board.Single(g => g.Expansion == Endwalker);
        Assert.False(endwalker.Masked);
        Assert.True(endwalker.Zones[^1].Masked);
        Assert.Equal(Elpis, endwalker.Zones[^1].Zone);
        Assert.DoesNotContain(endwalker.Empty, l => l.Masked);

        // A masked zone's Ready is not counted in the group's total.
        Assert.Equal(3, endwalker.Ready);
    }
}
