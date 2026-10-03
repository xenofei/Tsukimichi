namespace Tsukimichi.Core.Ui;

/// <summary>Where a panel beside a game window stands in its life (<see cref="PanelPresence"/>).</summary>
public enum PanelPhase : byte
{
    /// <summary>Not drawn.</summary>
    Hidden,

    /// <summary>Its first frame: drawn unseen so its size is known before it is placed.</summary>
    Measuring,

    /// <summary>Up, with a subject: it rises in, then stays; buttons act.</summary>
    Shown,

    /// <summary>Its subject went away: it holds its last content for <see cref="MotionTokens.Linger"/>, then fades out.</summary>
    Lingering,
}

/// <summary>
/// The timing of a panel beside a game window (feature plan v6 M2): the panels beside the Journal, a quest offer or
/// turn-in, the Duty Finder and an item tooltip rise in instead of blinking, and do not strobe while the player arrows
/// through a list.
/// <list type="bullet">
/// <item>The first appearance is measured unseen for one frame, then fades in over <see cref="MotionTokens.Rise"/> while
/// rising <see cref="MotionTokens.RiseLogical"/> px.</item>
/// <item>A new subject while up (another quest, another duty) is no reason to vanish: the content dips and comes back
/// over <see cref="MotionTokens.Swap"/> while the panel stays where it is.</item>
/// <item>When the subject goes away the panel holds its last content for <see cref="MotionTokens.Linger"/>, then fades out
/// over <see cref="MotionTokens.Leave"/>; a subject arriving meanwhile takes over in place, so stepping past an unlocked
/// duty or a header in a list keeps the panel up.</item>
/// </list>
/// Under Reduce motion nothing fades or rises (the panel is simply there, and gone after the hold). Times are the ImGui
/// clock's seconds. Pure and allocation-free.
/// </summary>
public sealed class PanelPresence
{
    private double shownAt = double.NegativeInfinity;
    private double swapAt = double.NegativeInfinity;
    private double lostAt = double.NegativeInfinity;

    /// <summary>The phase as of the last <see cref="Show"/> or <see cref="Lose"/>.</summary>
    public PanelPhase Phase { get; private set; }

    /// <summary>Whether the panel's buttons act: only while it is up with a subject, never unseen or on its way out.</summary>
    public bool Interactive => Phase == PanelPhase.Shown;

    /// <summary>Whether the panel is drawn unseen this frame, to learn its size.</summary>
    public bool Measuring => Phase == PanelPhase.Measuring;

    /// <summary>
    /// The panel has a subject this frame (<paramref name="changed"/>: a different one from the last frame's). A hidden
    /// panel starts measuring, a measured one starts rising in, and one already up (or lingering) swaps its content in place.
    /// </summary>
    public void Show(double now, bool changed)
    {
        switch (Phase)
        {
            case PanelPhase.Hidden:
                Phase = PanelPhase.Measuring;
                break;
            case PanelPhase.Measuring:
                Phase = PanelPhase.Shown;
                shownAt = now;
                swapAt = double.NegativeInfinity;
                break;
            case PanelPhase.Lingering:
                Phase = PanelPhase.Shown;
                if (FadingOut(now))
                {
                    // Caught on its way out: it comes back as one swap rather than rising in again.
                    swapAt = now;
                }
                else if (changed)
                {
                    swapAt = now;
                }

                break;
            default:
                if (changed)
                {
                    swapAt = now;
                }

                break;
        }
    }

    /// <summary>
    /// The panel has no subject this frame. Returns whether it is still drawn (its last content, lingering or fading
    /// out); false once it is gone, or when it was never seen.
    /// </summary>
    public bool Lose(double now, bool animate)
    {
        switch (Phase)
        {
            case PanelPhase.Shown:
                Phase = PanelPhase.Lingering;
                lostAt = now;
                return true;
            case PanelPhase.Lingering:
                var over = MotionTokens.Linger + (animate ? MotionTokens.Leave : 0f);
                if (now - lostAt < over && now >= lostAt)
                {
                    return true;
                }

                Phase = PanelPhase.Hidden;
                return false;
            default:
                Phase = PanelPhase.Hidden;
                return false;
        }
    }

    /// <summary>Starts the entrance again (the panel moved to another side of its game window): it fades in where it now stands.</summary>
    public void Reappear(double now)
    {
        if (Phase == PanelPhase.Shown)
        {
            shownAt = now;
        }
    }

    /// <summary>Forgets everything: the next subject is measured and rises in.</summary>
    public void Reset()
    {
        Phase = PanelPhase.Hidden;
        shownAt = swapAt = lostAt = double.NegativeInfinity;
    }

    /// <summary>
    /// The whole panel's opacity at <paramref name="now"/>: 0 while measuring, rising in after it, 1 while up, fading
    /// out after the hold. Without <paramref name="animate"/> (Reduce motion) it is 1 whenever the panel is drawn.
    /// </summary>
    public float Alpha(double now, bool animate)
    {
        switch (Phase)
        {
            case PanelPhase.Shown:
                return animate ? MotionMath.EaseOutCubic(Progress(shownAt, now, MotionTokens.Rise)) : 1f;
            case PanelPhase.Lingering:
                if (!animate)
                {
                    return 1f;
                }

                var rise = MotionMath.EaseOutCubic(Progress(shownAt, now, MotionTokens.Rise));
                var leaving = (now - lostAt - MotionTokens.Linger) / MotionTokens.Leave;
                return leaving <= 0d ? rise : rise * (1f - MotionMath.EaseOutCubic((float)leaving));
            default:
                return 0f;
        }
    }

    /// <summary>How far below its place the panel stands, as a share of <see cref="MotionTokens.RiseLogical"/>: 1 as it appears, 0 once risen.</summary>
    public float Rise(double now, bool animate) =>
        animate && Phase is PanelPhase.Shown or PanelPhase.Lingering ? 1f - MotionMath.EaseOutCubic(Progress(shownAt, now, MotionTokens.Rise)) : 0f;

    /// <summary>The content's opacity: <see cref="MotionTokens.SwapAlpha"/> while a new subject takes over in place, 1 otherwise.</summary>
    public float ContentAlpha(double now, bool animate)
    {
        if (!animate || Phase is not (PanelPhase.Shown or PanelPhase.Lingering))
        {
            return 1f;
        }

        var elapsed = now - swapAt;
        return elapsed < 0d || elapsed >= MotionTokens.Swap ? 1f : MotionTokens.SwapAlpha((float)(elapsed / MotionTokens.Swap));
    }

    private bool FadingOut(double now) => now - lostAt >= MotionTokens.Linger;

    /// <summary>Progress 0..1 since <paramref name="start"/> over <paramref name="seconds"/>; 1 once over (or before the clock's start).</summary>
    private static float Progress(double start, double now, float seconds)
    {
        var elapsed = now - start;
        return !double.IsFinite(elapsed) || elapsed >= seconds || elapsed < 0d ? 1f : (float)(elapsed / seconds);
    }
}
