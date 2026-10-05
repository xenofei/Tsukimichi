using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>The game UI parts the in-play chrome draws (r2lib.ATLAS), each graded and packed in one sheet.</summary>
public enum MoonfallChromePart
{
    /// <summary>The journal frame's vine corner (upper left; mirrored for the right), its cut stem faded.</summary>
    CornerTop,

    /// <summary>The journal frame's banner-and-reed corner (lower left; mirrored for the right).</summary>
    CornerBottom,

    /// <summary>The frame's gilt triple rule, horizontal (its middle columns stretch).</summary>
    RuleTop,

    /// <summary>The frame's bottom rule.</summary>
    RuleBottom,

    /// <summary>The frame's vertical rule.</summary>
    RuleSide,

    /// <summary>The crest over a title rule.</summary>
    Crest,

    /// <summary>A short gilt rule (the shelf across a rail).</summary>
    ShortRule,

    /// <summary>The vertical scroll ornament (the lower rails).</summary>
    Scroll,

    /// <summary>The vertical triple rule (the ball tube's cage).</summary>
    TubeRule,

    /// <summary>Lord of Verminion's gilt ring, its backing plate cut away (portraits, plates). Hole radius 53 hr px at (95.5, 95.5).</summary>
    Ring,

    /// <summary>The great lattice ring with crest points (the multiplier dial). Hole radius 89.5 hr px at (156.5, 156.5).</summary>
    RingGreat,

    /// <summary>The Gold Saucer's gilt pill, its fill recoloured as enamel; body 224 × 58 hr px at (14, 18).</summary>
    Pill,

    /// <summary>Triple Triad's laurel and ribbon, the crowned plaque cut out (banners).</summary>
    Laurel,

    /// <summary>Triple Triad's card selection glow.</summary>
    CardSelect,

    /// <summary>The PvP rank emblem's gilt wing with turquoise inlay (the launcher's crest; Brass Wings).</summary>
    Wing,

    /// <summary>The PvP emblem's gilt spire (the ball tube's finials).</summary>
    Spire,

    /// <summary>The scholar gauge's triple gem frame (three turns left).</summary>
    Gems,

    /// <summary>The Triple Triad card back (a companion the story has not introduced).</summary>
    CardBack,
}

/// <summary>Where a part lies in the chrome sheet (pixels) and its size in hr pixels.</summary>
public readonly record struct MoonfallChromeRect(int X, int Y, int W, int H);

/// <summary>The chrome sheet built (<see cref="MoonfallChromeArt.Build"/>): the packed parts, the parts that are missing, and the journal ground's grain.</summary>
public sealed class MoonfallChromeSheet
{
    private readonly MoonfallChromeRect?[] rects;

    internal MoonfallChromeSheet(MoonfallRgba sheet, MoonfallChromeRect?[] rects, MoonfallPlane? grain, IReadOnlyList<string> missing, TimeSpan cost)
    {
        Sheet = sheet;
        this.rects = rects;
        Grain = grain;
        Missing = missing;
        Cost = cost;
    }

    /// <summary>Every part, graded, packed at hr resolution.</summary>
    public MoonfallRgba Sheet { get; }

    /// <summary>The journal ground's grain (high-passed, mirror-tiled; 0.5 is level), for the rails' enamel in each level's palette; null when its texture is missing.</summary>
    public MoonfallPlane? Grain { get; }

    /// <summary>The game textures that were missing or changed (their parts fall back).</summary>
    public IReadOnlyList<string> Missing { get; }

    public TimeSpan Cost { get; }

    /// <summary>Where <paramref name="part"/> is, or null when its texture was missing or changed.</summary>
    public MoonfallChromeRect? this[MoonfallChromePart part] => rects[(int)part];

    /// <summary>Whether every part the frame needs is here (else the board keeps its interim frame).</summary>
    public bool FrameReady => this[MoonfallChromePart.RuleTop] is not null && this[MoonfallChromePart.CornerTop] is not null
        && this[MoonfallChromePart.CornerBottom] is not null && this[MoonfallChromePart.RuleSide] is not null && this[MoonfallChromePart.RuleBottom] is not null;

