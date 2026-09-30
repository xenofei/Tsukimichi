using System.Globalization;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The snapshot consistency suite (feature plan v4, D3–D7): the refiled catalog fixture against the real anonymised
/// character in <c>Fixtures/snapshot-v1.json</c>, taken with no allied society daily turned in (its DailyDone is
/// empty). Nothing may read Done this cycle, every genre the character has finished must count x / x, chains follow
/// their prerequisites and never point at a repeatable, the Job column reads "Any" for all-job quests, and no quest
/// that neither the Lodestone nor the wiki lists may be counted, tagged blue or listed as a chain step.
/// </summary>
public sealed class CountingConsistencyTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private static CharacterSnapshot LoadSnapshot()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.Path, "characters", "1.json"));
        var store = new JsonSnapshotStore(tmp.Path);
        var snapshot = store.Load(1);
        Assert.NotNull(snapshot);
        Assert.Empty(store.Warnings);
        return snapshot;
    }

    private Dictionary<uint, QuestEvaluation> Resolve(CharacterSnapshot snapshot)
    {
        var context = EvalContextBuilder.Build(fixture.Curated.Festivals, fixture.Bundle.Jobs, static () => DateTime.UtcNow, jobParents: fixture.Bundle.JobParents());
        return StateResolver.ResolveAll(Catalog, snapshot, context);
    }

    [Fact]
    public void With_no_daily_turned_in_nothing_reads_done_this_cycle()
    {
        var snapshot = LoadSnapshot();
        Assert.Empty(snapshot.DailyDone);
        var states = Resolve(snapshot);

        var doneThisCycle = states.Where(kv => kv.Value.State == QuestState.DoneThisCycle).Select(kv => kv.Key).ToList();
        Assert.True(doneThisCycle.Count == 0, "read Done this cycle: " + string.Join(", ", doneThisCycle.Select(id => $"{id} {Catalog.ByRowId[id].Name}")));

        // The dailies she has turned in before still count as done: their completion bits stay set.
        var doneBefore = Catalog.All.Where(q => q.IsAlliedSocietyDaily && snapshot.IsCompleted(q.QuestId)).ToList();
        output.WriteLine($"allied society dailies done at least once: {doneBefore.Count}");
        Assert.NotEmpty(doneBefore);
        Assert.All(doneBefore, q => Assert.True(states[q.RowId].RepeatableDoneBefore && states[q.RowId].CountsAsDone, $"{q.RowId} {q.Name}"));

        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: false);
        foreach (var genre in doneBefore.Select(q => q.Journal.GenreId).Distinct())
        {
            var dailies = Catalog.ByGenre[genre].Where(q => q.IsAlliedSocietyDaily && !states[q.RowId].LeavesTotals).ToList();
            Assert.Equal(dailies.Count(q => snapshot.IsCompleted(q.QuestId)), counts.Genre(genre).Done);
        }
    }

    [Fact]
    public void Every_genre_the_character_has_finished_counts_x_of_x()
    {
        var snapshot = LoadSnapshot();
        var states = Resolve(snapshot);
        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: false);

        // Finished: no counted quest left Ready, Blocked, In journal or unknown, the repeatables aside (a genre of
        // dailies is never finished this way, and has no quest to judge).
        var finished = 0;
        var wrong = new List<string>();
        foreach (var (genreId, quests) in Catalog.ByGenre.OrderBy(kv => kv.Key))
        {
            var judged = quests.Where(q => !q.IsRemoved && q.CountsInTotals && !q.IsRepeatable).ToList();
            if (genreId == 0 || judged.Count == 0 || judged.Any(q => IsLeft(states[q.RowId].State)))
            {
                continue;
            }

            finished++;
            var count = counts.Genre(genreId);
            output.WriteLine($"{genreId} {quests[0].Journal.GenreName}: {count.Done}/{count.Total}{(quests.Any(q => q.IsRepeatable || q.IsProgressTracker) ? " (has repeatables or trackers)" : string.Empty)}");
            if (count.Done != count.Total)
            {
                var open = quests.Where(q => q.EntersCounts && !q.IsRemoved && !states[q.RowId].CountsAsDone && !states[q.RowId].LeavesTotals);
                wrong.Add($"{genreId} {quests[0].Journal.GenreName}: {count.Done}/{count.Total} ({string.Join(", ", open.Select(q => $"{q.RowId} {q.Name} {states[q.RowId].State}"))})");
            }
        }

        output.WriteLine($"{finished} finished genres");
        Assert.True(finished >= 25, $"expected the fixture character's finished genres, found {finished}");
        Assert.True(wrong.Count == 0, "finished genres short of x/x:\n" + string.Join('\n', wrong));

        static bool IsLeft(QuestState state) => state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Blocked or QuestState.Accepted or QuestState.Unknown;
    }

    [Fact]
    public void Repeatables_and_trackers_stay_out_of_the_counts_so_finished_chronicles_close()
    {
        var snapshot = LoadSnapshot();
        var states = Resolve(snapshot);
        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: false);

        // Primal Focus (66845) and Unidentified Flying Object (67016) are repeatables of the Primals and Mhach genres.
        foreach (var repeatable in new uint[] { 66845, 67016 })
        {
            var quest = Catalog.ByRowId[repeatable];
            Assert.True(quest.IsRepeatable && !quest.EntersCounts, $"{repeatable} {quest.Name}");
            var genre = quest.Journal.GenreId;
            var counted = Catalog.ByGenre[genre].Where(q => !q.IsRemoved && q.EntersCounts && !states[q.RowId].LeavesTotals).ToList();
            Assert.Equal(counted.Count, counts.Genre(genre).Total);
            Assert.DoesNotContain(counted, q => q.RowId == repeatable);
            output.WriteLine($"{quest.Journal.GenreName}: {counts.Genre(genre).Done}/{counts.Genre(genre).Total}");
        }

        // No tracker in any count, whatever its state.
        var trackers = Catalog.All.Where(q => q.IsProgressTracker).ToList();
        Assert.Equal(ExpectedCounts.ProgressTrackers, trackers.Count);
        var readyTrackers = trackers.Where(q => states[q.RowId].State == QuestState.Ready).ToList();
        foreach (var genre in trackers.Select(q => q.Journal.GenreId).Distinct())
        {
            var counted = Catalog.ByGenre[genre].Count(q => !q.IsRemoved && q.EntersCounts && !states[q.RowId].LeavesTotals);
            Assert.Equal(counted, counts.Genre(genre).Total);
        }

        output.WriteLine($"trackers reading Ready (uncounted): {string.Join(", ", readyTrackers.Select(q => q.Name))}");
    }

    [Fact]
    public void The_job_column_reads_any_for_every_job_categories()
    {
        var bundle = fixture.Bundle;

        // Category 130 admits every job but the limited ones (Blue Mage, Beastmaster): short of the sheet's columns.
        Assert.True(bundle.Names.ClassJobInfos.Any(j => j.IsLimited), "the fixture carries the IsLimitedJob column");
        Assert.Equal(JobGroup.Any, bundle.ClassifyJobs(130).Group);
        Assert.Equal(JobGroup.Any, bundle.ClassifyJobs(1).Group);
        Assert.True(bundle.AdmitsEveryJob(130));
        Assert.False(bundle.Jobs.Admits(130, 36), "Blue Mage is a limited job");

        Assert.Equal(JobGroup.WarAndMagic, bundle.ClassifyJobs(142).Group);
        Assert.Equal(JobGroup.Hand, bundle.ClassifyJobs(33).Group);
        Assert.Equal(JobGroup.Land, bundle.ClassifyJobs(32).Group);
        Assert.Equal((JobGroup.Single, (byte)36), bundle.ClassifyJobs(129));

        var anyRows = Catalog.All.Count(q => !q.IsRemoved && q.ClassJobCategory == 130);
        output.WriteLine($"{anyRows} listed quests of category 130 read Any");
        Assert.True(anyRows >= 216, $"expected the 216 all-job rows the audit found, found {anyRows}");
    }

    [Fact]
    public void Chain_steps_follow_their_prerequisites_and_progress_skips_repeatables_and_the_locked_out()
    {
        var features = FeaturePresets.Derive(Catalog, fixture.Curated);
        var chains = ChainCatalog.Build(Catalog, fixture.Curated, StorySidequests.Build(Catalog, features));
        var states = Resolve(LoadSnapshot());

        var disorder = new List<string>();
        foreach (var chain in chains.Chains)
        {
            var place = new Dictionary<uint, int>();
            for (var i = 0; i < chain.RowIds.Count; i++)
            {
                place[chain.RowIds[i]] = i;
            }

            for (var i = 0; i < chain.RowIds.Count; i++)
            {
                var quest = Catalog.ByRowId[chain.RowIds[i]];
                Assert.False(quest.IsProgressTracker, $"{chain.Name}: {quest.RowId} {quest.Name} is a tracker");
                Assert.False(quest.IsRetired, $"{chain.Name}: {quest.RowId} {quest.Name} is retired");
                foreach (var previous in quest.PreviousQuests.QuestIds)
                {
                    if (place.TryGetValue(previous, out var at) && at > i)
                    {
                        disorder.Add($"{chain.Name}: {quest.RowId} {quest.Name} comes before its prerequisite {previous} {Catalog.ByRowId[previous].Name}");
                    }
                }
            }

            Assert.All(chain.RowIds.Where(id => Catalog.ByRowId[id].IsRepeatable), id => Assert.Contains(id, chain.Uncounted));

            var progress = ChainCatalog.Progress(chain, states);
            if (progress.NextRowId is { } next)
            {
                Assert.False(Catalog.ByRowId[next].IsRepeatable, $"{chain.Name}: next is the repeatable {next} {Catalog.ByRowId[next].Name}");
                Assert.False(states[next].LeavesTotals, $"{chain.Name}: next {next} {Catalog.ByRowId[next].Name} is {states[next].State}");
            }

            var counted = chain.RowIds.Count(id => !chain.Uncounted.Contains(id) && !states[id].LeavesTotals);
            Assert.Equal(counted, progress.Total);
        }

        Assert.True(disorder.Count == 0, string.Join('\n', disorder));

        // The four the audit found out of order: the refiled rows sort by the sheet's SortKey, before their prerequisite.
        var weaponOfChoice = Catalog.ByRowId[69378];
        Assert.Equal((byte)4, weaponOfChoice.RefiledFrom);
        var werlyt = chains.ForQuest(69378);
        Assert.NotNull(werlyt);
        Assert.True(ChainCatalog.IndexOf(werlyt, weaponOfChoice.PreviousQuests.QuestIds[0]) < ChainCatalog.IndexOf(werlyt, 69378));
        Assert.Null(chains.ForQuest(69580)); // A Message from Konogg, a tracker
        Assert.Null(chains.ForQuest(67870)); // Recondition the Anima, a service row
        Assert.Null(chains.ForQuest(69577)); // Forged Anew, a service row
    }

    /// <summary>
    /// The gate behind the tracker rule: a quest the Lodestone does not list (a genre-0 row, which no journal category
    /// holds, or a notListed verdict) and the wiki does not list either (notListed) is not a quest a player can find,
    /// so it may not be counted, tagged blue or listed as a chain step. The verdicts are the external verifier's,
    /// committed as <c>docs/data/quest-verification.csv</c>.
    /// </summary>
    [Fact]
    public void No_counted_feature_or_chain_quest_is_unlisted_by_both_the_lodestone_and_the_wiki()
    {
        // Real quasi-quests the wiki's title lookup misses; neither has a tracker's shape (see JournalRefiler.IsProgressTracker).
        var allowed = new Dictionary<uint, string>
        {
            [67752] = "The New Frontier: the level-60 Diadem entry quasi-quest, filed by refile_overrides.json",
            [69566] = "Open and Inviting: a level-1 Gold Saucer quasi-quest with no prerequisite, which characters complete",
        };

        var lodestoneUnlisted = new HashSet<uint>();
        var wikiUnlisted = new HashSet<uint>();
        foreach (var line in File.ReadLines(Path.Combine(RepositoryRoot(), "docs", "data", "quest-verification.csv")).Skip(1))
        {
            var fields = SplitCsv(line);
            if (fields.Count < 8 || fields[2] != "listed")
            {
                continue;
            }

            var rowId = uint.Parse(fields[0], CultureInfo.InvariantCulture);
            var (catalogValue, source, verdict) = (fields[3], fields[4], fields[7]);
            if (source == "lodestone" && (verdict == "notListed" || catalogValue == "unlisted"))
            {
                lodestoneUnlisted.Add(rowId);
            }
            else if (source == "wiki" && verdict == "notListed")
            {
                wikiUnlisted.Add(rowId);
            }
        }

        var unlisted = lodestoneUnlisted.Intersect(wikiUnlisted).OrderBy(id => id).ToList();
        output.WriteLine($"unlisted by both: {string.Join(", ", unlisted)}");
        Assert.NotEmpty(unlisted);

        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var features = FeaturePresets.Derive(Catalog, fixture.Curated, unique.Entries);
        var chains = ChainCatalog.Build(Catalog, fixture.Curated, StorySidequests.Build(Catalog, features));
        var wrong = new List<string>();
        foreach (var rowId in unlisted)
        {
            if (Catalog.GetByRowId(rowId) is not { } quest || allowed.ContainsKey(rowId))
            {
                continue;
            }

            var counted = !quest.IsRemoved && quest.EntersCounts;
            var feature = features.Contains(rowId);
            var step = chains.ForQuest(rowId) is not null;
            if (counted || feature || step)
            {
                wrong.Add($"{rowId} {quest.Name}: counted={counted} feature={feature} chain={step}");
            }
        }

        Assert.True(wrong.Count == 0, "unlisted by both sources yet shown as a quest:\n" + string.Join('\n', wrong));
        Assert.All(allowed.Keys, id => Assert.Contains(id, unlisted));
    }

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

    /// <summary>One CSV line into fields: commas outside double quotes separate, "" inside quotes is a quote.</summary>
    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }
}
