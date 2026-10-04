using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

/// <summary>
/// <c>--portrait-pack &lt;out dir&gt;</c> (feature plan v7 F4, decision 8): builds the optional portrait pack the plugin
/// offers as a download from Tsukimichi's GitHub release. For every named quest giver in the install it fetches the
/// Garland Tools NPC page (<c>/db/doc/npc/en/2/&lt;id&gt;.json</c>), and when that names a photo, the photo
/// (<c>/files/photos/npc/Enpc_&lt;id&gt;.png</c>, a full-body studio render on transparency, credit Celes), one request
/// a second, every answer cached on disk so a rerun fetches nothing it has. Each photo is head-cropped
/// (<see cref="PortraitHeadCrop"/>) to a <see cref="PortraitPackManifest.ImageSide"/> px square framed by the plugin's rule,
/// high in its bands (eye line 43 %, chin 83 %, spec-1.20 F4), quantised to a 256-colour PNG, and stored once however
/// many giver ids share it. A photo whose head box is under <see cref="MinBox"/> px is left out (a 72 px plate never
/// upscales), and each image's box goes in the manifest so the hover never shows a face larger than its source.
/// <para>
/// Writes <c>Tsukimichi-portraits.zip</c> (manifest and images; the same photos, install and .NET runtime make the same
/// bytes on any day: the manifest is stamped with the game version's date or <c>--built yyyy-MM-dd</c>, never the clock,
/// and <see cref="PortraitPackArchive.Write"/> pins everything machine-dependent in the zip), its
/// <c>.sha256</c>, <c>report.md</c> (coverage and every giver without a photo), and contact sheets under
/// <c>sheets/</c> for a spot check of the crops (<c>--skip &lt;ids&gt;</c> leaves out a giver whose crop missed). With <c>--offer &lt;file&gt; --tag &lt;vX.Y.Z&gt;</c> it also writes the
/// plugin's <c>portrait_pack.json</c>: the release, asset name, size and hash the plugin will accept. Nothing is uploaded.
/// </para>
/// </summary>
internal static partial class PortraitPackBuilder
{
    public const string AssetName = "Tsukimichi-portraits.zip";
    private const string DocUrl = "https://www.garlandtools.org/db/doc/npc/en/2/{0}.json";
    private const string PhotoUrl = "https://www.garlandtools.org/files/photos/npc/{0}";
    private const string Source = "Garland Tools NPC photos (garlandtools.org; photos credit: Celes)";

    /// <summary>The smallest head box kept, source px (spec-1.20 F4: the 72 px plate never upscales at 100 %).</summary>
    public const int MinBox = 72;

