using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// One backup per character (feature plan v5, 1.5.0 "Trust"; R4 proposal 8): <c>characters/&lt;ContentId&gt;.prev.json</c>,
/// a copy of the saved snapshot as it was before a save, refreshed at most once per <see cref="Interval"/>. Should a
/// bad capture ever be saved over good progress, the copy holds the character as it was up to a day earlier;
/// restoring is renaming it over <c>&lt;ContentId&gt;.json</c> with the game closed (docs/restore-backup.md). Written
/// through <see cref="AtomicFile"/> like the snapshot itself; a snapshot file that does not parse is never copied, so
/// a corrupt file cannot replace a good backup. Forgetting a character and Settings › Delete all data remove it with
/// the other per-character files (<see cref="Runtime.CharacterSidecars"/>).
/// </summary>
public static class SnapshotBackup
{
    public const string FileSuffix = ".prev.json";

    /// <summary>The least time between two refreshes of one character's backup.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>The backup of one character, beside <c>&lt;ContentId&gt;.json</c> in the characters directory.</summary>
    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
    }

    /// <summary>
    /// Whether the backup is due: none yet (<paramref name="backupWrittenUtc"/> null), or it was written at least
    /// <see cref="Interval"/> before <paramref name="nowUtc"/>. A backup stamped in the future (a clock moved back)
    /// is due too, or it would never be refreshed until the clock caught up.
    /// </summary>
    public static bool IsDue(DateTime? backupWrittenUtc, DateTime nowUtc) =>
        backupWrittenUtc is not { } written || nowUtc - written >= Interval || written > nowUtc + Interval;

    /// <summary>
    /// Before <paramref name="snapshotPath"/> is overwritten: copies it to <paramref name="backupPath"/> when the backup
    /// is due (<see cref="IsDue"/> on the backup's last write time) and the snapshot file holds a JSON object. Returns
    /// whether a copy was written. Read and write failures propagate to the caller, which decides whether a failed
    /// backup may hold up the save (it does not: <see cref="JsonSnapshotStore"/> logs and saves).
    /// </summary>
    public static bool RotateIfDue(string snapshotPath, string backupPath, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        DateTime? written = File.Exists(backupPath) ? File.GetLastWriteTimeUtc(backupPath) : null;
        if (!IsDue(written, nowUtc) || !File.Exists(snapshotPath))
        {
            return false;
        }

        var text = AtomicFile.Read(snapshotPath, out var error);
        if (text is null)
        {
            return error is null ? false : throw new IOException(error);
        }

        if (!IsJsonObject(text))
        {
            return false;
        }

        AtomicFile.Write(backupPath, text);
        return true;
    }

    private static bool IsJsonObject(string text)
    {
        try
        {
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
