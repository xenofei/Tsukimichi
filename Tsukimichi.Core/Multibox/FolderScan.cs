using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Multibox;

/// <summary>A snapshot file's last write time and size: when either moves, another client saved it.</summary>
public readonly record struct FileStamp(DateTime WriteUtc, long Length);

/// <summary>What one look at the characters folder found.</summary>
/// <param name="Heartbeats">Every readable heartbeat, stale ones included.</param>
/// <param name="Stamps">Every snapshot file's stamp now, to pass as <c>known</c> to the next scan.</param>
/// <param name="Changed">Snapshots new or saved since the last scan (read in full), other than the skipped ones.</param>
/// <param name="Removed">Characters whose snapshot file is gone since the last scan.</param>
/// <param name="Warnings">Problems reading, for the caller to log.</param>
public sealed record FolderScanResult(
    IReadOnlyList<Heartbeat> Heartbeats,
    IReadOnlyDictionary<ulong, FileStamp> Stamps,
    IReadOnlyList<CharacterSnapshot> Changed,
    IReadOnlyList<ulong> Removed,
    IReadOnlyList<string> Warnings);

/// <summary>
/// One pass over <c>characters/</c> for multibox sharing (D11): the heartbeats, plus every snapshot another client
/// saved since the last pass, read through the snapshot store. Runs on a worker; it only reads files, and a
/// snapshot being renamed into place meanwhile is simply read on the next pass. It never quarantines: a file a newer
/// plugin wrote, or one it cannot parse, is skipped with a warning (<see cref="JsonSnapshotStore.LoadShared"/>) and
/// read again only once it changes.
/// </summary>
public static class FolderScan
{
    /// <param name="charactersDir">The characters folder.</param>
    /// <param name="known">The stamps the previous pass returned (empty the first time: every snapshot reads as changed).</param>
    /// <param name="store">A store over the same folder, used by this worker only.</param>
    /// <param name="skip">Characters never read here (this client's own logged-in one, whose copy in memory is newer); their stamps are still recorded.</param>
    public static FolderScanResult Run(string charactersDir, IReadOnlyDictionary<ulong, FileStamp> known, JsonSnapshotStore store, IReadOnlySet<ulong> skip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(skip);

        var heartbeats = HeartbeatFile.ReadAll(charactersDir);
        var stamps = new Dictionary<ulong, FileStamp>();
        var changed = new List<CharacterSnapshot>();
        var warnings = new List<string>();

        if (Directory.Exists(charactersDir))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(charactersDir, "*.json"))
                {
                    var stem = Path.GetFileNameWithoutExtension(path);
                    if (!ulong.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var contentId))
                    {
                        continue;
                    }

                    FileStamp stamp;
                    try
                    {
                        var info = new FileInfo(path);
                        stamp = new FileStamp(info.LastWriteTimeUtc, info.Length);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    if (skip.Contains(contentId) || (known.TryGetValue(contentId, out var before) && before == stamp))
                    {
                        stamps[contentId] = stamp;
                        continue;
                    }

                    // Another client's file: read without quarantining anything, whatever it holds.
                    var read = store.LoadShared(contentId);
                    switch (read.Status)
                    {
                        case SharedLoad.Loaded:
                            stamps[contentId] = stamp;
                            changed.Add(read.Value!);
                            break;

                        case SharedLoad.Newer or SharedLoad.Invalid:
                            // Skipped, and not read again until it changes: its owner (a newer plugin, or one that will
                            // save over the damage) is the one to deal with it.
                            stamps[contentId] = stamp;
                            warnings.Add(read.Problem!);
                            break;

                        case SharedLoad.Unreadable:
                            // Not recorded: a file locked for a moment is read again on the next pass.
                            warnings.Add(read.Problem!);
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not list {charactersDir}: {ex.Message}");
                // A failed listing says nothing about what was removed.
                foreach (var (id, stamp) in known)
                {
                    stamps.TryAdd(id, stamp);
                }
            }
        }

        var removed = new List<ulong>();
        foreach (var id in known.Keys)
        {
            if (!stamps.ContainsKey(id) && !File.Exists(Path.Combine(charactersDir, id.ToString(CultureInfo.InvariantCulture) + ".json")))
            {
                removed.Add(id);
            }
        }

        return new FolderScanResult(heartbeats, stamps, changed, removed, warnings);
    }
}
