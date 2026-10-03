using System.Numerics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>Settings › Display › Glyph palette.</summary>
public enum GlyphPaletteKind
{
    /// <summary>The moons as designed (glyph proposal v2.1 §3.1): gold and silver lit parts, shading and interior detail.</summary>
    Standard = 0,

    /// <summary>
    /// High contrast (accessibility panel §2.2): flat colours on a luminance ladder, thicker rims and one in-disc mark
    /// per state, so every state reads without colour (greyscale, red-green and blue-yellow colour blindness).
    /// </summary>
    HighContrast = 1,
}

/// <summary>
/// The outline and fill a glyph shows before any mark: what is left when both colour and the marks are ignored. Two
/// states that share a silhouette must differ by at least 3 : 1 in the luminance of their <see cref="GlyphStyle.Identity"/>
/// in the high-contrast palette (the ladder).
/// </summary>
public enum GlyphSilhouette
{
    /// <summary>A solid disc (Completed).</summary>
    FullDisc,

    /// <summary>The right half lit (Ready, Ready on another job).</summary>
    HalfLit,

    /// <summary>A gibbous: more than half lit, waxing or waning (In journal, Done). Mirror images count as one silhouette (panel A2).</summary>
    GibbousLit,

    /// <summary>An empty disc inside a closed ring (Blocked).</summary>
    EmptyRing,

    /// <summary>An empty disc crossed by a diagonal bar (Locked out).</summary>
    BarredRing,

    /// <summary>An empty disc inside a broken ring (Not checked).</summary>
    DashedRing,
}

/// <summary>The non-colour channel a state glyph carries on top of its silhouette.</summary>
public enum GlyphMark
{
    /// <summary>Nothing beyond the silhouette.</summary>
    None,

    /// <summary>Standard Ready: a glow (a thin outer ring below r 9) outside the rim.</summary>
    Glow,

    /// <summary>Standard In journal: a small dark seal on the lit side.</summary>
    Seal,

    /// <summary>High-contrast In journal: the seal enlarged.</summary>
    LargeSeal,

    /// <summary>High-contrast Ready: a bold vertical bar in the dark half.</summary>
    Bar,

    /// <summary>High-contrast Ready on another job: a hollow (outlined) vertical bar in the dark half.</summary>
    HollowBar,

    /// <summary>High-contrast Done: a small check on the lit part.</summary>
    Check,

    /// <summary>High-contrast Completed: the disc is solid to the keyline, with no rim.</summary>
    SolidDisc,

    /// <summary>High-contrast Blocked: a rim about twice the usual width around an empty disc.</summary>
    ThickRim,

    /// <summary>Standard Locked out: a diagonal bar through the disc.</summary>
    DiagonalBar,

    /// <summary>High-contrast Locked out: the diagonal bar thickened.</summary>
    ThickDiagonalBar,

    /// <summary>Not checked: a dashed rim.</summary>
    Dashes,
}

/// <summary>How a state glyph's rim is stroked.</summary>
public enum RimStyle
{
    /// <summary>No rim (Completed).</summary>
    None,

    /// <summary>A closed ring just inside the edge.</summary>
    Solid,

    /// <summary>Evenly spaced dashes from 12 o'clock.</summary>
    Dashed,
}

/// <summary>Rim stroke width: <see cref="Fraction"/> · r clamped to <see cref="Min"/>..<see cref="Max"/> px.</summary>
public readonly record struct RimRule(float Fraction, float Min, float Max)
{
    /// <summary>The stroke for a glyph of radius <paramref name="radius"/> px.</summary>
    public float Width(float radius) => Math.Clamp(Fraction * radius, Min, Max);
}

/// <summary>
/// How one quest state is drawn in one palette: the silhouette, the colours of its lit part, rim and mark, the rim's
/// style and width rule, and the mark. Colours are opaque sRGB.
/// </summary>
/// <param name="State">The quest state.</param>
/// <param name="Silhouette">What the outline and fill show without colour or mark.</param>
/// <param name="Phase">The lit shape (<see cref="MoonPhase.New"/> when nothing is lit).</param>
/// <param name="Lit">Colour of the lit part; for Completed, the whole disc.</param>
/// <param name="Rim">Colour of the rim.</param>
/// <param name="RimStyle">Solid, dashed or none.</param>
/// <param name="RimRule">Rim stroke width rule.</param>
/// <param name="Mark">The non-colour mark.</param>
/// <param name="MarkColor">Colour of the mark.</param>
public readonly record struct GlyphStyle(
    QuestState State,
    GlyphSilhouette Silhouette,
    MoonPhase Phase,
    Vector4 Lit,
    Vector4 Rim,
    RimStyle RimStyle,
    RimRule RimRule,
    GlyphMark Mark,
    Vector4 MarkColor)
{
    /// <summary>Whether any part of the disc is lit.</summary>
    public bool HasLit => Phase != MoonPhase.New;

    /// <summary>
    /// The colour that carries the state's place on the luminance ladder: the lit part when there is one, otherwise the
    /// rim (the diagonal bar shares the rim's colour).
    /// </summary>
    public Vector4 Identity => HasLit ? Lit : Rim;

    /// <summary>Whether the mark is drawn on the lit part (seal, check) rather than on the unlit disc (bars) or the rim.</summary>
    public bool MarkOnLit => Mark is GlyphMark.Seal or GlyphMark.LargeSeal or GlyphMark.Check;

    /// <summary>The rim stroke for a glyph of radius <paramref name="radius"/> px.</summary>
    public float RimWidth(float radius) => RimRule.Width(radius);
}

