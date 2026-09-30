using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// One quest that left the journal without being completed (P10).
/// </summary>
/// <param name="QuestId">Runtime quest id (low 16 bits of the row id).</param>
/// <param name="AbandonedUtc">When the poller saw it leave the journal.</param>
/// <param name="Sequence">The journal sequence it had reached (<see cref="AcceptedQuest.Sequence"/>, 255 for the last step; 0 when unknown).</param>
/// <param name="StepCount">The quest's step count (<see cref="QuestRecord.StepCount"/>) when it was recorded; 0 when the sheet lists none.</param>
public sealed record AbandonedEntry(ushort QuestId, DateTime AbandonedUtc, byte Sequence, byte StepCount)
{
    /// <summary>Catalog row id of <see cref="QuestId"/>.</summary>
    public uint RowId => 0x10000u | QuestId;

    /// <summary>"step 3 of 5", "step 3" without a step count, or empty when the step was not known.</summary>
    public string StepText => Sequence == 0 ? string.Empty : BlockerText.StepText(Sequence, StepCount);
}

/// <summary>
/// The quests a character abandoned, kept per character in a sidecar beside the snapshot
/// (<c>characters/&lt;ContentId&gt;.abandoned.json</c>) the way <see cref="AcceptedSince"/> keeps accepted times, so
/// the snapshot schema stays untouched. The poller records an entry from each <see cref="QuestEventKind.Abandoned"/>
/// event with the step the quest had reached, and drops it when the quest is accepted again or completed. The game
/// keeps no such list, so this is the only record of a mis-click; a file that cannot be read starts over empty.
/// </summary>
public static class AbandonedLedger
{
    public const string FileSuffix = ".abandoned.json";

    /// <summary>The sidecar for one character, beside <c>&lt;ContentId&gt;.json</c> in the characters directory.</summary>
    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
    }

    /// <summary>
    /// Reads a ledger. A missing file yields an empty map; an unreadable or unparseable one yields an empty map and
    /// one line in <paramref name="warnings"/>, and is left in place to be overwritten. Times are normalized to UTC;
    /// entries without a quest id are skipped, and of two entries for one quest the later one wins.
    /// </summary>
    public static Dictionary<ushort, AbandonedEntry> Load(string path, IList<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings?.Add($"{fileName} could not be read; the abandoned list starts over: {ioError}");
            }

            return [];
        }

        LedgerFile? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LedgerFile>(text, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            warnings?.Add($"{fileName} could not be parsed; the abandoned list starts over: {ex.Message}");
            return [];
        }

        var result = new Dictionary<ushort, AbandonedEntry>();
        if (parsed?.Entries is not { } entries)
        {
            return result;
        }

        foreach (var entry in entries)
        {
            if (entry is null || entry.QuestId == 0)
            {
                continue;
            }

            var normalized = entry with { AbandonedUtc = AsUtc(entry.AbandonedUtc) };
            if (!result.TryGetValue(entry.QuestId, out var existing) || existing.AbandonedUtc <= normalized.AbandonedUtc)
            {
                result[entry.QuestId] = normalized;
            }
        }

        return result;
    }

    /// <summary>Writes the ledger atomically, newest first (then by quest id), under a version number.</summary>
    public static void Save(string path, IReadOnlyDictionary<ushort, AbandonedEntry> ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var file = new LedgerFile(LedgerFile.CurrentVersion, Newest(ledger));
        AtomicFile.Write(path, JsonSerializer.Serialize(file, StorageJson.Options));
    }

    /// <summary>The entries newest first, ties by quest id; what the dashboard lists and the file holds.</summary>
    public static List<AbandonedEntry> Newest(IReadOnlyDictionary<ushort, AbandonedEntry> ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var list = new List<AbandonedEntry>(ledger.Values);
        list.Sort(static (a, b) =>
        {
            var byTime = b.AbandonedUtc.CompareTo(a.AbandonedUtc);
            return byTime != 0 ? byTime : a.QuestId.CompareTo(b.QuestId);
        });
        return list;
    }

    /// <summary>
    /// Applies one poll's events: an <see cref="QuestEventKind.Abandoned"/> quest is recorded with the step it held
    /// in <paramref name="old"/>'s journal and its step count from <paramref name="catalog"/> (a later abandon of the
    /// same quest replaces the entry); an <see cref="QuestEventKind.Accepted"/> or <see cref="QuestEventKind.Completed"/>
    /// quest is dropped. Returns whether anything changed.
    /// </summary>
    public static bool Apply(Dictionary<ushort, AbandonedEntry> ledger, IReadOnlyList<QuestEvent> events, CharacterSnapshot old, QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(catalog);

        var changed = false;
        foreach (var e in events)
        {
            var questId = QuestRecord.ToQuestId(e.RowId);
            switch (e.Kind)
            {
                case QuestEventKind.Abandoned:
                    var stepCount = catalog.GetByRowId(e.RowId)?.StepCount ?? 0;
                    ledger[questId] = new AbandonedEntry(questId, AsUtc(e.TimeUtc), SequenceOf(old, questId), stepCount);
                    changed = true;
                    break;

                case QuestEventKind.Accepted:
                case QuestEventKind.Completed:
                    changed |= ledger.Remove(questId);
                    break;
            }
        }

        return changed;
    }

    /// <summary>
    /// Aligns the ledger with a snapshot when there is no earlier capture to diff against (the first pass after a
    /// login): a quest in the journal again or completed since is dropped. Returns whether anything changed.
    /// </summary>
    public static bool Reconcile(Dictionary<ushort, AbandonedEntry> ledger, CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (ledger.Count == 0)
        {
            return false;
        }

        List<ushort>? settled = null;
        foreach (var questId in ledger.Keys)
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
            ledger.Remove(questId);
        }

        return true;
    }

    /// <summary>"step 3 of 5 · 2 days ago", or only the age when the step is unknown.</summary>
    public static string Describe(AbandonedEntry entry, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var age = AgeText(nowUtc - entry.AbandonedUtc);
        var step = entry.StepText;
        return step.Length == 0 ? age : step + BlockerText.Separator + age;
    }

    /// <summary>"just now", "5 min ago", "3 h ago", "1 day ago", "12 days ago"; a negative age (clock change) reads "just now".</summary>
    public static string AgeText(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return ((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min ago";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return ((int)age.TotalHours).ToString(CultureInfo.InvariantCulture) + " h ago";
        }

        var days = (int)age.TotalDays;
        return days == 1 ? "1 day ago" : days.ToString(CultureInfo.InvariantCulture) + " days ago";
    }

    private static DateTime AsUtc(DateTime time) => time.Kind switch
    {
        DateTimeKind.Utc => time,
        DateTimeKind.Local => time.ToUniversalTime(),
        _ => DateTime.SpecifyKind(time, DateTimeKind.Utc),
    };

    private static byte SequenceOf(CharacterSnapshot snapshot, ushort questId)
    {
        foreach (var quest in snapshot.Accepted)
        {
            if (quest.QuestId == questId)
            {
                return quest.Sequence;
            }
        }

        return 0;
    }

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

    /// <summary>The file's shape: a version and the entries, newest first.</summary>
    private sealed record LedgerFile(int Version, List<AbandonedEntry>? Entries)
    {
        public const int CurrentVersion = 1;
    }
}
