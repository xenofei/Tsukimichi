using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Chains;

/// <summary>Story sidequests and side-story derivation over small hand-built catalogs.</summary>
public sealed class StorySidequestsTests
{
    private const uint Side = StorySidequests.SidequestSectionId;
    private const uint ZoneA = 1001;
    private const uint ZoneB = 1002;

    private static readonly IReadOnlySet<uint> NoFeatures = new HashSet<uint>();

    private static QuestRecord Quest(
        uint rowId,
        uint[]? previous = null,
        uint genre = 70,
        uint territory = ZoneA,
        uint icon = 100000,
        uint section = Side,
        bool repeatable = false,
        bool retired = false,
        int? sortKey = null) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        Name = $"Quest {rowId}",
        Journal = new JournalRef(section, "Sidequests", 7, "Category", genre, $"Genre {genre}", sortKey ?? (int)((genre << 16) | (rowId & 0xFFFF))),
        PreviousQuests = previous is null ? Prereq.None : new Prereq(previous, JoinKind.All),
        Issuer = new Issuer(1, "Giver", territory, 1, 0f, 0f, 0f),
        Icon = icon,
        IsRepeatable = repeatable,
        IsRetired = retired,
        Level = 1,
    };

    [Fact]
    public void Two_lines_joined_by_a_last_quest_are_one_story_in_play_order()
    {
        // The Shadowbringers-to-Dawntrail shape: A1 → A2 → A3, B1 → B2 → B3, then C requires A3 and B3. Journal order
        // interleaves nothing here, but C must come last whatever its row id.
        var catalog = QuestCatalog.Build(
        [
            Quest(10, sortKey: 1), Quest(11, [10], sortKey: 2), Quest(12, [11], sortKey: 3),
            Quest(20, sortKey: 5), Quest(21, [20], sortKey: 6), Quest(22, [21], sortKey: 7),
            Quest(5, [12, 22], sortKey: 0),
        ]);

        var stories = StorySidequests.Build(catalog, NoFeatures);

        var chain = Assert.Single(stories.Chains);
        Assert.True(chain.IsStory);
        Assert.Equal("Story: Quest 10", chain.Name);
        Assert.Equal(new uint[] { 10, 11, 12, 20, 21, 22, 5 }, chain.RowIds);
        Assert.Same(chain, stories.ChainOf(5));
        Assert.Equal(7, stories.Count);
    }

    [Fact]
    public void A_quest_waits_for_every_quest_it_requires()
    {
        // Journal order puts 31 before 32, but 31 requires 32 (the Delivery Moogle genre does this).
        var catalog = QuestCatalog.Build([Quest(30, sortKey: 1), Quest(31, [32], sortKey: 2), Quest(32, [30], sortKey: 3)]);

        var chain = Assert.Single(StorySidequests.Build(catalog, NoFeatures).Chains);

        Assert.Equal(new uint[] { 30, 32, 31 }, chain.RowIds);
    }

    [Fact]
    public void Links_join_the_same_genre_or_the_same_territory_only()
    {
        var catalog = QuestCatalog.Build(
        [
            // Same territory, different genre: linked.
            Quest(40, genre: 70), Quest(41, [40], genre: 71),
            // Different genre and territory: two lone quests, not a story.
            Quest(50, genre: 72, territory: ZoneA), Quest(51, [50], genre: 73, territory: ZoneB),
            // Same genre, different territory: linked.
            Quest(60, genre: 74, territory: ZoneA), Quest(61, [60], genre: 74, territory: ZoneB),
        ]);

        var stories = StorySidequests.Build(catalog, NoFeatures);

        Assert.Equal(2, stories.Chains.Count);
        Assert.Equal(new uint[] { 40, 41 }, stories.ChainOf(41)!.RowIds);
        Assert.Equal(new uint[] { 60, 61 }, stories.ChainOf(61)!.RowIds);
        Assert.Null(stories.ChainOf(50));
        Assert.Null(stories.ChainOf(51));
        Assert.True(stories.Contains(50));
    }

    [Fact]
    public void Only_live_one_off_non_unlock_sidequests_with_artwork_count()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1),
            Quest(2, icon: 0),
            Quest(3, section: 0),
            Quest(4, section: 6),
            Quest(5, repeatable: true),
            Quest(6, retired: true),
            Quest(7, genre: 0),
            Quest(8),
        ]);

        var stories = StorySidequests.Build(catalog, new HashSet<uint> { 8 });

        Assert.Equal(new uint[] { 1 }, stories.RowIds.Order());
        Assert.Empty(stories.Chains);
    }

    [Fact]
    public void An_unlock_or_art_less_quest_breaks_the_story_where_it_stands()
    {
        // 71 is an unlock quest and 72 has no artwork: 70 and 73 are left alone, 74 → 75 stays a story.
        var catalog = QuestCatalog.Build(
        [
            Quest(70), Quest(71, [70]), Quest(72, [71], icon: 0), Quest(73, [72]),
            Quest(74), Quest(75, [74]),
        ]);

        var stories = StorySidequests.Build(catalog, new HashSet<uint> { 71 });

        var chain = Assert.Single(stories.Chains);
        Assert.Equal(new uint[] { 74, 75 }, chain.RowIds);
        Assert.True(stories.Contains(70));
        Assert.True(stories.Contains(73));
        Assert.False(stories.Contains(71));
    }

    [Fact]
    public void Aether_current_lines_come_in_whole_and_other_unlocks_stay_out()
    {
        // Every quest here is blue (in the feature set). Zone A: 199 opens nothing of its own and leads to 200, which
        // grants the current; 201 and 202 follow; 203 joins 202 and a yellow 210. 204 follows 203 but unlocks a duty.
        // Zone B: 300 unlocks a duty and grants a current; 301 follows it; 310 is a blue quest linked to no current.
        var catalog = QuestCatalog.Build(
        [
            Quest(199), Quest(200, [199]), Quest(201, [200]), Quest(202, [201]), Quest(210, icon: 100001),
            Quest(203, [202, 210]), Quest(204, [203]),
            Quest(300, genre: 71, territory: ZoneB), Quest(301, [300], genre: 71, territory: ZoneB),
            Quest(310, genre: 72, territory: ZoneB),
        ]);
        var features = new HashSet<uint> { 199, 200, 201, 202, 203, 204, 300, 301, 310 };
        UniqueRewardEntry[] unique =
        [
            new(200, RewardKind.AetherCurrent, 1, 0, "Aether Current", Confidence.Static, "test"),
            new(204, RewardKind.DutyUnlock, 2, 0, "A Duty", Confidence.Static, "test"),
            new(300, RewardKind.AetherCurrent, 3, 0, "Aether Current", Confidence.Static, "test"),
            new(300, RewardKind.DutyUnlock, 4, 0, "Another Duty", Confidence.Static, "test"),
        ];

        var stories = StorySidequests.Build(catalog, features, CuratedData.Empty, unique);

        var chain = Assert.Single(stories.Chains);
        Assert.Equal("Story: Quest 199", chain.Name);
        Assert.Equal(new uint[] { 199, 200, 201, 202, 210, 203 }, chain.RowIds);
        Assert.Equal(new uint[] { 199, 200, 201, 202, 203, 210 }, stories.RowIds.Order());

        // The base rule alone leaves every blue quest out; without curated data the lines are not let in either.
        Assert.Equal(new uint[] { 210 }, StorySidequests.Build(catalog, features).RowIds.Order());
        Assert.Equal(new uint[] { 210 }, StorySidequests.Build(catalog, features, null, unique).RowIds.Order());
    }

    [Fact]
    public void Only_aether_current_unlocks_pass_the_current_only_test()
    {
        var currents = new HashSet<uint> { 1 };
        var others = new HashSet<uint> { 3 };
        var named = Quest(1) with { Rewards = [new RewardRef(RewardKind.Other, 0, 0, 1, "Aether Current", 0)] };
        var unnamedCurrentless = Quest(2) with { Rewards = [new RewardRef(RewardKind.Other, 0, 0, 1, "Something", 0)] };
        var action = Quest(4) with { Rewards = [new RewardRef(RewardKind.Action, 7, 0, 1, "An Action", 0)] };

        Assert.True(FeaturePresets.UnlocksOnlyAetherCurrents(named, CuratedData.Empty, currents, others));
        Assert.True(FeaturePresets.UnlocksOnlyAetherCurrents(Quest(5), CuratedData.Empty, currents, others));
        Assert.False(FeaturePresets.UnlocksOnlyAetherCurrents(unnamedCurrentless, CuratedData.Empty, currents, others));
        Assert.False(FeaturePresets.UnlocksOnlyAetherCurrents(Quest(3), CuratedData.Empty, currents, others));
        Assert.False(FeaturePresets.UnlocksOnlyAetherCurrents(action, CuratedData.Empty, currents, others));
    }

    [Fact]
    public void Reading_order_follows_the_journal_with_each_story_at_its_earliest_quest()
    {
        // Genre 70: lone 80, then a story whose play order (82 before 81) differs from journal order. Genre 71: 90.
        var catalog = QuestCatalog.Build(
        [
            Quest(80, genre: 70, territory: ZoneA),
            Quest(81, [82], genre: 70, territory: ZoneB),
            Quest(82, genre: 70, territory: ZoneB),
            Quest(90, genre: 71, territory: ZoneB),
        ]);

        var stories = StorySidequests.Build(catalog, NoFeatures);

        var order = stories.RowIds.OrderBy(stories.OrderOf).ToArray();
        Assert.Equal(new uint[] { 80, 82, 81, 90 }, order);
        Assert.Equal(int.MaxValue, stories.OrderOf(12345));
    }

    [Fact]
    public void Chain_catalog_adds_stories_after_genre_chains_and_names_them_through_the_shield()
    {
        // Genre 70 is a straight line, so the genre chain holds 100 and 101; the story 110 → 111 in genre 71 branches
        // off a quest outside it and joins the catalog as its own chain.
        var catalog = QuestCatalog.Build(
        [
            Quest(100, genre: 70, territory: ZoneA), Quest(101, [100], genre: 70, territory: ZoneA),
            Quest(110, genre: 71, territory: ZoneB), Quest(111, [110], genre: 71, territory: ZoneB), Quest(112, [110], genre: 71, territory: ZoneB),
        ]);
        var stories = StorySidequests.Build(catalog, NoFeatures);
        Assert.Equal(2, stories.Chains.Count);

        var chains = ChainCatalog.Build(catalog, CuratedData.Empty, stories);

        // The genre-70 story lies wholly inside the genre chain, so it adds nothing; genre 71 branches, so only the story holds it.
        Assert.Equal("Genre 70", chains.ForQuest(100)!.Name);
        Assert.DoesNotContain(chains.Chains, c => c.IsStory && c.RowIds.Contains(100u));
        var story = chains.ForQuest(112);
        Assert.NotNull(story);
        Assert.True(story.IsStory);
        Assert.Equal(new uint[] { 110, 111, 112 }, story.RowIds);

        string Shield(uint rowId) => rowId == 110 ? "Hidden" : $"Quest {rowId}";
        Assert.Equal("Story: Hidden", ChainCatalog.DisplayName(story, Shield));
        Assert.Equal("Hidden", ChainCatalog.Title(story, Shield));
        Assert.Equal("Genre 70", ChainCatalog.DisplayName(chains.ForQuest(100)!, Shield));
        Assert.Equal(2, ChainCatalog.IndexOf(story, 112));
        Assert.Equal(-1, ChainCatalog.IndexOf(story, 100));

        var progress = ChainCatalog.Progress(story, new Dictionary<uint, QuestEvaluation>
        {
            [110] = new(QuestState.Completed, [], null, null, null),
        });
        Assert.Equal(new ChainProgress(1, 3, 111), progress);

        // Without stories the catalog is what it was.
        Assert.Null(ChainCatalog.Build(catalog, CuratedData.Empty).ForQuest(112));
    }

    [Fact]
    public void Empty_catalog_has_no_stories()
    {
        var stories = StorySidequests.Build(QuestCatalog.Empty, NoFeatures);
        Assert.Same(StorySidequests.Empty, stories);
        Assert.Empty(stories.Chains);
    }
}