/// <summary>The colours of the non-moon marks (<c>Ui.Marks</c>) in a palette.</summary>
/// <param name="Check">Met, obtained.</param>
/// <param name="Cross">Unmet, not obtained.</param>
/// <param name="Dash">Not readable.</param>
/// <param name="LivePip">The live character.</param>
/// <param name="SnapshotPip">A stored snapshot.</param>
/// <param name="StrokeScale">Multiplies the marks' stroke width.</param>
public readonly record struct MarkColors(Vector4 Check, Vector4 Cross, Vector4 Dash, Vector4 LivePip, Vector4 SnapshotPip, float StrokeScale);

/// <summary>
/// The fixed colour tokens the glyphs are drawn with, as 0xRRGGBB (ui-revamp §4.3, glyph proposal v2.1 §3.4). The
/// plugin's <c>Ui.Theme</c> builds its tokens from these, so the palette tests check the colours that are drawn.
/// </summary>
public static class GlyphTokens
{
    public const uint NightHex = 0x0F1424;
    public const uint NightSunkenHex = 0x0B0F1C;
    public const uint MoonHex = 0xF2D27A;
    public const uint MoonHighHex = 0xFFF0BE;
    public const uint MoonDeepHex = 0xD6B25A;
    public const uint SilverHex = 0xDDE3F0;
    public const uint SilverHighHex = 0xFFFFFF;
    public const uint SilverDeepHex = 0xB9C2D8;
    public const uint DuskHex = 0x7C86A8;
    public const uint EclipseHex = 0xB25C7F;
    public const uint VeilHex = 0x4A5270;
    public const uint ShadowHex = 0x3A4363;
    public const uint VeilLineHex = 0x5C6584;

    // The chrome tokens beside the glyph colours (ui-revamp §4.3), so the contrast tests read the values Theme draws.

    /// <summary>#262D45 – hover fill for rows, tabs and buttons.</summary>
    public const uint NightHoverHex = 0x262D45;

    /// <summary>#2A3149 – hairlines, card borders, row separators.</summary>
    public const uint NightLineHex = 0x2A3149;

    /// <summary>#A9B2CC – secondary text.</summary>
    public const uint MistHex = 0xA9B2CC;

    /// <summary>#8A93B0 – the Unknown state's text and dashed ring.</summary>
    public const uint VeilTextHex = 0x8A93B0;

    /// <summary>#D68AA8 – Locked out text.</summary>
    public const uint EclipseTextHex = 0xD68AA8;

    // The Moon Road tokens (moon-road proposal §3).

    /// <summary>#080B16 – the deepest surface: the rail, the letterbox behind the hero banner, the strip under the status bar.</summary>
    public const uint AbyssHex = 0x080B16;

    /// <summary>#151C33 – the top stop of each pane's sky-over-water gradient (NightTop at the top, Night from 60 % down).</summary>
    public const uint NightTopHex = 0x151C33;

    /// <summary>#A88B52 – brass for ornament hairlines. Decoration only: never text, never a fill over 4 px, never the only carrier of meaning.</summary>
    public const uint GiltHex = 0xA88B52;

    /// <summary>#D9BE82 – ornament highlight points under 4 px (corner diamonds, the sigil star).</summary>
    public const uint GiltHighHex = 0xD9BE82;

    /// <summary>#6F8FD0 – the cool accent (unlock quests, wind and sky in Flight, links): AA for text on Night, never "act now".</summary>
    public const uint TideHex = 0x6F8FD0;

    /// <summary>#24345C – the bottom stop of the drawn night sky and Flight's water gradient. Surface only.</summary>
    public const uint TideDeepHex = 0x24345C;

    /// <summary>#747D9C – the high-contrast "dim" rung: 4.5 : 1 on Night and 4.1 : 1 on white, so both variants share it.</summary>
    public const uint HighContrastDimHex = 0x747D9C;

    /// <summary>#3C2C06 – high-contrast gold on light hosts: a dark bronze, 3.3 : 1 against the dim rung.</summary>
    public const uint HighContrastInkGoldHex = 0x3C2C06;

    /// <summary>#1C2338 – high-contrast "silver" on light hosts: a near-black navy for the thick Blocked rim and the hollow bar.</summary>
    public const uint HighContrastInkNavyHex = 0x1C2338;

    /// <summary>#F6F7FB – the high-contrast disc and keyline on light hosts.</summary>
    public const uint HighContrastPaperHex = 0xF6F7FB;

