using System.Text.Json;

namespace Tsukimichi.Core.Storage;

/// <summary>A user's verdict on whether a quest's reward is unique, overriding shipped data.</summary>
/// <param name="MarkedUtc">When the verdict was given; null for verdicts stored before 0.5.1.</param>
public sealed record UniqueOverride(bool Unique, string? Note, DateTime? MarkedUtc = null);

/// <summary>What one pin edit did: pinned or unpinned one quest, or dropped every pin of a forgotten character.</summary>
public enum PinChangeKind
{
    Pin,
    Unpin,
    Forget,
}

/// <summary>
/// One pin edit made in this game client (D11). Saves apply edits to the file as it is on disk, one quest at a time,
/// so two clients editing the same character's pins keep both edits (one unpins a quest while the other pins another).
/// </summary>
/// <param name="RowId">The quest row id; unused for <see cref="PinChangeKind.Forget"/>.</param>
public readonly record struct PinChange(ulong ContentId, uint RowId, PinChangeKind Kind);

/// <summary>
/// <c>user/pins.json</c>: content id to the quest row ids that character has pinned, oldest pin first.
/// Shape: <c>{ "&lt;contentId&gt;": [ 66038, 65576 ] }</c>. The array's order is the order the quests were pinned (the
/// Todo overlay lists them so, and "Pin all" appends a route's steps in route order); every reader and merge here keeps
/// it, and a new pin is appended. The shape is unchanged, so files of earlier versions load as they are.
/// </summary>
public static class PinsFile
{
    /// <summary>
    /// Loads pins; a missing file yields an empty map, an unreadable one is quarantined and reported in
    /// <paramref name="warnings"/>. A character whose list is null (<c>"123": null</c> in a hand-edited file) has no
    /// entry, so every reader can take a list it finds as it is.
    /// </summary>
    public static Dictionary<ulong, List<uint>> Load(string path, IList<string>? warnings = null)
    {
        var pins = UserFile.Load<Dictionary<ulong, List<uint>>>(path, warnings) ?? [];
        DropNull(pins);
        return pins;
    }

    /// <summary>
    /// Reads pins for a merge into memory; never quarantines. Only <see cref="SharedLoad.Loaded"/> is worth adopting.
    /// Null lists are dropped as in <see cref="Load"/>.
    /// </summary>
    public static SharedRead<Dictionary<ulong, List<uint>>> LoadShared(string path)
    {
        var read = UserFile.LoadShared<Dictionary<ulong, List<uint>>>(path);
        if (read.Value is { } pins)
        {
            DropNull(pins);
        }

        return read;
    }

    public static void Save(string path, IReadOnlyDictionary<ulong, List<uint>> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        AtomicFile.Write(path, JsonSerializer.Serialize(pins, StorageJson.Options));
    }

    /// <summary>A deep copy, safe to hand to the background writer while the original keeps changing.</summary>
    public static Dictionary<ulong, List<uint>> Copy(IReadOnlyDictionary<ulong, List<uint>> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        var copy = new Dictionary<ulong, List<uint>>(pins.Count);
        foreach (var (key, list) in pins)
        {
            copy[key] = list is null ? [] : [.. list];
        }

        return copy;
    }

    /// <summary>
    /// Applies <paramref name="changes"/> in order to <paramref name="pins"/> as set edits: a pin appends a quest not
    /// pinned yet, an unpin removes it, a forget drops the character. Characters left with no pin are removed.
    /// Applying the same edits twice changes nothing more.
    /// </summary>
    public static void Apply(Dictionary<ulong, List<uint>> pins, IEnumerable<PinChange> changes)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case PinChangeKind.Pin:
                    if (!pins.TryGetValue(change.ContentId, out var list) || list is null)
                    {
                        list = [];
                        pins[change.ContentId] = list;
                    }

                    if (!list.Contains(change.RowId))
                    {
                        list.Add(change.RowId);
                    }

                    break;

                case PinChangeKind.Unpin:
                    if (pins.TryGetValue(change.ContentId, out var held) && held is not null)
                    {
                        held.Remove(change.RowId);
                    }

                    break;

