using System.Buffers;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// What changed between two captures of the same character, in the shape the incremental resolver wants.
/// Identity fields (name, world) and <see cref="CharacterSnapshot.TakenUtc"/> are ignored.
/// </summary>
/// <param name="ChangedQuestIds">Quest ids whose completion bit, journal presence, sequence, accepting job or daily-done flag changed; ascending, distinct.</param>
/// <param name="ChangedJobs">ClassJob ids whose unsynced level changed, appeared or disappeared; ascending.</param>
/// <param name="ChangedFestivals">Festival ids that started, ended or changed phase; ascending.</param>
/// <param name="OtherChanged">
/// Any other evaluation input changed (Grand Company or ranks, tribe standing, allowances, cleared duties, current job,
/// achievements, entitlement caps, custom delivery ranks, carrier level, repeat flags, the gear-gate weapons, content id). These touch quests the reverse
/// index cannot enumerate, so the caller resolves everything.
/// </param>
/// <param name="CollectiblesChanged">
/// The owned collectibles (<see cref="CharacterSnapshot.Collectibles"/>) changed: a new mount, minion, emote, …. The
/// capture is saved and published. Only the owned mounts feed a quest's state (<see cref="MountsChanged"/>); the other
/// kinds need nothing resolved.
/// </param>
/// <param name="MountsChanged">
/// The owned mounts changed (the <c>Mount</c> set of <see cref="CharacterSnapshot.Collectibles"/>; 1.11.0, C2). The
/// mounts a quest needs owned (<see cref="Model.QuestRecord.MountRequired"/>, the mount-collection gates, judged by
/// <see cref="Evaluation.MountCheck"/>) are not in the reverse index, so the caller resolves everything
/// (<see cref="FullPass.Needed"/>); a new mount is rare, so the full pass costs little.
/// </param>
public sealed record SnapshotDiff(
    IReadOnlyList<ushort> ChangedQuestIds,
    IReadOnlyList<byte> ChangedJobs,
    IReadOnlyList<ushort> ChangedFestivals,
    bool OtherChanged,
    bool CollectiblesChanged = false,
    bool MountsChanged = false)
{
    /// <summary>The <see cref="CharacterSnapshot.Collectibles"/> key the owned mounts are saved under.</summary>
    private static readonly string MountKind = RewardKind.Mount.ToString();

    public static readonly SnapshotDiff Empty = new([], [], [], false);

    /// <summary>Largest id list the order-insensitive compare sorts on the stack; longer ones borrow from the array pool.</summary>
    private const int StackSetLimit = 256;

    public bool IsEmpty => ChangedQuestIds.Count == 0 && ChangedJobs.Count == 0 && ChangedFestivals.Count == 0 && !OtherChanged && !CollectiblesChanged;

    /// <summary>Compares two snapshots. Either may carry a shorter completion bitmask; missing bytes read as zero.</summary>
    public static SnapshotDiff Compute(CharacterSnapshot old, CharacterSnapshot @new)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(@new);

        // Fast path for the common poll: nothing moved, so no sets are built. The list checks are order-sensitive,
        // which is fine: a reordered-but-equal capture merely falls through to the exact comparison below.
        if (SameMask(old.CompletedBits, @new.CompletedBits)
            && SameSequence(old.Accepted, @new.Accepted)
            && SameEntries(old.DailyDone, @new.DailyDone)
            && SameEntries(old.JobLevels, @new.JobLevels)
            && SameSequence(old.ActiveFestivals, @new.ActiveFestivals)
            && SameSequence(old.ActiveFestivalPhases, @new.ActiveFestivalPhases)
            && !OtherInputsChanged(old, @new)
            && Collectibles.Same(old.Collectibles, @new.Collectibles))
        {
            return Empty;
        }

        var quests = new SortedSet<ushort>();
        DiffCompletedBits(old.CompletedBits, @new.CompletedBits, quests);
        DiffAccepted(old.Accepted, @new.Accepted, quests);
        DiffKeyed(old.DailyDone, @new.DailyDone, quests);

        var jobs = new SortedSet<byte>();
        DiffKeyed(old.JobLevels, @new.JobLevels, jobs);

        // A festival counts as changed when it started, ended or moved to another phase (a phased event opens its
        // later chapters mid-run, and those quests must re-resolve); an unknown phase is a value of its own.
        var festivals = new SortedSet<ushort>();
        DiffKeyed(FestivalPhases(old), FestivalPhases(@new), festivals);

        var other = OtherInputsChanged(old, @new);
        var collectibles = !Collectibles.Same(old.Collectibles, @new.Collectibles);
        var mounts = collectibles && !SameMounts(old, @new);

        if (quests.Count == 0 && jobs.Count == 0 && festivals.Count == 0 && !other && !collectibles)
        {
            return Empty;
        }

        return new SnapshotDiff([.. quests], [.. jobs], [.. festivals], other, collectibles, mounts);
    }

    /// <summary>The same owned and missing mounts in both captures; a capture that read none is a value of its own.</summary>
    private static bool SameMounts(CharacterSnapshot old, CharacterSnapshot @new)
    {
        old.Collectibles.TryGetValue(MountKind, out var before);
        @new.Collectibles.TryGetValue(MountKind, out var now);
        return before is null ? now is null : before.SameIds(now);
    }

    private static bool OtherInputsChanged(CharacterSnapshot old, CharacterSnapshot @new) =>
        old.ContentId != @new.ContentId
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
        || old.LevelCap != @new.LevelCap
        || old.CarrierLevel != @new.CarrierLevel
        || !SameEntries(old.SatisfactionRanks, @new.SatisfactionRanks)
        || !SameSequence(old.RepeatFlags, @new.RepeatFlags)
        || !GateItemCapture.Same(old.GateItems, @new.GateItems);

    /// <summary>Running festivals by id with their phase, −1 when the capture holds none; the first entry of a repeated id wins.</summary>
    private static Dictionary<ushort, int> FestivalPhases(CharacterSnapshot s)
    {
        var result = new Dictionary<ushort, int>(s.ActiveFestivals.Count);
        for (var i = 0; i < s.ActiveFestivals.Count; i++)
        {
            result.TryAdd(s.ActiveFestivals[i], i < s.ActiveFestivalPhases.Count ? s.ActiveFestivalPhases[i] : -1);
        }

        return result;
    }

    /// <summary>Bitmask equality where a shorter mask reads as zero-padded.</summary>
    private static bool SameMask(byte[] a, byte[] b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        var shared = Math.Min(a.Length, b.Length);
        if (!a.AsSpan(0, shared).SequenceEqual(b.AsSpan(0, shared)))
        {
            return false;
        }

        var longer = a.Length > b.Length ? a : b;
        return !longer.AsSpan(shared).ContainsAnyExcept((byte)0);
    }

    private static bool SameSequence<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
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

    /// <summary>
    /// Journal entries by quest id, compared on step and accepting job: the fast path compares whole
    /// <see cref="AcceptedQuest"/> records, so anything it sees must count here too, or a capture that differs
    /// only in <see cref="AcceptedQuest.AcceptClassJob"/> would never commit and every later poll would take the slow path.
    /// </summary>
    private static void DiffAccepted(IReadOnlyList<AcceptedQuest> a, IReadOnlyList<AcceptedQuest> b, SortedSet<ushort> changed)
    {
        var old = new Dictionary<ushort, (byte Sequence, byte AcceptClassJob)>(a.Count);
        foreach (var q in a)
        {
            old[q.QuestId] = (q.Sequence, q.AcceptClassJob);
        }

        var current = new Dictionary<ushort, (byte Sequence, byte AcceptClassJob)>(b.Count);
        foreach (var q in b)
        {
            current[q.QuestId] = (q.Sequence, q.AcceptClassJob);
        }

        DiffKeyed(old, current, changed);
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

    /// <summary>
    /// Key-and-value equality of two maps. Captures and stored files hold <see cref="Dictionary{TKey, TValue}"/>
    /// instances, which are walked with the struct enumerator so the once-a-second fast path boxes nothing; any
    /// other implementation takes the interface enumerator.
    /// </summary>
    private static bool SameEntries<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> a, IReadOnlyDictionary<TKey, TValue> b)
        where TKey : notnull
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        if (a.Count == 0)
        {
            return true;
        }

        if (a is Dictionary<TKey, TValue> dictionary)
        {
            foreach (var (key, value) in dictionary)
            {
                if (!b.TryGetValue(key, out var other) || !EqualityComparer<TValue>.Default.Equals(value, other))
                {
                    return false;
                }
            }

            return true;
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

    /// <summary>
    /// Order-insensitive equality of two id lists without a heap allocation: captures list ids in a fixed order, so
    /// the element-wise compare answers the common poll; a reordered pair is compared as two sorted copies on the
    /// stack (or, past <see cref="StackSetLimit"/>, in a pooled array).
    /// </summary>
    private static bool SameSet(IReadOnlyList<uint> a, IReadOnlyList<uint> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        if (SameSequence(a, b))
        {
            return true;
        }

        var n = a.Count;
        uint[]? rented = null;
        var buffer = n <= StackSetLimit
            ? stackalloc uint[2 * n]
            : (rented = ArrayPool<uint>.Shared.Rent(2 * n)).AsSpan(0, 2 * n);
        try
        {
            var left = buffer[..n];
            var right = buffer[n..];
            for (var i = 0; i < n; i++)
            {
                left[i] = a[i];
                right[i] = b[i];
            }

            left.Sort();
            right.Sort();
            return left.SequenceEqual(right);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<uint>.Shared.Return(rented);
            }
        }
    }
}
