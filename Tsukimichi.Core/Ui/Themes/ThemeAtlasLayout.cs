using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// A theme set's hero atlas layout, parsed from its <c>medals.json</c> (or <c>plain.json</c>), whose schema is the
/// Medallion atlas's own (docs/design/v7/themes/ATLAS-CONTRACT.md §2): the 1x size, ascending tiers, and a square cell per
/// sprite and tier. The @2x texture is the same layout doubled. Lookups allocate nothing.
/// </summary>
public sealed class HeroAtlasLayout
{
    private readonly int[] tiers;
    private readonly AtlasRect[] rects;

    private HeroAtlasLayout(int width, int height, int[] tiers, AtlasRect[] rects)
    {
        Width = width;
        Height = height;
        this.tiers = tiers;
        this.rects = rects;
    }

    /// <summary>The 1x texture's width and height in px.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>The cell sizes at 1x, smallest first.</summary>
    public IReadOnlyList<int> Tiers => tiers;

    /// <summary>The bytes the 1x texture takes once decoded (RGBA).</summary>
    public long Bytes1x => (long)Width * Height * 4;

    /// <summary>The bytes the 2x texture takes once decoded.</summary>
    public long Bytes2x => Bytes1x * 4;

    /// <summary>The sprite's cell at the 1x tier <paramref name="tier"/>; empty for a tier the atlas does not have.</summary>
    public AtlasRect Rect(MedalSprite sprite, int tier)
    {
        var s = (int)sprite;
        if ((uint)s >= MedalLayout.SpriteCount)
        {
            return default;
        }

        for (var t = 0; t < tiers.Length; t++)
        {
            if (tiers[t] == tier)
            {
                return rects[(s * tiers.Length) + t];
            }
        }

        return default;
    }

    /// <summary>Top-left and bottom-right UVs of <paramref name="rect"/> (the same in both textures).</summary>
    public (float U0, float V0, float U1, float V1) Uv(AtlasRect rect) =>
        ((float)rect.X / Width, (float)rect.Y / Height, (float)(rect.X + rect.Width) / Width, (float)(rect.Y + rect.Height) / Height);

    /// <summary>The tier and texture for a medal <paramref name="sizePx"/> across (<see cref="ThemeAtlasRules.PickTier"/>).</summary>
    public (int Tier, bool TwoX) Pick(float sizePx) => ThemeAtlasRules.PickTier(tiers, sizePx);

    /// <summary>
    /// Parses and checks a <c>medals.json</c>: every one of the 11 sprites (<see cref="MedalLayout.Key"/>) at every tier, each
    /// cell square at its tier's size, at least 1 px inside the image and <see cref="ThemeAtlasRules.MinPad"/> px from every
    /// other. False with a reason for anything else; extra sprites and keys are ignored.
    /// </summary>
    public static bool TryParse(string json, out HeroAtlasLayout? layout, out string? error)
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

            if (!root.TryGetProperty("tiers", out var tiersElement) || tiersElement.ValueKind != JsonValueKind.Array)
            {
                error = "no tiers";
                return false;
            }

            var tiers = tiersElement.EnumerateArray().Select(static t => t.GetInt32()).ToArray();
            if (tiers.Length == 0 || tiers[0] <= 0 || tiers.Zip(tiers.Skip(1)).Any(static p => p.Second <= p.First))
            {
                error = "tiers must be positive and ascending";
                return false;
            }

            if (!root.TryGetProperty("sprites", out var sprites) || sprites.ValueKind != JsonValueKind.Object)
            {
                error = "no sprites";
                return false;
            }

            var rects = new AtlasRect[MedalLayout.SpriteCount * tiers.Length];
            for (var s = 0; s < MedalLayout.SpriteCount; s++)
            {
                var key = MedalLayout.Key((MedalSprite)s);
                if (!sprites.TryGetProperty(key, out var cells))
                {
                    error = $"no sprite {key}";
                    return false;
                }

                for (var t = 0; t < tiers.Length; t++)
                {
                    if (!ThemeAtlasRules.TryReadCell(cells, tiers[t], width, height, out var rect, out error))
                    {
                        error = $"{key} {tiers[t]}: {error}";
                        return false;
                    }

                    rects[(s * tiers.Length) + t] = rect;
                }
            }