                case PinChangeKind.Forget:
                    pins.Remove(change.ContentId);
                    break;
            }
        }

        DropEmpty(pins);
    }

    /// <summary>
    /// Saves pin edits made in this client without losing any other client's (D11): under <see cref="SharedFile.Lock"/>
    /// the file is read again, <paramref name="changes"/> are applied to it quest by quest (<see cref="Apply"/>), and
    /// the result is written and returned for the caller to adopt. A file that exists but cannot be read throws rather
    /// than being replaced; one that cannot be parsed is quarantined and <paramref name="fallback"/> (this client's own
    /// map, edits included) stands in for it, so the other characters' pins are not lost with it.
    /// </summary>
    public static Dictionary<ulong, List<uint>> SaveChanges(
        string path,
        IReadOnlyList<PinChange> changes,
        IReadOnlyDictionary<ulong, List<uint>> fallback,
        IList<string>? warnings = null,
        TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(fallback);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = UserFile.LoadForMerge<Dictionary<ulong, List<uint>>>(path, warnings, out var invalid);
            var merged = invalid ? Copy(fallback) : Copy(disk ?? []);
            Apply(merged, changes);
            Save(path, merged);
            return merged;
        }
    }

    /// <summary>
    /// Saves the pins of the characters in <paramref name="touched"/> without losing what another game client saved for
    /// the others (D11): under <see cref="SharedFile.Lock"/> the file is read again, <see cref="KeyedMerge.Apply"/> takes
    /// the touched characters' lists from <paramref name="local"/>, empty lists are dropped, and the result is written
    /// and returned for the caller to adopt. A file that exists but cannot be read throws rather than being replaced;
    /// one that cannot be parsed is quarantined and <paramref name="local"/> stands in for it.
    /// </summary>
    public static Dictionary<ulong, List<uint>> SaveMerged(
        string path,
        IReadOnlyDictionary<ulong, List<uint>> local,
        IEnumerable<ulong> touched,
        IList<string>? warnings = null,
        TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(touched);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = UserFile.LoadForMerge<Dictionary<ulong, List<uint>>>(path, warnings, out var invalid);
            var merged = KeyedMerge.Apply(invalid ? local : disk ?? [], local, touched);
            DropEmpty(merged);
            Save(path, merged);
            return merged;
        }
    }

    /// <summary>
    /// "Delete all data" (D11): under <see cref="SharedFile.Lock"/>, rewrites the file with only the characters
    /// <paramref name="keep"/> selects (those live in another game client, whose pins that client still shows), so a
    /// merge in flight elsewhere cannot bring the rest back and the others' pins survive. Returns what was kept.
    /// </summary>
    public static Dictionary<ulong, List<uint>> KeepOnly(string path, Func<ulong, bool> keep, IList<string>? warnings = null, TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(keep);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = UserFile.LoadForMerge<Dictionary<ulong, List<uint>>>(path, warnings, out _) ?? [];
            var kept = new Dictionary<ulong, List<uint>>();
            foreach (var (id, list) in disk)
            {
                if (keep(id) && list is { Count: > 0 })
                {
                    kept[id] = list;
                }
            }

            Save(path, kept);
            return kept;
        }
    }

    private static void DropNull(Dictionary<ulong, List<uint>> pins)
    {
        foreach (var key in pins.Where(static p => p.Value is null).Select(static p => p.Key).ToList())
        {
            pins.Remove(key);
        }
    }

    private static void DropEmpty(Dictionary<ulong, List<uint>> pins)
    {
        foreach (var key in pins.Where(static p => p.Value is null || p.Value.Count == 0).Select(static p => p.Key).ToList())
        {
            pins.Remove(key);
        }
    }
}

/// <summary>
/// <c>user/overrides.json</c>: quest row id to the user's unique/not-unique override.
/// Shape: <c>{ "&lt;rowId&gt;": { "unique": true, "note": "...", "markedUtc": "2026-09-28T10:00:00Z" } }</c>; <c>note</c>
/// and <c>markedUtc</c> may be null or absent.
/// </summary>
public static class OverridesFile
{
    /// <summary>Loads overrides; a missing file yields an empty map, an unreadable one is quarantined and reported in <paramref name="warnings"/>.</summary>
    public static Dictionary<uint, UniqueOverride> Load(string path, IList<string>? warnings = null) =>
        UserFile.Load<Dictionary<uint, UniqueOverride>>(path, warnings) ?? [];

