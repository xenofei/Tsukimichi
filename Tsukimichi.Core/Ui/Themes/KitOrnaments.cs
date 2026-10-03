using System.Globalization;
using System.Text.Json;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>A Decoration ornament a frame kit ships as sprites (ATLAS-CONTRACT §8; Kirikane, plan v7 T15).</summary>
public enum KitOrnament : byte
{
    /// <summary>The kit's crest sigil, from <see cref="KitOrnaments.SigilMinPx"/> up.</summary>
    Sigil,

    /// <summary>The crest redrawn for <see cref="KitOrnaments.SmallSigilMinPx"/> to 12 px only.</summary>
    SigilSmall,

    /// <summary>A single 2 px lozenge (times the UI scale), standing in for the sigil below 10 px.</summary>
    Lozenge,

    /// <summary>The top-left corner mark; the other corners mirror it.</summary>
    Corner,
}

/// <summary>
/// The size rule of a kit's sprite ornaments (Sumi to Kinpaku concept, Round 2 tidy-up 3): the crest from 13 px, the small
/// crest at 10–12 px, and below 10 px never a crest (at 8 px it turns to a ring with a blob in it) but the lozenge, 2 px
/// times the UI scale, as Medallion's divider. Pure.
/// </summary>
public static class KitOrnaments
{
    /// <summary>The smallest sigil, in device px, that draws the full crest.</summary>
    public const int SigilMinPx = 13;

    /// <summary>The smallest sigil that draws a crest at all (the small one).</summary>
    public const int SmallSigilMinPx = 10;

    /// <summary>The lozenge's side in logical px (times the UI scale).</summary>
    public const float LozengeLogical = 2f;

    /// <summary>The sprite and whole-pixel cell a sigil <paramref name="sizePx"/> device px across draws, at layout scale <paramref name="scale"/>.</summary>
    public static (KitOrnament Sprite, int Cell) Sigil(float sizePx, float scale)
    {
        var px = float.IsFinite(sizePx) ? (int)MathF.Round(sizePx) : 0;
        if (px >= SigilMinPx)
        {
            return (KitOrnament.Sigil, px);
        }

        if (px >= SmallSigilMinPx)
        {
            return (KitOrnament.SigilSmall, px);
        }

        var s = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        return (KitOrnament.Lozenge, Math.Max(2, (int)MathF.Round(LozengeLogical * s)));
    }

    /// <summary>
    /// The least side a corner mark draws at: its leaf bar (2.2 of the sprite's 32 units) is then at least one device
    /// pixel, never a hairline.
    /// </summary>
    public const int CornerMinPx = 15;

    /// <summary>Where the corner sprite's L begins (its outer edge) and how long its arms are, in the sprite's 32 units.</summary>
    private const float CornerInsetUnits = 3f;

    private const float CornerArmUnits = 26f;

    private const float CornerBoxUnits = 32f;

    /// <summary>
    /// The whole-pixel box of a corner mark whose L has arms of <paramref name="armPx"/> (at least
    /// <see cref="CornerMinPx"/> across) with its outer corner on the frame's corner (<paramref name="cornerX"/>,
    /// <paramref name="cornerY"/>); <paramref name="right"/> and <paramref name="bottom"/> say which corner, so the
    /// sprite (drawn for the top left) is mirrored into it.
    /// </summary>
    public static (float X, float Y, int Side) CornerBox(float cornerX, float cornerY, bool right, bool bottom, float armPx)
    {
        var side = Math.Max(CornerMinPx, float.IsFinite(armPx) ? (int)MathF.Round(armPx * CornerBoxUnits / CornerArmUnits) : 0);
        var inset = side * CornerInsetUnits / CornerBoxUnits;
        var x = right ? cornerX + inset - side : cornerX - inset;
        var y = bottom ? cornerY + inset - side : cornerY - inset;
        return (MathF.Round(x), MathF.Round(y), side);
    }

    /// <summary>The ornament's key in <c>ornaments.json</c>.</summary>
    public static string Key(KitOrnament sprite) => sprite switch
    {
        KitOrnament.Sigil => "sigil",
        KitOrnament.SigilSmall => "sigil-small",
        KitOrnament.Lozenge => "lozenge",
        _ => "corner",
    };
}

