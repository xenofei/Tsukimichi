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
    public static bool TryPlace(in ScreenRect target, Vector2 size, in ScreenRect bounds, float gap, out Vector2 position, out CardSide side) =>
        TryPlace(in target, size, in bounds, gap, null, out position, out side);

    /// <summary>
    /// <see cref="TryPlace(in ScreenRect, Vector2, in ScreenRect, float, out Vector2, out CardSide)"/> with a sticky side
    /// (feature plan v6 M2): <paramref name="preferred"/> (the side the panel stood on last frame) is tried first and
    /// kept while the panel still fits there, so a brief that grows a line does not send the panel to the other side
    /// of the game window. Only when that side has no room does the usual order (right, left, below, above) decide.
    /// </summary>
    public static bool TryPlace(in ScreenRect target, Vector2 size, in ScreenRect bounds, float gap, CardSide? preferred, out Vector2 position, out CardSide side)
    {
        position = default;
        side = CardSide.Right;
        if (size.X <= 0f || size.Y <= 0f || size.X > bounds.Width || size.Y > bounds.Height)
        {
            return false;
        }

        if (preferred is { } sticky && TrySide(in target, size, in bounds, gap, sticky, out position))
        {
            side = sticky;
            return true;
        }

        ReadOnlySpan<CardSide> order = [CardSide.Right, CardSide.Left, CardSide.Below, CardSide.Above];
        foreach (var candidate in order)
        {
            if (TrySide(in target, size, in bounds, gap, candidate, out position))
            {
                side = candidate;
                return true;
            }
        }

        position = default;
        return false;
    }

    /// <summary>Whether the panel fits wholly on <paramref name="side"/> of the target, and where it then goes.</summary>
    private static bool TrySide(in ScreenRect target, Vector2 size, in ScreenRect bounds, float gap, CardSide side, out Vector2 position)
    {
        position = default;

        // Right and left: aligned with the target's top, slid vertically into the screen.
        if (side is CardSide.Right or CardSide.Left)
        {
            var y = Slide(target.Min.Y, size.Y, bounds.Min.Y, bounds.Max.Y);
            var x = side == CardSide.Right ? target.Max.X + gap : target.Min.X - gap - size.X;
            if (x < bounds.Min.X || x + size.X > bounds.Max.X)
            {
                return false;
            }

            position = new Vector2(x, y);
            return true;
        }

        // Below and above: aligned with the target's left edge, slid horizontally into the screen.
        var left = Slide(target.Min.X, size.X, bounds.Min.X, bounds.Max.X);
        var top = side == CardSide.Below ? target.Max.Y + gap : target.Min.Y - gap - size.Y;
        if (top < bounds.Min.Y || top + size.Y > bounds.Max.Y)
        {
            return false;
        }

        position = new Vector2(left, top);
        return true;
    }

    /// <summary><paramref name="start"/> moved so a span of <paramref name="length"/> lies within [min, max] (the caller checked it fits).</summary>
    private static float Slide(float start, float length, float min, float max) => Math.Max(min, Math.Min(start, max - length));
}
