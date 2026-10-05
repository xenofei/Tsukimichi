using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The quest-giver photos that ship inside the plugin (1.23; the portrait pack of feature plan v7 F4, no longer a
/// download): <c>&lt;plugin&gt;/assets/portraits/</c> holds <c>manifest.json</c> and one 128 px PNG per photo, written by
/// <c>Tsukimichi.DataGen --portrait-pack --bundle</c>, which checks every image against the manifest's SHA-256 and
/// decodes it before it is bundled. At start the plugin reads the manifest and makes sure every image it lists is there
/// and not empty (one directory listing; the hashes were checked at build time). Nothing here goes online.
/// <para>
/// 1.20 to 1.22 downloaded the pack into the config folder's <c>portraits/</c>; <see cref="RemoveDownloaded"/> deletes
/// that copy once, and only what that download wrote there.
/// </para>
/// </summary>
public static partial class BundledPortraits
{
    /// <summary>The folder under the plugin's <c>assets</c> that holds the pack.</summary>
    public const string FolderName = "portraits";

    /// <summary>The pack's tag in the log.</summary>
    public const string Tag = "bundled";

    /// <summary>
    /// The photos the plates may use under Settings › Look › Giver portraits: all of <paramref name="pack"/> under Game
    /// art + photos, none under Game art or Off.
    /// </summary>
    public static PortraitPack? InUse(Ui.GiverPortraitMode mode, PortraitPack? pack) =>
        mode == Ui.GiverPortraitMode.GameArtAndPack ? pack : null;

    /// <summary>
    /// Giver portraits once the photos ship with the plugin, for a configuration saved before (<paramref name="hadFile"/>):
    /// Game art, the old default, becomes Game art + photos, the new one, unless the player had downloaded the pack
    /// (<paramref name="hadDownloadedPack"/>) and still chose Game art. Every other choice, Off included, stays.
    /// </summary>
    public static Ui.GiverPortraitMode ModeOnceBundled(Ui.GiverPortraitMode saved, bool hadFile, bool hadDownloadedPack) =>
        hadFile && !hadDownloadedPack && saved == Ui.GiverPortraitMode.GameArt ? Ui.GiverPortraitMode.GameArtAndPack : saved;

    /// <summary>Whether <paramref name="root"/> (the config folder's <c>portraits/</c>) holds an installed downloaded pack.</summary>
    public static bool HasDownloadedPack(string root) => File.Exists(Path.Combine(root, "current.json"));

    /// <summary>
    /// The pack in <paramref name="folder"/>, or null with <paramref name="problem"/> saying why (no folder, no manifest
    /// or one this build cannot read, an image it lists missing or empty). <paramref name="cancellation"/> (the plugin
    /// unloading) stops it with <see cref="OperationCanceledException"/>.
    /// </summary>
    public static PortraitPack? Load(string folder, out string? problem, CancellationToken cancellation = default)
    {
        problem = null;
        try
        {
            var manifestFile = Path.Combine(folder, PortraitPackManifest.FileName);
            if (!File.Exists(manifestFile))
            {
                problem = $"no {PortraitPackManifest.FileName} in {folder}";
                return null;
            }

            var bytes = File.ReadAllBytes(manifestFile);
            var manifest = PortraitPackManifest.TryParse(bytes, out var error);
            if (manifest is null)
            {
                problem = $"{PortraitPackManifest.FileName}: {error}";
                return null;
            }

            var sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
            {
                cancellation.ThrowIfCancellationRequested();
                sizes[file.Name] = file.Length;
            }

            var missing = manifest.Files.Keys.Where(name => !sizes.TryGetValue(name, out var size) || size <= 0).ToList();
            if (missing.Count > 0)
            {
                problem = $"{missing.Count} image(s) the manifest lists are missing or empty, such as {missing[0]}";
                return null;
            }

            return new PortraitPack(folder, manifest, Convert.ToHexStringLower(SHA256.HashData(bytes)), Tag);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            problem = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Deletes the portrait pack 1.20–1.22 downloaded into <paramref name="root"/> (the config folder's
    /// <c>portraits/</c>): its <c>current.json</c>, the pack folders named by a hash (and the staging and set-aside copies
    /// of an install), and unfinished <c>download-*.part</c> files; then the folder itself when nothing else is in it.
    /// Anything else there is left alone. Returns how many entries it deleted; never throws (what it cannot delete now
    /// goes at the next start).
    /// </summary>
    public static int RemoveDownloaded(string root)
    {
        var removed = 0;
        try
        {
            if (!Directory.Exists(root))
            {
                return 0;
            }

            foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
            {
                try
                {
                    if (entry is DirectoryInfo dir && PackFolder().IsMatch(dir.Name))
                    {
                        dir.Delete(recursive: true);
                        removed++;
                    }
                    else if (entry is FileInfo file && (file.Name == "current.json" || DownloadFile().IsMatch(file.Name)))
                    {
                        file.Delete();
                        removed++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // In use or read-only: left for the next start.
                }
            }

            if (!Directory.EnumerateFileSystemEntries(root).Any())
            {
                Directory.Delete(root);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next start.
        }

        return removed;
    }

    // A pack folder (named by the first 12 hex digits of its zip's SHA-256), or its staging or set-aside copy.
    [GeneratedRegex(@"^[0-9a-f]{12}(\.staging-[0-9a-f]{8}|\.old-[0-9a-f]{8})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex PackFolder();

    // A download in progress (download-<12 hex>.part; older builds wrote download.part).
    [GeneratedRegex(@"^download(-[0-9a-f]{12})?\.part\z", RegexOptions.CultureInvariant)]
    private static partial Regex DownloadFile();
}
