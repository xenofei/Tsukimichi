using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tsukimichi.Core.Storage;

/// <summary>A quest that unlocks a system feature, from <c>curated/system_unlocks.json</c>.</summary>
public sealed record SystemUnlock(string Label, string Kind, string? Note);

/// <summary>A quest that unlocks duties, from <c>curated/duty_unlocks.json</c>.</summary>
public sealed record DutyUnlock(IReadOnlyList<uint> ContentFinderConditionIds, string? Note);

/// <summary>A seasonal event window, from <c>curated/festivals.json</c>. Null dates mean "unknown; use the live flag only".</summary>
public sealed record FestivalInfo(string Name, DateTime? Start, DateTime? End, bool MogStation);

/// <summary>
/// Hand-maintained overlays shipped in the plugin's <c>curated/</c> directory. Every file is optional and every entry
/// is validated on its own, so one bad line never hides the rest. Shapes (object keys are row ids as strings):
/// <code>
/// system_unlocks.json  { "66038": { "label": "Glamour Dresser", "kind": "system", "note": "..." } }
/// duty_unlocks.json    { "66038": [ 4, 5 ] }  or  { "66038": { "contentFinderConditionIds": [ 4, 5 ], "note": "..." } }
/// feature_quests.json  [ 66038, 66039 ]
/// festivals.json       { "1": { "name": "Starlight Celebration", "start": "2025-12-15T08:00:00Z", "end": "...", "mogStation": false } }
/// </code>
/// </summary>
public sealed class CuratedData
{
    public const string SystemUnlocksFileName = "system_unlocks.json";
    public const string DutyUnlocksFileName = "duty_unlocks.json";
    public const string FeatureQuestsFileName = "feature_quests.json";
    public const string FestivalsFileName = "festivals.json";

    private const string DefaultSystemKind = "system";

    private CuratedData(
        IReadOnlyDictionary<uint, SystemUnlock> systemUnlocks,
        IReadOnlyDictionary<uint, DutyUnlock> dutyUnlocks,
        IReadOnlySet<uint> featureQuests,
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        IReadOnlyList<string> warnings)
    {
        SystemUnlocks = systemUnlocks;
        DutyUnlocks = dutyUnlocks;
        FeatureQuests = featureQuests;
        Festivals = festivals;
        Warnings = warnings;
    }

    public static readonly CuratedData Empty = new(
        new Dictionary<uint, SystemUnlock>(),
        new Dictionary<uint, DutyUnlock>(),
        new HashSet<uint>(),
        new Dictionary<ushort, FestivalInfo>(),
        []);

    public IReadOnlyDictionary<uint, SystemUnlock> SystemUnlocks { get; }
    public IReadOnlyDictionary<uint, DutyUnlock> DutyUnlocks { get; }
    public IReadOnlySet<uint> FeatureQuests { get; }
    public IReadOnlyDictionary<ushort, FestivalInfo> Festivals { get; }

