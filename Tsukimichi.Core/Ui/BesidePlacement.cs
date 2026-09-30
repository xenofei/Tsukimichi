using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Placement of a small panel next to a game window it must never cover (the Duty Finder unlock hint, P13). Unlike
/// <see cref="OverlayGeometry.PlaceCard"/>, which clamps a card into the screen even when that puts it over its target,
/// a side is only taken when the whole panel fits on the screen on that side of the target; along that side the panel
/// slides (aligned with the target's near edge, then clamped) so it stays on screen. Pure and allocation-free.
/// </summary>
public static class BesidePlacement
{
    /// <summary>
    /// Top-left corner for a panel of <paramref name="size"/> beside <paramref name="target"/> inside
    /// <paramref name="bounds"/>, <paramref name="gap"/> away from it: to its right, else left, else below, else above.
    /// False when no side has room; the panel is then not to be drawn.
    /// </summary>
    public static bool TryPlace(in ScreenRect target, Vector2 size, in ScreenRect bounds, float gap, out Vector2 position, out CardSide side)
    {
        position = default;
        side = CardSide.Right;
        if (size.X <= 0f || size.Y <= 0f || size.X > bounds.Width || size.Y > bounds.Height)
        {
            return false;
        }

        // Right and left: aligned with the target's top, slid vertically into the screen.
        var y = Slide(target.Min.Y, size.Y, bounds.Min.Y, bounds.Max.Y);
        var right = target.Max.X + gap;
        if (right + size.X <= bounds.Max.X && right >= bounds.Min.X)
        {
            position = new Vector2(right, y);
            side = CardSide.Right;
            return true;
        }

        var left = target.Min.X - gap - size.X;
        if (left >= bounds.Min.X && left + size.X <= bounds.Max.X)
        {
            position = new Vector2(left, y);
            side = CardSide.Left;
            return true;
        }

        // Below and above: aligned with the target's left edge, slid horizontally into the screen.
        var x = Slide(target.Min.X, size.X, bounds.Min.X, bounds.Max.X);
        var below = target.Max.Y + gap;
        if (below + size.Y <= bounds.Max.Y && below >= bounds.Min.Y)
        {
            position = new Vector2(x, below);
            side = CardSide.Below;
            return true;
        }

        var above = target.Min.Y - gap - size.Y;
        if (above >= bounds.Min.Y && above + size.Y <= bounds.Max.Y)
        {
            position = new Vector2(x, above);
            side = CardSide.Above;
            return true;
        }

        return false;
    }

    /// <summary><paramref name="start"/> moved so a span of <paramref name="length"/> lies within [min, max] (the caller checked it fits).</summary>
    private static float Slide(float start, float length, float min, float max) => Math.Max(min, Math.Min(start, max - length));
}
