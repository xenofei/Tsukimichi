namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Which registered entries must be replaced when a list is rebuilt from the same sources (the Wotsit entries after a
/// spoiler mask or quest state change): the positions whose visible fields differ, and how much of a registration
/// order survives a change. Lets the caller replace entries one by one instead of unregistering the whole list.
/// </summary>
public static class RegistrationDiff
{
    /// <summary>
    /// Indices where <paramref name="current"/> and <paramref name="next"/> differ per <paramref name="same"/>, in
    /// ascending order; empty when nothing changed. Null when the lists differ in length, since positions no longer
    /// line up and the caller must rebuild the whole list.
    /// </summary>
    public static List<int>? ChangedIndices<T>(IReadOnlyList<T> current, IReadOnlyList<T> next, Func<T, T, bool> same)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(same);
        if (current.Count != next.Count)
        {
            return null;
        }

        var changed = new List<int>();
        for (var i = 0; i < current.Count; i++)
        {
            if (!same(current[i], next[i]))
            {
                changed.Add(i);
            }
        }

        return changed;
    }

    /// <summary>
    /// How many leading entries of <paramref name="order"/> may stay registered in a registry that keeps entries in
    /// the order they were registered and can only append (Wotsit): the longest prefix of <paramref name="order"/>
    /// whose entries are registered (<paramref name="sequence"/> above 0), unchanged per <paramref name="unchanged"/>,
    /// and already in that relative order. Every entry from that index on must be unregistered and registered again,
    /// in <paramref name="order"/>, to leave the registry in exactly that order.
    /// </summary>
    /// <param name="order">Positions in the order the registry should hold them.</param>
    /// <param name="sequence">Per position, when it was registered (ascending with each registration); 0 when it is not registered.</param>
    /// <param name="unchanged">Whether the registered entry at a position still matches the one to register there.</param>
    public static int KeptPrefix(IReadOnlyList<int> order, IReadOnlyList<long> sequence, Func<int, bool> unchanged)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(unchanged);
        long last = 0;
        for (var i = 0; i < order.Count; i++)
        {
            var position = order[i];
            var registered = position < sequence.Count ? sequence[position] : 0;
            if (registered <= last || !unchanged(position))
            {
                return i;
            }

            last = registered;
        }

        return order.Count;
    }
}
