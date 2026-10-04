using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>A frame kit's urgency tier (theme-system §3.3): which frame a state's medal wears.</summary>
public enum FrameUrgency : byte
{
    /// <summary>Ready: the shared gilt in every kit, so the colour of "act now" never changes.</summary>
    ActNow,

    /// <summary>Every other state but Completed and Not checked: the kit's own metal.</summary>
    Resting,

    /// <summary>Completed: a duller step of the metal.</summary>
    Finished,

    /// <summary>Not checked: the faintest.</summary>
    Ghost,
}

/// <summary>What an atlas holds: a set's unframed faces, or a kit's frames and badges (ATLAS-CONTRACT §7).</summary>
public enum PartAtlasKind : byte
{
    Faces,
    Frames,
}

/// <summary>
/// The frames axis's parts (feature plan v7 T11; theme-system §3.2–3.3; ATLAS-CONTRACT §7), and how a medal is composed
/// from them: the face's under layer (the well and emblem), the kit's frame for the state's urgency tier, the face's over
/// layer (the overhangs: a ribbon, a check), then from 32 px the kit's badge. Sprites are numbered so lookups never touch
/// a string: a faces atlas's sprite is the state's index (under) or 8 + it (over); a frames atlas's is urgency × 2 +
/// finish (Full 0, Quiet 1) for the eight frames, then the seven badges.
/// </summary>
public static class FrameParts
{
    /// <summary>The faces atlas's sprite slots: each state's under and over layer.</summary>
    public const int FaceSprites = 2 * AppearanceStates.Count;

    /// <summary>The frames atlas's sprite slots: eight frames and seven badges.</summary>
    public const int FrameSprites = 8 + BadgeCount;

    private const int BadgeCount = 7;

    /// <summary>The badge sprites' box in the 128-unit medal box: the slot's quarter (centre 95, 95, keyline 24 and its shadow).</summary>
    public static readonly AtlasRect BadgeBox = new(64, 64, 64, 64);

    /// <summary>A whole medal's box.</summary>
    public static readonly AtlasRect FullBox = new(0, 0, 128, 128);

    private static readonly string[] BadgeKeys = ["open", "closed", "journal", "seat-tank", "seat-healer", "seat-dps", "seat-hand"];
    private static readonly string[] UrgencyKeys = ["act-now", "resting", "finished", "ghost"];

    /// <summary>The urgency tier of <paramref name="state"/>'s frame: Ready acts now, Completed is finished, Not checked a ghost.</summary>
    public static FrameUrgency UrgencyOf(QuestState state) => state switch
    {
        QuestState.Ready => FrameUrgency.ActNow,
        QuestState.Completed => FrameUrgency.Finished,
        QuestState.ReadyOnOtherJob or QuestState.Accepted or QuestState.Blocked or QuestState.DoneThisCycle or QuestState.Foreclosed => FrameUrgency.Resting,
        _ => FrameUrgency.Ghost,
    };

    /// <summary>The faces atlas sprite of <paramref name="state"/>'s under layer, or its over layer.</summary>
    public static int Face(QuestState state, bool over) => AppearanceStates.Index(state) + (over ? AppearanceStates.Count : 0);

    /// <summary>The frames atlas sprite of <paramref name="state"/>'s frame at Full, or at Quiet (the kit's hairline).</summary>
    public static int Frame(QuestState state, bool quiet) => ((int)UrgencyOf(state) * 2) + (quiet ? 1 : 0);

    /// <summary>
    /// The frames atlas sprite of <paramref name="state"/>'s hero badge: the open lock on Ready, the book on In journal,
    /// the closed lock on Blocked, <paramref name="seat"/>'s empty seat on Ready on another job (the job icon goes in it);
    /// −1 for the states without one.
    /// </summary>
    public static int Badge(QuestState state, JobSeat seat) => state switch
    {
        QuestState.Ready => 8,
        QuestState.Blocked => 9,
        QuestState.Accepted => 10,
        QuestState.ReadyOnOtherJob => 11 + Math.Clamp((int)seat, 0, 3),
        _ => -1,
    };

