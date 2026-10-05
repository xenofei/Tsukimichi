using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>A sprite's place in the atlas and its anchor.</summary>
/// <param name="X">Left edge in the 1x atlas, px (the 2x atlas has it at twice this).</param>
/// <param name="Y">Top edge in the 1x atlas, px.</param>
/// <param name="W">Width: 1x px, which is board units (1 unit is 1 px at 1x).</param>
/// <param name="H">Height, as <paramref name="W"/>.</param>
/// <param name="AnchorX">The point the drawing code places, in units from the sprite's left edge.</param>
/// <param name="AnchorY">The point the drawing code places, in units from the sprite's top edge.</param>
public readonly record struct MoonfallSpriteRect(float X, float Y, float W, float H, float AnchorX, float AnchorY);

/// <summary>Which bucket a campaign's board draws (spec-moonfall.md, "The two bucket proposals"; owner decision 7).</summary>
public enum MoonfallBucketStyle : byte
{
    /// <summary>B: the lantern boat on its strip of water.</summary>
    Boat,

    /// <summary>A: the brass crescent cradle on its rail.</summary>
    Cradle,
}

/// <summary>An atlas manifest read: the atlas, or null with every reason it was refused.</summary>
public sealed record MoonfallAtlasLoad(MoonfallAtlas? Atlas, IReadOnlyList<string> Errors)
{
    public bool Ok => Atlas is not null;
}

/// <summary>
/// Moonfall's art set (feature plan v9 G8): the manifest <c>assets/moonfall/atlas.json</c> beside <c>atlas.png</c> (1x,
/// 1 px per board unit), <c>atlas@2x.png</c> (exactly twice the 1x sheet, same layout) and <c>sky.png</c> (the board's
/// night sky when a level names no scene). The format, version 1:
/// <code>
/// {
///   "format": "moonfall-atlas", "version": 1,
///   "width": 1024, "height": 220,                               // the 1x sheet, px
///   "files": { "1x": "atlas.png", "2x": "atlas@2x.png", "sky": "sky.png" },
///   "sky": { "x": 70, "y": 36, "w": 660, "h": 564 },            // where sky.png lies on the board, units
///   "pegVariants": 12,                                          // peg.&lt;kind&gt;.0 .. 11; peg i draws variant i mod 12
///   "buckets": { "base": "boat", "expansion": "boat" },         // "boat" or "cradle", per campaign
///   "gauge": { "from": 200, "to": 340 },                        // the free-ball gauge's arc, degrees (0 along +x, towards +y)
///   "inks": { "glow.blue": "#A8C6FF", ... },                    // every key in RequiredInks
///   "points": { "boat.lantern": [50, -32] },                    // the lantern from the bucket's anchor, units
///   "slices": { "frame.bead.outer": 12, "frame.bead.inner": 8 },// nine-slice margins, units
///   "sprites": { "peg.blue.0.unlit": { "x": 4, "y": 6, "w": 24, "h": 24, "ax": 12, "ay": 12 }, ... }
/// }
/// </code>
/// Every name <see cref="MoonfallSprites.Required"/> lists must be present and inside the sheet; unknown sprites and keys
/// are ignored, a newer version is refused. A refused, missing or unreadable set leaves the board on the stage 1
/// primitives (<see cref="MoonfallArtSlots{T}"/>). Drawing conventions per sprite are on <see cref="MoonfallSprite"/>.
/// </summary>
public sealed partial class MoonfallAtlas
{
    public const string Format = "moonfall-atlas";

    /// <summary>The newest manifest version this build reads.</summary>
    public const int Version = 1;

    /// <summary>The largest 1x sheet side (its 2x sheet is twice this).</summary>
    public const int MaxSide = 2048;

    /// <summary>Most peg variants per kind and state.</summary>
    public const int MaxPegVariants = 64;

    /// <summary>
    /// The least a brick sprite's middle may be (units): its width less its height, which is its two caps. The middle
    /// repeats along the brick (<see cref="MoonfallArtMath.BrickColumns"/>), so one barely wider than its caps would
    /// repeat thousands of times.
    /// </summary>
    public const float MinBrickMiddle = 4;

