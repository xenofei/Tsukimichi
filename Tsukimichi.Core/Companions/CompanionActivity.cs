namespace Tsukimichi.Core.Companions;

/// <summary>One hand-off under way, as the status bar names it.</summary>
/// <param name="Kind">Which hand-off it is; a single <see cref="StopTarget"/>.</param>
/// <param name="Text">Its live line ("Going to giver · Teleporting…", Questionable's step, "AutoDuty is running.").</param>
/// <param name="CanStop">Whether the segment offers a Stop for it.</param>
public readonly record struct CompanionActivity(StopTarget Kind, string Text, bool CanStop);

/// <summary>
/// The one companion activity segment of the main window's fixed status bar (feature plan v6, U4): the steady-layout
/// rule for hand-offs. Travel, Questionable, AutoDuty and Artisan used to add up to three live lines under the detail
/// pane's pills, and a pressed hand-off left a note in its card for a few seconds; each pushed the pane down. Now each
/// frame the live hand-offs are <see cref="Report"/>ed here and the status bar shows the first in
/// <see cref="StopAll.Order"/> (travel, Lifestream, Questionable, AutoDuty, Artisan) with its Stop. A note left by a
/// press ("Sent 2 × Maple Lumber to Artisan.", "Copied.") takes the text for <see cref="NoteSeconds"/> on a steady
/// clock, while the running hand-off keeps its Stop. Pure; the caller supplies the time.
/// </summary>
public sealed class CompanionActivityFeed
{
    /// <summary>How long a note shows, in seconds.</summary>
    public const double NoteSeconds = 6.0;

    private readonly CompanionActivity?[] live = new CompanionActivity?[StopAll.Order.Count];
    private string? note;
    private double noteUntil = double.NegativeInfinity;

    /// <summary>Sets what <paramref name="kind"/> is doing now: its live line, or null while it does not run.</summary>
    public void Report(StopTarget kind, string? text, bool canStop)
    {
        var slot = Slot(kind);
        if (slot < 0)
        {
            return;
        }

        live[slot] = text is { Length: > 0 } ? new CompanionActivity(kind, text, canStop) : null;
    }

    /// <summary>Leaves <paramref name="text"/> in the segment from <paramref name="now"/> for <see cref="NoteSeconds"/>; a newer note replaces it.</summary>
    public void Note(string text, double now)
    {
        if (text is not { Length: > 0 } || !double.IsFinite(now))
        {
            return;
        }

        note = text;
        noteUntil = now + NoteSeconds;
    }

    /// <summary>Drops the note before its time.</summary>
    public void ClearNote()
    {
        note = null;
        noteUntil = double.NegativeInfinity;
    }

    /// <summary>The hand-off the segment stands for: the first live one in <see cref="StopAll.Order"/>; null when none runs.</summary>
    public CompanionActivity? Live
    {
        get
        {
            foreach (var activity in live)
            {
                if (activity is not null)
                {
                    return activity;
                }
            }

            return null;
        }
    }

    /// <summary>The note still showing at <paramref name="now"/>, or null.</summary>
    public string? NoteAt(double now) => note is not null && now < noteUntil ? note : null;

    /// <summary>The segment's text at <paramref name="now"/>: a fresh note, else the live hand-off's line; null when there is nothing to say.</summary>
    public string? TextAt(double now) => NoteAt(now) ?? Live?.Text;

    private static int Slot(StopTarget kind)
    {
        var order = StopAll.Order;
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == kind)
            {
                return i;
            }
        }

        return -1;
    }
}
