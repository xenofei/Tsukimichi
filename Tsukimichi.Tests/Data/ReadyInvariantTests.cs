using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Ready never shows while a requirement is unmet (feature plan v4 L8), over the frozen catalog and the real anonymised
/// character in <c>Fixtures/snapshot-v1.json</c>, on her current job and on the three other jobs she has levelled
/// most. For every state the resolver gives: a Ready quest meets every requirement on the current job; a quest Ready on
/// another job misses only the class or job and the level here, and meets every requirement on the job it names; only
/// those two states' status lines open with the word Ready; and the detail pane's "Not yet" callout shows exactly for
/// the quests the character cannot take on the current job, naming at least one thing for a Blocked one.
/// </summary>
public sealed class ReadyInvariantTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context() =>
        EvalContextBuilder.Build(fixture.Curated.Festivals, fixture.Bundle.Jobs, static () => DateTime.UtcNow, jobParents: fixture.Bundle.JobParents());

    private static CharacterSnapshot LoadSnapshot()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.Path, "characters", "1.json"));
        var store = new JsonSnapshotStore(tmp.Path);
        var snapshot = store.Load(1);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    [Fact]
    public void Ready_never_shows_while_a_requirement_is_unmet()
    {
        var snapshot = LoadSnapshot();
        var context = Context();
        var names = fixture.Bundle.BlockerNames();
        var jobs = new List<byte> { snapshot.CurrentJob };
        jobs.AddRange(snapshot.JobLevels
            .Where(kv => kv.Value > 0 && kv.Key != snapshot.CurrentJob)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(3)
            .Select(kv => kv.Key));

        var offenders = new List<string>();
        var counts = new Dictionary<QuestState, int>();
        var readyOn = new Dictionary<byte, HashSet<uint>>();
        var resolved = new Dictionary<byte, Dictionary<uint, QuestEvaluation>>();
        foreach (var job in jobs)
        {
            var states = StateResolver.ResolveAll(Catalog, snapshot with { CurrentJob = job }, context);
            resolved[job] = states;
            foreach (var (rowId, evaluation) in states)
            {
                var quest = Catalog.ByRowId[rowId];
                var where = $"job {job}: {rowId} {quest.Name} ({evaluation.State})";
                counts[evaluation.State] = counts.GetValueOrDefault(evaluation.State) + 1;
                var unmet = evaluation.Requirements.Where(static r => !r.Met).ToList();
                switch (evaluation.State)
                {
                    case QuestState.Ready when unmet.Count > 0:
                        offenders.Add($"{where} is Ready with unmet {string.Join(", ", unmet.Select(static r => r.Req.Kind))}");
                        break;
                    case QuestState.ReadyOnOtherJob:
                        if (unmet.Count == 0 || unmet.Any(static r => r.Req.Kind is not (RequirementKind.ClassJob or RequirementKind.Level)))
                        {
                            offenders.Add($"{where} misses more than the job here: {string.Join(", ", unmet.Select(static r => r.Req.Kind))}");
                        }

                        if (evaluation.ReadyOnJob is not { } other || other == job)
                        {
                            offenders.Add($"{where} names no other job");
                        }
                        else
                        {
                            if (!readyOn.TryGetValue(other, out var rows))
                            {
                                rows = [];
                                readyOn[other] = rows;
                            }

                            rows.Add(rowId);
                        }

                        break;
                }

                // Only Ready and Ready on another job read "Ready…" anywhere the status line is shown.
                var status = BlockerText.StatusText(evaluation, quest, names, states);
                var readsReady = status.StartsWith(StateNames.Ready, StringComparison.Ordinal);
                if (readsReady != (evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob))
                {
                    offenders.Add($"{where} reads \"{status}\"");
                }

                // The "Not yet" callout: exactly for a quest the character cannot take on this job.
                var callout = NotYetText.Callout(evaluation, quest, names, states);
                var cannotTake = evaluation.State is QuestState.Blocked or QuestState.Foreclosed or QuestState.ReadyOnOtherJob;
                if ((callout is not null) != cannotTake)
                {
                    offenders.Add($"{where} callout \"{callout?.Text}\"");
                }
                else if (evaluation.State == QuestState.Blocked && callout!.Text == NotYetText.Lead)
                {
                    offenders.Add($"{where} callout names nothing");
                }
            }
        }

        // On the job a quest is Ready on, it misses nothing.
        foreach (var (job, rows) in readyOn)
        {
            if (!resolved.TryGetValue(job, out var there))
            {
                there = StateResolver.ResolveAll(Catalog, snapshot with { CurrentJob = job }, context);
                resolved[job] = there;
            }

            foreach (var rowId in rows)
            {
                var unmet = there[rowId].Requirements.Where(static r => !r.Met).Select(static r => r.Req.Kind).ToList();
                if (unmet.Count > 0 || there[rowId].State != QuestState.Ready)
                {
                    offenders.Add($"job {job}: {rowId} {Catalog.ByRowId[rowId].Name} is Ready on it by its other job, yet reads {there[rowId].State} there with unmet {string.Join(", ", unmet)}");
                }
            }
        }

        output.WriteLine($"jobs checked: {string.Join(", ", jobs)}; ready-on jobs re-resolved: {string.Join(", ", readyOn.Keys)}");
        foreach (var (state, count) in counts.OrderBy(static kv => kv.Key))
        {
            output.WriteLine($"{state}: {count}");
        }

        Assert.True(counts.GetValueOrDefault(QuestState.Ready) > 0, "expected Ready quests in the fixture");
        Assert.True(counts.GetValueOrDefault(QuestState.Blocked) > 0, "expected Blocked quests in the fixture");
        Assert.True(counts.GetValueOrDefault(QuestState.Foreclosed) > 0, "expected Locked out quests in the fixture");
        Assert.True(offenders.Count == 0, $"{offenders.Count} offenders:{Environment.NewLine}{string.Join(Environment.NewLine, offenders.Take(40))}");
    }
}
