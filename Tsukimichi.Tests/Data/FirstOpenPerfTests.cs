using System.Diagnostics;
using Lumina;
using Lumina.Data;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Xunit.Abstractions;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// No first-open freezes (feature plan v6 A11), measured. Before 1.13 each of these was built on the draw thread the
/// first time a pane needed it: the Flight tab its zone index, the Plan tab its duty kinds and tags, travel the
/// aetheryte index, Moonlit its icon sheets and merged catalog. The "before" column is what that first frame paid,
/// read cold from a fresh game data instance as the plugin's first open would; the "after" column is what the draw pays
/// now that the plugin warms them on workers at load (<see cref="WarmedValue{T}"/>): one volatile read.
/// </summary>
public class FirstOpenPerfTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    /// <summary>Generous for CI: a build on a worker may take this long without anyone waiting on it.</summary>
    public const int WorkerBudgetMs = 3000;

    /// <summary>What a draw may pay to read a warmed index.</summary>
    public const double ReadBudgetMs = 1.0;

    [GameDataFact]
    [Trait("Category", "Perf")]
    public async Task Every_first_open_index_moves_off_the_frame()
    {
        var path = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)!;
        using var game = new LuminaGameData(path, new LuminaOptions { DefaultExcelLanguage = Language.English, PanicOnSheetChecksumMismatch = false });
        var excel = game.Excel;
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var entries = RewardArtIndexTests.Entries();

        var before = new List<(string Name, double Ms)>();
        double Time(string name, Action build)
        {
            var watch = Stopwatch.StartNew();
            build();
            var ms = watch.Elapsed.TotalMilliseconds;
            before.Add((name, ms));
            return ms;
        }

        PlanDuties? duties = null;
        Time("Flight: zone index", () => FlightIndex.Build(excel, Language.English));
        Time("Plan: duty kinds", () => duties = DutyIndex.Build(excel, Language.English));
        Time("Travel: aetheryte index", () => AetheryteIndex.Build(excel, Language.English));
        Time("Duties section: AutoDuty index", () => DutyRunSheets.Build(excel, Language.English));
        Time("Moonlit: reward art", () => RewardArtIndex.Build(excel, Language.English, entries, icon => game.FileExists(RewardArtIndex.IconPath(icon))));
        UniqueRewardCatalog? rewards = null;
        Time("Moonlit: merged catalog", () => rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated));
        var bundle = fixture.Bundle;
        var features = CatalogIndexes.Build(bundle.Catalog, curated, unique.Entries).FeatureQuestIds;
        Time("Plan: unlock tags", () => UnlockTags.Build(bundle.Catalog, features, rewards!, duties!, bundle.BlockerNames().Tribe));

        // After: the same builds warmed on workers; the "draw" only reads the value.
        var warmed = new WarmedValue<FlightIndex>(() => FlightIndex.Build(excel, Language.English));
        await warmed.Start().WaitAsync(TimeSpan.FromSeconds(30));
        var read = Stopwatch.StartNew();
        var value = warmed.Value;
        var readMs = read.Elapsed.TotalMilliseconds;
        Assert.NotNull(value);

        var total = before.Sum(b => b.Ms);
        output.WriteLine("First open, on the draw thread, before 1.13 (cold):");
        foreach (var (name, ms) in before)
        {
            output.WriteLine($"  {name,-34} {ms,8:0.0} ms");
        }

        output.WriteLine($"  {"total",-34} {total,8:0.0} ms");
        output.WriteLine($"After: built on workers at load; the draw reads a warmed index in {readMs * 1000:0.0} µs.");
        Assert.All(before, b => Assert.True(b.Ms < WorkerBudgetMs, $"{b.Name} took {b.Ms:0} ms"));
        Assert.True(readMs < ReadBudgetMs, $"reading a warmed index took {readMs:0.000} ms");
    }
}
