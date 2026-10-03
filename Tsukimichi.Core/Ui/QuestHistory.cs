namespace Tsukimichi.Core.Ui;

/// <summary>
/// Back and forward through the quests the player looked at (feature plan v7 N1), as a browser keeps its pages. Every
/// quest the main window moves to by any means other than Back or Forward (a row click, a prerequisite or "unlocks
/// next" link, the search, Path, Next stops, a command or another plugin's IPC) is a <see cref="Visit"/>: it drops the
/// forward entries, so a new jump after Back starts a new trail, and it is skipped when it is the quest already current.
/// <see cref="Back"/> and <see cref="Forward"/> move along the entries without adding any. At most
/// <see cref="Capacity"/> entries are kept; the oldest goes first.
/// <para>
/// The quest shown can differ from the current entry: nothing is selected (the Tonight card), or a quest the catalog no
/// longer has. Then Back returns to the current entry itself, so "I cleared the selection" is one Back away from the
/// quest it showed. Entries whose quest no longer exists (a <c>exists</c> callback says so) and entries equal to the
/// quest shown are stepped over, never removed, so a catalog that comes back finds its trail intact. Pure, so it is
/// tested.
/// </para>
/// </summary>
public sealed class QuestHistory
{
    /// <summary>How many quests the history keeps by default (feature plan v7 N1: "about 50").</summary>
    public const int DefaultCapacity = 50;

    private readonly List<uint> entries = [];
    private int index = -1;

    public QuestHistory(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>The most entries kept.</summary>
    public int Capacity { get; }

    /// <summary>How many entries are kept, the current one and the forward ones included.</summary>
    public int Count => entries.Count;

    /// <summary>The current entry's position in <see cref="Entries"/>; -1 while empty.</summary>
    public int Index => index;

    /// <summary>The current entry's quest row id; null while empty.</summary>
    public uint? Current => index >= 0 ? entries[index] : null;

    /// <summary>The entries, oldest first.</summary>
    public IReadOnlyList<uint> Entries => entries;

    /// <summary>
    /// Records a move to <paramref name="rowId"/> by any means but Back and Forward: the forward entries are dropped and
    /// the quest becomes the current entry, unless it already is (consecutive duplicates are skipped, and then nothing
    /// is dropped). Past <see cref="Capacity"/> the oldest entry goes.
    /// </summary>
    public void Visit(uint rowId)
    {
        if (Current == rowId)
        {
            return;
        }

        var forward = entries.Count - (index + 1);
        if (forward > 0)
        {
            entries.RemoveRange(index + 1, forward);
        }

        entries.Add(rowId);
        if (entries.Count > Capacity)
        {
            entries.RemoveRange(0, entries.Count - Capacity);
        }

        index = entries.Count - 1;
    }

    /// <summary>Forgets every entry (a new session, say).</summary>
    public void Clear()
    {
        entries.Clear();
        index = -1;
    }

    /// <summary>The quest <see cref="Back"/> would land on with <paramref name="shown"/> showing, without moving; null when there is none.</summary>
    public uint? PeekBack(uint? shown, Func<uint, bool>? exists = null)
    {
        var at = BackIndex(shown, exists);
        return at >= 0 ? entries[at] : null;
    }

    /// <summary>The quest <see cref="Forward"/> would land on with <paramref name="shown"/> showing, without moving; null when there is none.</summary>
    public uint? PeekForward(uint? shown, Func<uint, bool>? exists = null)
    {
        var at = ForwardIndex(shown, exists);
        return at >= 0 ? entries[at] : null;
    }

    /// <summary>Whether Back has somewhere to go with <paramref name="shown"/> showing.</summary>
    public bool CanGoBack(uint? shown, Func<uint, bool>? exists = null) => BackIndex(shown, exists) >= 0;

    /// <summary>Whether Forward has somewhere to go with <paramref name="shown"/> showing.</summary>
    public bool CanGoForward(uint? shown, Func<uint, bool>? exists = null) => ForwardIndex(shown, exists) >= 0;

    /// <summary>
    /// Steps back to the nearest earlier entry whose quest exists and is not <paramref name="shown"/> (or to the current
    /// entry while something else is shown) and returns its quest; null, without moving, when there is none. Adds no
    /// entry: the caller shows the quest without calling <see cref="Visit"/>, or calls it with the same id, which is
    /// then the current entry and skipped.
    /// </summary>
    public uint? Back(uint? shown, Func<uint, bool>? exists = null)
    {
        var at = BackIndex(shown, exists);
        if (at < 0)
        {
            return null;
        }

        index = at;
        return entries[at];
    }

    /// <summary>
    /// Steps forward to the nearest later entry whose quest exists and is not <paramref name="shown"/>, and returns its
    /// quest; null, without moving, when there is none. Adds no entry.
    /// </summary>
    public uint? Forward(uint? shown, Func<uint, bool>? exists = null)
    {
        var at = ForwardIndex(shown, exists);
        if (at < 0)
        {
            return null;
        }

        index = at;
        return entries[at];
    }

    private int BackIndex(uint? shown, Func<uint, bool>? exists)
    {
        if (index < 0)
        {
            return -1;
        }

        // Showing the current entry, Back starts one before it; showing anything else (nothing, or a quest the history
        // did not record) it starts at the current entry.
        var start = shown == entries[index] ? index - 1 : index;
        for (var i = start; i >= 0; i--)
        {
            if (Lands(entries[i], shown, exists))
            {
                return i;
            }
        }

        return -1;
    }

    private int ForwardIndex(uint? shown, Func<uint, bool>? exists)
    {
        for (var i = index + 1; i < entries.Count; i++)
        {
            if (Lands(entries[i], shown, exists))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Lands(uint rowId, uint? shown, Func<uint, bool>? exists) =>
        rowId != shown && (exists is null || exists(rowId));
}
