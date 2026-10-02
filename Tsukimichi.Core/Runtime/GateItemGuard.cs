using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Which gear-gate read a capture keeps (<see cref="CharacterSnapshot.GateItems"/>). The inventory containers can report
/// loaded while the game is already clearing them (at logout, on a zone change), and a read of cleared containers finds
/// no relic weapon at all: saved as is, it leaves the stored character Blocked on every relic weapon step until the
/// next login. So:
/// <list type="bullet">
/// <item>The logout's last capture keeps the committed read: the gear cannot change on the way out.</item>
/// <item>A read that finds nothing worn or carried where the committed one (against the same weapon list) had weapons is
/// taken as unread (null, every gear gate "not checked") for one poll. The committed read is then null, so the next
/// poll that still finds nothing is taken in: a loss that persists across two polls is real (a weapon sold, discarded
/// or stored away).</item>
/// <item>Anything else is taken as read.</item>
/// </list>
/// Pure.
/// </summary>
public static class GateItemGuard
{
    /// <summary>The read the capture keeps.</summary>
    /// <param name="committed">The gear read of the last committed capture of the same character; null when it had none.</param>
    /// <param name="read">The gear read of this capture; null when it was not read.</param>
    /// <param name="loggingOut">The capture is the last one before the character goes.</param>
    public static GateItemCapture? Settle(GateItemCapture? committed, GateItemCapture? read, bool loggingOut)
    {
        if (loggingOut)
        {
            return committed;
        }

        if (read is { Equipped.Count: 0, Held.Count: 0 }
            && committed is { Held.Count: > 0 } before
            && before.Watch == read.Watch)
        {
            return null;
        }

        return read;
    }
}
