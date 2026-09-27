using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// What changed between two captures of the same character, in the shape the incremental resolver wants.
/// Identity fields (name, world) and <see cref="CharacterSnapshot.TakenUtc"/> are ignored.
/// </summary>
/// <param name="ChangedQuestIds">Quest ids whose completion bit, journal presence, sequence or daily-done flag changed; ascending, distinct.</param>
/// <param name="ChangedJobs">ClassJob ids whose unsynced level changed, appeared or disappeared; ascending.</param>
/// <param name="ChangedFestivals">Festival ids that started or ended; ascending.</param>
/// <param name="OtherChanged">
/// Any other evaluation input changed (Grand Company or ranks, tribe standing, allowances, cleared duties, current job,
/// achievements, entitlement caps, content id). These touch quests the reverse index cannot enumerate, so the caller
/// resolves everything.
/// </param>
public sealed record SnapshotDiff(
    IReadOnlyList<ushort> ChangedQuestIds,
    IReadOnlyList<byte> ChangedJobs,
    IReadOnlyList<ushort> ChangedFestivals,
    bool OtherChanged)
{
    public static readonly SnapshotDiff Empty = new([], [], [], false);

    public bool IsEmpty => ChangedQuestIds.Count == 0 && ChangedJobs.Count == 0 && ChangedFestivals.Count == 0 && !OtherChanged;

    /// <summary>Compares two snapshots. Either may carry a shorter completion bitmask; missing bytes read as zero.</summary>
    public static SnapshotDiff Compute(CharacterSnapshot old, CharacterSnapshot @new)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(@new);

        var quests = new SortedSet<ushort>();
        DiffCompletedBits(old.CompletedBits, @new.CompletedBits, quests);
        DiffAccepted(old.Accepted, @new.Accepted, quests);
        DiffKeyed(old.DailyDone, @new.DailyDone, quests);

        var jobs = new SortedSet<byte>();
        DiffKeyed(old.JobLevels, @new.JobLevels, jobs);

        var festivals = new SortedSet<ushort>(old.ActiveFestivals);
        festivals.SymmetricExceptWith(@new.ActiveFestivals);

        var other = old.ContentId != @new.ContentId
            || old.GrandCompany != @new.GrandCompany
            || !old.GcRanks.AsSpan().SequenceEqual(@new.GcRanks)
            || !SameEntries(old.Tribes, @new.Tribes)
            || old.TribeAllowance != @new.TribeAllowance
            || old.LeveAllowance != @new.LeveAllowance
            || !SameSet(old.UnlockedInstances, @new.UnlockedInstances)
            || old.CurrentJob != @new.CurrentJob
            || old.AchievementsLoaded != @new.AchievementsLoaded
            || !SameSet(old.CompletedAchievements, @new.CompletedAchievements)
            || old.MaxExpansion != @new.MaxExpansion
            || old.LevelCap != @new.LevelCap;

        if (quests.Count == 0 && jobs.Count == 0 && festivals.Count == 0 && !other)
        {
            return Empty;
        }

        return new SnapshotDiff([.. quests], [.. jobs], [.. festivals], other);
    }

    private static void DiffCompletedBits(byte[] a, byte[] b, SortedSet<ushort> changed)
    {
        var length = Math.Max(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            var x = (byte)((i < a.Length ? a[i] : 0) ^ (i < b.Length ? b[i] : 0));
            if (x == 0)
            {
                continue;
            }

            for (var bit = 0; bit < 8; bit++)
            {
                if ((x & (1 << bit)) != 0)
                {
                    var id = (i << 3) | bit;
                    if (id <= ushort.MaxValue)
                    {
                        changed.Add((ushort)id);
                    }
                }
            }
        }
    }

    private static void DiffAccepted(IReadOnlyList<AcceptedQuest> a, IReadOnlyList<AcceptedQuest> b, SortedSet<ushort> changed)
    {
        var oldSeq = new Dictionary<ushort, byte>(a.Count);
        foreach (var q in a)
        {
            oldSeq[q.QuestId] = q.Sequence;
        }

        var newSeq = new Dictionary<ushort, byte>(b.Count);
        foreach (var q in b)
        {
            newSeq[q.QuestId] = q.Sequence;
        }

        DiffKeyed(oldSeq, newSeq, changed);
    }

    /// <summary>Adds every key present in one map only, or present in both with different values.</summary>
    private static void DiffKeyed<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> a, IReadOnlyDictionary<TKey, TValue> b, SortedSet<TKey> changed)
        where TKey : notnull
    {
        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !EqualityComparer<TValue>.Default.Equals(value, other))
            {
                changed.Add(key);
            }
        }

        foreach (var key in b.Keys)
        {
            if (!a.ContainsKey(key))
            {
                changed.Add(key);
            }
        }
    }

    private static bool SameEntries<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> a, IReadOnlyDictionary<TKey, TValue> b)
        where TKey : notnull
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !EqualityComparer<TValue>.Default.Equals(value, other))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameSet<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        var set = new HashSet<T>(a);
        foreach (var item in b)
        {
            if (!set.Contains(item))
            {
                return false;
            }
        }

        return true;
    }
}
