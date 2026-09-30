namespace Tsukimichi.Core.Multibox;

/// <summary>Whether this client may keep writing its logged-in character's files.</summary>
public enum Ownership
{
    /// <summary>No other live client holds the character, or this client's claim is the newer one.</summary>
    Mine,

    /// <summary>Another live client holds the same character with a newer claim: this client stops writing it.</summary>
    Theirs,
}

/// <summary>
/// The rules of multibox sharing (D11), pure so they can be tested: when a heartbeat is stale, which characters are
/// live in another client, who owns a character's snapshot and which heartbeat files a client may delete.
/// </summary>
public static class LiveClients
{
    /// <summary>A heartbeat older than this (or this far in the future) means its client is gone.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    /// <summary>How often a client refreshes its heartbeat.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// True when the heartbeat was last written more than <see cref="StaleAfter"/> ago. A time that far ahead of the
    /// clock is stale too, so a file with a wrong stamp cannot claim a character forever.
    /// </summary>
    public static bool IsStale(Heartbeat heartbeat, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        var age = nowUtc - heartbeat.WrittenUtc;
        return age > StaleAfter || age < -StaleAfter;
    }

    /// <summary>
    /// True when the heartbeat was written by another game client. The process decides: a heartbeat of this process with
    /// another client id was left by an earlier load of the plugin in this same client (a reload), not another client.
    /// </summary>
    public static bool IsOtherClient(Heartbeat heartbeat, ClientIdentity me)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        return heartbeat.ProcessId != me.ProcessId;
    }

    /// <summary>A fresh heartbeat of another game client: its character is live there.</summary>
    public static bool IsLiveElsewhere(Heartbeat heartbeat, ClientIdentity me, DateTime nowUtc) =>
        IsOtherClient(heartbeat, me) && !IsStale(heartbeat, nowUtc);

    /// <summary>
    /// The characters live in other game clients, by content id, from every heartbeat read. The character this client
    /// holds itself (<paramref name="ownLive"/>) is left out whatever the files say: it is live here.
    /// </summary>
    public static Dictionary<ulong, Heartbeat> LiveElsewhere(IEnumerable<Heartbeat> heartbeats, ClientIdentity me, DateTime nowUtc, ulong? ownLive = null)
    {
        ArgumentNullException.ThrowIfNull(heartbeats);
        var result = new Dictionary<ulong, Heartbeat>();
        foreach (var heartbeat in heartbeats)
        {
            if (heartbeat.ContentId == ownLive || !IsLiveElsewhere(heartbeat, me, nowUtc))
            {
                continue;
            }

            if (!result.TryGetValue(heartbeat.ContentId, out var existing) || heartbeat.WrittenUtc > existing.WrittenUtc)
            {
                result[heartbeat.ContentId] = heartbeat;
            }
        }

        return result;
    }

    /// <summary>
    /// Who holds this client's logged-in character, given the heartbeat on disk for it: this client when there is none,
    /// it is stale or it is this client's own; otherwise the newer claim (<see cref="Heartbeat.SinceUtc"/>) wins, and on
    /// the same instant the greater client id. Two clients on one character cannot happen in the game, but if the files
    /// ever say so, exactly one of them keeps writing.
    /// </summary>
    public static Ownership Decide(Heartbeat? onDisk, ClientIdentity me, DateTime mySinceUtc, DateTime nowUtc)
    {
        if (onDisk is null || !IsLiveElsewhere(onDisk, me, nowUtc))
        {
            return Ownership.Mine;
        }

        if (onDisk.SinceUtc != mySinceUtc)
        {
            return onDisk.SinceUtc > mySinceUtc ? Ownership.Theirs : Ownership.Mine;
        }

        return string.CompareOrdinal(onDisk.ClientId, me.ClientId) > 0 ? Ownership.Theirs : Ownership.Mine;
    }

    /// <summary>
    /// Whether this client may write the snapshot (and sidecars) of <paramref name="contentId"/>: only its own logged-in
    /// character, and only while it owns it. A stored character, live in another client or not, is never written here.
    /// </summary>
    public static bool MayWriteSnapshot(ulong contentId, ulong? myLiveContentId, Ownership ownership) =>
        myLiveContentId == contentId && ownership == Ownership.Mine;

    /// <summary>
    /// Whether this client may delete a heartbeat file ("Delete all data", "Forget character", logout): its own, or a
    /// stale one left by a client that is gone; never a fresh heartbeat of another client. A missing or unreadable file
    /// (null) may go.
    /// </summary>
    public static bool MayDelete(Heartbeat? heartbeat, ClientIdentity me, DateTime nowUtc) =>
        heartbeat is null || !IsLiveElsewhere(heartbeat, me, nowUtc);

    /// <summary>Whether two maps of live characters say the same thing to the player (same characters, names and worlds).</summary>
    public static bool SameCharacters(IReadOnlyDictionary<ulong, Heartbeat> a, IReadOnlyDictionary<ulong, Heartbeat> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (id, heartbeat) in a)
        {
            if (!b.TryGetValue(id, out var other) || other.Name != heartbeat.Name || other.World != heartbeat.World || other.ProcessId != heartbeat.ProcessId)
            {
                return false;
            }
        }

        return true;
    }
}