    /// <summary>A part's texture coordinates, optionally a sub-rectangle of it in its own hr pixels.</summary>
    public (Vector2 Uv0, Vector2 Uv1) Uv(MoonfallChromePart part, float x0 = 0, float y0 = 0, float x1 = -1, float y1 = -1)
    {
        var r = this[part] ?? default;
        if (x1 < 0)
        {
            x1 = r.W;
        }

        if (y1 < 0)
        {
            y1 = r.H;
        }

        return (new Vector2((r.X + x0) / Sheet.Width, (r.Y + y0) / Sheet.Height), new Vector2((r.X + x1) / Sheet.Width, (r.Y + y1) / Sheet.Height));
    }
}

/// <summary>
/// The in-play chrome's game art (spec-rich2.md §1): the parts of nine UI textures read from the player's install at
/// runtime in their <c>_hr1</c> versions (nothing shipped), cut as the design cut them (r2lib.ATLAS, the component and
/// ring cut-outs), graded "gild" (lightness kept, hue pulled to the Medallion's gilt) and packed into one sheet when the
/// window opens, off the framework thread. A texture that is missing, or whose size a patch changed, drops only its parts:
/// the chrome draws the interim frame or plain shapes for them.
/// </summary>
public static class MoonfallChromeArt
{
    /// <summary>The UI textures read, and the size each must have (as of patch 2026.09.15) for its parts to be cut.</summary>
    public static readonly IReadOnlyList<(string Path, int Width, int Height)> Textures =
    [
        ("ui/uld/Journal_Frame_hr1.tex", 480, 384),
        ("ui/uld/Journal_Detail_hr1.tex", 1136, 344),
        ("ui/uld/LovmPalette_hr1.tex", 496, 912),
        ("ui/uld/TabButtonA_hr1.tex", 176, 104),
        ("ui/uld/TripleTriadCardSelect_hr1.tex", 232, 296),
        ("ui/uld/TripleTriadBattle_hr1.tex", 440, 984),
        ("ui/uld/TripleTriadResultCrown_hr1.tex", 720, 256),
        ("ui/uld/PVPRankEmblem3_hr1.tex", 344, 616),
        ("ui/uld/JobHudSCH0_hr1.tex", 304, 256),
    ];

    private sealed record Cut(MoonfallChromePart Part, string Texture, int X, int Y, int W, int H, float Gild, float Warm);

    private static readonly Cut[] Cuts =
    [
        new(MoonfallChromePart.CornerTop, "Journal_Frame", 13, 13, 180, 166, 0.55f, 0.1f),
        new(MoonfallChromePart.CornerBottom, "Journal_Frame", 13, 189, 176, 177, 0.55f, 0.1f),
        new(MoonfallChromePart.RuleTop, "Journal_Frame", 250, 13, 40, 34, 0.55f, 0.1f),
        new(MoonfallChromePart.RuleBottom, "Journal_Frame", 250, 330, 40, 36, 0.55f, 0.1f),
        new(MoonfallChromePart.RuleSide, "Journal_Frame", 13, 220, 34, 40, 0.55f, 0.1f),
        new(MoonfallChromePart.Crest, "Journal_Frame", 125, 102, 166, 49, 0.55f, 0f),
        new(MoonfallChromePart.ShortRule, "Journal_Frame", 125, 173, 102, 27, 0.55f, 0.1f),
        new(MoonfallChromePart.Scroll, "Journal_Frame", 413, 13, 59, 198, 0.55f, 0.1f),
        new(MoonfallChromePart.TubeRule, "Journal_Frame", 352, 13, 35, 86, 0.55f, 0.1f),
        new(MoonfallChromePart.Ring, "LovmPalette", 25, 28, 191, 191, 0.5f, 0f),
        new(MoonfallChromePart.RingGreat, "LovmPalette", 149, 83, 313, 313, 0.5f, 0f),
        new(MoonfallChromePart.Pill, "LovmPalette", 14, 621, 252, 94, 0.6f, 0f),
        new(MoonfallChromePart.Laurel, "TripleTriadResultCrown", 40, 58, 640, 160, 0.45f, 0.2f),
        new(MoonfallChromePart.CardSelect, "TripleTriadCardSelect", 6, 12, 220, 272, 0.3f, 0f),
        new(MoonfallChromePart.Wing, "PVPRankEmblem3", 6, 0, 338, 228, 0.5f, 0.05f),
        new(MoonfallChromePart.Spire, "PVPRankEmblem3", 2, 233, 76, 342, 0.5f, 0.05f),
        new(MoonfallChromePart.Gems, "JobHudSCH0", 7, 7, 290, 139, 0.5f, 0f),
        new(MoonfallChromePart.CardBack, "TripleTriadBattle", 27, 729, 202, 254, 0.5f, 0f),
    ];