    /// <summary>The inks the board draws with (see the format).</summary>
    public static readonly string[] RequiredInks =
    [
        "glow.blue", "glow.orange", "glow.green", "glow.purple",
        "flat.blue", "flat.orange", "flat.green", "flat.purple",
        "flat.blue.shade", "flat.orange.shade", "flat.green.shade", "flat.purple.shade",
        "ground", "enamel", "enamel.deep", "keyline", "lantern", "cup.lit", "moonlight", "dim", "band", "contact",
    ];

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private readonly MoonfallSpriteRect[] sprites;
    private readonly MoonfallSpriteRect[] pegs;
    private readonly MoonfallSpriteRect[] bricks;
    private readonly Vector4[] glows;
    private readonly Vector4[] flats;
    private readonly Vector4[] shades;
    private readonly Dictionary<string, MoonfallSpriteRect> named;

    private MoonfallAtlas(int width, int height, Dictionary<string, MoonfallSpriteRect> named, int pegVariants)
    {
        Width = width;
        Height = height;
        this.named = named;
        PegVariants = pegVariants;
        sprites = new MoonfallSpriteRect[MoonfallSprites.Names.Length];
        for (var i = 0; i < sprites.Length; i++)
        {
            sprites[i] = named[MoonfallSprites.Names[i]];
        }

        pegs = new MoonfallSpriteRect[MoonfallSprites.Kinds.Length * pegVariants * 2];
        bricks = new MoonfallSpriteRect[MoonfallSprites.Kinds.Length * 2];
        for (var kind = 0; kind < MoonfallSprites.Kinds.Length; kind++)
        {
            for (var lit = 0; lit < 2; lit++)
            {
                for (var v = 0; v < pegVariants; v++)
                {
                    pegs[PegIndex(kind, v, lit == 1)] = named[MoonfallSprites.Peg((PegColour)kind, v, lit == 1)];
                }

                bricks[(kind * 2) + lit] = named[MoonfallSprites.Brick((PegColour)kind, lit == 1)];
            }
        }

        glows = new Vector4[4];
        flats = new Vector4[4];
        shades = new Vector4[4];
    }

    /// <summary>The 1x sheet's width, px.</summary>
    public int Width { get; }

    /// <summary>The 1x sheet's height, px.</summary>
    public int Height { get; }

    /// <summary>The 1x sheet's file name, beside the manifest.</summary>
    public string OneXFile { get; private init; } = "atlas.png";

    /// <summary>The 2x sheet's file name.</summary>
    public string TwoXFile { get; private init; } = "atlas@2x.png";

    /// <summary>The night sky's file name.</summary>
    public string SkyFile { get; private init; } = "sky.png";

    /// <summary>Where the sky picture lies on the board, units (its pixel size, at 1 px per unit).</summary>
    public MoonfallSpriteRect Sky { get; private init; }

    /// <summary>Peg sprites per kind and state.</summary>
    public int PegVariants { get; }

    public MoonfallBucketStyle BaseBucket { get; private init; }

    public MoonfallBucketStyle ExpansionBucket { get; private init; }

    /// <summary>The free-ball gauge's arc, degrees.</summary>
    public float GaugeFrom { get; private init; }

    /// <inheritdoc cref="GaugeFrom"/>
    public float GaugeTo { get; private init; }

    /// <summary>The lantern's place from bucket B's anchor, units.</summary>
    public Vector2 BoatLantern { get; private init; }

    /// <summary>The outer bead's nine-slice margin, units.</summary>
    public float OuterSlice { get; private init; }

    /// <summary>The inner bead's nine-slice margin, units.</summary>
    public float InnerSlice { get; private init; }

    /// <summary>The board's ground under the pegs (the flat fill for Plain and while no sky is loaded).</summary>
    public Vector4 Ground { get; private init; }

    /// <summary>The rails' enamel at its lit (upper left) end.</summary>
    public Vector4 Enamel { get; private init; }

    /// <summary>The rails' enamel at its deep (lower right) end.</summary>
    public Vector4 EnamelDeep { get; private init; }

    /// <summary>Plain's 1 px frame keyline.</summary>
    public Vector4 Keyline { get; private init; }

    /// <summary>The lantern's light.</summary>
    public Vector4 Lantern { get; private init; }

    /// <summary>A lit Fever cup's glow and its value's ink.</summary>
    public Vector4 CupLit { get; private init; }

