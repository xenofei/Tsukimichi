using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Multibox;

/// <summary>
/// One character read from another launcher folder (plan v7, 1.21.0 P3): its last save there and, while its client
/// keeps the heartbeat fresh, that it is logged in.
/// </summary>
/// <param name="Folder">The other folder's Tsukimichi config directory (<c>…\pluginConfigs\Tsukimichi</c>).</param>
/// <param name="Live">A fresh heartbeat of another game process holds the character.</param>
public sealed record LinkedCharacter(string Folder, CharacterSnapshot Snapshot, bool Live)
{
    public ulong ContentId => Snapshot.ContentId;
}

/// <summary>
/// Characters in other XIVLauncher roaming folders (plan v7, 1.21.0 P3; community.md "multibox with separate roaming
/// paths"): the standard multibox setup gives every client its own <c>--roamingPath</c>, so each Tsukimichi keeps its
/// own <c>pluginConfigs\Tsukimichi</c> and the shared-folder multibox (D11) cannot see the others. Settings › Data ›
/// Characters lists folders to also read; this class finds candidates for Auto-detect and folds each scan of them.
/// <para>
/// <b>Read only.</b> A linked folder is only ever listed and read (<see cref="FolderScan.Run"/> over
/// <see cref="JsonSnapshotStore.LoadShared"/>, which never quarantines): nothing there is written, deleted, moved or
/// forgotten, and its per-character settings, pins and verdicts stay its own. A character this folder also has is shown
/// from this folder (its own file is the one this client knows); the first linked folder listing a character wins.
/// </para>
/// </summary>
public sealed class LinkedFolders
{
    /// <summary>The Tsukimichi config folder's name under <c>pluginConfigs</c>.</summary>
    public const string PluginFolder = "Tsukimichi";

    /// <summary>Dalamud's plugin configuration folder's name under a roaming folder.</summary>
    public const string ConfigsFolder = "pluginConfigs";

    /// <summary>The snapshots' folder's name under a Tsukimichi config folder.</summary>
    public const string CharactersFolder = "characters";

