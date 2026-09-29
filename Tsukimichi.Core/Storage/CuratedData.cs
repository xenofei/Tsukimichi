using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

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
/// A quest reward that the FFXIV Online Store also sells, from <c>curated/online_store.json</c>. The file is keyed by
/// the store item's row id; <paramref name="Kind"/> and <paramref name="RewardId"/> name the collectible that item
/// unlocks, so a reward a quest grants directly (an emote with no item) still matches.
/// </summary>
public sealed record OnlineStoreItem(string Name, RewardKind Kind, uint RewardId, string Evidence, string? Note);

/// <summary>
/// A quest pinned to a journal genre after the refiling rules ran, from <c>curated/refile_overrides.json</c>: a
/// quest whose sheet signals point at the wrong genre (the Eureka entry quasi-quests land in Kugane Sidequests by
/// their issuer's zone; they belong with The Forbidden Land, Eureka) or at none.
/// </summary>
/// <param name="GenreId">JournalGenre row id the quest is filed under.</param>
public sealed record RefileOverride(uint GenreId, string Note, string Evidence);

/// <summary>
/// A quest the game removed that the sheets do not mark, from <c>curated/retired_quests.json</c>: sidequests deleted
/// without a placeholder issuer or the hidden flag, and listed rows the game retired but never re-filed.
/// </summary>
/// <param name="Patch">The patch that removed it ("6.3"), when known; empty otherwise.</param>
public sealed record RetiredQuest(string Note, string Evidence, string Patch);

/// <summary>
/// Hand-maintained overlays shipped in the plugin's <c>curated/</c> directory. Every file is optional and every entry
/// is validated on its own, so one bad line never hides the rest. Shapes (object keys are row ids as strings; keys
/// starting with <c>$</c>, such as <c>$schema_note</c>, are comments and ignored everywhere):
/// <code>
/// system_unlocks.json  { "66038": { "label": "Glamour Dresser", "kind": "system", "note": "..." } }
/// duty_unlocks.json    { "66038": [ 4, 5 ] }  or  { "66038": { "contentFinderConditionIds": [ 4, 5 ], "note": "..." } }
/// feature_quests.json  [ 66038, 66039 ]  or  { "questRowIds": [ 66038, 66039 ], "note": "..." }   (written by DataGen, not by hand)
/// festivals.json       { "1": { "name": "Starlight Celebration", "start": "2025-12-15T08:00:00Z", "end": "...", "mogStation": false } }
///                      or  { "entries": { "1": { ... } } }
/// chains.json          { "chains": [ { "name": "Hildibrand", "genreIds": [ 93, 94 ], "note": "..." } ] }
/// online_store.json    { "schema": 1, "note": "...", "entries": { "22437": { "name": "Starlight Bear", "kind": "Mount", "rewardId": 99, "evidence": "https://...", "note": "..." } } }
/// refile_overrides.json { "schema": 1, "entries": { "68478": { "genre": 90, "note": "...", "evidence": "https://..." } } }
/// retired_quests.json  { "schema": 1, "entries": { "66033": { "note": "...", "evidence": "https://...", "patch": "6.3" } } }   (patch optional)
/// VERSION.json         { "curatedRevision": "573d225" }   (written by tools/regen.ps1; absent in a checkout that never ran it)
/// </code>
/// Every file must be strict JSON (no comments, no trailing commas), as the curated README requires.
/// </summary>
public sealed class CuratedData
{
    public const string SystemUnlocksFileName = "system_unlocks.json";
    public const string DutyUnlocksFileName = "duty_unlocks.json";
    public const string FeatureQuestsFileName = "feature_quests.json";
    public const string FestivalsFileName = "festivals.json";
    public const string ChainsFileName = "chains.json";
    public const string OnlineStoreFileName = "online_store.json";
    public const string RefileOverridesFileName = "refile_overrides.json";
    public const string RetiredQuestsFileName = "retired_quests.json";

    /// <summary>Written by <c>tools/regen.ps1</c>: the overlay's revision for the About stamp and the diagnostic block.</summary>
    public const string VersionFileName = "VERSION.json";

