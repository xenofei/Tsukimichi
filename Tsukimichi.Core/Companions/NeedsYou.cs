using System.Numerics;

namespace Tsukimichi.Core.Companions;

/// <summary>What a "Needs you" alert is about (plan v7, 1.18.0, A5). Flags, so the enabled kinds travel as one value.</summary>
[Flags]
public enum NeedsYouKind
{
    None = 0,

    /// <summary>The character died.</summary>
    Death = 1,

    /// <summary>vnavmesh says it is moving the character, and the character has not moved for <see cref="NeedsYouWatch.StuckSeconds"/>.</summary>
    Stuck = 2,

    /// <summary>The Duty Finder says a duty is ready.</summary>
    DutyPop = 4,

    /// <summary>Someone sent the character a tell.</summary>
    Tell = 8,

    All = Death | Stuck | DutyPop | Tell,
}

/// <summary>One frame's reading for <see cref="NeedsYouWatch.Tick"/>.</summary>
/// <param name="HandOff">A hand-off runs: Questionable, or a Go to giver, Lifestream task, AutoDuty run or Artisan craft Tsukimichi started.</param>
/// <param name="Dead">The character is dead.</param>
/// <param name="Moving">vnavmesh follows a path or is finding one.</param>
/// <param name="Position">The character's position; null when there is no character (loading, logged out).</param>
/// <param name="Now">A steady clock, in seconds.</param>
/// <param name="Pathfinding">vnavmesh is still finding a path: nothing moves yet, and that is progress, not a stall.</param>
/// <param name="Held">The game holds the character (a cutscene, a talk or another event, a loading screen): no stall counts.</param>
public readonly record struct NeedsYouFrame(bool HandOff, bool Dead, bool Moving, Vector3? Position, double Now, bool Pathfinding = false, bool Held = false);

/// <summary>
/// When to raise a "Needs you" alert, pure so it is tested without the game. Nothing is raised while no hand-off
/// runs, nor for a kind the player turned off. Death is raised when the character goes from alive to dead; stuck once
/// per stall (moving again, a pathfind still pending, vnavmesh stopping, or a cutscene, talk or loading screen re-arms
/// it); a duty pop and a tell each time they happen, and a walk whose recovery gave up through <see cref="Raise"/>. The same
/// kind is not raised again within <see cref="RepeatSeconds"/>, so a burst of tells or a death loop makes one alert a
/// minute, and the sound plays at most once every <see cref="SoundGapSeconds"/> whatever the kind.
/// </summary>
public sealed class NeedsYouWatch
{
    /// <summary>A character that should be walking and has not moved <see cref="StuckMove"/> in this long is stuck.</summary>
    public const double StuckSeconds = 30.0;

    /// <summary>
    /// Yalms the character must move for the stall clock to start again. Smaller than Go to giver's own 5 (which retries
    /// a path once before it gives up), so a character inching along a wall still counts as moving.
    /// </summary>
    public const float StuckMove = 3f;

    /// <summary>The same kind is not raised again within this many seconds.</summary>
    public const double RepeatSeconds = 60.0;

    /// <summary>The alert sound plays at most once in this many seconds.</summary>
    public const double SoundGapSeconds = 10.0;

    /// <summary>
    /// How long after a duty pop's alert the Duty Finder's own state is believed: the pop event can come a moment before
    /// the queue reads Ready.
    /// </summary>
    public const double PopSettleSeconds = 2.0;

    private readonly Dictionary<NeedsYouKind, double> lastRaised = [];
    private double lastSound = double.NegativeInfinity;
    private bool wasDead;
    private Vector3? anchor;
    private double anchorAt;
    private bool stallRaised;

