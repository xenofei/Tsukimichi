namespace Tsukimichi.Core.Ipc;

/// <summary>
/// The answers behind the "On Questionable's list (#n)" and "Questionable has a path / no path" badges (feature plan v5,
/// 1.6.0), cached so Questionable is never asked per frame. Pure; <c>Game.QuestionableIpc</c> fills it.
/// <para>
/// Paths: Questionable registers no "has a path" gate, so the answer is read from <c>IsQuestLockedReason</c>, which
/// says <c>(true, "")</c> for a quest it has no path for and gives a reason (or "not locked") for every quest it knows
/// (<see cref="HasPath"/>). Whether Questionable has a path does not depend on the character, so an answer holds until
/// Questionable's <c>Generation</c> moves (Dalamud's plugin list changed, or Questionable sent
/// <c>Questionable.ReloadData</c> after loading new paths). Unknown rows are asked lazily, as they are drawn, at most
/// <see cref="MaxAsksPerFrame"/> per frame, so opening a long route costs a few calls a frame, not hundreds at once.
/// </para>
/// <para>
/// The list: Questionable keeps its priority list in memory only and sends no message when it changes, so the list is
/// read with <c>ExportQuestPriority</c> when it is marked stale (after a send, when a pane opens, after a reload) and
/// otherwise at most every <see cref="ListMaxAgeSeconds"/> while something asks.
/// </para>
/// </summary>
public sealed class QuestionableBadges
{
    /// <summary>New path questions a frame may ask; the rest wait for the next frames.</summary>
    public const int MaxAsksPerFrame = 6;

    /// <summary>How old a list read may be before a badge reads it again.</summary>
    public const double ListMaxAgeSeconds = 30.0;

    private readonly Dictionary<uint, bool?> paths = [];
    private int pathGeneration = int.MinValue;
    private long askFrame = long.MinValue;
    private int asksLeft;

    private IReadOnlyDictionary<uint, int> positions = new Dictionary<uint, int>();
    private int listGeneration = int.MinValue;
    private double listTakenAt = double.NegativeInfinity;
    private bool listStale = true;

    /// <summary>
    /// Whether Questionable's reason-gate answer says it has a path for the quest: false for a locked answer with no
    /// reason, true for any other answer, null when there is no answer or the gate gives no reasons at all (the
    /// WigglyMuffin fork's <c>IsQuestLocked</c>, whose "locked" may or may not mean "no path").
    /// </summary>
    public static bool? HasPath(QuestionableAnswer? answer)
    {
        if (answer is null || answer.Reason is null)
        {
            return null;
        }

        return !answer.IsIndeterminate;
    }

    /// <summary>The cached path answer for a quest under <paramref name="generation"/>; false when it was never asked (or a new generation dropped it).</summary>
    public bool TryGetPath(uint rowId, int generation, out bool? hasPath)
    {
        if (generation != pathGeneration)
        {
            paths.Clear();
            pathGeneration = generation;
        }

        return paths.TryGetValue(rowId, out hasPath);
    }

    /// <summary>Stores a path answer (null: asked, no answer) under <paramref name="generation"/>.</summary>
    public void StorePath(uint rowId, int generation, bool? hasPath)
    {
        if (generation != pathGeneration)
        {
            paths.Clear();
            pathGeneration = generation;
        }

        paths[rowId] = hasPath;
    }

    /// <summary>Takes one of this frame's <see cref="MaxAsksPerFrame"/> questions; false when the frame has used them all.</summary>
    public bool TryTakeAsk(long frame)
    {
        if (frame != askFrame)
        {
            askFrame = frame;
            asksLeft = MaxAsksPerFrame;
        }

        if (asksLeft <= 0)
        {
            return false;
        }

        asksLeft--;
        return true;
    }

    /// <summary>The list should be read again: it was marked stale, Questionable's generation moved, or the last read is older than <see cref="ListMaxAgeSeconds"/>.</summary>
    public bool ListNeedsRead(int generation, double now) =>
        listStale || generation != listGeneration || now - listTakenAt >= ListMaxAgeSeconds;

    /// <summary>Asks for a fresh read on the next badge that needs the list (a send, a pane opened, a reload).</summary>
    public void MarkListStale() => listStale = true;

    /// <summary>Stores a list read: its element ids in order, or null when it could not be read (no badge then).</summary>
    public void StoreList(IReadOnlyList<string>? ids, int generation, double now)
    {
        positions = ids is null ? new Dictionary<uint, int>() : QuestionableList.Positions(ids);
        listGeneration = generation;
        listTakenAt = now;
        listStale = false;
    }

    /// <summary>Stores the positions a send just read back, as a fresh read.</summary>
    public void StorePositions(IReadOnlyDictionary<uint, int> fresh, int generation, double now)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        positions = fresh;
        listGeneration = generation;
        listTakenAt = now;
        listStale = false;
    }

    /// <summary>The quest's 1-based place on Questionable's list as last read; null when it is not on it.</summary>
    public int? Position(uint rowId) => positions.TryGetValue(rowId, out var position) ? position : null;

    /// <summary>Entries of the list as last read that are quests.</summary>
    public int QuestCount => positions.Count;
}
