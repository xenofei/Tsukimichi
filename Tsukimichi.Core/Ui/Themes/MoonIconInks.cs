using System.Numerics;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>One stop of a moon icon gradient: where it sits (0 to 1) and its colour.</summary>
public readonly record struct InkStop(float At, Vector4 Color);

/// <summary>A theme's moon icon face: its well (lit from the upper left), its crescent and the crescent's earthshine.</summary>
/// <param name="Well">The well's radial stops, from the lit upper left outward.</param>
/// <param name="Moon">The crescent's stops, from its lit upper-left limb to the far corner.</param>
/// <param name="Earth">The earthshine on the crescent's dark part (drawn at .38).</param>
public sealed record MoonIconFaceInks(InkStop[] Well, InkStop[] Moon, Vector4 Earth);

/// <summary>
/// The moon icon's fixed inks (feature plan v8 H1, H2; spec-1.22 "Colour language", H1, H2), from the approved mock
/// (<c>docs/design/v8/mock-src/v722.js</c>: <c>FACE22</c>, <c>face22</c>, <c>fx22</c>, <c>mi22</c>). The icon floats over the
/// game, not on a pane, so like the state glyphs it keeps its own colours on every palette; there are no new tokens
/// (spec decision 19): the rims are the frame kits' resting ramps, the hover glow is the moonlight of the v7 Completed
/// moon (<see cref="MoonIconHover.GlowHex"/>), and the dots are copper and Tide from the palette.
/// </summary>
public static class MoonIconInks
{
    /// <summary>#080B16, the keyline and the shadow (the medallion's Abyss).</summary>
    public static readonly Vector4 NightInk = ColorMath.FromHex(GlyphTokens.Medallion.KeylineHex);

    /// <summary>#0F1424, the 1.5 px Night ring round a dot.</summary>
    public static readonly Vector4 DotRing = ColorMath.FromHex(0x0F1424);

    /// <summary>#E2E8F4, the hover glow: cool moonlight, never the warm Ready halo.</summary>
    public static readonly Vector4 Glow = ColorMath.FromHex(MoonIconHover.GlowHex);

    /// <summary>#DDE3F0, Plain's flat glyph and ring, and Classic's hairline.</summary>
    public static readonly Vector4 PlainInk = ColorMath.FromHex(0xDDE3F0);

    /// <summary>#151A28, Plain's flat well.</summary>
    public static readonly Vector4 PlainWell = ColorMath.FromHex(0x151A28);

    /// <summary>#C3CBDF, the hairline round a Quiet face.</summary>
    public static readonly Vector4 QuietLine = ColorMath.FromHex(0xC3CBDF);

    /// <summary>#DEB862, Sumi's kirikane line inside the lacquer rim.</summary>
    public static readonly Vector4 Kirikane = ColorMath.FromHex(0xDEB862);

    /// <summary>#F2ECDD, Sumi's gofun hairline on the rim's edge.</summary>
    public static readonly Vector4 Gofun = ColorMath.FromHex(0xF2ECDD);

    /// <summary>#9BE6FF, Aether Crystal's faint facet lines in the well and on the crescent.</summary>
    public static readonly Vector4 Facet = ColorMath.FromHex(0x9BE6FF);

    /// <summary>#1B2A58, Ishgard Glass's deeper lower pane (drawn at .55).</summary>
    public static readonly Vector4 LowerGlass = ColorMath.FromHex(0x1B2A58);

    /// <summary>#DCE5FF and #FFE2A8, the medallion well's faint cool and warm stars.</summary>
    public static readonly Vector4 CoolStar = ColorMath.FromHex(0xDCE5FF);

    public static readonly Vector4 WarmStar = ColorMath.FromHex(0xFFE2A8);

    /// <summary>#F4E4BC, the Orrery's constellation stars.</summary>
    public static readonly Vector4 ConstellationStar = ColorMath.FromHex(0xF4E4BC);

    // ---- Particles (H2) ----

    /// <summary>#FFE9BE and #FFE2A8, a gold mote and its own faint halo.</summary>
    public static readonly Vector4 Mote = ColorMath.FromHex(0xFFE9BE);

    public static readonly Vector4 MoteHalo = ColorMath.FromHex(0xFFE2A8);

    /// <summary>#F4F2EA and #DCE5FF, a Classic star and its halo.</summary>
    public static readonly Vector4 Star = ColorMath.FromHex(0xF4F2EA);

    public static readonly Vector4 StarHalo = ColorMath.FromHex(0xDCE5FF);

    /// <summary>#E6F0FF, the frost glint's sparkle halo (its line and head are white).</summary>
    public static readonly Vector4 FrostHalo = ColorMath.FromHex(0xE6F0FF);

    /// <summary>#3E7FA8, #BDEFFF and #F4FCFF: a shard's body, its lit facet, and its flash (light, never a dark disc).</summary>
    public static readonly Vector4 ShardBody = ColorMath.FromHex(0x3E7FA8);

    public static readonly Vector4 ShardLit = ColorMath.FromHex(0xBDEFFF);

    public static readonly Vector4 ShardFlash = ColorMath.FromHex(0xF4FCFF);

