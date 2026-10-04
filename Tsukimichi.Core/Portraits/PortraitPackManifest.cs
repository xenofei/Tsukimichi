using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The portrait pack's <c>manifest.json</c> (feature plan v7 F4): which image is whose, and the SHA-256 of every image,
/// so the plugin can check each file it extracts. Written by <c>Tsukimichi.DataGen --portrait-pack</c>, read by the
/// plugin before anything of the pack is installed.
/// <code>
/// { "format": 1, "gameVersion": "2026.09.15.0000.0000", "builtUtc": "2026-10-03T12:00:00Z", "side": 128,
///   "source": "Garland Tools NPC photos (credit: Celes)",
///   "files":   { "1001000.png": "&lt;sha-256, lower-case hex&gt;", … },
///   "boxes":   { "1001000.png": 118, … },
///   "entries": { "1001000": "1001000.png", "1012527": "1001000.png", … } }
/// </code>
/// <c>entries</c> maps an ENpcResident id (the quest giver) to its image; several ids can share one (Garland's
/// appearance aliases). <c>boxes</c> (optional) gives each image's head box in the photo's own pixels, so the hover never
/// shows a face larger than its source. Image names are flat (<see cref="IsSafeFileName"/>): no folders, nothing but <c>.png</c>.
/// </summary>
public sealed partial class PortraitPackManifest
{
    /// <summary>The manifest format this build reads and writes.</summary>
    public const int CurrentFormat = 1;

    /// <summary>The manifest's name inside the zip and in the installed folder.</summary>
    public const string FileName = "manifest.json";

    /// <summary>The folder the images sit in inside the zip.</summary>
    public const string ImageFolder = "portraits";

    /// <summary>The most images a pack may hold (the catalog has about 3,200 giver ids).</summary>
    public const int MaxFiles = 20_000;

    /// <summary>The largest manifest the plugin reads, bytes.</summary>
    public const int MaxManifestBytes = 4 * 1024 * 1024;

    /// <summary>The image side a pack's images have (<see cref="PortraitSources.TextureSize"/> of the pack family).</summary>
    public const int ImageSide = 128;

    private PortraitPackManifest(int format, string gameVersion, DateTime builtUtc, int side, string source, FrozenDictionary<string, string> files, FrozenDictionary<uint, string> entries, FrozenDictionary<string, int>? boxes = null)
    {
        Boxes = boxes ?? FrozenDictionary<string, int>.Empty;
        Format = format;
        GameVersion = gameVersion;
        BuiltUtc = builtUtc;
        Side = side;
        Source = source;
        Files = files;
        Entries = entries;
    }

    public int Format { get; }

    /// <summary>The game version the pack's giver list was read from.</summary>
    public string GameVersion { get; }

    public DateTime BuiltUtc { get; }

    /// <summary>Every image's side, px.</summary>
    public int Side { get; }

    /// <summary>Where the images come from, for the record (the plugin shows its own fixed credit line).</summary>
    public string Source { get; }

    /// <summary>Image name to its SHA-256 (lower-case hex).</summary>
    public FrozenDictionary<string, string> Files { get; }

    /// <summary>ENpcResident id to image name.</summary>
    public FrozenDictionary<uint, string> Entries { get; }

    /// <summary>Image name to its head box's side in the photo's own pixels; missing when the pack does not say.</summary>
    public FrozenDictionary<string, int> Boxes { get; }

    /// <summary>A flat image name: a lower-case letter or digit first, then letters, digits, '_' or '-', and <c>.png</c>; 64 characters at most.</summary>
    public static bool IsSafeFileName(string? name) => name is not null && SafeName().IsMatch(name);

    /// <summary>Whether <paramref name="text"/> is a SHA-256 as the pack writes it: 64 lower-case hex digits.</summary>
    public static bool IsSha256(string? text) => text is not null && Sha256Hex().IsMatch(text);

    /// <summary>Makes a manifest, checked as <see cref="TryParse"/> checks one; throws when it would not read back.</summary>
    public static PortraitPackManifest Create(string gameVersion, DateTime builtUtc, string source, IReadOnlyDictionary<string, string> files, IReadOnlyDictionary<uint, string> entries, int side = ImageSide, IReadOnlyDictionary<string, int>? boxes = null)
    {
        var made = new PortraitPackManifest(CurrentFormat, gameVersion ?? string.Empty, builtUtc, side, source ?? string.Empty, files.ToFrozenDictionary(StringComparer.Ordinal), entries.ToFrozenDictionary(), boxes?.ToFrozenDictionary(StringComparer.Ordinal));
        var problem = Check(made);
        return problem is null ? made : throw new ArgumentException(problem);
    }

