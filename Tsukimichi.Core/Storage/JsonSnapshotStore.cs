using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// Stores each character as <c>characters/&lt;ContentId&gt;.json</c> under a root directory.
/// Writes are atomic; a file that cannot be parsed is quarantined (see <see cref="AtomicFile.Quarantine"/>) and a warning
/// is queued in <see cref="Warnings"/> for the caller to log once. A file that cannot be read at all (locked by another
/// process, permissions, disk) is skipped with a warning and left in place, and so is one a newer plugin wrote (a higher
/// schema version, D11): it is valid, only not readable here. <see cref="LoadShared"/> never quarantines anything, for
/// files another game client owns. Unknown JSON properties are ignored on read,
/// and quest ids the catalog does not know are carried through untouched. Before a save overwrites a character's file,
/// the file is copied to its once-a-day backup (<see cref="SnapshotBackup"/>); a backup that fails is reported through
/// <see cref="BackupFailed"/> and never holds up the save.
/// <para>
/// Quest completion dates are not written into the snapshot file: <see cref="Save"/> writes them to the character's
/// dates file (<see cref="CompletionDateFile"/>) first and the snapshot without them, and a load reads them back in, so
/// callers see one snapshot as before. A snapshot without dates never touches the dates file, and dates a 1.5 preview
/// wrote inline are read from the snapshot file until the next save moves them.
/// </para>
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

    /// <summary>
    /// Called (on the saving thread) when the backup before a save could not be written: the snapshot path and the
    /// error. The save goes ahead. Null ignores the failure.
    /// </summary>
    public Action<string, Exception>? BackupFailed { get; init; }

    /// <summary>The clock the backup's once-a-day rule reads; UTC now by default.</summary>
    public Func<DateTime> Clock { get; init; } = static () => DateTime.UtcNow;

    /// <summary>
    /// The quest catalog the backup refresh judges a lost-progress file by (<see cref="CapturePlausibility"/>), read on
    /// the saving thread; null (or a null result, no catalog built yet) refreshes the backup without that check.
    /// </summary>
    public Func<QuestCatalog?>? Catalog { get; init; }

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

                var snapshot = ReadSnapshot(path, withDates: false);
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

    /// <summary>
    /// Writes the snapshot: the backup refresh when due, then the completion dates to their own file (only when the
    /// snapshot records dates), then the snapshot file without them. A failed dates write throws before the snapshot
    /// is written, so the caller retries both.
    /// </summary>
    public void Save(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var stamped = snapshot with { SchemaVersion = CharacterSnapshot.CurrentSchemaVersion };
        var json = JsonSerializer.Serialize(CompletionDateFile.Strip(stamped), StorageJson.Options);
        var path = PathFor(snapshot.ContentId);
        RotateBackup(snapshot.ContentId, force: false);
        if (stamped.CompletionDatesSinceUtc is not null)
        {
            CompletionDateFile.Save(CompletionDateFile.PathFor(charactersDir, snapshot.ContentId), stamped);
        }

        AtomicFile.Write(path, json);
    }

    /// <summary>
    /// Copies the character's saved file to its backup now, whether or not the once-a-day refresh is due (the older
    /// generation moves back one), unless the file lost progress against the backup. For a capture the plausibility
    /// guard held back and then accepted: the save it is about to overwrite is kept. Failures go to
    /// <see cref="BackupFailed"/>. Returns whether a copy was written.
    /// </summary>
    public bool BackupNow(ulong contentId) => RotateBackup(contentId, force: true);

    private bool RotateBackup(ulong contentId, bool force)
    {
        var path = PathFor(contentId);
        try
        {
            return SnapshotBackup.RotateIfDue(
                path,
                SnapshotBackup.PathFor(charactersDir, contentId),
                Clock(),
                SnapshotBackup.OlderPathFor(charactersDir, contentId),
                LostProgress,
                force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BackupFailed?.Invoke(path, ex);
            return false;
        }
    }

    /// <summary>
    /// Whether the saved file (<paramref name="snapshotText"/>) lost many completed quests against the backup
    /// (<paramref name="backupText"/>), or reads as an empty character, by <see cref="CapturePlausibility"/>. False
    /// without a catalog or when either file does not parse here.
    /// </summary>
    private bool LostProgress(string backupText, string snapshotText)
    {
        if (Catalog?.Invoke() is not { } catalog)
        {
            return false;
        }

        var backup = ParseSnapshot(backupText);
        var saved = ParseSnapshot(snapshotText);
        if (backup is null || saved is null || backup.ContentId != saved.ContentId)
        {
            return false;
        }

        return CapturePlausibility.Check(backup, saved, catalog).Verdict is PlausibilityVerdict.EmptyCapture or PlausibilityVerdict.LostCompletions;
    }

    /// <summary>A snapshot from its file text, or null when it does not parse here (or a newer plugin wrote it).</summary>
    private CharacterSnapshot? ParseSnapshot(string text)
    {
        try
        {
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (root is null || SnapshotMigrator.IsNewer(root, out _))
            {
                return null;
            }

            return migrator.Migrate(root, out _).Deserialize<CharacterSnapshot>(StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The owner's read of a character's completion dates for a first pass (worker-safe: no warning is queued). The
    /// dates file as <see cref="CompletionDateFile.Read"/> returns it, except: a file that does not parse is
    /// quarantined (still reported <see cref="SharedLoad.Invalid"/>, its problem saying where it went), and with no
    /// dates file the snapshot file's inline dates from a 1.5 preview are used. With neither, the result is
    /// <see cref="SharedLoad.Missing"/>; a snapshot file that cannot be read right now (it may hold inline dates) is
    /// <see cref="SharedLoad.Unreadable"/>, so the caller retries rather than starts over. (A snapshot file a newer
    /// plugin wrote keeps its dates in the dates file, so without one it has none.)
    /// </summary>
    public SharedRead<CharacterSnapshot> LoadDates(ulong contentId)
    {
        var datesPath = CompletionDateFile.PathFor(charactersDir, contentId);
        var dates = CompletionDateFile.Read(datesPath, contentId);
        switch (dates.Status)
        {
            case SharedLoad.Missing:
                break;

            case SharedLoad.Invalid:
                var note = AtomicFile.TryQuarantine(datesPath, out var moved, out var quarantineError)
                    ? $"{dates.Problem} (moved to {Path.GetFileName(moved)})"
                    : $"{dates.Problem} (could not be quarantined: {quarantineError})";
                return new SharedRead<CharacterSnapshot>(SharedLoad.Invalid, null, note);

            default:
                return dates;
        }

        var path = PathFor(contentId);
        var snapshot = ReadShared(path, out _, withDates: false);
        switch (snapshot.Status)
        {
            case SharedLoad.Loaded when snapshot.Value is { CompletionDatesSinceUtc: not null } inline:
                return SharedRead<CharacterSnapshot>.Of(inline);

            case SharedLoad.Unreadable:
                return snapshot;

            default:
                return SharedRead<CharacterSnapshot>.Missing;
        }
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

    private CharacterSnapshot? ReadSnapshot(string path, bool quarantine = true, bool withDates = true)
    {
        var read = ReadShared(path, out var parseError, withDates);
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
    /// <param name="withDates">
    /// Reads the character's dates file into the snapshot when it can be read (<see cref="CompletionDateFile"/>);
    /// otherwise the snapshot keeps what its own file says (no dates, or a 1.5 preview's inline ones).
    /// </param>
    private SharedRead<CharacterSnapshot> ReadShared(string path, out string? parseError, bool withDates = true)
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
            var snapshot = Normalized(migrated.Deserialize<CharacterSnapshot>(StorageJson.Options)
                ?? throw new InvalidDataException("Snapshot deserialized to null."));

            // Builds before 1.4.2 saved the raw rank byte, with the "ranked up today" bit set on a rank-up day.
            snapshot = snapshot.WithMaskedTribeRanks();
            if (withDates)
            {
                var dates = CompletionDateFile.Read(CompletionDateFile.PathFor(charactersDir, snapshot.ContentId), snapshot.ContentId);
                if (dates.Value is { } loaded)
                {
                    snapshot = CompletionDateFile.AttachTo(snapshot, loaded);
                }
            }

            return SharedRead<CharacterSnapshot>.Of(snapshot);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Whatever a hand-edited or damaged file makes the reader throw (a null list a migration or repair then walks
            // included) is that file's problem, never the caller's: one bad file must not stop a folder scan.
            parseError = ex.Message;
            return new SharedRead<CharacterSnapshot>(SharedLoad.Invalid, null, $"Snapshot {fileName} could not be parsed and was left in place: {ex.Message}");
        }
    }

    /// <summary>
    /// A deserialized snapshot with every collection a hand-edited or damaged file wrote as null (<c>"tribes": null</c>)
    /// read as empty, as <see cref="CharacterSettings.Normalize"/> does for the settings: the model's readers never meet
    /// a null list. This instance when nothing is null.
    /// </summary>
    internal static CharacterSnapshot Normalized(CharacterSnapshot snapshot)
    {
        // The properties are declared non-null, but the deserializer writes a JSON null into them as it is.
        if (snapshot.Name is not null && snapshot.CompletedBits is not null && snapshot.Accepted is not null && snapshot.DailyDone is not null
            && snapshot.RepeatFlags is not null && snapshot.JobLevels is not null && snapshot.GcRanks is not null && snapshot.Tribes is not null
            && snapshot.UnlockedInstances is not null && snapshot.ActiveFestivals is not null && snapshot.ActiveFestivalPhases is not null
            && snapshot.SatisfactionRanks is not null && snapshot.CompletedAchievements is not null && snapshot.JobItemLevels is not null
            && snapshot.Collectibles is not null && snapshot.CompletedUtc is not null && snapshot.CompletedAfterUtc is not null
            && snapshot.TriadRecords is null or { Beaten: not null, Cards: not null }
            && snapshot.DutyRecords is null or { Unlocked: not null, Cleared: not null }
            && snapshot.GateItems is null or { Equipped: not null, Held: not null })
        {
            return snapshot;
        }

        return snapshot with
        {
            Name = snapshot.Name ?? string.Empty,
            CompletedBits = snapshot.CompletedBits ?? [],
            Accepted = snapshot.Accepted ?? [],
            DailyDone = snapshot.DailyDone ?? new Dictionary<ushort, byte>(),
            RepeatFlags = snapshot.RepeatFlags ?? [],
            JobLevels = snapshot.JobLevels ?? new Dictionary<byte, short>(),
            GcRanks = snapshot.GcRanks ?? [],
            Tribes = snapshot.Tribes ?? new Dictionary<byte, TribeStanding>(),
            UnlockedInstances = snapshot.UnlockedInstances ?? [],
            ActiveFestivals = snapshot.ActiveFestivals ?? [],
            ActiveFestivalPhases = snapshot.ActiveFestivalPhases ?? [],
            SatisfactionRanks = snapshot.SatisfactionRanks ?? new Dictionary<byte, byte>(),
            CompletedAchievements = snapshot.CompletedAchievements ?? [],
            JobItemLevels = snapshot.JobItemLevels ?? new Dictionary<byte, ushort>(),
            Collectibles = snapshot.Collectibles ?? new Dictionary<string, CollectibleSet>(),
            CompletedUtc = snapshot.CompletedUtc ?? new Dictionary<ushort, DateTime>(),
            CompletedAfterUtc = snapshot.CompletedAfterUtc ?? new Dictionary<ushort, DateTime>(),
            TriadRecords = snapshot.TriadRecords?.Normalized(),
            DutyRecords = snapshot.DutyRecords is { } duties && (duties.Unlocked is null || duties.Cleared is null)
                ? duties with { Unlocked = duties.Unlocked ?? [], Cleared = duties.Cleared ?? [] }
                : snapshot.DutyRecords,
            GateItems = snapshot.GateItems is { } items && (items.Equipped is null || items.Held is null)
                ? items with { Equipped = items.Equipped ?? [], Held = items.Held ?? [] }
                : snapshot.GateItems,
        };
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
