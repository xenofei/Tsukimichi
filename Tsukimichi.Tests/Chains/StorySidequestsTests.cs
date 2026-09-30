using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
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