    public static readonly Vector4 Night = ColorMath.FromHex(NightHex);
    public static readonly Vector4 NightSunken = ColorMath.FromHex(NightSunkenHex);
    public static readonly Vector4 Moon = ColorMath.FromHex(MoonHex);
    public static readonly Vector4 MoonHigh = ColorMath.FromHex(MoonHighHex);
    public static readonly Vector4 MoonDeep = ColorMath.FromHex(MoonDeepHex);
    public static readonly Vector4 Silver = ColorMath.FromHex(SilverHex);
    public static readonly Vector4 SilverHigh = ColorMath.FromHex(SilverHighHex);
    public static readonly Vector4 SilverDeep = ColorMath.FromHex(SilverDeepHex);
    public static readonly Vector4 Dusk = ColorMath.FromHex(DuskHex);
    public static readonly Vector4 Eclipse = ColorMath.FromHex(EclipseHex);
    public static readonly Vector4 Veil = ColorMath.FromHex(VeilHex);
    public static readonly Vector4 Shadow = ColorMath.FromHex(ShadowHex);
    public static readonly Vector4 VeilLine = ColorMath.FromHex(VeilLineHex);

    /// <summary>Moon dimmed toward Dusk (35 %), the quieter gold of finished things.</summary>
    public static readonly Vector4 MoonDim = Vector4.Lerp(Moon, Dusk, 0.35f);

    /// <summary>A panel one step above Night: a quarter of the way to Veil (#1E2437).</summary>
    public static readonly Vector4 NightRaised = Vector4.Lerp(Night, Veil, 0.25f);

    public static readonly Vector4 NightHover = ColorMath.FromHex(NightHoverHex);
    public static readonly Vector4 NightLine = ColorMath.FromHex(NightLineHex);
    public static readonly Vector4 Mist = ColorMath.FromHex(MistHex);
    public static readonly Vector4 VeilText = ColorMath.FromHex(VeilTextHex);
    public static readonly Vector4 EclipseText = ColorMath.FromHex(EclipseTextHex);

    public static readonly Vector4 Abyss = ColorMath.FromHex(AbyssHex);
    public static readonly Vector4 NightTop = ColorMath.FromHex(NightTopHex);
    public static readonly Vector4 Gilt = ColorMath.FromHex(GiltHex);
    public static readonly Vector4 GiltHigh = ColorMath.FromHex(GiltHighHex);
    public static readonly Vector4 Tide = ColorMath.FromHex(TideHex);
    public static readonly Vector4 TideDeep = ColorMath.FromHex(TideDeepHex);

    public static readonly Vector4 HighContrastDim = ColorMath.FromHex(HighContrastDimHex);
    public static readonly Vector4 HighContrastInkGold = ColorMath.FromHex(HighContrastInkGoldHex);
    public static readonly Vector4 HighContrastInkNavy = ColorMath.FromHex(HighContrastInkNavyHex);
    public static readonly Vector4 HighContrastPaper = ColorMath.FromHex(HighContrastPaperHex);

    /// <summary>
    /// The "Menphina's Medallion" palette (glyph design v6, round 5; the palette table in
    /// docs/design/moon-v6/round5/medallion-r5/concept.md, "Light, palette and sources"), as 0xRRGGBB. New UI from 1.11
    /// on draws with these instead of the old gold (<see cref="MoonHex"/> and its stops). The glyphs themselves still
    /// draw with the tokens above until they are redrawn in the medallion style (G1, 1.12).
    /// </summary>
    public static class Medallion
    {
        /// <summary>#080B16 – the keyline and cast shadows (the same Abyss as <see cref="GlyphTokens.AbyssHex"/>).</summary>
        public const uint KeylineHex = AbyssHex;

        // Gilt, lightest to darkest: rims, the badge ring, the check, the arrow, the frame.

        /// <summary>#FFF4D6 – the gilt's specular glint on upper-left edges.</summary>
        public const uint GiltSpecularHex = 0xFFF4D6;

        /// <summary>#E6CF98 – the gilt's lit face.</summary>
        public const uint GiltHighHex = 0xE6CF98;

        /// <summary>#D9BE82 – the gilt's body.</summary>
        public const uint GiltHex = 0xD9BE82;

        /// <summary>#9A7E4A – the gilt turning from the light.</summary>
        public const uint GiltMidHex = 0x9A7E4A;

        /// <summary>#7C6236 – the gilt's shaded side.</summary>
        public const uint GiltShadeHex = 0x7C6236;

        /// <summary>#5C4724 – the gilt's lower-right edges.</summary>
        public const uint GiltDeepHex = 0x5C4724;

        /// <summary>#33260F – the gilt's darkest crease.</summary>
        public const uint GiltDarkHex = 0x33260F;

        // Lit lapis (Ready): the sky runs top to horizon, the sea horizon to bottom.

        /// <summary>#6090DE – Ready's sky at the top.</summary>
        public const uint LapisSkyTopHex = 0x6090DE;

        /// <summary>#8AB2F3 – Ready's sky at the horizon.</summary>
        public const uint LapisSkyHorizonHex = 0x8AB2F3;

        /// <summary>#5480C8 – Ready's sea at the horizon.</summary>
        public const uint LapisSeaHorizonHex = 0x5480C8;

        /// <summary>#30529A – Ready's sea at the bottom.</summary>
        public const uint LapisSeaBottomHex = 0x30529A;

        /// <summary>#EDCB94 – Ready's twilight afterglow on the horizon (OKLCH C .081), the one warm note in its scene.</summary>
        public const uint AfterglowHex = 0xEDCB94;

        // The night scene (In journal), the same way round.