    /// <summary>The rings' geometry in their crops: centre x, y, hole radius, gilt's outer radius (hr px; r2lib.RING_GEOM).</summary>
    public static readonly Vector4 RingGeometry = new(95.5f, 95.5f, 53f, 76f);

    /// <inheritdoc cref="RingGeometry"/>
    public static readonly Vector4 RingGreatGeometry = new(156.5f, 156.5f, 89.5f, 123f);

    /// <summary>The pill's body inside its crop, hr px (x0, y0, x1, y1).</summary>
    public static readonly Vector4 PillBody = new(14, 18, 238, 76);

    /// <summary>The width of the sheet.</summary>
    public const int SheetWidth = 1024;

    /// <summary>The game path of a UI texture by its short name.</summary>
    public static string PathOf(string name) => $"ui/uld/{name}_hr1.tex";

    /// <summary>
    /// Cuts, grades and packs every part from <paramref name="textures"/> (game path to its decoded picture, with alpha);
    /// a texture missing from it, or of another size than <see cref="Textures"/> says, drops its parts.
    /// </summary>
    public static MoonfallChromeSheet Build(IReadOnlyDictionary<string, MoonfallImage> textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var missing = new List<string>();
        var usable = new Dictionary<string, MoonfallImage>(StringComparer.Ordinal);
        foreach (var (path, w, h) in Textures)
        {
            if (!textures.TryGetValue(path, out var image))
            {
                missing.Add(path);
            }
            else if (image.Width != w || image.Height != h || image.A is null)
            {
                missing.Add($"{path} ({image.Width} x {image.Height}, not {w} x {h})");
            }
            else
            {
                usable[path] = image;
            }
        }

        var parts = new List<(MoonfallChromePart Part, MoonfallImage Image)>();
        foreach (var cut in Cuts)
        {
            if (!usable.TryGetValue(PathOf(cut.Texture), out var source))
            {
                continue;
            }

            var crop = source.Crop(cut.X, cut.Y, cut.W, cut.H);
            crop = Prepare(cut.Part, crop);
            parts.Add((cut.Part, MoonfallGrade.Gild(crop, cut.Gild, cut.Warm)));
            if (cut.Part == MoonfallChromePart.Pill)
            {
                parts[^1] = (cut.Part, PillFill(parts[^1].Image, crop));
            }
        }

        var grain = usable.TryGetValue(PathOf("Journal_Detail"), out var detail) ? Grain(detail.Crop(40, 180, 640, 128)) : null;
        var (sheet, rects) = Pack(parts);
        timer.Stop();
        return new MoonfallChromeSheet(sheet, rects, grain, missing, timer.Elapsed);
    }

