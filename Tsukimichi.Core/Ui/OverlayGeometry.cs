using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>An axis-aligned screen rectangle in pixels; empty when either side is not positive.</summary>
public readonly record struct ScreenRect(Vector2 Min, Vector2 Max)
{
    public float Width => Max.X - Min.X;

    public float Height => Max.Y - Min.Y;

    public Vector2 Size => Max - Min;

    public Vector2 Center => (Min + Max) * 0.5f;

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public static ScreenRect FromSize(Vector2 min, Vector2 size) => new(min, min + size);

    /// <summary>Smallest rectangle containing both; an empty side is ignored.</summary>
    public static ScreenRect Union(in ScreenRect a, in ScreenRect b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        return new ScreenRect(Vector2.Min(a.Min, b.Min), Vector2.Max(a.Max, b.Max));
    }

    /// <summary>Overlap of both; empty when they do not touch.</summary>
    public static ScreenRect Intersect(in ScreenRect a, in ScreenRect b) =>
        new(Vector2.Max(a.Min, b.Min), Vector2.Min(a.Max, b.Max));

    /// <summary>Grown by <paramref name="pad"/> on every side (shrunk when negative).</summary>
    public ScreenRect Expand(float pad) => new(Min - new Vector2(pad), Max + new Vector2(pad));

    public bool Contains(Vector2 p) => p.X >= Min.X && p.X < Max.X && p.Y >= Min.Y && p.Y < Max.Y;

    public float Area => IsEmpty ? 0f : Width * Height;
}

/// <summary>Where a tutorial card was placed relative to its target.</summary>
public enum CardSide
{
    Right,
    Left,
    Below,
    Above,
}

/// <summary>
/// Pure geometry for the tutorial overlay: the dimming layer as a set of rectangles around one or more holes
/// (ImGui has no cutouts, so the area minus the holes is drawn as bands), and card placement beside a target
/// that flips sides when the preferred side runs out of screen. No ImGui dependency; allocation-free.
/// </summary>
public static class OverlayGeometry
{
    /// <summary>Order in which sides are tried for a card; the first with room wins.</summary>
    private static readonly CardSide[] SideOrder = [CardSide.Right, CardSide.Left, CardSide.Below, CardSide.Above];

    /// <summary>
    /// Writes non-overlapping rectangles covering <paramref name="area"/> minus every hole into
    /// <paramref name="output"/> and returns how many were written. Each hole can split every piece into up to four
    /// bands, so <paramref name="output"/> and <paramref name="scratch"/> need 4^holes entries; when a step would
    /// overflow them the hole is left undimmed rather than throwing.
    /// </summary>
    public static int Cutout(in ScreenRect area, ReadOnlySpan<ScreenRect> holes, Span<ScreenRect> output, Span<ScreenRect> scratch)
    {
        if (area.IsEmpty || output.Length == 0)
        {
            return 0;
        }

        output[0] = area;
        var count = 1;
        foreach (var hole in holes)
        {
            if (hole.IsEmpty)
            {
                continue;
            }

            var n = 0;
            for (var i = 0; i < count; i++)
            {
                if (n + 4 > scratch.Length)
                {
                    return count;
                }

                n += Subtract(in output[i], in hole, scratch[n..]);
            }

            if (n > output.Length)
            {
                return count;
            }

            scratch[..n].CopyTo(output);
            count = n;
        }

        return count;
    }

    /// <summary>
    /// <paramref name="piece"/> minus <paramref name="hole"/> as up to four bands (top and bottom at full width,
    /// left and right at the hole's height) written to <paramref name="output"/>; the piece itself when they do
    /// not overlap. Returns the number written.
    /// </summary>
    public static int Subtract(in ScreenRect piece, in ScreenRect hole, Span<ScreenRect> output)
    {
        var h = ScreenRect.Intersect(in piece, in hole);
        if (h.IsEmpty)
        {
            output[0] = piece;
            return 1;
        }

        var n = 0;
        if (h.Min.Y > piece.Min.Y)
        {
            output[n++] = new ScreenRect(piece.Min, new Vector2(piece.Max.X, h.Min.Y));
        }

        if (h.Max.Y < piece.Max.Y)
        {
            output[n++] = new ScreenRect(new Vector2(piece.Min.X, h.Max.Y), piece.Max);
        }

        if (h.Min.X > piece.Min.X)
        {
            output[n++] = new ScreenRect(new Vector2(piece.Min.X, h.Min.Y), new Vector2(h.Min.X, h.Max.Y));
        }

        if (h.Max.X < piece.Max.X)
        {
            output[n++] = new ScreenRect(new Vector2(h.Max.X, h.Min.Y), new Vector2(piece.Max.X, h.Max.Y));
        }

        return n;
    }

    /// <summary>
    /// Top-left corner for a card of <paramref name="cardSize"/> next to <paramref name="target"/>: to its right,
    /// else left, else below, else above, whichever first fits inside <paramref name="bounds"/> with
    /// <paramref name="gap"/> between them. The card's near edge aligns with the target's; the result is clamped
    /// into the bounds either way.
    /// </summary>
    public static Vector2 PlaceCard(in ScreenRect target, Vector2 cardSize, in ScreenRect bounds, float gap, out CardSide side)
    {
        foreach (var candidate in SideOrder)
        {
            var pos = Anchor(in target, cardSize, gap, candidate);
            if (Fits(pos, cardSize, in bounds))
            {
                side = candidate;
                return Clamp(pos, cardSize, in bounds);
            }
        }

        side = CardSide.Right;
        return Clamp(Anchor(in target, cardSize, gap, CardSide.Right), cardSize, in bounds);
    }

    /// <summary>Top-left corner that centres a box of <paramref name="size"/> in <paramref name="bounds"/>, clamped inside it.</summary>
    public static Vector2 CenterIn(in ScreenRect bounds, Vector2 size) =>
        Clamp(bounds.Center - size * 0.5f, size, in bounds);

    /// <summary>Moves <paramref name="pos"/> so a box of <paramref name="size"/> lies inside <paramref name="bounds"/> (top-left wins when it cannot).</summary>
    public static Vector2 Clamp(Vector2 pos, Vector2 size, in ScreenRect bounds)
    {
        var maxPos = bounds.Max - size;
        var x = Math.Max(bounds.Min.X, Math.Min(pos.X, maxPos.X));
        var y = Math.Max(bounds.Min.Y, Math.Min(pos.Y, maxPos.Y));
        return new Vector2(x, y);
    }

    private static Vector2 Anchor(in ScreenRect target, Vector2 cardSize, float gap, CardSide side) => side switch
    {
        CardSide.Right => new Vector2(target.Max.X + gap, target.Min.Y),
        CardSide.Left => new Vector2(target.Min.X - gap - cardSize.X, target.Min.Y),
        CardSide.Below => new Vector2(target.Min.X, target.Max.Y + gap),
        _ => new Vector2(target.Min.X, target.Min.Y - gap - cardSize.Y),
    };

    private static bool Fits(Vector2 pos, Vector2 size, in ScreenRect bounds) =>
        pos.X >= bounds.Min.X && pos.Y >= bounds.Min.Y && pos.X + size.X <= bounds.Max.X && pos.Y + size.Y <= bounds.Max.Y;
}