    /// <summary>The key of sprite <paramref name="sprite"/> in an atlas of <paramref name="kind"/> (the JSON's name for it).</summary>
    public static string Key(PartAtlasKind kind, int sprite)
    {
        if (kind == PartAtlasKind.Faces)
        {
            var state = AppearanceStates.All[sprite % AppearanceStates.Count];
            return sprite < AppearanceStates.Count ? AppearanceStates.Key(state) : AppearanceStates.Key(state) + "-over";
        }

        return sprite < 8 ? $"frame-{UrgencyKeys[sprite / 2]}-{(sprite % 2 == 0 ? "full" : "quiet")}" : "badge-" + BadgeKeys[sprite - 8];
    }

    /// <summary>The sprite slots of an atlas of <paramref name="kind"/>.</summary>
    public static int Count(PartAtlasKind kind) => kind == PartAtlasKind.Faces ? FaceSprites : FrameSprites;

    /// <summary>
    /// Whether an atlas of <paramref name="kind"/> must carry <paramref name="sprite"/>: every under layer (over layers
    /// exist only where a face overhangs its frame), every frame, and in a hero atlas every badge (a row strip has none).
    /// </summary>
    public static bool Required(PartAtlasKind kind, int sprite, bool row) =>
        kind == PartAtlasKind.Faces ? sprite < AppearanceStates.Count : sprite < 8 || !row;

    /// <summary>The light-palette outer keyline's alpha (spec-1.17 §B2): Abyss at .6.</summary>
    public const float LightKeylineAlpha = 0.6f;

    /// <summary>
    /// Whether a medal gets the 1 px Abyss outer keyline (spec-1.17 §B2, the supervisor's condition for Silver on Ishgard
    /// Snow): on a light palette, at Decoration Full, in every kit, so a medal's outline never depends on its metal's own
    /// darkest stop. At Quiet only a <paramref name="composed"/> medal gets it: Medallion's own Quiet hairline has a light
    /// variant, but a kit's Quiet frame sprite is one for every palette, and on Ishgard Snow's Quiet panes the outer edge
    /// of Brass's row hairline reads at about 1.7–2.3 : 1 and Astrolabe's at 2.9–3.4 : 1 (Silver's and Came's at 3 : 1 or
    /// better from 16 px), under the 3 : 1 an outline needs. Not at Plain (its flat rim is the outline), nor under high
    /// contrast.
    /// </summary>
    public static bool LightKeyline(bool lightPalette, bool highContrast, MedalFinish finish, bool composed) =>
        lightPalette && !highContrast && (finish == MedalFinish.Gilt || (finish == MedalFinish.LightRim && composed));

    /// <summary>
    /// The light-palette keyline's radius for a medal <paramref name="size"/> px across: centred half a pixel outside the
    /// medal's own keyline (r 63.2 of the 128-unit box), so the 1 px line lies just outside the rim.
    /// </summary>
    public static float LightKeylineRadius(float size) => (size * MedalArt.KeylineRadius / 128f) + 0.5f;

    /// <summary>
    /// The destination of a part covering <paramref name="box"/> (in the 128-unit box) of a medal drawn in the box
    /// <paramref name="min"/> .. <paramref name="min"/> + <paramref name="size"/>: its top-left corner and its size.
    /// </summary>
    public static (float X, float Y, float Width, float Height) Place(AtlasRect box, float minX, float minY, float size)
    {
        var k = size / 128f;
        return (minX + (box.X * k), minY + (box.Y * k), box.Width * k, box.Height * k);
    }
}

/// <summary>
/// A faces or frames atlas layout (ATLAS-CONTRACT §7), parsed from <c>faces.json</c>, <c>faces-row.json</c>,
/// <c>frames.json</c> or <c>frames-row.json</c>. A hero atlas lists <c>tiers</c> and each sprite's <c>box</c> (the part of
/// the 128-unit medal box it covers, so its cell is box × tier / 128 px; the @2x texture is the layout doubled). A row
/// atlas lists <c>sizes</c> (every whole device pixel from 12 to 31), and every cell is the whole box at that size.
/// Lookups allocate nothing.
/// </summary>
public sealed class PartAtlasLayout
{
    private readonly int[] cells;
    private readonly AtlasRect[] boxes;
    private readonly AtlasRect[] rects;
    private readonly bool[] present;

