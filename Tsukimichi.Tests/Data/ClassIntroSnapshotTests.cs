using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The evidence behind <see cref="QuestRecord.CountsInTotals"/>: the class intro quasi-quests ("So You Want to Be
/// a …", refiling rule 2) against the real anonymised character in <c>Fixtures/snapshot-v1.json</c>. Each A Realm
/// Reborn class genre holds two openings: the starter's ("Way of the Lancer" <c>ClsLnc000</c>, no prerequisite, handed
/// to a character who chose the class at creation) and the switcher's (the intro, then "Way of the Lancer"
/// <c>ClsLnc998</c> and "My First Spear"). A character walks exactly one of them, so the class they started as never
/// gets its intro flagged; a genre total that counted the intro would stay one short for good.
/// </summary>
public sealed class ClassIntroSnapshotTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private const uint LancerIntro = 65716;         // So You Want to Be a Lancer (ClsLnc999)
    private const uint StarterWayOfTheLancer = 65559; // Way of the Lancer (ClsLnc000), the starter's opening
    private const uint SwitcherWayOfTheLancer = 65668; // Way of the Lancer (ClsLnc998), after the intro
    private const uint MyFirstSpear = 65754;
    private const uint LancerGenre = 159;

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

    [Fact]
    public void The_starting_class_intro_is_never_flagged_while_every_other_levelled_class_intro_is()
    {
        var catalog = fixture.Bundle.Catalog;
        var snapshot = LoadSnapshot();
        var jobs = fixture.Bundle.Names.ClassJobInfos.Where(j => j.RowId != 0).ToDictionary(j => j.RowId);

        var intros = catalog.All.Where(q => q.RefiledFrom == JournalRefiler.ClassIntroRule).ToList();
        Assert.Equal(ExpectedCounts.RefiledByRule2, intros.Count);
        Assert.All(intros, q => Assert.True(JournalRefiler.IsClassIntro(q.InternalId)));
        // The class intros stay out of the totals; the job intros (nobody starts as a job) count, and hers are done.
        Assert.All(intros.Where(q => JournalRefiler.IsStartingClassIntro(q.InternalId)), q => Assert.False(q.CountsInTotals, $"{q.RowId} {q.Name} should stay out of the totals"));
        var jobIntros = intros.Where(q => !JournalRefiler.IsStartingClassIntro(q.InternalId)).ToList();
        Assert.Equal(ExpectedCounts.CountedJobIntros, jobIntros.Count);
        Assert.All(jobIntros, q => Assert.True(q.CountsInTotals, $"{q.RowId} {q.Name} should count"));
        Assert.True(snapshot.IsCompleted(catalog.ByRowId[67646].QuestId), "A Dark Spectacle (DRK) is done");
        Assert.True(snapshot.IsCompleted(catalog.ByRowId[67659].QuestId), "What's Your Sign (AST) is done");

        // Every class with a level (jobs inherit their base class), with the base class's intro row.
        var undone = new List<string>();
        var flagged = new List<string>();
        foreach (var (jobId, level) in snapshot.JobLevels.OrderBy(kv => kv.Key))
        {
            var job = jobs[jobId];
            if (job.ParentRowId != job.RowId)
            {
                continue; // a job: its class carries the intro
            }

            var intro = intros.FirstOrDefault(q => q.Name.EndsWith(" " + job.Name, StringComparison.Ordinal));
            if (intro is null)
            {
                continue; // Dark Knight, Samurai, Blue Mage: no class intro of that shape
            }

            var done = snapshot.IsCompleted(intro.QuestId);
            (done ? flagged : undone).Add(job.Abbreviation);
            output.WriteLine($"{job.Abbreviation} Lv {level,3}: {intro.RowId} {intro.Name} done={done}");
        }

        // Nine levelled classes have their intro flagged; the one she started as (Lancer, Gridania) does not.
        Assert.Equal(["GLA", "PGL", "MRD", "ARC", "CNJ", "CUL", "BTN", "FSH", "ROG"], flagged);
        Assert.Equal(["LNC"], undone);
        Assert.True(snapshot.JobLevels[4] >= 60, "Lancer is levelled (shared with Dragoon)");

        // The starter's opening is done; the switcher's opening, the intro included, is not and never can be.
        Assert.True(snapshot.IsCompleted(catalog.ByRowId[StarterWayOfTheLancer].QuestId));
        Assert.False(snapshot.IsCompleted(catalog.ByRowId[LancerIntro].QuestId));
        Assert.False(snapshot.IsCompleted(catalog.ByRowId[SwitcherWayOfTheLancer].QuestId));
        Assert.False(snapshot.IsCompleted(catalog.ByRowId[MyFirstSpear].QuestId));
        Assert.Equal([LancerIntro], catalog.ByRowId[SwitcherWayOfTheLancer].PreviousQuests.QuestIds);
        Assert.Empty(catalog.ByRowId[StarterWayOfTheLancer].PreviousQuests.QuestIds);
        Assert.Equal(LancerGenre, catalog.ByRowId[LancerIntro].Journal.GenreId);
        Assert.Equal(LancerGenre, catalog.ByRowId[StarterWayOfTheLancer].Journal.GenreId);
    }

    [Fact]
    public void The_lancer_genre_total_leaves_the_intro_out_while_the_table_still_lists_it()
    {
        var catalog = fixture.Bundle.Catalog;
        var snapshot = LoadSnapshot();
        var context = EvalContextBuilder.Build(fixture.Curated.Festivals, fixture.Bundle.Jobs, static () => DateTime.UtcNow, jobParents: fixture.Bundle.JobParents());
        var states = StateResolver.ResolveAll(catalog, snapshot, context);
        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: false);

        // The intro evaluates Ready for her (nothing gates it), and would sit in the genre's total for good.
        Assert.Equal(QuestState.Ready, states[LancerIntro].State);
        Assert.False(states[LancerIntro].LeavesTotals);

        var genre = catalog.ByGenre[LancerGenre];
        Assert.Contains(genre, q => q.RowId == LancerIntro);
        var counted = genre.Where(q => !q.IsRemoved && q.CountsInTotals).ToList();
        Assert.Equal(genre.Count(q => !q.IsRemoved) - 1, counted.Count);
        Assert.Equal(counted.Count(q => !states[q.RowId].LeavesTotals), counts.Genre(LancerGenre).Total);
        Assert.Equal(counted.Count(q => states[q.RowId].State == QuestState.Completed), counts.Genre(LancerGenre).Done);

        // Still a row of the genre for the table, search and reveal, and still an unlock quest.
        var rows = QuestQuery.Apply(catalog, states, new FilterSet(), QuestScope.Genre(LancerGenre), SortSpec.Default, null, QueryContext.Empty);
        Assert.Contains(rows.Rows, r => r.Quest.RowId == LancerIntro);
        IReadOnlySet<uint> features = FeaturePresets.Derive(catalog, fixture.Curated);
        Assert.Contains(LancerIntro, features);
        output.WriteLine($"Lancer genre: {genre.Count} rows, {counts.Genre(LancerGenre).Done}/{counts.Genre(LancerGenre).Total} counted");
    }
}
