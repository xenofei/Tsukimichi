using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tsukimichi.Core.Storage;

/// <summary>A quest that unlocks a system feature, from <c>curated/system_unlocks.json</c>.</summary>
public sealed record SystemUnlock(string Label, string Kind, string? Note);

/// <summary>A quest that unlocks duties, from <c>curated/duty_unlocks.json</c>.</summary>
public sealed record DutyUnlock(IReadOnlyList<uint> ContentFinderConditionIds, string? Note);

/// <summary>A seasonal event window, from <c>curated/festivals.json</c>. Null dates mean "unknown; use the live flag only".</summary>
public sealed record FestivalInfo(string Name, DateTime? Start, DateTime? End, bool MogStation);

/// <summary>A named quest chain assembled from journal genres in the listed order, from <c>curated/chains.json</c>.</summary>
public sealed record CuratedChain(string Name, IReadOnlyList<uint> GenreIds, string? Note);

/// <summary>
/// Hand-maintained overlays shipped in the plugin's <c>curated/</c> directory. Every file is optional and every entry
/// is validated on its own, so one bad line never hides the rest. Shapes (object keys are row ids as strings; keys
/// starting with <c>$</c>, such as <c>$schema_note</c>, are comments and ignored everywhere):
/// <code>
/// system_unlocks.json  { "66038": { "label": "Glamour Dresser", "kind": "system", "note": "..." } }
/// duty_unlocks.json    { "66038": [ 4, 5 ] }  or  { "66038": { "contentFinderConditionIds": [ 4, 5 ], "note": "..." } }
/// feature_quests.json  [ 66038, 66039 ]  or  { "questRowIds": [ 66038, 66039 ], "note": "..." }
/// festivals.json       { "1": { "name": "Starlight Celebration", "start": "2025-12-15T08:00:00Z", "end": "...", "mogStation": false } }
///                      or  { "entries": { "1": { ... } } }
/// chains.json          { "chains": [ { "name": "Hildibrand", "genreIds": [ 93, 94 ], "note": "..." } ] }
/// </code>
/// </summary>
public sealed class CuratedData
{
    public const string SystemUnlocksFileName = "system_unlocks.json";
    public const string DutyUnlocksFileName = "duty_unlocks.json";
    public const string FeatureQuestsFileName = "feature_quests.json";
    public const string FestivalsFileName = "festivals.json";
    public const string ChainsFileName = "chains.json";

    private const string DefaultSystemKind = "system";

    /// <summary>Object files may wrap their entries under this key (festivals.json does); the wrapper's other keys are ignored.</summary>
    private const string EntriesKey = "entries";

    /// <summary>feature_quests.json may wrap its ids under this key.</summary>
    private const string QuestRowIdsKey = "questRowIds";

    /// <summary>Object keys starting with this are comments (<c>$schema_note</c>) and never entries.</summary>
    private const char CommentKeyPrefix = '$';

    private CuratedData(
        IReadOnlyDictionary<uint, SystemUnlock> systemUnlocks,
        IReadOnlyDictionary<uint, DutyUnlock> dutyUnlocks,
        IReadOnlySet<uint> featureQuests,
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        IReadOnlyList<CuratedChain> chains,
        IReadOnlyList<string> warnings)
    {
        SystemUnlocks = systemUnlocks;
        DutyUnlocks = dutyUnlocks;
        FeatureQuests = featureQuests;
        Festivals = festivals;
        Chains = chains;
        Warnings = warnings;
    }

    public static readonly CuratedData Empty = new(
        new Dictionary<uint, SystemUnlock>(),
        new Dictionary<uint, DutyUnlock>(),
        new HashSet<uint>(),
        new Dictionary<ushort, FestivalInfo>(),
        [],
        []);

    public IReadOnlyDictionary<uint, SystemUnlock> SystemUnlocks { get; }
    public IReadOnlyDictionary<uint, DutyUnlock> DutyUnlocks { get; }
    public IReadOnlySet<uint> FeatureQuests { get; }
    public IReadOnlyDictionary<ushort, FestivalInfo> Festivals { get; }