    /// <summary>The design's cut-outs before grading: the corners' own piece (and their faded stems), the rings without their plates, the banner laurel without its plaque.</summary>
    private static MoonfallImage Prepare(MoonfallChromePart part, MoonfallImage crop)
    {
        switch (part)
        {
            case MoonfallChromePart.CornerTop:
                crop = Component(crop, 12, 12);
                FadeStem(crop, fromTop: false);
                return crop;
            case MoonfallChromePart.CornerBottom:
                crop = Component(crop, 12, 160);
                FadeStem(crop, fromTop: true);
                return crop;
            case MoonfallChromePart.Ring:
            case MoonfallChromePart.RingGreat:
                var g = part == MoonfallChromePart.Ring ? RingGeometry : RingGreatGeometry;
                var a = crop.A!;
                for (var y = 0; y < crop.Height; y++)
                {
                    for (var x = 0; x < crop.Width; x++)
                    {
                        var d = MathF.Sqrt(((x + 0.5f - g.X) * (x + 0.5f - g.X)) + ((y + 0.5f - g.Y) * (y + 0.5f - g.Y)));
                        var keep = Math.Clamp((g.W + 2.5f - d) / 2f, 0f, 1f);
                        if (part == MoonfallChromePart.RingGreat)
                        {
                            keep = MathF.Max(keep, Math.Clamp((22 - MathF.Abs(x + 0.5f - g.X)) / 3f, 0f, 1f) * Math.Clamp((153 - d) / 2f, 0f, 1f));
                        }

                        a[x, y] *= keep;
                    }
                }

                return crop;
            case MoonfallChromePart.Laurel:
                var la = crop.A!;
                for (var y = 0; y < crop.Height; y++)
                {
                    for (var x = 0; x < crop.Width; x++)
                    {
                        var plaque = Math.Clamp(MathF.Min(x - 262f, 380f - x) / 3f, 0f, 1f) * Math.Clamp((118f - y) / 3f, 0f, 1f);
                        la[x, y] *= 1 - plaque;
                    }
                }

                return crop;
            default:
                return crop;
        }
    }

