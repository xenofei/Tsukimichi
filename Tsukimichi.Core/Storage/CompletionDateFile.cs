using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// A character's quest completion dates (decision 9) in their own file beside the snapshot:
/// <c>characters/&lt;ContentId&gt;.dates.json</c>. They used to ride in the snapshot file, where a 1.4 build or a
/// downgrade rewriting the snapshot silently dropped them, and dates cannot be rebuilt. The file holds the dates, the
/// completion bits at the start of recording (the quests that never get a date) and the capture the dates were last
/// carried to (its time and completion bits), so a first pass dates the quests completed since without reading the
/// snapshot, even one another build rewrote meanwhile. Written through <see cref="AtomicFile"/> by
/// <see cref="JsonSnapshotStore.Save"/> before the snapshot itself; removed with the character's other files
/// (<see cref="Runtime.CharacterSidecars"/>).
/// </summary>
public static class CompletionDateFile
{
    public const string FileSuffix = ".dates.json";

    /// <summary>The file version this build writes; a higher one was written by a newer build and is left alone.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The dates file of one character, beside <c>&lt;ContentId&gt;.json</c> in the characters directory.</summary>
    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
    }

    /// <summary>
    /// Writes the dates <paramref name="snapshot"/> carries, with it as the basis (its time and completion bits).
    /// Throws when it records no dates (<see cref="CharacterSnapshot.CompletionDatesSinceUtc"/> null): a snapshot
    /// without dates must never replace a dates file.
    /// </summary>
    public static void Save(string path, CharacterSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CompletionDatesSinceUtc is not { } since)
        {
            throw new ArgumentException("The snapshot records no completion dates.", nameof(snapshot));
        }

        var data = new DateFileData
        {
            Version = CurrentVersion,
            SinceUtc = since,
            BeforeBits = snapshot.CompletedBeforeBits ?? Runtime.CompletionDates.LegacyBefore(snapshot),
            BasisUtc = snapshot.TakenUtc,
            BasisBits = snapshot.CompletedBits,
            CompletedUtc = new Dictionary<ushort, DateTime>(snapshot.CompletedUtc),
            CompletedAfterUtc = new Dictionary<ushort, DateTime>(snapshot.CompletedAfterUtc),
        };
        AtomicFile.Write(path, JsonSerializer.Serialize(data, StorageJson.Options));
    }

    /// <summary>
    /// Reads a dates file without touching it. A loaded value is a snapshot of <paramref name="contentId"/> carrying
    /// only the dates and their basis: <see cref="CharacterSnapshot.TakenUtc"/> and
    /// <see cref="CharacterSnapshot.CompletedBits"/> are the capture the dates were last carried to, which is what
    /// <see cref="Runtime.CompletionDates.Begin"/> continues from. <see cref="SharedRead{T}.Problem"/> says what went
    /// wrong otherwise.
    /// </summary>
    public static SharedRead<CharacterSnapshot> Read(string path, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            return ioError is null
                ? SharedRead<CharacterSnapshot>.Missing
                : new SharedRead<CharacterSnapshot>(SharedLoad.Unreadable, null, $"Completion dates {fileName} could not be read and were left in place: {ioError}");
        }

        try
        {
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                ?? throw new InvalidDataException("The file is not a JSON object.");
            var version = root.TryGetPropertyValue("version", out var node) && node is JsonValue value && value.TryGetValue<int>(out var v) ? v : 0;
            if (version > CurrentVersion)
            {
                return new SharedRead<CharacterSnapshot>(
                    SharedLoad.Newer,
                    null,
                    $"Completion dates {fileName} were saved by a newer version of Tsukimichi (version {version}, this one reads up to {CurrentVersion}); they are left in place and not recorded this session.");
            }

            var data = root.Deserialize<DateFileData>(StorageJson.Options) ?? throw new InvalidDataException("The file deserialized to null.");
            if (data.SinceUtc == default)
            {
                throw new InvalidDataException("The file has no start date.");
            }

            return SharedRead<CharacterSnapshot>.Of(new CharacterSnapshot
            {
                ContentId = contentId,
                TakenUtc = Utc(data.BasisUtc),
                CompletedBits = data.BasisBits ?? [],
                CompletionDatesSinceUtc = Utc(data.SinceUtc),
                CompletedBeforeBits = data.BeforeBits ?? [],
                CompletedUtc = Normalize(data.CompletedUtc),
                CompletedAfterUtc = Normalize(data.CompletedAfterUtc),
            });
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException)
        {
            return new SharedRead<CharacterSnapshot>(SharedLoad.Invalid, null, $"Completion dates {fileName} could not be parsed: {ex.Message}");
        }
    }

    /// <summary><paramref name="snapshot"/> with the dates of <paramref name="dates"/> (a <see cref="Read"/> result); its own state is kept.</summary>
    public static CharacterSnapshot AttachTo(CharacterSnapshot snapshot, CharacterSnapshot dates)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(dates);
        return snapshot with
        {
            CompletionDatesSinceUtc = dates.CompletionDatesSinceUtc,
            CompletedBeforeBits = dates.CompletedBeforeBits,
            CompletedUtc = dates.CompletedUtc,
            CompletedAfterUtc = dates.CompletedAfterUtc,
        };
    }

    /// <summary><paramref name="snapshot"/> without its dates, as the snapshot file is written; the same instance when it has none.</summary>
    public static CharacterSnapshot Strip(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CompletionDatesSinceUtc is null && snapshot.CompletedBeforeBits is null && snapshot.CompletedUtc.Count == 0 && snapshot.CompletedAfterUtc.Count == 0)
        {
            return snapshot;
        }

        return snapshot with
        {
            CompletionDatesSinceUtc = null,
            CompletedBeforeBits = null,
            CompletedUtc = new Dictionary<ushort, DateTime>(),
            CompletedAfterUtc = new Dictionary<ushort, DateTime>(),
        };
    }

    private static Dictionary<ushort, DateTime> Normalize(Dictionary<ushort, DateTime>? times)
    {
        var result = new Dictionary<ushort, DateTime>(times?.Count ?? 0);
        if (times is not null)
        {
            foreach (var (questId, time) in times)
            {
                result[questId] = Utc(time);
            }
        }

        return result;
    }

    private static DateTime Utc(DateTime time) => time.Kind switch
    {
        DateTimeKind.Utc => time,
        DateTimeKind.Local => time.ToUniversalTime(),
        _ => DateTime.SpecifyKind(time, DateTimeKind.Utc),
    };

    /// <summary>The file's shape. Bit masks are written as base64, like the snapshot's own.</summary>
    internal sealed class DateFileData
    {
        public int Version { get; set; }
        public DateTime SinceUtc { get; set; }
        public byte[]? BeforeBits { get; set; }
        public DateTime BasisUtc { get; set; }
        public byte[]? BasisBits { get; set; }
        public Dictionary<ushort, DateTime>? CompletedUtc { get; set; }
        public Dictionary<ushort, DateTime>? CompletedAfterUtc { get; set; }
    }
}
