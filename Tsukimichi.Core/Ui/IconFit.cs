using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Where a game icon goes inside its box. Almost every icon is square and fills the box; a duty's own emblem
/// (<c>ContentFinderCondition.Icon</c>, a 136 × 168 card, see <see cref="Unlocks.DutyArt"/>) is not, and is drawn whole
/// and centred, never stretched.
/// </summary>
public static class IconFit
{
    /// <summary>
    /// The largest rectangle of the texture's aspect that fits <paramref name="min"/>..<paramref name="max"/>, centred;
    /// the box itself for a square texture or an unknown size.
    /// </summary>
    public static (Vector2 Min, Vector2 Max) Contain(Vector2 min, Vector2 max, float textureWidth, float textureHeight)
    {
        var box = max - min;
        if (!(textureWidth > 0f) || !(textureHeight > 0f) || !(box.X > 0f) || !(box.Y > 0f) || (textureWidth == textureHeight && box.X == box.Y))
        {
            return (min, max);
        }

        var scale = MathF.Min(box.X / textureWidth, box.Y / textureHeight);
        var size = new Vector2(textureWidth * scale, textureHeight * scale);
        var offset = (box - size) * 0.5f;
        return (min + offset, min + offset + size);
    }
}
