using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The seven refiling rules of docs/data/unlisted-report.md section 4 on synthetic catalogs (the QA panel's T1 list),
/// plus the curated overrides and the flags the tree and the detail pane read. The frozen catalog is covered by
/// <see cref="RefilingFixtureTests"/>.
/// </summary>
public class JournalRefilerTests
{
    private const uint G27 = 27;
    private const uint G107 = 107;
    private const uint G108 = 108;
    private const uint G124 = 124;
    private const uint G128 = 128;
    private const uint G156 = 156;

    private static readonly Issuer Placeholder = new(JournalRefiler.PlaceholderIssuer, string.Empty, 0, 0, 0, 0, 0);

    private static QuestRecord Quest(
        uint rowId,
        string name = "Quest",
        uint genre = 0,
        uint section = 2,
        uint category = 20,
        byte expansion = 0,
        string? internalId = null,
        uint[]? previous = null,
        uint[]? locks = null,
        Issuer? issuer = null,
        bool hidden = false,
        byte grandCompany = 0,
        byte eventIcon = 3,
        int sortKey = 0) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = internalId ?? $"Test_{rowId}",
        Name = name,
        Journal = genre == 0
            ? JournalRef.None with { SectionId = 255, SortKey = (900 << 16) | (sortKey == 0 ? (int)(rowId & 0xFFFF) : sortKey) }
            : new JournalRef(section, $"Section {section}", category, $"Category {category}", genre, $"Genre {genre}", ((int)genre << 16) | (sortKey == 0 ? (int)(rowId & 0xFFFF) : sortKey)),
        Expansion = expansion,
        PreviousQuests = previous is null ? Prereq.None : new Prereq(previous, JoinKind.All),
        QuestLocks = locks ?? [],
        Issuer = issuer,
        IsHidden = hidden,
        GrandCompany = grandCompany,
        EventIconType = eventIcon,
    };

    private static Issuer At(uint territory, uint npc = 1000000) => new(npc, "Someone", territory, 1, 0, 0, 0);

    private static Dictionary<uint, QuestRecord> Refile(CuratedData? curated = null, params QuestRecord[] quests) =>
        JournalRefiler.Apply(quests, curated ?? CuratedData.Empty).ToDictionary(q => q.RowId);

    private static CuratedData Curated(string? overrides = null, string? retired = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "tsukimichi-refiler-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        if (overrides is not null)
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.RefileOverridesFileName), overrides);
        }

        if (retired is not null)
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.RetiredQuestsFileName), retired);
        }

        try
        {
            return CuratedData.Load(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Rule1_placeholder_issuer_marks_retired_and_leaves_the_journal_alone()
    {
        var filed = Refile(null, Quest(70001, issuer: Placeholder));

        var quest = filed[70001];
        Assert.True(quest.IsRetired);
        Assert.Equal(JournalRefiler.RetiredRule, quest.RefiledFrom);
        Assert.True(quest.IsUnlisted);
        Assert.True(quest.IsRemoved);
        Assert.Equal(0u, quest.Journal.GenreId);
    }

    [Fact]
    public void Rule1_hidden_flag_marks_retired_without_the_placeholder_issuer()
    {
        var filed = Refile(null, Quest(70001, issuer: At(128), hidden: true));

        Assert.True(filed[70001].IsRetired);
        Assert.Equal(JournalRefiler.RetiredRule, filed[70001].RefiledFrom);
    }

    [Fact]
    public void Rule1_retires_a_listed_row_on_the_placeholder_issuer_and_it_keeps_its_genre()
    {
        // But I Hardly Noah after the 6.3 Crystal Tower rewrite: the sheet keeps genre 18 but moved the issuer and set
        // the hidden flag. No curated entry is needed for the retirement itself.
        var filed = Refile(null, Quest(66033, genre: 18, issuer: Placeholder, hidden: true), Quest(66035, genre: 18));
        var retired = filed[66033];
        Assert.True(retired.IsRetired);
        Assert.Equal(JournalRefiler.RetiredRule, retired.RefiledFrom);
        Assert.Equal(18u, retired.Journal.GenreId);
        Assert.False(retired.IsUnlisted);
        Assert.True(retired.IsRemoved);
        Assert.False(filed[66035].IsRetired);

        var catalog = QuestCatalog.Build(filed.Values);
        var states = new Dictionary<uint, QuestState> { [66033] = QuestState.Completed, [66035] = QuestState.Ready };
        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: false);
        Assert.Equal(new NodeCount(0, 1, 0), counts.Genre(18));
        Assert.Equal(new NodeCount(1, 1, 0), counts.Unlisted);
        Assert.Equal(new NodeCount(0, 1, 0), counts.Overall);
        Assert.Equal(new NodeCount(1, 2, 0), TreeCounts.Compute(catalog, states, includeUnlisted: true).Overall);
        Assert.Equal([66033u], catalog.Removed.Select(q => q.RowId));

        // With a curated entry too, the sheet signal is the rule that fires; the entry only lends its patch note.
        var curated = Curated(retired: """{ "schema": 1, "entries": { "66033": { "note": "removed", "evidence": "https://example.test/x", "patch": "6.3" } } }""");
        Assert.Empty(curated.Warnings);
        Assert.Equal("6.3", curated.RetiredQuests[66033].Patch);
        var withNote = Refile(curated, Quest(66033, genre: 18, issuer: Placeholder, hidden: true), Quest(66035, genre: 18));
        Assert.True(withNote[66033].IsRetired);
        Assert.Equal(JournalRefiler.RetiredRule, withNote[66033].RefiledFrom);
    }

    [Fact]
    public void Curated_retired_quests_json_retires_a_listed_row_the_sheet_does_not_mark()
    {
        // Meet, Greet, and Deceit (3.05): a normal issuer, no hidden flag, a genre; only the curated file knows.
        var curated = Curated(retired: """{ "schema": 1, "entries": { "66023": { "note": "removed", "evidence": "https://example.test/x", "patch": "3.05" } } }""");
        Assert.Empty(curated.Warnings);

        var filed = Refile(curated, Quest(66023, genre: 112, issuer: At(129)), Quest(66024, genre: 112, issuer: At(129)));
        var retired = filed[66023];
        Assert.True(retired.IsRetired);
        Assert.Equal(JournalRefiler.CuratedRule, retired.RefiledFrom);
        Assert.Equal(112u, retired.Journal.GenreId);
        Assert.False(retired.IsUnlisted);
        Assert.True(retired.IsRemoved);
        Assert.False(filed[66024].IsRetired);
        Assert.False(Refile(null, Quest(66023, genre: 112, issuer: At(129)))[66023].IsRetired);
    }

    [Fact]
    public void Rule2_class_intro_takes_the_first_listed_successors_genre_and_is_a_feature_quest()
    {
        var filed = Refile(
            null,
            Quest(65713, "So You Want to Be a Gladiator", internalId: "ClsGla001_00177", issuer: At(130), eventIcon: FeaturePresets.QuasiQuestEventIconType),
            Quest(65821, "Way of the Gladiator", genre: G156, section: 6, category: 93, previous: [65713]));

        var intro = filed[65713];
        Assert.Equal(G156, intro.Journal.GenreId);
        Assert.Equal(2, intro.RefiledFrom);
        Assert.False(intro.IsUnlisted);
        Assert.False(intro.IsRetired);
        Assert.Equal("Genre 156", intro.Journal.GenreName);
        Assert.Equal(6u, intro.Journal.SectionId);
        Assert.True(FeaturePresets.IsFeatureQuest(intro, CuratedData.Empty));

        // Listed under the genre, out of its numbers: the class a character started as never gets its intro.
        Assert.False(intro.CountsInTotals);
        Assert.True(filed[65821].CountsInTotals);
        var catalog = QuestCatalog.Build(filed.Values);
        var counts = TreeCounts.Compute(catalog, new Dictionary<uint, QuestState> { [65713] = QuestState.Ready, [65821] = QuestState.Completed }, includeUnlisted: false);
        Assert.Equal(new NodeCount(1, 1, 0), counts.Genre(G156));
        Assert.Equal(new NodeCount(1, 1, 0), counts.Overall);
        Assert.Equal([65713u, 65821u], catalog.ByGenre[G156].Select(q => q.RowId));
    }

    [Theory]
    [InlineData("ClsGla001_00177", true)]
    [InlineData("ClsRog999_00101", true)]
    [InlineData("JobDrk299_01000", true)]
    [InlineData("ClsGla002_00178", false)]
    [InlineData("JobDrk300_01001", false)]
    [InlineData("ManFst002_00085", false)]
    [InlineData("XClsGla001_00177", false)]
    public void Rule2_regex_matches_intro_ids_and_rejects_ordinary_class_quests(string internalId, bool matches)
    {
        Assert.Equal(matches, JournalRefiler.IsClassIntro(internalId));
    }

    [Fact]
    public void Rule3_grand_company_maps_to_233_plus_the_company()
    {
        var filed = Refile(
            null,
            Quest(67925, "Squadron and Commander", grandCompany: 2, issuer: At(132)),
            Quest(66216, "A Grand Company Quest", genre: 235, section: 3, category: 30));

        var squadron = filed[67925];
        Assert.Equal(235u, squadron.Journal.GenreId);
        Assert.Equal(3, squadron.RefiledFrom);
        // The reference is the listed quest's: names, section and category, with the quest's own sheet key inside.
        Assert.Equal("Genre 235", squadron.Journal.GenreName);
        Assert.Equal(3u, squadron.Journal.SectionId);
        Assert.Equal(30u, squadron.Journal.CategoryId);
        Assert.False(squadron.IsRemoved);
    }

    [Fact]
    public void A_genre_no_listed_quest_holds_leaves_the_quest_unlisted_rather_than_in_the_main_scenario()
    {
        // Rule 3 computes its genre instead of reading it off a listed quest, so it is the one rule that can name an
        // empty genre. Filing there with JournalRef.None would put the quest in section 0 under a nameless node.
        var filed = Refile(null, Quest(67925, "Squadron and Commander", grandCompany: 2, issuer: At(132)));

        var squadron = filed[67925];
        Assert.True(squadron.IsUnlisted);
        Assert.True(squadron.IsRemoved);
        Assert.False(squadron.IsRetired);
        Assert.Equal(JournalRefiler.UnlistedRule, squadron.RefiledFrom);
        Assert.Equal(255u, squadron.Journal.SectionId);
        Assert.Equal(0u, squadron.Journal.CategoryId);
        Assert.False(QuestCatalog.Build(filed.Values).BySection.ContainsKey(0));
    }

    [Fact]
    public void An_override_to_a_genre_no_listed_quest_holds_is_ignored()
    {
        var curated = Curated(overrides: """{ "schema": 1, "entries": { "68478": { "genre": 9, "note": "typo for 90", "evidence": "https://example.test/pagos" }, "68614": { "genre": 9, "note": "typo for 90", "evidence": "https://example.test/eureka" } } }""");
        Assert.Empty(curated.Warnings);

        var eureka = Quest(68614, "And We Shall Call It Eureka", genre: 90, section: 2, category: 56, expansion: 2);
        var filed = Refile(curated, Quest(68478, "And We Shall Call It Pagos", expansion: 2, issuer: At(628)), eureka);

        // The unlisted row stays unlisted under rule 7 (the log names it); the listed row keeps the sheet's filing.
        Assert.True(filed[68478].IsUnlisted);
        Assert.Equal(JournalRefiler.UnlistedRule, filed[68478].RefiledFrom);
        Assert.Same(eureka, filed[68614]);
        Assert.Equal(90u, filed[68614].Journal.GenreId);
        Assert.Equal(0, filed[68614].RefiledFrom);
    }

    [Fact]
    public void Rule4_walks_unlisted_prerequisites_in_slot_order_within_the_same_expansion()
    {
        var target = Quest(69582, "Strange Glagg", expansion: 3, previous: [69581]);
        var hiddenStep = Quest(69581, "Dwarves of a Beard", expansion: 3, previous: [69580, 69579]);
        var otherHidden = Quest(69580, "A Message from Konogg", expansion: 3, previous: [69579]);
        var listed = Quest(69579, "Komra Wasn't Built in a Day", genre: G27, section: 2, category: 19, expansion: 3);

        var filed = Refile(null, target, hiddenStep, otherHidden, listed);
        Assert.Equal(G27, filed[69582].Journal.GenreId);
        Assert.Equal(4, filed[69582].RefiledFrom);
        Assert.Equal(G27, filed[69581].Journal.GenreId);
        Assert.Equal(G27, filed[69580].Journal.GenreId);

        // The same graph with the listed prerequisite in another expansion: rule 4 has nothing, and with no
        // successor, lock or territory either, rule 7 keeps the quest unlisted.
        var elsewhere = Refile(null, target, hiddenStep, otherHidden, listed with { Expansion = 0 });
        Assert.True(elsewhere[69582].IsUnlisted);
        Assert.Equal(JournalRefiler.UnlistedRule, elsewhere[69582].RefiledFrom);
    }

    [Fact]
    public void Rule4_skips_main_scenario_prerequisites()
    {
        var filed = Refile(
            null,
            Quest(70001, previous: [65621, 66001]),
            Quest(65621, "Close to Home", genre: 1, section: 0, category: 1),
            Quest(66001, "A Side Story", genre: G27, section: 2, category: 19));

        Assert.Equal(G27, filed[70001].Journal.GenreId);
        Assert.Equal(4, filed[70001].RefiledFrom);
    }

    [Fact]
    public void Rule5_uses_the_first_listed_successor_then_a_quest_lock_partner()
    {
        var bySuccessor = Refile(
            null,
            Quest(70187, "An Odd Job", expansion: 4),
            Quest(70188, "Variant Dungeons", genre: G108, section: 2, category: 50, expansion: 4, previous: [70187]));
        Assert.Equal(G108, bySuccessor[70187].Journal.GenreId);
        Assert.Equal(5, bySuccessor[70187].RefiledFrom);

        var byLock = Refile(
            null,
            Quest(70180, "Seeing the Cieldalaes", expansion: 4, locks: [70179]),
            Quest(70179, "Seeking Sanctuary", genre: G107, section: 2, category: 49, expansion: 4));
        Assert.Equal(G107, byLock[70180].Journal.GenreId);
        Assert.Equal(5, byLock[70180].RefiledFrom);

        // The expansion constraint applies to locks too (the report's probe kept it; the curated override files
        // the real Seeing the Cieldalaes).
        var mismatch = Refile(
            null,
            Quest(70180, "Seeing the Cieldalaes", expansion: 0, locks: [70179]),
            Quest(70179, "Seeking Sanctuary", genre: G107, section: 2, category: 49, expansion: 4));
        Assert.True(mismatch[70180].IsUnlisted);
    }

    [Fact]
    public void Rule6_takes_the_dominant_regional_genre_of_the_issuers_territory()
    {
        var filed = Refile(
            null,
            Quest(68457, "Leves of Kugane", expansion: 2, issuer: At(628)),
            Quest(68001, genre: G128, category: 67, issuer: At(628)),
            Quest(68002, genre: G128, category: 67, issuer: At(628)),
            Quest(68003, genre: G124, category: 66, issuer: At(628)),
            // A listed quest in the same territory but outside categories 59-85 does not vote.
            Quest(68004, genre: 91, category: 55, issuer: At(628)),
            Quest(68005, genre: 91, category: 55, issuer: At(628)),
            Quest(68006, genre: 91, category: 55, issuer: At(628)));

        Assert.Equal(G128, filed[68457].Journal.GenreId);
        Assert.Equal(6, filed[68457].RefiledFrom);
    }

    [Fact]
    public void Rule6_vote_ignores_a_listed_row_that_is_retired_by_the_sheet_or_the_curated_file()
    {
        // A tie between G128 and G124 that only a retired row would break, once by the placeholder issuer and once by
        // retired_quests.json: the vote stays tied and the quest stays unlisted.
        var curated = Curated(retired: """{ "schema": 1, "entries": { "68003": { "note": "removed", "evidence": "https://example.test/x" } } }""");
        Assert.Empty(curated.Warnings);

        var bySheet = Refile(
            null,
            Quest(68457, "Leves of Kugane", issuer: At(628)),
            Quest(68001, genre: G128, category: 67, issuer: At(628)),
            Quest(68002, genre: G128, category: 67, issuer: new Issuer(JournalRefiler.PlaceholderIssuer, string.Empty, 628, 1, 0, 0, 0)),
            Quest(68003, genre: G124, category: 66, issuer: At(628)));
        Assert.True(bySheet[68002].IsRetired);
        Assert.True(bySheet[68457].IsUnlisted);
        Assert.Equal(JournalRefiler.UnlistedRule, bySheet[68457].RefiledFrom);

        var byCurated = Refile(
            curated,
            Quest(68457, "Leves of Kugane", issuer: At(628)),
            Quest(68001, genre: G128, category: 67, issuer: At(628)),
            Quest(68003, genre: G124, category: 66, issuer: At(628)),
            Quest(68004, genre: G124, category: 66, issuer: At(628)));
        Assert.True(byCurated[68003].IsRetired);
        Assert.True(byCurated[68457].IsUnlisted);

        // The same rows with the retired one live: G124 wins.
        var live = Refile(
            null,
            Quest(68457, "Leves of Kugane", issuer: At(628)),
            Quest(68001, genre: G128, category: 67, issuer: At(628)),
            Quest(68003, genre: G124, category: 66, issuer: At(628)),
            Quest(68004, genre: G124, category: 66, issuer: At(628)));
        Assert.Equal(G124, live[68457].Journal.GenreId);
    }

    [Fact]
    public void Rule6_tie_stays_unlisted_with_rule_seven()
    {
        var filed = Refile(
            null,
            Quest(68457, "Leves of Kugane", issuer: At(628)),
            Quest(68001, genre: G128, category: 67, issuer: At(628)),
            Quest(68003, genre: G124, category: 66, issuer: At(628)));

        Assert.True(filed[68457].IsUnlisted);
        Assert.False(filed[68457].IsRetired);
        Assert.True(filed[68457].IsRemoved);
        Assert.Equal(JournalRefiler.UnlistedRule, filed[68457].RefiledFrom);
    }

    [Fact]
    public void Rule7_no_signal_stays_unlisted()
    {
        var filed = Refile(null, Quest(67752, "The New Frontier", issuer: new Issuer(1011000, "Aurvael", 0, 0, 0, 0, 0)));

        Assert.True(filed[67752].IsUnlisted);
        Assert.Equal(JournalRefiler.UnlistedRule, filed[67752].RefiledFrom);
    }

    [Fact]
    public void Rules_apply_in_order_so_retired_wins_over_class_intro_and_grand_company()
    {
        var filed = Refile(
            null,
            Quest(65713, internalId: "ClsGla001_00177", issuer: Placeholder, grandCompany: 1),
            Quest(65821, genre: G156, previous: [65713]));

        Assert.True(filed[65713].IsRetired);
        Assert.Equal(0u, filed[65713].Journal.GenreId);
    }

    [Fact]
    public void Overrides_win_over_rules_and_mark_the_curated_rule()
    {
        var curated = Curated(overrides: """{ "schema": 1, "entries": { "68478": { "genre": 90, "note": "Eureka", "evidence": "https://example.test/pagos" } } }""");
        Assert.Empty(curated.Warnings);

        var filed = Refile(
            curated,
            Quest(68478, "And We Shall Call It Pagos", expansion: 2, issuer: At(628)),
            Quest(68001, genre: G128, category: 67, issuer: At(628)),
            Quest(68614, "And We Shall Call It Eureka", genre: 90, section: 2, category: 56, expansion: 2));

        var pagos = filed[68478];
        Assert.Equal(90u, pagos.Journal.GenreId);
        Assert.Equal("Genre 90", pagos.Journal.GenreName);
        Assert.Equal(JournalRefiler.CuratedRule, pagos.RefiledFrom);
        Assert.False(pagos.IsRemoved);
    }

    [Fact]
    public void Overrides_never_move_a_retired_quest()
    {
        var curated = Curated(overrides: """{ "schema": 1, "entries": { "70001": { "genre": 90, "note": "x", "evidence": "https://example.test/x" } } }""");
        var filed = Refile(curated, Quest(70001, issuer: Placeholder), Quest(68614, genre: 90));

        Assert.True(filed[70001].IsRetired);
        Assert.True(filed[70001].IsUnlisted);
    }

    [Fact]
    public void Curated_entries_without_note_or_evidence_are_skipped_with_a_warning()
    {
        var curated = Curated(
            overrides: """{ "schema": 1, "entries": { "68478": { "genre": 90, "note": "no evidence" }, "68479": { "genre": 0, "note": "x", "evidence": "https://e" } } }""",
            retired: """{ "schema": 1, "entries": { "66033": { "evidence": "https://e" }, "abc": { "note": "x", "evidence": "https://e" } } }""");

        Assert.Empty(curated.RefileOverrides);
        Assert.Empty(curated.RetiredQuests);
        Assert.Equal(4, curated.Warnings.Count);
    }

    [Fact]
    public void Refiled_quest_sorts_inside_its_new_genre_by_its_sheet_key()
    {
        var filed = JournalRefiler.Apply(
        [
            Quest(65713, "So You Want to Be a Gladiator", internalId: "ClsGla001_00177", sortKey: 5),
            Quest(65821, "Way of the Gladiator", genre: G156, previous: [65713], sortKey: 10),
            Quest(65822, "Next Gladiator Quest", genre: G156, previous: [65821], sortKey: 20),
            Quest(65900, "Other Genre", genre: 157, sortKey: 1),
        ], CuratedData.Empty);

        var catalog = QuestCatalog.Build(filed);
        Assert.Equal([65713u, 65821u, 65822u], catalog.ByGenre[G156].Select(q => q.RowId));
        Assert.Equal(5, catalog.ByRowId[65713].Journal.SortKey & 0xFFFF);
        Assert.Equal(catalog.ByRowId[65821].Journal.SortKey >> 16, catalog.ByRowId[65713].Journal.SortKey >> 16);
        Assert.Empty(catalog.Removed);
    }

    [Fact]
    public void Listed_quests_pass_through_untouched()
    {
        var listed = Quest(65621, "Close to Home", genre: 1, section: 0, category: 1);
        var filed = JournalRefiler.Apply([listed], CuratedData.Empty);

        Assert.Same(listed, filed[0]);
        Assert.Equal(0, filed[0].RefiledFrom);
    }

    [Fact]
    public void Retired_quests_evaluate_locked_out_unless_completed_and_never_derive_as_feature_quests()
    {
        var retired = Quest(70001, "Old Story", issuer: Placeholder, eventIcon: FeaturePresets.FeatureEventIconType) with { IsRetired = true, RefiledFrom = 1 };
        var catalog = QuestCatalog.Build([retired]);

        var undone = Core.Evaluation.StateResolver.Resolve(retired, Evaluation.Fixture.Snapshot(), catalog, Core.Evaluation.EvalContext.Default);
        Assert.Equal(QuestState.Foreclosed, undone.State);
        Assert.Equal(RequirementKind.Retired, undone.NextStep!.Req.Kind);
        Assert.Equal("Locked out · removed from the game", Core.Evaluation.BlockerText.StatusText(undone, retired, Core.Evaluation.BlockerNames.Default, null));

        var done = Evaluation.Fixture.Snapshot(retired.RowId);
        Assert.Equal(QuestState.Completed, Core.Evaluation.StateResolver.Resolve(retired, done, catalog, Core.Evaluation.EvalContext.Default).State);

        Assert.False(FeaturePresets.IsFeatureQuest(retired, CuratedData.Empty));
        Assert.Empty(FeaturePresets.Derive(catalog, CuratedData.Empty));
    }
}
