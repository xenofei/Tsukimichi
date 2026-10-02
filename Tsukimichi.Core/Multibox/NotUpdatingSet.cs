using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Multibox;

/// <summary>
/// The characters whose snapshot file this client cannot read (1.8.0, R7 G: a newer plugin's, or one that does not
/// parse), kept across multibox scans, and how the lists show them. A scan reports such a file only when it changed, so
/// the mark stays until the file reads (another client saved it), goes away, or this client saves over it itself. The
/// character logged in here is never shown as "not updating": what is shown for it is live, not its file.
/// </summary>
public static class NotUpdatingSet
{
    private static readonly IReadOnlyDictionary<ulong, SharedLoad> None = new Dictionary<ulong, SharedLoad>();

    /// <summary>
    /// Folds one scan into <paramref name="notUpdating"/>: a snapshot read in full, a file gone, or a save this client
    /// made itself (<paramref name="savedHere"/>, the character logged in here: the scan never reads its file, so only
    /// that save says it reads again) clears the mark; a file the scan could not take in sets it. True when the set changed.
    /// </summary>
    public static bool Track(Dictionary<ulong, SharedLoad> notUpdating, FolderScanResult result, IEnumerable<ulong> savedHere)
    {
        ArgumentNullException.ThrowIfNull(notUpdating);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(savedHere);
        var changed = false;
        foreach (var id in savedHere)
        {
            changed |= notUpdating.Remove(id);
        }

        foreach (var snapshot in result.Changed)
        {
            changed |= notUpdating.Remove(snapshot.ContentId);
        }

        foreach (var id in notUpdating.Keys.Where(id => !result.Stamps.ContainsKey(id)).ToList())
        {
            changed |= notUpdating.Remove(id);
        }

        foreach (var (id, status) in result.Problems ?? None)
        {
            if (!notUpdating.TryGetValue(id, out var was) || was != status)
            {
                notUpdating[id] = status;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// What the lists show: <paramref name="all"/> without the character logged in here (<paramref name="liveHere"/>).
    /// <paramref name="all"/> itself when it does not name that character.
    /// </summary>
    public static IReadOnlyDictionary<ulong, SharedLoad> Shown(IReadOnlyDictionary<ulong, SharedLoad> all, ulong? liveHere)
    {
        ArgumentNullException.ThrowIfNull(all);
        if (liveHere is not { } live || !all.ContainsKey(live))
        {
            return all;
        }

        var shown = new Dictionary<ulong, SharedLoad>(all.Count);
        foreach (var (id, status) in all)
        {
            if (id != live)
            {
                shown[id] = status;
            }
        }

        return shown;
    }

    /// <summary>Whether one character reads the same in both sets: absent from both, or present with the same reason.</summary>
    public static bool SameFor(IReadOnlyDictionary<ulong, SharedLoad> a, IReadOnlyDictionary<ulong, SharedLoad> b, ulong contentId)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var inA = a.TryGetValue(contentId, out var x);
        var inB = b.TryGetValue(contentId, out var y);
        return inA == inB && (!inA || x == y);
    }

    /// <summary>The same characters with the same reasons.</summary>
    public static bool Same(IReadOnlyDictionary<ulong, SharedLoad> a, IReadOnlyDictionary<ulong, SharedLoad> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var was) && was == p.Value);
    }
}
