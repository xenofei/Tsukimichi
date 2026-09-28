using System.Diagnostics;
using Lumina.Data;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

/// <summary>
/// Offline generator: local game files -> unique_quests.json plus the review reports under docs/data.
/// Usage: Tsukimichi.DataGen --game "<sqpack path>" --out <unique_quests.json> [--curated <dir>] [--reports <dir>] [--keep-nonexclusive-items]
///        Tsukimichi.DataGen --verify --game "<sqpack path>" [--data <unique_quests.json>] [--report <verification-report.md>] [--no-xivapi] [--sample N] [--seed N]
///        Tsukimichi.DataGen --dump-catalog <file.json.gz or directory> --game "<sqpack path>"
/// </summary>
public static class Program
{
    private const string DefaultData = "Tsukimichi/Data/unique_quests.json";
    private const string DefaultVerifyReport = "docs/data/verification-report.md";

    public static int Main(string[] args)
    {
        if (args.Contains("--verify"))
            return Verify(args);
        if (args.Contains("--dump-catalog"))
            return DumpCatalog(args);

        string? game = null;
        string? output = null;
        string? curated = null;
        var reports = Path.Combine("docs", "data");
        var strictItems = true;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game" when i + 1 < args.Length:
                    game = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--curated" when i + 1 < args.Length:
                    curated = args[++i];
                    break;
                case "--reports" when i + 1 < args.Length:
                    reports = args[++i];
                    break;
                case "--keep-nonexclusive-items":
                    strictItems = false;
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (game is null || output is null)
        {
            Console.Error.WriteLine("--game and --out are required.");
            PrintUsage();
            return 2;
        }

        if (!Directory.Exists(game))
        {
            Console.Error.WriteLine($"sqpack directory not found: {game}");
            return 2;
        }

        var clock = Stopwatch.StartNew();
        var generatedUtc = DateTime.UtcNow;

        Console.WriteLine($"game:    {game}");
        Console.WriteLine($"out:     {output}");
        Console.WriteLine($"curated: {curated ?? "(none)"}");
        Console.WriteLine($"reports: {reports}");
        Console.WriteLine($"items:   {(strictItems ? "strict exclusivity rule" : "legacy rule (--keep-nonexclusive-items)")}");

        var sheets = new GameSheets(game);
        Console.WriteLine($"version: {sheets.GameVersion}  (sheets opened in {clock.Elapsed.TotalSeconds:F1} s)");

        var generator = new UniqueRewardGenerator(sheets) { StrictItemExclusivity = strictItems };
        generator.Run();
        Console.WriteLine($"static:  {generator.Entries.Count} entries in {clock.Elapsed.TotalSeconds:F1} s ({generator.Dropped.Count} item rewards dropped as non-exclusive or unnamed)");

        CuratedOverlay.Apply(curated, sheets, generator, Console.Out);

        var entries = generator.Entries.ToList();
        OutputFile.Write(output, sheets.GameVersion, generatedUtc, entries);
        Console.WriteLine($"wrote:   {output} ({entries.Count} entries)");

        if (curated is not null && Directory.Exists(curated))
        {
            // feature_quests.json is derived, not maintained: the runtime rule over the mapped catalog, the other
            // curated files and the entries just written. CuratedInvariantsTests compares the shipped file to this.
            var curatedData = CuratedData.Load(curated);
            foreach (var warning in curatedData.Warnings)
                Console.WriteLine($"curated: {warning}");
            var bundle = CatalogMapper.Map(sheets.Data.Excel, Language.English);
            var featureIds = FeaturePresets.Derive(bundle.Catalog, curatedData.WithoutFeatureQuests(), entries);
            var featurePath = Path.Combine(curated, CuratedData.FeatureQuestsFileName);
            FeatureQuestsFile.Write(featurePath, featureIds);
            Console.WriteLine($"wrote:   {featurePath} ({featureIds.Count} feature quests, derived)");
        }

        Directory.CreateDirectory(reports);
        var uniqueReportPath = Path.Combine(reports, "unique-report.md");
        var catalogStatsPath = Path.Combine(reports, "catalog-stats.md");
        File.WriteAllText(uniqueReportPath, Reports.UniqueReport(sheets, generator, entries, generatedUtc));
        File.WriteAllText(catalogStatsPath, Reports.CatalogStats(sheets, generatedUtc));
        Console.WriteLine($"wrote:   {uniqueReportPath}");
        Console.WriteLine($"wrote:   {catalogStatsPath}");

        foreach (var group in entries.GroupBy(e => e.Kind).OrderBy(k => k.Key))
            Console.WriteLine($"  {group.Key,-16} {group.Count(),5}");
        Console.WriteLine("otherSources by value:");
        foreach (var group in entries.SelectMany(e => e.OtherSources).GroupBy(s => s).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {group.Key,-16} {group.Count(),5}");

        var failures = SanityChecks(entries);
        clock.Stop();
        Console.WriteLine($"done in {clock.Elapsed.TotalSeconds:F1} s");

        if (failures.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"SANITY CHECK FAILED ({failures.Count}):");
            foreach (var f in failures)
                Console.Error.WriteLine($"  - {f}");
            return 1;
        }

        Console.WriteLine("sanity checks passed");
        return 0;
    }