    private readonly Dictionary<string, Dictionary<ulong, FileStamp>> known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<ulong, CharacterSnapshot>> saved = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Heartbeat>> beats = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The Tsukimichi config folder a path the player typed or picked stands for: the Tsukimichi folder itself, its
    /// <c>pluginConfigs</c> folder, or the roaming folder above that, whichever holds a <c>characters</c> folder. Null
    /// when none does.
    /// </summary>
    public static string? Resolve(string? path, Func<string, bool>? directoryExists = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var exists = directoryExists ?? Directory.Exists;
        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim().Trim('"')));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return null;
        }

        foreach (var candidate in new[] { full, Path.Combine(full, PluginFolder), Path.Combine(full, ConfigsFolder, PluginFolder) })
        {
            if (exists(Path.Combine(candidate, CharactersFolder)))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Auto-detect: the Tsukimichi folders of the roaming folders directly under each of <paramref name="roots"/> (for
    /// example <c>%AppData%</c>, where <c>XIVLauncher</c>, <c>XIVLauncher2</c> and the like sit, and the folder above
    /// this client's own roaming folder), other than <paramref name="ownConfigDir"/>, each once, in name order.
    /// </summary>
    public static List<string> Candidates(string ownConfigDir, IEnumerable<string> roots, Func<string, IEnumerable<string>>? listDirectories = null, Func<string, bool>? directoryExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownConfigDir);
        ArgumentNullException.ThrowIfNull(roots);
        var list = listDirectories ?? SafeList;
        var exists = directoryExists ?? Directory.Exists;
        var own = Normal(ownConfigDir);
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            foreach (var roaming in list(root))
            {
                var config = Path.Combine(roaming, ConfigsFolder, PluginFolder);
                if (exists(Path.Combine(config, CharactersFolder)) && !string.Equals(Normal(config), own, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(Normal(config));
                }
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Folds one scan of <paramref name="folder"/> (its <see cref="FolderScan.Run"/> with <see cref="KnownStamps"/>):
    /// snapshots read again replace the old, removed ones go, heartbeats are kept to judge who is live.
    /// </summary>
    public void Take(string folder, FolderScanResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(result);
        known[folder] = new Dictionary<ulong, FileStamp>(result.Stamps);
        if (!saved.TryGetValue(folder, out var snapshots))
        {
            saved[folder] = snapshots = [];
        }

        foreach (var snapshot in result.Changed)
        {
            snapshots[snapshot.ContentId] = snapshot;
        }

        foreach (var id in result.Removed)
        {
            snapshots.Remove(id);
        }

        beats[folder] = [.. result.Heartbeats];
    }

    /// <summary>
    /// One pass over <paramref name="folders"/> (the linked folders, in order): forgets the unlinked ones, then folds each
    /// one's <paramref name="scan"/>. A folder whose scan says it is gone (null: its <c>characters</c> folder no longer
    /// exists) is forgotten with its characters. A folder whose scan throws keeps its last snapshots but loses its
    /// heartbeats, so none of its characters stays "Live" on a stale read; the other folders are scanned all the same.
    /// </summary>
    /// <param name="scan">Scans one folder (<see cref="FolderScan.Run"/> with <see cref="KnownStamps"/>); null when the folder is gone.</param>
    /// <param name="failed">Told of a folder whose scan threw; null tells no one.</param>
    public void ScanAll(IReadOnlyList<string> folders, Func<string, FolderScanResult?> scan, Action<string, Exception>? failed = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(scan);
        Keep(folders);
        foreach (var folder in folders)
        {
            try
            {
                if (scan(folder) is { } result)
                {
                    Take(folder, result);
                }
                else
                {
                    Forget(folder);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                beats.Remove(folder);
                failed?.Invoke(folder, ex);
            }
        }
    }

    /// <summary>The stamps the last scan of <paramref name="folder"/> returned (empty before the first), to pass as <c>known</c>.</summary>
    public IReadOnlyDictionary<ulong, FileStamp> KnownStamps(string folder) =>
        known.TryGetValue(folder, out var stamps) ? stamps : new Dictionary<ulong, FileStamp>();

    /// <summary>Forgets every folder not in <paramref name="folders"/> (a folder unlinked in Settings).</summary>
    public void Keep(IReadOnlyCollection<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var keep = new HashSet<string>(folders, StringComparer.OrdinalIgnoreCase);
        foreach (var folder in known.Keys.Where(f => !keep.Contains(f)).ToList())
        {
            Forget(folder);
        }
    }

    private void Forget(string folder)
    {
        known.Remove(folder);
        saved.Remove(folder);
        beats.Remove(folder);
    }

    /// <summary>
    /// The characters to show, in <paramref name="folders"/> order: each character once (the first folder listing it), none
    /// this client's own folder has (<paramref name="ownIds"/>), live while a fresh heartbeat of another game process
    /// holds it (<see cref="LiveClients.IsLiveElsewhere"/>).
    /// </summary>
    public List<LinkedCharacter> Characters(IReadOnlyList<string> folders, IReadOnlySet<ulong> ownIds, ClientIdentity me, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(ownIds);
        var seen = new HashSet<ulong>();
        var result = new List<LinkedCharacter>();
        foreach (var folder in folders)
        {
            if (!saved.TryGetValue(folder, out var snapshots))
            {
                continue;
            }

            var live = beats.TryGetValue(folder, out var heartbeats) ? LiveClients.LiveElsewhere(heartbeats, me, nowUtc) : new Dictionary<ulong, Heartbeat>();
            foreach (var snapshot in snapshots.Values.OrderBy(static s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(static s => s.ContentId))
            {
                if (ownIds.Contains(snapshot.ContentId) || !seen.Add(snapshot.ContentId))
                {
                    continue;
                }

                result.Add(new LinkedCharacter(folder, snapshot, live.ContainsKey(snapshot.ContentId)));
            }
        }

        return result;
    }

    private static string Normal(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private static IEnumerable<string> SafeList(string root)
    {
        try
        {
            return Directory.Exists(root) ? Directory.GetDirectories(root) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
