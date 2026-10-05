using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The veil round a round peg (dress2.veil: 1 − k × blur₆(smooth(18, 0, distance to the peg's edge))), as a sprite the
/// in-play drawing puts under each live peg at its own place, faded with the peg as it clears. Baking it into the scene
/// left a dark peg-shaped place wherever a peg had cleared (runtime supervision round 1, M1); a sprite goes with its peg
/// and follows a mover along its path. White, alpha the veil's shape at k = 1 (the drawing tints it black at the
/// recipe's k). A brick carries the same veil as a row of these sprites along its middle line, each at
/// <see cref="BrickAlpha"/>, so a cleared brick leaves nothing behind either.
/// </summary>
public static class MoonfallVeil
{
    /// <summary>From the peg's centre to the sprite's edge, in peg radii (the veil reaches 18 units past the edge, blurred 6 more).</summary>
    public const float Reach = 4.6f;

    /// <summary>How far apart a brick's veil sprites sit along its middle line, units.</summary>
    public const float BrickSpacing = 10f;

    /// <summary>
    /// How many of a brick's sprites cover a point on its middle line, weighted by the sprite's shape (1 at the nearest,
    /// two each at 10, 20, 30 and 40 units: 1 + 2 × (1 + 0.9 + 0.4 + 0.05)): the alpha each takes so that together
    /// they darken the brick's line as one veil of k does.
    /// </summary>
    public const float BrickOverlap = 5.7f;

    /// <summary>Each of a brick's sprites' strength for a veil of <paramref name="k"/>.</summary>
    public static float BrickAlpha(float k) => 1f - MathF.Pow(1f - Math.Clamp(k, 0f, 0.99f), 1f / BrickOverlap);

    /// <summary>
    /// Where a brick's veil sprites sit: every <see cref="BrickSpacing"/> units along a straight brick from (x, y) to
    /// (x2, y2), or along a curved brick's arc (centre x, y, radius, start and sweep in radians); at most
    /// <paramref name="into"/>'s length. Returns how many.
    /// </summary>
    public static int BrickSpots(PegShape shape, float x, float y, float x2, float y2, float radius, float start, float sweep, Span<Vector2> into)
    {
        var count = 0;
        if (shape == PegShape.Line)
        {
            var a = new Vector2(x, y);
            var b = new Vector2(x2, y2);
            var n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / BrickSpacing));
            for (var i = 0; i <= n && count < into.Length; i++)
            {
                into[count++] = Vector2.Lerp(a, b, i / (float)n);
            }
        }
        else if (shape == PegShape.Arc)
        {
            var n = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweep) * radius / BrickSpacing));
            for (var i = 0; i <= n && count < into.Length; i++)
            {
                var t = start + (sweep * i / n);
                into[count++] = new Vector2(x + (radius * MathF.Cos(t)), y + (radius * MathF.Sin(t)));
            }
        }

        return count;
    }

    /// <summary>The sprite, <paramref name="size"/> pixels square, for a peg of <see cref="MoonfallRules.PegRadius"/>.</summary>
    public static MoonfallRgba Sprite(int size = 96)
    {
        var r = (float)MoonfallRules.PegRadius;
        var unitsPerPx = 2 * Reach * r / size;
        var plane = new MoonfallPlane(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = ((x + 0.5f) * unitsPerPx) - (Reach * r);
                var dy = ((y + 0.5f) * unitsPerPx) - (Reach * r);
                var edge = MathF.Max(0, MathF.Sqrt((dx * dx) + (dy * dy)) - r);
                plane.Data[(y * size) + x] = MoonfallColor.Smooth(18f, 0f, edge);
            }
        }

        var soft = MoonfallFilters.Blur(plane, 6f / unitsPerPx);
        var bytes = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            bytes[i * 4] = bytes[(i * 4) + 1] = bytes[(i * 4) + 2] = 255;
            bytes[(i * 4) + 3] = MoonfallImage.ToByte(soft.Data[i]);
        }

        return new MoonfallRgba(size, size, bytes, new Vector4(0, 0, size, size));
    }
}