    /// <summary>#F2DDA8 and #FFF6DE, the Orrery's bead and its highlight.</summary>
    public static readonly Vector4 Bead = ColorMath.FromHex(0xF2DDA8);

    public static readonly Vector4 BeadHigh = ColorMath.FromHex(0xFFF6DE);

    /// <summary>#EAD3A0, the Orrery's orbit hairline (at .14) and constellation lines.</summary>
    public static readonly Vector4 Orbit = ColorMath.FromHex(0xEAD3A0);

    /// <summary>#F4DA92 and #C9A24E, a gold-leaf fleck catching the light, and at rest.</summary>
    public static readonly Vector4 LeafLit = ColorMath.FromHex(0xF4DA92);

    public static readonly Vector4 LeafBody = ColorMath.FromHex(0xC9A24E);

    // ---- Rims: each kit's resting ramp, lit from the upper left (high, body, shadow, deep) ----

    /// <summary>The medallion gilt's resting ramp (#E6CF98 → #9A7E4A → #7C6236 → #5C4724), without its specular glint.</summary>
    private static readonly InkStop[] BrassRim = Ramp(
        ColorMath.FromHex(GlyphTokens.Medallion.GiltHighHex),
        ColorMath.FromHex(GlyphTokens.Medallion.GiltMidHex),
        ColorMath.FromHex(GlyphTokens.Medallion.GiltShadeHex),
        ColorMath.FromHex(GlyphTokens.Medallion.GiltDeepHex));

    private static readonly InkStop[] SilverRim = Ramp(FrameKitMetals.Silver.Ornament);
    private static readonly InkStop[] CameRim = Ramp(FrameKitMetals.Came.Ornament);
    private static readonly InkStop[] AstrolabeRim = Ramp(FrameKitMetals.Astrolabe.Ornament);

    /// <summary>Sumi's black lacquer (#4A444C → #060508): its kit's leaf is the kirikane line over it, not the rim.</summary>
    private static readonly InkStop[] LacquerRim = Ramp(ColorMath.FromHex(0x4A444C), ColorMath.FromHex(0x2A262E), ColorMath.FromHex(0x141216), ColorMath.FromHex(0x060508));

    private static readonly MoonIconFaceInks MedallionFace = new(Well(0x2C3D80, 0x16204A, 0x0C1230), Moon(0xF7F4EA, 0xD9D2BE), ColorMath.FromHex(0x3C4C8A));
    private static readonly MoonIconFaceInks ClassicFace = new(Well(0x24305E, 0x1C2752, 0x141C40), Moon(0xF4E9C6, 0xE2D3A6), ColorMath.FromHex(0x2C3866));
    private static readonly MoonIconFaceInks GlassFace = new(Well(0x4A68AE, 0x2E4682, 0x1E3064), Moon(0xF6F1E2, 0xE3DAC3), ColorMath.FromHex(0x3A5292));
    private static readonly MoonIconFaceInks CrystalFace = new(Well(0x2C3C62, 0x18233E, 0x0E1528), Moon(0xEEF8FF, 0xC9E3F2), ColorMath.FromHex(0x2A3E66));
    private static readonly MoonIconFaceInks OrreryFace = new(Well(0x2A3778, 0x1A245A, 0x111A40), Moon(0xF8EBCF, 0xE2CFA6), ColorMath.FromHex(0x2E3C7A));
    private static readonly MoonIconFaceInks SumiFace = new(Well(0x16131A, 0x0E0C10, 0x060508), Moon(0xF3EAD3, 0xE2D7BB), ColorMath.FromHex(0x2A2832));

    /// <summary>The face <paramref name="theme"/> draws (Medallion's for anything else).</summary>
    public static MoonIconFaceInks Face(ThemeId theme) => theme switch
    {
        ThemeId.Classic => ClassicFace,
        ThemeId.IshgardGlass => GlassFace,
        ThemeId.AetherCrystal => CrystalFace,
        ThemeId.Orrery => OrreryFace,
        ThemeId.Sumi => SumiFace,
        _ => MedallionFace,
    };

    /// <summary>The rim <paramref name="kit"/> draws: its resting ramp at 0, .45, .8 and 1 (Sumi's kit draws lacquer under its kirikane line).</summary>
    public static InkStop[] Rim(FrameKitId kit) => kit switch
    {
        FrameKitId.Silver => SilverRim,
        FrameKitId.Came => CameRim,
        FrameKitId.Astrolabe => AstrolabeRim,
        FrameKitId.Kirikane => LacquerRim,
        _ => BrassRim,
    };

    private static InkStop[] Ramp(Vector4 high, Vector4 body, Vector4 shadow, Vector4 deep) =>
        [new(0f, high), new(0.45f, body), new(0.8f, shadow), new(1f, deep)];

    private static InkStop[] Ramp(BrassTokens metal) => Ramp(metal.High, metal.Body, metal.Shadow, metal.Deep);

    private static InkStop[] Well(uint light, uint mid, uint deep) =>
        [new(0f, ColorMath.FromHex(light)), new(0.6f, ColorMath.FromHex(mid)), new(1f, ColorMath.FromHex(deep))];

    private static InkStop[] Moon(uint high, uint low) =>
        [new(0f, ColorMath.FromHex(high)), new(1f, ColorMath.FromHex(low))];
}