    /// <summary>Moonlight: the gauge's notch bloom.</summary>
    public Vector4 Moonlight { get; private init; }

    /// <summary>The night the sky dims towards in the approach to the last orange and under Full Moon.</summary>
    public Vector4 Dim { get; private init; }

    /// <summary>The Full Moon banner's band behind the pegs.</summary>
    public Vector4 Band { get; private init; }

    /// <summary>The contact shade the rails lay on the board.</summary>
    public Vector4 Contact { get; private init; }

    /// <summary>The bucket <paramref name="campaign"/>'s board draws.</summary>
    public MoonfallBucketStyle BucketFor(MoonfallCampaignKind campaign) => campaign == MoonfallCampaignKind.Expansion ? ExpansionBucket : BaseBucket;

    public ref readonly MoonfallSpriteRect this[MoonfallSprite sprite] => ref sprites[(int)sprite];

    /// <summary>Peg <paramref name="variant"/> (taken modulo <see cref="PegVariants"/>) of <paramref name="colour"/>.</summary>
    public ref readonly MoonfallSpriteRect Peg(PegColour colour, int variant, bool lit) =>
        ref pegs[PegIndex((int)colour & 3, (int)((uint)variant % (uint)PegVariants), lit)];

    public ref readonly MoonfallSpriteRect Brick(PegColour colour, bool lit) => ref bricks[(((int)colour & 3) * 2) + (lit ? 1 : 0)];

    /// <summary>A kind's lit halo ink.</summary>
    public Vector4 Glow(PegColour colour) => glows[(int)colour & 3];

    /// <summary>A kind's flat ink (Plain).</summary>
    public Vector4 Flat(PegColour colour) => flats[(int)colour & 3];

    /// <summary>A kind's flat ink for the unlit part (Plain).</summary>
    public Vector4 FlatShade(PegColour colour) => shades[(int)colour & 3];

    /// <summary>A sprite by its manifest name, for checks; false when the manifest has none.</summary>
    public bool TryGet(string name, out MoonfallSpriteRect rect) => named.TryGetValue(name, out rect);

    /// <summary>The sprite's texture coordinates, the same on both sheets.</summary>
    public (Vector2 Uv0, Vector2 Uv1) Uv(in MoonfallSpriteRect rect) =>
        (new Vector2(rect.X / Width, rect.Y / Height), new Vector2((rect.X + rect.W) / Width, (rect.Y + rect.H) / Height));

    /// <summary>A point of <paramref name="rect"/> (in units from its top-left) as texture coordinates.</summary>
    public Vector2 UvAt(in MoonfallSpriteRect rect, float x, float y) => new((rect.X + x) / Width, (rect.Y + y) / Height);

    /// <summary>The bytes a sheet holds on the GPU (RGBA, 4 per pixel): the 1x, or the 2x at twice each side.</summary>
    public long SheetBytes(bool twoX) => (long)Width * Height * 4 * (twoX ? 4 : 1);

    /// <summary>The sky's bytes on the GPU.</summary>
    public long SkyBytes => (long)Sky.W * (long)Sky.H * 4;

    private int PegIndex(int kind, int variant, bool lit) => (((kind * 2) + (lit ? 1 : 0)) * PegVariants) + variant;

    // \z, not $: $ also matches before a final newline, which would let "atlas.png\n" through as a file name.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_@.-]*\\.png\\z", RegexOptions.CultureInvariant)]
    private static partial Regex FilePattern();

    /// <summary>Reads a manifest; never throws on bad input.</summary>
    public static MoonfallAtlasLoad Parse(string? json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("the manifest is empty");
            return new MoonfallAtlasLoad(null, errors);
        }