            if (!ThemeAtlasRules.Apart(rects, out error))
            {
                return false;
            }

            layout = new HeroAtlasLayout(width, height, tiers, rects);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>A row strip's finish (ATLAS-CONTRACT §3).</summary>
public enum RowFinish : byte
{
    /// <summary>As designed (Decoration Full, and Quiet without a strip of its own).</summary>
    Full,

    /// <summary>A hairline-rim variant for Decoration Quiet.</summary>
    Quiet,

    /// <summary>The flat finish for Decoration Plain.</summary>
    Plain,
}

/// <summary>
/// A theme set's row strips, parsed from its <c>row.json</c> (ATLAS-CONTRACT §3): every state at every whole device pixel
/// from <see cref="MinSize"/> to <see cref="MaxSize"/>, per finish, each a real render at that size. One 1x texture (the
/// cells are device pixels already). Lookups allocate nothing.
/// </summary>
public sealed class RowStripLayout
{
    private const int FinishCount = 3;
    private readonly AtlasRect[]?[] finishes;

    private RowStripLayout(int width, int height, int minSize, int maxSize, AtlasRect[]?[] finishes)
    {
        Width = width;
        Height = height;
        MinSize = minSize;
        MaxSize = maxSize;
        this.finishes = finishes;
    }

    /// <summary>The texture's width and height in px.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>The smallest and largest cell sizes (12 and 31 in the contract).</summary>
    public int MinSize { get; }

    public int MaxSize { get; }

    /// <summary>The bytes the texture takes once decoded (RGBA).</summary>
    public long Bytes => (long)Width * Height * 4;

    /// <summary>Whether the strip has <paramref name="finish"/>.</summary>
    public bool Has(RowFinish finish) => (uint)finish < FinishCount && finishes[(int)finish] is not null;

    /// <summary>
    /// The cell of <paramref name="state"/> at <paramref name="sizePx"/> (clamped to <see cref="MinSize"/>..<see cref="MaxSize"/>)
    /// in <paramref name="finish"/>; false when the strip has no such finish.
    /// </summary>
    public bool TryRect(QuestState state, RowFinish finish, int sizePx, out AtlasRect rect)
    {
        if ((uint)finish >= FinishCount || finishes[(int)finish] is not { } cells)
        {
            rect = default;
            return false;
        }

        var size = Math.Clamp(sizePx, MinSize, MaxSize);
        rect = cells[(AppearanceStates.Index(state) * (MaxSize - MinSize + 1)) + (size - MinSize)];
        return true;
    }

    /// <summary>Top-left and bottom-right UVs of <paramref name="rect"/>.</summary>
    public (float U0, float V0, float U1, float V1) Uv(AtlasRect rect) =>
        ((float)rect.X / Width, (float)rect.Y / Height, (float)(rect.X + rect.Width) / Width, (float)(rect.Y + rect.Height) / Height);

    /// <summary>
    /// Parses and checks a <c>row.json</c> (states straight under <c>sprites</c> for the full finish alone, or keyed by
    /// finish first): contiguous whole-pixel sizes, the <c>full</c> finish and any of <c>quiet</c> and
    /// <c>plain</c>, each with all eight states (<see cref="AppearanceStates.Key"/>) at every size, cells square at their
    /// size, inside the image and apart. False with a reason for anything else.
    /// </summary>
    public static bool TryParse(string json, out RowStripLayout? layout, out string? error)
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

            if (!root.TryGetProperty("sizes", out var sizesElement) || sizesElement.ValueKind != JsonValueKind.Array)
            {
                error = "no sizes";
                return false;
            }

            var sizes = sizesElement.EnumerateArray().Select(static s => s.GetInt32()).ToArray();
            if (sizes.Length == 0 || sizes[0] <= 0 || sizes.Select((s, i) => s - i).Distinct().Count() != 1)
            {
                error = "sizes must be contiguous whole pixels";
                return false;
            }

            if (!root.TryGetProperty("sprites", out var sprites) || sprites.ValueKind != JsonValueKind.Object)
            {
                error = "no sprites";
                return false;
            }