        /// <summary>#1B2856 – In journal's sky at the top.</summary>
        public const uint NightSkyTopHex = 0x1B2856;

        /// <summary>#2A3D72 – In journal's sky at the horizon.</summary>
        public const uint NightSkyHorizonHex = 0x2A3D72;

        /// <summary>#22346A – In journal's sea at the horizon.</summary>
        public const uint NightSeaHorizonHex = 0x22346A;

        /// <summary>#111A3C – In journal's sea at the bottom.</summary>
        public const uint NightSeaBottomHex = 0x111A3C;

        /// <summary>#1D2B5A – the resting enamel, lit end.</summary>
        public const uint EnamelHex = 0x1D2B5A;

        /// <summary>#131C40 – the resting enamel, deep end.</summary>
        public const uint EnamelDeepHex = 0x131C40;

        // Moonstone, lightest to darkest: the cool moon.

        /// <summary>#F4F2EA – moonstone's glint.</summary>
        public const uint MoonstoneSpecularHex = 0xF4F2EA;

        /// <summary>#E2E8F4 – moonstone's lit face.</summary>
        public const uint MoonstoneHighHex = 0xE2E8F4;

        /// <summary>#C3CEE4 – moonstone's body.</summary>
        public const uint MoonstoneHex = 0xC3CEE4;

        /// <summary>#95A5C8 – moonstone mid.</summary>
        public const uint MoonstoneMidHex = 0x95A5C8;

        /// <summary>#5E6E97 – moonstone low.</summary>
        public const uint MoonstoneDeepHex = 0x5E6E97;

        // Cloud (the Mist family), lightest to darkest.

        /// <summary>#BAC3DB – a cloud's lit top.</summary>
        public const uint CloudHighHex = 0xBAC3DB;

        /// <summary>#6F7BA2 – a cloud's body.</summary>
        public const uint CloudHex = 0x6F7BA2;

        /// <summary>#46507A – a cloud's shaded side.</summary>
        public const uint CloudShadeHex = 0x46507A;

        /// <summary>#262E4E – a cloud's underside.</summary>
        public const uint CloudDeepHex = 0x262E4E;

        // The ribbon (the In journal bookmark), in Tide silk.

        /// <summary>#6F8FD0 – the ribbon (the same Tide as <see cref="GlyphTokens.TideHex"/>).</summary>
        public const uint RibbonHex = TideHex;

        /// <summary>#A9BEEA – the ribbon's lit fold.</summary>
        public const uint RibbonHighHex = 0xA9BEEA;

        /// <summary>#3F5A98 – the ribbon's shaded fold.</summary>
        public const uint RibbonDeepHex = 0x3F5A98;

        // Dalamud, the red moon.

        /// <summary>#DA6470 – Dalamud's lit face.</summary>
        public const uint DalamudHex = 0xDA6470;

        /// <summary>#C24A58 – Dalamud's body.</summary>
        public const uint DalamudShadeHex = 0xC24A58;

        /// <summary>#7A2838 – Dalamud's dark limb.</summary>
        public const uint DalamudDeepHex = 0x7A2838;

        /// <summary>#F4AAB2 – the glowing edge of Dalamud's cracks.</summary>
        public const uint DalamudCrackHex = 0xF4AAB2;

        /// <summary>#0B0408 – Dalamud's empty socket.</summary>
        public const uint DalamudSocketHex = 0x0B0408;

        // Jade, the green check, lightest to darkest.

        /// <summary>#E4F6DC – jade's glint.</summary>
        public const uint JadeSpecularHex = 0xE4F6DC;

        /// <summary>#A9DCA2 – jade's lit face.</summary>
        public const uint JadeHighHex = 0xA9DCA2;

        /// <summary>#6FAE79 – jade's body.</summary>
        public const uint JadeHex = 0x6FAE79;

        /// <summary>#3F7A50 – jade's shaded side.</summary>
        public const uint JadeDeepHex = 0x3F7A50;

        // The role seats behind a job icon (Ready on another job): each role's seat and its shade.

        /// <summary>#5878C2 – the tank seat.</summary>
        public const uint TankHex = 0x5878C2;

        /// <summary>#2C417E – the tank seat's shade.</summary>
        public const uint TankDeepHex = 0x2C417E;

        /// <summary>#5C9A68 – the healer seat.</summary>
        public const uint HealerHex = 0x5C9A68;

        /// <summary>#2B5A38 – the healer seat's shade.</summary>
        public const uint HealerDeepHex = 0x2B5A38;

        /// <summary>#B25A64 – the DPS seat.</summary>
        public const uint DpsHex = 0xB25A64;

        /// <summary>#5E2632 – the DPS seat's shade.</summary>
        public const uint DpsDeepHex = 0x5E2632;

        /// <summary>#E0B860 – the lantern lights on the far shore.</summary>
        public const uint LanternHex = 0xE0B860;

