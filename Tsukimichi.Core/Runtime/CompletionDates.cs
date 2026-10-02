using Tsukimichi.Core.Model;

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
/// Quest completion dates (decision 9), kept in the snapshot (<see cref="CharacterSnapshot.CompletedUtc"/>,
/// <see cref="CharacterSnapshot.CompletedAfterUtc"/>, <see cref="CharacterSnapshot.CompletionDatesSinceUtc"/>) so a
/// stored character and other game clients have them too. Nothing is guessed: a quest already complete at the first
/// capture that records dates gets none ("before" that capture), a quest seen completing gets the capture's time, and a
/// quest found completed at a login gets the login's time with the previous capture's time as "after". A date, once
/// set, is kept: a seasonal quest whose bit the game clears keeps its first date, and a capture that briefly reads
/// fewer quests (a client still loading) cannot restamp anything when they come back.
/// </summary>
public static class CompletionDates
{
    /// <summary>
    /// The first capture of a session (or of a catalog): the dates carried over from the character's stored snapshot,
    /// and the quests completed since it stamped with this capture's time and, as "after", the stored capture's time.
    /// With nothing stored, or a file from before dates were recorded, recording starts now with no dates.
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
                CompletedUtc = new Dictionary<ushort, DateTime>(),
                CompletedAfterUtc = new Dictionary<ushort, DateTime>(),
            };
        }

        var completed = new Dictionary<ushort, DateTime>(stored.CompletedUtc);
        var after = new Dictionary<ushort, DateTime>(stored.CompletedAfterUtc);
        var storedTaken = Utc(stored.TakenUtc);
        foreach (var questId in NewlyCompleted(stored.CompletedBits, capture.CompletedBits))
        {
            if (completed.TryAdd(questId, now))
            {
                after[questId] = storedTaken;
            }
        }

        return capture with { CompletionDatesSinceUtc = Utc(since), CompletedUtc = completed, CompletedAfterUtc = after };
    }

    /// <summary>
    /// A later capture of the same character: the previous capture's dates carried over, and every quest whose bit
    /// turned on since stamped with this capture's time. An unchanged completion mask (the usual poll) shares the
    /// previous maps, so the diff compares them by reference.
    /// </summary>
    public static CharacterSnapshot Carry(CharacterSnapshot previous, CharacterSnapshot capture)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(capture);
        if (previous.ContentId != capture.ContentId)
        {
            return Begin(null, capture);
        }

        var since = previous.CompletionDatesSinceUtc ?? Utc(capture.TakenUtc);
        if (ReferenceEquals(previous.CompletedBits, capture.CompletedBits))
        {
            return capture with
            {
                CompletionDatesSinceUtc = since,
                CompletedUtc = previous.CompletedUtc,
                CompletedAfterUtc = previous.CompletedAfterUtc,
            };
        }

        Dictionary<ushort, DateTime>? completed = null;
        var now = Utc(capture.TakenUtc);
        foreach (var questId in NewlyCompleted(previous.CompletedBits, capture.CompletedBits))
        {
            if (!previous.CompletedUtc.ContainsKey(questId))
            {
                completed ??= new Dictionary<ushort, DateTime>(previous.CompletedUtc);
                completed[questId] = now;
            }
        }

        return capture with
        {
            CompletionDatesSinceUtc = since,
            CompletedUtc = completed ?? previous.CompletedUtc,
            CompletedAfterUtc = previous.CompletedAfterUtc,
        };
    }

    /// <summary>
    /// When <paramref name="questId"/> was completed, as far as <paramref name="snapshot"/> knows; null when the quest is
    /// not completed or the snapshot records no dates (a file from before 1.5).
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

        return snapshot.CompletionDatesSinceUtc is { } since ? new QuestCompletionDate(CompletionDateKind.Before, Utc(since)) : null;
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
