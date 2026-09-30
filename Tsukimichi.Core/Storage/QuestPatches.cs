using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// One line of <see cref="QuestPatches.History"/>: which run assigned patches, to how many quest ids, from what.
/// <paramref name="Source"/> is <c>garland</c> for the seed (Tsukimichi.Verify patches) or <c>diff</c> for a DataGen
/// run that stamped the quest ids it had not seen before with <paramref name="Patch"/>.
/// </summary>
public sealed record QuestPatchesRun(string GameVersion, string Source, string Patch, int Assigned);

/// <summary>What <see cref="QuestPatches.Diff"/> found: the quest ids new since the previous file, and the file to write.</summary>
/// <param name="NewRowIds">Catalog row ids the previous file did not list, ascending.</param>
/// <param name="Updated">The previous file plus <paramref name="NewRowIds"/> stamped with the patch; the previous file itself when nothing was new.</param>
public sealed record QuestPatchesDiff(IReadOnlyList<uint> NewRowIds, QuestPatches Updated);

/// <summary>
/// <c>Tsukimichi/Data/quest_patches.json</c>: the patch each quest was added in (P8), keyed by Quest sheet row id.
/// Every quest id the catalog held when the file was last written is listed, with an empty string when the patch is
/// unknown, so "not listed" always means "new since then". Seeded once from Garland Tools' patch data
/// (<c>Tsukimichi.Verify patches</c>) and kept current offline by DataGen (<c>--patches</c>), which stamps every quest
/// id it has not seen before with the patch being regenerated for. Read at catalog build into
/// <see cref="QuestRecord.AddedIn"/>. Immutable.
/// </summary>
public sealed class QuestPatches
{
    public const string FileName = "quest_patches.json";

    public const string SourceGarland = "garland";
    public const string SourceDiff = "diff";

    private const string SchemaNote =
        "Quest sheet row id -> the patch the quest was added in (\"2.0\", \"6.55\", \"7.5\"), for the plugin's \"Added in\" filter, the Unlocks quick view's \"New in 7.5x\" group and the detail pane. " +
        "Every quest id the catalog held at the last write is listed; \"\" means the patch is unknown, and an id not listed is new since then. " +
        "Seeded from Garland Tools' per-patch documents by `Tsukimichi.Verify patches` (facts only; see docs/data/quest-patches-report.md), " +
        "which lays the hand corrections of docs/data/quest-patch-corrections.json over Garland's values; " +
        "maintained offline by `Tsukimichi.DataGen --patches ... --patch <x.y>` (tools/regen.ps1 -Patch), which stamps every quest id not seen before with that patch. " +
        "history lists each run. Do not edit by hand: correct a patch in docs/data/quest-patch-corrections.json (with the reason and evidence) and in this file.";

    private readonly Dictionary<uint, string> byRowId;

    public QuestPatches(string gameVersion, IReadOnlyDictionary<uint, string> byRowId, IReadOnlyList<QuestPatchesRun> history, IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(byRowId);
        ArgumentNullException.ThrowIfNull(history);
        GameVersion = gameVersion ?? string.Empty;
        this.byRowId = new Dictionary<uint, string>(byRowId.Count);
        foreach (var (id, patch) in byRowId)
        {
            this.byRowId[id] = PatchVersion.Normalize(patch);
        }

        History = history;
        Warnings = warnings ?? [];
        Newest = PatchVersion.Newest(this.byRowId.Values);
        KnownCount = this.byRowId.Values.Count(PatchVersion.IsPatch);
    }

    /// <summary>No data: every quest's patch is unknown and nothing is "new this patch".</summary>
    public static QuestPatches Empty { get; } = new(string.Empty, new Dictionary<uint, string>(), []);

    /// <summary>The game version of the catalog the file was last written against.</summary>
    public string GameVersion { get; }

    /// <summary>Every listed quest id; an empty value is a quest whose patch is unknown.</summary>
    public IReadOnlyDictionary<uint, string> ByRowId => byRowId;

    public IReadOnlyList<QuestPatchesRun> History { get; }

