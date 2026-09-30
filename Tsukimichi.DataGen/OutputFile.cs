using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Tsukimichi.Core.Model;

namespace Tsukimichi.DataGen;

/// <summary>
/// The unique_quests.json contract:
/// { "gameVersion", "generatedUtc", "entries": [ { questRowId, kind, rewardId, itemId, rewardName, confidence, source,
/// otherSources, otherSourceNotes? } ] } with enums serialized as strings; otherSourceNotes only where non-empty. The plugin's ShippedData loader reads exactly this shape.
/// </summary>
internal sealed class OutputFile
{
    public string GameVersion { get; init; } = string.Empty;
    public string GeneratedUtc { get; init; } = string.Empty;
    public List<UniqueRewardEntry> Entries { get; init; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } },
    };

    /// <summary>
    /// A property marked <see cref="OmitWhenEmptyAttribute"/> (<c>otherSourceNotes</c>) is left out while empty, as the
    /// plugin's own serializer does, so only the entries that carry a note grow the file.
    /// </summary>
    private static void OmitEmptyCollections(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider?.IsDefined(typeof(OmitWhenEmptyAttribute), inherit: false) == true)
            {
                property.ShouldSerialize = static (_, value) => value is ICollection collection ? collection.Count > 0 : value is not null;
            }
        }
    }

    public static void Write(string path, string gameVersion, DateTime generatedUtc, IEnumerable<UniqueRewardEntry> entries)
    {
        var file = new OutputFile
        {
            GameVersion = gameVersion,
            GeneratedUtc = generatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Entries = entries
                .OrderBy(e => e.QuestRowId)
                .ThenBy(e => e.Kind)
                .ThenBy(e => e.RewardId)
                .ToList(),
        };

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Temp file + rename so a crash never leaves a half-written data file behind.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, Options) + Environment.NewLine);
        File.Move(tmp, path, overwrite: true);
    }
}
