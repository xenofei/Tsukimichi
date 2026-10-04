namespace Tsukimichi.Core.Companions;

/// <summary>What <c>/tsuki stop</c> can stop, in the order it stops them (<see cref="StopAll.Order"/>).</summary>
[Flags]
public enum StopTarget
{
    None = 0,

    /// <summary>A Walk to giver or Go to giver Tsukimichi started: its walk or flight (a pathfind still pending too), teleport or hop.</summary>
    Travel = 1,

    /// <summary>An aethernet hop or <c>/li</c> task (<c>/li island</c>, <c>/li occult</c>) Tsukimichi handed Lifestream outside a Go to giver run.</summary>
    Lifestream = 2,

    /// <summary>A Questionable run.</summary>
    Questionable = 4,

    /// <summary>An AutoDuty run Tsukimichi started.</summary>
    AutoDuty = 8,

    /// <summary>An Artisan craft Tsukimichi started.</summary>
    Artisan = 16,
}

/// <summary>What one <c>/tsuki stop</c> does: stop these now, and ask before stopping those.</summary>
/// <param name="StopNow">Stopped by this press.</param>
/// <param name="Ask">Left running until the player presses again within <see cref="StopConfirm.WindowMs"/>.</param>
public readonly record struct StopDecision(StopTarget StopNow, StopTarget Ask);

/// <summary>
/// One Stop for every hand-off (1.11.0, A1): <c>/tsuki stop</c> stops travel first, then Lifestream, Questionable,
/// AutoDuty and Artisan. Two of them ask first, as their buttons do: Questionable while it would run its command after
/// a stop and the player asked to be asked, and AutoDuty inside a duty. A chat command cannot show a question, so the
/// question is the chat line and the answer is the same command again (<see cref="StopConfirm"/>). Pure.
/// </summary>
public static class StopAll
{
    /// <summary>The order the targets are stopped and named in.</summary>
    public static IReadOnlyList<StopTarget> Order { get; } =
        [StopTarget.Travel, StopTarget.Lifestream, StopTarget.Questionable, StopTarget.AutoDuty, StopTarget.Artisan];

    /// <summary>
    /// Splits the <paramref name="running"/> targets into those stopped now and those asked about: a target in
    /// <paramref name="needsConfirm"/> is asked about unless this press <paramref name="confirmed"/> an earlier question.
    /// </summary>
    public static StopDecision Decide(StopTarget running, StopTarget needsConfirm, bool confirmed)
    {
        var ask = confirmed ? StopTarget.None : running & needsConfirm;
        return new StopDecision(running & ~ask, ask);
    }

    /// <summary>
    /// What a knock-out or a stall during a run stops (1.18.0, A5) among the <paramref name="running"/> targets:
    /// nothing unless Settings says to stop the hand-offs (<paramref name="stopHandOffs"/>), Questionable only when the
    /// player opted in (<paramref name="stopQuestionable"/>), and, <paramref name="inDuty"/>, neither AutoDuty nor
    /// Questionable (whose stop ends the AutoDuty run it started): in a duty the NPC healers raise the character and the
    /// run carries on, so an alert is all it gets, as <c>/tsuki stop</c> asks before stopping AutoDuty there.
    /// </summary>
    public static StopTarget OnTrouble(StopTarget running, bool stopHandOffs, bool stopQuestionable, bool inDuty)
    {
        if (!stopHandOffs)
        {
            return StopTarget.None;
        }

        var stop = running;
        if (!stopQuestionable || inDuty)
        {
            stop &= ~StopTarget.Questionable;
        }

        if (inDuty)
        {
            stop &= ~StopTarget.AutoDuty;
        }

        return stop;
    }

    /// <summary>The single targets set in <paramref name="targets"/>, in <see cref="Order"/>.</summary>
    public static IEnumerable<StopTarget> Each(StopTarget targets) => Order.Where(target => (targets & target) != 0);
}

/// <summary>
/// The "press again to confirm" of <c>/tsuki stop</c>: a press that asked arms it, and a second press within
/// <see cref="WindowMs"/> confirms (once). Any later press asks again.
/// </summary>
public sealed class StopConfirm
{
    /// <summary>How long a question waits for the second press.</summary>
    public const long WindowMs = 10_000;

    private long? askedAt;

    /// <summary>The window in whole seconds, for the chat line.</summary>
    public static int WindowSeconds => (int)(WindowMs / 1000);

    /// <summary>A press asked at <paramref name="now"/>.</summary>
    public void Ask(long now) => askedAt = now;

    /// <summary>True when a question is open at <paramref name="now"/>: this press answers it. Either way the question closes.</summary>
    public bool Answer(long now)
    {
        var open = askedAt is { } at && now - at >= 0 && now - at <= WindowMs;
        askedAt = null;
        return open;
    }
}
