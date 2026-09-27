using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// Stores each character as <c>characters/&lt;ContentId&gt;.json</c> under a root directory.
/// Writes are atomic; a file that cannot be parsed is quarantined (see <see cref="AtomicFile.Quarantine"/>) and a warning
/// is queued in <see cref="Warnings"/> for the caller to log once. A file that cannot be read at all (locked by another
/// process, permissions, disk) is skipped with a warning and left in place. Unknown JSON properties are ignored on read,
/// and quest ids the catalog does not know are carried through untouched.
/// </summary>
public sealed class JsonSnapshotStore : ISnapshotStore
{
    private const string CharactersFolder = "characters";

    private readonly string charactersDir;
    private readonly SnapshotMigrator migrator;
    private readonly List<string> warnings = [];

    public JsonSnapshotStore(string rootDir, SnapshotMigrator? migrator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDir);
        charactersDir = Path.Combine(rootDir, CharactersFolder);
        this.migrator = migrator ?? new SnapshotMigrator();
    }

    /// <summary>Problems met while reading, oldest first. The caller logs them and calls <see cref="ClearWarnings"/>.</summary>
    public IReadOnlyList<string> Warnings => warnings;

    public void ClearWarnings() => warnings.Clear();

    /// <summary>Every readable snapshot. A file that is locked or corrupt is skipped with a warning; it never aborts the listing.</summary>
    public IReadOnlyList<SnapshotSummary> List()
    {
        if (!Directory.Exists(charactersDir))
        {
            return [];
        }

        var summaries = new List<SnapshotSummary>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(charactersDir, "*.json"))
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                if (!ulong.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                var snapshot = ReadSnapshot(path);
                if (snapshot is not null)
                {
                    summaries.Add(Summarize(snapshot));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not list {charactersDir}: {ex.Message}");
        }

        return summaries;
    }

    public CharacterSnapshot? Load(ulong contentId)
    {
        var path = PathFor(contentId);
        return File.Exists(path) ? ReadSnapshot(path) : null;
    }

    public void Save(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var stamped = snapshot with { SchemaVersion = CharacterSnapshot.CurrentSchemaVersion };
        var json = JsonSerializer.Serialize(stamped, StorageJson.Options);
        AtomicFile.Write(PathFor(snapshot.ContentId), json);
    }

    public void Delete(ulong contentId)
    {
        var path = PathFor(contentId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(ulong contentId) =>
        Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + ".json");

    private CharacterSnapshot? ReadSnapshot(string path)
    {
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings.Add($"Snapshot {fileName} could not be read and was left in place: {ioError}");
            }

            return null;
        }

        try
        {
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidDataException("Snapshot file is empty.");

            var migrated = migrator.Migrate(root, out _);
            return migrated.Deserialize<CharacterSnapshot>(StorageJson.Options)
                ?? throw new InvalidDataException("Snapshot deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException)
        {
            if (AtomicFile.TryQuarantine(path, out var moved, out var quarantineError))
            {
                warnings.Add($"Snapshot {fileName} could not be read and was moved to {Path.GetFileName(moved)}: {ex.Message}");
            }
            else
            {
                warnings.Add($"Snapshot {fileName} could not be read ({ex.Message}) and could not be quarantined: {quarantineError}");
            }

            return null;
        }
    }

    private static SnapshotSummary Summarize(CharacterSnapshot snapshot) =>
        new(snapshot.ContentId, snapshot.Name, snapshot.World, snapshot.TakenUtc, CountBits(snapshot.CompletedBits));

    private static int CountBits(byte[] bits)
    {
        var count = 0;
        foreach (var b in bits)
        {
            count += BitOperations.PopCount(b);
        }

        return count;
    }
}
