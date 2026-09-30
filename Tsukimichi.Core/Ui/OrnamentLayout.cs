namespace Tsukimichi.Core.Ui;

/// <summary>
/// The seventeen original gap glyphs (Moon Road proposal §5, §6.1): monoline Silver strokes with one gold accent,
/// drawn from the ornament atlas where the game has no legible icon for a journal node or a plugin concept.
/// <see cref="None"/> means "no glyph"; a node with an official icon carries it.
/// </summary>
public enum OrnamentGlyph : byte
{
    None = 0,
    AllQuests,
    Removed,
    Chronicles,
    ChroniclesOfLight,
    Hildibrand,
    SideStory,
    Relic,
    Endeavors,
    Other,
    Special,
    Festival,
    DeepDungeon,
    RegionCoerthas,
    RegionMordhona,
    Moonlit,
    Flight,
    PlanFallback,
}

/// <summary>The atlas pieces that are not gap glyphs.</summary>
public enum OrnamentSprite : byte
{
    /// <summary>The rail crest: the moon over night water in a double brass ring (40 px at 1x).</summary>
    Crest,

    /// <summary>The top-left corner mark (12 px); the other three corners flip its UVs.</summary>
    CornerMark,

    /// <summary>The four-point sigil star (12 px).</summary>
    SigilStar,

    /// <summary>The moon-road divider's middle: waxing crescent, full moon, waning crescent (40 × 12); the arms are primitives.</summary>
    DividerPhases,
}

/// <summary>A sprite's rectangle in the 1x atlas, in pixels.</summary>
public readonly record struct AtlasRect(int X, int Y, int Width, int Height)
{
    /// <summary>Top-left UV in an atlas of the given size (the 2x atlas has the same UVs).</summary>
    public (float U, float V) Uv0 => ((float)X / OrnamentLayout.Width, (float)Y / OrnamentLayout.Height);

    /// <summary>Bottom-right UV.</summary>
    public (float U, float V) Uv1 => ((float)(X + Width) / OrnamentLayout.Width, (float)(Y + Height) / OrnamentLayout.Height);
}

/// <summary>
/// Layout of the ornament atlas, <c>Tsukimichi/assets/ui/ornaments.png</c> (256 × 128) and <c>ornaments@2x.png</c>
/// (512 × 256, the same layout doubled). Written by <c>docs/design/moon-road/gen_atlas.py</c>, which also writes
/// <c>ornaments.json</c>; a test holds this table to that file and to the PNG sizes. Sprites sit 2 px apart (4 px at 2x)
/// so bilinear sampling never bleeds a neighbour in.
/// </summary>
public static class OrnamentLayout
{
    public const int Width = 256;
    public const int Height = 128;

    /// <summary>Glyph cells: 24 px, eight to a row at a 28 px pitch, from (2, 46).</summary>
    public const int GlyphSize = 24;
    private const int GlyphPitch = 28;
    private const int GlyphLeft = 2;
    private const int GlyphTop = 46;
    private const int GlyphsPerRow = 8;

    /// <summary>Manifest resource names (the csproj's <c>LogicalName</c>s).</summary>
    public const string ResourceName1x = "Tsukimichi.assets.ui.ornaments.png";
    public const string ResourceName2x = "Tsukimichi.assets.ui.ornaments@2x.png";

    /// <summary>The number of real glyphs (every <see cref="OrnamentGlyph"/> but <see cref="OrnamentGlyph.None"/>).</summary>
    public const int GlyphCount = (int)OrnamentGlyph.PlanFallback;

    /// <summary>The glyph's cell; <see cref="OrnamentGlyph.None"/> (or an out-of-range value) has an empty rectangle.</summary>
    public static AtlasRect Glyph(OrnamentGlyph glyph)
    {
        var i = (int)glyph - 1;
        if (i < 0 || i >= GlyphCount)
        {
            return default;
        }

        return new AtlasRect(GlyphLeft + (i % GlyphsPerRow) * GlyphPitch, GlyphTop + (i / GlyphsPerRow) * GlyphPitch, GlyphSize, GlyphSize);
    }

    /// <summary>A non-glyph sprite's rectangle.</summary>
    public static AtlasRect Sprite(OrnamentSprite sprite) => sprite switch
    {
        OrnamentSprite.Crest => new AtlasRect(2, 2, 40, 40),
        OrnamentSprite.CornerMark => new AtlasRect(46, 2, 12, 12),
        OrnamentSprite.SigilStar => new AtlasRect(62, 2, 12, 12),
        OrnamentSprite.DividerPhases => new AtlasRect(78, 2, 40, 12),
        _ => default,
    };

    /// <summary>The glyph's file stem in <c>docs/design/moon-road/ornaments/</c> and its key in <c>ornaments.json</c> ("glyph-all-quests").</summary>
    public static string GlyphKey(OrnamentGlyph glyph) => glyph switch
    {
        OrnamentGlyph.AllQuests => "glyph-all-quests",
        OrnamentGlyph.Removed => "glyph-removed",
        OrnamentGlyph.Chronicles => "glyph-chronicles",
        OrnamentGlyph.ChroniclesOfLight => "glyph-chronicles-of-light",
        OrnamentGlyph.Hildibrand => "glyph-hildibrand",
        OrnamentGlyph.SideStory => "glyph-side-story",
        OrnamentGlyph.Relic => "glyph-relic",
        OrnamentGlyph.Endeavors => "glyph-endeavors",
        OrnamentGlyph.Other => "glyph-other",
        OrnamentGlyph.Special => "glyph-special",
        OrnamentGlyph.Festival => "glyph-festival",
        OrnamentGlyph.DeepDungeon => "glyph-deep-dungeon",
        OrnamentGlyph.RegionCoerthas => "glyph-region-coerthas",
        OrnamentGlyph.RegionMordhona => "glyph-region-mordhona",
        OrnamentGlyph.Moonlit => "glyph-moonlit",
        OrnamentGlyph.Flight => "glyph-flight",
        OrnamentGlyph.PlanFallback => "glyph-plan-fallback",
        _ => string.Empty,
    };

    /// <summary>The sprite's key in <c>ornaments.json</c>.</summary>
    public static string SpriteKey(OrnamentSprite sprite) => sprite switch
    {
        OrnamentSprite.Crest => "crest",
        OrnamentSprite.CornerMark => "corner-mark",
        OrnamentSprite.SigilStar => "sigil-star",
        OrnamentSprite.DividerPhases => "divider-phases",
        _ => string.Empty,
    };
}

/// <summary>
/// The Moon Road ornament colours (proposal §3) as hex, for the ornament and orbit primitives. The palette task adds
/// the same six to <see cref="GlyphTokens"/> and <c>Theme</c>; until then the primitives read them from here.
/// </summary>
public static class OrnamentTokens
{
    public const uint AbyssHex = 0x080B16;
    public const uint NightTopHex = 0x151C33;
    public const uint GiltHex = 0xA88B52;
    public const uint GiltHighHex = 0xD9BE82;
    public const uint TideHex = 0x6F8FD0;
    public const uint TideDeepHex = 0x24345C;
}
