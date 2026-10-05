using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Chains;

/// <summary>1.21.0 storylines over a hand-built catalog: the curated forms (P5), Side stories, Loose ends (N8) and the cast (N10).</summary>
public sealed class StorylinesTests
{
    private const uint SideSection = 3;
    private const uint JobSection = 6;

    private static QuestRecord Quest(uint rowId, uint genre, int sortKey, uint[]? previous = null, uint section = SideSection, byte icon = 1, byte level = 50) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Quest {rowId}",
        Journal = new JournalRef(section, "Section", genre / 10, $"Category {genre / 10}", genre, $"Genre {genre}", (int)(genre << 16) | sortKey),
        PreviousQuests = previous is null ? Prereq.None : new Prereq(previous, JoinKind.All),
        Level = level,
        EventIconType = icon,
    };

    private static IReadOnlyDictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] states) =>
        states.ToDictionary(s => s.RowId, s => new QuestEvaluation(s.State, [], null, null, null));

    private static CuratedData Curated(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "tsukimichi-storylines-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.ChainsFileName), json);
            return CuratedData.Load(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ------------------------------------------------------------------ curated forms

    [Fact]
    public void A_line_grows_from_its_start_through_every_side_quest_that_requires_it()
    {
        // 100 → 101 → 103 (also needs an outside quest 900); 102 requires 100; 104 is unrelated; 105 is main scenario.
        var catalog = QuestCatalog.Build(
        [
            Quest(100, 10, 1),
            Quest(101, 20, 1, [100]),
            Quest(102, 30, 1, [100]),
            Quest(103, 40, 1, [101, 900]),
            Quest(104, 40, 2),
            Quest(105, 1, 1, [103], section: 0, icon: 3),
            Quest(900, 50, 1),
        ]);

        Assert.Equal([100u, 101u, 102u, 103u], ChainCatalog.GrowFrom(100, catalog).Order());
        Assert.Empty(ChainCatalog.GrowFrom(999, catalog));
    }

    [Fact]
    public void Curated_quest_and_start_lines_are_named_curated_and_ongoing_as_the_file_says()
    {
        var catalog = QuestCatalog.Build([Quest(100, 10, 1), Quest(101, 10, 2, [100]), Quest(200, 20, 1), Quest(201, 20, 2, [200])]);
        var curated = Curated("""
            { "chains": [
              { "name": "Listed", "questIds": [101, 100], "ongoing": true, "note": "n" },
              { "name": "Grown", "startQuest": 200, "note": "n" }
            ] }
            """);

        var chains = ChainCatalog.Build(catalog, curated);

        var listed = chains.Chains.Single(c => c.Name == "Listed");
        Assert.Equal([100u, 101u], listed.RowIds); // prerequisite order wins over the listed order
        Assert.True(listed.IsCurated);
        Assert.True(listed.Ongoing);
        var grown = chains.Chains.Single(c => c.Name == "Grown");
        Assert.Equal([200u, 201u], grown.RowIds);
        Assert.False(grown.Ongoing);
        Assert.Same(grown, chains.ForQuest(201));
    }

    // ------------------------------------------------------------------ Side stories (P5)

    [Fact]
    public void Side_stories_order_started_then_fresh_then_caught_up_then_finished()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(100, 10, 1), Quest(101, 10, 2, [100]),
            Quest(200, 20, 1), Quest(201, 20, 2, [200]),
            Quest(300, 30, 1), Quest(301, 30, 2, [300]),
            Quest(400, 40, 1), Quest(401, 40, 2, [400]),
        ]);
        var curated = Curated("""
            { "chains": [
              { "name": "Finished", "genreIds": [10], "note": "n" },
              { "name": "Caught up", "genreIds": [20], "ongoing": true, "note": "n" },
              { "name": "Fresh", "genreIds": [30], "note": "n" },
              { "name": "Started", "genreIds": [40], "note": "n" }
            ] }
            """);
        var chains = ChainCatalog.Build(catalog, curated);
        var states = States(
            (100, QuestState.Completed), (101, QuestState.Completed),
            (200, QuestState.Completed), (201, QuestState.Completed),
            (300, QuestState.Ready), (301, QuestState.Blocked),
            (400, QuestState.Completed), (401, QuestState.Ready));

        var rows = SideStories.Build(chains, states, _ => false);

        Assert.Equal(["Started", "Fresh", "Caught up", "Finished"], rows.Select(r => r.Chain.Name));
        Assert.Equal([SideStoryStatus.ToGo, SideStoryStatus.ToGo, SideStoryStatus.CaughtUp, SideStoryStatus.Finished], rows.Select(r => r.Status));
        Assert.Equal(1, rows[0].Left);
        Assert.Equal(401u, rows[0].Progress.NextRowId);
    }

    [Fact]
    public void A_side_story_nothing_of_which_is_done_past_the_story_point_is_ahead()
    {
        var catalog = QuestCatalog.Build([Quest(100, 10, 1), Quest(101, 10, 2, [100]), Quest(200, 20, 1), Quest(201, 20, 2, [200])]);
        var chains = ChainCatalog.Build(catalog, Curated("""{ "chains": [ { "name": "Later", "genreIds": [10], "note": "n" }, { "name": "Begun", "genreIds": [20], "note": "n" } ] }"""));
        var states = States((100, QuestState.Blocked), (101, QuestState.Blocked), (200, QuestState.Completed), (201, QuestState.Blocked));

        var rows = SideStories.Build(chains, states, _ => true);

        Assert.False(rows.Single(r => r.Chain.Name == "Begun").Ahead); // something is done: it is the character's own
        Assert.True(rows.Single(r => r.Chain.Name == "Later").Ahead);
    }

    // ------------------------------------------------------------------ Loose ends (N8)

    [Theory]
    [InlineData(1, 5, false)]
    [InlineData(2, 5, true)]
    [InlineData(1, 4, true)]
    [InlineData(1, 2, true)]
    [InlineData(0, 3, false)]
    public void Started_means_two_done_or_one_on_a_line_of_up_to_four(int done, int total, bool started) =>
        Assert.Equal(started, LooseEnds.IsStarted(done, total));

    [Fact]
    public void Loose_ends_rank_ready_yellow_finales_first_then_other_ready_finales_then_fewest_left()
    {
        // Job line A (genre 60): 3 quests, 2 done, its finale Ready and blue. Job line B (61): 3 quests, 2 done, finale
        // Ready and yellow. Line C (62): 6 quests, 2 done (4 left). Line D (63): 5 quests, 1 done: not started.
        // Line E (64): 2 quests, both done: finished.
        var quests = new List<QuestRecord>
        {
            Quest(600, 600, 1, section: JobSection), Quest(601, 600, 2, [600], JobSection), Quest(602, 600, 3, [601], JobSection, icon: 8),
            Quest(610, 610, 1, section: JobSection), Quest(611, 610, 2, [610], JobSection), Quest(612, 610, 3, [611], JobSection, icon: 1),
            Quest(640, 640, 1, section: JobSection), Quest(641, 640, 2, [640], JobSection),
        };
        for (uint i = 0; i < 6; i++)
        {
            quests.Add(Quest(620 + i, 620, (int)i + 1, i == 0 ? null : [619 + i], JobSection));
        }

        for (uint i = 0; i < 5; i++)
        {
            quests.Add(Quest(630 + i, 630, (int)i + 1, i == 0 ? null : [629 + i], JobSection));
        }

        var catalog = QuestCatalog.Build(quests);
        var states = States(
            (600, QuestState.Completed), (601, QuestState.Completed), (602, QuestState.Ready),
            (610, QuestState.Completed), (611, QuestState.Completed), (612, QuestState.Ready),
            (620, QuestState.Completed), (621, QuestState.Completed), (622, QuestState.Ready),
            (630, QuestState.Completed), (631, QuestState.Ready),
            (640, QuestState.Completed), (641, QuestState.Completed));

        var ends = LooseEnds.Find(LooseEnds.Lines(catalog, ChainCatalog.Empty), states, catalog);

        Assert.Equal(["Genre 610", "Genre 600", "Genre 620"], ends.Select(e => e.Line.Chain.Name));
        Assert.True(ends[0].IsReadyFinale);
        Assert.True(ends[0].FinaleMarkedYellow);
        Assert.False(ends[1].FinaleMarkedYellow);
        Assert.Equal(4, ends[2].Left);
        Assert.False(ends[2].IsFinale);
        Assert.All(ends, e => Assert.Equal(StoryLineKind.Job, e.Line.Kind));

        // "Not for me" on a line's next quest (P4's set-aside list) takes the line off the card.
        var kept = LooseEnds.Find(LooseEnds.Lines(catalog, ChainCatalog.Empty), states, catalog, new HashSet<uint> { 612 });
        Assert.Equal(["Genre 600", "Genre 620"], kept.Select(e => e.Line.Chain.Name));
    }

    [Fact]
    public void A_short_line_counts_with_one_done_and_a_finale_not_ready_sorts_by_what_is_left()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(700, 700, 1), Quest(701, 700, 2, [700]), Quest(702, 700, 3, [701]),
            Quest(710, 710, 1), Quest(711, 710, 2, [710]),
        ]);
        var curated = Curated("""{ "chains": [ { "name": "Short", "genreIds": [700], "note": "n" }, { "name": "Pair", "genreIds": [710], "note": "n" } ] }""");
        var chains = ChainCatalog.Build(catalog, curated);
        var states = States((700, QuestState.Completed), (701, QuestState.Blocked), (702, QuestState.Blocked), (710, QuestState.Completed), (711, QuestState.Blocked));

        var ends = LooseEnds.Find(LooseEnds.Lines(catalog, chains), states, catalog);

        Assert.Equal(["Pair", "Short"], ends.Select(e => e.Line.Chain.Name));
        Assert.True(ends[0].IsFinale);
        Assert.False(ends[0].IsReadyFinale);
        Assert.Equal(2, ends[1].Left);
    }

    [Fact]
    public void The_finale_notice_and_the_overlay_section_are_off_by_default_and_the_notice_id_is_stable()
    {
        Assert.False(LooseEnds.FinaleNoticeDefault);
        Assert.False(LooseEnds.OverlaySectionDefault);
        Assert.Equal("finale:69286", LooseEnds.NoticeId(69286));
    }

    // ------------------------------------------------------------------ the cast (N10)

    private static Dictionary<uint, IReadOnlyList<CastName>> Scripts(params (uint RowId, string[] Names)[] scripts)
    {
        var result = new Dictionary<uint, IReadOnlyList<CastName>>();
        uint npc = 1_000_000;
        foreach (var (rowId, names) in scripts)
        {
            result[rowId] = names.Select(n => new CastName(npc++, n, n)).ToArray();
        }

        return result;
    }

    [Fact]
    public void Recurring_characters_need_enough_story_quests_and_generic_and_blocked_names_never_count()
    {
        // Story quests 1-3 (main scenario), side quest 10.
        var catalog = QuestCatalog.Build(
        [
            Quest(1, 1, 1, section: 0, icon: 3), Quest(2, 1, 2, [1], 0, 3), Quest(3, 1, 3, [2], 0, 3),
            Quest(10, 10, 1, [1]),
        ]);
        var scripts = Scripts(
            (1, ["Thancred", "serpent officer", "Nero tol Scaeva", "Wedge"]),
            (2, ["Thancred", "Thancred", "Temple Knight guard", "Nero", "Wedge"]),
            (3, ["Urianger", "Wedge"]),
            (10, ["Thancred", "Urianger", "Nero", "Wedge"]));
        var curation = new StoryCastCuration(new Dictionary<string, string> { ["Nero tol Scaeva"] = "Nero" }, new HashSet<string> { "Wedge" });

        var cast = StoryCast.Build(catalog, scripts, curation, minStoryQuests: 2);

        Assert.Equal(["Nero", "Thancred"], cast.Members.Select(m => m.Key).Order());
        Assert.Equal([1u, 2u], cast.Find("Nero")!.StoryQuests); // joined: "Nero tol Scaeva" in 1, "Nero" in 2
        Assert.Null(cast.Find("Urianger")); // one story quest is not enough
        Assert.Null(cast.Find("serpent officer"));
        Assert.Null(cast.Find("Wedge")); // blocked
        Assert.Equal(["Thancred", "Nero"], cast.Of(10).Select(m => m.Key));
        Assert.True(cast.HasCast(10));
    }

    [Fact]
    public void Only_characters_met_in_a_completed_story_quest_are_named()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, 1, 1, section: 0, icon: 3), Quest(2, 1, 2, [1], 0, 3), Quest(3, 1, 3, [2], 0, 3), Quest(4, 1, 4, [3], 0, 3),
            Quest(10, 10, 1, [1]),
        ]);
        var scripts = Scripts(
            (1, ["Thancred"]), (2, ["Thancred"]),
            (3, ["Ryne"]), (4, ["Ryne"]),
            (10, ["Thancred", "Ryne"]));
        var cast = StoryCast.Build(catalog, scripts, minStoryQuests: 2);
        var done = new HashSet<uint> { 1 };

        var line = cast.Line(10, done.Contains);

        Assert.Equal(["Thancred"], line.Met.Select(m => m.Name));
        Assert.Equal(1, line.Familiar);
        Assert.False(StoryCast.HasMet(cast.Find("Ryne")!, done.Contains));
        Assert.True(cast.Line(99, done.Contains).IsEmpty);
    }

    [Fact]
    public void A_quest_knows_which_rows_of_each_character_its_script_names()
    {
        // The cast plate wears the row the quest shows (the 1.22.1 portrait audit, C2), not the character's lowest row.
        var catalog = QuestCatalog.Build([Quest(1, 1, 1, section: 0, icon: 3), Quest(2, 1, 2, [1], 0, 3), Quest(10, 10, 1, [1])]);
        var scripts = new Dictionary<uint, IReadOnlyList<CastName>>
        {
            [1] = [new CastName(1_000_010, "Thancred", "Thancred"), new CastName(1_000_011, "Thancred", "Thancred")],
            [2] = [new CastName(1_000_030, "Urianger", "Urianger"), new CastName(1_000_020, "Thancred", "Thancred")],
            [10] = [new CastName(1_000_040, "Urianger", "Urianger")],
        };
        var cast = StoryCast.Build(catalog, scripts, minStoryQuests: 2);
        var thancred = cast.Find("Thancred")!;

        Assert.Equal([1_000_010u, 1_000_011u, 1_000_020u], thancred.NpcIds);
        Assert.Equal([1_000_010u, 1_000_011u], cast.RowsIn(1, thancred));
        Assert.Equal([1_000_020u], cast.RowsIn(2, thancred));
        Assert.Empty(cast.RowsIn(10, thancred));
        Assert.Empty(cast.RowsIn(99, thancred));
    }
}