    /// <summary>r2lib._component: the connected piece of a crop holding (sx, sy), with its soft shadow grown 7 px.</summary>
    private static MoonfallImage Component(MoonfallImage crop, int sx, int sy, float threshold = 0.16f, int grow = 7)
    {
        int w = crop.Width, h = crop.Height;
        var a = crop.A!;
        var on = new bool[w * h];
        for (var i = 0; i < on.Length; i++)
        {
            on[i] = a.Data[i] > threshold;
        }

        var seed = (sy * w) + sx;
        if (!on[seed])
        {
            var best = float.MaxValue;
            for (var i = 0; i < on.Length; i++)
            {
                if (on[i])
                {
                    var d = (((i % w) - sx) * ((i % w) - sx)) + (((i / w) - sy) * ((i / w) - sy));
                    if (d < best)
                    {
                        best = d;
                        seed = i;
                    }
                }
            }
        }

        // 8-connected flood (the design grows by a 3 × 3 max filter within the mask).
        var keep = new MoonfallPlane(w, h);
        var stack = new Stack<int>();
        stack.Push(seed);
        keep.Data[seed] = 1;
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            int x = i % w, y = i / w;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    {
                        continue;
                    }

                    var j = (ny * w) + nx;
                    if (on[j] && keep.Data[j] == 0)
                    {
                        keep.Data[j] = 1;
                        stack.Push(j);
                    }
                }
            }
        }

        // Grown by `grow` px (a max filter) and softened: the piece's own soft shadow comes with it.
        var grown = new MoonfallPlane(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var any = 0f;
                for (var dy = -grow; dy <= grow && any == 0; dy++)
                {
                    for (var dx = -grow; dx <= grow; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && keep.Data[(ny * w) + nx] > 0)
                        {
                            any = 1;
                            break;
                        }
                    }
                }

                grown.Data[(y * w) + x] = any;
            }
        }

        var soft = MoonfallFilters.Blur(grown, 2f);
        var out_ = crop.Copy();
        for (var i = 0; i < out_.A!.Data.Length; i++)
        {
            out_.A.Data[i] *= MathF.Max(keep.Data[i], soft.Data[i]);
        }

        return out_;
    }

    /// <summary>The corner art's stem beyond the band fades over its last 28 hr px (round 3, UX m8).</summary>
    private static void FadeStem(MoonfallImage crop, bool fromTop)
    {
        var a = crop.A!;
        for (var y = 0; y < crop.Height; y++)
        {
            var ramp = Math.Clamp((fromTop ? y : crop.Height - 1 - y) / 28f, 0f, 1f);
            for (var x = 35; x < crop.Width; x++)
            {
                a[x, y] *= ramp;
            }
        }
    }

    /// <summary>r2kit._pill("normal"): the pill's leather fill recoloured as enamel (its grain kept faintly, a glaze along the top).</summary>
    private static MoonfallImage PillFill(MoonfallImage gilded, MoonfallImage raw)
    {
        ReadOnlySpan<(float, Vector3)> stops = [(0f, MoonfallColor.Hex("#2C428C")), (0.5f, MoonfallColor.Hex("#1A2A66")), (1f, MoonfallColor.Hex("#0E1640"))];
        var glaze = MoonfallColor.Hex("#BFD0FF");
        var out_ = gilded.Copy();
        for (var y = 0; y < raw.Height; y++)
        {
            for (var x = 0; x < raw.Width; x++)
            {
                // The body's interior: inset 4 hr px from the rim (x 18..234, y 22..72, radius 25).
                var sd = SdRoundRect(x + 0.5f, y + 0.5f, 18f, 22f, 234f, 72f, 25f);
                var m = Math.Clamp(0.5f - sd, 0f, 1f);
                if (m <= 0)
                {
                    continue;
                }

                var t = Math.Clamp((y - 22) / 50f, 0f, 1f);
                var fill = MoonfallColor.Ramp(t, stops);
                var luma = MoonfallColor.Luma(raw.R[x, y], raw.G[x, y], raw.B[x, y]);
                fill *= 0.85f + (0.6f * (luma - 0.22f));
                var gl = MathF.Exp(-MathF.Pow((y - 26) / 4.5f, 2)) * 0.22f;
                fill = new Vector3(MoonfallColor.Screen(fill.X, glaze.X * gl), MoonfallColor.Screen(fill.Y, glaze.Y * gl), MoonfallColor.Screen(fill.Z, glaze.Z * gl));
                out_.R[x, y] = (out_.R[x, y] * (1 - m)) + (fill.X * m);
                out_.G[x, y] = (out_.G[x, y] * (1 - m)) + (fill.Y * m);
                out_.B[x, y] = (out_.B[x, y] * (1 - m)) + (fill.Z * m);
            }
        }

        return out_;
    }

    /// <summary>A rounded rectangle's signed distance (the design's sd_rrect).</summary>
    public static float SdRoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
    {
        var cx = (x0 + x1) / 2;
        var cy = (y0 + y1) / 2;
        var hx = ((x1 - x0) / 2) - r;
        var hy = ((y1 - y0) / 2) - r;
        var qx = MathF.Abs(x - cx) - hx;
        var qy = MathF.Abs(y - cy) - hy;
        var outside = MathF.Sqrt((MathF.Max(qx, 0) * MathF.Max(qx, 0)) + (MathF.Max(qy, 0) * MathF.Max(qy, 0)));
        return outside + MathF.Min(MathF.Max(qx, qy), 0) - r;
    }

    /// <summary>r2lib._ground: the journal ground's grain only (high-passed so its gradient and bevel go), mirror-tiled so no seam shows.</summary>
    private static MoonfallPlane Grain(MoonfallImage panel)
    {
        var y = new MoonfallPlane(panel.Width, panel.Height);
        for (var i = 0; i < y.Data.Length; i++)
        {
            y.Data[i] = MoonfallColor.Luma(panel.R.Data[i], panel.G.Data[i], panel.B.Data[i]);
        }

        var low = MoonfallFilters.Blur(y, 12);
        int w = panel.Width, h = panel.Height;
        var tile = new MoonfallPlane(w * 2, h * 2);
        for (var yy = 0; yy < h * 2; yy++)
        {
            var sy = yy < h ? yy : (2 * h) - 1 - yy;
            for (var xx = 0; xx < w * 2; xx++)
            {
                var sx = xx < w ? xx : (2 * w) - 1 - xx;
                var i = (sy * w) + sx;
                tile[xx, yy] = y.Data[i] - low.Data[i] + 0.5f;
            }
        }

        return tile;
    }

    /// <summary>
    /// The rails' enamel tile for a level's palette (r2lib.enamel_ground, chrome2.rails): the grain through a ramp from
    /// the abyss to the level's sky and on toward its first jewel, at one pixel a board unit (the grain at half its hr
    /// size), brightened by 1.12 so the plugin's vertical light (1.12 at the top to 0.82 at the foot) is a vertex tint.
    /// </summary>
    public static MoonfallRgba EnamelTile(MoonfallPlane grain, MoonfallChromePalette palette)
    {
        ArgumentNullException.ThrowIfNull(grain);
        ArgumentNullException.ThrowIfNull(palette);
        var w = grain.Width / 2;
        var h = grain.Height / 2;
        var small = MoonfallFilters.Resample(grain, 0, 0, grain.Width, grain.Height, w, h);
        var mid = Vector3.Lerp(palette.Sky, palette.Jewel1, 0.45f);
        ReadOnlySpan<(float, Vector3)> stops = [(0f, MoonfallColor.Hex("#070A1E")), (0.45f, palette.Sky), (1f, mid)];
        var bytes = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++)
        {
            var tt = Math.Clamp(0.5f + ((small.Data[i] - 0.5f) * 7f), 0f, 1f);
            var c = MoonfallColor.Ramp((tt * 0.55f) + 0.30f, stops) * 1.12f;
            bytes[i * 4] = MoonfallImage.ToByte(c.X);
            bytes[(i * 4) + 1] = MoonfallImage.ToByte(c.Y);
            bytes[(i * 4) + 2] = MoonfallImage.ToByte(c.Z);
            bytes[(i * 4) + 3] = 255;
        }

        return new MoonfallRgba(w, h, bytes, new Vector4(0, 0, w, h));
    }

    private static (MoonfallRgba Sheet, MoonfallChromeRect?[] Rects) Pack(List<(MoonfallChromePart Part, MoonfallImage Image)> parts)
    {
        const int Pad = 2;
        var rects = new MoonfallChromeRect?[Enum.GetValues<MoonfallChromePart>().Length];
        var order = parts.OrderByDescending(static p => p.Image.Height).ThenByDescending(static p => p.Image.Width).ToList();
        int x = 0, y = 0, shelf = 0;
        var placed = new List<(MoonfallChromePart, MoonfallImage, int, int)>();
        foreach (var (part, image) in order)
        {
            var w = image.Width + (2 * Pad);
            var h = image.Height + (2 * Pad);
            if (x + w > SheetWidth)
            {
                x = 0;
                y += shelf;
                shelf = 0;
            }

            placed.Add((part, image, x + Pad, y + Pad));
            rects[(int)part] = new MoonfallChromeRect(x + Pad, y + Pad, image.Width, image.Height);
            x += w;
            shelf = Math.Max(shelf, h);
        }

        var height = Math.Max(4, (y + shelf + 3) / 4 * 4);
        var bytes = new byte[SheetWidth * height * 4];
        foreach (var (_, image, px, py) in placed)
        {
            // Each part with its edge pixels replicated into the padding, so a bilinear sample at its rim stays its own.
            for (var yy = -Pad; yy < image.Height + Pad; yy++)
            {
                var sy = Math.Clamp(yy, 0, image.Height - 1);
                for (var xx = -Pad; xx < image.Width + Pad; xx++)
                {
                    var sx = Math.Clamp(xx, 0, image.Width - 1);
                    var o = (((py + yy) * SheetWidth) + px + xx) * 4;
                    var i = (sy * image.Width) + sx;
                    var edge = xx < 0 || yy < 0 || xx >= image.Width || yy >= image.Height;
                    bytes[o] = MoonfallImage.ToByte(image.R.Data[i]);
                    bytes[o + 1] = MoonfallImage.ToByte(image.G.Data[i]);
                    bytes[o + 2] = MoonfallImage.ToByte(image.B.Data[i]);
                    bytes[o + 3] = edge ? (byte)0 : MoonfallImage.ToByte(image.A!.Data[i]);
                }
            }
        }

        return (new MoonfallRgba(SheetWidth, height, bytes, new Vector4(0, 0, SheetWidth, height)), rects);
    }
}
