using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Runtime;

/// <summary>How much a quest's completion date says (<see cref="QuestCompletionDate"/>).</summary>
public enum CompletionDateKind
{
    /// <summary>The plugin saw the quest's completion bit turn on: it was completed at <see cref="QuestCompletionDate.Utc"/> (within a poll).</summary>
    Seen,

    /// <summary>
    /// Found completed at a login: it was completed between <see cref="QuestCompletionDate.AfterUtc"/> (the capture
    /// before) and <see cref="QuestCompletionDate.Utc"/>, while the plugin was not watching.
    /// </summary>
    By,

    /// <summary>Already completed when dates started being recorded (<see cref="QuestCompletionDate.Utc"/>): no date is known.</summary>
    Before,
}

/// <summary>What is known about when a completed quest was completed.</summary>
public readonly record struct QuestCompletionDate(CompletionDateKind Kind, DateTime Utc, DateTime? AfterUtc = null);

/// <summary>
/// Quest completion dates (decision 9), carried on the snapshot (<see cref="CharacterSnapshot.CompletedUtc"/>,
/// <see cref="CharacterSnapshot.CompletedAfterUtc"/>, <see cref="CharacterSnapshot.CompletionDatesSinceUtc"/>,
/// <see cref="CharacterSnapshot.CompletedBeforeBits"/>) and saved in their own file beside it
/// (<see cref="CompletionDateFile"/>), so a stored character and other game clients have them too. Nothing is guessed:
/// a quest already complete at the first capture that records dates gets none ("before" that capture), a quest seen
/// completing gets the capture's time, and a quest found completed at a login gets the login's time with the previous
/// capture's time as "after". A date, once set, is kept, and a quest complete before recording started never gets
/// one: a seasonal quest whose bit the game clears keeps its first date (or stays "before"), and a capture that
/// briefly reads fewer quests (a client still loading, a chapter the game clears for a replay) cannot stamp anything
/// when they come back.
/// </summary>
public static class CompletionDates
{
    /// <summary>
    /// The first capture of a session (or of a catalog): the dates carried over from <paramref name="stored"/> (the
    /// character's dates file as <see cref="CompletionDateFile.Read"/> returns it, or the last capture of this
    /// session), and the quests completed since it stamped with this capture's time and, as "after", the stored
    /// capture's time. With nothing stored, or nothing recorded in it, recording starts now with no dates.
    /// </summary>
    public static CharacterSnapshot Begin(CharacterSnapshot? stored, CharacterSnapshot capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var now = Utc(capture.TakenUtc);
        if (stored is null || stored.ContentId != capture.ContentId || stored.CompletionDatesSinceUtc is not { } since)
        {
            return capture with
            {
                CompletionDatesSinceUtc = now,
                CompletedBeforeBits = (byte[])capture.CompletedBits.Clone(),
                CompletedUtc = new Dictionary<ushort, DateTime>(),
                CompletedAfterUtc = new Dictionary<ushort, DateTime>(),
            };
        }

        var before = stored.CompletedBeforeBits ?? LegacyBefore(stored);
        var completed = new Dictionary<ushort, DateTime>(stored.CompletedUtc);
        var after = new Dictionary<ushort, DateTime>(stored.CompletedAfterUtc);
        var storedTaken = Utc(stored.TakenUtc);
        foreach (var questId in NewlyCompleted(stored.CompletedBits, capture.CompletedBits))
        {
            if (!IsSet(before, questId) && completed.TryAdd(questId, now))
            {
                after[questId] = storedTaken;
            }
        }

        return capture with
        {
            CompletionDatesSinceUtc = Utc(since),
            CompletedBeforeBits = before,
            CompletedUtc = completed,
            CompletedAfterUtc = after,
        };
    }

    /// <summary>
    /// A first pass that continues from this session's own last capture of the character (<paramref name="last"/>:
    /// the catalog was rebuilt, or the save of that capture had not landed yet) rather than from the file:
    /// <see cref="Begin"/> from it, except that a session recording no dates (a newer build's dates file) stays without.
    /// </summary>
    public static CharacterSnapshot Resume(CharacterSnapshot last, CharacterSnapshot capture)
    {
        ArgumentNullException.ThrowIfNull(last);
        ArgumentNullException.ThrowIfNull(capture);
        return last.ContentId == capture.ContentId && last.CompletionDatesSinceUtc is null
            ? CompletionDateFile.Strip(capture)
            : Begin(last, capture);
    }

    /// <summary>
    /// <see cref="Begin"/> from a read of the character's dates file, for the first pass. A missing file starts
    /// recording now; a file that could not be parsed (quarantined by the store) starts over too, with a line in
    /// <paramref name="warnings"/>; a file a newer build wrote is left alone and nothing is recorded this session (the
    /// capture comes back without dates, so no save replaces that file). A file that exists but cannot be read right
    /// now throws <see cref="IOException"/>: starting over would save an empty record over dates that cannot be
    /// rebuilt, so the first pass fails and is retried.
    /// </summary>
    public static CharacterSnapshot BeginFrom(SharedRead<CharacterSnapshot> stored, CharacterSnapshot capture, IList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(warnings);
        switch (stored.Status)
        {
            case SharedLoad.Loaded when stored.Value is not null:
                return Begin(stored.Value, capture);

            case SharedLoad.Missing:
                return Begin(null, capture);

            case SharedLoad.Invalid:
                warnings.Add((stored.Problem ?? "Completion dates could not be parsed") + "; recording starts over");
                return Begin(null, capture);

            case SharedLoad.Newer:
                warnings.Add(stored.Problem ?? "Completion dates were saved by a newer version of Tsukimichi; they are not recorded this session");
                return CompletionDateFile.Strip(capture);

            default:
                throw new IOException(stored.Problem ?? "Completion dates could not be read");
        }
    }

