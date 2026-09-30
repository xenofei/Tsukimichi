using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The detail pane's "Not yet" callout and jump buttons (feature plan v4 L8) over the frozen catalog and the real
/// anonymised character in <c>Fixtures/snapshot-v1.json</c>, on every job she has levelled (the release 1.3 review's
/// probe): no callout or gap meter compares the level of a job the quest does not admit; every option of a choice says
/// "Choose one of N"; and no jump starts from a Locked out quest or lands on a quest removed from the game or Locked out.
/// </summary>
public sealed class NotYetCalloutDataTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const uint VitalTitle = 66097;
    private const uint CloseToHome = 65644;
    private const uint SleeplessInTheStable = 65825;
    private const uint WhatsItToU = 66557;
    private const uint CantDoItWithoutU = 66552;

    private static CharacterSnapshot LoadSnapshot()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.Path, "characters", "1.json"));
        var snapshot = new JsonSnapshotStore(tmp.Path).Load(1);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    [Fact]
    public void Callouts_and_jumps_hold_on_every_levelled_job()
    {
        var bundle = fixture.Bundle;
        var catalog = bundle.Catalog;
        var context = EvalContextBuilder.Build(fixture.Curated.Festivals, bundle.Jobs, static () => DateTime.UtcNow, jobParents: bundle.JobParents(), jobRoles: bundle.JobRoles());
        var names = bundle.BlockerNames();
        uint Unlock(uint job) => bundle.Names.ClassJobInfos.FirstOrDefault(i => i.RowId == job)?.UnlockQuestRowId ?? 0u;

        var snapshot = LoadSnapshot();
        var jobs = new List<byte> { snapshot.CurrentJob };
        jobs.AddRange(snapshot.JobLevels.Where(kv => kv.Value > 0 && kv.Key != snapshot.CurrentJob).Select(kv => kv.Key));
        var offenders = new List<string>();
        var choices = 0;
        var measured = 0;
        foreach (var job in jobs)
        {
            var s = snapshot with { CurrentJob = job };
            var states = StateResolver.ResolveAll(catalog, s, context);
            foreach (var (rowId, raw) in states)
            {
                var quest = catalog.ByRowId[rowId];
                var shown = NotYetText.OnAdmittedJob(raw, quest, s, context);
                var where = $"job {job}: {rowId} {quest.Name} ({raw.State})";
                var notAdmitted = shown.Requirements.Any(static r => r.Req is ClassJobRequirement && !r.Met);
                var callout = NotYetText.Callout(shown, quest, names, states);
                if (notAdmitted && shown.Requirements.Any(static r => r.Req is LevelRequirement { MeasuredOn: 0, NoJob: false }))
                {
                    offenders.Add($"{where}: level measured on a job the quest does not admit");
                }

                measured += notAdmitted && shown.Requirements.Any(static r => r.Req is LevelRequirement { MeasuredOn: not 0 }) ? 1 : 0;
                if (callout is not null && notAdmitted && raw.State == QuestState.ReadyOnOtherJob && callout.Text.Contains("level", StringComparison.Ordinal))
                {
                    offenders.Add($"{where}: '{callout.Text}' names a level beside the job it is ready on");
                }

                if (callout is not null && raw.ChoiceOf > 1 && raw.State is QuestState.Blocked or QuestState.ReadyOnOtherJob)
                {
                    choices++;
                    if (!callout.Text.EndsWith(PathText.ChooseOne(raw.ChoiceOf), StringComparison.Ordinal))
                    {
                        offenders.Add($"{where}: '{callout.Text}' lost its choice");
                    }
                }

                foreach (var result in shown.Requirements)
                {
                    if (NotYetText.JumpTarget(result, quest, catalog, states, Unlock) is not { } target)
                    {
                        continue;
                    }

                    var to = catalog.ByRowId[target];
                    if (raw.State == QuestState.Foreclosed)
                    {
                        offenders.Add($"{where}: jumps to {target} from a Locked out quest");
                    }

                    if (to.IsRemoved || states.GetValueOrDefault(target)?.State == QuestState.Foreclosed)
                    {
                        offenders.Add($"{where}: jumps to {target} {to.Name}, which can never be taken");
                    }
                }
            }

            // The review's examples.
            Assert.EndsWith("Choose one of 2", NotYetText.Callout(states[VitalTitle], catalog.ByRowId[VitalTitle], names, states)!.Text);
            Assert.All(states[CloseToHome].Requirements, r => Assert.Null(NotYetText.JumpTarget(r, catalog.ByRowId[CloseToHome], catalog, states, Unlock)));
            Assert.All(states[SleeplessInTheStable].Requirements, r => Assert.Null(NotYetText.JumpTarget(r, catalog.ByRowId[SleeplessInTheStable], catalog, states, Unlock)));
            Assert.All(states[WhatsItToU].Requirements, r => Assert.NotEqual(CantDoItWithoutU, NotYetText.JumpTarget(r, catalog.ByRowId[WhatsItToU], catalog, states, Unlock)));
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders.Take(40)));
        Assert.True(choices > 0, "no option of a choice was checked");
        Assert.True(measured > 0, "no level was measured on another job");
    }
}
