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

    /// <summary>
    /// <see cref="ContextForStored"/> for a save a worker has yet to read: what it reads of the session (the base context,
    /// the logged-in character, the curated festivals) is taken here, on the framework thread, so the function returned
    /// is pure and safe to call on the worker that loads the save.
    /// </summary>
    public System.Func<CharacterSnapshot, EvalContext> StoredContextFactory()
    {
        var context = baseContext;
        var live = liveSnapshot;
        var festivals = Curated.Festivals;
        return snapshot => context.ForStoredCharacter(ServerFestivals.For(snapshot, live, festivals, System.DateTime.UtcNow), StoredCycleClock).WithDailyOffer(null);
    }
}