/// <summary>
/// A kit's ornament strip, parsed from its <c>ornaments.json</c> (ATLAS-CONTRACT §8): each <see cref="KitOrnament"/> at a
/// contiguous range of whole device pixel sizes, one 1x texture. Lookups allocate nothing.
/// </summary>
public sealed class KitOrnamentLayout
{
    private const int SpriteCount = (int)KitOrnament.Corner + 1;
    private readonly AtlasRect[][] cells;
    private readonly int[] minSizes;

    private KitOrnamentLayout(int width, int height, AtlasRect[][] cells, int[] minSizes)
    {
        Width = width;
        Height = height;
        this.cells = cells;
        this.minSizes = minSizes;
    }

    /// <summary>The texture's width and height in px.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>The bytes the texture takes once decoded (RGBA).</summary>
    public long Bytes => (long)Width * Height * 4;

    /// <summary>The smallest and largest cell of <paramref name="sprite"/>.</summary>
    public (int Min, int Max) Range(KitOrnament sprite) => (minSizes[(int)sprite], minSizes[(int)sprite] + cells[(int)sprite].Length - 1);

    /// <summary>
    /// The cell of <paramref name="sprite"/> at <paramref name="sizePx"/>, clamped to its range (a size above it draws the
    /// largest cell scaled; below it, the smallest). False for a sprite id out of range.
    /// </summary>
    public bool TryRect(KitOrnament sprite, int sizePx, out AtlasRect rect)
    {
        if ((uint)sprite >= SpriteCount)
        {
            rect = default;
            return false;
        }

        var row = cells[(int)sprite];
        rect = row[Math.Clamp(sizePx - minSizes[(int)sprite], 0, row.Length - 1)];
        return true;
    }

    /// <summary>Top-left and bottom-right UVs of <paramref name="rect"/>.</summary>
    public (float U0, float V0, float U1, float V1) Uv(AtlasRect rect) =>
        ((float)rect.X / Width, (float)rect.Y / Height, (float)(rect.X + rect.Width) / Width, (float)(rect.Y + rect.Height) / Height);

    /// <summary>
    /// Parses and checks an <c>ornaments.json</c>: every ornament, each at a contiguous range of sizes, cells square at their
    /// size, inside the image and apart (<see cref="ThemeAtlasRules.MinPad"/>). False with a reason for anything else.
    /// </summary>
    public static bool TryParse(string json, out KitOrnamentLayout? layout, out string? error)
    {
        layout = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!ThemeAtlasRules.TryReadSize(root, out var width, out var height, out error))
            {
                return false;
            }

            if (!root.TryGetProperty("sprites", out var sprites) || sprites.ValueKind != JsonValueKind.Object)
            {
                error = "no sprites";
                return false;
            }

            var cells = new AtlasRect[SpriteCount][];
            var mins = new int[SpriteCount];
            var all = new List<AtlasRect>();
            for (var s = 0; s < SpriteCount; s++)
            {
                var key = KitOrnaments.Key((KitOrnament)s);
                if (!sprites.TryGetProperty(key, out var bySize) || bySize.ValueKind != JsonValueKind.Object)
                {
                    error = $"no sprite {key}";
                    return false;
                }

                var sizes = bySize.EnumerateObject().Select(static p => int.Parse(p.Name, NumberStyles.None, CultureInfo.InvariantCulture)).Order().ToArray();
                if (sizes.Length == 0 || sizes[0] <= 0 || sizes.Select((v, i) => v - i).Distinct().Count() != 1)
                {
                    error = $"{key}: sizes must be contiguous whole pixels";
                    return false;
                }

                mins[s] = sizes[0];
                cells[s] = new AtlasRect[sizes.Length];
                for (var i = 0; i < sizes.Length; i++)
                {
                    if (!ThemeAtlasRules.TryReadCell(bySize, sizes[i], width, height, out var rect, out error))
                    {
                        error = $"{key} {sizes[i]}: {error}";
                        return false;
                    }

                    cells[s][i] = rect;
                    all.Add(rect);
                }
            }

            if (!ThemeAtlasRules.Apart(all, out error))
            {
                return false;
            }

            layout = new KitOrnamentLayout(width, height, cells, mins);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            error = ex.Message;
            return false;
        }
    }
}
