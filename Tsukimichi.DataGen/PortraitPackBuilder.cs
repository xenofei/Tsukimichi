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
/// a second, every answer cached on disk so a rerun fetches nothing it has. Each photo is cut to a
/// <see cref="PortraitPackManifest.ImageSide"/> px square framed by the plugin's rule (eye line 44 %, chin 81 %): the
/// square a per-NPC box names (<c>--overrides</c>, default <see cref="PortraitPackOverrides.DefaultPath"/>; the portrait
/// audit's change C9), else the head finder's (<see cref="PortraitHeadCrop"/>, change C3). It is quantised to a
/// 256-colour PNG and stored once however many giver ids share it. A head smaller than <see cref="MinBox"/> px is held at
/// that size, centred on the same eye line (a 72 px plate never upscales), and each image's box goes in the manifest so
/// the hover never shows a face larger than its source.
/// <para>
/// Writes <c>Tsukimichi-portraits.zip</c> (manifest and images; the same photos, install and .NET runtime make the same
/// bytes on any day: the manifest is stamped with the game version's date or <c>--built yyyy-MM-dd</c>, never the clock,
/// and <see cref="PortraitPackArchive.Write"/> pins everything machine-dependent in the zip), its
/// <c>.sha256</c>, <c>report.md</c> (coverage, framing, every giver without a photo and every per-NPC box not used),
/// <c>boxes.json</c> (each image's square in its photo) and contact sheets under <c>sheets/</c> for a spot check of the
/// crops (<c>--skip &lt;ids&gt;</c> leaves out a giver; a per-NPC box re-cuts one). A per-NPC box outside its photo,
/// under <see cref="MinBox"/> px, or disagreeing with another for the same photo stops the build. With
/// <c>--offer &lt;file&gt; --tag &lt;portraits-N&gt;</c> it also writes the plugin's <c>portrait_pack.json</c>: the
/// release, asset name, size and hash the plugin will accept. Nothing is uploaded.
/// </para>
/// </summary>
internal static partial class PortraitPackBuilder
{
    public const string AssetName = "Tsukimichi-portraits.zip";
    private const string DocUrl = "https://www.garlandtools.org/db/doc/npc/en/2/{0}.json";
    private const string PhotoUrl = "https://www.garlandtools.org/files/photos/npc/{0}";
    private const string Source = "Garland Tools NPC photos (garlandtools.org; photos credit: Celes)";

    /// <summary>The smallest square cut, source px (spec-1.20 F4: the 72 px plate never upscales at 100 %).</summary>
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
        var overridesPath = PortraitPackOverrides.DefaultPath;
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
                case "--overrides" when i + 1 < args.Length:
                    overridesPath = args[++i];
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