    /// <summary>The key in <see cref="VersionFileName"/> holding the short git hash of the last commit touching the overlay.</summary>
    public const string CuratedRevisionKey = "curatedRevision";

    private const string DefaultSystemKind = "system";

    /// <summary>Object files may wrap their entries under this key (festivals.json does); the wrapper's other keys are ignored.</summary>
    private const string EntriesKey = "entries";

    /// <summary>feature_quests.json may wrap its ids under this key.</summary>
    private const string QuestRowIdsKey = "questRowIds";

    /// <summary>Object keys starting with this are comments (<c>$schema_note</c>) and never entries.</summary>
    private const char CommentKeyPrefix = '$';

    /// <summary>The parse options every curated file must satisfy: no comments, no trailing commas.</summary>
    public static readonly JsonDocumentOptions StrictOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    private CuratedData(
        IReadOnlyDictionary<uint, SystemUnlock> systemUnlocks,
        IReadOnlyDictionary<uint, DutyUnlock> dutyUnlocks,
        IReadOnlySet<uint> featureQuests,
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        IReadOnlyList<CuratedChain> chains,
        IReadOnlyDictionary<uint, OnlineStoreItem> onlineStore,
        IReadOnlyDictionary<uint, RefileOverride> refileOverrides,
        IReadOnlyDictionary<uint, RetiredQuest> retiredQuests,
        string curatedRevision,
        IReadOnlyList<string> warnings)
    {
        SystemUnlocks = systemUnlocks;
        DutyUnlocks = dutyUnlocks;
        FeatureQuests = featureQuests;
        Festivals = festivals;
        Chains = chains;
        OnlineStore = onlineStore;
        RefileOverrides = refileOverrides;
        RetiredQuests = retiredQuests;
        CuratedRevision = curatedRevision;
        Warnings = warnings;
    }

    public static readonly CuratedData Empty = new(
        new Dictionary<uint, SystemUnlock>(),
        new Dictionary<uint, DutyUnlock>(),
        new HashSet<uint>(),
        new Dictionary<ushort, FestivalInfo>(),
        [],
        new Dictionary<uint, OnlineStoreItem>(),
        new Dictionary<uint, RefileOverride>(),
        new Dictionary<uint, RetiredQuest>(),
        string.Empty,
        []);

    public IReadOnlyDictionary<uint, SystemUnlock> SystemUnlocks { get; }
    public IReadOnlyDictionary<uint, DutyUnlock> DutyUnlocks { get; }
    public IReadOnlySet<uint> FeatureQuests { get; }
    public IReadOnlyDictionary<ushort, FestivalInfo> Festivals { get; }

    /// <summary>Named chains in file order; genre ids are not checked against the catalog here.</summary>
    public IReadOnlyList<CuratedChain> Chains { get; }

    /// <summary>Rewards the Online Store also sells, by store item row id.</summary>
    public IReadOnlyDictionary<uint, OnlineStoreItem> OnlineStore { get; }

    /// <summary>Quests pinned to a genre after the refiling rules, by quest row id; read by <c>JournalRefiler</c>.</summary>
    public IReadOnlyDictionary<uint, RefileOverride> RefileOverrides { get; }

    /// <summary>Quests the game removed that the sheets do not mark, by quest row id; read by <c>JournalRefiler</c>.</summary>
    public IReadOnlyDictionary<uint, RetiredQuest> RetiredQuests { get; }

    /// <summary>
    /// Short git hash of the last commit touching the overlay, from <see cref="VersionFileName"/> ("573d225", or
    /// "573d225-dirty" when regenerated with uncommitted changes); empty when the file is absent or has no value.
    /// </summary>
    public string CuratedRevision { get; }

    /// <summary>One line per skipped entry or unreadable file, for the caller to log once.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// The same data with <see cref="FeatureQuests"/> empty: what DataGen derives <c>feature_quests.json</c> from and
    /// what the invariants test compares the shipped file against, so the file never feeds its own derivation.
    /// </summary>
    public CuratedData WithoutFeatureQuests() =>
        FeatureQuests.Count == 0 ? this : new CuratedData(SystemUnlocks, DutyUnlocks, new HashSet<uint>(), Festivals, Chains, OnlineStore, RefileOverrides, RetiredQuests, CuratedRevision, Warnings);

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
        var onlineStore = new Dictionary<uint, OnlineStoreItem>();
        var refileOverrides = new Dictionary<uint, RefileOverride>();
        var retiredQuests = new Dictionary<uint, RetiredQuest>();

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