    private PartAtlasLayout(PartAtlasKind kind, bool row, int width, int height, int[] cells, AtlasRect[] boxes, AtlasRect[] rects, bool[] present)
    {
        Kind = kind;
        Row = row;
        Width = width;
        Height = height;
        this.cells = cells;
        this.boxes = boxes;
        this.rects = rects;
        this.present = present;
    }

    /// <summary>What the atlas holds.</summary>
    public PartAtlasKind Kind { get; }

    /// <summary>Whether it is a row strip (whole device pixels, 1x only) rather than hero tiers.</summary>
    public bool Row { get; }

    /// <summary>The 1x texture's size in px.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>The hero tiers or the row sizes, ascending.</summary>
    public IReadOnlyList<int> Cells => cells;

    /// <summary>The bytes the 1x texture takes once decoded (RGBA).</summary>
    public long Bytes1x => (long)Width * Height * 4;

    /// <summary>The bytes the 2x texture takes once decoded (hero atlases only).</summary>
    public long Bytes2x => Row ? 0 : Bytes1x * 4;

    /// <summary>Whether the atlas carries <paramref name="sprite"/> (an over layer may be absent).</summary>
    public bool Has(int sprite) => (uint)sprite < (uint)present.Length && present[sprite];

    /// <summary>The part of the 128-unit box <paramref name="sprite"/> covers (the whole box in a row atlas).</summary>
    public AtlasRect Box(int sprite) => (uint)sprite < (uint)boxes.Length ? boxes[sprite] : default;

    /// <summary>The cell of <paramref name="sprite"/> at <paramref name="cell"/> (a tier, or a row size); false when it has none.</summary>
    public bool TryRect(int sprite, int cell, out AtlasRect rect)
    {
        rect = default;
        if (!Has(sprite))
        {
            return false;
        }

        for (var c = 0; c < cells.Length; c++)
        {
            if (cells[c] == cell)
            {
                rect = rects[(sprite * cells.Length) + c];
                return true;
            }
        }

        return false;
    }

    /// <summary>Top-left and bottom-right UVs of <paramref name="rect"/> (the same in the 2x texture).</summary>
    public (float U0, float V0, float U1, float V1) Uv(AtlasRect rect) =>
        ((float)rect.X / Width, (float)rect.Y / Height, (float)(rect.X + rect.Width) / Width, (float)(rect.Y + rect.Height) / Height);

    /// <summary>
    /// The cell for a medal <paramref name="sizePx"/> device px across: in a hero atlas the tier and texture
    /// (<see cref="ThemeAtlasRules.PickTier"/>); in a row atlas the exact size, clamped to the strip's.
    /// </summary>
    public (int Cell, bool TwoX) Pick(float sizePx) =>
        Row ? (Math.Clamp((int)MathF.Round(sizePx), cells[0], cells[^1]), false) : ThemeAtlasRules.PickTier(cells, sizePx);

    /// <summary>
    /// Parses and checks an atlas of <paramref name="kind"/>: its size; ascending <c>tiers</c> (hero) or contiguous
    /// <c>sizes</c> (row); every required sprite (<see cref="FrameParts.Required"/>) at every cell; a hero sprite's box
    /// whole pixels at every tier and inside the 128-unit box (a badge's the badge box); each cell its box's size at that
    /// tier (or the row size), at least 1 px inside the image and <see cref="ThemeAtlasRules.MinPad"/> px from every other.
    /// Unknown sprites and keys are ignored. False with a reason for anything else.
    /// </summary>
    public static bool TryParse(string json, PartAtlasKind kind, out PartAtlasLayout? layout, out string? error)
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

            var row = root.TryGetProperty("sizes", out var cellsElement);
            if (!row && !root.TryGetProperty("tiers", out cellsElement))
            {
                error = "no tiers or sizes";
                return false;
            }

