using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Runtime;

/// <summary>Where the game showed a quest as available (feature plan v7, C1).</summary>
[Flags]
public enum OfferSource : byte
{
    None = 0,

    /// <summary>An "available quest" marker on the map or minimap (<c>Map.UnacceptedQuestMarkers</c>).</summary>
    Marker = 1,

    /// <summary>The quest offer window (<c>JournalAccept</c>) showed it.</summary>
    Offer = 2,
}

/// <summary>
/// One quest the game showed the character as available: when it was first and last seen, and where.
/// </summary>
/// <param name="QuestId">Runtime quest id (low 16 bits of the row id).</param>
/// <param name="FirstSeenUtc">The first time the game showed it.</param>
/// <param name="LastSeenUtc">The latest time it was seen, refreshed at most every <see cref="OfferSightings.Refresh"/>.</param>
/// <param name="Sources">Every place it was seen.</param>
public sealed record OfferSighting(ushort QuestId, DateTime FirstSeenUtc, DateTime LastSeenUtc, OfferSource Sources)
{
    /// <summary>Catalog row id of <see cref="QuestId"/>. Derived, so never written to the sidecar.</summary>
    [JsonIgnore]
    public uint RowId => 0x10000u | QuestId;
}

/// <summary>
/// "The game says available" (feature plan v7, C1): the quests the game itself showed a character as available, kept
/// per character in a sidecar beside the snapshot (<c>characters/&lt;ContentId&gt;.offers.json</c>), the way
/// <see cref="AbandonedLedger"/> keeps abandoned quests. The game's map markers and quest offer window are evidence
/// Tsukimichi's own computed state is checked against (<see cref="Diagnostics.GameOfferChecks"/>); a sighting never
/// changes a state. A sighting lasts until the quest is accepted or completed. Repeatable quests are not recorded:
/// their offer changes with the cycle, so a sighting would go stale by the next reset. A file that cannot be read
/// starts over empty.
/// </summary>
public static class OfferSightings
{
    public const string FileSuffix = ".offers.json";

    /// <summary>A quest seen on every poll moves its last-seen time at most this often, so the sidecar is not rewritten each poll.</summary>
    public static readonly TimeSpan Refresh = TimeSpan.FromMinutes(10);