        public static readonly Vector4 Keyline = ColorMath.FromHex(KeylineHex);
        public static readonly Vector4 GiltSpecular = ColorMath.FromHex(GiltSpecularHex);
        public static readonly Vector4 GiltHigh = ColorMath.FromHex(GiltHighHex);
        public static readonly Vector4 Gilt = ColorMath.FromHex(GiltHex);
        public static readonly Vector4 GiltMid = ColorMath.FromHex(GiltMidHex);
        public static readonly Vector4 GiltShade = ColorMath.FromHex(GiltShadeHex);
        public static readonly Vector4 GiltDeep = ColorMath.FromHex(GiltDeepHex);
        public static readonly Vector4 GiltDark = ColorMath.FromHex(GiltDarkHex);
        public static readonly Vector4 LapisSkyTop = ColorMath.FromHex(LapisSkyTopHex);
        public static readonly Vector4 LapisSkyHorizon = ColorMath.FromHex(LapisSkyHorizonHex);
        public static readonly Vector4 LapisSeaHorizon = ColorMath.FromHex(LapisSeaHorizonHex);
        public static readonly Vector4 LapisSeaBottom = ColorMath.FromHex(LapisSeaBottomHex);
        public static readonly Vector4 Afterglow = ColorMath.FromHex(AfterglowHex);
        public static readonly Vector4 NightSkyTop = ColorMath.FromHex(NightSkyTopHex);
        public static readonly Vector4 NightSkyHorizon = ColorMath.FromHex(NightSkyHorizonHex);
        public static readonly Vector4 NightSeaHorizon = ColorMath.FromHex(NightSeaHorizonHex);
        public static readonly Vector4 NightSeaBottom = ColorMath.FromHex(NightSeaBottomHex);
        public static readonly Vector4 Enamel = ColorMath.FromHex(EnamelHex);
        public static readonly Vector4 EnamelDeep = ColorMath.FromHex(EnamelDeepHex);
        public static readonly Vector4 MoonstoneSpecular = ColorMath.FromHex(MoonstoneSpecularHex);
        public static readonly Vector4 MoonstoneHigh = ColorMath.FromHex(MoonstoneHighHex);
        public static readonly Vector4 Moonstone = ColorMath.FromHex(MoonstoneHex);
        public static readonly Vector4 MoonstoneMid = ColorMath.FromHex(MoonstoneMidHex);
        public static readonly Vector4 MoonstoneDeep = ColorMath.FromHex(MoonstoneDeepHex);
        public static readonly Vector4 CloudHigh = ColorMath.FromHex(CloudHighHex);
        public static readonly Vector4 Cloud = ColorMath.FromHex(CloudHex);
        public static readonly Vector4 CloudShade = ColorMath.FromHex(CloudShadeHex);
        public static readonly Vector4 CloudDeep = ColorMath.FromHex(CloudDeepHex);
        public static readonly Vector4 Ribbon = ColorMath.FromHex(RibbonHex);
        public static readonly Vector4 RibbonHigh = ColorMath.FromHex(RibbonHighHex);
        public static readonly Vector4 RibbonDeep = ColorMath.FromHex(RibbonDeepHex);
        public static readonly Vector4 Dalamud = ColorMath.FromHex(DalamudHex);
        public static readonly Vector4 DalamudShade = ColorMath.FromHex(DalamudShadeHex);
        public static readonly Vector4 DalamudDeep = ColorMath.FromHex(DalamudDeepHex);
        public static readonly Vector4 DalamudCrack = ColorMath.FromHex(DalamudCrackHex);
        public static readonly Vector4 DalamudSocket = ColorMath.FromHex(DalamudSocketHex);
        public static readonly Vector4 JadeSpecular = ColorMath.FromHex(JadeSpecularHex);
        public static readonly Vector4 JadeHigh = ColorMath.FromHex(JadeHighHex);
        public static readonly Vector4 Jade = ColorMath.FromHex(JadeHex);
        public static readonly Vector4 JadeDeep = ColorMath.FromHex(JadeDeepHex);
        public static readonly Vector4 Tank = ColorMath.FromHex(TankHex);
        public static readonly Vector4 TankDeep = ColorMath.FromHex(TankDeepHex);
        public static readonly Vector4 Healer = ColorMath.FromHex(HealerHex);
        public static readonly Vector4 HealerDeep = ColorMath.FromHex(HealerDeepHex);
        public static readonly Vector4 Dps = ColorMath.FromHex(DpsHex);
        public static readonly Vector4 DpsDeep = ColorMath.FromHex(DpsDeepHex);
        public static readonly Vector4 Lantern = ColorMath.FromHex(LanternHex);
    }
}

