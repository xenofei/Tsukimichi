using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Verify.Game;
using Tsukimichi.Verify.Net;
using Tsukimichi.Verify.Output;
using Tsukimichi.Verify.Sources;
using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify;

/// <summary>
/// Tsukimichi.Verify — full-catalog verification against external sources (feature plan v3, T2a).
/// <code>
/// Tsukimichi.Verify quests  [--game &lt;sqpack&gt;] [--cache &lt;dir&gt;] [--offline] [--since &lt;csv&gt;] [--rate &lt;seconds&gt;] [--limit N] [--out &lt;dir&gt;]
/// Tsukimichi.Verify rewards [same options]
/// Tsukimichi.Verify summary [--out &lt;dir&gt;]       exits 1 when any row is unresolved or catalogWrong outside the allowlist
/// Tsukimichi.Verify patches [--game, --cache, --offline, --rate, --limit N, --out] [--patches &lt;file&gt;] [--patch-corrections &lt;file&gt;] [--no-quest-documents]
/// </code>
/// </summary>
public static class Program
{
    private const string DefaultGame = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            Usage();
            return args.Length == 0 ? 2 : 0;
        }

        var command = args[0];
        var opts = new VerifyOptions();
        try
        {
            opts = VerifyOptions.Parse(args.Skip(1).ToArray());
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Usage();
            return 2;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.Error.WriteLine("cancelling; the cache keeps everything fetched so far");
        };

        try
        {
            return command switch
            {
                "quests" => await QuestsAsync(opts, cts.Token),
                "rewards" => await RewardsAsync(opts, cts.Token),
                "summary" => Summary(opts),
                "patches" => await PatchesAsync(opts, cts.Token),
                _ => Unknown(command),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return 130;
        }
        catch (HostBlockedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("rerun later; cached fetches are reused and the run resumes where it stopped");
            return 3;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"unknown command '{command}'");
        Usage();
        return 2;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage: Tsukimichi.Verify <quests|rewards|summary|patches> [options]");
        Console.Error.WriteLine("  --game <sqpack>    game sqpack directory (default: the Steam install)");
        Console.Error.WriteLine("  --cache <dir>      fetch cache (default: %LOCALAPPDATA%\\Tsukimichi.Verify\\<gameVersion>); never inside the repo");
        Console.Error.WriteLine("  --offline          never fetch; a cache miss is an unresolved row");
        Console.Error.WriteLine("  --since <csv>      print verdict changes against a previous quest-verification.csv");
        Console.Error.WriteLine("  --rate <seconds>   minimum seconds between requests per host (default 2.0)");
        Console.Error.WriteLine("  --limit N          verify only the first N quests / reward entries (smoke runs)");
        Console.Error.WriteLine("  --out <dir>        output directory (default: <repo>/docs/data)");
        Console.Error.WriteLine("  --data <file>      unique_quests.json (default: <repo>/Tsukimichi/Data/unique_quests.json)");
        Console.Error.WriteLine("  --curated <dir>    curated directory (default: <repo>/Tsukimichi/Data/curated)");
        Console.Error.WriteLine("  patches only:");
        Console.Error.WriteLine("  --patches <file>   quest_patches.json to write (default: <repo>/Tsukimichi/Data/quest_patches.json); its values fill what Garland cannot");
        Console.Error.WriteLine("  --patch-corrections <file>  hand corrections laid over Garland's values (default: <repo>/docs/data/quest-patch-corrections.json)");
        Console.Error.WriteLine("  --no-quest-documents  read Garland's per-quest documents from the cache only; never fetch one");
        Console.Error.WriteLine("  (with patches, --limit N caps the per-quest fetches of one run; the rest wait for the next)");
    }

    private static string ToolVersion => Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    private static string UserAgent => $"Tsukimichi.Verify/{ToolVersion} (+https://github.com/xenofei/Tsukimichi)";

    private sealed record Context(GameCatalog Game, PoliteHttp Http, LodestoneSource Lodestone, WikiSource Wiki, CollectSource Collect, GarlandSource Garland, IReadOnlyDictionary<uint, List<uint>> CuratedDutyUnlocks, IReadOnlyDictionary<uint, string> CuratedSystemUnlocks, IReadOnlyList<UniqueRewardEntry> UniqueEntries, string OutDir, Stopwatch Clock) : IDisposable
    {
        public void Dispose() => Http.Dispose();
    }

    private static Context Open(VerifyOptions opts)
    {
        var clock = Stopwatch.StartNew();
        var game = opts.Game ?? DefaultGame;
        if (!Directory.Exists(game))
        {
            throw new ArgumentException($"sqpack directory not found: {game} (pass --game)");
        }

        var log = Console.Out;
        var curatedData = Tsukimichi.Core.Storage.CuratedData.Load(opts.CuratedDir);
        foreach (var warning in curatedData.Warnings)
        {
            log.WriteLine("curated: " + warning);
        }

        var catalog = new GameCatalog(game, curatedData, log);
        var cacheRoot = opts.Cache ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tsukimichi.Verify", catalog.GameVersion);
        var repo = opts.RepoRoot;
        if (Path.GetFullPath(cacheRoot).StartsWith(Path.GetFullPath(repo), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"the cache must live outside the repository: {cacheRoot}");
        }

        log.WriteLine($"game:    {game} (version {catalog.GameVersion}, {catalog.Catalog.Count} named quests, opened in {clock.Elapsed.TotalSeconds:F1} s)");
        log.WriteLine($"cache:   {cacheRoot}{(opts.Offline ? " (offline)" : string.Empty)}");
        log.WriteLine($"rate:    {opts.RateSeconds:F1} s per host; UA {UserAgent}");
        log.WriteLine($"out:     {opts.OutDir}");

        var http = new PoliteHttp(cacheRoot, UserAgent, opts.RateSeconds, opts.Offline, log);
        var curated = LoadCuratedDutyUnlocks(Path.Combine(opts.CuratedDir, "duty_unlocks.json"));
        var systems = LoadCuratedSystemUnlocks(Path.Combine(opts.CuratedDir, "system_unlocks.json"));
        var entries = LoadUniqueEntries(opts.DataFile);
        log.WriteLine($"data:    {entries.Count} unique-reward entries, {curated.Count} curated duty unlocks, {systems.Count} curated system unlocks");
        return new Context(catalog, http, new LodestoneSource(http, log), new WikiSource(http, log), new CollectSource(http, log), new GarlandSource(http), curated, systems, entries, opts.OutDir, clock);
    }

    private static async Task<int> QuestsAsync(VerifyOptions opts, CancellationToken ct)
    {
        using var c = Open(opts);
        var log = Console.Out;
        var selection = c.Game.Catalog.All.OrderBy(q => q.RowId).ToList();
        if (opts.Limit > 0)
        {
            selection = SmokeSelection(selection, opts.Limit, c.CuratedDutyUnlocks);
        }

        log.WriteLine($"quests:  verifying {selection.Count} of {c.Game.Catalog.Count}");
        var verifier = new QuestVerifier(c.Game, c.Lodestone, c.Wiki, c.Garland, c.CuratedDutyUnlocks, c.CuratedSystemUnlocks, c.UniqueEntries, log);
        var questCsv = Path.Combine(c.OutDir, "quest-verification.csv");
        var summaryCsv = Path.Combine(c.OutDir, "quest-verification-summary.csv");
        List<QuestRow> rows;
        var completed = false;
        try
        {
            rows = await verifier.RunAsync(selection, (partial, done) =>
            {
                Csv.WriteQuestRows(questCsv, partial);
                Csv.WriteSummary(summaryCsv, partial);
            }, ct);
            completed = true;
        }
        finally
        {
            c.Http.WriteManifest(Path.Combine(c.OutDir, "verification-manifest.json"), c.Game.GameVersion);
            if (!completed)
            {
                log.WriteLine("quests: interrupted; partial CSVs and the manifest are on disk, rerun to resume");
            }
        }

        Csv.WriteQuestRows(questCsv, rows);
        Csv.WriteSummary(summaryCsv, rows);
        WriteFestivalSeed(Path.Combine(c.OutDir, "festival-end-dates.json"), verifier.Festivals, c.Game.GameVersion);
        var allowlistPath = Path.Combine(c.OutDir, "verification-allowlist.json");
        if (!File.Exists(allowlistPath))
        {
            Allowlist.WriteEmpty(allowlistPath);
        }

        if (opts.Since is not null)
        {
            SinceDiff.Print(opts.Since, rows, log);
        }

        var rewardCsv = Path.Combine(c.OutDir, "reward-verification.csv");
        var rewards = File.Exists(rewardCsv) ? Csv.ReadRewardRows(rewardCsv) : [];
        WriteReport(c, opts, "quests", rows, rewards, allowlistPath, log, [
            $"Lodestone: {verifier.LodestoneListed} ids enumerated from the category listings, {verifier.LodestonePagesFetched} quest pages parsed; wiki: {verifier.WikiPagesFound} quest pages matched, {verifier.NameAliasesResolved} prerequisite spellings resolved through the wiki's id-gt",
        ]);
        PrintTotals(rows.Select(r => r.Verdict), "quest rows", log);
        log.WriteLine($"done in {PoliteHttp.Elapsed(c.Clock)}; {c.Http.LiveRequests} live requests, {c.Http.CacheHits} cache hits, {c.Http.RobotsRefusals} robots refusals");
        return 0;
    }

    private static async Task<int> RewardsAsync(VerifyOptions opts, CancellationToken ct)
    {
        using var c = Open(opts);
        var log = Console.Out;
        var entries = c.UniqueEntries.OrderBy(e => e.QuestRowId).ThenBy(e => e.Kind).ThenBy(e => e.RewardId).ToList();
        if (opts.Limit > 0)
        {
            // A smoke run wants every kind represented, not the first N rows of one kind.
            var kinds = entries.Select(e => e.Kind).Distinct().Count();
            entries = entries.GroupBy(e => e.Kind).SelectMany(g => g.Take(Math.Max(2, opts.Limit / Math.Max(1, kinds)))).OrderBy(e => e.QuestRowId).ToList();
        }

        log.WriteLine($"rewards: verifying {entries.Count} of {c.UniqueEntries.Count} entries");
        var verifier = new RewardVerifier(c.Game, c.Collect, c.Wiki, c.Lodestone, c.Garland, c.CuratedDutyUnlocks, log);
        var rewardCsv = Path.Combine(c.OutDir, "reward-verification.csv");
        List<RewardRow> rows;
        try
        {
            rows = await verifier.RunAsync(entries, ct);
        }
        finally
        {
            c.Http.WriteManifest(Path.Combine(c.OutDir, "verification-manifest.json"), c.Game.GameVersion);
        }

        Csv.WriteRewardRows(rewardCsv, rows);
        var allowlistPath = Path.Combine(c.OutDir, "verification-allowlist.json");
        if (!File.Exists(allowlistPath))
        {
            Allowlist.WriteEmpty(allowlistPath);
        }

        var questCsv = Path.Combine(c.OutDir, "quest-verification.csv");
        var quests = File.Exists(questCsv) ? Csv.ReadQuestRows(questCsv) : [];
        WriteReport(c, opts, "rewards", quests, rows, allowlistPath, log, [], verifier.SystemRewardCoverage(c.UniqueEntries).ToList());
        PrintTotals(rows.Select(r => r.Verdict), "reward rows", log);
        log.WriteLine($"done in {PoliteHttp.Elapsed(c.Clock)}; {c.Http.LiveRequests} live requests, {c.Http.CacheHits} cache hits");
        return 0;
    }

    /// <summary>
    /// P8 seed: Garland's patch data for every named quest into <c>quest_patches.json</c>, with
    /// <c>quest-patches-report.md</c> beside the other reports. Resumable: everything fetched is cached, and a rerun
    /// fetches only what is missing.
    /// </summary>
    private static async Task<int> PatchesAsync(VerifyOptions opts, CancellationToken ct)
    {
        using var c = Open(opts);
        var log = Console.Out;
        var previous = File.Exists(opts.PatchesFile) ? QuestPatches.Load(opts.PatchesFile) : QuestPatches.Empty;
        foreach (var warning in previous.Warnings)
        {
            log.WriteLine("patches: " + warning);
        }

        log.WriteLine($"patches: previous file {(previous.ByRowId.Count == 0 ? "none" : $"{previous.KnownCount} known of {previous.ByRowId.Count} listed (game {previous.GameVersion})")}");
        var corrections = QuestPatchCorrections.Load(opts.PatchCorrectionsFile);
        foreach (var warning in corrections.Warnings)
        {
            log.WriteLine("patches: " + warning);
        }

        log.WriteLine($"patches: {corrections.ByRowId.Count} hand corrections from {opts.PatchCorrectionsFile}");
        var seeder = new PatchSeeder(c.Game, new GarlandPatchSource(c.Http, log), previous, corrections, log);
        // verification-manifest.json is left alone: it audits the quests and rewards runs, and the report names every
        // Garland document this one read.
        var result = await seeder.RunAsync(!opts.Offline && opts.FetchQuestDocuments, opts.Limit, ct);
        result.Write(opts.PatchesFile);
        var reportPath = Path.Combine(c.OutDir, "quest-patches-report.md");
        seeder.WriteReport(reportPath, result, DateTime.UtcNow.ToString("yyyy-MM-dd"), UserAgent, opts.RateSeconds, opts.Offline);
        var known = seeder.Rows.Count(r => r.Patch.Length > 0);
        log.WriteLine($"wrote:   {opts.PatchesFile} ({known} of {seeder.Rows.Count} named quests with a patch, newest {result.Newest})");
        log.WriteLine($"wrote:   {reportPath} ({seeder.Findings.Count} findings)");
        if (seeder.QuestDocumentsPending > 0)
        {
            log.WriteLine($"patches: {seeder.QuestDocumentsPending} quest documents still to fetch; rerun to resume");
        }

        log.WriteLine($"done in {PoliteHttp.Elapsed(c.Clock)}; {c.Http.LiveRequests} live requests, {c.Http.CacheHits} cache hits");
        return 0;
    }

    private static int Summary(VerifyOptions opts)
    {
        var log = Console.Out;
        var questCsv = Path.Combine(opts.OutDir, "quest-verification.csv");
        var rewardCsv = Path.Combine(opts.OutDir, "reward-verification.csv");
        var allowlist = Allowlist.Load(Path.Combine(opts.OutDir, "verification-allowlist.json"));
        var current = PluginVersion(opts.RepoRoot);
        var quests = File.Exists(questCsv) ? Csv.ReadQuestRows(questCsv) : [];
        var rewards = File.Exists(rewardCsv) ? Csv.ReadRewardRows(rewardCsv) : [];
        log.WriteLine($"summary: {quests.Count} quest rows over {quests.Select(r => r.RowId).Distinct().Count()} quests; {rewards.Count} reward rows; plugin version {(current?.ToString() ?? "unknown")}; allowlist {allowlist.Entries.Count} entries");
        PrintTotals(quests.Select(r => r.Verdict), "quest rows", log);
        PrintTotals(rewards.Select(r => r.Verdict), "reward rows", log);

        var open = 0;
        foreach (var r in quests.Where(r => Verdicts.FailsGate(r.Verdict)))
        {
            if (allowlist.Covering(r, current) is null)
            {
                open++;
                log.WriteLine($"  GATE {Verdicts.Name(r.Verdict)} {r.RowId} {r.Name} [{r.Fact}/{r.Source}] catalog={r.CatalogValue} source={r.SourceValue}: {r.Reason} {r.SourceRef}");
            }
        }

        foreach (var r in rewards.Where(r => Verdicts.FailsGate(r.Verdict)))
        {
            if (allowlist.Covering(r, current) is null)
            {
                open++;
                log.WriteLine($"  GATE {Verdicts.Name(r.Verdict)} {r.QuestRowId} {r.QuestName} [{r.Kind} {r.RewardName}/{r.Source}] {r.Reason} {r.SourceRef}");
            }
        }

        foreach (var e in allowlist.Entries.Where(e => Allowlist.Expired(e, current)))
        {
            log.WriteLine($"  allowlist entry expired: {e.RowId} {e.Fact} until {e.Until}");
        }

        log.WriteLine(open == 0 ? "summary: gate passed" : $"summary: gate FAILED, {open} row(s) unresolved or catalogWrong outside the allowlist");
        return open == 0 ? 0 : 1;
    }

    private static void WriteReport(Context c, VerifyOptions opts, string command, List<QuestRow> quests, List<RewardRow> rewards, string allowlistPath, TextWriter log, List<string> notes, List<(uint RowId, string Name, uint SystemReward, bool Covered)>? coverage = null)
    {
        var allowlist = Allowlist.Load(allowlistPath);
        var current = PluginVersion(opts.RepoRoot);
        coverage ??= new RewardVerifier(c.Game, c.Collect, c.Wiki, c.Lodestone, c.Garland, c.CuratedDutyUnlocks, log).SystemRewardCoverage(c.UniqueEntries).ToList();
        var run = new RunInfo(command, c.Game.GameVersion, DateTime.UtcNow.ToString("yyyy-MM-dd"), UserAgent, opts.RateSeconds, opts.Offline, opts.Limit, c.Http.LiveRequests, c.Http.CacheHits, PoliteHttp.Elapsed(c.Clock), c.Http.BlockedHosts, notes);
        FullReport.Write(Path.Combine(c.OutDir, "verification-full.md"), quests, rewards, allowlist, current, run, c.Game.Catalog.Count, coverage);
    }

    /// <summary>A smoke selection touches every section and the special cases: offsets, any-joins, duties, curated unlocks, seasonal, unlisted.</summary>
    private static List<QuestRecord> SmokeSelection(List<QuestRecord> all, int limit, IReadOnlyDictionary<uint, List<uint>> curated)
    {
        var picked = new List<QuestRecord>();
        var seen = new HashSet<uint>();
        void Add(IEnumerable<QuestRecord> source, int count)
        {
            foreach (var q in source)
            {
                if (picked.Count >= limit)
                {
                    return;
                }

                if (seen.Add(q.RowId))
                {
                    picked.Add(q);
                    if (--count <= 0)
                    {
                        return;
                    }
                }
            }
        }

        var perSection = Math.Max(1, limit / 12);
        foreach (var section in all.Where(q => !q.IsUnlisted).GroupBy(q => q.Journal.SectionId).OrderBy(g => g.Key))
        {
            Add(section, perSection);
        }

        Add(all.Where(q => q.LevelOffset > 0), 3);
        Add(all.Where(q => q.PreviousQuests.Join == JoinKind.Any), 3);
        Add(all.Where(q => q.InstanceContentRequired.Length > 1), 2);
        Add(all.Where(q => curated.ContainsKey(q.RowId)), 3);
        Add(all.Where(q => q.Festival != 0), 3);
        Add(all.Where(q => q.IsUnlisted), 2);
        Add(all.Where(q => q.ClassJobRequired != 0), 2);
        Add(all.Where(q => q.GrandCompany != 0), 2);
        Add(all, limit);
        return picked.OrderBy(q => q.RowId).ToList();
    }

    private static void PrintTotals(IEnumerable<Verdict> verdicts, string what, TextWriter log)
    {
        var list = verdicts.ToList();
        if (list.Count == 0)
        {
            return;
        }

        log.WriteLine($"{what}: {list.Count} = " + string.Join(", ", Enum.GetValues<Verdict>().Select(v => $"{Verdicts.Name(v)} {list.Count(x => x == v)}")));
    }

    private static void WriteFestivalSeed(string path, IReadOnlyDictionary<ushort, FestivalWindow> festivals, string gameVersion)
    {
        // Only windows with an official announcement are kept: P11 shows "announced to end <date> (Lodestone)" and must not quote a wiki-only date.
        var entries = new JsonObject();
        var rejected = new JsonObject();
        var seen = festivals.Count;
        foreach (var f in festivals.Values.Where(f => f.LodestoneUrl is not null && f.End.Length > 0).OrderBy(f => f.FestivalId))
        {
            // The dates come from the wiki page that cites the announcement; keep them only when they are consistent with it.
            var start = IsoDate(f.Start);
            var end = IsoDate(f.End);
            var urlYear = Regex.Match(f.LodestoneUrl!, @"/special/(\d{4})/") is { Success: true } ym ? ym.Groups[1].Value : null;
            var problem = start.Length == 0 || end.Length == 0 ? "a date did not parse"
                : string.CompareOrdinal(start, end) > 0 ? "start after end"
                : urlYear is not null && urlYear != start[..4] && urlYear != end[..4] ? $"the announcement is from {urlYear}, the dates are not"
                : null;
            if (problem is not null)
            {
                rejected[f.FestivalId.ToString()] = $"{f.EventName}: {problem} (wiki {f.WikiUrl})";
                continue;
            }

            var o = new JsonObject
            {
                ["name"] = f.EventName,
                ["start"] = start,
                ["end"] = end,
                ["startText"] = f.Start,
                ["endText"] = f.End,
                ["source"] = f.LodestoneUrl,
                ["sourceKind"] = "lodestone",
                ["wiki"] = f.WikiUrl,
                ["questRowIds"] = new JsonArray(f.QuestRowIds.Distinct().OrderBy(i => i).Select(i => (JsonNode)i).ToArray()),
            };
            entries[f.FestivalId.ToString()] = o;
        }

        var root = new JsonObject
        {
            ["$schema_note"] = "Sourced seasonal-event windows. Nothing reads this file yet: curated/festivals.json consumes these windows later (P11, feature plan v3 §3, 0.8.0), where the plugin says \"announced to end <date> (Lodestone)\". festivalId -> { name, start, end (ISO dates), startText, endText (as written), source (the official Lodestone announcement or special-site page), sourceKind (always lodestone), wiki (the page the dates and the announcement link were read from), questRowIds (Quest rows carrying this Festival id) }. Only festivals with an official Lodestone URL are listed; festivals the wiki dates without an announcement link are left out, and so are windows whose dates do not parse, run backwards or fall outside the announcement's year (listed under rejected for a human to fix on the wiki). Generated by Tsukimichi.Verify quests; facts only.",
            ["gameVersion"] = gameVersion,
            ["festivalIdsSeen"] = seen,
            ["entries"] = entries,
            ["rejected"] = rejected,
        };
        Csv.Atomic(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }

    /// <summary>"December 31st, 2017" / "31 December 2017" / "2017-12-31" → ISO date, else empty.</summary>
    private static string IsoDate(string text)
    {
        var t = Regex.Replace(Names.Clean(text).Replace("'''", string.Empty).Replace("''", string.Empty).Trim(), @"(\d)(st|nd|rd|th)\b", "$1");
        t = Regex.Replace(t, @"\s*\(.*?\)", string.Empty);
        t = Regex.Replace(t, @"\s+at\s+.*$", string.Empty);
        foreach (var format in new[] { "MMMM d, yyyy", "MMMM d yyyy", "d MMMM yyyy", "yyyy-MM-dd", "MMM d, yyyy", "d MMM yyyy" })
        {
            if (DateTime.TryParseExact(t, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
            {
                return d.ToString("yyyy-MM-dd");
            }
        }

        return DateTime.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var any) ? any.ToString("yyyy-MM-dd") : string.Empty;
    }

    private static IReadOnlyDictionary<uint, List<uint>> LoadCuratedDutyUnlocks(string path)
    {
        var result = new Dictionary<uint, List<uint>>();
        if (!File.Exists(path))
        {
            return result;
        }

        var root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })?.AsObject();
        if (root is null)
        {
            return result;
        }

        foreach (var (key, value) in root)
        {
            if (!uint.TryParse(key, out var questId) || value is not JsonObject o || o["contentFinderConditionIds"] is not JsonArray ids)
            {
                continue;
            }

            result[questId] = ids.Select(n => n is JsonValue v && v.TryGetValue<uint>(out var id) ? id : 0u).Where(id => id != 0).ToList();
        }

        return result;
    }

    /// <summary>curated/system_unlocks.json: quest row id → label of the system the quest unlocks.</summary>
    private static IReadOnlyDictionary<uint, string> LoadCuratedSystemUnlocks(string path)
    {
        var result = new Dictionary<uint, string>();
        if (!File.Exists(path))
        {
            return result;
        }

        var root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })?.AsObject();
        foreach (var (key, value) in root ?? [])
        {
            if (uint.TryParse(key, out var questId) && value is JsonObject o)
            {
                result[questId] = o["label"]?.GetValue<string>() ?? "system";
            }
        }

        return result;
    }

    private static IReadOnlyList<UniqueRewardEntry> LoadUniqueEntries(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        var list = new List<UniqueRewardEntry>();
        foreach (var e in (root?["entries"] as JsonArray ?? []).OfType<JsonObject>())
        {
            list.Add(new UniqueRewardEntry(
                e["questRowId"]?.GetValue<uint>() ?? 0,
                Enum.TryParse<RewardKind>(e["kind"]?.GetValue<string>(), true, out var kind) ? kind : RewardKind.Other,
                e["rewardId"]?.GetValue<uint>() ?? 0,
                e["itemId"]?.GetValue<uint>() ?? 0,
                e["rewardName"]?.GetValue<string>() ?? string.Empty,
                Enum.TryParse<Confidence>(e["confidence"]?.GetValue<string>(), true, out var conf) ? conf : Confidence.Static,
                e["source"]?.GetValue<string>() ?? string.Empty)
            {
                OtherSources = (e["otherSources"] as JsonArray ?? []).Select(n => n?.GetValue<string>() ?? string.Empty).Where(n => n.Length > 0).ToList(),
            });
        }

        return list;
    }

    private static Version? PluginVersion(string repoRoot)
    {
        var csproj = Path.Combine(repoRoot, "Tsukimichi", "Tsukimichi.csproj");
        if (!File.Exists(csproj))
        {
            return null;
        }

        var m = Regex.Match(File.ReadAllText(csproj), @"<Version>([\d.]+)</Version>");
        return m.Success && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;
    }
}