        ForEachEntry(Path.Combine(dir, OnlineStoreFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint itemId) || itemId == 0)
            {
                warn("key is not an item row id");
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

            var kindText = StorageJson.ReadString(obj, "kind");
            if (kindText is null || !Enum.TryParse<RewardKind>(kindText, ignoreCase: false, out var kind) || !Enum.IsDefined(kind))
            {
                warn($"kind '{kindText}' is not a RewardKind");
                return;
            }

            if (!obj.TryGetPropertyValue("rewardId", out var rewardNode) || !StorageJson.TryReadId(rewardNode, out var rewardId) || rewardId == 0)
            {
                warn("rewardId is not a positive integer");
                return;
            }

            var evidence = StorageJson.ReadString(obj, "evidence");
            if (string.IsNullOrWhiteSpace(evidence))
            {
                warn("evidence is missing");
                return;
            }

            onlineStore[itemId] = new OnlineStoreItem(name.Trim(), kind, rewardId, evidence.Trim(), StorageJson.ReadString(obj, "note"));
        });

        ForEachEntry(Path.Combine(dir, RefileOverridesFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!obj.TryGetPropertyValue("genre", out var genreNode) || !StorageJson.TryReadId(genreNode, out var genreId) || genreId == 0)
            {
                warn("genre is not a positive JournalGenre row id");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            refileOverrides[rowId] = new RefileOverride(genreId, note, evidence);
        });

        ForEachEntry(Path.Combine(dir, RetiredQuestsFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            retiredQuests[rowId] = new RetiredQuest(note, evidence, StorageJson.ReadString(obj, "patch")?.Trim() ?? string.Empty);
        });

        var curatedRevision = LoadRevision(Path.Combine(dir, VersionFileName), warnings);

        return new CuratedData(systemUnlocks, dutyUnlocks, featureQuests, festivals, chains, onlineStore, refileOverrides, retiredQuests, curatedRevision, warnings);
    }

    /// <summary>The note and evidence URL every refiling entry must carry (the curated README's rule for hand-filed quests).</summary>
    private static bool TryReadNoteAndEvidence(JsonObject obj, Action<string> warn, out string note, out string evidence)
    {
        note = StorageJson.ReadString(obj, "note")?.Trim() ?? string.Empty;
        evidence = StorageJson.ReadString(obj, "evidence")?.Trim() ?? string.Empty;
        if (note.Length == 0)
        {
            warn("note is missing");
            return false;
        }

        if (evidence.Length == 0)
        {
            warn("evidence is missing");
            return false;
        }

        return true;
    }

    /// <summary>
    /// VERSION.json: an object with a <see cref="CuratedRevisionKey"/> string. A missing file is the normal state of a
    /// checkout that never ran <c>tools/regen.ps1</c> and reads as an empty revision without a warning; a file without
    /// the key, or with a blank one, is warned about.
    /// </summary>
    private static string LoadRevision(string path, List<string> warnings)
    {
        if (!File.Exists(path) || ParseRoot(path, warnings) is not { } root)
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(path);
        if (root is not JsonObject obj)
        {
            warnings.Add($"{fileName}: root is not an object; no curated revision.");
            return string.Empty;
        }

        var revision = StorageJson.ReadString(obj, CuratedRevisionKey);
        if (string.IsNullOrWhiteSpace(revision))
        {
            warnings.Add($"{fileName}: {CuratedRevisionKey} is missing; no curated revision.");
            return string.Empty;
        }

        return revision.Trim();
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
            // Strict, as the curated README promises: a comment or a trailing comma is a parse error, not a tolerance.
            return JsonNode.Parse(text, documentOptions: StrictOptions) ?? throw new JsonException("file is empty");
        }
        catch (JsonException ex)
        {
            warnings.Add($"{Path.GetFileName(path)} could not be parsed: {ex.Message}");
            return null;
        }
    }
}
