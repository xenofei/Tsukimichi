using System.Globalization;
using System.Text;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Evaluation;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The refiler over the frozen catalog with the shipped curated files: the golden file
/// <c>docs/data/refile-expected.csv</c> row by row, the rule counts of docs/data/unlisted-report.md appendix A, the
/// one-node invariant, the retired rows out of every count, the retired twins, the Legacy filing's 0.6.0 totals and
/// the Lodestone totals modulo the documented lag. Runs everywhere; no game files needed.
/// </summary>
public class RefilingFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>Set to rewrite the golden file from the fixture instead of diffing against it (review the diff before committing).</summary>
    public const string RegenerateEnvVar = "TSUKIMICHI_REGEN_GOLDEN";

    private const string GoldenRelativePath = "docs/data/refile-expected.csv";

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private QuestCatalog Legacy => fixture.LegacyBundle.Catalog;

    private static string GoldenPath() => Path.Combine(RepositoryRoot(), GoldenRelativePath);

    private static string RepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return dir;
    }

    /// <summary>Every row the refiler touches: the sheet's genre-0 rows and the curated retired listed rows, by row id.</summary>
    private IEnumerable<QuestRecord> Touched() =>
        Catalog.All.Where(q => q.RefiledFrom != 0 || q.IsRetired).OrderBy(q => q.RowId);

    private static string CsvLine(QuestRecord q) =>
        string.Join(",", q.RowId.ToString(CultureInfo.InvariantCulture), Quote(q.Name), q.RefiledFrom.ToString(CultureInfo.InvariantCulture), q.Journal.GenreId.ToString(CultureInfo.InvariantCulture));

    private static string Quote(string name) => name.Contains(',') || name.Contains('"') ? "\"" + name.Replace("\"", "\"\"") + "\"" : name;

    [Fact]
    public void Refiler_outcome_matches_the_committed_expected_csv()
    {
        var path = GoldenPath();
        var actual = new List<string>();
        foreach (var quest in Touched())
        {
            actual.Add(CsvLine(quest));
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RegenerateEnvVar)))
        {
            var text = new StringBuilder();
            text.Append("# Refiling outcome per touched quest, generated from the catalog fixture by RefilingFixtureTests (set ").Append(RegenerateEnvVar).Append(" to rewrite).\n");
            text.Append("# rule: 1 retired, 2 class or job intro, 3 Grand Company, 4 nearest listed prerequisite, 5 nearest listed successor or lock, 6 issuer's zone, 7 no signal, 8 curated file, 9 hidden progress tracker (filed, not counted). genreId 0 = stays unlisted.\n");
            text.Append("rowId,name,rule,genreId\n");
            foreach (var line in actual)
            {
                text.Append(line).Append('\n');
            }

            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            output.WriteLine($"rewrote {path} ({actual.Count} rows)");
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing; run the tests once with {RegenerateEnvVar}=1 to write it");
        var expected = File.ReadAllLines(path)
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("rowId,", StringComparison.Ordinal))
            .ToDictionary(l => l[..l.IndexOf(',', StringComparison.Ordinal)]);
        var actualByRow = actual.ToDictionary(l => l[..l.IndexOf(',', StringComparison.Ordinal)]);

        var moved = new List<string>();
        foreach (var (rowId, line) in actualByRow)
        {
            if (!expected.TryGetValue(rowId, out var was))
            {
                moved.Add($"+ {line}");
            }
            else if (was != line)
            {
                moved.Add($"~ {was}  ->  {line}");
            }
        }

        foreach (var (rowId, line) in expected)
        {
            if (!actualByRow.ContainsKey(rowId))
            {
                moved.Add($"- {line}");
            }
        }

        foreach (var line in moved)
        {
            output.WriteLine(line);
        }

        Assert.True(moved.Count == 0, $"{moved.Count} refiling outcome(s) differ from {GoldenRelativePath} (+ new, - gone, ~ moved):\n" + string.Join('\n', moved.Take(40)));
        Assert.Equal(ExpectedCounts.LegacyUnlisted + ExpectedCounts.CuratedRetiredListed, actual.Count);
    }

    [Fact]
    public void Rule_outcome_counts_match_the_report_before_and_after_overrides()
    {
        // The rules alone, as the report's probe ran them.
        var rulesOnly = JournalRefiler.Apply(Legacy.All, CuratedData.Empty).Where(q => Legacy.ByRowId[q.RowId].IsUnlisted).ToList();
        Assert.Equal(ExpectedCounts.LegacyUnlisted, rulesOnly.Count);
        var byRule = rulesOnly.GroupBy(q => q.RefiledFrom).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (rule, count) in byRule.OrderBy(kv => kv.Key))
        {
            output.WriteLine($"rule {rule}: {count}");
        }

        Assert.Equal(ExpectedCounts.RetiredByRule1, byRule.GetValueOrDefault((byte)1));
        Assert.Equal(ExpectedCounts.RefiledByRule2, byRule.GetValueOrDefault((byte)2));
        Assert.Equal(ExpectedCounts.RefiledByRule3, byRule.GetValueOrDefault((byte)3));
        Assert.Equal(ExpectedCounts.RefiledByRule4, byRule.GetValueOrDefault((byte)4));
        Assert.Equal(ExpectedCounts.ProgressTrackers, byRule.GetValueOrDefault(JournalRefiler.TrackerRule));
        Assert.Equal(ExpectedCounts.RefiledByRule5, byRule.GetValueOrDefault((byte)5));
        Assert.Equal(ExpectedCounts.RefiledByRule6, byRule.GetValueOrDefault((byte)6));
        Assert.Equal(ExpectedCounts.LeftByRule7, byRule.GetValueOrDefault((byte)7));
        Assert.Equal(ExpectedCounts.RetiredByRule1, rulesOnly.Count(q => q.IsRetired));
        Assert.Equal(ExpectedCounts.RefiledByRules, rulesOnly.Count(q => !q.IsUnlisted));
        Assert.Equal(ExpectedCounts.RetiredByRule1 + ExpectedCounts.LeftByRule7, rulesOnly.Count(q => q.IsUnlisted));

        // With the shipped overrides: six genre-0 rows move by the curated file, the last unlisted one among them.
        var shipped = Catalog.All.Where(q => Legacy.ByRowId[q.RowId].IsUnlisted).ToList();
        Assert.Equal(ExpectedCounts.OverriddenUnlisted, shipped.Count(q => q.RefiledFrom == JournalRefiler.CuratedRule));
        Assert.Equal(ExpectedCounts.RetiredByRule1, shipped.Count(q => q.IsRetired));
        Assert.Equal(ExpectedCounts.RetiredByRule1, shipped.Count(q => q.IsUnlisted));
        Assert.Equal(ExpectedCounts.RefiledByRules + ExpectedCounts.LeftByRule7, shipped.Count(q => !q.IsUnlisted));
        Assert.Equal(90u, Catalog.ByRowId[68478].Journal.GenreId);
        Assert.Equal(90u, Catalog.ByRowId[68148].Journal.GenreId);
        Assert.Equal(90u, Catalog.ByRowId[68149].Journal.GenreId);
        Assert.Equal(107u, Catalog.ByRowId[70180].Journal.GenreId);
        Assert.Equal(117u, Catalog.ByRowId[67752].Journal.GenreId);
        Assert.Equal(103u, Catalog.ByRowId[67923].Journal.GenreId); // What Lies Beneath, Palace of the Dead
        Assert.Equal("The Forbidden Land, Eureka", Catalog.ByRowId[68478].Journal.GenreName);

        // The eight curated retired rows keep their genre and read retired: the five the sheet marks by rule 1
        // (the curated entry only lends its patch note), the 3.05 trio by the curated file alone.
        Assert.Equal(ExpectedCounts.CuratedRetiredListed, fixture.Curated.RetiredQuests.Count);
        foreach (var rowId in fixture.Curated.RetiredQuests.Keys)
        {
            var quest = Catalog.ByRowId[rowId];
            Assert.True(quest.IsRetired, $"{rowId} {quest.Name} should be retired");
            Assert.Equal(JournalRefiler.IsRetiredRow(quest) ? JournalRefiler.RetiredRule : JournalRefiler.CuratedRule, quest.RefiledFrom);
            Assert.NotEqual(0u, quest.Journal.GenreId);
        }

        // Rule 1 alone, no curated file: the five listed rows on the placeholder issuer retire, the 3.05 trio do not.
        var sheetRetiredListed = JournalRefiler.Apply(Legacy.All, CuratedData.Empty).Where(q => q.IsRetired && !q.IsUnlisted).Select(q => q.RowId).OrderBy(id => id).ToList();
        Assert.Equal([66033u, 66034u, 67819u, 68629u, 68727u], sheetRetiredListed);
        Assert.Equal(ExpectedCounts.SheetRetiredListed, sheetRetiredListed.Count);
        Assert.Equal(ExpectedCounts.CuratedOnlyRetiredListed, fixture.Curated.RetiredQuests.Keys.Count(id => !sheetRetiredListed.Contains(id)));

        Assert.Equal(ExpectedCounts.Retired, Catalog.All.Count(q => q.IsRetired));
        Assert.Equal(ExpectedCounts.Retired, Catalog.Removed.Count);
        Assert.DoesNotContain(Catalog.Removed, q => !q.IsRetired);
    }

    [Fact]
    public void Landmarks_land_where_the_report_says()
    {
        Assert.Equal((156u, (byte)2), Where(65713)); // So You Want to Be a Gladiator
        Assert.Equal((234u, (byte)3), Where(67926)); // Squadron and Commander (Maelstrom)
        Assert.Equal((27u, JournalRefiler.TrackerRule), Where(69580)); // A Message from Konogg (a YoRHa progress tracker)
        Assert.Equal((100u, JournalRefiler.TrackerRule), Where(69296)); // The Mendicant's Court (an Ishgardian Restoration tracker)
        Assert.Equal((91u, (byte)4), Where(69563)); // A Seaside Story: level 1 behind a level-80 quest, but a real quest (Disciples of War and Magic)
        Assert.Equal((103u, JournalRefiler.CuratedRule), Where(67923)); // What Lies Beneath
        Assert.Equal((198u, (byte)4), Where(71036)); // Free for All
        Assert.Equal((108u, (byte)5), Where(70187)); // An Odd Job
        Assert.Equal((124u, (byte)5), Where(68477)); // Reach Long and Prosper
        Assert.Equal((128u, (byte)6), Where(68457)); // Leves of Kugane
        Assert.Equal((119u, (byte)6), Where(67643)); // Sights of the North
        Assert.Equal((114u, (byte)4), Where(65973)); // Triple Triad Trial (the Gold Saucer opener is its listed prerequisite)
        Assert.Equal((0u, (byte)1), Where(66269)); // Never Forget (retired, replaced by 69396)
        Assert.Equal((0u, (byte)1), Where(67653)); // I Believe I Can Fly (retired, no replacement)

        foreach (var quest in Catalog.All.Where(q => q.IsUnlisted))
        {
            Assert.Equal(string.Empty, quest.Journal.SectionName);
            Assert.Equal(string.Empty, quest.Journal.CategoryName);
            Assert.Equal(string.Empty, quest.Journal.GenreName);
        }

        (uint, byte) Where(uint rowId)
        {
            var quest = Catalog.ByRowId[rowId];
            return (quest.Journal.GenreId, quest.RefiledFrom);
        }
    }

    [Fact]
    public void Every_quest_lands_in_exactly_one_node()
    {
        var states = Catalog.All.ToDictionary(q => q.RowId, _ => QuestState.Ready);
        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: true);

        // The class intros and the hidden progress trackers sit in a genre node but in none of its numbers
        // (QuestRecord.CountsInTotals); the three job intros filed by the same rule count like any other quest.
        var intros = Catalog.All.Where(q => !q.CountsInTotals && q.RefiledFrom == JournalRefiler.ClassIntroRule).ToList();
        Assert.Equal(ExpectedCounts.UncountedClassIntros, intros.Count);
        Assert.All(intros, q => Assert.True(JournalRefiler.IsStartingClassIntro(q.InternalId), $"{q.RowId} {q.InternalId}"));
        var trackers = Catalog.All.Where(q => q.IsProgressTracker).ToList();
        Assert.Equal(ExpectedCounts.ProgressTrackers, trackers.Count);
        Assert.All(trackers, q => Assert.False(q.CountsInTotals, $"{q.RowId} {q.Name}"));
        Assert.Equal(intros.Count + trackers.Count, Catalog.All.Count(q => !q.CountsInTotals));
        var jobIntros = Catalog.All.Where(q => q.RefiledFrom == JournalRefiler.ClassIntroRule && !JournalRefiler.IsStartingClassIntro(q.InternalId)).ToList();
        Assert.Equal(ExpectedCounts.CountedJobIntros, jobIntros.Count);
        Assert.Equal([67645u, 67646u, 67659u], jobIntros.Select(q => q.RowId).OrderBy(id => id));
        Assert.All(jobIntros, q => Assert.True(q.CountsInTotals, $"{q.RowId} {q.Name}"));
        Assert.Equal(ExpectedCounts.RefiledByRule2, intros.Count + jobIntros.Count);
        Assert.All(Catalog.All.Where(q => q.RefiledFrom is not (JournalRefiler.ClassIntroRule or JournalRefiler.TrackerRule)), q => Assert.True(q.CountsInTotals, $"{q.RowId} {q.Name}"));

        // The repeatables other than the allied society dailies are listed but in no count either.
        var repeatables = Catalog.All.Where(q => !q.IsRemoved && q.CountsInTotals && !q.EntersCounts).ToList();
        Assert.Equal(ExpectedCounts.UncountedRepeatables, repeatables.Count);
        Assert.All(repeatables, q => Assert.True(q.IsRepeatable && !q.IsAlliedSocietyDaily, $"{q.RowId} {q.Name}"));
        var uncounted = Catalog.All.Where(q => !q.IsRemoved && !q.EntersCounts).ToList();
        Assert.Equal(ExpectedCounts.UncountedClassIntros + ExpectedCounts.ProgressTrackers + ExpectedCounts.UncountedRepeatables, uncounted.Count);

        var inGenres = counts.Genres.Values.Sum(c => c.Total);
        Assert.Equal(Catalog.Count, inGenres + counts.Unlisted.Total + uncounted.Count);
        Assert.Equal(Catalog.Count, counts.Overall.Total + uncounted.Count);
        Assert.Equal(inGenres, counts.Sections.Values.Sum(c => c.Total));
        Assert.Equal(inGenres, counts.Categories.Values.Sum(c => c.Total));
        Assert.False(counts.Genres.ContainsKey(0));

        foreach (var quest in Catalog.All)
        {
            var listed = !quest.IsRemoved;
            var removed = quest.IsRemoved;
            Assert.True(listed ^ removed, $"{quest.RowId} {quest.Name}");
            if (!listed)
            {
                continue;
            }

            Assert.Contains(quest, Catalog.ByGenre[quest.Journal.GenreId]);

            // A refiled quest sits in a genre that real journal rows name, with the genre's section and category.
            Assert.NotEqual(0u, quest.Journal.GenreId);
            Assert.NotEqual(string.Empty, quest.Journal.GenreName);
            Assert.NotEqual(string.Empty, quest.Journal.SectionName);
            var neighbour = Catalog.ByGenre[quest.Journal.GenreId].First(q => q.RefiledFrom == 0);
            Assert.Equal(neighbour.Journal with { SortKey = 0 }, quest.Journal with { SortKey = 0 });
            Assert.Equal(neighbour.Journal.SortKey >> 16, quest.Journal.SortKey >> 16);
        }
    }

    [Fact]
    public void Retired_quests_never_enter_a_count_a_query_a_feature_set_a_chain_or_the_zone_lists()
    {
        var retired = Catalog.All.Where(q => q.IsRetired).ToList();
        Assert.Equal(ExpectedCounts.Retired, retired.Count);
        var states = Catalog.All.ToDictionary(q => q.RowId, _ => QuestState.Ready);

        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: false);
        var uncounted = Catalog.All.Count(q => !q.IsRemoved && !q.EntersCounts);
        Assert.Equal(Catalog.Count - retired.Count - uncounted, counts.Overall.Total);
        Assert.Equal(Catalog.Count - retired.Count - uncounted, counts.Sections.Values.Sum(c => c.Total));
        Assert.Equal(retired.Count, counts.Unlisted.Total);
        Assert.Equal(Catalog.ByGenre[18].Count(q => !q.IsRetired && q.EntersCounts), counts.Genre(18).Total);

        var all = QuestQuery.Apply(Catalog, states, new FilterSet(), QuestScope.None, SortSpec.Default, null, QueryContext.Empty);
        Assert.Equal(Catalog.Count - retired.Count, all.Rows.Length);
        Assert.DoesNotContain(all.Rows, r => r.Quest.IsRetired);
        var crystalTower = QuestQuery.Apply(Catalog, states, new FilterSet(), QuestScope.Genre(18), SortSpec.Default, null, QueryContext.Empty);
        Assert.DoesNotContain(crystalTower.Rows, r => r.Quest.IsRetired);
        var removedNode = QuestQuery.Apply(Catalog, states, new FilterSet(), QuestScope.VirtualUnlisted, SortSpec.Default, null, QueryContext.Empty);
        Assert.Equal(retired.Select(q => q.RowId).OrderBy(id => id), removedNode.Rows.Select(r => r.Quest.RowId).OrderBy(id => id));
        var withRemoved = QuestQuery.Apply(Catalog, states, new FilterSet { IncludeUnlisted = true }, QuestScope.None, SortSpec.Default, null, QueryContext.Empty);
        Assert.Equal(Catalog.Count, withRemoved.Rows.Length);

        var features = FeaturePresets.Derive(Catalog, fixture.Curated);
        Assert.DoesNotContain(features, id => Catalog.ByRowId[id].IsRetired);

        var chains = ChainCatalog.Build(Catalog, fixture.Curated);
        Assert.All(retired, q => Assert.Null(chains.ForQuest(q.RowId)));

        Assert.Empty(QuestDiscovery.IssuedBy(Catalog, JournalRefiler.PlaceholderIssuer));

        var msq = MsqProgress.Compute(Catalog, states);
        Assert.NotNull(msq);
        Assert.Equal(Catalog.BySection[0].Count(q => !q.IsRemoved) + Catalog.BySection[1].Count(q => !q.IsRemoved), msq.Total);
    }

    [Fact]
    public void Retired_twins_keep_the_old_completion_and_leave_the_new_quest_undone()
    {
        // The report's A1 group: a retired row whose name a live listed row also carries (the 5.3, 5.5 and 6.x
        // rewrites gave the replacement an Xx* internal id; the 4.x ones kept the old script family).
        var listedByName = Catalog.All.Where(q => !q.IsRemoved).ToLookup(q => q.Name, StringComparer.Ordinal);
        var pairs = new List<(QuestRecord Old, QuestRecord New)>();
        foreach (var old in Catalog.All.Where(q => q.IsRetired && q.RefiledFrom == JournalRefiler.RetiredRule))
        {
            var twin = listedByName[old.Name].OrderByDescending(q => q.InternalId.StartsWith("Xx", StringComparison.Ordinal)).ThenBy(q => q.RowId).FirstOrDefault();
            if (twin is not null)
            {
                pairs.Add((old, twin));
                output.WriteLine($"{old.RowId} {old.InternalId} -> {twin.RowId} {twin.InternalId} \"{old.Name}\"");
            }
        }

        Assert.Equal(ExpectedCounts.RetiredTwins, pairs.Count);
        Assert.True(pairs.Count(p => p.New.InternalId.StartsWith("Xx", StringComparison.Ordinal)) >= 41, "most replacements carry an Xx* internal id");
        var ctx = EvalContext.Default with { ClassJobs = fixture.Bundle.Jobs };
        foreach (var (old, twin) in pairs)
        {
            Assert.NotEqual(old.QuestId, twin.QuestId);
            var snapshot = Fixture.Snapshot(old.RowId);
            Assert.Equal(QuestState.Completed, StateResolver.Resolve(old, snapshot, Catalog, ctx).State);
            var fresh = StateResolver.Resolve(twin, snapshot, Catalog, ctx).State;
            Assert.NotEqual(QuestState.Completed, fresh);
        }

        // Revealed directly, the old row still reads Completed; undone, it reads Locked out with the reason.
        var neverForget = Catalog.ByRowId[66269];
        Assert.True(neverForget.IsRetired);
        var done = StateResolver.Resolve(neverForget, Fixture.Snapshot(66269), Catalog, ctx);
        Assert.Equal(QuestState.Completed, done.State);
        var undone = StateResolver.Resolve(neverForget, Fixture.Snapshot(), Catalog, ctx);
        Assert.Equal(QuestState.Foreclosed, undone.State);
        Assert.Equal("Locked out · removed from the game", BlockerText.StatusText(undone, neverForget, fixture.Bundle.BlockerNames(), null));
    }

    [Fact]
    public void Legacy_filing_reproduces_the_0_6_0_totals()
    {
        Assert.Equal(ExpectedCounts.NamedQuests, Legacy.Count);
        Assert.Equal(ExpectedCounts.LegacyUnlisted, Legacy.All.Count(q => q.IsUnlisted));
        Assert.All(Legacy.All, q => Assert.Equal(0, q.RefiledFrom));
        Assert.All(Legacy.All, q => Assert.False(q.IsRetired));

        // The listing is 0.6.0's; the counts leave out the repeatables other than the allied society dailies (1.2).
        Assert.Equal(ExpectedCounts.LegacyListed, Legacy.All.Count(q => !q.IsRemoved));
        foreach (var (section, total) in ExpectedCounts.LegacySectionTotals)
        {
            Assert.Equal(total, Legacy.BySection[section].Count(q => !q.IsRemoved));
        }

        var states = Legacy.All.ToDictionary(q => q.RowId, _ => QuestState.Ready);
        var counts = TreeCounts.Compute(Legacy, states, includeUnlisted: false);
        Assert.Equal(ExpectedCounts.LegacyListed - Legacy.All.Count(q => !q.IsRemoved && !q.EntersCounts), counts.Overall.Total);
        Assert.Equal(ExpectedCounts.LegacyUnlisted, counts.Unlisted.Total);
        foreach (var section in ExpectedCounts.LegacySectionTotals.Keys)
        {
            Assert.Equal(Legacy.BySection[section].Count(q => !q.IsRemoved && q.EntersCounts), counts.Section(section).Total);
        }

        Assert.Equal(ExpectedCounts.LegacyUnlisted, Legacy.Removed.Count);
    }

    [Fact]
    public void Listed_totals_equal_the_lodestone_counts_plus_the_documented_lag_plus_the_refiled_rows()
    {
        var states = Catalog.All.ToDictionary(q => q.RowId, _ => QuestState.Ready);
        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: false);

        // The lag list: the named rows per section, plus the seasonal rows the report counts but does not name.
        foreach (var (section, lodestone) in ExpectedCounts.LodestoneSectionTotals)
        {
            var lagRows = ExpectedCounts.LodestoneLag.GetValueOrDefault(section) ?? [];
            foreach (var rowId in lagRows)
            {
                var quest = Catalog.ByRowId[rowId];
                Assert.True(!quest.IsRemoved && quest.Journal.SectionId == section && quest.RefiledFrom == 0, $"lag row {rowId} {quest.Name} is not a listed sheet row of section {section}");
            }

            var lag = lagRows.Length + ExpectedCounts.LodestoneLagSectionCounts.GetValueOrDefault(section);
            var refiled = Catalog.BySection[section].Count(q => !q.IsRemoved && q.RefiledFrom != 0 && q.EntersCounts);

            // The Lodestone lists the repeatables; the counts leave out all but the allied society dailies.
            var repeatables = Catalog.BySection[section].Count(q => !q.IsRemoved && q.RefiledFrom == 0 && !q.EntersCounts);
            output.WriteLine($"section {section}: lodestone {lodestone} + lag {lag} + refiled {refiled} - repeatables {repeatables} = {lodestone + lag + refiled - repeatables}; catalog {counts.Section(section).Total}");
            Assert.True(lodestone + lag + refiled - repeatables == counts.Section(section).Total, $"section {section}: lodestone {lodestone} + lag {lag} + refiled {refiled} - repeatables {repeatables} != catalog {counts.Section(section).Total}");
        }

        var lagByCategory = ExpectedCounts.LodestoneLag.Values.SelectMany(ids => ids).ToLookup(id => Catalog.ByRowId[id].Journal.CategoryId);
        foreach (var (category, lodestone) in ExpectedCounts.LodestoneCategoryTotals)
        {
            var lag = lagByCategory[category].Count() + ExpectedCounts.LodestoneLagCategoryCounts.GetValueOrDefault(category);
            var refiled = Catalog.ByCategory[category].Count(q => !q.IsRemoved && q.RefiledFrom != 0 && q.EntersCounts);
            var repeatables = Catalog.ByCategory[category].Count(q => !q.IsRemoved && q.RefiledFrom == 0 && !q.EntersCounts);
            output.WriteLine($"category {category}: lodestone {lodestone} + lag {lag} + refiled {refiled} - repeatables {repeatables} = {lodestone + lag + refiled - repeatables}; catalog {counts.Category(category).Total}");
            Assert.True(lodestone + lag + refiled - repeatables == counts.Category(category).Total, $"category {category}: lodestone {lodestone} + lag {lag} + refiled {refiled} - repeatables {repeatables} != catalog {counts.Category(category).Total}");
        }
    }

    [Fact]
    public void Quasi_quests_join_the_unlock_quests()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        IReadOnlySet<uint> ids = FeaturePresets.Derive(Catalog, fixture.Curated.WithoutFeatureQuests(), unique.Entries);

        var quasi = Catalog.All.Where(q => q.EventIconType == FeaturePresets.QuasiQuestEventIconType).ToList();
        var refiledQuasi = quasi.Where(q => q.RefiledFrom != 0 && !q.IsRemoved).ToList();
        var listedQuasi = quasi.Where(q => q.RefiledFrom == 0 && !q.IsRemoved).ToList();
        output.WriteLine($"EventIconType 10: {quasi.Count} rows, {refiledQuasi.Count} refiled, {listedQuasi.Count} listed, {quasi.Count(q => q.IsRetired)} retired; feature set {ids.Count}");

        Assert.Equal(ExpectedCounts.RefiledQuasiQuests, refiledQuasi.Count);
        Assert.Equal(ExpectedCounts.ListedQuasiQuests, listedQuasi.Count);
        Assert.All(refiledQuasi, q => Assert.Contains(q.RowId, ids));
        Assert.All(listedQuasi, q => Assert.Contains(q.RowId, ids));
        Assert.DoesNotContain(ids, id => Catalog.ByRowId[id].IsRetired);
        Assert.Contains(65713u, ids);
        Assert.Contains(68457u, ids);
        Assert.Contains(68478u, ids);

        Assert.Equal(ExpectedCounts.FeatureQuests, ids.Count);

        // Under Legacy nothing is retired or a tracker, so the same rule keeps the removed rows and the trackers that
        // carry an unlock or the icon; every other member is the same quest, filed elsewhere.
        IReadOnlySet<uint> legacyIds = FeaturePresets.Derive(Legacy, fixture.Curated.WithoutFeatureQuests(), unique.Entries);
        var legacyOnly = legacyIds.Where(id => !ids.Contains(id)).OrderBy(id => id).ToList();
        output.WriteLine($"legacy set {legacyIds.Count}; legacy-only: {string.Join(", ", legacyOnly.Select(id => $"{id} {Catalog.ByRowId[id].Name}"))}");
        Assert.All(legacyOnly, id => Assert.True(Catalog.ByRowId[id].IsRetired || Catalog.ByRowId[id].IsProgressTracker, $"{id} {Catalog.ByRowId[id].Name} left the feature set without being retired or a tracker"));
        Assert.DoesNotContain(ids, id => !legacyIds.Contains(id));
        Assert.Equal(ExpectedCounts.RetiredFeatureRows, legacyOnly.Count(id => Catalog.ByRowId[id].IsRetired));
        Assert.Equal(ExpectedCounts.TrackerFeatureRows, legacyOnly.Count(id => Catalog.ByRowId[id].IsProgressTracker));
    }
}