    /// <summary>Problems met while reading; the entries that parsed are kept.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>The newest patch any quest carries (<see cref="PatchVersion.Compare"/>); empty when none does.</summary>
    public string Newest { get; }

    /// <summary>Listed quests with a known patch.</summary>
    public int KnownCount { get; }

    /// <summary>The quest's patch; empty when unknown or not listed.</summary>
    public string For(uint rowId) => byRowId.TryGetValue(rowId, out var patch) ? patch : string.Empty;

    /// <summary>Whether the quest id was in the catalog when the file was last written (known patch or not).</summary>
    public bool Lists(uint rowId) => byRowId.ContainsKey(rowId);

    /// <summary>
    /// The records with <see cref="QuestRecord.AddedIn"/> set from this file (empty for a quest it has no patch for).
    /// The catalog build and the fixture reader both lay the file over the mapped records this way; with no data the
    /// input is returned as is.
    /// </summary>
    public IReadOnlyList<QuestRecord> Apply(IReadOnlyList<QuestRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (byRowId.Count == 0)
        {
            return records;
        }

        var result = new QuestRecord[records.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var record = records[i];
            var patch = For(record.RowId);
            result[i] = string.Equals(record.AddedIn, patch, StringComparison.Ordinal) ? record : record with { AddedIn = patch };
        }

        return result;
    }

    /// <summary>Reads the file; a missing or unreadable file is <see cref="Empty"/> with a warning.</summary>
    public static QuestPatches Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new QuestPatches(string.Empty, new Dictionary<uint, string>(), [], [$"{Path.GetFileName(path)} not found; every quest's patch reads as unknown."]);
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            return new QuestPatches(string.Empty, new Dictionary<uint, string>(), [], [$"{Path.GetFileName(path)} could not be read: {ex.Message}"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new QuestPatches(string.Empty, new Dictionary<uint, string>(), [], [$"{Path.GetFileName(path)} could not be read: {ex.Message}"]);
        }

        return Parse(text, Path.GetFileName(path));
    }

    /// <summary>Parses the file's text. Entries whose key is not a row id or whose value is not a string are skipped with a warning.</summary>
    public static QuestPatches Parse(string json, string fileName = FileName)
    {
        var warnings = new List<string>();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            return new QuestPatches(string.Empty, new Dictionary<uint, string>(), [], [$"{fileName} could not be parsed: {ex.Message}"]);
        }

        if (root is not JsonObject obj)
        {
            return new QuestPatches(string.Empty, new Dictionary<uint, string>(), [], [$"{fileName}: root is not an object; file ignored."]);
        }

        var gameVersion = obj["gameVersion"] is JsonValue gv && gv.TryGetValue<string>(out var g) ? g : string.Empty;
        var patches = new Dictionary<uint, string>();
        if (obj["patches"] is JsonObject map)
        {
            foreach (var (key, value) in map)
            {
                if (!uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    warnings.Add($"{fileName}: patches[\"{key}\"] skipped: key is not a quest row id");
                    continue;
                }

                if (value is not JsonValue v || !v.TryGetValue<string>(out var patch))
                {
                    warnings.Add($"{fileName}: patches[\"{key}\"] skipped: value is not a string");
                    continue;
                }

                var normalized = PatchVersion.Normalize(patch);
                if (normalized.Length > 0 && !PatchVersion.IsPatch(normalized))
                {
                    warnings.Add($"{fileName}: patches[\"{key}\"] = \"{patch}\" is not a patch number; read as unknown");
                    normalized = string.Empty;
                }

                patches[id] = normalized;
            }
        }
        else
        {
            warnings.Add($"{fileName}: no \"patches\" object; every quest's patch reads as unknown.");
        }

        var history = new List<QuestPatchesRun>();
        foreach (var node in obj["history"] as JsonArray ?? [])
        {
            if (node is JsonObject run)
            {
                history.Add(new QuestPatchesRun(
                    StorageJson.ReadString(run, "gameVersion") ?? string.Empty,
                    StorageJson.ReadString(run, "source") ?? string.Empty,
                    StorageJson.ReadString(run, "patch") ?? string.Empty,
                    run["assigned"] is JsonValue av && av.TryGetValue<int>(out var assigned) ? assigned : 0));
            }
        }

