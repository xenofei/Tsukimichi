namespace Tsukimichi.Core.Model;

/// <summary>
/// What one capture read about one collectible kind (<see cref="CharacterSnapshot.Collectibles"/>): the reward ids the
/// character owns and the ids it was checked for and does not own. An id in neither list was not checked (the reward
/// data gained it after the capture), which reads as unknown rather than missing. Both lists ascending, distinct.
/// </summary>
public sealed record CollectibleSet
{
    public IReadOnlyList<uint> Owned { get; init; } = [];

    public IReadOnlyList<uint> Missing { get; init; } = [];

    /// <summary>Same ids in both lists, ignoring order.</summary>
    public bool SameIds(CollectibleSet? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && SameSet(Owned, other.Owned) && SameSet(Missing, other.Missing);
    }

    private static bool SameSet(IReadOnlyList<uint> a, IReadOnlyList<uint> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        var ordered = true;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                ordered = false;
                break;
            }
        }

        if (ordered)
        {
            return true;
        }

        var left = a.ToArray();
        var right = b.ToArray();
        Array.Sort(left);
        Array.Sort(right);
        return left.AsSpan().SequenceEqual(right);
    }
}
