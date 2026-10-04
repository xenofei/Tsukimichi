using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// An installed portrait pack (feature plan v7 F4): the folder it lives in, its manifest, and each giver's image path,
/// worked out once so a plate asks for one without allocating. Immutable; safe to read from any thread.
/// </summary>
public sealed class PortraitPack
{
    private readonly FrozenDictionary<uint, string> paths;

    public PortraitPack(string folder, PortraitPackManifest manifest, string sha256, string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        Folder = Path.GetFullPath(folder);
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Sha256 = sha256 ?? string.Empty;
        Tag = tag ?? string.Empty;
        var files = manifest.Files.Keys.ToDictionary(name => name, name => Path.Combine(Folder, name), StringComparer.Ordinal);
        paths = manifest.Entries.ToFrozenDictionary(kv => kv.Key, kv => files[kv.Value]);
    }

    /// <summary>The installed folder (full path).</summary>
    public string Folder { get; }

    public PortraitPackManifest Manifest { get; }

    /// <summary>The SHA-256 of the zip it was installed from (compared with the offer to see an update).</summary>
    public string Sha256 { get; }

    /// <summary>The release tag it came from ("v1.20.0").</summary>
    public string Tag { get; }

    /// <summary>How many givers have a portrait in it.</summary>
    public int Givers => paths.Count;

    /// <summary>The game version the pack was built for.</summary>
    public string GameVersion => Manifest.GameVersion;

    /// <summary>Whether the pack has a portrait of ENpcResident <paramref name="npcId"/>.</summary>
    public bool Has(uint npcId) => paths.ContainsKey(npcId);

    /// <summary>The full path of <paramref name="npcId"/>'s image; false when the pack has none.</summary>
    public bool TryGetPath(uint npcId, [MaybeNullWhen(false)] out string path) => paths.TryGetValue(npcId, out path);
}

/// <summary>
/// The portrait pack's folder in the plugin's config directory (feature plan v7 F4): <c>portraits/</c> holds one folder
/// per installed pack, named by its zip's hash (<c>portraits/3fa2c1d0e9b8/</c>, so a new pack never shares a texture
/// path with the old one), each download while it runs (<c>download-&lt;id&gt;.part</c>), and <c>current.json</c>, which names the
/// pack in use and is written last. Each step leaves either the old pack or the new one in use, never half of one: a
/// pack is extracted into a staging folder, renamed into place, and only then named in <c>current.json</c>. Folders
/// <c>current.json</c> does not name (a crash mid-extract, an old pack) are deleted by <see cref="CleanUp"/>.
/// </summary>
public sealed partial class PortraitPackStore
{
    private const string CurrentFileName = "current.json";

    /// <summary>A folder <see cref="CleanUp"/> leaves alone while this young: another game client may be extracting into it.</summary>
    private static readonly TimeSpan BusyFolderAge = TimeSpan.FromMinutes(10);

    public PortraitPackStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>The <c>portraits</c> folder (full path).</summary>
    public string Root { get; }

    /// <summary>A file of its own for one download while it runs (<c>download-&lt;id&gt;.part</c>), so two game clients never share one.</summary>
    public string NewDownloadFile() => Path.Combine(Root, "download-" + Guid.NewGuid().ToString("N")[..12] + ".part");

    private string CurrentFile => Path.Combine(Root, CurrentFileName);

    /// <summary>
    /// The installed pack, or null. <paramref name="damaged"/> is true when <c>current.json</c> names a pack that is not
    /// whole (its folder or manifest gone or unreadable, or a manifest this build cannot read): the Settings row then
    /// offers to download it again or remove it.
    /// </summary>
    public PortraitPack? Load(out bool damaged)
    {
        damaged = false;
        try
        {
            if (!File.Exists(CurrentFile))
            {
                return null;
            }

            var root = JsonNode.Parse(File.ReadAllBytes(CurrentFile)) as JsonObject;
            var folder = Text(root?["folder"]);
            var sha = Text(root?["sha256"]) ?? string.Empty;
            var tag = Text(root?["tag"]) ?? string.Empty;
            if (folder is null || !FolderName().IsMatch(folder) || !PortraitPackManifest.IsSha256(sha))
            {
                damaged = true;
                return null;
            }

            var path = Path.Combine(Root, folder);
            var manifestFile = Path.Combine(path, PortraitPackManifest.FileName);
            if (!File.Exists(manifestFile))
            {
                damaged = true;
                return null;
            }

            var manifest = PortraitPackManifest.TryParse(File.ReadAllBytes(manifestFile), out _);
            if (manifest is null)
            {
                damaged = true;
                return null;
            }

            return new PortraitPack(path, manifest, sha, tag);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            damaged = true;
            return null;
        }
    }