    /// <summary>The sidecar for one character, beside <c>&lt;ContentId&gt;.json</c> in the characters directory.</summary>
    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
    }

    /// <summary>
    /// Reads a book. A missing file yields an empty map; an unreadable or unparseable one yields an empty map and one
    /// line in <paramref name="warnings"/>. Times are normalized to UTC; entries without a quest id or a source are
    /// skipped, and of two entries for one quest the sources merge and the widest span wins.
    /// </summary>
    public static Dictionary<ushort, OfferSighting> Load(string path, IList<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings?.Add($"{fileName} could not be read; the game's offers start over: {ioError}");
            }

            return [];
        }

        BookFile? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<BookFile>(text, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            warnings?.Add($"{fileName} could not be parsed; the game's offers start over: {ex.Message}");
            return [];
        }

        var result = new Dictionary<ushort, OfferSighting>();
        if (parsed?.Entries is not { } entries)
        {
            return result;
        }

        foreach (var entry in entries)
        {
            if (entry is null || entry.QuestId == 0 || entry.Sources == OfferSource.None)
            {
                continue;
            }

            var first = AsUtc(entry.FirstSeenUtc);
            var last = AsUtc(entry.LastSeenUtc);
            var normalized = entry with { FirstSeenUtc = first, LastSeenUtc = last < first ? first : last };
            result[entry.QuestId] = result.TryGetValue(entry.QuestId, out var existing) ? Merge(existing, normalized) : normalized;
        }

        return result;
    }

    /// <summary>Writes the book atomically, newest sighting first (then by quest id), under a version number.</summary>
    public static void Save(string path, IReadOnlyDictionary<ushort, OfferSighting> book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var file = new BookFile(BookFile.CurrentVersion, Newest(book));
        AtomicFile.Write(path, JsonSerializer.Serialize(file, StorageJson.Options));
    }

    /// <summary>The sightings, latest first, ties by quest id.</summary>
    public static List<OfferSighting> Newest(IReadOnlyDictionary<ushort, OfferSighting> book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var list = new List<OfferSighting>(book.Values);
        list.Sort(static (a, b) =>
        {
            var byTime = b.LastSeenUtc.CompareTo(a.LastSeenUtc);
            return byTime != 0 ? byTime : a.QuestId.CompareTo(b.QuestId);
        });
        return list;
    }

    /// <summary>
    /// Records what the game showed at <paramref name="nowUtc"/>: a quest row the catalog knows, that is not repeatable,
    /// not in <paramref name="snapshot"/>'s journal and not completed. A new quest or a new source always changes the
    /// book; a quest already known moves its last-seen time only once <see cref="Refresh"/> has passed. Returns
    /// whether anything changed.
    /// </summary>
    public static bool Record(
        Dictionary<ushort, OfferSighting> book,
        IEnumerable<uint> rowIds,
        OfferSource source,
        DateTime nowUtc,
        QuestCatalog catalog,
        CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(rowIds);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (source == OfferSource.None)
        {
            return false;
        }

        var now = AsUtc(nowUtc);
        var changed = false;
        foreach (var rowId in rowIds)
        {
            if (catalog.GetByRowId(rowId) is not { IsRepeatable: false } quest
                || snapshot.IsCompleted(quest.QuestId)
                || InJournal(snapshot, quest.QuestId))
            {
                continue;
            }

            if (!book.TryGetValue(quest.QuestId, out var known))
            {
                book[quest.QuestId] = new OfferSighting(quest.QuestId, now, now, source);
                changed = true;
                continue;
            }

            var sources = known.Sources | source;
            var moved = now - known.LastSeenUtc >= Refresh;
            if (sources != known.Sources || moved)
            {
                book[quest.QuestId] = known with { Sources = sources, LastSeenUtc = moved ? now : known.LastSeenUtc };
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Drops every sighting the character has moved past: the quest is in <paramref name="snapshot"/>'s journal or
    /// completed. Returns whether anything changed.
    /// </summary>
    public static bool Reconcile(Dictionary<ushort, OfferSighting> book, CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (book.Count == 0)
        {
            return false;
        }

        List<ushort>? settled = null;
        foreach (var questId in book.Keys)
        {
            if (snapshot.IsCompleted(questId) || InJournal(snapshot, questId))
            {
                (settled ??= []).Add(questId);
            }
        }

        if (settled is null)
        {
            return false;
        }

        foreach (var questId in settled)
        {
            book.Remove(questId);
        }

        return true;
    }

    /// <summary>A marker's objective id as a Quest sheet row id: a full row id (65536 and up) as it is, a runtime id with the row base added; 0 stays 0.</summary>
    public static uint MarkerRowId(uint objectiveId) => objectiveId switch
    {
        0 => 0,
        > ushort.MaxValue => objectiveId,
        _ => 0x10000u | objectiveId,
    };

    private static OfferSighting Merge(OfferSighting a, OfferSighting b) => a with
    {
        FirstSeenUtc = a.FirstSeenUtc <= b.FirstSeenUtc ? a.FirstSeenUtc : b.FirstSeenUtc,
        LastSeenUtc = a.LastSeenUtc >= b.LastSeenUtc ? a.LastSeenUtc : b.LastSeenUtc,
        Sources = a.Sources | b.Sources,
    };

    private static bool InJournal(CharacterSnapshot snapshot, ushort questId)
    {
        foreach (var quest in snapshot.Accepted)
        {
            if (quest.QuestId == questId)
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime AsUtc(DateTime time) => time.Kind switch
    {
        DateTimeKind.Utc => time,
        DateTimeKind.Local => time.ToUniversalTime(),
        _ => DateTime.SpecifyKind(time, DateTimeKind.Utc),
    };

    /// <summary>The file's shape: a version and the entries, latest first.</summary>
    private sealed record BookFile(int Version, List<OfferSighting>? Entries)
    {
        public const int CurrentVersion = 1;
    }
}
