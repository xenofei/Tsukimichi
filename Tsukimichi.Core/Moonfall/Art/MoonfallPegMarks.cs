using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>One mark of the colour-blind assist.</summary>
public enum MoonfallPegMark : byte
{
    /// <summary>Blue stays plain.</summary>
    None,

    /// <summary>A crescent, on orange.</summary>
    Crescent,

    /// <summary>A leaf, pointed at both ends with its midrib, on green.</summary>
    Leaf,

    /// <summary>A four-point star, 55% of the peg, on purple (drawn dark, over its light rim).</summary>
    Star,

    /// <summary>The star's light rim.</summary>
    StarRim,
}

/// <summary>
/// "Peg marks", the colour-blind assist (spec-rich2.md §4, play2.mark; decision 27: off by default, with a first-run
/// hint): a mark engraved on each kind's face, because the kinds differ by hue alone. The marks are made once as a small
/// alpha sheet (white, alpha the mark, softened at its edge), drawn over each peg at its size: the crescent, leaf and star
/// in a dark engraving ink, the star's rim in moonstone. Pure, so the shapes are tested without the game.
/// </summary>
public static class MoonfallPegMarks
{
    /// <summary>The cells in the sheet, in <see cref="MoonfallPegMark"/> order from <see cref="MoonfallPegMark.Crescent"/>.</summary>
    public const int Cells = 4;

    /// <summary>The engraving's ink and strength.</summary>
    public static readonly Vector4 Ink = new(0x1A / 255f, 0x0C / 255f, 0x04 / 255f, 0.78f);

    /// <summary>The star rim's ink and strength.</summary>
    public static readonly Vector4 RimInk = new(0xF4 / 255f, 0xEC / 255f, 0xFF / 255f, 0.8f);

    /// <summary>The mark a peg of <paramref name="colour"/> carries.</summary>
    public static MoonfallPegMark For(PegColour colour) => colour switch
    {
        PegColour.Orange => MoonfallPegMark.Crescent,
        PegColour.Green => MoonfallPegMark.Leaf,
        PegColour.Purple => MoonfallPegMark.Star,
        _ => MoonfallPegMark.None,
    };

    /// <summary>The least a brick's mark box spans on screen, pixels: at the 640 window a brick's 10-unit thickness is 7 px,
    /// where the star and the crescent shrink to a dot (UX runtime round 2, m1); a round peg's mark is 12-13 px there.</summary>
    public const float MinBrickMarkPixels = 11f;

    /// <summary>
    /// Where a piece's mark is drawn, board units: a round peg's over its face (its radius); a brick's once, upright, at the
    /// middle of its middle line, sized to its thickness (a line brick's midpoint; a curved brick's at half its sweep), so
    /// a purple or green brick is told from a blue one as a peg is (UX runtime round 1, M1), and never under
    /// <see cref="MinBrickMarkPixels"/> across at <paramref name="pixelsPerUnit"/> (it may then overhang the brick a little).
    /// </summary>
    public static (Vector2 Centre, float HalfSize) Place(in MoonfallPegView piece, float pixelsPerUnit = float.MaxValue)
    {
        var floor = pixelsPerUnit > 0 ? MinBrickMarkPixels / 2 / pixelsPerUnit : 0f;
        switch (piece.Shape)
        {
            case PegShape.Line:
                return (new Vector2((float)((piece.X + piece.X2) / 2), (float)((piece.Y + piece.Y2) / 2)), MathF.Max((float)(piece.Thickness / 2), floor));
            case PegShape.Arc:
                var t = piece.StartRadians + (piece.SweepRadians / 2);
                return (new Vector2((float)(piece.X + (piece.Radius * Math.Cos(t))), (float)(piece.Y + (piece.Radius * Math.Sin(t)))), MathF.Max((float)(piece.Thickness / 2), floor));
            default:
                return (new Vector2((float)piece.X, (float)piece.Y), (float)piece.Radius);
        }
    }

    /// <summary>Whether (u, v), in peg radii from its centre, is inside <paramref name="mark"/>.</summary>
    public static bool Inside(MoonfallPegMark mark, float u, float v)
    {
        switch (mark)
        {
            case MoonfallPegMark.Crescent:
                return MathF.Sqrt((u * u) + (v * v)) < 0.52f && MathF.Sqrt(((u - 0.22f) * (u - 0.22f)) + ((v + 0.12f) * (v + 0.12f))) > 0.42f;
            case MoonfallPegMark.Leaf:
                var a = (u + v) / 1.414f;
                var b = (v - u) / 1.414f;
                var leaf = MathF.Sqrt(((MathF.Abs(b) + 0.42f) * (MathF.Abs(b) + 0.42f)) + (a * a)) < 0.78f && MathF.Abs(a) < 0.62f;
                var midrib = MathF.Abs(b) < 0.05f && MathF.Abs(a) < 0.5f;
                return leaf && !midrib;
            case MoonfallPegMark.Star:
                return MathF.Sqrt(MathF.Abs(u)) + MathF.Sqrt(MathF.Abs(v)) < 0.86f;
            case MoonfallPegMark.StarRim:
                var st = MathF.Sqrt(MathF.Abs(u)) + MathF.Sqrt(MathF.Abs(v));
                return st is < 1.02f and >= 0.86f;
            default:
                return false;
        }
    }

    /// <summary>
    /// The sheet: <see cref="Cells"/> square cells of <paramref name="cell"/> pixels side by side (a cell is a peg's
    /// diameter), white with alpha the mark, its edge softened as the design's blur of 0.06 radii.
    /// </summary>
    public static MoonfallRgba Sheet(int cell = 64)
    {
        var w = cell * Cells;
        var alpha = new MoonfallPlane(w, cell);
        for (var k = 0; k < Cells; k++)
        {
            var mark = (MoonfallPegMark)(k + 1);
            const int Super = 4;
            for (var y = 0; y < cell; y++)
            {
                for (var x = 0; x < cell; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < Super; sy++)
                    {
                        for (var sx = 0; sx < Super; sx++)
                        {
                            var u = (((x + ((sx + 0.5f) / Super)) / cell) * 2) - 1;
                            var v = (((y + ((sy + 0.5f) / Super)) / cell) * 2) - 1;
                            hits += Inside(mark, u, v) ? 1 : 0;
                        }
                    }

                    alpha.Data[(y * w) + (k * cell) + x] = hits / (float)(Super * Super);
                }
            }
        }

        var soft = MoonfallFilters.Blur(alpha, 0.03f * cell);
        var bytes = new byte[w * cell * 4];
        for (var i = 0; i < w * cell; i++)
        {
            bytes[i * 4] = bytes[(i * 4) + 1] = bytes[(i * 4) + 2] = 255;
            bytes[(i * 4) + 3] = MoonfallImage.ToByte(soft.Data[i]);
        }

        return new MoonfallRgba(w, cell, bytes, new Vector4(0, 0, w, cell));
    }

    /// <summary>A mark's texture coordinates in the sheet.</summary>
    public static (Vector2 Uv0, Vector2 Uv1) Uv(MoonfallPegMark mark)
    {
        var k = (int)mark - 1;
        return (new Vector2(k / (float)Cells, 0), new Vector2((k + 1) / (float)Cells, 1));
    }
}