    /// <summary>
    /// Installs the downloaded zip at <paramref name="zipPath"/>: opens it once, with writes shut out, checks its size and
    /// SHA-256 against <paramref name="offer"/> on that same handle, extracts it from that handle into a staging folder
    /// (<see cref="PortraitPackArchive.Extract(ZipArchive, string, CancellationToken, out PortraitPackManifest?, out string?)"/>),
    /// renames that into place and names it in <c>current.json</c>. So the bytes extracted are the bytes checked. On any
    /// failure the pack in use stays as it was and the staging folder is deleted.
    /// </summary>
    public PortraitPackFailure Install(string zipPath, PortraitPackOffer offer, CancellationToken cancellation, out PortraitPack? pack, out string? detail)
    {
        ArgumentNullException.ThrowIfNull(offer);
        pack = null;
        detail = null;
        var folder = offer.Sha256[..12];
        var staging = Path.Combine(Root, folder + ".staging-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(Root);
            using var file = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length != offer.Size)
            {
                return PortraitPackFailure.SizeMismatch;
            }

            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(file)), offer.Sha256, StringComparison.Ordinal))
            {
                return PortraitPackFailure.HashMismatch;
            }

            file.Position = 0;
            PortraitPackFailure result;
            PortraitPackManifest? manifest;
            try
            {
                using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
                result = PortraitPackArchive.Extract(zip, staging, cancellation, out manifest, out detail);
            }
            catch (InvalidDataException ex)
            {
                detail = ex.Message;
                return PortraitPackFailure.BadArchive;
            }

            if (result != PortraitPackFailure.None || manifest is null)
            {
                return result == PortraitPackFailure.None ? PortraitPackFailure.BadManifest : result;
            }

            cancellation.ThrowIfCancellationRequested();
            var final = Path.Combine(Root, folder);
            if (Directory.Exists(final))
            {
                // The same pack again (a repair): set the old copy aside, so the swap is a rename either way.
                var aside = Path.Combine(Root, folder + ".old-" + Guid.NewGuid().ToString("N")[..8]);
                Directory.Move(final, aside);
                TryDeleteFolder(aside);
            }

            Directory.Move(staging, final);
            WriteCurrent(folder, offer.Sha256, offer.Tag);
            pack = new PortraitPack(final, manifest, offer.Sha256, offer.Tag);
            return PortraitPackFailure.None;
        }
        catch (OperationCanceledException)
        {
            return PortraitPackFailure.Cancelled;
        }
        catch (IOException ex)
        {
            detail = ex.Message;
            return IsDiskFull(ex) ? PortraitPackFailure.DiskFull : PortraitPackFailure.DiskError;
        }
        catch (UnauthorizedAccessException ex)
        {
            detail = ex.Message;
            return PortraitPackFailure.DiskError;
        }
        finally
        {
            TryDeleteFolder(staging);
        }
    }

    /// <summary>
    /// Removes the pack: <c>current.json</c> goes first (from then on no pack is in use), then its folder. A folder that
    /// cannot be deleted now (a texture still loading from it) goes at the next <see cref="CleanUp"/>. False when
    /// <c>current.json</c> could not be deleted.
    /// </summary>
    public bool Remove()
    {
        string? folder = null;
        try
        {
            if (File.Exists(CurrentFile) && JsonNode.Parse(File.ReadAllBytes(CurrentFile)) is JsonObject root)
            {
                folder = Text(root["folder"]);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            folder = null;
        }

        try
        {
            File.Delete(CurrentFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (folder is not null && FolderName().IsMatch(folder))
        {
            TryDeleteFolder(Path.Combine(Root, folder));
        }

        CleanUp(ignoreAge: true);
        return true;
    }

    /// <summary>
    /// Deletes what an interrupted download or install left (<c>download-*.part</c>, staging folders) and old packs'
    /// folders: every folder under <see cref="Root"/> that <c>current.json</c> does not name. A folder changed in the
    /// last ten minutes is left (another game client may be installing into it) unless <paramref name="ignoreAge"/>.
    /// Never throws.
    /// </summary>
    public void CleanUp(bool ignoreAge = false)
    {
        try
        {
            if (!Directory.Exists(Root))
            {
                return;
            }

            string? keep = null;
            if (File.Exists(CurrentFile) && JsonNode.Parse(File.ReadAllBytes(CurrentFile)) is JsonObject root)
            {
                keep = Text(root["folder"]);
            }

            var now = DateTime.UtcNow;
            foreach (var dir in Directory.EnumerateDirectories(Root))
            {
                var name = Path.GetFileName(dir);
                if (string.Equals(name, keep, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ignoreAge || now - Directory.GetLastWriteTimeUtc(dir) > BusyFolderAge)
                {
                    TryDeleteFolder(dir);
                }
            }

            foreach (var part in Directory.EnumerateFiles(Root, "download*.part"))
            {
                if (ignoreAge || now - File.GetLastWriteTimeUtc(part) > BusyFolderAge)
                {
                    TryDelete(part);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            // Left for the next start.
        }
    }

    /// <summary>Whether <paramref name="ex"/> says the disk is full (ERROR_DISK_FULL or ERROR_HANDLE_DISK_FULL).</summary>
    public static bool IsDiskFull(IOException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var code = ex.HResult & 0xFFFF;
        return code is 0x70 or 0x27;
    }

    /// <summary>Deletes a file if it is there; never throws.</summary>
    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next clean-up.
        }
    }

    private static void TryDeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next clean-up.
        }
    }

    private void WriteCurrent(string folder, string sha256, string tag)
    {
        var root = new JsonObject
        {
            ["format"] = 1,
            ["folder"] = folder,
            ["sha256"] = sha256,
            ["tag"] = tag,
            ["installedUtc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        };
        AtomicFile.Write(CurrentFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"^[0-9a-f]{12}\z", RegexOptions.CultureInvariant)]
    private static partial Regex FolderName();
}
