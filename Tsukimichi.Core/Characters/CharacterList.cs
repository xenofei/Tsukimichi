namespace Tsukimichi.Core.Characters;

/// <summary>
/// One character as every alt list shows it (1.8.0, R7 B/E/H): the character switcher, the Characters list, the
/// Compare picker, the account view and the collection grid.
/// </summary>
/// <param name="WorldName">The world's name, or its id as text when the World sheet could not be read.</param>
/// <param name="DataCenter">The world's data center name; empty when unknown.</param>
/// <param name="TakenUtc">The last capture saved (or, for the character logged in here and not saved, the latest one).</param>
/// <param name="LiveHere">Logged in on this game client.</param>
/// <param name="LiveElsewhere">Logged in on another game client (multibox, D11).</param>
/// <param name="Hidden">Hidden from the lists by the player.</param>
/// <param name="Tracked">False after "Don't track this character".</param>
public sealed record CharacterEntry(
    ulong ContentId,
    string Name,
    uint World,
    string WorldName,
    string DataCenter,
    DateTime TakenUtc,
    int CompletedCount,
    bool LiveHere = false,
    bool LiveElsewhere = false,
    bool Hidden = false,
    bool Tracked = true)
{
    /// <summary>Logged in here or on another client: these head the list.</summary>
    public bool Live => LiveHere || LiveElsewhere;
}

/// <summary>
/// One run of the Characters list under a data center heading (empty for the live characters' run at the top and for
/// the single run of an ungrouped list). <paramref name="UnknownDataCenter"/> marks the run of characters whose data
/// center is not known, which is grouped last and needs a heading of its own (its <see cref="DataCenter"/> is empty).
/// </summary>
public sealed record CharacterGroup(string DataCenter, IReadOnlyList<CharacterEntry> Entries, bool UnknownDataCenter = false);

/// <summary>
/// The rules every alt list follows (1.8.0, R7 B, E, H). The order is stable: the character live here first, then the
/// ones live in other game clients, then the rest by name, world and content id. A save in another client never moves
/// a row (the capture time is not part of the order); only a login or a logout does.
/// </summary>
public static class CharacterList
{
    /// <summary>Name, then world, case-insensitive and culture-neutral, so every client sorts the same way.</summary>
    private static readonly StringComparer NameOrder = StringComparer.OrdinalIgnoreCase;

    /// <summary>The list order (see the class remarks); a new list, <paramref name="entries"/> is left as it is.</summary>
    public static List<CharacterEntry> Sort(IEnumerable<CharacterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        list.Sort(Compare);
        return list;
    }

    /// <summary>The order of two entries in every list.</summary>
    public static int Compare(CharacterEntry? a, CharacterEntry? b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a is null)
        {
            return 1;
        }

        if (b is null)
        {
            return -1;
        }

        var rank = Rank(a).CompareTo(Rank(b));
        if (rank != 0)
        {
            return rank;
        }

        var name = NameOrder.Compare(a.Name, b.Name);
        if (name != 0)
        {
            return name;
        }

        var world = NameOrder.Compare(a.WorldName, b.WorldName);
        return world != 0 ? world : a.ContentId.CompareTo(b.ContentId);
    }

    /// <summary>
    /// The entries a list shows: hidden ones only with <paramref name="includeHidden"/>, except the character live here,
    /// which always shows (it is the one being played). Order kept.
    /// </summary>
    public static List<CharacterEntry> Visible(IEnumerable<CharacterEntry> sorted, bool includeHidden)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        return sorted.Where(e => includeHidden || !e.Hidden || e.LiveHere).ToList();
    }

    /// <summary>
    /// Whether an entry matches the list's search box: every word typed appears in its name, world or data center,
    /// ignoring case. An empty box matches everything.
    /// </summary>
    public static bool Matches(CharacterEntry entry, string? search)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        foreach (var word in search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                && !entry.WorldName.Contains(word, StringComparison.OrdinalIgnoreCase)
                && !entry.DataCenter.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The list's runs: the live characters first, under no heading, then one run per data center (alphabetical, an
    /// unknown one last, flagged <see cref="CharacterGroup.UnknownDataCenter"/>) in list order. With
    /// <paramref name="byDataCenter"/> off, or when every character is on one data center, a single run without a heading.
    /// </summary>
    public static List<CharacterGroup> Group(IReadOnlyList<CharacterEntry> sorted, bool byDataCenter)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        var centers = sorted.Select(static e => e.DataCenter).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (!byDataCenter || centers <= 1)
        {
            return sorted.Count == 0 ? [] : [new CharacterGroup(string.Empty, sorted)];
        }

        var groups = new List<CharacterGroup>();
        var live = sorted.Where(static e => e.Live).ToList();
        if (live.Count > 0)
        {
            groups.Add(new CharacterGroup(string.Empty, live));
        }

        foreach (var run in sorted.Where(static e => !e.Live)
                     .GroupBy(static e => e.DataCenter, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static g => g.Key.Length == 0 ? 1 : 0)
                     .ThenBy(static g => g.Key, NameOrder))
        {
            groups.Add(new CharacterGroup(run.Key, run.ToList(), UnknownDataCenter: run.Key.Length == 0));
        }

        return groups;
    }

    /// <summary>
    /// The character the Compare section shows beside <paramref name="viewed"/>: the one remembered for it when it is
    /// still listed (and not the viewed one), else the first in <paramref name="sorted"/> that is not the viewed one (the
    /// character live here when an alt is viewed). A fixed choice, never "the newest save". Null with no other character.
    /// </summary>
    public static ulong? CompareTarget(IReadOnlyList<CharacterEntry> sorted, ulong viewed, ulong? remembered)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        ulong? first = null;
        foreach (var entry in sorted)
        {
            if (entry.ContentId == viewed)
            {
                continue;
            }

            if (entry.ContentId == remembered)
            {
                return entry.ContentId;
            }

            first ??= entry.ContentId;
        }

        return first;
    }

    /// <summary>
    /// "Forget characters not seen in N days": the characters whose last capture is <paramref name="days"/> days or
    /// more before <paramref name="nowUtc"/>, oldest first. Never one live here or in another client (that client owns
    /// its files and would write them again); hidden and untracked ones are included. Nothing below one day.
    /// </summary>
    public static List<CharacterEntry> NotSeenFor(IEnumerable<CharacterEntry> entries, int days, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (days < 1)
        {
            return [];
        }

        var cutoff = nowUtc - TimeSpan.FromDays(days);
        return entries.Where(e => !e.Live && e.TakenUtc <= cutoff)
            .OrderBy(static e => e.TakenUtc)
            .ThenBy(static e => e.ContentId)
            .ToList();
    }

    private static int Rank(CharacterEntry entry) => entry.LiveHere ? 0 : entry.LiveElsewhere ? 1 : 2;
}