            // The plain form (what build_themes.py writes) keys states straight under "sprites" and is the full finish; the
            // nested form keys finishes first ("full", "quiet", "plain").
            var nested = sprites.TryGetProperty(ThemeAtlasRules.FinishKey(RowFinish.Full), out _);
            var all = new List<AtlasRect>();
            var finishes = new AtlasRect[]?[FinishCount];
            foreach (var finish in Enum.GetValues<RowFinish>())
            {
                var name = ThemeAtlasRules.FinishKey(finish);
                var states = sprites;
                if (nested ? !sprites.TryGetProperty(name, out states) : finish != RowFinish.Full)
                {
                    if (finish == RowFinish.Full)
                    {
                        error = "no full finish";
                        return false;
                    }

                    continue;
                }

                var cells = new AtlasRect[AppearanceStates.Count * sizes.Length];
                foreach (var state in AppearanceStates.All)
                {
                    var key = AppearanceStates.Key(state);
                    if (!states.TryGetProperty(key, out var bySize))
                    {
                        error = $"{name}: no sprite {key}";
                        return false;
                    }

                    for (var i = 0; i < sizes.Length; i++)
                    {
                        if (!ThemeAtlasRules.TryReadCell(bySize, sizes[i], width, height, out var rect, out error))
                        {
                            error = $"{name} {key} {sizes[i]}: {error}";
                            return false;
                        }

                        cells[(AppearanceStates.Index(state) * sizes.Length) + i] = rect;
                    }
                }

                finishes[(int)finish] = cells;
                all.AddRange(cells);
            }

            if (!ThemeAtlasRules.Apart(all, out error))
            {
                return false;
            }

            layout = new RowStripLayout(width, height, sizes[0], sizes[^1], finishes);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>Where a theme set's medal of a given size comes from (<see cref="ThemeAtlasRules.Pick"/>).</summary>
public enum AtlasSource : byte
{
    /// <summary>Nothing of the set's can draw it (not loaded, or no such file): Medallion's procedural medal stands in.</summary>
    StandIn,

    /// <summary>The hero atlas (<c>medals</c>), at <see cref="AtlasPick.Cell"/>, 1x or 2x.</summary>
    Medals,

    /// <summary>The flat hero atlas (<c>plain</c>).</summary>
    Plain,

    /// <summary>A row strip cell of exactly <see cref="AtlasPick.Cell"/> px in <see cref="AtlasPick.Finish"/>.</summary>
    Row,
}

/// <summary>A choice of texture and cell for one medal.</summary>
public readonly record struct AtlasPick(AtlasSource Source, int Cell, bool TwoX, RowFinish Finish)
{
    /// <summary>Medallion's medal stands in.</summary>
    public static AtlasPick StandIn => default;
}

/// <summary>The atlas rules shared by Medallion's embedded atlas and the theme sets' (ATLAS-CONTRACT §2–4). Pure.</summary>
public static class ThemeAtlasRules
{
    /// <summary>The least gap between two cells at 1x, so bilinear sampling never bleeds a neighbour in.</summary>
    public const int MinPad = 2;

    /// <summary>The folder under the plugin's <c>assets/ui/themes/</c> that holds a set's files (its key).</summary>
    public static string Folder(GlyphSetId set) => GlyphSets.Get(set).Key;

    /// <summary>A file's path relative to the plugin directory: <c>assets/ui/themes/&lt;key&gt;/&lt;file&gt;</c>.</summary>
    public static string RelativePath(GlyphSetId set, string file) => Path.Combine("assets", "ui", "themes", Folder(set), file);

    /// <summary>A frame kit's file relative to the plugin directory: <c>assets/ui/kits/&lt;key&gt;/&lt;file&gt;</c> (ATLAS-CONTRACT §7).</summary>
    public static string RelativePath(FrameKitId kit, string file) => Path.Combine("assets", "ui", "kits", FrameKits.Get(kit).Key, file);

