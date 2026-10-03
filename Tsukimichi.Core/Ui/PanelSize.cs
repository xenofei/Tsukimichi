using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The size of a panel beside a game window (feature plan v6 M2): the size its content asked for last frame, and the
/// size it shows, which eases there over <see cref="MotionTokens.Select"/> instead of the window being re-measured
/// unseen each time its content changes shape (the old two-frame blink). The first measurement, and any made while the
/// panel is measured unseen, is taken at once. Whole pixels, so the frame does not shimmer while it eases. Pure and
/// allocation-free.
/// </summary>
public sealed class PanelSize
{
    private Vector2 target;
    private Vector2 shown;
    private bool known;

    /// <summary>Whether a size was measured since the last <see cref="Reset"/>.</summary>
    public bool Known => known;

    /// <summary>Forgets the size: the next measurement is taken at once.</summary>
    public void Reset() => known = false;

    /// <summary>
    /// Notes the size the content asked for (<paramref name="extent"/>, padding included). The first one, and one taken
    /// while <paramref name="measuring"/>, is shown at once; later ones are eased to.
    /// </summary>
    public void Measure(Vector2 extent, bool measuring)
    {
        if (!float.IsFinite(extent.X) || !float.IsFinite(extent.Y) || extent.X <= 0f || extent.Y <= 0f)
        {
            return;
        }

        target = extent;
        if (!known || measuring)
        {
            shown = extent;
            known = true;
        }
    }

    /// <summary>
    /// The size to show this frame, <paramref name="deltaSeconds"/> after the last: eased towards the measured one, or
    /// that one itself without <paramref name="animate"/> (Reduce motion). Rounded up to whole pixels.
    /// </summary>
    public Vector2 Shown(float deltaSeconds, bool animate)
    {
        shown = animate
            ? new Vector2(MotionMath.Approach(shown.X, target.X, MotionMath.SelectRate, deltaSeconds), MotionMath.Approach(shown.Y, target.Y, MotionMath.SelectRate, deltaSeconds))
            : target;

        // Within half a pixel counts as there, so the size settles instead of creeping.
        if (MathF.Abs(shown.X - target.X) < 0.5f)
        {
            shown.X = target.X;
        }

        if (MathF.Abs(shown.Y - target.Y) < 0.5f)
        {
            shown.Y = target.Y;
        }

        return new Vector2(MathF.Ceiling(shown.X), MathF.Ceiling(shown.Y));
    }
}
