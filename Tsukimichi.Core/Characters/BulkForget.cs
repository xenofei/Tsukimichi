namespace Tsukimichi.Core.Characters;

/// <summary>What "Forget characters not seen in N days" does once confirmed: the characters to forget now, and how many it keeps.</summary>
/// <param name="Forget">The picked characters that still qualify, in the order they were picked.</param>
/// <param name="Kept">
/// Picked characters that no longer qualify: logged in here or on another game client since, or saved since (played
/// elsewhere and logged out while the question was open). Counted in the result line.
/// </param>
public sealed record BulkForgetPlan(IReadOnlyList<ulong> Forget, int Kept);

/// <summary>
/// "Forget characters not seen in N days" (1.8.0, R7 H) at confirm time. The question names the characters picked when
/// it opened; the answer can come minutes later, so each is checked again against the list as it is now
/// (<see cref="CharacterList.NotSeenFor"/>): one that logged in, or was saved since, is kept and counted, and one
/// already gone (forgotten in another client) is neither forgotten nor counted.
/// </summary>
public static class BulkForget
{
    /// <param name="picked">The characters the confirmation named.</param>
    /// <param name="current">Every character as the lists show it now (live flags and last save up to date).</param>
    /// <param name="days">The "not seen in" setting now.</param>
    /// <param name="nowUtc">The clock the age is read against.</param>
    public static BulkForgetPlan Recheck(IEnumerable<CharacterEntry> picked, IEnumerable<CharacterEntry> current, int days, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(current);
        var listed = current.ToList();
        var known = listed.Select(static e => e.ContentId).ToHashSet();
        var qualifying = CharacterList.NotSeenFor(listed, days, nowUtc).Select(static e => e.ContentId).ToHashSet();
        var forget = new List<ulong>();
        var kept = 0;
        foreach (var entry in picked)
        {
            if (qualifying.Contains(entry.ContentId))
            {
                if (!forget.Contains(entry.ContentId))
                {
                    forget.Add(entry.ContentId);
                }
            }
            else if (known.Contains(entry.ContentId))
            {
                kept++;
            }
        }

        return new BulkForgetPlan(forget, kept);
    }
}
