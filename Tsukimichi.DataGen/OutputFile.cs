using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.DataGen;

/// <summary>
/// The unique_quests.json contract:
/// { "gameVersion", "generatedUtc", "entries": [ { questRowId, kind, rewardId, itemId, rewardName, confidence, source } ] }
/// with enums serialized as strings. The plugin's ShippedData loader reads exactly this shape.
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
    };

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