    /// <summary>
    /// Reads a manifest; null with <paramref name="error"/> set when it is not one this build can trust: not JSON, a
    /// format other than <see cref="CurrentFormat"/>, an unsafe image name, a hash that is not SHA-256, an entry naming
    /// an image the manifest does not list, no entries, or more than <see cref="MaxFiles"/>.
    /// </summary>
    public static PortraitPackManifest? TryParse(ReadOnlySpan<byte> json, out string? error)
    {
        error = null;
        if (json.Length == 0 || json.Length > MaxManifestBytes)
        {
            error = "the manifest is empty or too large";
            return null;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 8 }) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            root = null;
        }

        if (root is null)
        {
            error = "the manifest is not a JSON object";
            return null;
        }

        try
        {
            return Read(root, out error);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            error = "the manifest repeats a key";
            return null;
        }
    }

    private static PortraitPackManifest? Read(JsonObject root, out string? error)
    {
        error = null;
        if (!TryInt(root["format"], out var format))
        {
            error = "the manifest has no format";
            return null;
        }

        if (format != CurrentFormat)
        {
            error = format > CurrentFormat ? "the pack needs a newer Tsukimichi" : "the pack's format is no longer read";
            return null;
        }

        if (!TryInt(root["side"], out var side))
        {
            side = ImageSide;
        }

        var builtUtc = DateTime.TryParse(Text(root["builtUtc"]), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var built)
            ? built
            : default;

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["files"] is JsonObject fileNodes)
        {
            foreach (var (name, node) in fileNodes)
            {
                files[name] = Text(node) ?? string.Empty;
            }
        }

        var entries = new Dictionary<uint, string>();
        if (root["entries"] is JsonObject entryNodes)
        {
            foreach (var (key, node) in entryNodes)
            {
                if (!uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
                {
                    error = "an entry's id is not a number: " + Clip(key);
                    return null;
                }

                entries[id] = Text(node) ?? string.Empty;
            }
        }

        var boxes = new Dictionary<string, int>(StringComparer.Ordinal);
        if (root["boxes"] is JsonObject boxNodes)
        {
            foreach (var (name, node) in boxNodes)
            {
                if (TryInt(node, out var box) && box is > 0 and <= 4096)
                {
                    boxes[name] = box;
                }
            }
        }

        var manifest = new PortraitPackManifest(format, Text(root["gameVersion"]) ?? string.Empty, builtUtc, side, Text(root["source"]) ?? string.Empty, files.ToFrozenDictionary(StringComparer.Ordinal), entries.ToFrozenDictionary(), boxes.ToFrozenDictionary(StringComparer.Ordinal));
        error = Check(manifest);
        return error is null ? manifest : null;
    }

    /// <summary>The manifest as the pack stores it: UTF-8 JSON, keys in order, so a rebuild from the same photos is byte-identical.</summary>
    public byte[] ToJson()
    {
        var files = new JsonObject();
        foreach (var (name, sha) in Files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            files[name] = sha;
        }

        var entries = new JsonObject();
        foreach (var (id, name) in Entries.OrderBy(kv => kv.Key))
        {
            entries[id.ToString(CultureInfo.InvariantCulture)] = name;
        }

        var boxes = new JsonObject();
        foreach (var (name, box) in Boxes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            boxes[name] = box;
        }

        var root = new JsonObject
        {
            ["format"] = Format,
            ["gameVersion"] = GameVersion,
            ["builtUtc"] = BuiltUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["side"] = Side,
            ["source"] = Source,
            ["files"] = files,
            ["boxes"] = boxes,
            ["entries"] = entries,
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static string? Check(PortraitPackManifest manifest)
    {
        if (manifest.Side is < 16 or > 512)
        {
            return "the image side is out of range";
        }

        if (manifest.Files.Count == 0 || manifest.Entries.Count == 0)
        {
            return "the manifest lists no portraits";
        }

        if (manifest.Files.Count > MaxFiles || manifest.Entries.Count > MaxFiles * 4)
        {
            return "the manifest lists too many portraits";
        }

        foreach (var (name, sha) in manifest.Files)
        {
            if (!IsSafeFileName(name))
            {
                return "an image name is not allowed: " + Clip(name);
            }

            if (!IsSha256(sha))
            {
                return "an image's hash is not a SHA-256: " + Clip(name);
            }
        }

        foreach (var (id, name) in manifest.Entries)
        {
            if (!manifest.Files.ContainsKey(name))
            {
                return "entry " + id.ToString(CultureInfo.InvariantCulture) + " names an image the manifest does not list";
            }
        }

        return null;
    }

    private static bool TryInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue v && v.TryGetValue(out value);
    }

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static string Clip(string text) => text.Length <= 40 ? text : text[..40] + "…";

    // \z, not $: $ also matches before a final line break.
    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]{0,59}\.png\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();

    [GeneratedRegex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();
}
