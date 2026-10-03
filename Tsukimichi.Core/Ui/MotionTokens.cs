namespace Tsukimichi.Core.Ui;

/// <summary>
/// The one table every animation in the plugin takes its pace from (feature plan v6 U8). Two tiers:
/// <list type="bullet">
/// <item><b>Interaction</b> (hover, selection, chevrons, reveals, content veils, popups, travel): 120–220 ms, at every
/// Flair level, off under Reduce motion. Entrances ease out; continuous targets use the exponential approach of
/// <see cref="MotionMath.Approach"/> at the rate <see cref="RateFor"/> gives for the token.</item>
/// <item><b>Moments</b> (the moon waxing on a completion): one-shot, never looping, never longer than a second, off under
/// Reduce motion.</item>
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

    /// <summary>Every interaction token, for tests and the glyph window.</summary>
    public static ReadOnlySpan<float> Interaction => [HoverIn, HoverOut, Select, Chevron, Reveal, Veil, Popup, Travel];

    /// <summary>The moon waxing to full when a quest the player can see is completed (a moment, not an interaction).</summary>
    public const float Wax = 0.6f;

    /// <summary>Where the waxing moon starts: the half moon, so the change reads as the moon filling, not appearing.</summary>
    public const float WaxFrom = 0.5f;

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
}