    private static int Verify(string[] args)
    {
        string? game = null;
        var data = DefaultData;
        var report = DefaultVerifyReport;
        var xivapi = true;
        var sample = 48;
        var seed = 20260927;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--verify":
                    break;
                case "--game" when i + 1 < args.Length:
                    game = args[++i];
                    break;
                case "--data" when i + 1 < args.Length:
                    data = args[++i];
                    break;
                case "--report" when i + 1 < args.Length:
                    report = args[++i];
                    break;
                case "--no-xivapi":
                    xivapi = false;
                    break;
                case "--sample" when i + 1 < args.Length && int.TryParse(args[i + 1], out var n):
                    sample = n;
                    i++;
                    break;
                case "--seed" when i + 1 < args.Length && int.TryParse(args[i + 1], out var s):
                    seed = s;
                    i++;
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (game is null || !Directory.Exists(game))
        {
            Console.Error.WriteLine("--verify needs --game pointing at an existing sqpack directory.");
            PrintUsage();
            return 2;
        }
        if (!File.Exists(data))
        {
            Console.Error.WriteLine($"data file not found: {data}");
            return 2;
        }

        var clock = Stopwatch.StartNew();
        Console.WriteLine($"game:    {game}");
        Console.WriteLine($"xivapi:  {(xivapi ? $"sample {sample}, seed {seed}" : "off")}");
        var notes = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)) ?? string.Empty, "verification-notes.md");
        var code = Verifier.Run(new VerifyOptions(game, data, report, File.Exists(notes) ? notes : null, xivapi, sample, seed));
        Console.WriteLine($"done in {clock.Elapsed.TotalSeconds:F1} s");
        return code;
    }

    /// <summary>
    /// Freezes the mapped catalog to a gzipped JSON fixture the tests read in place of the game files. The path is the
    /// file to write, or a directory that receives <c>catalog-&lt;gameVersion&gt;.json.gz</c>.
    /// </summary>
    private static int DumpCatalog(string[] args)
    {
        string? game = null;
        string? target = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game" when i + 1 < args.Length:
                    game = args[++i];
                    break;
                case "--dump-catalog" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (game is null || !Directory.Exists(game))
        {
            Console.Error.WriteLine("--dump-catalog needs --game pointing at an existing sqpack directory.");
            PrintUsage();
            return 2;
        }
        if (target is null)
        {
            Console.Error.WriteLine("--dump-catalog needs a file or directory path.");
            PrintUsage();
            return 2;
        }

        var clock = Stopwatch.StartNew();
        var gameVersion = GameSheets.ReadGameVersion(game);
        var path = Directory.Exists(target) || target.EndsWith(Path.DirectorySeparatorChar) || target.EndsWith(Path.AltDirectorySeparatorChar)
            ? Path.Combine(target, CatalogFixtureFile.FileName(gameVersion))
            : target;

        Console.WriteLine($"game:    {game}");
        Console.WriteLine($"version: {gameVersion}");
        Console.WriteLine($"out:     {path}");

        using var data = new Lumina.GameData(game, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.English,
            PanicOnSheetChecksumMismatch = false,
        });
        var bundle = CatalogMapper.Map(data.Excel, Lumina.Data.Language.English, log: line => Console.WriteLine($"  {line}"));
        CatalogFixtureFile.Write(path, bundle, gameVersion);

        var bytes = new FileInfo(path).Length;
        Console.WriteLine($"wrote:   {path} ({bundle.Catalog.Count} quests, {bytes / 1024.0 / 1024.0:F2} MB gzipped) in {clock.Elapsed.TotalSeconds:F1} s");
        return 0;
    }

    /// <summary>Known facts the output must reproduce. A failure means the sheet layout or a rule regressed.</summary>
    private static List<string> SanityChecks(IReadOnlyList<UniqueRewardEntry> entries)
    {
        var failures = new List<string>();

        void Require(bool ok, string message)
        {
            if (!ok) failures.Add(message);
        }

        Require(entries.Any(e => e is { QuestRowId: 66038, Kind: RewardKind.Emote, RewardId: 114 }),
            "66038 Her Last Vow should yield Emote 114");
        Require(entries.Any(e => e is { QuestRowId: 66038, Kind: RewardKind.Minion }),
            "66038 Her Last Vow should yield a Minion");
        Require(entries.Any(e => e is { QuestRowId: 70058, Kind: RewardKind.Mount, RewardId: 6, ItemId: 6008 }),
            "70058 The Ultimate Weapon should yield Mount 6 (Magitek Armor via item 6008)");
        Require(entries.All(e => !string.IsNullOrWhiteSpace(e.RewardName)),
            "every entry should carry a reward name");
        Require(entries.All(e => e.Kind == RewardKind.SystemUnlock || e.RewardId != 0),
            "every entry except SystemUnlock should carry a reward id");
        Require(entries.Any(e => e is { QuestRowId: 69254, Kind: RewardKind.Orchestrion, RewardId: 350, ItemId: 28894 }),
            "69254 On the Threshold should yield Orchestrion 350 (Significance (Nothing), via item 28894 AdditionalData)");
        Require(entries.Any(e => e is { QuestRowId: 70012, Kind: RewardKind.DutyUnlock, RewardId: 808 }),
            "70012 Where Familiars Dare should yield DutyUnlock 808 (Asphodelos: The First Circle, curated)");
        Require(!entries.Any(e => e is { QuestRowId: 70011, Kind: RewardKind.DutyUnlock, RewardId: 808 }),
            "70011 The Crystal from Beyond must not yield DutyUnlock 808 (it only starts the chain; verification-report-2 row 5)");
        Require(entries.Any(e => e is { QuestRowId: 68546, Kind: RewardKind.Mount, RewardId: 99 } && e.SoldOnOnlineStore),
            "68546 Starlight Stakeout's Mount 99 should carry OnlineStore from curated/online_store.json");
        Require(entries.All(e => e.Source.Contains(";otherSource=", StringComparison.Ordinal) == e.OtherSources.Any(s => s != OtherSource.OnlineStore)),
            "source text ;otherSource= and the structured otherSources must agree (store aside)");

        var mounts = entries.Count(e => e.Kind == RewardKind.Mount);
        var minions = entries.Count(e => e.Kind == RewardKind.Minion);
        var currents = entries.Count(e => e.Kind == RewardKind.AetherCurrent);
        Require(mounts >= 30, $"expected at least 30 Mount entries, got {mounts}");
        Require(minions >= 50, $"expected at least 50 Minion entries, got {minions}");
        Require(currents >= 140, $"expected at least 140 AetherCurrent entries, got {currents}");

        return failures;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Tsukimichi.DataGen --game <sqpack path> --out <unique_quests.json> [--curated <curated dir>] [--reports <docs/data dir>] [--keep-nonexclusive-items]");
        Console.WriteLine($"       Tsukimichi.DataGen --verify --game <sqpack path> [--data <unique_quests.json>] [--report <report.md>] [--no-xivapi] [--sample N] [--seed N]");
        Console.WriteLine($"       defaults: --data {DefaultData} --report {DefaultVerifyReport} --sample 48 --seed 20260927");
        Console.WriteLine("       a verification-notes.md next to the report is inserted after the header (hand-written findings).");
        Console.WriteLine("       Tsukimichi.DataGen --dump-catalog <file.json.gz or directory> --game <sqpack path>");
        Console.WriteLine("       freezes the mapped catalog for the tests (Tsukimichi.Tests/Fixtures/catalog-<gameVersion>.json.gz).");
    }
}
