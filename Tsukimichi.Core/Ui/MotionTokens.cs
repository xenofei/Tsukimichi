namespace Tsukimichi.Core.Ui;

/// <summary>
/// The one table every animation in the plugin takes its pace from (feature plan v6 U8). Two tiers:
/// <list type="bullet">
/// <item><b>Interaction</b> (hover, selection, chevrons, reveals, content veils, popups, travel): 120–220 ms, at every
/// Flair level, off under Reduce motion. Entrances ease out; continuous targets use the exponential approach of
/// <see cref="MotionMath.Approach"/> at the rate <see cref="RateFor"/> gives for the token.</item>
/// <item><b>Moments</b> (the moon waxing on a completion, the road glint, the Ready halo, the Todo overlay's completion
/// beat): one-shot, never looping, never longer than a second, off under Reduce motion, at a soft gold that never
/// passes <see cref="MomentPeak"/>.</item>
/// </list>
/// Text the player is reading only ever changes opacity: nothing a player reads slides, grows or moves (the fixed-frame
/// rule of owner point 3). Durations are in seconds.
/// </summary>
public static class MotionTokens
{
    /// <summary>The shortest interaction token.</summary>
    public const float Shortest = 0.12f;

    /// <summary>The longest interaction token.</summary>
    public const float Longest = 0.22f;

    /// <summary>A hover wash fading in.</summary>
    public const float HoverIn = 0.12f;

    /// <summary>A hover wash fading out: a little slower than in, so a pass over a list leaves a soft trail.</summary>
    public const float HoverOut = 0.18f;

    /// <summary>A selection ring and wash settling.</summary>
    public const float Select = 0.15f;

    /// <summary>A disclosure chevron turning.</summary>
    public const float Chevron = 0.14f;

    /// <summary>Something appearing in place (expanded children, a chip, a toast): opacity only.</summary>
    public const float Reveal = 0.14f;

    /// <summary>The veil over a pane whose content was swapped (tab, quest, filter): opacity only.</summary>
    public const float Veil = 0.14f;

    /// <summary>A tooltip or popup fading in.</summary>
    public const float Popup = 0.12f;

    /// <summary>An ornament travelling between two places (the rail's bead between stations).</summary>
    public const float Travel = 0.22f;

    /// <summary>
    /// A panel beside a game window rising into place (feature plan v6 M2, "moonrise"): it fades in while it rises
    /// <see cref="RiseLogical"/> px. The Todo overlay fades in and out over the same time when it opens or steps aside.
    /// </summary>
    public const float Rise = 0.16f;

    /// <summary>A panel's content changing in place (another quest, another duty): its text dips to <see cref="SwapDip"/> and back.</summary>
    public const float Swap = 0.12f;

    /// <summary>
    /// How long a panel beside a game window stays up after its subject went away (the player arrowing through the
    /// Duty Finder or the Journal), so the next subject replaces it instead of the panel blinking out and back.
    /// </summary>
    public const float Linger = 0.15f;

    /// <summary>A panel leaving: shorter than its entrance, as every exit is.</summary>
    public const float Leave = 0.12f;

    /// <summary>Every interaction token, for tests and the glyph window.</summary>
    public static ReadOnlySpan<float> Interaction => [HoverIn, HoverOut, Select, Chevron, Reveal, Veil, Popup, Travel, Rise, Swap, Linger, Leave];

    /// <summary>How far a panel rises as it appears, in logical px: a hint of movement, never a slide.</summary>
    public const float RiseLogical = 4f;

    /// <summary>The lowest a panel's content dims to while it changes in place.</summary>
    public const float SwapDip = 0.4f;

    /// <summary>The moon waxing to full when a quest the player can see is completed (a moment, not an interaction).</summary>
    public const float Wax = 0.6f;

    /// <summary>Where the waxing moon starts: the half moon, so the change reads as the moon filling, not appearing.</summary>
    public const float WaxFrom = 0.5f;

    /// <summary>The glint running once along a tree road when a quest under it is completed (Full flair only).</summary>
    public const float Glint = 0.6f;

    /// <summary>The soft halo that swells once round a quest's moon when it becomes Ready, or when it is completed.</summary>
    public const float Halo = 0.6f;

    /// <summary>How far that halo swells past the moon's rim, in logical px.</summary>
    public const float HaloLogical = 4f;

    /// <summary>
    /// The brightest any moment draws (a glint, a halo, a stripe flash), as an alpha of the gold: a soft cue, never a
    /// flash, for photosensitive players.
    /// </summary>
    public const float MomentPeak = 0.6f;

    /// <summary>
    /// The Todo overlay's completion beat: a finished row stays this long as a ghost (its moon fills, one halo, then it
    /// fades out where it stood, with no collapse).
    /// </summary>
    public const float Beat = 0.6f;

    /// <summary>How long after a completion a count that grew may still start a road glint.</summary>
    public const float CompletionWindow = 1.5f;