            if (cellsElement.ValueKind != JsonValueKind.Array)
            {
                error = "tiers or sizes is not a list";
                return false;
            }

            var cells = cellsElement.EnumerateArray().Select(static t => t.GetInt32()).ToArray();
            var ascending = cells.Length > 0 && cells[0] > 0 && cells.Zip(cells.Skip(1)).All(static p => p.Second > p.First);
            if (!ascending || (row && cells.Select((s, i) => s - i).Distinct().Count() != 1))
            {
                error = row ? "sizes must be contiguous whole pixels" : "tiers must be positive and ascending";
                return false;
            }

            if (!root.TryGetProperty("sprites", out var sprites) || sprites.ValueKind != JsonValueKind.Object)
            {
                error = "no sprites";
                return false;
            }

            var hasBoxes = root.TryGetProperty("boxes", out var boxesElement) && boxesElement.ValueKind == JsonValueKind.Object;
            var count = FrameParts.Count(kind);
            var boxes = new AtlasRect[count];
            var rects = new AtlasRect[count * cells.Length];
            var present = new bool[count];
            var all = new List<AtlasRect>();
            for (var s = 0; s < count; s++)
            {
                var key = FrameParts.Key(kind, s);
                if (!sprites.TryGetProperty(key, out var byCell))
                {
                    if (FrameParts.Required(kind, s, row))
                    {
                        error = $"no sprite {key}";
                        return false;
                    }

                    continue;
                }

                var box = FrameParts.FullBox;
                if (!row)
                {
                    if (!hasBoxes || !boxesElement.TryGetProperty(key, out var b) || b.ValueKind != JsonValueKind.Array || b.GetArrayLength() != 4)
                    {
                        error = $"{key}: no box";
                        return false;
                    }

                    box = new AtlasRect(b[0].GetInt32(), b[1].GetInt32(), b[2].GetInt32(), b[3].GetInt32());
                    if (box.X < 0 || box.Y < 0 || box.Width <= 0 || box.Height <= 0 || box.X + box.Width > 128 || box.Y + box.Height > 128)
                    {
                        error = $"{key}: box {box} is outside the 128-unit box";
                        return false;
                    }

                    if (key.StartsWith("badge-", StringComparison.Ordinal) && box != FrameParts.BadgeBox)
                    {
                        error = $"{key}: a badge's box must be {FrameParts.BadgeBox}";
                        return false;
                    }
                }

                boxes[s] = box;
                for (var c = 0; c < cells.Length; c++)
                {
                    if (!TryReadPart(byCell, cells[c], box, row, width, height, out var rect, out error))
                    {
                        error = $"{key} {cells[c]}: {error}";
                        return false;
                    }

                    rects[(s * cells.Length) + c] = rect;
                    all.Add(rect);
                }

                present[s] = true;
            }

            if (!ThemeAtlasRules.Apart(all, out error))
            {
                return false;
            }

            layout = new PartAtlasLayout(kind, row, width, height, cells, boxes, rects, present);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadPart(JsonElement byCell, int cell, AtlasRect box, bool row, int width, int height, out AtlasRect rect, out string? error)
    {
        rect = default;
        if (!byCell.TryGetProperty(cell.ToString(CultureInfo.InvariantCulture), out var r) || r.ValueKind != JsonValueKind.Array || r.GetArrayLength() != 4)
        {
            error = "missing";
            return false;
        }

        rect = new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());
        var (w, h) = row ? (cell, cell) : (box.Width * cell / 128, box.Height * cell / 128);
        if (!row && (box.Width * cell % 128 != 0 || box.Height * cell % 128 != 0))
        {
            error = $"box {box} is not whole pixels at {cell}";
            return false;
        }

        if (rect.Width != w || rect.Height != h)
        {
            error = $"cell is {rect.Width}×{rect.Height}, not {w}×{h}";
            return false;
        }

        if (rect.X < 1 || rect.Y < 1 || (long)rect.X + rect.Width > width - 1 || (long)rect.Y + rect.Height > height - 1)
        {
            error = "cell not inside the image";
            return false;
        }

        error = null;
        return true;
    }
}