    /// <summary>
    /// The texture and tier to draw a medal <paramref name="sizePx"/> device px across from <paramref name="tiers"/>: the
    /// smallest 1x tier at or above it, else the smallest 2x tier (twice a 1x tier) at or above it, else the largest 2x
    /// tier. So a medal is never shrunk by more than 1.5× between tiers 48 … 128, and the 2x texture is only asked for
    /// above the largest 1x tier. (Medallion's <see cref="MedalLayout.Pick"/> is this over its own tiers.)
    /// </summary>
    public static (int Tier, bool TwoX) PickTier(IReadOnlyList<int> tiers, float sizePx)
    {
        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i] >= sizePx)
            {
                return (tiers[i], false);
            }
        }

        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i] * 2 >= sizePx)
            {
                return (tiers[i], true);
            }
        }

        return (tiers[^1], true);
    }

    /// <summary>
    /// Where a theme set's medal <paramref name="sizePx"/> px across comes from at <paramref name="finish"/>, given the
    /// layouts that have loaded (null for one that has not, or that the set does not ship), per ATLAS-CONTRACT §4:
    /// <list type="bullet">
    /// <item>Hero (from <see cref="MedalLayout.RowTierMaxPx"/>): the <c>medals</c> atlas at Full and Quiet; the <c>plain</c>
    /// atlas at Plain.</item>
    /// <item>Row: the strip's cell of exactly that size (the smallest cell below it), Quiet from the <c>full</c> cells when
    /// the strip has no Quiet finish, Plain only from a Plain finish.</item>
    /// <item>Anything missing: <see cref="AtlasSource.StandIn"/>.</item>
    /// </list>
    /// </summary>
    public static AtlasPick Pick(float sizePx, MedalFinish finish, HeroAtlasLayout? medals, HeroAtlasLayout? plain, RowStripLayout? row)
    {
        if (!(sizePx > 0f) || finish == MedalFinish.Classic)
        {
            return AtlasPick.StandIn;
        }

        var flat = finish == MedalFinish.Plain;
        if (sizePx >= MedalLayout.RowTierMaxPx)
        {
            var hero = flat ? plain : medals;
            if (hero is null)
            {
                return AtlasPick.StandIn;
            }

            var (tier, twoX) = hero.Pick(sizePx);
            return new AtlasPick(flat ? AtlasSource.Plain : AtlasSource.Medals, tier, twoX, RowFinish.Full);
        }

        if (row is null)
        {
            return AtlasPick.StandIn;
        }

        var rowFinish = flat ? RowFinish.Plain : finish == MedalFinish.LightRim && row.Has(RowFinish.Quiet) ? RowFinish.Quiet : RowFinish.Full;
        if (!row.Has(rowFinish))
        {
            return AtlasPick.StandIn;
        }

        var cell = Math.Clamp((int)MathF.Round(sizePx), row.MinSize, row.MaxSize);
        return new AtlasPick(AtlasSource.Row, cell, false, rowFinish);
    }

    internal static string FinishKey(RowFinish finish) => finish switch
    {
        RowFinish.Quiet => "quiet",
        RowFinish.Plain => "plain",
        _ => "full",
    };

    internal static bool TryReadSize(JsonElement root, out int width, out int height, out string? error)
    {
        width = height = 0;
        if (!root.TryGetProperty("size", out var size) || size.ValueKind != JsonValueKind.Array || size.GetArrayLength() != 2)
        {
            error = "no size";
            return false;
        }

        width = size[0].GetInt32();
        height = size[1].GetInt32();
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
        {
            error = "size out of range";
            return false;
        }

        error = null;
        return true;
    }

    internal static bool TryReadCell(JsonElement cells, int cell, int width, int height, out AtlasRect rect, out string? error)
    {
        rect = default;
        if (!cells.TryGetProperty(cell.ToString(CultureInfo.InvariantCulture), out var r) || r.ValueKind != JsonValueKind.Array || r.GetArrayLength() != 4)
        {
            error = "missing";
            return false;
        }

        rect = new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());
        if (rect.Width != cell || rect.Height != cell)
        {
            error = $"cell is {rect.Width}×{rect.Height}, not {cell}";
            return false;
        }

        // In long: a cell at x near int.MaxValue would wrap round to a negative right edge and pass in int.
        if (rect.X < 1 || rect.Y < 1 || (long)rect.X + rect.Width > width - 1 || (long)rect.Y + rect.Height > height - 1)
        {
            error = "cell not inside the image";
            return false;
        }

        error = null;
        return true;
    }

    internal static bool Apart(IReadOnlyList<AtlasRect> rects, out string? error)
    {
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                if (!(a.X + a.Width + MinPad <= b.X || b.X + b.Width + MinPad <= a.X || a.Y + a.Height + MinPad <= b.Y || b.Y + b.Height + MinPad <= a.Y))
                {
                    error = $"{a} and {b} are closer than {MinPad} px";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
