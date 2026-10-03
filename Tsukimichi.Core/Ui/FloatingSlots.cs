using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>The layers that float over the main window's body, in the order they are given a place.</summary>
public enum FloatingLayer
{
    /// <summary>The Undo toast after a data change: bottom centre.</summary>
    Undo = 0,

    /// <summary>The notice dock: bottom right.</summary>
    Dock = 1,

    /// <summary>A passing hint: bottom left.</summary>
    Hint = 2,
}

/// <summary>
/// The one slot manager for everything that floats over the main window's body (feature plan v6 U2): the notice dock,
/// the Undo toast and hints. Each has a home along the body's bottom edge (the Undo toast centred, the dock to the
/// right, a hint to the left), and the rules are the same for all of them: never cover the status bar (the body's
/// bottom is its top), never cover the selected row, and never cover each other. A layer that would collide stacks
/// above the ones already placed; one that would cover the selected row moves to the top edge instead, or beside the
/// row when the body is too short for either. Pure geometry, so the rules are tested.
/// </summary>
public static class FloatingSlots
{
    /// <summary>
    /// Places <paramref name="layer"/>, <paramref name="size"/> big, inside <paramref name="area"/> (the body, its
    /// bottom at the status bar's top) with <paramref name="margin"/> from its edges, clear of
    /// <paramref name="keepClear"/> (the selected row; empty when none is on screen) and of every rectangle in
    /// <paramref name="taken"/> (the layers placed before it).
    /// </summary>
    public static ScreenRect Place(FloatingLayer layer, Vector2 size, in ScreenRect area, in ScreenRect keepClear, ReadOnlySpan<ScreenRect> taken, float margin)
    {
        var width = MathF.Max(0f, MathF.Min(size.X, area.Width - (2f * margin)));
        var height = MathF.Max(0f, size.Y);
        var x = layer switch
        {
            FloatingLayer.Undo => area.Min.X + ((area.Width - width) * 0.5f),
            FloatingLayer.Dock => area.Max.X - margin - width,
            _ => area.Min.X + margin,
        };
        x = MathF.Round(MathF.Max(area.Min.X, x));

        var top = area.Min.Y + margin;
        var bottom = area.Max.Y - margin;
        var gap = margin * 0.5f;

        // Home: the bottom edge, stacked above whatever is already there.
        var rect = Stack(new ScreenRect(new Vector2(x, bottom - height), new Vector2(x + width, bottom)), taken, gap, up: true);
        if (Fits(rect, top, bottom) && !Overlaps(rect, keepClear))
        {
            return rect;
        }

        // The selected row is down there: the top edge, stacked below what is there.
        var high = Stack(new ScreenRect(new Vector2(x, top), new Vector2(x + width, top + height)), taken, gap, up: false);
        if (Fits(high, top, bottom) && !Overlaps(high, keepClear))
        {
            return high;
        }

        // Too short for either: just above the row, else just below it, else as high as it goes.
        if (!keepClear.IsEmpty)
        {
            var above = new ScreenRect(new Vector2(x, keepClear.Min.Y - gap - height), new Vector2(x + width, keepClear.Min.Y - gap));
            if (above.Min.Y >= top)
            {
                return above;
            }

            var below = new ScreenRect(new Vector2(x, keepClear.Max.Y + gap), new Vector2(x + width, keepClear.Max.Y + gap + height));
            if (below.Max.Y <= bottom)
            {
                return below;
            }
        }

        return Clamp(Fits(rect, top, bottom) ? rect : high, area);
    }

    /// <summary>Moves <paramref name="rect"/> up (or down) past every rectangle of <paramref name="taken"/> it meets.</summary>
    private static ScreenRect Stack(ScreenRect rect, ReadOnlySpan<ScreenRect> taken, float gap, bool up)
    {
        // Each pass clears at least one rectangle for good, so this many passes always settle it.
        for (var pass = 0; pass <= taken.Length; pass++)
        {
            var moved = false;
            foreach (var other in taken)
            {
                if (!Overlaps(rect, other.Expand(gap * 0.5f)))
                {
                    continue;
                }

                var dy = up ? other.Min.Y - gap - rect.Max.Y : other.Max.Y + gap - rect.Min.Y;
                rect = new ScreenRect(rect.Min + new Vector2(0f, dy), rect.Max + new Vector2(0f, dy));
                moved = true;
            }

            if (!moved)
            {
                break;
            }
        }

        return rect;
    }

    private static bool Fits(in ScreenRect rect, float top, float bottom) => rect.Min.Y >= top - 0.5f && rect.Max.Y <= bottom + 0.5f;

    private static bool Overlaps(in ScreenRect a, in ScreenRect b) =>
        !a.IsEmpty && !b.IsEmpty && a.Min.X < b.Max.X && b.Min.X < a.Max.X && a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;

    /// <summary>Inside <paramref name="area"/> vertically; a layer taller than the body starts at its top.</summary>
    private static ScreenRect Clamp(in ScreenRect rect, in ScreenRect area)
    {
        var y = MathF.Max(area.Min.Y, MathF.Min(rect.Min.Y, area.Max.Y - rect.Height));
        return new ScreenRect(new Vector2(rect.Min.X, y), new Vector2(rect.Max.X, y + rect.Height));
    }
}