    /// <summary>
    /// A later capture of the same character: the previous capture's dates carried over, and every quest whose bit
    /// turned on since stamped with this capture's time, unless it already has a date or was complete when recording
    /// started. An unchanged completion mask (the usual poll) shares the previous maps, so the diff compares them by
    /// reference. A previous capture that records no dates (a newer build's dates file) keeps the session without.
    /// </summary>
    public static CharacterSnapshot Carry(CharacterSnapshot previous, CharacterSnapshot capture)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(capture);
        if (previous.ContentId != capture.ContentId)
        {
            return Begin(null, capture);
        }

        if (previous.CompletionDatesSinceUtc is not { } since)
        {
            return CompletionDateFile.Strip(capture);
        }

        var before = previous.CompletedBeforeBits;
        if (ReferenceEquals(previous.CompletedBits, capture.CompletedBits))
        {
            return capture with
            {
                CompletionDatesSinceUtc = since,
                CompletedBeforeBits = before,
                CompletedUtc = previous.CompletedUtc,
                CompletedAfterUtc = previous.CompletedAfterUtc,
            };
        }

        Dictionary<ushort, DateTime>? completed = null;
        var now = Utc(capture.TakenUtc);
        foreach (var questId in NewlyCompleted(previous.CompletedBits, capture.CompletedBits))
        {
            if (!previous.CompletedUtc.ContainsKey(questId) && (before is null || !IsSet(before, questId)))
            {
                completed ??= new Dictionary<ushort, DateTime>(previous.CompletedUtc);
                completed[questId] = now;
            }
        }

        return capture with
        {
            CompletionDatesSinceUtc = since,
            CompletedBeforeBits = before,
            CompletedUtc = completed ?? previous.CompletedUtc,
            CompletedAfterUtc = previous.CompletedAfterUtc,
        };
    }

    /// <summary>
    /// When <paramref name="questId"/> was completed, as far as <paramref name="snapshot"/> knows; null when the quest is
    /// not completed, the snapshot records no dates (a file from before 1.5), or it was completed while no build that
    /// records dates was watching and no capture since could place it (an older build played the character meanwhile).
    /// </summary>
    public static QuestCompletionDate? For(CharacterSnapshot? snapshot, ushort questId)
    {
        if (snapshot is null || !snapshot.IsCompleted(questId))
        {
            return null;
        }

        if (snapshot.CompletedUtc.TryGetValue(questId, out var at))
        {
            return snapshot.CompletedAfterUtc.TryGetValue(questId, out var after)
                ? new QuestCompletionDate(CompletionDateKind.By, Utc(at), Utc(after))
                : new QuestCompletionDate(CompletionDateKind.Seen, Utc(at));
        }

        if (snapshot.CompletionDatesSinceUtc is not { } since)
        {
            return null;
        }

        return snapshot.CompletedBeforeBits is not { } before || IsSet(before, questId)
            ? new QuestCompletionDate(CompletionDateKind.Before, Utc(since))
            : null;
    }

    /// <summary>
    /// The quests complete before recording started, for dates that do not say (a 1.5 preview kept them inline in the
    /// snapshot): every completed quest of <paramref name="snapshot"/> without a date.
    /// </summary>
    public static byte[] LegacyBefore(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var before = (byte[])snapshot.CompletedBits.Clone();
        foreach (var questId in snapshot.CompletedUtc.Keys)
        {
            var index = questId >> 3;
            if (index < before.Length)
            {
                before[index] &= (byte)~(1 << (questId & 7));
            }
        }

        return before;
    }

    private static bool IsSet(byte[] bits, ushort questId)
    {
        var index = questId >> 3;
        return index < bits.Length && (bits[index] & (1 << (questId & 7))) != 0;
    }

    /// <summary>Quest ids whose bit is set in <paramref name="current"/> and not in <paramref name="previous"/>; a shorter mask reads as zero.</summary>
    private static List<ushort> NewlyCompleted(byte[] previous, byte[] current)
    {
        var result = new List<ushort>();
        for (var i = 0; i < current.Length; i++)
        {
            var added = (byte)(current[i] & ~(i < previous.Length ? previous[i] : 0));
            if (added == 0)
            {
                continue;
            }

            for (var bit = 0; bit < 8; bit++)
            {
                if ((added & (1 << bit)) != 0)
                {
                    var id = (i << 3) | bit;
                    if (id <= ushort.MaxValue)
                    {
                        result.Add((ushort)id);
                    }
                }
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
}
