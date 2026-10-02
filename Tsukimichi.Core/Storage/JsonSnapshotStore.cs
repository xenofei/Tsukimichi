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
/// process, permissions, disk) is skipped with a warning and left in place, and so is one a newer plugin wrote (a higher
/// schema version, D11): it is valid, only not readable here. <see cref="LoadShared"/> never quarantines anything, for
/// files another game client owns. Unknown JSON properties are ignored on read,
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

    /// <summary>
    /// Loads one character for a reader that does not own the file (D11: the multibox scan, or a character live in
    /// another game client): nothing is quarantined and no warning is queued; the result says what was found. A file
    /// a newer plugin wrote reads as <see cref="SharedLoad.Newer"/>, a corrupt one as <see cref="SharedLoad.Invalid"/>.
    /// </summary>
    public SharedRead<CharacterSnapshot> LoadShared(ulong contentId) => ReadShared(PathFor(contentId));

    /// <summary>
    /// <see cref="Load(ulong)"/>, quarantining a corrupt file only when <paramref name="quarantine"/> is true: the
    /// client that owns the character. A file a newer plugin wrote is never quarantined (<see cref="SnapshotMigrator.IsNewer"/>).
    /// </summary>
    public CharacterSnapshot? Load(ulong contentId, bool quarantine)
    {
        var path = PathFor(contentId);
        return File.Exists(path) ? ReadSnapshot(path, quarantine) : null;
    }

    private CharacterSnapshot? ReadSnapshot(string path, bool quarantine = true)
    {
        var read = ReadShared(path, out var parseError);
        switch (read.Status)
        {
            case SharedLoad.Loaded:
                return read.Value;

            case SharedLoad.Invalid when quarantine:
                var fileName = Path.GetFileName(path);
                if (AtomicFile.TryQuarantine(path, out var moved, out var quarantineError))
                {
                    warnings.Add($"Snapshot {fileName} could not be read and was moved to {Path.GetFileName(moved)}: {parseError}");
                }
                else
                {
                    warnings.Add($"Snapshot {fileName} could not be read ({parseError}) and could not be quarantined: {quarantineError}");
                }

                return null;

            case SharedLoad.Missing:
                return null;

            default:
                warnings.Add(read.Problem ?? $"Snapshot {Path.GetFileName(path)} could not be read and was left in place.");
                return null;
        }
    }

    /// <summary>Reads and migrates one snapshot file without touching it. <see cref="SharedRead{T}.Problem"/> is the warning text.</summary>
    private SharedRead<CharacterSnapshot> ReadShared(string path) => ReadShared(path, out _);

    /// <param name="parseError">For <see cref="SharedLoad.Invalid"/>, the parser's own message.</param>
    private SharedRead<CharacterSnapshot> ReadShared(string path, out string? parseError)
    {
        parseError = null;
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            return ioError is null
                ? SharedRead<CharacterSnapshot>.Missing
                : new SharedRead<CharacterSnapshot>(SharedLoad.Unreadable, null, $"Snapshot {fileName} could not be read and was left in place: {ioError}");
        }

        try
        {
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidDataException("Snapshot file is empty.");

            if (SnapshotMigrator.IsNewer(root, out var version))
            {
                return new SharedRead<CharacterSnapshot>(
                    SharedLoad.Newer,
                    null,
                    $"Snapshot {fileName} was saved by a newer version of Tsukimichi (schema {version}, this one reads up to {CharacterSnapshot.CurrentSchemaVersion}); it is skipped and left in place.");
            }

            var migrated = migrator.Migrate(root, out _);
            var snapshot = migrated.Deserialize<CharacterSnapshot>(StorageJson.Options)
                ?? throw new InvalidDataException("Snapshot deserialized to null.");

            // Builds before 1.4.2 saved the raw rank byte, with the "ranked up today" bit set on a rank-up day.
            return SharedRead<CharacterSnapshot>.Of(snapshot.WithMaskedTribeRanks());
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException)
        {
            parseError = ex.Message;
            return new SharedRead<CharacterSnapshot>(SharedLoad.Invalid, null, $"Snapshot {fileName} could not be parsed and was left in place: {ex.Message}");
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
