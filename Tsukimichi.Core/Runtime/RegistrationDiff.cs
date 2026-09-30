namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Which registered entries must be replaced when a list is rebuilt from the same sources in the same order (the
/// Wotsit entries after a spoiler mask change): the positions whose visible fields differ. Lets the caller replace a
/// handful of entries one by one instead of unregistering and registering the whole list.
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
}
