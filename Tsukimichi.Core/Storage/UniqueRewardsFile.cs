using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>Contents of the shipped <c>unique_quests.json</c>.</summary>
/// <param name="GameVersion">Game version the entries were generated from.</param>
/// <param name="GeneratedUtc">When the DataGen tool produced the file.</param>
public sealed record UniqueRewardsData(string GameVersion, DateTime GeneratedUtc, IReadOnlyList<UniqueRewardEntry> Entries)
{
    public static readonly UniqueRewardsData Empty = new(string.Empty, default, []);

    /// <summary>Problems met while loading. Never written to disk.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Single source of truth for the <c>unique_quests.json</c> shape, used by the plugin to read and by DataGen to write:
/// <code>
/// {
///   "gameVersion": "2026.09.20.0000.0000",
///   "generatedUtc": "2026-09-27T08:00:00Z",
///   "entries": [
///     { "questRowId": 66038, "kind": "Emote", "rewardId": 114, "itemId": 0,
///       "rewardName": "Most Gentlemanly", "confidence": "Static", "source": "Quest.EmoteReward" }
///   ]
/// }
/// </code>
/// Enums are serialized as their names.
/// </summary>
public static class UniqueRewardsFile
{
    /// <summary>
    /// Loads the file. Missing or unparseable → <see cref="UniqueRewardsData.Empty"/> plus a warning; a malformed entry is
    /// skipped with a warning while the rest load. The shipped file is never renamed or modified.
    /// </summary>
    public static UniqueRewardsData Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var warnings = new List<string>();
        var fileName = Path.GetFileName(path);

        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            warnings.Add(ioError is null
                ? $"{fileName} not found at {path}; no unique reward data is available."
                : $"{fileName} could not be read; no unique reward data is available: {ioError}");
            return UniqueRewardsData.Empty with { Warnings = warnings };
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) as JsonObject ?? throw new InvalidDataException("root is not a JSON object");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            warnings.Add($"{fileName} could not be parsed: {ex.Message}");
            return UniqueRewardsData.Empty with { Warnings = warnings };
        }

        var gameVersion = StorageJson.ReadString(root, "gameVersion") ?? string.Empty;
        if (!StorageJson.TryReadUtc(root, "generatedUtc", out var generated))
        {
            warnings.Add($"{fileName}: generatedUtc is not a valid timestamp; ignored.");
        }

        var entries = new List<UniqueRewardEntry>();
        if (root.TryGetPropertyValue("entries", out var entriesNode) && entriesNode is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var entry = ParseEntry(array[i], out var reason);
                if (entry is null)
                {
                    warnings.Add($"{fileName}: entries[{i}] skipped: {reason}");
                    continue;
                }

                entries.Add(entry);
            }
        }
        else if (entriesNode is not null)
        {
            warnings.Add($"{fileName}: entries is not an array; no entries loaded.");
        }

        return new UniqueRewardsData(gameVersion, generated ?? default, entries) { Warnings = warnings };
    }

    /// <summary>Writes the file atomically in the shape documented on this class.</summary>
    public static void Write(string path, UniqueRewardsData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        AtomicFile.Write(path, JsonSerializer.Serialize(data, StorageJson.Options));
    }

    private static UniqueRewardEntry? ParseEntry(JsonNode? node, out string reason)
    {
        if (node is not JsonObject)
        {
            reason = "not an object";
            return null;
        }

        UniqueRewardEntry? entry;
        try
        {
            entry = node.Deserialize<UniqueRewardEntry>(StorageJson.Options);
        }
        catch (JsonException ex)
        {
            reason = ex.Message;
            return null;
        }

        if (entry is null || entry.QuestRowId == 0)
        {
            reason = "questRowId missing or zero";
            return null;
        }

        reason = string.Empty;
        return entry with
        {
            RewardName = entry.RewardName ?? string.Empty,
            Source = entry.Source ?? string.Empty,
        };
    }
}