    /// <summary>Named chains in file order; genre ids are not checked against the catalog here.</summary>
    public IReadOnlyList<CuratedChain> Chains { get; }

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
        var chains = new List<CuratedChain>();

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

        ForEachElement(Path.Combine(dir, FeatureQuestsFileName), QuestRowIdsKey, warnings, (index, node, warn) =>
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

        LoadChains(Path.Combine(dir, ChainsFileName), chains, warnings);

        return new CuratedData(systemUnlocks, dutyUnlocks, featureQuests, festivals, chains, warnings);
    }

    /// <summary>
    /// chains.json: an object with a "chains" array (a bare array is accepted too). Each chain needs a name and at
    /// least one genre id; ids may be numbers or digit strings. Duplicate ids within a chain are dropped.
    /// </summary>
    private static void LoadChains(string path, List<CuratedChain> chains, List<string> warnings)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        var array = root switch
        {
            JsonArray bare => bare,
            JsonObject obj when obj.TryGetPropertyValue("chains", out var node) && node is JsonArray inner => inner,
            _ => null,
        };

        if (array is null)
        {
            warnings.Add($"{fileName}: root must be an object with a \"chains\" array; file ignored.");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var label = $"{fileName}: chains[{i}] skipped: ";
            if (array[i] is not JsonObject chain)
            {
                warnings.Add(label + "not an object");
                continue;
            }

            var name = StorageJson.ReadString(chain, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                warnings.Add(label + "name is missing");
                continue;
            }

            if (!chain.TryGetPropertyValue("genreIds", out var idsNode) || idsNode is not JsonArray ids)
            {
                warnings.Add(label + "genreIds is not an array");
                continue;
            }

            var genreIds = new List<uint>(ids.Count);
            var valid = true;
            foreach (var element in ids)
            {
                if (!StorageJson.TryReadId(element, out var genreId) || genreId == 0)
                {
                    warnings.Add(label + $"genre id '{element}' is not a positive integer");
                    valid = false;
                    break;
                }

                if (!genreIds.Contains(genreId))
                {
                    genreIds.Add(genreId);
                }
            }

            if (!valid)
            {
                continue;
            }

            if (genreIds.Count == 0)
            {
                warnings.Add(label + "genreIds is empty");
                continue;
            }

            chains.Add(new CuratedChain(name.Trim(), genreIds, StorageJson.ReadString(chain, "note")));
        }
    }

    private delegate void EntryHandler(string key, JsonNode? value, Action<string> warn);

    private delegate void ElementHandler(int index, JsonNode? value, Action<string> warn);

    /// <summary>
    /// Visits each property of a JSON object file. The root may instead wrap the entries under
    /// <see cref="EntriesKey"/>; keys starting with <see cref="CommentKeyPrefix"/> are skipped silently. Missing file →
    /// nothing; unparseable → one warning.
    /// </summary>
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

        if (obj.TryGetPropertyValue(EntriesKey, out var entriesNode) && entriesNode is JsonObject entries)
        {
            obj = entries;
        }

        foreach (var pair in obj)
        {
            if (IsCommentKey(pair.Key))
            {
                continue;
            }

            handle(pair.Key, pair.Value, reason => warnings.Add($"{fileName}: entry \"{pair.Key}\" skipped: {reason}"));
        }
    }

    /// <summary>
    /// Visits each element of a JSON array file. The root may instead be an object holding the array under
    /// <paramref name="wrapperKey"/> (its other keys, such as a note, are ignored). Missing file → nothing;
    /// unparseable or neither shape → one warning.
    /// </summary>
    private static void ForEachElement(string path, string wrapperKey, List<string> warnings, ElementHandler handle)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        var array = root switch
        {
            JsonArray bare => bare,
            JsonObject obj when obj.TryGetPropertyValue(wrapperKey, out var node) && node is JsonArray inner => inner,
            _ => null,
        };

        if (array is null)
        {
            warnings.Add($"{fileName}: root must be a JSON array or an object with a \"{wrapperKey}\" array; file ignored.");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var index = i;
            handle(index, array[index], reason => warnings.Add($"{fileName}: element [{index}] skipped: {reason}"));
        }
    }

    private static bool IsCommentKey(string key) => key.Length > 0 && key[0] == CommentKeyPrefix;

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
