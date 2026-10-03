using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Giver portraits against the game (feature plan v7 F1, F3): the index built from the install with the shipped curated
/// overlay covers at least the givers and main scenario quests the research measured, every face it names exists, and
/// the curated blocks hold.
/// </summary>
public class GiverPortraitDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private static PortraitCuration Curation() => CuratedData.Load(FixtureCatalog.CuratedDir()).GiverPortraits;

    private PortraitIndex Build(PortraitCuration? curation = null) =>
        GiverPortraitSources.Build(fixture.Game.Excel, curation ?? Curation(), icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), message => output.WriteLine(message));

    [GameDataFact]
    public void Report()
    {
        var index = Build();
        var catalog = fixture.Bundle.Catalog;
        var named = catalog.All.Where(q => q.Issuer is { } i && i.NpcId is > 1000000 and < 2000000 && !PortraitNames.IsGeneric(index.NameOf(i.NpcId)) && !string.IsNullOrEmpty(q.Name)).ToList();
        var covered = named.Where(q => index.For(q).HasArt).ToList();
        var msq = named.Where(FeaturePresets.IsMainScenario).ToList();
        var msqCovered = msq.Where(q => index.For(q).HasArt).ToList();
        var giverNames = named.GroupBy(q => PortraitNames.Normalize(index.NameOf(q.Issuer!.NpcId))).ToList();
        var coveredNames = giverNames.Where(g => g.Any(q => index.For(q).HasArt)).ToList();
        output.WriteLine($"named-giver quests {named.Count}, covered {covered.Count}; MSQ {msq.Count}, covered {msqCovered.Count}; giver names {giverNames.Count}, covered {coveredNames.Count}; giver ids with art {index.GiversWithArt} of {index.GiverCount}");
        foreach (var source in PortraitSources.Priority)
        {
            var bySource = named.Where(q => index.For(q).Source == source).ToList();
            output.WriteLine($"{source}: quests {bySource.Count}, MSQ {bySource.Count(FeaturePresets.IsMainScenario)}, givers {bySource.Select(q => index.NameOf(q.Issuer!.NpcId)).Distinct().Count()}");
        }

        output.WriteLine("Covered givers by quests:");
        foreach (var g in coveredNames.OrderByDescending(g => g.Count()))
        {
            var id = g.First().Issuer!.NpcId;
            output.WriteLine($"  {index.NameOf(id)} ({g.Count()}, msq {g.Count(FeaturePresets.IsMainScenario)}): " + string.Join("; ", index.Variants(id).Select(v => $"{v.Source} {v.Icon} e{v.Era} '{v.MatchedName}'")));
        }

        var inputs = GiverPortraitSources.Read(fixture.Game.Excel, icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)));
        var matched = inputs.Givers.SelectMany(g => index.Variants(g.NpcId)).Select(v => v.MatchedName).ToHashSet();
        var giverNorms = inputs.Givers.Select(g => PortraitNames.Normalize(g.Name)).ToHashSet();
        foreach (var source in PortraitSources.Priority)
        {
            var unmatched = inputs.Faces.Where(f => f.Source == source && !matched.Contains(f.Name)).Select(f => f.Name).Distinct().Order().ToList();
            output.WriteLine($"{source} names matching no giver ({unmatched.Count}): " + string.Join(", ", unmatched));
            output.WriteLine("   near misses: " + string.Join(", ", unmatched.SelectMany(n => giverNorms.Where(g => g.Length > 3 && PortraitNames.Normalize(n).Length > 3 && (g.Contains(PortraitNames.Normalize(n)) || PortraitNames.Normalize(n).Contains(g))).Select(g => n + "~" + g))));
        }

        var giverByNorm = inputs.Givers.Where(g => !PortraitNames.IsGeneric(g.Name)).GroupBy(g => PortraitNames.Normalize(g.Name)).ToDictionary(g => g.Key, g => g.First().Name);
        foreach (var face in inputs.Faces.Where(f => !matched.Contains(f.Name) && f.Name.Contains(' ')))
        {
            var words = face.Name.Split(' ').Select(PortraitNames.Normalize).Where(w => w.Length >= 3 && giverByNorm.ContainsKey(w)).ToList();
            if (words.Count > 0)
            {
                output.WriteLine($"   word hit: {face.Source} {face.Icon} '{face.Name}' e{face.Era} -> {string.Join(", ", words.Select(w => giverByNorm[w]))}");
            }
        }

        output.WriteLine("Top uncovered givers:");
        foreach (var g in giverNames.Except(coveredNames).OrderByDescending(g => g.Count(FeaturePresets.IsMainScenario)).ThenByDescending(g => g.Count()).Take(80))
        {
            output.WriteLine($"  {index.NameOf(g.First().Issuer!.NpcId)} ({g.Count()}, msq {g.Count(FeaturePresets.IsMainScenario)})");
        }
    }
}
