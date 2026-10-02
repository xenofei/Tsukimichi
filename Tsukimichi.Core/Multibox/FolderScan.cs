using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Multibox;

/// <summary>A snapshot file's last write time and size: when either moves, another client saved it.</summary>
public readonly record struct FileStamp(DateTime WriteUtc, long Length);

/// <summary>
/// The stamps of one character's accepted-time and abandoned sidecars (null when the file is absent): the owning
/// client writes them just after the snapshot, so a scan between the two sees the new snapshot with the old sidecars,
/// and their own change is how the next scan catches up (1.8.0, R7 F5).
/// </summary>
public readonly record struct SidecarStamp(FileStamp? Accepted, FileStamp? Abandoned);

/// <summary>What one look at the characters folder found.</summary>
/// <param name="Heartbeats">Every readable heartbeat, stale ones included.</param>
/// <param name="Stamps">Every snapshot file's stamp now, to pass as <c>known</c> to the next scan.</param>
/// <param name="Changed">Snapshots new or saved since the last scan (read in full), other than the skipped ones.</param>
/// <param name="Removed">Characters whose snapshot file is gone since the last scan.</param>
/// <param name="Warnings">Problems reading, for the caller to log.</param>
/// <param name="Problems">
/// Characters whose snapshot was read this pass and could not be taken in: <see cref="SharedLoad.Newer"/> (a newer
/// plugin wrote it) or <see cref="SharedLoad.Invalid"/>. What this client shows for them stops updating until the file
/// reads again; the lists say so (1.8.0, R7 G).
/// </param>
/// <param name="SidecarStamps">Every character's sidecar stamps now, to pass as <c>knownSidecars</c> to the next scan.</param>
/// <param name="SidecarsChanged">
/// Characters seen by the previous scan whose sidecars changed while their snapshot did not (the owner wrote them just
/// after it): their accepted times and abandoned quests are read again.
/// </param>
public sealed record FolderScanResult(
    IReadOnlyList<Heartbeat> Heartbeats,
    IReadOnlyDictionary<ulong, FileStamp> Stamps,
    IReadOnlyList<CharacterSnapshot> Changed,
    IReadOnlyList<ulong> Removed,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<ulong, SharedLoad>? Problems = null,
    IReadOnlyDictionary<ulong, SidecarStamp>? SidecarStamps = null,
    IReadOnlyList<ulong>? SidecarsChanged = null);

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
    /// <param name="knownSidecars">The sidecar stamps the previous pass returned; null or empty the first time.</param>
    public static FolderScanResult Run(string charactersDir, IReadOnlyDictionary<ulong, FileStamp> known, JsonSnapshotStore store, IReadOnlySet<ulong> skip, IReadOnlyDictionary<ulong, SidecarStamp>? knownSidecars = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(skip);

        var heartbeats = HeartbeatFile.ReadAll(charactersDir);
        var stamps = new Dictionary<ulong, FileStamp>();
        var changed = new List<CharacterSnapshot>();
        var warnings = new List<string>();
        var problems = new Dictionary<ulong, SharedLoad>();
        var sidecars = new Dictionary<ulong, SidecarStamp>();

        if (Directory.Exists(charactersDir))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(charactersDir, "*.json"))
                {
                    var stem = Path.GetFileNameWithoutExtension(path);
                    if (!ulong.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var contentId))
                    {
                        RecordSidecar(path, sidecars);
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
                            problems[contentId] = read.Status;
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

        // A sidecar that moved under a snapshot that did not (the owner writes the sidecars just after the snapshot).
        var sidecarsChanged = new List<ulong>();
        var changedIds = changed.Select(static s => s.ContentId).ToHashSet();
        foreach (var id in stamps.Keys)
        {
            if (!known.ContainsKey(id) || skip.Contains(id) || changedIds.Contains(id))
            {
                continue;
            }

            var before = knownSidecars is not null && knownSidecars.TryGetValue(id, out var was) ? was : default;
            var now = sidecars.TryGetValue(id, out var current) ? current : default;
            if (before != now)
            {
                sidecarsChanged.Add(id);
            }
        }

        return new FolderScanResult(heartbeats, stamps, changed, removed, warnings, problems, sidecars, sidecarsChanged);
    }

    /// <summary>Records the stamp of an accepted-time or abandoned sidecar; any other file is ignored.</summary>
    private static void RecordSidecar(string path, Dictionary<ulong, SidecarStamp> sidecars)
    {
        var name = Path.GetFileName(path);
        var accepted = name.EndsWith(AcceptedSince.FileSuffix, StringComparison.OrdinalIgnoreCase);
        if (!accepted && !name.EndsWith(AbandonedLedger.FileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var suffix = accepted ? AcceptedSince.FileSuffix : AbandonedLedger.FileSuffix;
        if (!ulong.TryParse(name.AsSpan(0, name.Length - suffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return;
        }

        FileStamp stamp;
        try
        {
            var info = new FileInfo(path);
            stamp = new FileStamp(info.LastWriteTimeUtc, info.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        sidecars.TryGetValue(id, out var entry);
        sidecars[id] = accepted ? entry with { Accepted = stamp } : entry with { Abandoned = stamp };
    }
}
