using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>Whether a first capture after login may be committed; see <see cref="LoginReadiness.Check"/>.</summary>
public enum LoginVerdict
{
    /// <summary>The capture carries character data (or the character really is empty); commit it.</summary>
    Ready,

    /// <summary>The client has not delivered the character's quest data yet; capture again on the next poll.</summary>
    NotReady,

    /// <summary>
    /// Still empty after <see cref="LoginReadiness.MaxWait"/> with nothing stored for the character: commit it as it
    /// is rather than wait forever. Never returned while a stored snapshot has data (see <see cref="LoginReadiness.Overdue"/>).
    /// </summary>
    ReadyAfterTimeout,
}

/// <summary>
/// Guards the poller's first pass after login. Right after <c>Login</c> the player is loaded and has a content id
/// before the client has received the quest data, so a capture on that tick shows an all-zero completion mask and an
/// empty journal. Committing it would drop every stored accepted time (<see cref="AcceptedSince.Reconcile"/>), then
/// the next poll would diff thousands of quests and announce every pinned one. Such a capture is not ready unless the
/// stored snapshot of the same character was empty too (a character that really has nothing), or nothing is stored
/// and the wait exceeded <see cref="MaxWait"/>, so a genuinely new character is not held up. A character whose stored
/// snapshot has data is never committed empty, however long the client takes: the timeout would overwrite that
/// snapshot with the empty capture. Not thread-safe: the framework thread owns it.
/// </summary>
public sealed class LoginReadiness
{
    public static readonly TimeSpan DefaultMaxWait = TimeSpan.FromSeconds(10);

    public LoginReadiness(TimeSpan? maxWait = null)
    {
        var wait = maxWait ?? DefaultMaxWait;
        if (wait < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWait), wait, "Wait cannot be negative.");
        }

        MaxWait = wait;
    }

    /// <summary>Longest an empty-looking first capture of a character with nothing stored is refused before it is committed anyway.</summary>
    public TimeSpan MaxWait { get; }

    /// <summary>When the current run of empty captures began; null while nothing is being waited on.</summary>
    public DateTime? WaitingSinceUtc { get; private set; }

    /// <summary>
    /// True once the wait passed <see cref="MaxWait"/> for a character whose stored snapshot has data: the capture
    /// stays refused until the client delivers, and the poller logs the overrun once. Cleared with the wait.
    /// </summary>
    public bool Overdue { get; private set; }

    /// <summary>
    /// An all-zero completion mask with nothing in the journal and no daily done: what the client reports before the
    /// character's quest data arrived. A capture with even one bit or one journal entry is settled.
    /// </summary>
    public static bool LooksEmpty(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Accepted.Count == 0
            && snapshot.DailyDone.Count == 0
            && !snapshot.CompletedBits.AsSpan().ContainsAnyExcept((byte)0);
    }

    /// <summary>
    /// Judges a first-pass capture. A settled capture, or an empty one for a character whose stored snapshot
    /// (<paramref name="stored"/>, null when none exists) was empty as well, is ready at once and ends any wait.
    /// Otherwise the wait starts on the first empty capture. With nothing stored the verdict turns to
    /// <see cref="LoginVerdict.ReadyAfterTimeout"/> once <see cref="MaxWait"/> has passed; with a stored snapshot
    /// that has data it stays <see cref="LoginVerdict.NotReady"/> and <see cref="Overdue"/> turns on instead.
    /// </summary>
    public LoginVerdict Check(CharacterSnapshot capture, CharacterSnapshot? stored, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(capture);

        if (!LooksEmpty(capture) || (stored is not null && LooksEmpty(stored)))
        {
            Reset();
            return LoginVerdict.Ready;
        }

        WaitingSinceUtc ??= nowUtc;
        if (nowUtc - WaitingSinceUtc.Value < MaxWait)
        {
            return LoginVerdict.NotReady;
        }

        if (stored is not null)
        {
            // The stored snapshot has data this capture lacks. Committing it would replace that character with an
            // empty one (and every accepted time with "now" on the next poll), so no timeout applies here.
            Overdue = true;
            return LoginVerdict.NotReady;
        }

        Reset();
        return LoginVerdict.ReadyAfterTimeout;
    }

    /// <summary>Forgets any wait in progress (logout, character change).</summary>
    public void Reset()
    {
        WaitingSinceUtc = null;
        Overdue = false;
    }
}
