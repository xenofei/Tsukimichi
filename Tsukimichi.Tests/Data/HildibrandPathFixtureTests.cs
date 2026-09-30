using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The T16 gate on the frozen catalog: Hildibrand's 57-quest line (which pulls in the main scenario its later
/// chapters require) builds a star chart with folded runs, the target last, and a scroll that puts the target at 60 %.
/// </summary>
public class HildibrandPathFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>The chart child's minimum height (path-section proposal §4.12), logical px.</summary>
    private const float ChartMinHeight = 160f;

    [Fact]
    public void Hildibrand_mid_chain_target_folds_the_walked_road_and_scrolls_to_the_target()
    {
        var catalog = fixture.Bundle.Catalog;
        var chain = ChainCatalog.Build(catalog, fixture.Curated).Chains.First(c => c.Name == "Hildibrand");
        Assert.Equal(57, chain.RowIds.Count);

        // A character who has walked everything up to the 41st Hildibrand quest: the 42nd is Ready, the rest to the
        // target (the 46th) are Blocked behind it, and every other quest on the path is done.
        var target = chain.RowIds[45];
        var undone = chain.RowIds.Skip(41).Take(5).ToHashSet();
        var states = new Dictionary<uint, QuestEvaluation>();
        foreach (var step in PathFinder.PathTo(target, catalog, states))
        {
            var state = !undone.Contains(step.RowId) ? QuestState.Completed
                : step.RowId == chain.RowIds[41] ? QuestState.Ready
                : QuestState.Blocked;
            states[step.RowId] = new QuestEvaluation(state, [], null, null, null);
        }

        var path = PathFinder.PathTo(target, catalog, states);
        var rows = PathRows.Build(path, id => catalog.GetByRowId(id)?.Expansion ?? 0, PathFinder.Alternatives(path, catalog, states));
        output.WriteLine($"{path.Count} steps → {rows.Count} rows: {rows.Count(r => r.Kind == PathRowKind.Band)} bands, {rows.Count(r => r.Kind == PathRowKind.FoldedRun)} folded runs");

        // The target is the last row and the right quest; the five undone Hildibrand quests are listed in chain order.
        var targetIndex = PathRows.TargetIndex(rows);
        Assert.Equal(rows.Count - 1, targetIndex);
        Assert.Equal(target, rows[targetIndex].RowId);
        Assert.Equal(path.Count - 1, rows[targetIndex].PathIndex);
        Assert.Equal(chain.RowIds.Skip(41).Take(5), rows.Where(r => r.Kind == PathRowKind.Step && undone.Contains(r.RowId)).Select(r => r.RowId));

        // Hundreds of walked steps fold into a handful of beads, and every step is accounted for once.
        Assert.True(path.Count > 500, $"expected the main scenario on the path, got {path.Count} steps");
        Assert.True(rows.Count(r => r.Kind == PathRowKind.FoldedRun) >= 3);
        Assert.True(rows.Count < 40, $"expected the walked road folded, got {rows.Count} rows");
        var covered = rows.Where(r => r.Kind == PathRowKind.Step).Select(r => r.PathIndex)
            .Concat(rows.Where(r => r.Kind == PathRowKind.FoldedRun).SelectMany(r => Enumerable.Range(r.PathIndex, r.Count)))
            .OrderBy(i => i);
        Assert.Equal(Enumerable.Range(0, path.Count), covered);

        // The completed step the gold thread arrives from is never folded.
        for (var i = 0; i + 1 < path.Count; i++)
        {
            if (path[i].Done && !path[i + 1].Done)
            {
                Assert.Contains(rows, r => r.Kind == PathRowKind.Step && r.PathIndex == i);
            }
        }

        // Laid out at logical heights with the Unlocks next tail under the target (its header, one row per unlock and
        // the trailing dots), the chart is taller than its smallest view, so it scrolls; scrolled to the target, the
        // target's centre sits at 60 % of the view.
        var y = 0f;
        var targetCentre = 0f;
        for (var i = 0; i < rows.Count; i++)
        {
            if (i == targetIndex)
            {
                targetCentre = y + (PathRows.LogicalHeight(rows[i]) * 0.5f);
            }

            y += PathRows.LogicalHeight(rows[i]);
        }

        var unlocks = catalog.All.Count(q => Array.IndexOf(q.PreviousQuests.QuestIds, target) >= 0);
        Assert.True(unlocks >= 1, "the 46th Hildibrand quest unlocks the 47th");
        y += PathRows.UnlocksTailHeight(unlocks, more: false);

        Assert.True(y > ChartMinHeight, $"chart is {y} px, expected it to scroll in a {ChartMinHeight} px view");
        var scroll = PathRows.ScrollFor(targetCentre, y, ChartMinHeight);
        output.WriteLine($"chart {y} px, target centre {targetCentre}, scroll {scroll}");
        Assert.Equal(ChartMinHeight * PathRows.TargetViewFraction, targetCentre - scroll, 3);

        // Each band has its pre-seeded sky.
        var bands = StarField.ForPath(rows);
        Assert.Equal(rows.Count(r => r.Kind == PathRowKind.Band), bands.Count);
        Assert.All(bands, b => Assert.Equal(StarField.Seed(b.Expansion, b.RowCount), b.Seed));
    }

    [Fact]
    public void Scrolling_to_the_target_clamps_at_the_ends()
    {
        Assert.Equal(0f, PathRows.ScrollFor(50f, 400f, 160f));
        Assert.Equal(240f, PathRows.ScrollFor(390f, 400f, 160f));
        Assert.Equal(104f, PathRows.ScrollFor(200f, 400f, 160f));
        Assert.Equal(0f, PathRows.ScrollFor(100f, 120f, 160f));
    }
}
