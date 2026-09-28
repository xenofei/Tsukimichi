using System.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The poller's first pass resolves the whole catalog for a character. It runs off the framework thread now, but
/// the cost still bounds how long the Journal shows "evaluating" after login, and a regression here would also hit
/// the level-up full resolve, which stays on the framework thread.
/// </summary>
public class ResolveAllPerfTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>Generous for CI; the owner's machine resolves the fixture in a few tens of milliseconds.</summary>
    public const int BudgetMs = 500;

    [Fact]
    [Trait("Category", "Perf")]
    public void Full_resolve_of_the_fixture_catalog_stays_under_budget()
    {
        var bundle = fixture.Bundle;
        var catalog = bundle.Catalog;
        var context = EvalContextBuilder.Build(new Dictionary<ushort, FestivalInfo>(), bundle.Jobs, static () => DateTime.UtcNow, jobParents: bundle.JobParents());

        // A mid-game character: every third quest done, a journal, levels on a few jobs, one festival running.
        var done = catalog.All.Where((_, i) => i % 3 == 0).Select(q => q.RowId).ToArray();
        var inJournal = catalog.All.Where((_, i) => i % 3 == 1).Take(2).Select(q => Fixture.Accepted(q.RowId, 2)).ToArray();
        var snapshot = Fixture.Snapshot(done) with
        {
            CurrentJob = 22,
            JobLevels = Fixture.Levels((4, 90), (22, 90), (1, 50), (19, 50), (6, 30)),
            Accepted = inJournal,
            ActiveFestivals = [48],
        };

        // Warm up the JIT once, then measure a full pass.
        var warm = StateResolver.ResolveAll(catalog, snapshot, context);
        Assert.Equal(catalog.Count, warm.Count);

        var watch = Stopwatch.StartNew();
        var states = StateResolver.ResolveAll(catalog, snapshot, context);
        watch.Stop();

        output.WriteLine($"ResolveAll over {catalog.Count} quests: {watch.Elapsed.TotalMilliseconds:0.0} ms");
        Assert.Equal(catalog.Count, states.Count);
        Assert.Contains(states.Values, e => e.State == QuestState.Completed);
        Assert.Contains(states.Values, e => e.State == QuestState.Accepted);
        Assert.True(watch.ElapsedMilliseconds < BudgetMs, $"full resolve took {watch.ElapsedMilliseconds} ms, budget {BudgetMs} ms");
    }
}