    /// <summary>
    /// The shooting star (docs/design/v7/ui/spec.md §3.6): one streak across the largest empty sky when a quest is
    /// completed (Full only, its own setting), or, far fainter, every few minutes at rest (Revision 3).
    /// </summary>
    public const float Meteor = 0.7f;

    /// <summary>
    /// The completion meteor's head at its brightest: above every star (the near stars reach .60), so it reads as a
    /// meteor and not a moving star. The one moment above <see cref="MomentPeak"/>: under a second, small and cool white.
    /// </summary>
    public const float MeteorPeak = 0.80f;

    /// <summary>The ambient meteor's head at its brightest (Revision 3): a rare, faint one.</summary>
    public const float AmbientMeteorPeak = 0.45f;

    /// <summary>The tail's alpha where it meets the head, at the completion meteor's peak; it fades to 0 along its length.</summary>
    public const float MeteorTailFrom = 0.45f;

    /// <summary>The share of <see cref="Meteor"/> the head takes to fade in.</summary>
    public const float MeteorFadeIn = 0.08f;

    /// <summary>The fewest seconds between two completion meteors, and how long one keeps an ambient meteor away.</summary>
    public const float MeteorCooldown = 30f;

    /// <summary>The ambient meteor's interval, in seconds of focused time (Revision 3): a random 3 to 6 minutes.</summary>
    public const float AmbientMeteorMinSeconds = 180f;

    public const float AmbientMeteorMaxSeconds = 360f;

    /// <summary>
    /// The moving night sky's drift (Revision 3), logical px a minute, right to left: you notice it over minutes, never
    /// second to second.
    /// </summary>
    public const float SkyDriftPxPerMinute = 6f;

    /// <summary>How far the stars fade in and out at a sky rect's left and right edges as they drift, logical px.</summary>
    public const float SkyEdgeFadeLogical = 10f;

    /// <summary>Every moment token: each one-shot, none over a second.</summary>
    public static ReadOnlySpan<float> Moments => [Wax, Glint, Halo, Beat, Meteor];

    /// <summary>ln 10: an exponential approach at rate k covers 90 % of the way in ln 10 / k seconds.</summary>
    public const float Ln10 = 2.30258509f;

    /// <summary>The approach rate (per second) that covers 90 % of the way in <paramref name="seconds"/>; 0 for none.</summary>
    public static float RateFor(float seconds) => seconds > 0f && float.IsFinite(seconds) ? Ln10 / seconds : 0f;

    /// <summary>The seconds an approach at <paramref name="rate"/> takes to cover 90 % of the way; 0 for no rate.</summary>
    public static float SecondsFor(float rate) => rate > 0f && float.IsFinite(rate) ? Ln10 / rate : 0f;

    /// <summary>
    /// The lit fraction of a waxing moon at <paramref name="progress"/> (0..1 over <see cref="Wax"/>, as a pulse reports
    /// it): from <see cref="WaxFrom"/> to full, eased out. -1 when no wax is playing (a negative or non-finite progress,
    /// or 1 and beyond), so the glyph drawer draws the state's own moon.
    /// </summary>
    public static float WaxFraction(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress >= 1f)
        {
            return -1f;
        }

        return WaxFrom + ((1f - WaxFrom) * MotionMath.EaseOutCubic(progress));
    }

    /// <summary>
    /// A moment's soft gold at <paramref name="progress"/> (0..1): <see cref="MomentPeak"/> fading out to nothing, eased
    /// so most of the light goes early. 0 when no moment is playing (negative, non-finite, or 1 and beyond).
    /// </summary>
    public static float MomentAlpha(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress >= 1f)
        {
            return 0f;
        }

        return MomentPeak * (1f - MotionMath.EaseOutCubic(progress));
    }

    /// <summary>
    /// A meteor's head alpha at <paramref name="progress"/> (0..1 over <see cref="Meteor"/>): up to
    /// <paramref name="peak"/> over the first <see cref="MeteorFadeIn"/>, then <see cref="MomentAlpha"/>'s eased fade
    /// scaled to that peak. 0 when none is playing.
    /// </summary>
    public static float MeteorAlpha(float progress, float peak = MeteorPeak)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress >= 1f || !(peak > 0f))
        {
            return 0f;
        }

        if (progress < MeteorFadeIn)
        {
            return peak * (progress / MeteorFadeIn);
        }

        return MomentAlpha((progress - MeteorFadeIn) / (1f - MeteorFadeIn)) / MomentPeak * peak;
    }

    /// <summary>
    /// The opacity of content changing in place at <paramref name="progress"/> (0..1 over <see cref="Swap"/>): down to
    /// <see cref="SwapDip"/> at the middle and back to 1. 1 when no change is playing.
    /// </summary>
    public static float SwapAlpha(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress >= 1f)
        {
            return 1f;
        }

        var depth = 1f - MathF.Abs((2f * progress) - 1f);
        return 1f - ((1f - SwapDip) * depth);
    }
}