    /// <summary>
    /// One frame. Returns the kinds raised now, among <paramref name="enabled"/>; <see cref="NeedsYouKind.None"/> on
    /// almost every frame.
    /// </summary>
    public NeedsYouKind Tick(in NeedsYouFrame frame, NeedsYouKind enabled)
    {
        var died = frame.Dead && !wasDead;
        wasDead = frame.Dead;
        if (!frame.HandOff || !double.IsFinite(frame.Now))
        {
            ResetStall();
            return NeedsYouKind.None;
        }

        var raised = NeedsYouKind.None;
        if (died && Allow(NeedsYouKind.Death, enabled, frame.Now))
        {
            raised |= NeedsYouKind.Death;
        }

        if (Stalled(frame) && Allow(NeedsYouKind.Stuck, enabled, frame.Now))
        {
            raised |= NeedsYouKind.Stuck;
        }

        return raised;
    }

    /// <summary>
    /// A duty pop or a tell at <paramref name="now"/>: true when it is raised (a hand-off runs, the kind is enabled and
    /// it was not raised in the last <see cref="RepeatSeconds"/>).
    /// </summary>
    public bool Event(NeedsYouKind kind, bool handOff, NeedsYouKind enabled, double now) =>
        handOff && double.IsFinite(now) && kind is NeedsYouKind.DutyPop or NeedsYouKind.Tell && Allow(kind, enabled, now);

    /// <summary>
    /// An alert raised from outside the frame, of any single kind (a Walk or Go to giver whose recovery gave up raises
    /// <see cref="NeedsYouKind.Stuck"/>): true when it is raised (the kind is enabled and was not raised in the last
    /// <see cref="RepeatSeconds"/>). The caller knows its own hand-off runs.
    /// </summary>
    public bool Raise(NeedsYouKind kind, NeedsYouKind enabled, double now) =>
        double.IsFinite(now) && kind is NeedsYouKind.Death or NeedsYouKind.Stuck or NeedsYouKind.DutyPop or NeedsYouKind.Tell && Allow(kind, enabled, now);

    /// <summary>
    /// Whether a duty pop's alert is over: the duty started (<paramref name="inDuty"/>), or the pop window closed
    /// (<paramref name="popOpen"/> false: the player committed, withdrew or let it lapse) at least
    /// <see cref="PopSettleSeconds"/> after the alert. A null <paramref name="popOpen"/> (the queue cannot be read now)
    /// leaves the alert up until the duty starts or the player dismisses it.
    /// </summary>
    public static bool DutyPopOver(bool? popOpen, bool inDuty, double secondsSinceRaised) =>
        inDuty || (popOpen == false && secondsSinceRaised >= PopSettleSeconds);

    /// <summary>True when the sound may play at <paramref name="now"/>; taking it starts the gap.</summary>
    public bool TakeSound(double now)
    {
        if (!double.IsFinite(now) || now - lastSound < SoundGapSeconds)
        {
            return false;
        }

        lastSound = now;
        return true;
    }

    private bool Allow(NeedsYouKind kind, NeedsYouKind enabled, double now)
    {
        if ((enabled & kind) == 0)
        {
            return false;
        }

        if (lastRaised.TryGetValue(kind, out var at) && now - at < RepeatSeconds)
        {
            return false;
        }

        lastRaised[kind] = now;
        return true;
    }

    /// <summary>
    /// True on the frame a stall reaches <see cref="StuckSeconds"/>; once per stall. A pathfind still pending counts as
    /// progress (a long path takes a while to find), and a cutscene, talk or loading screen starts the stall afresh.
    /// </summary>
    private bool Stalled(in NeedsYouFrame frame)
    {
        if (!frame.Moving || frame.Position is not { } here || frame.Dead || frame.Held)
        {
            ResetStall();
            return false;
        }

        if (anchor is not { } from || frame.Pathfinding || Vector3.Distance(from, here) >= StuckMove)
        {
            anchor = here;
            anchorAt = frame.Now;
            stallRaised = false;
            return false;
        }

        if (stallRaised || frame.Now - anchorAt < StuckSeconds)
        {
            return false;
        }

        stallRaised = true;
        return true;
    }

    private void ResetStall()
    {
        anchor = null;
        stallRaised = false;
    }
}