        var overrides = PortraitPackOverrides.Load(overridesPath, out var overrideErrors);
        if (overrideErrors.Count > 0)
        {
            Console.Error.WriteLine($"{overridesPath}: {string.Join("; ", overrideErrors)}");
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
        var layout = new SortedDictionary<string, PhotoCrop>(StringComparer.Ordinal);
        var errors = new List<string>();

        // First every giver's photo (givers who share one share an image, and an override for any of them frames it),
        // then each photo once.
        var byPhoto = new Dictionary<string, List<PortraitGiver>>(StringComparer.Ordinal);
        var photoOrder = new List<(string FileName, string Photo)>();
        var done = 0;
        foreach (var giver in givers)
        {
            done++;
            if (done % 50 == 0)
            {
                Console.WriteLine($"  {done}/{givers.Count}: {byPhoto.Values.Sum(g => g.Count)} with a photo ({fetch.Live} fetched, {fetch.Cached} cached)");
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
            if (photo is null || photoMatch is not { Success: true })
            {
                missing.Add((giver.NpcId, giver.Name, "no photo"));
                continue;
            }

            var fileName = photoMatch.Groups[1].Value + ".png";
            if (!byPhoto.TryGetValue(fileName, out var sharing))
            {
                byPhoto[fileName] = sharing = [];
                photoOrder.Add((fileName, photo));
            }

            sharing.Add(giver);
        }

        foreach (var (fileName, photo) in photoOrder)
        {
            var sharing = byPhoto[fileName];
            var bytes = fetch.Get(string.Format(CultureInfo.InvariantCulture, PhotoUrl, photo), $"photos/{photo}");
            if (bytes is null || !PackPng.TryDecode(bytes, out var width, out var height, out var rgba, maxSide: 4096))
            {
                var why = bytes is null ? "photo missing" : "photo does not decode";
                missing.AddRange(sharing.Select(g => (g.NpcId, g.Name, why)));
                continue;
            }

            // The first (busiest) giver's race sizes the head: givers who share a photo wear the same body.
            var crop = CropPhoto(rgba, width, height, sharing[0].Race, [.. sharing.Select(g => g.NpcId)], overrides);
            if (crop.Problem is not null)
            {
                errors.Add($"{fileName} (giver {string.Join(", ", sharing.Select(g => g.NpcId))}): {crop.Problem}");
                continue;
            }

            if (crop.Rgba is null)
            {
                missing.AddRange(sharing.Select(g => (g.NpcId, g.Name, "no face found in the photo (no figure, or the frame caught a weapon, hat or ears)")));
                continue;
            }

            layout[fileName] = crop;
            boxes[fileName] = (int)Math.Round(crop.Box.Side);
            images[fileName] = PortraitQuantizer.Encode(crop.Rgba, PortraitPackManifest.ImageSide, PortraitPackManifest.ImageSide);
            sheet.Add((sharing[0].NpcId, sharing[0].Name, crop.Rgba));
            foreach (var giver in sharing)
            {
                entries[giver.NpcId] = fileName;
            }
        }

        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"{overridesPath}: {errors.Count} per-NPC box(es) cannot be used; nothing written:");
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"  {error}");
            }

            return 1;
        }

        // An override for an NPC the install does not name as a quest giver is reported, not fatal (a patch can retire
        // one); so is one whose giver has no image in this build.
        var named = inputs.Givers.Where(g => g.Name.Length > 0 && !PortraitNames.IsGeneric(g.Name)).Select(g => g.NpcId).ToHashSet();
        var unknown = overrides.Unknown(named);
        var asked = givers.Select(g => g.NpcId).ToHashSet();
        var unused = overrides.Entries.Keys.Where(id => asked.Contains(id) && !entries.ContainsKey(id)).ToList();
        foreach (var id in unknown)
        {
            Console.WriteLine($"  {overridesPath}: {id} is not a named quest giver in this install (override not used)");
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
        WriteBoxes(Path.Combine(outDir, "boxes.json"), layout);
        var report = Report(gameVersion, builtUtc, inputs, gameIndex, givers, entries, images, missing, zipBytes.LongLength, zipSha)
            + Framing(overridesPath, layout, unknown, unused);
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

    /// <summary>
    /// One photo's image: its <paramref name="Rgba"/> (<see cref="PortraitPackManifest.ImageSide"/> px; null when the
    /// photo gives none), the square it was cut from, the head finder's own square (null when it found no head),
    /// whether a per-NPC box chose the square, and the <paramref name="Problem"/> that makes a per-NPC box unusable.
    /// </summary>
    internal readonly record struct PhotoCrop(byte[]? Rgba, PhotoBox Box, PhotoBox? Head, bool FromOverride, string? Problem);

    /// <summary>
    /// The image for a photo the givers <paramref name="npcIds"/> share: a per-NPC box when one of them has one
    /// (<see cref="PortraitPackOverrides"/>; it must lie inside the photo and be at least <see cref="MinBox"/> px), else
    /// <see cref="PortraitHeadCrop"/>'s square held at <see cref="MinBox"/> px. A per-NPC box is the owner's or the audit's
    /// call, so it is kept even where the head finder would have dropped the frame as empty (a helmet, a mask).
    /// </summary>
    internal static PhotoCrop CropPhoto(byte[] rgba, int width, int height, byte race, IReadOnlyList<uint> npcIds, PortraitPackOverrides overrides)
    {
        var head = PortraitHeadCrop.Find(rgba, width, height, race);
        var chosen = overrides.For(npcIds, out var conflict);
        if (conflict is not null)
        {
            return new PhotoCrop(null, default, head, true, conflict);
        }

        if (chosen is { } box)
        {
            var problem = PortraitPackOverrides.Check(box, width, height, MinBox);
            return problem is null
                ? new PhotoCrop(PortraitHeadCrop.Render(rgba, width, height, box, PortraitPackManifest.ImageSide), box, head, true, null)
                : new PhotoCrop(null, box, head, true, problem);
        }

        if (head is not { } found)
        {
            return new PhotoCrop(null, default, null, false, null);
        }

        var crop = PortraitHeadCrop.Crop(rgba, width, height, found, PortraitPackManifest.ImageSide, MinBox, out var framed);
        return new PhotoCrop(crop, framed, head, false, null);
    }

    /// <summary><c>boxes.json</c>: every image's square in its photo, where it came from, and the head finder's own square.</summary>
    private static void WriteBoxes(string path, SortedDictionary<string, PhotoCrop> layout)
    {
        static JsonArray Box(PhotoBox b) => [Math.Round(b.Left, 1), Math.Round(b.Top, 1), Math.Round(b.Side, 1)];
        var root = new JsonObject();
        foreach (var (file, crop) in layout)
        {
            var row = new JsonObject { ["photoBox"] = Box(crop.Box), ["from"] = crop.FromOverride ? "override" : "head" };
            if (crop.Head is { } head)
            {
                row["head"] = Box(head);
            }

            root[file] = row;
        }

        File.WriteAllText(path, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    /// <summary>The report's framing section: how many images each source framed, and every override not used.</summary>
    private static string Framing(string overridesPath, SortedDictionary<string, PhotoCrop> layout, IReadOnlyList<uint> unknown, List<uint> unused)
    {
        // A per-NPC box "agrees" with the head finder when their centres are within 0.05 of the box and their sides
        // within 10 %: such an override could go once the head finder is trusted (changes.json C9).
        static bool Agrees(PhotoBox a, PhotoBox b) =>
            Math.Abs((a.Left + (a.Side / 2)) - (b.Left + (b.Side / 2))) <= 0.05 * a.Side
            && Math.Abs((a.Top + (a.Side / 2)) - (b.Top + (b.Side / 2))) <= 0.05 * a.Side
            && Math.Abs((b.Side / a.Side) - 1) <= 0.1;
        var fromOverride = layout.Values.Where(c => c.FromOverride).ToList();
        var fromHead = layout.Values.Where(c => !c.FromOverride).ToList();
        var text = new StringBuilder();
        text.AppendLine("## Framing");
        text.AppendLine();
        text.AppendLine("Eye line at 44 % of the square, chin at 81 %. `boxes.json` lists every image's square in its photo.");
        text.AppendLine();
        text.AppendLine("| | images |");
        text.AppendLine("|---|---|");
        text.AppendLine($"| From a per-NPC box (`{overridesPath}`) | {fromOverride.Count:N0} |");
        text.AppendLine($"| … where the head finder's square agrees (centre within 0.05, side within 10 %) | {fromOverride.Count(c => c.Head is { } h && Agrees(c.Box, h)):N0} |");
        text.AppendLine($"| From the head finder | {fromHead.Count:N0} |");
        text.AppendLine($"| … held at {MinBox} px (the face smaller than the rule frames it) | {fromHead.Count(c => c.Head is { } h && h.Side < MinBox):N0} |");
        text.AppendLine();
        if (unknown.Count > 0)
        {
            text.AppendLine($"### Per-NPC boxes for NPCs that are not named quest givers ({unknown.Count})");
            text.AppendLine();
            text.AppendLine(string.Join(", ", unknown));
            text.AppendLine();
        }

        if (unused.Count > 0)
        {
            text.AppendLine($"### Per-NPC boxes whose giver has no image in this build ({unused.Count})");
            text.AppendLine();
            text.AppendLine(string.Join(", ", unused));
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
