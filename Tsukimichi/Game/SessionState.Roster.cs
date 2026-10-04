using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Game;

/// <summary>What the All characters roster (plan v7, 1.21.0 P3) reads of the session for a character it is not showing.</summary>
public sealed partial class SessionState
{
    /// <summary>
    /// The context another stored character is resolved with (the roster, its goals and Up next for it): the base
    /// context, the festivals running on the server now, and the stored cycle clock, as the Characters pane's comparison
    /// resolves one. Framework thread; the result is immutable and safe to hand to a worker.
    /// </summary>
    public EvalContext ContextForStored(CharacterSnapshot snapshot)
    {
        System.ArgumentNullException.ThrowIfNull(snapshot);
        return StoredContext(snapshot).WithDailyOffer(null);
    }
}
