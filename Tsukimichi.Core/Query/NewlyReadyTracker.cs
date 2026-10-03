namespace Tsukimichi.Core.Query;

/// <summary>
/// The Journal badge's bookkeeping for the viewed character (plan v7, spec Revision 3 R3.2): its tallies, the quests
/// newly ready (<see cref="NewlyReady"/>), the seen set and the Newly ready list the badge opened. The seen set is read
/// from and written to the per-character store through the two callbacks; once seeded it is also kept here, so a
/// character whose set is not stored (not tracked, or no store yet) keeps its marks for as long as it is viewed instead
/// of being seeded again on every evaluation. Switching character starts over. Not thread-safe: the window's thread.
/// </summary>
public sealed class NewlyReadyTracker
{
    private readonly Func<ulong, IReadOnlyList<uint>?> load;
    private readonly Func<ulong, uint[], bool> save;

    private HashSet<uint> available = [];

    // Null until seeded: nothing stored and no settled evaluation with anything available yet.
    private uint[]? seen;

    // The store held a seen set for this character while it was viewed (read or written); when it later holds none, the
    // character was forgotten (or "Delete all data" ran), and nothing is written for it again until another is viewed.
    private bool stored;
    private bool forgotten;

    private uint[]? openIds;

    /// <param name="load">The stored seen set of a character; null when none (first look, not stored, or no store).</param>
    /// <param name="save">Stores a character's seen set; false when it declined (a character not tracked, no store).</param>
    public NewlyReadyTracker(Func<ulong, IReadOnlyList<uint>?> load, Func<ulong, uint[], bool> save)
    {
        this.load = load ?? throw new ArgumentNullException(nameof(load));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <summary>The character viewed; 0 for none.</summary>
    public ulong ContentId { get; private set; }

    /// <summary>The last evaluation's tallies.</summary>
    public ReadyTally Tally { get; private set; } = ReadyTally.Empty;

    /// <summary>The available quests not seen yet, ascending.</summary>
    public uint[] New { get; private set; } = [];

    /// <summary>The seen set held here; null until seeded.</summary>
    public IReadOnlyList<uint>? Seen => seen;

    /// <summary>The quests of the Newly ready list the badge opened; null while it is not open.</summary>
    public IReadOnlyList<uint>? OpenIds => openIds;

    /// <summary>
    /// Views another character: everything starts over. A list still open closes without marking its quests seen: they
    /// belong to the character left, whose entry may just have been forgotten, and marking them would write it again.
    /// </summary>
    public void SwitchTo(ulong contentId)
    {
        if (contentId == ContentId)
        {
            return;
        }

        ContentId = contentId;
        Tally = ReadyTally.Empty;
        available = [];
        New = [];
        seen = null;
        stored = false;
        forgotten = false;
        openIds = null;
    }

    /// <summary>
    /// Takes a new evaluation of the viewed character. The stored seen set wins; without one the set held here is used.
    /// With neither, the set is seeded with everything available, but only from a <paramref name="settled"/> evaluation
    /// (a capture with quest data in it) that has something available, so an empty first capture does not seed an empty
    /// set and report the next real evaluation as all new.
    /// </summary>
    /// <param name="tally">The evaluation's tallies (<see cref="NewlyReady.Tally"/>).</param>
    /// <param name="gone">Whether a seen quest left availability for good (<see cref="NewlyReady.IsGone"/>).</param>
    /// <param name="settled">Whether the evaluation is of a capture with quest data, so it may seed the set.</param>
    public void Recompute(ReadyTally tally, Func<uint, bool>? gone, bool settled)
    {
        ArgumentNullException.ThrowIfNull(tally);
        Tally = tally;
        available = new HashSet<uint>(tally.Available);
        var fromStore = ContentId == 0 ? null : load(ContentId);
        if (fromStore is not null)
        {
            stored = true;
        }
        else if (stored)
        {
            forgotten = true;
        }

        var basis = (IReadOnlyCollection<uint>?)fromStore ?? seen;
        if (basis is null && !settled)
        {
            New = [];
            return;
        }

        var state = NewlyReady.Reconcile(tally.Available, basis, gone);
        New = state.New;
        seen = state.Seen;
        if (state.SeenChanged)
        {
            Save();
        }
    }

    /// <summary>The player selected a quest: it stops being new.</summary>
    public void Select(uint rowId)
    {
        if (Array.BinarySearch(New, rowId) >= 0)
        {
            MarkSeen([rowId]);
        }
    }

    /// <summary>
    /// The badge was clicked: the quests to list as newly ready, or null (none new) to change nothing. They are taken
    /// before a list still open is replaced (its quests marked seen), so that close does not empty the list being opened.
    /// The caller does nothing at all while its list is the one on screen.
    /// </summary>
    public IReadOnlyList<uint>? Open()
    {
        var ids = New;
        if (ids.Length == 0)
        {
            return null;
        }

        CloseList(markSeen: true);
        openIds = ids;
        return ids;
    }

    /// <summary>The Newly ready list was closed or replaced; its quests are marked seen when <paramref name="markSeen"/>.</summary>
    public void CloseList(bool markSeen)
    {
        if (openIds is not { } ids)
        {
            return;
        }

        openIds = null;
        if (markSeen)
        {
            MarkSeen(ids);
        }
    }

    private void MarkSeen(IEnumerable<uint> rowIds)
    {
        if (seen is null || NewlyReady.MarkSeen(seen, rowIds, available) is not { } next)
        {
            return;
        }

        seen = next;
        New = NewlyReady.Reconcile(Tally.Available, seen).New;
        Save();
    }

    private void Save()
    {
        if (ContentId == 0 || forgotten || seen is null)
        {
            return;
        }

        stored |= save(ContentId, seen);
    }
}
