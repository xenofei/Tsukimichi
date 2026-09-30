using System.Text.Json;

namespace Tsukimichi.Core.Storage;

/// <summary>A user's verdict on whether a quest's reward is unique, overriding shipped data.</summary>
/// <param name="MarkedUtc">When the verdict was given; null for verdicts stored before 0.5.1.</param>
public sealed record UniqueOverride(bool Unique, string? Note, DateTime? MarkedUtc = null);

/// <summary>
/// <c>user/pins.json</c>: content id to the quest row ids that character has pinned.
/// Shape: <c>{ "&lt;contentId&gt;": [ 66038, 65576 ] }</c>.
/// </summary>
public static class PinsFile
{
    /// <summary>Loads pins; a missing file yields an empty map, an unreadable one is quarantined and reported in <paramref name="warnings"/>.</summary>
    public static Dictionary<ulong, List<uint>> Load(string path, IList<string>? warnings = null) =>
        UserFile.Load<Dictionary<ulong, List<uint>>>(path, warnings) ?? [];

    public static void Save(string path, IReadOnlyDictionary<ulong, List<uint>> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        AtomicFile.Write(path, JsonSerializer.Serialize(pins, StorageJson.Options));
    }

    /// <summary>
    /// Saves the pins of the characters in <paramref name="touched"/> without losing what another game client saved for
    /// the others (D11): under <see cref="SharedFile.Lock"/> the file is read again, <see cref="KeyedMerge.Apply"/> takes
    /// the touched characters' lists from <paramref name="local"/>, empty lists are dropped, and the result is written
    /// and returned for the caller to adopt. A file that exists but cannot be read throws rather than being replaced.
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
            var disk = UserFile.LoadForMerge<Dictionary<ulong, List<uint>>>(path, warnings) ?? [];
            var merged = KeyedMerge.Apply(disk, local, touched);
            foreach (var key in merged.Where(static p => p.Value is null || p.Value.Count == 0).Select(static p => p.Key).ToList())
            {
                merged.Remove(key);
            }

            Save(path, merged);
            return merged;
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

    public static void Save(string path, IReadOnlyDictionary<uint, UniqueOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        AtomicFile.Write(path, JsonSerializer.Serialize(overrides, StorageJson.Options));
    }

    /// <summary>
    /// Saves the verdicts of the quests in <paramref name="touched"/> without losing verdicts another game client saved
    /// for other quests (D11): read again under <see cref="SharedFile.Lock"/>, merged with <see cref="KeyedMerge.Apply"/>,
    /// written, and returned for the caller to adopt. A file that exists but cannot be read throws rather than being replaced.
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
            var disk = UserFile.LoadForMerge<Dictionary<uint, UniqueOverride>>(path, warnings) ?? [];
            var merged = KeyedMerge.Apply(disk, local, touched);
            Save(path, merged);
            return merged;
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

        return Parse<T>(path, text, warnings);
    }

    /// <summary>
    /// <see cref="Load{T}"/> for a read-merge-write: a file that exists but cannot be read throws
    /// <see cref="IOException"/>, since saving over it would lose what it holds. Missing → null; corrupt → quarantined, null.
    /// </summary>
    public static T? LoadForMerge<T>(string path, IList<string>? warnings) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (ioError is not null)
        {
            throw new IOException($"{Path.GetFileName(path)} could not be read, so it was not saved over: {ioError}");
        }

        return text is null ? null : Parse<T>(path, text, warnings);
    }

    private static T? Parse<T>(string path, string text, IList<string>? warnings) where T : class
    {
        var fileName = Path.GetFileName(path);
        try
        {
            return JsonSerializer.Deserialize<T>(text, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
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
