using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.DataGen;

/// <summary>
/// Reads the curated JSON overlay (spec section 6): system_unlocks.json (questId -> { label, ... }),
/// duty_unlocks.json (questId -> contentFinderConditionId[]) and online_store.json (itemId -> { name, kind, rewardId,
/// evidence }). Files must be strict JSON (the curated README's rule); within a file, unknown shapes are skipped with
/// a warning.
/// </summary>
internal static class CuratedOverlay
{
    public const string OnlineStoreFileName = "online_store.json";
    private const int OnlineStoreSchema = 1;

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
        applied += ApplyOnlineStore(Path.Combine(curatedDir, OnlineStoreFileName), sheets, generator, log);
        return applied;
    }

    /// <summary>
    /// online_store.json: <c>{ "schema": 1, "note", "entries": { "&lt;itemId&gt;": { name, kind, rewardId, evidence, note? } } }</c>.
    /// Each store item is resolved through its ItemAction to the collectible it unlocks and must agree with the declared
    /// kind and reward id; every entry delivered as that item or granting that collectible gains
    /// <see cref="OtherSource.OnlineStore"/>. Returns the number of entries marked.
    /// </summary>
    private static int ApplyOnlineStore(string path, GameSheets sheets, UniqueRewardGenerator generator, TextWriter log)
    {
        var root = LoadObject(path, log);
        if (root is null)
            return 0;

        if (root["schema"] is not JsonValue schemaNode || !schemaNode.TryGetValue<int>(out var schema) || schema != OnlineStoreSchema)
        {
            log.WriteLine($"curated: {OnlineStoreFileName} schema is not {OnlineStoreSchema}; file skipped.");
            return 0;
        }

        if (root["entries"] is not JsonObject entries)
        {
            log.WriteLine($"curated: {OnlineStoreFileName} has no entries object; file skipped.");
            return 0;
        }

        var marked = 0;
        var items = 0;
        var unmatched = new List<string>();
        foreach (var (key, value) in entries)
        {
            if (!uint.TryParse(key, out var itemId) || itemId == 0)
            {
                log.WriteLine($"curated: online_store key '{key}' is not an item id; skipped.");
                continue;
            }

            if (value is not JsonObject obj)
            {
                log.WriteLine($"curated: online_store entry {itemId} is not an object; skipped.");
                continue;
            }

            // Read like the runtime loader (CuratedData): a non-string kind or evidence, or a numeric kind, is a
            // warning and a skipped entry, never an exception out of the generator.
            var kindText = ReadString(obj, "kind");
            var rewardId = obj["rewardId"] is JsonValue r && r.TryGetValue<uint>(out var rid) ? rid : 0;
            var evidence = ReadString(obj, "evidence");
            if (kindText is null || !Enum.TryParse<RewardKind>(kindText, ignoreCase: false, out var kind) || !Enum.IsDefined(kind))
            {
                log.WriteLine($"curated: online_store entry {itemId} kind '{kindText}' is not a RewardKind name; skipped.");
                continue;
            }

            if (rewardId == 0 || string.IsNullOrWhiteSpace(evidence))
            {
                log.WriteLine($"curated: online_store entry {itemId} needs rewardId and evidence; skipped.");
                continue;
            }

            if (sheets.Items.GetRowOrDefault(itemId) is not { } item || UniqueRewardGenerator.Text(item.Name).Length == 0)
            {
                log.WriteLine($"curated: online_store entry {itemId} is not a named Item row; skipped.");
                continue;
            }

            if (!generator.TryResolveCollectible(item, out var actualKind, out var actualId) || actualKind != kind || actualId != rewardId)
            {
                log.WriteLine($"curated: online_store entry {itemId} '{UniqueRewardGenerator.Text(item.Name)}' declares {kind} {rewardId} but the item unlocks {actualKind} {actualId}; skipped.");
                continue;
            }

            items++;
            var count = generator.MarkOtherSource(itemId, kind, rewardId, OtherSource.OnlineStore);
            if (count == 0)
                unmatched.Add($"{itemId} ({kind} {rewardId})");
            marked += count;
        }

        log.WriteLine($"curated: {OnlineStoreFileName} applied {items} items, marked {marked} entries {OtherSource.OnlineStore}.");
        if (unmatched.Count > 0)
            log.WriteLine($"curated: {OnlineStoreFileName} {unmatched.Count} item(s) match no entry (orphaned by a regen?): {string.Join(", ", unmatched)}");
        return marked;
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
            // Strict, as the README requires and as the plugin's CuratedData loader reads them.
            var node = JsonNode.Parse(File.ReadAllText(path), documentOptions: CuratedData.StrictOptions);
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

    /// <summary>A string property, or null when absent, null or not a string (a number is not coerced).</summary>
    private static string? ReadString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
