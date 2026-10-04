using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// What finished between two captures of the live character's journal: a quest whose sequence moved on
/// (<see cref="QuestFinished"/> false) or a quest that left the journal completed (<see cref="QuestFinished"/> true).
/// </summary>
public readonly record struct StepFinish(ushort QuestId, bool QuestFinished);

/// <summary>
/// Notices a step or a quest done (plan v7, 1.21.0 P8, "Say what's next in chat"): compares the journal before and
/// after one capture. A quest that left the journal counts only when it is completed now (an abandoned one is not
/// done); a newly taken quest is no step done. A finished quest wins over a step, so the line says "Quest done". Pure.
/// </summary>
public static class StepProgress
{
    /// <param name="before">The journal at the previous capture.</param>
    /// <param name="after">The journal now.</param>
    /// <param name="isCompleted">Whether a quest id is completed now.</param>
    public static StepFinish? Finished(IReadOnlyList<AcceptedQuest> before, IReadOnlyList<AcceptedQuest> after, Func<ushort, bool> isCompleted)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(isCompleted);
        StepFinish? step = null;
        foreach (var old in before)
        {
            var still = false;
            foreach (var now in after)
            {
                if (now.QuestId != old.QuestId)
                {
                    continue;
                }

                still = true;
                if (now.Sequence > old.Sequence && old.Sequence != 0)
                {
                    step ??= new StepFinish(now.QuestId, false);
                }

                break;
            }

            if (!still && isCompleted(old.QuestId))
            {
                return new StepFinish(old.QuestId, true);
            }
        }

        return step;
    }
}

/// <summary>
/// At most one line every <see cref="Interval"/> (P8: "Say what's next in chat" prints at most one line every 10 s): a
/// line asked for sooner is dropped, never queued, so a burst of steps reads as one line. Pure; the caller passes the
/// clock.
/// </summary>
public sealed class GuidanceThrottle
{
    /// <summary>The shortest time between two lines, in milliseconds.</summary>
    public const long Interval = 10_000;

    private long last = long.MinValue;

    /// <summary>True, and the window starts again, when a line may print at <paramref name="nowMs"/>.</summary>
    public bool TryTake(long nowMs)
    {
        if (last != long.MinValue && nowMs - last < Interval)
        {
            return false;
        }

        last = nowMs;
        return true;
    }
}