    /// <summary>One line per skipped entry or unreadable file, for the caller to log once.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Loads every curated file under <paramref name="dir"/>. A missing directory or file yields empty collections.</summary>
    public static CuratedData Load(string dir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        if (!Directory.Exists(dir))
        {
            return Empty;
        }

        var warnings = new List<string>();
        var systemUnlocks = new Dictionary<uint, SystemUnlock>();
        var dutyUnlocks = new Dictionary<uint, DutyUnlock>();
        var featureQuests = new HashSet<uint>();
        var festivals = new Dictionary<ushort, FestivalInfo>();

        ForEachEntry(Path.Combine(dir, SystemUnlocksFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId))
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var label = StorageJson.ReadString(obj, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                warn("label is missing");
                return;
            }

            var kind = StorageJson.ReadString(obj, "kind");
            systemUnlocks[rowId] = new SystemUnlock(
                label,
                string.IsNullOrWhiteSpace(kind) ? DefaultSystemKind : kind,
                StorageJson.ReadString(obj, "note"));
        });

        ForEachEntry(Path.Combine(dir, DutyUnlocksFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId))
            {
                warn("key is not a quest row id");
                return;
            }

            JsonArray? ids;
            string? note = null;
            switch (node)
            {
                case JsonArray bare:
                    ids = bare;
                    break;
                case JsonObject obj:
                    ids = obj.TryGetPropertyValue("contentFinderConditionIds", out var idsNode) ? idsNode as JsonArray : null;
                    note = StorageJson.ReadString(obj, "note");
                    break;
                default:
                    ids = null;
                    break;
            }

            if (ids is null)
            {
                warn("value must be an array of content finder condition ids or an object with contentFinderConditionIds");
                return;
            }

            var parsed = new List<uint>(ids.Count);
            foreach (var element in ids)
            {
                if (!StorageJson.TryReadId(element, out var cfc))
                {
                    warn($"content finder condition id '{element}' is not a non-negative integer");
                    return;
                }

                parsed.Add(cfc);
            }

            dutyUnlocks[rowId] = new DutyUnlock(parsed, note);
        });

        ForEachElement(Path.Combine(dir, FeatureQuestsFileName), warnings, (index, node, warn) =>
        {
            if (!StorageJson.TryReadId(node, out var rowId))
            {
                warn("not a quest row id");
                return;
            }

            featureQuests.Add(rowId);
        });

        ForEachEntry(Path.Combine(dir, FestivalsFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out ushort festivalId))
            {
                warn("key is not a festival id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                warn("name is missing");
                return;
            }

            if (!StorageJson.TryReadUtc(obj, "start", out var start))
            {
                warn("start is not a valid timestamp");
                return;
            }

            if (!StorageJson.TryReadUtc(obj, "end", out var end))
            {
                warn("end is not a valid timestamp");
                return;
            }

            var mogStation = false;
            if (obj.TryGetPropertyValue("mogStation", out var mogNode) && mogNode is not null)
            {
                if (mogNode is not JsonValue mogValue || !mogValue.TryGetValue<bool>(out mogStation))
                {
                    warn("mogStation is not a boolean");
                    return;
                }
            }

            festivals[festivalId] = new FestivalInfo(name, start, end, mogStation);
        });

        return new CuratedData(systemUnlocks, dutyUnlocks, featureQuests, festivals, warnings);
    }

    private delegate void EntryHandler(string key, JsonNode? value, Action<string> warn);

    private delegate void ElementHandler(int index, JsonNode? value, Action<string> warn);

    /// <summary>Visits each property of a JSON object file. Missing file → nothing; unparseable → one warning.</summary>
    private static void ForEachEntry(string path, List<string> warnings, EntryHandler handle)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        if (root is not JsonObject obj)
        {
            warnings.Add($"{fileName}: root is not a JSON object; file ignored.");
            return;
        }

        foreach (var pair in obj)
        {
            handle(pair.Key, pair.Value, reason => warnings.Add($"{fileName}: entry \"{pair.Key}\" skipped: {reason}"));
        }
    }

    /// <summary>Visits each element of a JSON array file. Missing file → nothing; unparseable → one warning.</summary>
    private static void ForEachElement(string path, List<string> warnings, ElementHandler handle)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        if (root is not JsonArray array)
        {
            warnings.Add($"{fileName}: root is not a JSON array; file ignored.");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var index = i;
            handle(index, array[index], reason => warnings.Add($"{fileName}: element [{index}] skipped: {reason}"));
        }
    }

    /// <summary>Missing file → null silently; locked or inaccessible → null with one warning (the file is never moved).</summary>
    private static JsonNode? ParseRoot(string path, List<string> warnings)
    {
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings.Add($"{Path.GetFileName(path)} could not be read: {ioError}");
            }

            return null;
        }

        try
        {
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new JsonException("file is empty");
        }
        catch (JsonException ex)
        {
            warnings.Add($"{Path.GetFileName(path)} could not be parsed: {ex.Message}");
            return null;
        }
    }
}
