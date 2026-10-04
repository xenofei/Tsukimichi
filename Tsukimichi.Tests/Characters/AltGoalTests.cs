using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Characters;

/// <summary>
/// Alt goals (plan v7, 1.21.0 N11): what each of the four goals leaves, over the frozen catalog with the shipped patch
/// data where the goal reads the game's data (the story up to a patch), and over small catalogs for the rest.
/// </summary>
public sealed class AltGoalTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static readonly Lazy<CatalogBundle> DatedBundle = new(() =>
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var patches = QuestPatches.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), QuestPatches.FileName));
        return CatalogFixtureFile.Read(FixtureCatalog.CatalogPath(), JournalFiling.Refiled, curated, patches).Bundle;
    });

    private static QuestEvaluation State(QuestState state) => new(state, [], null, null, null);

    /// <summary>Every story quest before <paramref name="at"/> completed, that one Ready, the rest blocked.</summary>
    private static Dictionary<uint, QuestEvaluation> AtStory(QuestCatalog catalog, int at)
    {
        var story = MsqGraph.For(catalog).Story;
        var states = new Dictionary<uint, QuestEvaluation>();
        foreach (var quest in catalog.All)
        {
            states[quest.RowId] = State(QuestState.Blocked);
        }

        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = State(i < at ? QuestState.Completed : i == at ? QuestState.Ready : QuestState.Blocked);
        }

        return states;
    }

    [Fact]
    public void The_story_goal_counts_the_main_scenario_left_to_the_patch()
    {
        var catalog = DatedBundle.Value.Catalog;
        var story = MsqGraph.For(catalog).Story;
        var patches = AltGoals.StoryPatches(catalog);
        Assert.Contains(patches, p => p.Patch == "7.0");
        Assert.Contains(patches, p => p.Patch == "6.0");
        Assert.True(patches.Zip(patches.Skip(1)).All(p => PatchVersion.Compare(p.First.Patch, p.Second.Patch) < 0), "oldest first");

        // At Dawntrail's first quest: the story to 7.0 is every 7.0 quest; to the newest patch it is everything left.
        var at = story.ToList().FindIndex(q => q.Expansion == 5);
        var states = AtStory(catalog, at);
        var to70 = AltGoals.Story(catalog, states, "7.0");
        Assert.Equal(story.Skip(at).TakeWhile(q => !PatchVersion.IsPatch(q.AddedIn) || PatchVersion.Compare(q.AddedIn, "7.0") <= 0).Count(), to70.Left);
        Assert.Equal(1, to70.Doable);
        Assert.Equal(story[at].RowId, to70.Quests[0].RowId);

        var latest = AltGoals.Story(catalog, states, patches[^1].Patch);
        Assert.Equal(MsqLeft.For(catalog, states)!.LeftToLatest, latest.Left);
        Assert.True(latest.Left > to70.Left);

        // A patch already behind the character is reached.
        var to60 = AltGoals.Story(catalog, states, "6.0");
        Assert.True(to60.Reached);
        Assert.Empty(to60.Quests);
    }

    [Fact]
    public void A_story_quest_with_no_known_patch_takes_the_one_before_it()
    {
        var quests = new[]
        {
            Quest(1, "A", section: 0, genre: 1) with { AddedIn = "6.0" },
            Quest(2, "B", section: 0, genre: 1),
            Quest(3, "C", section: 0, genre: 1) with { AddedIn = "7.0" },
            Quest(4, "D", section: 0, genre: 1),
        };

        Assert.Equal(1, AltGoals.StoryEnd(quests, "6.0"));
        Assert.Equal(3, AltGoals.StoryEnd(quests, "7.0"));
        Assert.Equal(-1, AltGoals.StoryEnd(quests, "5.0"));
    }

    [Fact]
    public void The_story_goal_leaves_out_quests_out_of_the_totals()
    {
        // 1.21 Core review: before choosing a Grand Company all three "The Company You Keep" counted, so Left was two
        // too high and Ready listed three. A spare alternative and another path's quest are not left to do.
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Start", section: 0, genre: 1) with { AddedIn = "2.0" },
            Quest(2, "Maelstrom", section: 0, genre: 1) with { AddedIn = "2.0" },
            Quest(3, "Twin Adder", section: 0, genre: 1) with { AddedIn = "2.0" },
            Quest(4, "Immortal Flames", section: 0, genre: 1) with { AddedIn = "2.0" },
            Quest(5, "After", section: 0, genre: 1) with { AddedIn = "2.0" },
        ]);
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = State(QuestState.Completed),
            [2] = State(QuestState.Ready),
            [3] = State(QuestState.Ready) with { IsSpareAlternative = true },
            [4] = State(QuestState.Ready) with { IsSpareAlternative = true },
            [5] = State(QuestState.Blocked),
        };

        var progress = AltGoals.Story(catalog, states, "2.0");
        Assert.Equal(2, progress.Left);
        Assert.Equal([2u, 5u], progress.Quests.Select(static q => q.RowId));
        Assert.Equal(1, progress.Doable);
    }

    [Fact]
    public void The_roulettes_goal_counts_a_shared_duty_once_and_a_level_as_a_level()
    {
        // 1.21 Core review: Left summed Needed - Unlocked per roulette, so a duty in two roulettes counted twice, and a
        // roulette closed only by level added a duty.
        var unlock = Quest(1, "Unlock");
        var duty = new DutyRunInfo(100, 200, 300, DutyRunInfo.Dungeons, "Shared", true, false);
        var leveling = new RouletteInfo(1, "Leveling", DutyRoulettes.Leveling, false, 15, 0, 0, 0);
        var high = new RouletteInfo(2, "High-level", DutyRoulettes.HighLevel, false, 50, 0, 0, 0);
        var expert = new RouletteInfo(3, "Expert", DutyRoulettes.Expert, false, 100, 0, 0, 0);
        var states = new Dictionary<uint, QuestEvaluation> { [1] = State(QuestState.Ready) };
        var shared = new BoardDuty(duty, [unlock]);

        var board = new DutyBoardModel(
        [
            new RouletteLine(leveling, RouletteLock.NeedsDuties, 0, 1, [shared]) { NeedsEvery = true },
            new RouletteLine(high, RouletteLock.NeedsDuties, 0, 1, [shared]) { NeedsEvery = true },
        ], []);
        var progress = AltGoals.Roulettes(states, board);
        Assert.Equal(1, progress.Left);
        Assert.Equal(0, progress.LevelNeeded);

        var level = new DutyBoardModel([new RouletteLine(expert, RouletteLock.NeedsLevel, 4, 4, [])], []);
        var byLevel = AltGoals.Roulettes(states, level);
        Assert.Equal(0, byLevel.Left);
        Assert.Equal(100, byLevel.LevelNeeded);
        Assert.False(byLevel.Reached);
    }

    [Fact]
    public void The_match_goal_is_the_unlock_quests_the_other_has_done()
    {
        var catalog = fixture.Bundle.Catalog;
        var feature = FeaturePresets.Derive(catalog, fixture.Curated);
        var quests = catalog.All.Where(q => feature.Contains(q.RowId) && q.EntersCounts && !q.IsRemoved).Take(6).ToArray();
        var side = catalog.All.First(q => !feature.Contains(q.RowId) && q.EntersCounts && !q.IsRemoved && !FeaturePresets.IsMainScenario(q));

        var mine = catalog.All.ToDictionary(static q => q.RowId, _ => State(QuestState.Blocked));
        var theirs = catalog.All.ToDictionary(static q => q.RowId, _ => State(QuestState.Blocked));
        foreach (var quest in quests.Append(side))
        {
            theirs[quest.RowId] = State(QuestState.Completed);
        }

        // The first two are done on both; one is Ready here, one in the journal; the side quest never counts.
        mine[quests[0].RowId] = State(QuestState.Completed);
        mine[quests[1].RowId] = State(QuestState.Completed);
        mine[quests[2].RowId] = State(QuestState.Ready);
        mine[quests[3].RowId] = State(QuestState.Accepted);

        var progress = AltGoals.Match(catalog, mine, theirs, feature);
        Assert.Equal(4, progress.Left);
        Assert.Equal(2, progress.Doable);
        Assert.Equal(2, progress.Waiting);
        Assert.Equal(quests[2].RowId, progress.Quests[0].RowId);
        Assert.Equal(quests[3].RowId, progress.Quests[1].RowId);
        Assert.DoesNotContain(progress.Quests, q => q.RowId == side.RowId);

        Assert.False(AltGoals.Match(catalog, mine, null, feature).Known);
        Assert.True(AltGoals.Match(catalog, theirs, theirs, feature).Reached);
    }

    [Fact]
    public void The_flying_goal_counts_zones_with_a_current_quest_left()
    {
        var catalog = QuestCatalog.Build([Quest(1, "A"), Quest(2, "B"), Quest(3, "C"), Quest(4, "D")]);
        var zones = new[]
        {
            new AltGoalZone(10, "Zone 10", 4, [1, 2]),
            new AltGoalZone(11, "Zone 11", 4, [3]),
            new AltGoalZone(12, "Zone 12", 5, [4]),
        };
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = State(QuestState.Completed),
            [2] = State(QuestState.Ready),
            [3] = State(QuestState.Completed),
            [4] = State(QuestState.Blocked),
        };

        var endwalker = AltGoals.Flying(catalog, states, zones, 4);
        Assert.Equal(1, endwalker.Left);
        Assert.Equal([2u], endwalker.Quests.Select(static q => q.RowId));
        Assert.Equal(1, endwalker.Doable);

        var dawntrail = AltGoals.Flying(catalog, states, zones, 5);
        Assert.Equal(1, dawntrail.Left);
        Assert.Equal(0, dawntrail.Doable);
        Assert.True(AltGoals.Flying(catalog, states, zones, 3).Reached);
    }

    [Fact]
    public void The_roulettes_goal_counts_the_duties_left_to_open_them()
    {
        var unlock = Quest(1, "Unlock");
        var other = Quest(2, "Other");
        var duty = new DutyRunInfo(100, 200, 300, DutyRunInfo.Dungeons, "Dungeon", true, false);
        var second = new DutyRunInfo(101, 201, 301, DutyRunInfo.Dungeons, "Second", true, false);
        var roulette = new RouletteInfo(1, "Leveling", DutyRoulettes.Leveling, false, 15, 0, 0, 0);
        var raid = new RouletteInfo(2, "Alliance", DutyRoulettes.AllianceRaids, false, 50, 0, 0, 0);
        var states = new Dictionary<uint, QuestEvaluation> { [1] = State(QuestState.Ready), [2] = State(QuestState.Blocked) };

        var closed = new DutyBoardModel(
        [
            new RouletteLine(roulette, RouletteLock.NeedsDuties, 0, 2, [new BoardDuty(duty, [unlock]), new BoardDuty(second, [other])]),
            new RouletteLine(raid, RouletteLock.Open, 3, 1, []),
        ], []);
        var progress = AltGoals.Roulettes(states, closed);
        Assert.Equal(2, progress.Left);
        Assert.Equal([1u, 2u], progress.Quests.Select(static q => q.RowId));
        Assert.Equal(1, progress.Doable);

        var open = new DutyBoardModel([new RouletteLine(roulette, RouletteLock.Open, 2, 2, [])], []);
        Assert.True(AltGoals.Roulettes(states, open).Reached);

        // An expansion the account lacks is not the character's to open; no records at all cannot answer.
        var expansion = new DutyBoardModel([new RouletteLine(raid, RouletteLock.NeedsExpansion, 0, 1, [])], []);
        Assert.True(AltGoals.Roulettes(states, expansion).Reached);
        Assert.False(AltGoals.Roulettes(states, DutyBoardModel.Empty).Known);
    }

    [Fact]
    public void A_goal_needs_its_argument()
    {
        Assert.True(AltGoal.Story("7.0").IsValid);
        Assert.True(AltGoal.Match(5).IsValid);
        Assert.True(AltGoal.Flying(0).IsValid);
        Assert.True(AltGoal.Roulettes().IsValid);
        Assert.False(new AltGoal { Kind = AltGoalKind.Story }.IsValid);
        Assert.False(new AltGoal { Kind = AltGoalKind.MatchCharacter }.IsValid);
        Assert.False(new AltGoal { Kind = AltGoalKind.Flying }.IsValid);
        Assert.False(new AltGoal().IsValid);
    }
}
