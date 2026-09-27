using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// Upgrades a snapshot's JSON from an older <c>schemaVersion</c> to <see cref="CharacterSnapshot.CurrentSchemaVersion"/>
/// before it is deserialized. Steps are registered per source version and chained; the registry is empty at v1.
/// </summary>
public sealed class SnapshotMigrator
{
    private const string VersionProperty = "schemaVersion";

    private readonly Dictionary<int, Func<JsonNode, JsonNode>> steps = [];

    /// <summary>Registers the step that turns a version-<paramref name="fromVersion"/> document into version <c>fromVersion + 1</c>.</summary>
    public void Register(int fromVersion, Func<JsonNode, JsonNode> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!steps.TryAdd(fromVersion, step))
        {
            throw new ArgumentException($"A migration from schema version {fromVersion} is already registered.", nameof(fromVersion));
        }
    }

    /// <summary>
    /// Applies every step from the document's version up to the current one and stamps the result.
    /// A missing version property is treated as version 1 (the first shipped schema).
    /// </summary>
    /// <exception cref="InvalidDataException">The root is not an object, the version is newer than this build, or a step is missing.</exception>
    public JsonNode Migrate(JsonNode root, out int fromVersion)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root is not JsonObject obj)
        {
            throw new InvalidDataException("Snapshot root must be a JSON object.");
        }

        fromVersion = ReadVersion(obj);
        if (fromVersion > CharacterSnapshot.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Snapshot schema version {fromVersion} is newer than this build supports ({CharacterSnapshot.CurrentSchemaVersion}).");
        }

        JsonNode current = obj;
        for (var version = fromVersion; version < CharacterSnapshot.CurrentSchemaVersion; version++)
        {
            if (!steps.TryGetValue(version, out var step))
            {
                throw new InvalidDataException($"No migration registered from snapshot schema version {version}.");
            }

            current = step(current) ?? throw new InvalidDataException($"Migration from schema version {version} returned null.");
            if (current is not JsonObject)
            {
                throw new InvalidDataException($"Migration from schema version {version} did not produce a JSON object.");
            }
        }

        SetVersion(current.AsObject(), CharacterSnapshot.CurrentSchemaVersion);
        return current;
    }

    private static int ReadVersion(JsonObject obj)
    {
        var key = FindVersionKey(obj);
        if (key is null)
        {
            return 1;
        }

        if (obj[key] is JsonValue value && value.TryGetValue<int>(out var version))
        {
            return version;
        }

        throw new InvalidDataException("Snapshot schemaVersion is not an integer.");
    }

    private static void SetVersion(JsonObject obj, int version)
    {
        var key = FindVersionKey(obj) ?? VersionProperty;
        obj[key] = version;
    }

    private static string? FindVersionKey(JsonObject obj)
    {
        foreach (var pair in obj)
        {
            if (string.Equals(pair.Key, VersionProperty, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }
}
