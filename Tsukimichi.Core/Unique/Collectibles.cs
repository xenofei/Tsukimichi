using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Unique;

/// <summary>One reward the capture checks: its kind, its id in that kind's sheet and the item that grants it (0 for none).</summary>
public readonly record struct CollectibleTarget(RewardKind Kind, uint RewardId, uint ItemId);

/// <summary>
/// The collectible rewards whose owned state is saved in the snapshot (<see cref="CharacterSnapshot.Collectibles"/>,
/// decision 9): the kinds the client keeps an unlock flag for. Only the rewards the unique-reward data lists are
/// checked (<see cref="Targets"/>, about 900), not whole sheets, so the capture stays small and so does the file.
/// <see cref="Obtained"/> is the one rule every reader follows: the live flag when this client can read it, else what
/// the snapshot saved, else unknown.
/// </summary>
public static class Collectibles
{
    /// <summary>The kinds saved in the snapshot, in a fixed order.</summary>
    public static readonly IReadOnlyList<RewardKind> StoredKinds =
    [
        RewardKind.Emote,
        RewardKind.Minion,
        RewardKind.Mount,
        RewardKind.Orchestrion,
        RewardKind.Ornament,
        RewardKind.TripleTriadCard,
        RewardKind.Barding,
        RewardKind.Hairstyle,
        RewardKind.AetherCurrent,
        RewardKind.Instance,
        RewardKind.DutyUnlock,
    ];

    private static readonly FrozenSet<RewardKind> Stored = StoredKinds.ToFrozenSet();

    /// <summary>Whether <paramref name="kind"/> is read from an unlock flag and saved in the snapshot.</summary>
    public static bool IsStored(RewardKind kind) => Stored.Contains(kind);

    /// <summary>
    /// The rewards a capture checks: every shipped entry of a stored kind, plus the duties
    /// <c>curated/duty_unlocks.json</c> adds (the Moonlit catalog lists both). Distinct by kind and id, in kind then id
    /// order; the first entry's item wins.
    /// </summary>
    public static IReadOnlyList<CollectibleTarget> Targets(UniqueRewardsData data, CuratedData curated)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(curated);

        var seen = new Dictionary<(RewardKind, uint), uint>();
        foreach (var entry in data.Entries)
        {
            if (IsStored(entry.Kind) && entry.RewardId != 0)
            {
                seen.TryAdd((entry.Kind, entry.RewardId), entry.ItemId);
            }
        }

        foreach (var unlock in curated.DutyUnlocks.Values)
        {
            foreach (var cfc in unlock.ContentFinderConditionIds)
            {
                if (cfc != 0)
                {
                    seen.TryAdd((RewardKind.DutyUnlock, cfc), 0);
                }
            }
        }

        var targets = new List<CollectibleTarget>(seen.Count);
        foreach (var ((kind, id), item) in seen)
        {
            targets.Add(new CollectibleTarget(kind, id, item));
        }

        targets.Sort(static (a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.RewardId.CompareTo(b.RewardId));
        return targets;
    }

    /// <summary>
    /// The snapshot field for one read: per kind, the ids owned and the ids checked and missing. A target the client
    /// could not answer (null) is left out of both, so it reads unknown. Kinds with no answered target are left out.
    /// </summary>
    public static Dictionary<string, CollectibleSet> Read(IReadOnlyList<CollectibleTarget> targets, Func<CollectibleTarget, bool?> owned)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(owned);

        var byKind = new Dictionary<RewardKind, (List<uint> Owned, List<uint> Missing)>();
        foreach (var target in targets)
        {
            if (owned(target) is not { } has)
            {
                continue;
            }

            if (!byKind.TryGetValue(target.Kind, out var lists))
            {
                byKind[target.Kind] = lists = ([], []);
            }

            (has ? lists.Owned : lists.Missing).Add(target.RewardId);
        }

        var result = new Dictionary<string, CollectibleSet>(byKind.Count, StringComparer.Ordinal);
        foreach (var (kind, (have, missing)) in byKind)
        {
            have.Sort();
            missing.Sort();
            result[kind.ToString()] = new CollectibleSet { Owned = have, Missing = missing };
        }

        return result;
    }

    /// <summary>Same kinds with the same ids in each; order-insensitive. Reference-equal maps (an unchanged capture) answer at once.</summary>
    public static bool Same(IReadOnlyDictionary<string, CollectibleSet> a, IReadOnlyDictionary<string, CollectibleSet> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (kind, set) in a)
        {
            if (!b.TryGetValue(kind, out var other) || !set.SameIds(other))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The obtained answer every reader gives (live first, then stored, then unknown): the client's flag when
    /// <paramref name="canReadLive"/> (the viewed character is logged in here, on the framework thread) and the read
    /// answered; otherwise what the viewed snapshot saved (<paramref name="stored"/>, null when it saved nothing).
    /// </summary>
    public static bool? Obtained(bool canReadLive, Func<bool?> live, CollectibleLookup? stored, RewardKind kind, uint rewardId)
    {
        ArgumentNullException.ThrowIfNull(live);
        if (canReadLive && live() is { } read)
        {
            return read;
        }

        return stored?.Owns(kind, rewardId);
    }
}

/// <summary>
/// Owned answers from one snapshot's <see cref="CharacterSnapshot.Collectibles"/>, indexed once. Immutable; the caller
/// keeps one per snapshot instance (<see cref="For"/> returns null for a snapshot that saved none).
/// </summary>
public sealed class CollectibleLookup
{
    private readonly FrozenDictionary<(RewardKind Kind, uint Id), bool> answers;

    private CollectibleLookup(FrozenDictionary<(RewardKind, uint), bool> answers, DateTime asOfUtc)
    {
        this.answers = answers;
        AsOfUtc = asOfUtc;
    }

    /// <summary>When the snapshot was captured: what the answers are "as of".</summary>
    public DateTime AsOfUtc { get; }

    /// <summary>The lookup for <paramref name="snapshot"/>; null when it is null or saved no collectibles (a file from before 1.5).</summary>
    public static CollectibleLookup? For(CharacterSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Collectibles.Count == 0)
        {
            return null;
        }

        var answers = new Dictionary<(RewardKind, uint), bool>();
        foreach (var (name, set) in snapshot.Collectibles)
        {
            // A kind a newer build added and this one does not know is skipped.
            if (set is null || name.Length == 0 || !char.IsLetter(name[0])
                || !Enum.TryParse<RewardKind>(name, ignoreCase: false, out var kind) || !Enum.IsDefined(kind))
            {
                continue;
            }

            foreach (var id in set.Missing ?? [])
            {
                answers[(kind, id)] = false;
            }

            foreach (var id in set.Owned ?? [])
            {
                answers[(kind, id)] = true;
            }
        }

        return new CollectibleLookup(answers.ToFrozenDictionary(), snapshot.TakenUtc);
    }

    /// <summary>True when owned, false when checked and missing, null when the capture did not check it.</summary>
    public bool? Owns(RewardKind kind, uint rewardId) => answers.TryGetValue((kind, rewardId), out var owned) ? owned : null;
}
