using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The portrait pack this build of the plugin offers (feature plan v7 F4, decision 8): the GitHub release that hosts it,
/// the asset's name, and the size and SHA-256 the download must match. Shipped as <c>Data/portrait_pack.json</c>,
/// written by <c>Tsukimichi.DataGen --portrait-pack … --offer</c> when the pack is built for a release.
/// <code>
/// { "schema": 1, "tag": "portraits-1", "asset": "Tsukimichi-portraits.zip", "size": 12345678,
///   "sha256": "&lt;lower-case hex&gt;", "format": 1, "gameVersion": "2026.09.15.0000.0000", "givers": 2412 }
/// </code>
/// The pack has releases of its own, <c>portraits-1</c>, <c>portraits-2</c> … (spec-1.20 F4: "pack 1", "pack 2"); a
/// plugin release names the one it pairs with, and the plugin never asks GitHub whether there is a newer one. The address
/// is never read from the file: it is always <see cref="ReleaseBase"/> + tag + asset, both checked here, so the plugin
/// can only ever fetch an asset of a Tsukimichi release on GitHub.
/// </summary>
public sealed partial record PortraitPackOffer(string Tag, string Asset, long Size, string Sha256, int Format, string GameVersion, int Givers)
{
    /// <summary>The file's name in the plugin's data folder.</summary>
    public const string FileName = "portrait_pack.json";

    /// <summary>Every download starts here: Tsukimichi's own GitHub releases.</summary>
    public const string ReleaseBase = "https://github.com/xenofei/Tsukimichi/releases/download/";

    /// <summary>The largest pack the plugin will download, bytes, whatever the file says.</summary>
    public const long MaxSize = 64L * 1024 * 1024;

    /// <summary>
    /// The hosts a release download may be redirected to: GitHub serves release assets from its own storage hosts.
    /// Anything else is refused.
    /// </summary>
    public static readonly IReadOnlyList<string> AssetHosts = ["objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    /// <summary>The address of the pack: <see cref="ReleaseBase"/>, the tag and the asset.</summary>
    public Uri DownloadUri => new(ReleaseBase + Tag + "/" + Asset, UriKind.Absolute);

    /// <summary>The release's name, for the confirmation ("release portraits-1").</summary>
    public string ReleaseName => Tag;

    /// <summary>The pack's number (1 for <c>portraits-1</c>), for "pack 1"; 0 when the tag does not say.</summary>
    public int PackNumber => PackNumberOf(Tag);

    /// <summary>The pack number in a <c>portraits-N</c> tag; 0 for anything else.</summary>
    public static int PackNumberOf(string? tag) =>
        tag is not null && tag.StartsWith(TagPrefix, StringComparison.Ordinal)
        && int.TryParse(tag.AsSpan(TagPrefix.Length), System.Globalization.NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private const string TagPrefix = "portraits-";

    /// <summary>
    /// Whether <paramref name="uri"/> may be fetched at all: the pinned release address itself, or HTTPS on one of
    /// GitHub's asset hosts (<see cref="AssetHosts"/>), the only places a release download redirects to. No other host,
    /// scheme, port or user info is ever fetched.
    /// </summary>
    public bool Allows(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
        {
            return false;
        }

        if (uri == DownloadUri)
        {
            return true;
        }

        return AssetHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Reads the shipped offer file; null (no pack offered) when it is missing, and null with a warning when it is broken.</summary>
    public static PortraitPackOffer? Load(string path, out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return Parse(File.ReadAllBytes(path), out warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warning = "portrait pack offer not readable: " + ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Reads an offer; null when the file offers no pack (no tag yet), and null with <paramref name="warning"/> when a
    /// field is not what a release writes: a tag other than <c>portraits-N</c>, an asset name other than a plain <c>.zip</c>
    /// name, a size outside 1 byte to <see cref="MaxSize"/>, a hash that is not SHA-256, or a key given twice. Never throws
    /// on what the file holds: the plugin reads it while it loads.
    /// </summary>
    public static PortraitPackOffer? Parse(ReadOnlySpan<byte> json, out string? warning)
    {
        warning = null;
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            warning = "portrait pack offer is not JSON: " + ex.Message;
            return null;
        }

        if (root is null)
        {
            warning = "portrait pack offer is not a JSON object";
            return null;
        }

        try
        {
            return Read(root, out warning);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // JsonObject builds its key lookup on the first read and throws on a repeated key ("tag" twice).
            warning = "portrait pack offer repeats a key";
            return null;
        }
    }

    private static PortraitPackOffer? Read(JsonObject root, out string? warning)
    {
        warning = null;
        var tag = Text(root["tag"]);
        if (string.IsNullOrEmpty(tag))
        {
            // Shipped before a pack is built: nothing is offered.
            return null;
        }

        var asset = Text(root["asset"]) ?? string.Empty;
        var sha = Text(root["sha256"]) ?? string.Empty;
        long size = root["size"] is JsonValue sizeValue && sizeValue.TryGetValue<long>(out var s) ? s : 0;
        int format = root["format"] is JsonValue formatValue && formatValue.TryGetValue<int>(out var f) ? f : 0;
        int givers = root["givers"] is JsonValue giversValue && giversValue.TryGetValue<int>(out var g) ? g : 0;
        if (!TagPattern().IsMatch(tag) || !AssetPattern().IsMatch(asset))
        {
            warning = "portrait pack offer names an address that is not a release asset";
            return null;
        }

        if (size <= 0 || size > MaxSize || !PortraitPackManifest.IsSha256(sha) || format <= 0)
        {
            warning = "portrait pack offer has no valid size, hash or format";
            return null;
        }

        return new PortraitPackOffer(tag, asset, size, sha, format, Text(root["gameVersion"]) ?? string.Empty, Math.Max(0, givers));
    }

    /// <summary>The offer file as a release writes it.</summary>
    public byte[] ToJson()
    {
        var root = new JsonObject
        {
            ["schema"] = 1,
            ["note"] = "Written by Tsukimichi.DataGen --portrait-pack. The release step uploads the zip with exactly this name, size and hash to this tag.",
            ["tag"] = Tag,
            ["asset"] = Asset,
            ["size"] = Size,
            ["sha256"] = Sha256,
            ["format"] = Format,
            ["gameVersion"] = GameVersion,
            ["givers"] = Givers,
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    /// <summary>The size as the Settings row says it: "14.2 MB".</summary>
    public static string SizeText(long bytes) =>
        bytes >= 1024 * 1024
            ? (bytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
            : Math.Max(1, (int)Math.Round(bytes / 1024d)).ToString(CultureInfo.InvariantCulture) + " KB";

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"^portraits-[1-9][0-9]{0,3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\.zip\z", RegexOptions.CultureInvariant)]
    private static partial Regex AssetPattern();
}
