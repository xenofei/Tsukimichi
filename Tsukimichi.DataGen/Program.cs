using System.Diagnostics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.DataGen;

/// <summary>
/// Offline generator: local game files -> unique_quests.json plus the review reports under docs/data.
/// Usage: Tsukimichi.DataGen --game "<sqpack path>" --out <unique_quests.json> [--curated <dir>] [--reports <dir>]
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? game = null;
        string? output = null;
        string? curated = null;
        var reports = Path.Combine("docs", "data");

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

        var sheets = new GameSheets(game);
        Console.WriteLine($"version: {sheets.GameVersion}  (sheets opened in {clock.Elapsed.TotalSeconds:F1} s)");

        var generator = new UniqueRewardGenerator(sheets);
        generator.Run();
        Console.WriteLine($"static:  {generator.Entries.Count} entries in {clock.Elapsed.TotalSeconds:F1} s");

        CuratedOverlay.Apply(curated, sheets, generator, Console.Out);

        var entries = generator.Entries.ToList();
        OutputFile.Write(output, sheets.GameVersion, generatedUtc, entries);
        Console.WriteLine($"wrote:   {output} ({entries.Count} entries)");

        Directory.CreateDirectory(reports);
        var uniqueReportPath = Path.Combine(reports, "unique-report.md");
        var catalogStatsPath = Path.Combine(reports, "catalog-stats.md");
        File.WriteAllText(uniqueReportPath, Reports.UniqueReport(sheets, generator, entries, generatedUtc));
        File.WriteAllText(catalogStatsPath, Reports.CatalogStats(sheets, generatedUtc));
        Console.WriteLine($"wrote:   {uniqueReportPath}");
        Console.WriteLine($"wrote:   {catalogStatsPath}");

        foreach (var group in entries.GroupBy(e => e.Kind).OrderBy(k => k.Key))
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
        Console.WriteLine("Usage: Tsukimichi.DataGen --game <sqpack path> --out <unique_quests.json> [--curated <curated dir>] [--reports <docs/data dir>]");
    }
}