    public static int Run(string[] args)
    {
        string? game = null;
        string? outDir = null;
        string? cache = null;
        string? offerPath = null;
        string? tag = null;
        DateTime? built = null;
        var curatedDir = Path.Combine("Tsukimichi", "Data", "curated");
        var rate = 1.0;
        var limit = int.MaxValue;
        HashSet<uint>? only = null;
        var skip = new HashSet<uint>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--portrait-pack" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--game" when i + 1 < args.Length:
                    game = args[++i];
                    break;
                case "--cache" when i + 1 < args.Length:
                    cache = args[++i];
                    break;
                case "--curated" when i + 1 < args.Length:
                    curatedDir = args[++i];
                    break;
                case "--offer" when i + 1 < args.Length:
                    offerPath = args[++i];
                    break;
                case "--tag" when i + 1 < args.Length:
                    tag = args[++i];
                    break;
                case "--built" when i + 1 < args.Length && DateTime.TryParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var day):
                    built = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
                    i++;
                    break;
                case "--rate" when i + 1 < args.Length && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) && r >= 0.5:
                    rate = r;
                    i++;
                    break;
                case "--limit" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0:
                    limit = n;
                    i++;
                    break;
                case "--ids" or "--skip" when i + 1 < args.Length:
                    var into = args[i] == "--ids" ? only = [] : skip;
                    foreach (var part in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                        {
                            Console.Error.WriteLine($"{args[i - 1]}: '{part}' is not an NPC id.");
                            return 2;
                        }

                        into.Add(id);
                    }

                    break;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    return 2;
            }
        }

        if (game is null || !Directory.Exists(game) || outDir is null)
        {
            Console.Error.WriteLine("--portrait-pack needs an output directory and --game pointing at an existing sqpack directory.");
            return 2;
        }

        if ((offerPath is null) != (tag is null))
        {
            Console.Error.WriteLine("--offer and --tag go together: the offer names the release the zip is uploaded to.");
            return 2;
        }

        Directory.CreateDirectory(outDir);
        cache ??= Path.Combine(outDir, "cache");
        var gameVersion = GameSheets.ReadGameVersion(game);

        // The manifest's build date is an input, never the clock, so the same photos and install make the same zip.
        var builtUtc = built ?? PortraitPackManifest.BuiltDateOf(gameVersion);
        if (builtUtc == default)
        {
            Console.Error.WriteLine($"The game version '{gameVersion}' holds no date: give the build date with --built yyyy-MM-dd.");
            return 2;
        }
        using var data = new Lumina.GameData(game, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.English,
            PanicOnSheetChecksumMismatch = false,
        });

        var inputs = GiverPortraitSources.Read(data.Excel, icon => data.FileExists(RewardArtIndex.IconPath(icon)), line => Console.WriteLine($"  {line}"));
        var curated = CuratedData.Load(curatedDir);
        var gameIndex = PortraitIndex.Build(inputs, curated.GiverPortraits);

        // Named givers only (a generic "troubled adventurer" keeps its silhouette), the busiest first so a --limit
        // sample is the most useful one.
        var questsByGiver = inputs.Quests.GroupBy(q => q.GiverId).ToDictionary(g => g.Key, g => g.Count());
        var givers = inputs.Givers
            .Where(g => g.Name.Length > 0 && !PortraitNames.IsGeneric(g.Name) && (only is null || only.Contains(g.NpcId)) && !skip.Contains(g.NpcId))
            .DistinctBy(g => g.NpcId)
            .OrderByDescending(g => questsByGiver.GetValueOrDefault(g.NpcId))
            .ThenBy(g => g.NpcId)
            .Take(limit)
            .ToList();
        Console.WriteLine($"game:    {gameVersion}");
        Console.WriteLine($"givers:  {givers.Count} named (of {inputs.Givers.Select(g => g.NpcId).Distinct().Count()} giver ids)");
        Console.WriteLine($"cache:   {cache}");

        using var fetch = new PoliteFetch(cache, TimeSpan.FromSeconds(rate));
        var images = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var entries = new SortedDictionary<uint, string>();
        var missing = new List<(uint Id, string Name, string Why)>();
        var sheet = new List<(uint Id, string Name, byte[] Rgba)>();
        var boxes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var done = 0;
        foreach (var giver in givers)
        {
            done++;
            if (done % 50 == 0)
            {
                Console.WriteLine($"  {done}/{givers.Count}: {entries.Count} with a photo ({fetch.Live} fetched, {fetch.Cached} cached)");
            }

            var doc = fetch.Get(string.Format(CultureInfo.InvariantCulture, DocUrl, giver.NpcId), $"npc/{giver.NpcId}.json");
            if (doc is null)
            {
                missing.Add((giver.NpcId, giver.Name, "no Garland page"));
                continue;
            }

            string? photo = null;
            try
            {
                photo = (JsonNode.Parse(doc)?["npc"]?["photo"] as JsonValue)?.GetValue<string>();
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
            {
                photo = null;
            }

            var photoMatch = photo is null ? null : PhotoName().Match(photo);
            if (photoMatch is not { Success: true })
            {
                missing.Add((giver.NpcId, giver.Name, "no photo"));
                continue;
            }

            var fileName = photoMatch.Groups[1].Value + ".png";
            if (!images.ContainsKey(fileName))
            {
                var bytes = fetch.Get(string.Format(CultureInfo.InvariantCulture, PhotoUrl, photo), $"photos/{photo}");
                if (bytes is null || !PackPng.TryDecode(bytes, out var width, out var height, out var rgba, maxSide: 4096))
                {
                    missing.Add((giver.NpcId, giver.Name, bytes is null ? "photo missing" : "photo does not decode"));
                    continue;
                }

                var crop = PortraitHeadCrop.Crop(rgba, width, height, giver.Race, PortraitPackManifest.ImageSide, out var box);
                if (crop is null)
                {
                    missing.Add((giver.NpcId, giver.Name, "no face found in the photo (no figure, or the frame caught a weapon, hat or ears)"));
                    continue;
                }

                if (box < MinBox)
                {
                    missing.Add((giver.NpcId, giver.Name, $"head box under {MinBox} px"));
                    continue;
                }

                boxes[fileName] = box;

                images[fileName] = PortraitQuantizer.Encode(crop, PortraitPackManifest.ImageSide, PortraitPackManifest.ImageSide);
                sheet.Add((giver.NpcId, giver.Name, crop));
            }

            entries[giver.NpcId] = fileName;
        }

        if (images.Count == 0)
        {
            Console.Error.WriteLine("No photos: nothing to pack.");
            return 1;
        }

        var files = images.ToDictionary(kv => kv.Key, kv => Convert.ToHexStringLower(SHA256.HashData(kv.Value)), StringComparer.Ordinal);
        var manifest = PortraitPackManifest.Create(gameVersion, builtUtc, Source, files, entries, boxes: boxes);
        var zipPath = Path.Combine(outDir, AssetName);
        using (var zipFile = File.Create(zipPath))
        {
            PortraitPackArchive.Write(zipFile, manifest, images);
        }

        var zipBytes = File.ReadAllBytes(zipPath);
        var zipSha = Convert.ToHexStringLower(SHA256.HashData(zipBytes));
        File.WriteAllText(zipPath + ".sha256", $"{zipSha}  {AssetName}\n");

        // Check the zip exactly as the plugin will, before anyone uploads it.
        var check = Path.Combine(outDir, "check-" + Guid.NewGuid().ToString("N")[..8]);
        var verdict = PortraitPackArchive.Extract(zipPath, check, CancellationToken.None, out _, out var detail);
        Directory.Delete(check, recursive: true);
        if (verdict != PortraitPackFailure.None)
        {
            Console.Error.WriteLine($"The pack does not pass the plugin's checks: {verdict} {detail}");
            return 1;
        }

        WriteSheets(Path.Combine(outDir, "sheets"), sheet);
        var report = Report(gameVersion, builtUtc, inputs, gameIndex, givers, entries, images, missing, zipBytes.LongLength, zipSha);
        File.WriteAllText(Path.Combine(outDir, "report.md"), report);

        Console.WriteLine($"wrote:   {zipPath} ({PortraitPackOffer.SizeText(zipBytes.LongLength)}, {images.Count} images, {entries.Count} givers)");
        Console.WriteLine($"sha256:  {zipSha}");
        Console.WriteLine($"fetched: {fetch.Live} live, {fetch.Cached} from the cache");
        if (offerPath is not null && tag is not null)
        {
            var offer = new PortraitPackOffer(tag, AssetName, zipBytes.LongLength, zipSha, manifest.Format, gameVersion, entries.Count);
            if (PortraitPackOffer.Parse(offer.ToJson(), out var warning) is null)
            {
                Console.Error.WriteLine($"--tag {tag}: not a release tag the plugin accepts ({warning ?? "no tag"}).");
                return 2;
            }

            File.WriteAllBytes(offerPath, offer.ToJson());
            Console.WriteLine($"offer:   {offerPath} ({offer.DownloadUri})");
        }

        return 0;
    }

    private static string Report(string gameVersion, DateTime builtUtc, PortraitInputs inputs, PortraitIndex gameIndex, List<PortraitGiver> givers, SortedDictionary<uint, string> entries, SortedDictionary<string, byte[]> images, List<(uint Id, string Name, string Why)> missing, long zipSize, string zipSha)
    {
        var quests = inputs.Quests.Where(q => q.GiverId != 0).ToList();
        int game = 0, pack = 0, either = 0;
        foreach (var quest in quests)
        {
            var hasGame = gameIndex.For(quest.GiverId, quest.QuestId).HasArt;
            var hasPack = entries.ContainsKey(quest.GiverId);
            game += hasGame ? 1 : 0;
            pack += hasPack ? 1 : 0;
            either += hasGame || hasPack ? 1 : 0;
        }

        string Share(int n) => quests.Count == 0 ? "0" : (100d * n / quests.Count).ToString("0.0", CultureInfo.InvariantCulture);
        var text = new StringBuilder();
        text.AppendLine("# Portrait pack build report");
        text.AppendLine();
        text.AppendLine($"Game {gameVersion}. Built {builtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} by `Tsukimichi.DataGen --portrait-pack`. Never commit the zip or the sheets: they are Square Enix art (renders by Garland Tools, credit Celes).");
        text.AppendLine();
        text.AppendLine("| | count |");
        text.AppendLine("|---|---|");
        text.AppendLine($"| Named givers asked for | {givers.Count:N0} |");
        text.AppendLine($"| Givers with a photo | {entries.Count:N0} ({(givers.Count == 0 ? 0 : 100d * entries.Count / givers.Count):0.0}%) |");
        text.AppendLine($"| Distinct images | {images.Count:N0} |");
        text.AppendLine($"| Zip | {PortraitPackOffer.SizeText(zipSize)} ({zipSize:N0} bytes), SHA-256 `{zipSha}` |");
        text.AppendLine($"| Quests with a giver | {quests.Count:N0} |");
        text.AppendLine($"| … with a game-art face | {game:N0} ({Share(game)}%) |");
        text.AppendLine($"| … with a pack photo | {pack:N0} ({Share(pack)}%) |");
        text.AppendLine($"| … with either (game art + pack) | {either:N0} ({Share(either)}%) |");
        text.AppendLine();
        text.AppendLine("## Givers without a photo");
        text.AppendLine();
        foreach (var group in missing.GroupBy(m => m.Why).OrderByDescending(g => g.Count()))
        {
            text.AppendLine($"### {group.Key} ({group.Count()})");
            text.AppendLine();
            text.AppendLine(string.Join(", ", group.Select(m => $"{m.Name} ({m.Id})")));
            text.AppendLine();
        }

        return text.ToString();
    }

    /// <summary>The crops on night plates, 16 to a row, 160 to a page, labelled with the NPC id: for a spot check.</summary>
    private static void WriteSheets(string dir, List<(uint Id, string Name, byte[] Rgba)> cells)
    {
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "contact-*.png"))
        {
            File.Delete(old);
        }

        const int Cell = 104;
        const int Columns = 16;
        const int PerPage = 160;
        var side = PortraitPackManifest.ImageSide;
        for (var page = 0; page * PerPage < cells.Count; page++)
        {
            var slice = cells.Skip(page * PerPage).Take(PerPage).ToList();
            var rows = (slice.Count + Columns - 1) / Columns;
            var canvas = new SheetCanvas(Columns * Cell, rows * (Cell + 12));
            for (var i = 0; i < slice.Count; i++)
            {
                var left = (i % Columns) * Cell;
                var top = (i / Columns) * (Cell + 12);
                var (id, _, rgba) = slice[i];
                var scale = (double)side / (Cell - 8);
                for (var y = 0; y < Cell - 8; y++)
                {
                    for (var x = 0; x < Cell - 8; x++)
                    {
                        var o = (((int)(y * scale) * side) + (int)(x * scale)) * 4;
                        canvas.Blend(left + 4 + x, top + 4 + y, rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3] / 255d);
                    }
                }

                // The plate's circle and the eye and chin lines of the framing rule.
                var c = (Cell - 8) / 2;
                canvas.Circle(left + 4 + c, top + 4 + c, c, (230, 207, 152), 0.5);
                canvas.Fill(left + 4, top + 4 + (int)((Cell - 8) * PortraitHeadCrop.EyeLine), Cell - 8, 1, (230, 207, 152), 0.35);
                canvas.Fill(left + 4, top + 4 + (int)((Cell - 8) * PortraitHeadCrop.ChinLine), Cell - 8, 1, (255, 120, 100), 0.35);
                canvas.Text(left + 4, top + Cell, id.ToString(CultureInfo.InvariantCulture), (226, 222, 210), Cell - 8);
            }

            canvas.Save(Path.Combine(dir, $"contact-{page + 1:D2}.png"));
        }
    }

    [GeneratedRegex(@"^Enpc_([0-9]{1,10})\.png\z", RegexOptions.CultureInvariant)]
    private static partial Regex PhotoName();

    /// <summary>
    /// Serial GETs at most one every <c>gap</c>, an identifying User-Agent, retries with back-off on 429, 5xx and dropped
    /// connections, and a permanent cache: a 200 body is kept as the file, a 404 as a <c>.404</c> marker beside it.
    /// </summary>
    private sealed class PoliteFetch : IDisposable
    {
        private readonly HttpClient http;
        private readonly string root;
        private readonly TimeSpan gap;
        private DateTime last = DateTime.MinValue;

        public PoliteFetch(string root, TimeSpan gap)
        {
            this.root = root;
            this.gap = gap;
            http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Tsukimichi.DataGen/1.0 (+https://github.com/xenofei/Tsukimichi; portrait pack build, 1 request/s)");
        }

        public int Live { get; private set; }

        public int Cached { get; private set; }

        /// <summary>The body, or null for a 404. Throws when the host keeps failing.</summary>
        public byte[]? Get(string url, string cacheName)
        {
            var path = Path.Combine(root, cacheName.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                Cached++;
                return File.ReadAllBytes(path);
            }

            if (File.Exists(path + ".404"))
            {
                Cached++;
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            for (var attempt = 1; ; attempt++)
            {
                var wait = last + gap - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    Thread.Sleep(wait);
                }

                last = DateTime.UtcNow;
                Live++;
                try
                {
                    using var response = http.GetAsync(url).GetAwaiter().GetResult();
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        File.WriteAllText(path + ".404", string.Empty);
                        return null;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var body = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        File.WriteAllBytes(path, body);
                        return body;
                    }

                    if (attempt >= 5 || ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.TooManyRequests))
                    {
                        throw new InvalidOperationException($"{url}: HTTP {(int)response.StatusCode}");
                    }
                }
                catch (HttpRequestException) when (attempt < 5)
                {
                    // Retried below.
                }
                catch (TaskCanceledException) when (attempt < 5)
                {
                    // A time-out: retried below.
                }

                Thread.Sleep(TimeSpan.FromSeconds(5 * Math.Pow(3, attempt - 1)));
            }
        }

        public void Dispose() => http.Dispose();
    }
}