/// <summary>
/// Everything that differs between the glyph palettes (Settings › Display › Glyph palette; accessibility panel §2.2):
/// per state the colours, silhouette, mark and rim width (<see cref="GlyphStyle"/>), plus the ground the high-contrast
/// glyphs carry with them, the halo gauge's colours and stroke, the table stripe's colours and width, and the
/// colours of the check / cross / dash / pip marks. Pure data; the plugin's <c>MoonGlyph</c>, <c>Marks</c> and table
/// stripe paint from the palette <c>Theme</c> resolves once per frame.
///
/// <para><b>Standard</b> is the palette as shipped in 1.0 (proposal v2.1 §3.1) and draws exactly as before.</para>
///
/// <para><b>High contrast</b> comes in two variants picked by the host window's luminance (<see cref="Resolve"/>): the
/// dark one for Night and dark Dalamud themes, the light one for light themes. Each is flat (no gradient, detail or
/// glow: they lower the limb's contrast) and built on a luminance ladder of four inks over a ground:</para>
/// <list type="table">
/// <item><term>Dark variant</term><description>ground #0B0F1C (L 0.005) · Eclipse #B25C7F (0.187) · dim #747D9C (0.208) ·
/// gold #FFF0BE (0.873) · white #FFFFFF (1.0)</description></item>
/// <item><term>Light variant</term><description>ground #F6F7FB (0.931) · dim #747D9C (0.208) · Eclipse #B25C7F (0.187) ·
/// bronze #3C2C06 (0.028) · navy #1C2338 (0.017)</description></item>
/// </list>
/// <para>States that share a silhouette sit on rungs at least 3 : 1 apart (Ready gold vs Ready on another job dim,
/// 3.6 : 1 dark and 3.3 : 1 light; In journal gold vs Done dim, the same), every ink is at least 3 : 1 against its
/// background and against the ground it is drawn on, and every state has its own mark. The ground is drawn as a
/// keyline disc around each glyph (and a well under the halo track), so a glyph still reads on a surface that does not
/// follow the host — the Night hover cards and the Duty Finder panel under a light theme, or the todo overlay over snow.</para>
/// </summary>
public sealed class GlyphPalette
{
    /// <summary>Host window luminance above which the light high-contrast variant is used: where black text starts to out-contrast white.</summary>
    public const float LightHostLuminance = 0.179f;

    private readonly GlyphStyle[] styles;
    private readonly Vector4[] stripes;

    private GlyphPalette(
        GlyphPaletteKind kind,
        bool light,
        string name,
        Vector4 ground,
        float keylineFraction,
        GlyphStyle[] styles,
        Vector4 haloTrack,
        Vector4 haloTrackOnCard,
        Vector4 haloArc,
        Vector4 haloCompleteDim,
        float haloStrokeMin,
        float haloStrokeFraction,
        Vector4[] stripes,
        float stripeWidth,
        MarkColors marks)
    {
        Kind = kind;
        Light = light;
        Name = name;
        Ground = ground;
        KeylineFraction = keylineFraction;
        this.styles = styles;
        HaloTrack = haloTrack;
        HaloTrackOnCard = haloTrackOnCard;
        HaloArc = haloArc;
        HaloCompleteDim = haloCompleteDim;
        HaloStrokeMin = haloStrokeMin;
        HaloStrokeFraction = haloStrokeFraction;
        this.stripes = stripes;
        StripeWidth = stripeWidth;
        Marks = marks;
    }

    /// <summary>Which setting this palette belongs to.</summary>
    public GlyphPaletteKind Kind { get; }

    /// <summary>Whether this is the high-contrast palette (either variant).</summary>
    public bool HighContrast => Kind == GlyphPaletteKind.HighContrast;

    /// <summary>Whether this is the variant for light host windows (dark inks on a light ground).</summary>
    public bool Light { get; }

    /// <summary>A short name for the glyph window.</summary>
    public string Name { get; }

    /// <summary>The unlit disc (and, in high contrast, the keyline around the glyph and the well under the halo track).</summary>
    public Vector4 Ground { get; }

    /// <summary>Keyline width as a fraction of r (at least 1 px when non-zero); 0 for Standard, which has none.</summary>
    public float KeylineFraction { get; }

    /// <summary>Halo track on a window.</summary>
    public Vector4 HaloTrack { get; }

    /// <summary>Halo track on a raised card.</summary>
    public Vector4 HaloTrackOnCard { get; }

    /// <summary>Halo arc (and the complete ring).</summary>
    public Vector4 HaloArc { get; }

    /// <summary>A complete gauge that steps back (the Journal tree's finished nodes).</summary>
    public Vector4 HaloCompleteDim { get; }

    /// <summary>Minimum halo stroke in px.</summary>
    public float HaloStrokeMin { get; }

    /// <summary>Halo stroke as a fraction of R.</summary>
    public float HaloStrokeFraction { get; }

    /// <summary>The table stripe's width in logical px (scaled by the UI scale, at least 2 device px).</summary>
    public float StripeWidth { get; }

    /// <summary>Check, cross, dash and pip colours.</summary>
    public MarkColors Marks { get; }

    /// <summary>The eight state styles, in <see cref="QuestState"/> order.</summary>
    public IReadOnlyList<GlyphStyle> Styles => styles;

    /// <summary>The style of <paramref name="state"/>; an unknown value draws as Not checked.</summary>
    public GlyphStyle Style(QuestState state) =>
        (uint)state < (uint)styles.Length ? styles[(int)state] : styles[(int)QuestState.Unknown];

    /// <summary>Keyline width for a glyph of radius <paramref name="radius"/>: 0 in Standard, else max(1, fraction · r).</summary>
    public float Keyline(float radius) => KeylineFraction > 0f ? MathF.Max(1f, KeylineFraction * radius) : 0f;

    /// <summary>Halo track and arc stroke for a gauge of half-size <paramref name="radius"/>.</summary>
    public float HaloStroke(float radius) => MathF.Max(HaloStrokeMin, HaloStrokeFraction * radius);

