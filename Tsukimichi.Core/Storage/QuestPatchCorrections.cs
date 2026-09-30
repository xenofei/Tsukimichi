using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>One hand correction of a quest's patch: the value to ship instead of Garland's, why, and the page that says so.</summary>
/// <param name="Name">The quest's name, as a check that the row id is the quest meant.</param>
/// <param name="Patch">The patch the quest was added in (normalized, <see cref="PatchVersion.Normalize"/>).</param>
/// <param name="Reason">Why Garland's value is wrong.</param>
/// <param name="Evidence">An https page that states <paramref name="Patch"/> (the wiki quest page).</param>
public sealed record QuestPatchCorrection(string Name, string Patch, string Reason, string Evidence);

/// <summary>
/// <c>docs/data/quest-patch-corrections.json</c>: quest patches Garland Tools gets wrong, corrected by hand.
/// <c>Tsukimichi.Verify patches</c> lays them over Garland's values when it (re)seeds <c>quest_patches.json</c>, so a
/// re-seed never reverts a correction; <c>QuestPatchesFixtureTests</c> holds the shipped file to them. Shape:
/// <code>{ "schema": 1, "note": "...", "entries": { "65557": { "name": "Way of the Archer", "patch": "2.0", "reason": "...", "evidence": "https://..." } } }</code>
/// An entry without a patch number, a reason or an https evidence URL is skipped with a warning. Immutable.
/// </summary>
public sealed class QuestPatchCorrections
{
    public const string FileName = "quest-patch-corrections.json";

    private QuestPatchCorrections(IReadOnlyDictionary<uint, QuestPatchCorrection> byRowId, IReadOnlyList<string> warnings)
    {
        ByRowId = byRowId;
        Warnings = warnings;
    }

    /// <summary>No corrections.</summary>
    public static QuestPatchCorrections Empty { get; } = new(new Dictionary<uint, QuestPatchCorrection>(), []);

    public IReadOnlyDictionary<uint, QuestPatchCorrection> ByRowId { get; }

    /// <summary>Problems met while reading; the entries that parsed are kept.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Reads the file; a missing file is <see cref="Empty"/> (no corrections), an unreadable one Empty with a warning.</summary>
    public static QuestPatchCorrections Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            return Parse(File.ReadAllText(path), Path.GetFileName(path));
        }
        catch (IOException ex)
        {
            return new QuestPatchCorrections(new Dictionary<uint, QuestPatchCorrection>(), [$"{Path.GetFileName(path)} could not be read: {ex.Message}"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new QuestPatchCorrections(new Dictionary<uint, QuestPatchCorrection>(), [$"{Path.GetFileName(path)} could not be read: {ex.Message}"]);
        }
    }

    /// <summary>Parses the file's text (strict JSON, like the curated files).</summary>
    public static QuestPatchCorrections Parse(string json, string fileName = FileName)
    {
        var warnings = new List<string>();
        var entries = new Dictionary<uint, QuestPatchCorrection>();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        }
        catch (JsonException ex)
        {
            return new QuestPatchCorrections(entries, [$"{fileName} could not be parsed: {ex.Message}"]);
        }

        if (root is not JsonObject obj || obj["entries"] is not JsonObject map)
        {
            return new QuestPatchCorrections(entries, [$"{fileName}: no \"entries\" object; no corrections read."]);
        }

        foreach (var (key, node) in map)
        {
            void Warn(string message) => warnings.Add($"{fileName}: entries[\"{key}\"] skipped: {message}");
            if (!uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                Warn("key is not a quest row id");
                continue;
            }

            if (node is not JsonObject entry)
            {
                Warn("not an object");
                continue;
            }

            var patch = PatchVersion.Normalize(StorageJson.ReadString(entry, "patch"));
            var reason = StorageJson.ReadString(entry, "reason")?.Trim();
            var evidence = StorageJson.ReadString(entry, "evidence")?.Trim();
            if (!PatchVersion.IsPatch(patch))
            {
                Warn("patch is not a patch number");
                continue;
            }

            if (string.IsNullOrEmpty(reason))
            {
                Warn("reason is missing");
                continue;
            }

            if (evidence is null || !evidence.StartsWith("https://", StringComparison.Ordinal))
            {
                Warn("evidence is not an https URL");
                continue;
            }

            entries[id] = new QuestPatchCorrection(StorageJson.ReadString(entry, "name")?.Trim() ?? string.Empty, patch, reason, evidence);
        }

        return new QuestPatchCorrections(entries, warnings);
    }
}