        return new QuestPatches(gameVersion, patches, history, warnings);
    }

    /// <summary>
    /// The DataGen diff: every id in <paramref name="catalogRowIds"/> the file does not list is new since it was last
    /// written and is stamped with <paramref name="patch"/>. Ids the file lists keep their value (a quest the game
    /// dropped keeps its line too, so a later return is not mistaken for new). The result carries
    /// <paramref name="gameVersion"/> and, when anything was new, a <see cref="SourceDiff"/> history line.
    /// </summary>
    /// <exception cref="InvalidOperationException">New ids were found and <paramref name="patch"/> is not a patch number.</exception>
    public QuestPatchesDiff Diff(IEnumerable<uint> catalogRowIds, string gameVersion, string? patch)
    {
        ArgumentNullException.ThrowIfNull(catalogRowIds);
        var fresh = catalogRowIds.Where(id => !byRowId.ContainsKey(id)).Distinct().Order().ToList();
        if (fresh.Count == 0)
        {
            return new QuestPatchesDiff(fresh, gameVersion == GameVersion ? this : new QuestPatches(gameVersion, byRowId, History));
        }

        var stamp = PatchVersion.Normalize(patch);
        if (!PatchVersion.IsPatch(stamp))
        {
            throw new InvalidOperationException(
                $"{fresh.Count} quest id(s) are new since {FileName} was last written (game {GameVersion} -> {gameVersion}); pass the patch this game version ships as --patch <x.y> (tools/regen.ps1 -Patch <x.y>).");
        }

        var merged = new Dictionary<uint, string>(byRowId);
        foreach (var id in fresh)
        {
            merged[id] = stamp;
        }

        var history = History.Append(new QuestPatchesRun(gameVersion, SourceDiff, stamp, fresh.Count)).ToList();
        return new QuestPatchesDiff(fresh, new QuestPatches(gameVersion, merged, history));
    }

    /// <summary>
    /// The file's text: fixed key order, one quest per line in row id order, LF line endings and a trailing newline,
    /// so a regeneration's diff is exactly the quests that changed.
    /// </summary>
    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"$schema_note\": ").Append(Quote(SchemaNote)).Append(",\n");
        sb.Append("  \"gameVersion\": ").Append(Quote(GameVersion)).Append(",\n");
        sb.Append("  \"newestPatch\": ").Append(Quote(Newest)).Append(",\n");
        sb.Append("  \"known\": ").Append(KnownCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("  \"listed\": ").Append(byRowId.Count.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("  \"history\": [");
        for (var i = 0; i < History.Count; i++)
        {
            var run = History[i];
            sb.Append(i == 0 ? "\n" : ",\n");
            sb.Append("    { \"gameVersion\": ").Append(Quote(run.GameVersion))
                .Append(", \"source\": ").Append(Quote(run.Source))
                .Append(", \"patch\": ").Append(Quote(run.Patch))
                .Append(", \"assigned\": ").Append(run.Assigned.ToString(CultureInfo.InvariantCulture)).Append(" }");
        }

        sb.Append(History.Count == 0 ? "],\n" : "\n  ],\n");
        sb.Append("  \"patches\": {");
        var first = true;
        foreach (var (id, patch) in byRowId.OrderBy(kv => kv.Key))
        {
            sb.Append(first ? "\n" : ",\n");
            first = false;
            sb.Append("    \"").Append(id.ToString(CultureInfo.InvariantCulture)).Append("\": ").Append(Quote(patch));
        }

        sb.Append(first ? "}\n" : "\n  }\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>Writes <see cref="ToJson"/> as UTF-8 without a BOM, through a temporary file so a failed write keeps the old one.</summary>
    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = full + ".tmp";
        File.WriteAllText(temp, ToJson(), new UTF8Encoding(false));
        File.Move(temp, full, overwrite: true);
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text ?? string.Empty, QuoteOptions);

    /// <summary>Quotes and backslashes escaped, nothing else: the note reads as written.</summary>
    private static readonly JsonSerializerOptions QuoteOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
