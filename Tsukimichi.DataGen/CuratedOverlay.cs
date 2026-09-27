using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

namespace Tsukimichi.DataGen;

/// <summary>
/// Reads the curated JSON overlay (spec section 6): system_unlocks.json (questId -> { label, ... }) and
/// duty_unlocks.json (questId -> contentFinderConditionId[]). Parsing is tolerant: unknown shapes are skipped with a warning.
/// </summary>
internal static class CuratedOverlay
{
    public static int Apply(string? curatedDir, GameSheets sheets, UniqueRewardGenerator generator, TextWriter log)
    {
        if (curatedDir is null)
        {
            log.WriteLine("curated: no --curated directory given; skipping overlay.");
            return 0;
        }
        if (!Directory.Exists(curatedDir))
        {
            log.WriteLine($"curated: directory '{curatedDir}' does not exist; skipping overlay.");
            return 0;
        }

        var applied = 0;
        applied += ApplySystemUnlocks(Path.Combine(curatedDir, "system_unlocks.json"), generator, log);
        applied += ApplyDutyUnlocks(Path.Combine(curatedDir, "duty_unlocks.json"), sheets, generator, log);
        return applied;
    }

    private static int ApplySystemUnlocks(string path, UniqueRewardGenerator generator, TextWriter log)
    {
        var root = LoadObject(path, log);
        if (root is null)
            return 0;

        var count = 0;
        foreach (var (key, value) in root)
        {
            if (!uint.TryParse(key, out var questId))
            {
                log.WriteLine($"curated: system_unlocks key '{key}' is not a quest id; skipped.");
                continue;
            }

            string? label = value switch
            {
                JsonValue v when v.TryGetValue<string>(out var s) => s,
                JsonObject o => (o["label"] ?? o["name"])?.GetValue<string>(),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(label))
            {
                log.WriteLine($"curated: system_unlocks entry {questId} has no label; skipped.");
                continue;
            }

            generator.AddCurated(new UniqueRewardEntry(questId, RewardKind.SystemUnlock, 0, 0, label.Trim(), Confidence.Curated, "curated/system_unlocks.json"));
            count++;
        }

        log.WriteLine($"curated: system_unlocks.json applied {count} entries.");
        return count;
    }

    private static int ApplyDutyUnlocks(string path, GameSheets sheets, UniqueRewardGenerator generator, TextWriter log)
    {
        var root = LoadObject(path, log);
        if (root is null)
            return 0;

        var count = 0;
        foreach (var (key, value) in root)
        {
            if (!uint.TryParse(key, out var questId))
            {
                log.WriteLine($"curated: duty_unlocks key '{key}' is not a quest id; skipped.");
                continue;
            }

            var ids = ExtractIds(value);
            if (ids.Count == 0)
            {
                log.WriteLine($"curated: duty_unlocks entry {questId} has no content finder condition ids; skipped.");
                continue;
            }

            foreach (var cfcId in ids)
            {
                var cfc = sheets.ContentFinderConditions.GetRowOrDefault(cfcId);
                var name = cfc is { } c ? UniqueRewardGenerator.Text(c.Name) : string.Empty;
                if (name.Length == 0)
                    name = $"Duty {cfcId}";
                generator.AddCurated(new UniqueRewardEntry(questId, RewardKind.DutyUnlock, cfcId, 0, name, Confidence.Curated, "curated/duty_unlocks.json"));
                count++;
            }
        }

        log.WriteLine($"curated: duty_unlocks.json applied {count} entries.");
        return count;
    }

    /// <summary>Accepts a bare number, an array of numbers, or an object whose first numeric-array property lists the ids.</summary>
    private static List<uint> ExtractIds(JsonNode? value)
    {
        var ids = new List<uint>();
        switch (value)
        {
            case JsonValue v when v.TryGetValue<uint>(out var single):
                ids.Add(single);
                break;
            case JsonArray arr:
                foreach (var n in arr)
                {
                    if (n is JsonValue nv && nv.TryGetValue<uint>(out var id))
                        ids.Add(id);
                }
                break;
            case JsonObject obj:
                foreach (var (_, prop) in obj)
                {
                    if (prop is JsonArray)
                    {
                        ids = ExtractIds(prop);
                        if (ids.Count > 0)
                            break;
                    }
                }
                break;
        }
        return ids;
    }

    private static JsonObject? LoadObject(string path, TextWriter log)
    {
        if (!File.Exists(path))
        {
            log.WriteLine($"curated: {Path.GetFileName(path)} not found; skipped.");
            return null;
        }
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (node is JsonObject obj)
                return obj;
            log.WriteLine($"curated: {Path.GetFileName(path)} is not a JSON object; skipped.");
            return null;
        }
        catch (JsonException ex)
        {
            log.WriteLine($"curated: {Path.GetFileName(path)} failed to parse ({ex.Message}); skipped.");
            return null;
        }
    }
}
