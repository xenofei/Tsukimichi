using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// <see cref="RequirementDetail.Render"/> is what a translation of the detail pane's requirement clause is built from,
/// at display time; in English it must say exactly what the evaluator wrote into <see cref="RequirementResult.Detail"/>
/// (V2-19). Checked over a whole resolve of the fixture catalog for a mid-game character, so every requirement kind the
/// data holds is compared, met and unmet.
/// </summary>
public class RequirementDetailTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    [Fact]
    public void Rendered_english_matches_the_evaluator_for_every_requirement_of_the_catalog()
    {
        var bundle = fixture.Bundle;
        var catalog = bundle.Catalog;
        var context = EvalContextBuilder.Build(new Dictionary<ushort, FestivalInfo>(), bundle.Jobs, static () => DateTime.UtcNow, jobParents: bundle.JobParents());
        var done = catalog.All.Where((_, i) => i % 3 == 0).Select(q => q.RowId).ToArray();
        var snapshot = Fixture.Snapshot(done) with
        {
            CurrentJob = 22,
            JobLevels = Fixture.Levels((4, 90), (22, 90), (1, 50), (19, 50), (6, 30)),
            ActiveFestivals = [48],
        };

        var states = StateResolver.ResolveAll(catalog, snapshot, context);
        var names = new BlockerNames { Catalog = catalog };
        var kinds = new HashSet<RequirementKind>();
        var compared = 0;
        var offenders = new List<string>();
        foreach (var (rowId, evaluation) in states)
        {
            foreach (var result in evaluation.Requirements)
            {
                if (result.Req is ClassJobRequirement)
                {
                    // Says "the current job" or "this job" by the snapshot's job, which Render is told separately.
                    var rendered = RequirementDetail.Render(result, names, snapshot.CurrentJob);
                    if (rendered != result.Detail && RequirementDetail.Render(result, names, 0) != result.Detail)
                    {
                        offenders.Add($"{rowId} {result.Req.Kind}: \"{rendered}\" vs \"{result.Detail}\"");
                    }

                    compared++;
                    kinds.Add(result.Req.Kind);
                    continue;
                }

                var text = RequirementDetail.Render(result, names, snapshot.CurrentJob);
                if (text is null)
                {
                    continue;
                }

                compared++;
                kinds.Add(result.Req.Kind);
                if (text != result.Detail)
                {
                    offenders.Add($"{rowId} {result.Req.Kind}: \"{text}\" vs \"{result.Detail}\"");
                }
            }
        }

        Assert.True(compared > 1000, $"expected many requirements, compared {compared}");
        Assert.Contains(RequirementKind.Level, kinds);
        Assert.Contains(RequirementKind.PreviousQuests, kinds);
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders.Take(40)));
    }
}
