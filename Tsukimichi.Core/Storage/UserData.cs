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