    /// <summary>
    /// The colour of a state's table stripe on Night: in Standard the table's own mapping (which the table swaps for the
    /// host's text colour on light themes), in high contrast the state's <see cref="GlyphStyle.Identity"/>.
    /// </summary>
    public Vector4 Stripe(QuestState state) =>
        (uint)state < (uint)stripes.Length ? stripes[(int)state] : stripes[(int)QuestState.Unknown];

    /// <summary>Every ink a glyph of this palette draws with, for the contrast checks: lit parts, rims, marks, halo, stripes and marks.</summary>
    public IEnumerable<(string Part, Vector4 Color)> Inks()
    {
        foreach (var style in styles)
        {
            if (style.HasLit) yield return ($"{style.State} lit", style.Lit);
            if (style.RimStyle != RimStyle.None) yield return ($"{style.State} rim", style.Rim);
            if (style.Mark != GlyphMark.None && !style.MarkOnLit && style.Mark is not (GlyphMark.SolidDisc or GlyphMark.ThickRim or GlyphMark.Dashes))
                yield return ($"{style.State} mark", style.MarkColor);
        }

        yield return ("halo track", HaloTrack);
        yield return ("halo arc", HaloArc);
        yield return ("halo complete (dim)", HaloCompleteDim);
        yield return ("check", Marks.Check);
        yield return ("cross", Marks.Cross);
        yield return ("dash", Marks.Dash);
        yield return ("live pip", Marks.LivePip);
        yield return ("snapshot pip", Marks.SnapshotPip);
        foreach (QuestState state in Enum.GetValues<QuestState>())
        {
            yield return ($"{state} stripe", Stripe(state));
        }
    }

    // ------------------------------------------------------------------ the palettes

    // Initialised before the palettes below (static fields run in textual order).
    private static readonly RimRule StandardRim = new(0.12f, 1.5f, 3f);
    private static readonly RimRule ContrastRim = new(0.16f, 2f, 4f);
    private static readonly RimRule ContrastThickRim = new(0.28f, 3f, 6f);

    /// <summary>#8A93B0 – the Unknown text tone the Standard stripe uses for Blocked and Not checked.</summary>
    private static readonly Vector4 VeilText = ColorMath.FromHex(0x8A93B0);

    /// <summary>The palette as shipped (proposal v2.1 §3.1; the colours <c>MoonGlyph</c> draws Standard with).</summary>
    public static readonly GlyphPalette Standard = BuildStandard();

    /// <summary>High contrast on Night and dark hosts.</summary>
    public static readonly GlyphPalette HighContrastDark = BuildContrast(light: false);

    /// <summary>High contrast on light hosts.</summary>
    public static readonly GlyphPalette HighContrastLight = BuildContrast(light: true);

    /// <summary>
    /// The palette to draw with for the setting <paramref name="kind"/> on a host whose window colour is
    /// <paramref name="window"/>: Standard as it is; high contrast in the light variant when the window's luminance is
    /// above <see cref="LightHostLuminance"/>, else the dark one. Allocates nothing.
    /// </summary>
    public static GlyphPalette Resolve(GlyphPaletteKind kind, Vector4 window)
    {
        if (kind != GlyphPaletteKind.HighContrast)
        {
            return Standard;
        }

        return ColorMath.Luminance(window) > LightHostLuminance ? HighContrastLight : HighContrastDark;
    }

    private static GlyphPalette BuildStandard()
    {
        var styles = new GlyphStyle[StateCount()];
        Set(styles, new(QuestState.Completed, GlyphSilhouette.FullDisc, MoonPhase.Full, GlyphTokens.Moon, GlyphTokens.Moon, RimStyle.None, StandardRim, GlyphMark.None, GlyphTokens.Moon));
        Set(styles, new(QuestState.Accepted, GlyphSilhouette.GibbousLit, MoonPhase.WaxingGibbous, GlyphTokens.Moon, GlyphTokens.Silver, RimStyle.Solid, StandardRim, GlyphMark.Seal, GlyphTokens.Night));
        Set(styles, new(QuestState.Ready, GlyphSilhouette.HalfLit, MoonPhase.FirstQuarter, GlyphTokens.Moon, GlyphTokens.Dusk, RimStyle.Solid, StandardRim, GlyphMark.Glow, GlyphTokens.Moon));
        Set(styles, new(QuestState.ReadyOnOtherJob, GlyphSilhouette.HalfLit, MoonPhase.FirstQuarter, GlyphTokens.Silver, GlyphTokens.Moon, RimStyle.Solid, StandardRim, GlyphMark.None, GlyphTokens.Silver));
        Set(styles, new(QuestState.DoneThisCycle, GlyphSilhouette.GibbousLit, MoonPhase.WaningGibbous, GlyphTokens.Silver, GlyphTokens.Dusk, RimStyle.Solid, StandardRim, GlyphMark.None, GlyphTokens.Silver));
        Set(styles, new(QuestState.Blocked, GlyphSilhouette.EmptyRing, MoonPhase.New, GlyphTokens.Shadow, GlyphTokens.Silver, RimStyle.Solid, StandardRim, GlyphMark.None, GlyphTokens.Silver));
        Set(styles, new(QuestState.Foreclosed, GlyphSilhouette.BarredRing, MoonPhase.New, GlyphTokens.Shadow, GlyphTokens.Eclipse, RimStyle.Solid, StandardRim, GlyphMark.DiagonalBar, GlyphTokens.Eclipse));
        Set(styles, new(QuestState.Unknown, GlyphSilhouette.DashedRing, MoonPhase.New, GlyphTokens.Shadow, GlyphTokens.Dusk, RimStyle.Dashed, StandardRim, GlyphMark.Dashes, GlyphTokens.Dusk));

        // The table's own mapping on Night (TablePane.StripeColor); on a light host it swaps silver for the host text.
        var stripes = new Vector4[styles.Length];
        stripes[(int)QuestState.Completed] = GlyphTokens.MoonDim;
        stripes[(int)QuestState.Accepted] = GlyphTokens.Moon;
        stripes[(int)QuestState.Ready] = GlyphTokens.Moon;
        stripes[(int)QuestState.ReadyOnOtherJob] = GlyphTokens.Silver;
        stripes[(int)QuestState.DoneThisCycle] = GlyphTokens.Silver;
        stripes[(int)QuestState.Blocked] = VeilText;
        stripes[(int)QuestState.Foreclosed] = GlyphTokens.Eclipse;
        stripes[(int)QuestState.Unknown] = VeilText;

        return new GlyphPalette(
            GlyphPaletteKind.Standard,
            light: false,
            "Standard",
            GlyphTokens.Shadow,
            keylineFraction: 0f,
            styles,
            haloTrack: GlyphTokens.VeilLine,
            haloTrackOnCard: GlyphTokens.Dusk with { W = 0.80f },
            haloArc: GlyphTokens.Moon,
            haloCompleteDim: GlyphTokens.MoonDim,
            haloStrokeMin: 2f,
            haloStrokeFraction: 0.18f,
            stripes,
            stripeWidth: 3f,
            new MarkColors(GlyphTokens.MoonDim, GlyphTokens.Dusk, GlyphTokens.Dusk, GlyphTokens.Moon, GlyphTokens.Dusk, 1f));
    }

