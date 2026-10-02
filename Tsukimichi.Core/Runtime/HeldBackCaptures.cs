using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// When the plausibility guard (<see cref="CapturePlausibility"/>) gives way. A held-back capture is never committed,
/// so the last committed capture never moves; a loss the game really made (a chapter the game clears, a journal the
/// player emptied in another client) would freeze tracking for the session and be saved at the next login anyway.
/// A bad read comes back right within seconds; a real loss reads the same every time. So a held-back capture is taken
/// in once the same loss (the same completed quests read as not completed, the same journal quests gone, against the
/// same last capture) has been read for <see cref="AcceptAfter"/> over at least <see cref="MinCaptures"/> captures. A
/// capture that reads as an empty character is never taken in. An accepted loss stays accepted until the caller commits
/// it (<see cref="Reset"/>): a commit that never happens (a deferred pass dropped at logout, or one that faulted) leaves
/// the next capture of the same loss taken in at once, rather than held back for another few minutes. Not thread-safe:
/// the framework thread owns it.
/// </summary>
public sealed class HeldBackCaptures
{
    /// <summary>How long one loss must keep reading the same before it is taken in.</summary>
    public static readonly TimeSpan AcceptAfter = TimeSpan.FromMinutes(2);

    /// <summary>The fewest held-back captures of one loss before it is taken in.</summary>
    public const int MinCaptures = 3;

    private ulong contentId;
    private DateTime lastTakenUtc;
    private byte[] lost = [];
    private ushort[] left = [];
    private DateTime firstUtc;

    /// <summary>Held-back captures of the current loss so far; 0 when none is being watched.</summary>
    public int Count { get; private set; }

    /// <summary>When the current loss was first read; null when none is being watched.</summary>
    public DateTime? SinceUtc => Count > 0 ? firstUtc : null;

    /// <summary>The current loss read the same long enough and is taken in; it stays so until <see cref="Reset"/> (the commit).</summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Records a capture the guard judged (<paramref name="result"/>) against <paramref name="last"/>. Returns true when
    /// it is to be taken in after all: its loss has read the same long enough, or was already accepted and not committed
    /// yet (<see cref="Accepted"/>). A plausible capture, an empty one or a different loss starts the watch over.
    /// </summary>
    public bool Observe(CharacterSnapshot last, CharacterSnapshot capture, PlausibilityResult result, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(last);
        ArgumentNullException.ThrowIfNull(capture);
        if (result.Plausible || result.Verdict == PlausibilityVerdict.EmptyCapture)
        {
            Reset();
            return false;
        }

        var lostNow = Lost(last.CompletedBits, capture.CompletedBits);
        var leftNow = Left(last, capture);
        if (Count == 0
            || contentId != capture.ContentId
            || lastTakenUtc != last.TakenUtc
            || !lost.AsSpan().SequenceEqual(lostNow)
            || !left.AsSpan().SequenceEqual(leftNow))
        {
            contentId = capture.ContentId;
            lastTakenUtc = last.TakenUtc;
            lost = lostNow;
            left = leftNow;
            firstUtc = nowUtc;
            Count = 1;
            Accepted = false;
            return false;
        }

        Count++;
        if (Accepted || (Count >= MinCaptures && nowUtc - firstUtc >= AcceptAfter))
        {
            Accepted = true;
            return true;
        }

        return false;
    }

    /// <summary>Stops watching: a capture was committed (an accepted one included), or the character or session changed.</summary>
    public void Reset()
    {
        Count = 0;
        Accepted = false;
        lost = [];
        left = [];
    }

    /// <summary>The completion bits set in <paramref name="before"/> and clear in <paramref name="now"/>; a shorter mask reads as zero.</summary>
    private static byte[] Lost(byte[] before, byte[] now)
    {
        var result = new byte[before.Length];
        for (var i = 0; i < before.Length; i++)
        {
            result[i] = (byte)(before[i] & ~(i < now.Length ? now[i] : 0));
        }

        return result;
    }

    /// <summary>The quests of the last journal that are neither in the new one nor completed, ascending.</summary>
    private static ushort[] Left(CharacterSnapshot last, CharacterSnapshot capture)
    {
        var result = new List<ushort>();
        foreach (var accepted in last.Accepted)
        {
            if (capture.IsCompleted(accepted.QuestId))
            {
                continue;
            }

            var stillThere = false;
            foreach (var now in capture.Accepted)
            {
                if (now.QuestId == accepted.QuestId)
                {
                    stillThere = true;
                    break;
                }
            }

            if (!stillThere)
            {
                result.Add(accepted.QuestId);
            }
        }

        result.Sort();
        return [.. result];
    }
}
