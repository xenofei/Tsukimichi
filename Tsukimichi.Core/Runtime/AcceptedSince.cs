using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// When each accepted quest entered the journal, kept per character in a sidecar next to the snapshot
/// (<c>characters/&lt;ContentId&gt;.accepted.json</c>, runtime quest id to UTC time) so the snapshot schema stays
/// untouched. The poller maintains it from each <see cref="SnapshotDiff"/>: a quest that enters the journal gets the
/// current time, one that leaves is dropped, and a step change refreshes the time (the quest was worked on). It is
/// derived data: a file that cannot be read starts over empty and is rewritten on the next change.
/// </summary>
public static class AcceptedSince
{
    public const string FileSuffix = ".accepted.json";

    /// <summary>The sidecar for one character, beside <c>&lt;ContentId&gt;.json</c> in the characters directory.</summary>
    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
    }

    /// <summary>
    /// Reads a sidecar. A missing file yields an empty map; an unreadable or unparseable one yields an empty map and
    /// one line in <paramref name="warnings"/>, and is left in place to be overwritten. Times are normalized to UTC.
    /// </summary>
    public static Dictionary<ushort, DateTime> Load(string path, IList<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings?.Add($"{fileName} could not be read; accepted times start over: {ioError}");
            }

            return [];
        }

        Dictionary<ushort, DateTime>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<ushort, DateTime>>(text, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            warnings?.Add($"{fileName} could not be parsed; accepted times start over: {ex.Message}");
            return [];
        }

        if (parsed is null)
        {
            return [];
        }

        var result = new Dictionary<ushort, DateTime>(parsed.Count);
        foreach (var (questId, time) in parsed)
        {
            result[questId] = time.Kind switch
            {
                DateTimeKind.Utc => time,
                DateTimeKind.Local => time.ToUniversalTime(),
                _ => DateTime.SpecifyKind(time, DateTimeKind.Utc),
            };
        }

        return result;
    }

    public static void Save(string path, IReadOnlyDictionary<ushort, DateTime> since)
    {
        ArgumentNullException.ThrowIfNull(since);
        AtomicFile.Write(path, JsonSerializer.Serialize(since, StorageJson.Options));
    }

    /// <summary>
    /// Aligns the map with a snapshot's journal when there is no earlier capture to diff against: quests in the
    /// journal without a time get <paramref name="nowUtc"/> (the earliest moment they are known to have been
    /// accepted), quests no longer in the journal are dropped. Returns whether anything changed.
    /// </summary>
    public static bool Reconcile(Dictionary<ushort, DateTime> since, CharacterSnapshot snapshot, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(since);
        ArgumentNullException.ThrowIfNull(snapshot);

        var accepted = new HashSet<ushort>(snapshot.Accepted.Count);
        foreach (var quest in snapshot.Accepted)
        {
            accepted.Add(quest.QuestId);
        }

        var changed = false;
        foreach (var questId in accepted)
        {
            if (since.TryAdd(questId, nowUtc))
            {
                changed = true;
            }
        }

        if (since.Count > accepted.Count)
        {
            List<ushort>? stale = null;
            foreach (var questId in since.Keys)
            {
                if (!accepted.Contains(questId))
                {
                    (stale ??= []).Add(questId);
                }
            }

            if (stale is not null)
            {
                foreach (var questId in stale)
                {
                    since.Remove(questId);
                }

                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Applies one poll's diff: for every changed quest id, a quest now in the journal that was not before, or whose
    /// step moved, is stamped <paramref name="nowUtc"/>; one that left the journal (turned in or abandoned) is dropped;
    /// a quest whose journal entry did not move keeps its time. Returns whether anything changed.
    /// </summary>
    public static bool Apply(Dictionary<ushort, DateTime> since, CharacterSnapshot old, CharacterSnapshot @new, SnapshotDiff diff, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(since);
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(@new);
        ArgumentNullException.ThrowIfNull(diff);

        if (diff.ChangedQuestIds.Count == 0)
        {
            return false;
        }

        var changed = false;
        foreach (var questId in diff.ChangedQuestIds)
        {
            if (Sequence(@new, questId) is not { } newSequence)
            {
                changed |= since.Remove(questId);
                continue;
            }

            var oldSequence = Sequence(old, questId);
            if (oldSequence is null || oldSequence != newSequence || !since.ContainsKey(questId))
            {
                since[questId] = nowUtc;
                changed = true;
            }
        }

        return changed;
    }

    private static byte? Sequence(CharacterSnapshot snapshot, ushort questId)
    {
        foreach (var quest in snapshot.Accepted)
        {
            if (quest.QuestId == questId)
            {
                return quest.Sequence;
            }
        }

        return null;
    }
}