    private static GlyphPalette BuildContrast(bool light)
    {
        var ground = light ? GlyphTokens.HighContrastPaper : GlyphTokens.NightSunken;
        var gold = light ? GlyphTokens.HighContrastInkGold : GlyphTokens.MoonHigh;
        var bright = light ? GlyphTokens.HighContrastInkNavy : GlyphTokens.SilverHigh;
        var dim = GlyphTokens.HighContrastDim;
        var alert = GlyphTokens.Eclipse;

        var styles = new GlyphStyle[StateCount()];
        Set(styles, new(QuestState.Completed, GlyphSilhouette.FullDisc, MoonPhase.Full, gold, gold, RimStyle.None, ContrastRim, GlyphMark.SolidDisc, gold));
        Set(styles, new(QuestState.Accepted, GlyphSilhouette.GibbousLit, MoonPhase.WaxingGibbous, gold, gold, RimStyle.Solid, ContrastRim, GlyphMark.LargeSeal, ground));
        Set(styles, new(QuestState.Ready, GlyphSilhouette.HalfLit, MoonPhase.FirstQuarter, gold, gold, RimStyle.Solid, ContrastRim, GlyphMark.Bar, gold));
        Set(styles, new(QuestState.ReadyOnOtherJob, GlyphSilhouette.HalfLit, MoonPhase.FirstQuarter, dim, dim, RimStyle.Solid, ContrastRim, GlyphMark.HollowBar, bright));
        Set(styles, new(QuestState.DoneThisCycle, GlyphSilhouette.GibbousLit, MoonPhase.WaningGibbous, dim, dim, RimStyle.Solid, ContrastRim, GlyphMark.Check, ground));
        Set(styles, new(QuestState.Blocked, GlyphSilhouette.EmptyRing, MoonPhase.New, ground, bright, RimStyle.Solid, ContrastThickRim, GlyphMark.ThickRim, bright));
        Set(styles, new(QuestState.Foreclosed, GlyphSilhouette.BarredRing, MoonPhase.New, ground, alert, RimStyle.Solid, ContrastRim, GlyphMark.ThickDiagonalBar, alert));
        Set(styles, new(QuestState.Unknown, GlyphSilhouette.DashedRing, MoonPhase.New, ground, dim, RimStyle.Dashed, ContrastRim, GlyphMark.Dashes, dim));

        var stripes = new Vector4[styles.Length];
        foreach (var style in styles)
        {
            stripes[(int)style.State] = style.Identity;
        }

        return new GlyphPalette(
            GlyphPaletteKind.HighContrast,
            light,
            light ? "High contrast (light host)" : "High contrast",
            ground,
            keylineFraction: 0.08f,
            styles,
            haloTrack: dim,
            haloTrackOnCard: dim,
            haloArc: gold,
            haloCompleteDim: dim,
            haloStrokeMin: 2.5f,
            haloStrokeFraction: 0.20f,
            stripes,
            stripeWidth: 4f,
            new MarkColors(gold, dim, dim, gold, dim, 1.35f));
    }

    private static int StateCount()
    {
        var max = 0;
        foreach (var state in Enum.GetValues<QuestState>())
        {
            max = Math.Max(max, (int)state);
        }

        return max + 1;
    }

    private static void Set(GlyphStyle[] styles, GlyphStyle style) => styles[(int)style.State] = style;
}