    /// <summary>Reads overrides for a merge into memory; never quarantines. Only <see cref="SharedLoad.Loaded"/> is worth adopting.</summary>
    public static SharedRead<Dictionary<uint, UniqueOverride>> LoadShared(string path) =>
        UserFile.LoadShared<Dictionary<uint, UniqueOverride>>(path);

    public static void Save(string path, IReadOnlyDictionary<uint, UniqueOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        AtomicFile.Write(path, JsonSerializer.Serialize(overrides, StorageJson.Options));
    }

    /// <summary>
    /// Saves the verdicts of the quests in <paramref name="touched"/> without losing verdicts another game client saved
    /// for other quests (D11): read again under <see cref="SharedFile.Lock"/>, merged with <see cref="KeyedMerge.Apply"/>,
    /// written, and returned for the caller to adopt. A file that exists but cannot be read throws rather than being
    /// replaced; one that cannot be parsed is quarantined and <paramref name="local"/> stands in for it.
    /// </summary>
    public static Dictionary<uint, UniqueOverride> SaveMerged(
        string path,
        IReadOnlyDictionary<uint, UniqueOverride> local,
        IEnumerable<uint> touched,
        IList<string>? warnings = null,
        TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(touched);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = UserFile.LoadForMerge<Dictionary<uint, UniqueOverride>>(path, warnings, out var invalid);
            var merged = KeyedMerge.Apply(invalid ? local : disk ?? [], local, touched);
            Save(path, merged);
            return merged;
        }
    }

    /// <summary>"Delete all data" (D11): writes an empty file under <see cref="SharedFile.Lock"/>, so a merge in flight elsewhere cannot bring the verdicts back.</summary>
    public static void Clear(string path, TimeSpan? lockTimeout = null)
    {
        using (SharedFile.Lock(path, lockTimeout))
        {
            Save(path, new Dictionary<uint, UniqueOverride>());
        }
    }
}

internal static class UserFile
{
    /// <summary>
    /// Reads and deserializes a user-owned file. Missing → null silently; unreadable (locked, permissions) → warned,
    /// left in place, null; corrupt → quarantined, warned, null.
    /// </summary>
    public static T? Load<T>(string path, IList<string>? warnings) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings?.Add($"{fileName} could not be read and was left in place: {ioError}");
            }

            return null;
        }

        return Parse<T>(path, text, warnings, out _);
    }

    /// <summary>Reads and deserializes a file another game client may be saving; never quarantines or warns (the result says what happened).</summary>
    public static SharedRead<T> LoadShared<T>(string path) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            return ioError is null
                ? SharedRead<T>.Missing
                : new SharedRead<T>(SharedLoad.Unreadable, null, $"{fileName} could not be read and was left in place: {ioError}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(text, StorageJson.Options) is { } value
                ? SharedRead<T>.Of(value)
                : new SharedRead<T>(SharedLoad.Invalid, null, $"{fileName} holds no data and was left in place.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return new SharedRead<T>(SharedLoad.Invalid, null, $"{fileName} could not be parsed and was left in place: {ex.Message}");
        }
    }

    /// <summary>
    /// <see cref="Load{T}"/> for a read-merge-write: a file that exists but cannot be read throws
    /// <see cref="IOException"/>, since saving over it would lose what it holds. Missing → null; corrupt → quarantined,
    /// null, and <paramref name="invalid"/> true so the caller merges onto its own copy rather than onto nothing.
    /// </summary>
    public static T? LoadForMerge<T>(string path, IList<string>? warnings, out bool invalid) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (ioError is not null)
        {
            throw new IOException($"{Path.GetFileName(path)} could not be read, so it was not saved over: {ioError}");
        }

        invalid = false;
        return text is null ? null : Parse<T>(path, text, warnings, out invalid);
    }

    private static T? Parse<T>(string path, string text, IList<string>? warnings, out bool invalid) where T : class
    {
        var fileName = Path.GetFileName(path);
        invalid = false;
        try
        {
            return JsonSerializer.Deserialize<T>(text, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            invalid = true;
            if (AtomicFile.TryQuarantine(path, out var moved, out var quarantineError))
            {
                warnings?.Add($"{fileName} could not be read and was moved to {Path.GetFileName(moved)}: {ex.Message}");
            }
            else
            {
                warnings?.Add($"{fileName} could not be read ({ex.Message}) and could not be quarantined: {quarantineError}");
            }

            return null;
        }
    }
}