        try
        {
            using var document = JsonDocument.Parse(json, Options);
            var atlas = Read(document.RootElement, errors);
            return new MoonfallAtlasLoad(errors.Count == 0 ? atlas : null, errors);
        }
        catch (JsonException ex)
        {
            errors.Add("not JSON: " + ex.Message);
            return new MoonfallAtlasLoad(null, errors);
        }
    }

    private static MoonfallAtlas? Read(JsonElement root, List<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object || Text(root, "format") != Format)
        {
            errors.Add($"format must be \"{Format}\"");
            return null;
        }

        if (Number(root, "version") is not { } version || version != Math.Floor(version) || version < 1)
        {
            errors.Add("version is missing or not a version");
            return null;
        }

        if (version > Version)
        {
            errors.Add($"version {version} was written for a newer Moonfall (this one reads {Version})");
            return null;
        }

        var width = Whole(root, "width");
        var height = Whole(root, "height");
        if (width is not (>= 1 and <= MaxSide) || height is not (>= 1 and <= MaxSide))
        {
            errors.Add($"width and height must be 1 to {MaxSide}");
            return null;
        }

        var variants = Whole(root, "pegVariants");
        if (variants is not (>= 1 and <= MaxPegVariants))
        {
            errors.Add($"pegVariants must be 1 to {MaxPegVariants}");
            return null;
        }

        var named = new Dictionary<string, MoonfallSpriteRect>(StringComparer.Ordinal);
        if (!root.TryGetProperty("sprites", out var list) || list.ValueKind != JsonValueKind.Object)
        {
            errors.Add("sprites must be an object");
            return null;
        }

        foreach (var node in list.EnumerateObject())
        {
            if (ReadRect(node.Value) is not { } rect || rect.W <= 0 || rect.H <= 0 || rect.X < 0 || rect.Y < 0 || rect.X + rect.W > width || rect.Y + rect.H > height)
            {
                errors.Add($"sprite {node.Name}: x, y, w, h must be numbers inside the {width} x {height} sheet, and ax, ay numbers");
                continue;
            }

            named[node.Name] = rect;
        }

        foreach (var name in MoonfallSprites.Required(variants.Value))
        {
            if (!named.TryGetValue(name, out var rect))
            {
                errors.Add($"sprite {name} is missing");
            }
            else if (MoonfallSprites.IsBrick(name) && !(rect.W >= rect.H + MinBrickMiddle))
            {
                // Its two caps are half its height each; the middle that repeats along the brick must have some length.
                errors.Add($"sprite {name}: a brick must be at least {MinBrickMiddle} units wider than it is high");
            }
        }

        var files = root.TryGetProperty("files", out var f) && f.ValueKind == JsonValueKind.Object ? f : default;
        string? FileName(string key)
        {
            var name = files.ValueKind == JsonValueKind.Object ? Text(files, key) : null;
            if (name is null || !FilePattern().IsMatch(name))
            {
                errors.Add($"files.{key} must be a .png file name beside the manifest");
                return null;
            }

            return name;
        }

        var oneX = FileName("1x");
        var twoX = FileName("2x");
        var skyFile = FileName("sky");
        var sky = root.TryGetProperty("sky", out var skyNode) ? ReadRect(skyNode) : null;
        if (sky is not { W: >= 1 and <= MaxSide, H: >= 1 and <= MaxSide } skyRect || skyRect.W != MathF.Floor(skyRect.W) || skyRect.H != MathF.Floor(skyRect.H))
        {
            errors.Add("sky must give the picture's place: x, y and whole w, h");
            sky = null;
        }

        var buckets = root.TryGetProperty("buckets", out var b) && b.ValueKind == JsonValueKind.Object ? b : default;
        MoonfallBucketStyle Bucket(string key)
        {
            switch (buckets.ValueKind == JsonValueKind.Object ? Text(buckets, key) : null)
            {
                case "boat":
                    return MoonfallBucketStyle.Boat;
                case "cradle":
                    return MoonfallBucketStyle.Cradle;
                default:
                    errors.Add($"buckets.{key} must be \"boat\" or \"cradle\"");
                    return MoonfallBucketStyle.Boat;
            }
        }

        var baseBucket = Bucket("base");
        var expansionBucket = Bucket("expansion");

        var gauge = root.TryGetProperty("gauge", out var g) && g.ValueKind == JsonValueKind.Object ? g : default;
        var from = gauge.ValueKind == JsonValueKind.Object ? Number(gauge, "from") : null;
        var to = gauge.ValueKind == JsonValueKind.Object ? Number(gauge, "to") : null;
        if (from is null || to is null || !(to > from) || to - from > 360)
        {
            errors.Add("gauge must give from and to, in degrees, to after from");
        }

        var inks = root.TryGetProperty("inks", out var i) && i.ValueKind == JsonValueKind.Object ? i : default;
        var colours = new Dictionary<string, Vector4>(StringComparer.Ordinal);
        foreach (var key in RequiredInks)
        {
            if (inks.ValueKind != JsonValueKind.Object || !TryHex(Text(inks, key), out var colour))
            {
                errors.Add($"inks.{key} must be a #RRGGBB colour");
                continue;
            }

            colours[key] = colour;
        }

        var points = root.TryGetProperty("points", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
        var lantern = Vector2.Zero;
        if (points.ValueKind != JsonValueKind.Object || !points.TryGetProperty("boat.lantern", out var lanternNode) || !TryPoint(lanternNode, out lantern))
        {
            errors.Add("points.boat.lantern must be [x, y]");
        }

        var slices = root.TryGetProperty("slices", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
        float Slice(MoonfallSprite sprite)
        {
            var name = MoonfallSprites.Name(sprite);
            var value = slices.ValueKind == JsonValueKind.Object ? Number(slices, name) : null;
            if (value is not > 0 || (named.TryGetValue(name, out var rect) && (value * 2 >= rect.W || value * 2 >= rect.H)))
            {
                errors.Add($"slices.{name} must be above 0 and under half the sprite");
                return 1;
            }

            return (float)value;
        }

        var outer = Slice(MoonfallSprite.FrameBeadOuter);
        var inner = Slice(MoonfallSprite.FrameBeadInner);
        if (errors.Count > 0 || oneX is null || twoX is null || skyFile is null || sky is null)
        {
            return null;
        }

        var atlas = new MoonfallAtlas(width.Value, height.Value, named, variants.Value)
        {
            OneXFile = oneX,
            TwoXFile = twoX,
            SkyFile = skyFile,
            Sky = sky.Value,
            BaseBucket = baseBucket,
            ExpansionBucket = expansionBucket,
            GaugeFrom = (float)from!.Value,
            GaugeTo = (float)to!.Value,
            BoatLantern = lantern,
            OuterSlice = outer,
            InnerSlice = inner,
            Ground = colours["ground"],
            Enamel = colours["enamel"],
            EnamelDeep = colours["enamel.deep"],
            Keyline = colours["keyline"],
            Lantern = colours["lantern"],
            CupLit = colours["cup.lit"],
            Moonlight = colours["moonlight"],
            Dim = colours["dim"],
            Band = colours["band"],
            Contact = colours["contact"],
        };
        for (var k = 0; k < 4; k++)
        {
            atlas.glows[k] = colours["glow." + MoonfallSprites.Kinds[k]];
            atlas.flats[k] = colours["flat." + MoonfallSprites.Kinds[k]];
            atlas.shades[k] = colours["flat." + MoonfallSprites.Kinds[k] + ".shade"];
        }

        return atlas;
    }

    private static MoonfallSpriteRect? ReadRect(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var x = Number(node, "x");
        var y = Number(node, "y");
        var w = Number(node, "w");
        var h = Number(node, "h");
        if (x is null || y is null || w is null || h is null)
        {
            return null;
        }

        var ax = node.TryGetProperty("ax", out _) ? Number(node, "ax") : 0;
        var ay = node.TryGetProperty("ay", out _) ? Number(node, "ay") : 0;
        if (ax is null || ay is null)
        {
            return null;
        }

        return new MoonfallSpriteRect((float)x, (float)y, (float)w, (float)h, (float)ax, (float)ay);
    }

    private static bool TryPoint(JsonElement node, out Vector2 point)
    {
        point = default;
        if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() != 2)
        {
            return false;
        }

        var x = node[0];
        var y = node[1];
        if (x.ValueKind != JsonValueKind.Number || y.ValueKind != JsonValueKind.Number || !x.TryGetDouble(out var px) || !y.TryGetDouble(out var py)
            || !double.IsFinite(px) || !double.IsFinite(py))
        {
            return false;
        }

        point = new Vector2((float)px, (float)py);
        return true;
    }

    /// <summary>A <c>#RRGGBB</c> colour, opaque.</summary>
    public static bool TryHex(string? text, out Vector4 colour)
    {
        colour = default;
        if (text is not { Length: 7 } || text[0] != '#'
            || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        colour = Ui.ColorMath.FromHex(rgb);
        return true;
    }

    private static string? Text(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static int? Whole(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}
