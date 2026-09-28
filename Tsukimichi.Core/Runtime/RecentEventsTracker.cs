namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The session's recent quest events, newest first and capped, scoped to one character. Recording or following a
/// different content id than the events belong to drops them first, so a character switch without a logout gap
/// (Login arriving with no not-ready tick in between) never shows the previous character's activity in Recent
/// activity or re-announces its notices to the new one. Not thread-safe: the framework thread owns it.
/// </summary>
public sealed class RecentEventsTracker
{
    public const int DefaultCapacity = 100;

    private readonly List<QuestEvent> events = [];

    public RecentEventsTracker(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>Most events kept; older ones fall off the end.</summary>
    public int Capacity { get; }

    /// <summary>The character the events belong to; null after <see cref="Clear"/> or before the first character.</summary>
    public ulong? ContentId { get; private set; }

    /// <summary>Newest first. The same list instance throughout, so callers may hold it.</summary>
    public IReadOnlyList<QuestEvent> Events => events;

    /// <summary>
    /// Makes <paramref name="contentId"/> the current character. A different character than before drops every
    /// event recorded so far. Returns true when the list changed.
    /// </summary>
    public bool Follow(ulong contentId)
    {
        if (ContentId == contentId)
        {
            return false;
        }

        ContentId = contentId;
        if (events.Count == 0)
        {
            return false;
        }

        events.Clear();
        return true;
    }

    /// <summary>
    /// Records one poll's events for <paramref name="contentId"/>, following it first (see <see cref="Follow"/>).
    /// The batch is inserted at the front keeping its own order, so its first event is the newest. Returns true
    /// when the list changed.
    /// </summary>
    public bool Add(ulong contentId, IReadOnlyList<QuestEvent> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var changed = Follow(contentId);
        if (batch.Count == 0)
        {
            return changed;
        }

        for (var i = batch.Count - 1; i >= 0; i--)
        {
            events.Insert(0, batch[i]);
        }

        if (events.Count > Capacity)
        {
            events.RemoveRange(Capacity, events.Count - Capacity);
        }

        return true;
    }

    /// <summary>Logout or data deletion: drops every event and forgets the character.</summary>
    public void Clear()
    {
        events.Clear();
        ContentId = null;
    }
}
