namespace Tsukimichi.Core.Ui;

/// <summary>The notices the main window's dock can show, most important first.</summary>
public enum NoticeKind
{
    /// <summary>A catalog rebuild failed while the older catalog stays in use; Retry. Stays.</summary>
    RebuildFailed = 0,

    /// <summary>"Game updated: N quests are newer than Tsukimichi's data"; Show them or Dismiss. Stays.</summary>
    Freshness = 1,

    /// <summary>"Viewing Alt · 3 filters on" on the first open of a session; Follow me, Clear.</summary>
    Context = 2,

    /// <summary>"Pinned. Show your pins on screen while you play?" after the first pin.</summary>
    PinPrompt = 3,

    /// <summary>"Set up your road" is waiting in the detail column while a quest is selected.</summary>
    Setup = 4,

    /// <summary>What's new is waiting in the detail column while a quest is selected.</summary>
    WhatsNew = 5,

    /// <summary>Since you were away is waiting in the detail column while a quest is selected.</summary>
    WelcomeBack = 6,
}

/// <summary>
/// The notice dock's queue (feature plan v6 U2, decision 6): which notices are due, the one on screen (one at a time,
/// with a pager), and the clock of one-time prompts. A one-time prompt closes by itself after
/// <see cref="OneTimeSeconds"/> on screen, and the pointer resting on the dock stops its clock; a notice that needs the
/// player to act (<see cref="Stays"/>) stays until its cause goes, and the player may fold it down to a chip
/// (<see cref="SetCollapsed"/>) meanwhile. A closed prompt stays closed for the session. Only
/// the notice on screen uses time, so one waiting behind the pager is still there when the player pages to it. Fed with
/// a steady clock (ImGui's time), and only the time the dock is drawn counts: a step is capped at
/// <see cref="MaxStepSeconds"/> and <see cref="Suspend"/> forgets the last tick, so a window closed for minutes does not
/// close a prompt the moment it reopens. Free of ImGui so it is tested.
/// </summary>
public sealed class NoticeQueue
{
    /// <summary>How long a one-time prompt stays on screen, not counting the time the pointer rests on it.</summary>
    public const double OneTimeSeconds = 15.0;

    /// <summary>The most one tick takes off a prompt's clock: a frame hitch or a gap between draws counts as one frame.</summary>
    public const double MaxStepSeconds = 0.1;

    private static readonly int KindCount = Enum.GetValues<NoticeKind>().Length;

    private readonly bool[] due = new bool[KindCount];
    private readonly bool[] closed = new bool[KindCount];
    private readonly bool[] collapsed = new bool[KindCount];
    private readonly double[] remaining = new double[KindCount];
    private int current = -1;
    private double lastTick = double.NaN;

    /// <summary>Whether <paramref name="kind"/> needs the player to act, so it never closes by itself.</summary>
    public static bool Stays(NoticeKind kind) => kind is NoticeKind.RebuildFailed or NoticeKind.Freshness;

    /// <summary>How many notices are waiting, the one on screen included.</summary>
    public int Count
    {
        get
        {
            var count = 0;
            for (var i = 0; i < KindCount; i++)
            {
                count += Showable(i) ? 1 : 0;
            }

            return count;
        }
    }

    /// <summary>The notice on screen, or null when none is waiting.</summary>
    public NoticeKind? Current => current >= 0 && Showable(current) ? (NoticeKind)current : null;

    /// <summary>The notice on screen's place among the waiting ones, from 1 (0 with none).</summary>
    public int Position
    {
        get
        {
            if (Current is null)
            {
                return 0;
            }

            var position = 0;
            for (var i = 0; i <= current; i++)
            {
                position += Showable(i) ? 1 : 0;
            }

            return position;
        }
    }

    /// <summary>Seconds left on the notice on screen; infinite for one that stays, 0 with none.</summary>
    public double Remaining => Current is { } kind ? Stays(kind) ? double.PositiveInfinity : remaining[(int)kind] : 0.0;

    /// <summary>Whether <paramref name="kind"/> was closed (by the player or its clock) this session.</summary>
    public bool Closed(NoticeKind kind) => closed[(int)kind];

    /// <summary>Whether the player folded <paramref name="kind"/> down to its chip; only a notice that stays folds.</summary>
    public bool Collapsed(NoticeKind kind) => collapsed[(int)kind];

    /// <summary>
    /// Folds a notice that stays (<see cref="Stays"/>) down to a small chip, or opens it again. It stays folded while its
    /// cause holds; once the cause goes, a new one shows in full. A one-time prompt does not fold (it closes instead).
    /// </summary>
    public void SetCollapsed(NoticeKind kind, bool fold) => collapsed[(int)kind] = fold && Stays(kind);

    /// <summary>
    /// Says whether <paramref name="kind"/> is due now (its cause holds). A newly due notice that outranks the one on
    /// screen takes its place; one that ranks lower waits behind the pager. A notice whose cause went is dropped.
    /// </summary>
    public void Set(NoticeKind kind, bool isDue)
    {
        var i = (int)kind;
        if (due[i] == isDue)
        {
            return;
        }

        due[i] = isDue;
        if (!isDue)
        {
            collapsed[i] = false;
        }

        if (isDue)
        {
            remaining[i] = OneTimeSeconds;
            if (!closed[i] && (current < 0 || !Showable(current) || i < current))
            {
                current = i;
            }
        }

        Settle();
    }

    /// <summary>Closes <paramref name="kind"/> for the session (its × or "Not now", or its clock ran out).</summary>
    public void Close(NoticeKind kind)
    {
        closed[(int)kind] = true;
        Settle();
    }

    /// <summary>Pages by <paramref name="delta"/> through the waiting notices, wrapping around.</summary>
    public void Step(int delta)
    {
        var count = Count;
        if (count < 2 || delta == 0)
        {
            return;
        }

        var target = ((Position - 1 + delta) % count + count) % count;
        for (var i = 0; i < KindCount; i++)
        {
            if (Showable(i) && target-- == 0)
            {
                current = i;
                return;
            }
        }
    }

    /// <summary>
    /// Advances the clock to <paramref name="now"/>: the one-time prompt on screen uses the time, unless
    /// <paramref name="paused"/> (the pointer is on the dock), and closes when it runs out. Returns whether a notice is on
    /// screen afterwards.
    /// </summary>
    public bool Tick(double now, bool paused)
    {
        var step = double.IsFinite(lastTick) ? Math.Min(now - lastTick, MaxStepSeconds) : 0.0;
        lastTick = now;
        if (Current is not { } kind)
        {
            return false;
        }

        if (!paused && !Stays(kind) && double.IsFinite(step) && step > 0.0)
        {
            remaining[(int)kind] -= step;
            if (remaining[(int)kind] <= 0.0)
            {
                Close(kind);
            }
        }

        return Current is not null;
    }

    /// <summary>
    /// The dock is not drawn (the window closed, or nothing is waiting): the next <see cref="Tick"/> starts the clock
    /// afresh instead of counting the time in between.
    /// </summary>
    public void Suspend() => lastTick = double.NaN;

    private bool Showable(int i) => due[i] && !closed[i];

    /// <summary>Keeps the notice on screen if it can stay; otherwise the next waiting one, then the previous one.</summary>
    private void Settle()
    {
        if (current >= 0 && Showable(current))
        {
            return;
        }

        var from = Math.Max(0, current);
        for (var i = from; i < KindCount; i++)
        {
            if (Showable(i))
            {
                current = i;
                return;
            }
        }

        for (var i = from - 1; i >= 0; i--)
        {
            if (Showable(i))
            {
                current = i;
                return;
            }
        }

        current = -1;
    }
}
