namespace Tsukimichi.Core.Runtime;

/// <summary>What a clickable action on one of Tsukimichi's chat lines does (1.7.0).</summary>
public enum ChatLinkAction : byte
{
    /// <summary>Opens the quest in Tsukimichi's main window.</summary>
    Open = 1,

    /// <summary>Pins the quest for the logged-in character.</summary>
    Pin = 2,

    /// <summary>Opens the unlock route to the quest.</summary>
    Route = 3,

    /// <summary>Opens the main window on the quests an "Opened:" line counted; the value is the batch's serial.</summary>
    ShowOpened = 4,
}

/// <summary>
/// The command ids behind Tsukimichi's clickable chat actions ("[Open] [Pin] [Route]" after a quest link, "[Show]" after
/// an "Opened:" line) and which of them are registered with the game's chat now.
/// <para>
/// Dalamud's link payload carries only the plugin's name and a command id (API 15: <c>IChatGui.AddChatLinkHandler(uint,
/// Action&lt;uint, SeString&gt;)</c>; the payload's extra values are set by Dalamud alone), and a click hands the
/// handler that id and the link's own text. So the quest travels in the id: <see cref="Encode"/> puts the action in the
/// top byte and the value (a quest row id, or an "Opened:" batch serial) in the low 24 bits, and the handler reads both
/// back with <see cref="TryDecode"/>. One handler is registered per (action, value) pair, the first time a line needs
/// it; printing the same quest again reuses it.
/// </para>
/// <para>
/// The registry is capped: <see cref="Touch"/> marks an id as the most recently printed and, when a new id would exceed
/// <see cref="Capacity"/>, hands back the least recently printed one for the caller to unregister, so a long session
/// never accumulates thousands of handlers. A link on an old line whose id was evicted does nothing (Dalamud logs it at
/// debug level); printing that quest again brings its id back, and with it the old lines' links, since the id is the
/// same. Not thread-safe: the framework thread owns it.
/// </para>
/// </summary>
public sealed class ChatLinkRegistry
{
    /// <summary>Most handlers kept registered: enough for every link a busy evening of chat can still show.</summary>
    public const int DefaultCapacity = 300;

    /// <summary>The largest value an id can carry (24 bits); quest row ids (65 536 and up, under 100 000) fit.</summary>
    public const uint MaxValue = 0x00FF_FFFF;

    // Recency order, oldest first; the dictionary points at each id's node.
    private readonly LinkedList<uint> order = new();
    private readonly Dictionary<uint, LinkedListNode<uint>> nodes = [];

    public ChatLinkRegistry(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

    /// <summary>How many ids are registered now.</summary>
    public int Count => nodes.Count;

    /// <summary>The registered ids, least recently printed first.</summary>
    public IReadOnlyCollection<uint> Registered => order;

    /// <summary>The command id for <paramref name="action"/> on <paramref name="value"/> (a quest row id or a batch serial).</summary>
    public static uint Encode(ChatLinkAction action, uint value)
    {
        if (action is < ChatLinkAction.Open or > ChatLinkAction.ShowOpened)
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown chat action.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxValue);
        return ((uint)action << 24) | value;
    }

    /// <summary>Reads an id <see cref="Encode"/> made; false for an id of no known action.</summary>
    public static bool TryDecode(uint commandId, out ChatLinkAction action, out uint value)
    {
        action = (ChatLinkAction)(commandId >> 24);
        value = commandId & MaxValue;
        if (action is < ChatLinkAction.Open or > ChatLinkAction.ShowOpened)
        {
            action = default;
            value = 0;
            return false;
        }

        return true;
    }

    /// <summary>Whether <paramref name="commandId"/> is registered now.</summary>
    public bool Contains(uint commandId) => nodes.ContainsKey(commandId);

    /// <summary>
    /// Marks <paramref name="commandId"/> as just printed. Returns true when it was not registered yet (the caller
    /// registers its handler); <paramref name="evicted"/> is the least recently printed id the cap pushed out (the caller
    /// unregisters it), or null.
    /// </summary>
    public bool Touch(uint commandId, out uint? evicted)
    {
        evicted = null;
        if (nodes.TryGetValue(commandId, out var node))
        {
            order.Remove(node);
            order.AddLast(node);
            return false;
        }

        if (nodes.Count >= Capacity && order.First is { } oldest)
        {
            order.RemoveFirst();
            nodes.Remove(oldest.Value);
            evicted = oldest.Value;
        }

        nodes[commandId] = order.AddLast(commandId);
        return true;
    }

    /// <summary>Forgets every id and returns them, least recently printed first, for the caller to unregister.</summary>
    public List<uint> Clear()
    {
        var all = new List<uint>(order);
        order.Clear();
        nodes.Clear();
        return all;
    }
}
