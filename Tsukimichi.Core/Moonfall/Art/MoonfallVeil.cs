using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The veil round a round peg (dress2.veil: 1 − k × blur₆(smooth(18, 0, distance to the peg's edge))), as a sprite the
/// in-play drawing puts under each live peg at its own place, faded with the peg as it clears. Baking it into the scene
/// left a dark peg-shaped place wherever a peg had cleared (runtime supervision round 1, M1); a sprite goes with its peg
/// and follows a mover along its path. White, alpha the veil's shape at k = 1 (the drawing tints it black at the
/// recipe's k).
/// </summary>
public static class MoonfallVeil
{
    /// <summary>From the peg's centre to the sprite's edge, in peg radii (the veil reaches 18 units past the edge, blurred 6 more).</summary>
    public const float Reach = 4.6f;

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