/// <summary>Parsed command line.</summary>
internal sealed record VerifyOptions
{
    public string? Game { get; init; }
    public string? Cache { get; init; }
    public bool Offline { get; init; }
    public string? Since { get; init; }
    public double RateSeconds { get; init; } = 2.0;
    public int Limit { get; init; }
    public string RepoRoot { get; init; } = FindRepoRoot();
    public string OutDir { get; init; } = Path.Combine(FindRepoRoot(), "docs", "data");
    public string DataFile { get; init; } = Path.Combine(FindRepoRoot(), "Tsukimichi", "Data", "unique_quests.json");
    public string CuratedDir { get; init; } = Path.Combine(FindRepoRoot(), "Tsukimichi", "Data", "curated");
    public string PatchesFile { get; init; } = Path.Combine(FindRepoRoot(), "Tsukimichi", "Data", QuestPatches.FileName);
    public string PatchCorrectionsFile { get; init; } = Path.Combine(FindRepoRoot(), "docs", "data", QuestPatchCorrections.FileName);
    public bool FetchQuestDocuments { get; init; } = true;

    public static VerifyOptions Parse(string[] args)
    {
        var o = new VerifyOptions();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--game":
                    o = o with { Game = Next() };
                    break;
                case "--cache":
                    o = o with { Cache = Next() };
                    break;
                case "--offline":
                    o = o with { Offline = true };
                    break;
                case "--since":
                    o = o with { Since = Next() };
                    break;
                case "--rate":
                    o = o with { RateSeconds = double.TryParse(Next(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) && r >= 0 ? r : throw new ArgumentException("--rate needs a non-negative number of seconds") };
                    break;
                case "--limit":
                    o = o with { Limit = int.TryParse(Next(), out var n) && n > 0 ? n : throw new ArgumentException("--limit needs a positive integer") };
                    break;
                case "--out":
                    o = o with { OutDir = Path.GetFullPath(Next()) };
                    break;
                case "--data":
                    o = o with { DataFile = Path.GetFullPath(Next()) };
                    break;
                case "--curated":
                    o = o with { CuratedDir = Path.GetFullPath(Next()) };
                    break;
                case "--patches":
                    o = o with { PatchesFile = Path.GetFullPath(Next()) };
                    break;
                case "--patch-corrections":
                    o = o with { PatchCorrectionsFile = Path.GetFullPath(Next()) };
                    break;
                case "--no-quest-documents":
                    o = o with { FetchQuestDocuments = false };
                    break;
                default:
                    throw new ArgumentException($"unknown option {args[i]}");
            }
        }

        return o;
    }

    /// <summary>The directory holding Tsukimichi.sln, searched upward from the working directory, else the working directory.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Tsukimichi.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
