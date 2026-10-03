using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// Colour tokens (spec §2.3, ui-revamp §4.3) and the style pushes built from them. Two kinds:
/// <list type="bullet">
/// <item>The <b>palette</b> (plan v7 T2, theme-system §8): every colour the chrome draws with, by role, from the
/// <see cref="UiPalette"/> in effect (<see cref="Palette"/>): the surface and text roles in <see cref="Surface"/>, gold as
/// text in <see cref="Accent"/>, the chrome inks (<see cref="Gold"/>, <see cref="Danger"/>, <see cref="DangerText"/> …),
/// the state inks (<see cref="StateColor"/>, <see cref="StateText"/>), the brass (<see cref="Brass"/>) and the scene
/// (<see cref="Scene"/>, <see cref="DropShadow"/>, <see cref="Glow"/>, <see cref="ShowStars"/>). It is Night, or with
/// <see cref="Configuration.FollowDalamudColours"/> on a palette mapped from the user's Dalamud style (game UX panel
/// finding 9). <see cref="Refresh"/> resolves it once per frame, before any window draws, and re-packs it only when it
/// changes.</item>
/// <item>The <b>fixed tokens</b> (Night … TideDeep, the glyph gradients): the glyphs' own colours, the same in every
/// palette. Only glyph code (the Classic moons, the glyph window) draws with them; <c>PaletteLintTests</c> keeps the
/// chrome on the palette.</item>
/// </list>
///
/// Gold discipline (game UX panel finding 2): Moon is for what the player can act on now (Ready, Accepted, the next
/// step, pins, the MSQ pill, primary buttons, progress) and the selected tree row's rule; chrome that is not a call to
/// action (sort arrows, selection outlines, focus rings, generic badges, active segments) uses Silver or VeilLine.
///
/// New UI from 1.11 on takes its gilt, moonstone and scene colours from the "Menphina's Medallion" palette,
/// <see cref="GlyphTokens.Medallion"/> (docs/design/moon-v6/round5/medallion-r5/concept.md), not from the old gold.
/// </summary>
public static class Theme
{
    /// <summary>#0F1424 – panel backgrounds for the detail pane and path view.</summary>
    public static readonly Vector4 Night = Rgb(GlyphTokens.NightHex);

    /// <summary>#F2D27A – Completed, Ready, gold accents, progress fill.</summary>
    public static readonly Vector4 Moon = Rgb(GlyphTokens.MoonHex);

    /// <summary>#DDE3F0 – primary text on Night, silver glyphs.</summary>
    public static readonly Vector4 Silver = Rgb(GlyphTokens.SilverHex);

    /// <summary>#7C86A8 – tertiary text, rings, separators (5.1 : 1 on Night; fails AA on raised surfaces, so never body text on cards).</summary>
    public static readonly Vector4 Dusk = Rgb(GlyphTokens.DuskHex);

    /// <summary>#B25C7F – Foreclosed, destructive actions.</summary>
    public static readonly Vector4 Eclipse = Rgb(GlyphTokens.EclipseHex);

    /// <summary>#4A5270 – disabled, unknown.</summary>
    public static readonly Vector4 Veil = Rgb(GlyphTokens.VeilHex);

    /// <summary>#3A4363 – the dark side of every state moon and the filling moon (glyph proposal v2.1 §3.4); always behind a rim.</summary>
    public static readonly Vector4 Shadow = Rgb(GlyphTokens.ShadowHex);

    /// <summary>#5C6584 – line tone that clears 3 : 1 on Night (3.19 : 1): the halo gauge track and similar thin strokes.</summary>
    public static readonly Vector4 VeilLine = Rgb(GlyphTokens.VeilLineHex);

    /// <summary>Dark disc of the unlit glyph states. Now <see cref="Shadow"/>; the name stays so callers compile.</summary>
    public static readonly Vector4 UnlitDisc = Shadow;

    /// <summary>#2C334A – the old unlit disc, kept as the maria, crater and vignette tint of the moons' interior detail only (glyph proposal §3.6).</summary>
    public static readonly Vector4 Umbra = Rgb(0x2C334A);

    /// <summary>#FFF0BE – the highlight stop of the gold gradient (glyph proposal §3.1); also the terminator glow on gold.</summary>
    public static readonly Vector4 MoonHigh = Rgb(GlyphTokens.MoonHighHex);

    /// <summary>#D6B25A – the deep stop of the gold gradient, at the limb away from the light.</summary>
    public static readonly Vector4 MoonDeep = Rgb(GlyphTokens.MoonDeepHex);

    /// <summary>#FFFFFF – the highlight stop of the silver gradient.</summary>
    public static readonly Vector4 SilverHigh = Rgb(GlyphTokens.SilverHighHex);

    /// <summary>#B9C2D8 – the deep stop of the silver gradient.</summary>
    public static readonly Vector4 SilverDeep = Rgb(GlyphTokens.SilverDeepHex);

    /// <summary>
    /// Moon dimmed toward Dusk, for the name and count of a completed tree node: still reads as gold, but quieter than
    /// a Ready row's glyph so finished chapters recede (ui-revamp §2.3, T11).
    /// </summary>
    public static readonly Vector4 MoonDim = Vector4.Lerp(Moon, Dusk, 0.35f);

    /// <summary>A panel one step above Night (a quarter of the way to Veil, #1E2437), for cards and header cards on the Night background.</summary>
    public static readonly Vector4 NightRaised = GlyphTokens.NightRaised;

    /// <summary>#0B0F1C – wells below the window: search pill, gauge wells, level pills, the title bar (ui-revamp §4.3).</summary>
    public static readonly Vector4 NightSunken = Rgb(GlyphTokens.NightSunkenHex);

    /// <summary>#262D45 – hover fill for rows, tabs and buttons.</summary>
    public static readonly Vector4 NightHover = Rgb(GlyphTokens.NightHoverHex);

    /// <summary>#2A3149 – hairlines, card borders, row separators (subtle; <see cref="VeilLine"/> where 3 : 1 is needed).</summary>
    public static readonly Vector4 NightLine = Rgb(GlyphTokens.NightLineHex);

    /// <summary>#A9B2CC – secondary text: hints, captions, chip labels (8.7 : 1 on Night, 7.3 on NightRaised). Dusk stays tertiary.</summary>
    public static readonly Vector4 Mist = Rgb(GlyphTokens.MistHex);

    /// <summary>#8A93B0 – the Unknown state's text and dashed ring (Veil itself fails AA for text).</summary>
    public static readonly Vector4 VeilText = Rgb(GlyphTokens.VeilTextHex);

    /// <summary>#D68AA8 – Foreclosed text and state-pill text (Eclipse fails 4.5 : 1 for text).</summary>
    public static readonly Vector4 EclipseText = Rgb(GlyphTokens.EclipseTextHex);

    // ---- Moon Road tokens (moon-road proposal §3). The fixed Night values; what to draw with this frame is in Surface
    // (Deep, Top, Ornament, OrnamentHigh, Cool, CoolDeep), which follows a Dalamud style and the high-contrast palette.

    /// <summary>#080B16 – the deepest surface: the rail, the letterbox behind the hero banner, the strip under the status bar.</summary>
    public static readonly Vector4 Abyss = Rgb(GlyphTokens.AbyssHex);

    /// <summary>#151C33 – the top stop of each pane's gradient (<see cref="Ornament.PaneGradient"/>).</summary>
    public static readonly Vector4 NightTop = Rgb(GlyphTokens.NightTopHex);

    /// <summary>#A88B52 – ornament hairlines (5.7 : 1 on Night), usually at alpha 0.55–0.8. Never text, never the only carrier of meaning.</summary>
    public static readonly Vector4 Gilt = Rgb(GlyphTokens.GiltHex);

    /// <summary>#D9BE82 – ornament highlight points under 4 px.</summary>
    public static readonly Vector4 GiltHigh = Rgb(GlyphTokens.GiltHighHex);

    /// <summary>#6F8FD0 – the cool accent: AA for text on Night (5.7 : 1) and NightRaised (4.8 : 1). Never "act now".</summary>
    public static readonly Vector4 Tide = Rgb(GlyphTokens.TideHex);

    /// <summary>#24345C – the bottom stop of the drawn night sky and Flight's water. Surface only (1.5 : 1 on Night).</summary>
    public static readonly Vector4 TideDeep = Rgb(GlyphTokens.TideDeepHex);

    /// <summary>
    /// Alternate table row tint for zebra striping: the palette's own (<see cref="UiPalette.Zebra"/>, Snow's navy at .03),
    /// else its disabled tone (Veil on Night) at low alpha.
    /// </summary>
    public static Vector4 ZebraRow => Palette.Zebra ?? Surface.TextDisabled with { W = 0.16f };

    public static readonly uint NightU32 = Pack(Night);
    public static readonly uint MoonU32 = Pack(Moon);
    public static readonly uint SilverU32 = Pack(Silver);
    public static readonly uint DuskU32 = Pack(Dusk);
    public static readonly uint EclipseU32 = Pack(Eclipse);
    public static readonly uint VeilU32 = Pack(Veil);
    public static readonly uint ShadowU32 = Pack(Shadow);
    public static readonly uint VeilLineU32 = Pack(VeilLine);
    public static readonly uint UnlitDiscU32 = Pack(UnlitDisc);
    public static readonly uint UmbraU32 = Pack(Umbra);
    public static readonly uint MoonHighU32 = Pack(MoonHigh);
    public static readonly uint MoonDeepU32 = Pack(MoonDeep);
    public static readonly uint SilverHighU32 = Pack(SilverHigh);
    public static readonly uint SilverDeepU32 = Pack(SilverDeep);
    public static readonly uint MoonDimU32 = Pack(MoonDim);
    public static readonly uint NightRaisedU32 = Pack(NightRaised);
    public static readonly uint NightSunkenU32 = Pack(NightSunken);
    public static readonly uint NightHoverU32 = Pack(NightHover);
    public static readonly uint NightLineU32 = Pack(NightLine);
    public static readonly uint MistU32 = Pack(Mist);
    public static readonly uint VeilTextU32 = Pack(VeilText);
    public static readonly uint EclipseTextU32 = Pack(EclipseText);
    public static readonly uint AbyssU32 = Pack(Abyss);
    public static readonly uint NightTopU32 = Pack(NightTop);
    public static readonly uint GiltU32 = Pack(Gilt);
    public static readonly uint GiltHighU32 = Pack(GiltHigh);
    public static readonly uint TideU32 = Pack(Tide);
    public static readonly uint TideDeepU32 = Pack(TideDeep);

    /// <summary>The Night palette as surface roles: what <see cref="Surface"/> is unless the user follows Dalamud's colours.</summary>
    public static readonly SurfaceColors NightSurface = SurfaceColors.Night;

    /// <summary>
    /// The palette in effect this frame (see <see cref="Refresh"/>): the chosen palette, or its high-contrast form under
    /// the high-contrast glyph palette. Every palette-driven token below is resolved from it.
    /// </summary>
    public static UiPalette Palette { get; private set; } = UiPalettes.Night;

    /// <summary>
    /// The surface and text roles in effect this frame (see <see cref="Refresh"/>), with the Moon Road roles: under the
    /// high-contrast palette those are its own versions (<see cref="SurfaceColors.ForHighContrast"/>).
    /// </summary>
    public static SurfaceColors Surface { get; private set; } = NightSurface;

    /// <summary>Whether <see cref="Surface"/> is mapped from the user's Dalamud style this frame.</summary>
    public static bool FollowingDalamud { get; private set; }

    /// <summary>Whether the palette in effect is light (dark ink on a light ground): <see cref="UiPalette.IsLight"/>.</summary>
    public static bool IsLight => Surface.Light;

    /// <summary>The palette's scene this frame: sky, stars, shadows, glows, halos and the night grades.</summary>
    public static SceneTokens Scene { get; private set; } = UiPalettes.Night.Scene;

    /// <summary>The palette's Decoration brass this frame: the card frame's ramp and the corner marks.</summary>
    public static BrassTokens Brass { get; private set; } = UiPalettes.Night.Brass;

    // ---- The chrome inks (UiPalette.Inks), resolved once per palette change. Night values in brackets.

    /// <summary>Gold fills, rules, outlines, progress and the "act now" marks (Moon). Text in gold uses <see cref="Accent"/>.</summary>
    public static Vector4 Gold { get; private set; } = Moon;

    /// <summary><see cref="Gold"/> packed.</summary>
    public static uint GoldU32 { get; private set; } = MoonU32;

    /// <summary>Gold's highlight: sheens, glints, the selected bead (MoonHigh).</summary>
    public static Vector4 GoldHigh { get; private set; } = MoonHigh;

    /// <summary><see cref="GoldHigh"/> packed.</summary>
    public static uint GoldHighU32 { get; private set; } = MoonHighU32;

    /// <summary>Gold's deep stop: a pressed gold pill, a ring's shade (MoonDeep).</summary>
    public static Vector4 GoldDeep { get; private set; } = MoonDeep;

    /// <summary><see cref="GoldDeep"/> packed.</summary>
    public static uint GoldDeepU32 { get; private set; } = MoonDeepU32;

    /// <summary>The quieter gold of finished things as a fill or stroke (MoonDim); as text, <see cref="AccentDim"/>.</summary>
    public static Vector4 GoldDim { get; private set; } = MoonDim;

    /// <summary><see cref="GoldDim"/> packed.</summary>
    public static uint GoldDimU32 { get; private set; } = MoonDimU32;

    /// <summary>Gold as a hairline: the Ready road, the selected row's rules, the drawer's set mark (Moon).</summary>
    public static Vector4 GoldLine { get; private set; } = Moon;

    /// <summary>The gold an "on" toggle's track is mixed toward (Moon), and its crescent knob (MoonHigh).</summary>
    public static Vector4 ToggleOn { get; private set; } = Moon;

    public static Vector4 ToggleKnob { get; private set; } = MoonHigh;

    /// <summary>
    /// Text in the ornament: Full's Section headings, the sorted column header, the drawer's heads (the medallion's
    /// GiltLight on Night; <see cref="UiPalette.OrnamentLight"/>).
    /// </summary>
    public static Vector4 OrnamentLight { get; private set; } = UiPalettes.Night.OrnamentLight;

    /// <summary>The giver portrait plate's well, keylines and fallback ink this frame.</summary>
    public static PlateTokens Plate { get; private set; } = UiPalettes.Night.Plate;

    /// <summary>Text and marks on a gold fill (Night).</summary>
    public static Vector4 OnGold { get; private set; } = Night;

    /// <summary><see cref="OnGold"/> packed.</summary>
    public static uint OnGoldU32 { get; private set; } = NightU32;

    /// <summary>Destructive actions and Locked out as a fill or rule (Eclipse); never text.</summary>
    public static Vector4 Danger { get; private set; } = Eclipse;

    /// <summary><see cref="Danger"/> packed.</summary>
    public static uint DangerU32 { get; private set; } = EclipseU32;

    /// <summary>Locked out and error text (EclipseText).</summary>
    public static Vector4 DangerText { get; private set; } = EclipseText;

    /// <summary><see cref="DangerText"/> packed.</summary>
    public static uint DangerTextU32 { get; private set; } = EclipseTextU32;

    /// <summary>Text on a destructive button (Silver).</summary>
    public static Vector4 OnDanger { get; private set; } = Silver;

    /// <summary>Not checked text and captions (VeilText).</summary>
    public static Vector4 UnknownText { get; private set; } = VeilText;

    /// <summary><see cref="UnknownText"/> packed.</summary>
    public static uint UnknownTextU32 { get; private set; } = VeilTextU32;

    /// <summary>A tree node's progress ring: the arc and a finished node's ring.</summary>
    public static Vector4 GaugeArc { get; private set; } = UiPalettes.NightInks.GaugeArc;

    public static Vector4 GaugeDone { get; private set; } = UiPalettes.NightInks.GaugeDone;

    /// <summary>
    /// A drop shadow at its designed <paramref name="alpha"/> (the alpha for a dark window), packed: the scene's shadow
    /// colour (Abyss on Night) at that alpha times <see cref="SceneTokens.ShadowStrength"/>, so a light palette casts a
    /// softer one.
    /// </summary>
    public static uint DropShadow(float alpha) => WithAlpha(Scene.Shadow, alpha * Scene.ShadowStrength);

    /// <summary>
    /// A soft cast shadow (cards, the drawer) at its designed <paramref name="alpha"/>, packed: the scene's ShadowInk
    /// (black on Night, navy on a light palette) at that alpha times <see cref="SceneTokens.ShadowStrength"/>.
    /// </summary>
    public static uint CastShadow(float alpha) => WithAlpha(Scene.ShadowInk, alpha * Scene.ShadowStrength);

    /// <summary>
    /// A gold glow or bloom at its designed <paramref name="alpha"/>, packed: <see cref="Gold"/> at that alpha times
    /// <see cref="SceneTokens.GlowStrength"/>; on a light palette that washes instead of glowing
    /// (<see cref="SceneTokens.WashInsteadOfGlow"/>), the scene's warm wash colour.
    /// </summary>
    public static uint Glow(float alpha) => WithAlpha(Scene.WashInsteadOfGlow ? Scene.GlowWash : Gold, alpha * Scene.GlowStrength);

    /// <summary>
    /// The 1 px lit edge inside the top of a raised surface at its designed <paramref name="alpha"/>, packed: MoonHigh on
    /// Night; on a light palette white at the scene's own alpha (<see cref="SceneTokens.TopHighlightAlpha"/>), near opaque.
    /// </summary>
    public static uint TopHighlight(float alpha) => WithAlpha(Scene.TopHighlight, Scene.TopHighlightAlpha > 0f ? Scene.TopHighlightAlpha : alpha);

    /// <summary>
    /// Whether a light palette lays warm washes where Night glows (<see cref="SceneTokens.WashInsteadOfGlow"/>; spec-1.16
    /// §A4): the Ready row's medal wash, the hero halo, the selected rows. Nothing else glows there.
    /// </summary>
    public static bool Washes => Scene.WashInsteadOfGlow;

    /// <summary>The palette's gauge inks this frame (<see cref="UiPalette.Gauges"/>): the medal's material on Night, their own on a light palette.</summary>
    public static GaugeInks Gauges { get; private set; } = UiPalettes.Night.Gauges;

    /// <summary>A highlight sheen or glint at its designed <paramref name="alpha"/>, packed: <see cref="GoldHigh"/> at that alpha times <see cref="SceneTokens.GlowStrength"/>.</summary>
    public static uint Sheen(float alpha) => WithAlpha(GoldHigh, alpha * Scene.GlowStrength);

    /// <summary>The halo behind text drawn over brass or sky, at <paramref name="alpha"/>, packed (Abyss on Night; light on a light palette).</summary>
    public static uint Halo(float alpha) => WithAlpha(Scene.TextHalo, alpha);

    /// <summary><see cref="SurfaceColors.Deep"/> this frame, packed (Abyss on Night).</summary>
    public static uint DeepU32 { get; private set; } = AbyssU32;

    /// <summary><see cref="SurfaceColors.Top"/> this frame, packed (NightTop on Night; the window under high contrast).</summary>
    public static uint TopU32 { get; private set; } = NightTopU32;

    /// <summary><see cref="SurfaceColors.Ornament"/> this frame, opaque and packed (Gilt on Night; VeilLine under high contrast).</summary>
    public static uint OrnamentU32 { get; private set; } = GiltU32;

    /// <summary><see cref="SurfaceColors.OrnamentHigh"/> this frame, packed (GiltHigh on Night).</summary>
    public static uint OrnamentHighU32 { get; private set; } = GiltHighU32;

    /// <summary><see cref="SurfaceColors.Cool"/> this frame, packed (Tide on Night): usable as text.</summary>
    public static uint CoolU32 { get; private set; } = TideU32;

    /// <summary>
    /// Settings › Display › Flair as drawn this frame (<see cref="FlairRules.Effective"/>): the setting, capped at Quiet
    /// under the high-contrast palette. Ask <see cref="FlairRules"/> what a level draws, or use the shortcuts below.
    /// </summary>
    public static Flair Flair { get; private set; } = Flair.Full;

    /// <summary>
    /// Settings › Display › Flair as chosen, before the high-contrast cap: for the art that high contrast keeps with a
    /// solid band behind its text instead of dropping it (Flight's zone banner, like the detail hero).
    /// </summary>
    public static Flair FlairSetting { get; private set; } = Flair.Full;

    /// <summary>
    /// Settings › Display › Look › Moon style this frame (feature plan v6 G3): whether <see cref="MoonGlyph"/>, the orbit
    /// rings and the quest table draw the 1.12 medals and gauges or the 1.11 moons (<see cref="LegacyMoonGlyph"/>). Since
    /// 1.16.0 it is the appearance in effect (<see cref="Themes.GlyphSeam.Appearance"/>): Classic for the Classic theme,
    /// Medallion for every other, so a pushed preview switches it too.
    /// </summary>
    public static MoonStyle MoonStyle => Themes.GlyphSeam.Appearance.MoonStyle;

    /// <summary>Whether the 1.11 moons draw this frame (<see cref="MoonStyle.Classic"/>).</summary>
    public static bool ClassicMoons => MoonStyle == MoonStyle.Classic;

    /// <summary>Whether the pane gradient draws this frame (Full only).</summary>
    public static bool ShowPaneGradient => FlairRules.PaneGradient(Flair);

    /// <summary>
    /// How rules and dividers draw this frame (<see cref="FlairRules.Rule"/>): the Moon Road's brass at Full, a flat
    /// hairline at Quiet, the palette's line at Plain.
    /// </summary>
    public static RuleStyle RuleStyle => FlairRules.Rule(Flair);

    /// <summary>
    /// Whether the panes are laid out in headed sections with a rule under each heading (Full and Quiet), rather than the
    /// plain lines of the ledger (Plain). The rule itself follows <see cref="RuleStyle"/>.
    /// </summary>
    public static bool Sectioned => RuleStyle != RuleStyle.Line;

    /// <summary>Whether the Moon Road's brass art draws this frame: sigils, fading brass rules, roads, moon-road dividers (Full).</summary>
    public static bool MoonRoadArt => RuleStyle == RuleStyle.MoonRoad;

    /// <summary>Whether corner marks draw this frame (Full only).</summary>
    public static bool ShowCornerMarks => FlairRules.CornerMarks(Flair);

    /// <summary>Whether glows draw this frame (Full only).</summary>
    public static bool ShowGlow => FlairRules.Glow(Flair);

    /// <summary>Whether the faint star field draws in empty sky this frame (Full only).</summary>
    /// <remarks>And only on a palette with a star field (<see cref="SceneTokens.StarField"/>): a light palette has none.</remarks>
    public static bool ShowStars => FlairRules.StarField(Flair) && Scene.StarField;

    /// <summary>Whether the Moon Road's own motion plays this frame (Full, and Reduce motion off).</summary>
    public static bool FlairMotion => FlairRules.Motion(Flair, UiMetrics.ReduceMotion);

    /// <summary>Whether hover fades, gauge fills and the reveal pulse play this frame (Full and Quiet, Reduce motion off).</summary>
    public static bool UiMotion => FlairRules.UiMotion(Flair, UiMetrics.ReduceMotion);

    /// <summary>The medals' finish this frame (<see cref="FlairRules.Medal"/>): gilt, light rim, plain, or the Classic moons.</summary>
    public static MedalFinish MedalFinish => FlairRules.Medal(Flair, MoonStyle);

    /// <summary>The detail pane's hero this frame (<see cref="FlairRules.Hero"/>).</summary>
    public static HeroStyle HeroStyle => FlairRules.Hero(Flair);

    /// <summary>The quest table's header this frame (<see cref="FlairRules.TableHeader"/>).</summary>
    public static TableHeaderStyle TableHeader => FlairRules.TableHeader(Flair);

    /// <summary>The level's spacing tokens this frame (<see cref="FlairRules.Spacing"/>), logical px.</summary>
    public static FlairSpacing Spacing => FlairRules.Spacing(Flair);

    /// <summary>The level's surfaces this frame (<see cref="FlairTones.For"/>): Quiet's tonal panes, Plain's ledger bands.</summary>
    public static FlairTones Tones { get; private set; } = FlairTones.For(Flair.Full, UiPalettes.Night);

    /// <summary>
    /// A structure line's colour this frame: Quiet's hairline or Plain's line, and under the high-contrast palette the
    /// strong line, so a boundary never rests on tone alone.
    /// </summary>
    public static Vector4 RuleColor => Glyphs.HighContrast ? Surface.StrongLine : Tones.Rule;

    /// <summary>
    /// Draws as Decoration <paramref name="flair"/> until the returned scope is disposed, then restores the frame's level:
    /// the live preview in Settings. The high-contrast cap still applies. A struct; <c>using</c> allocates nothing.
    /// </summary>
    public static FlairScope PushFlair(Flair flair)
    {
        var previous = Flair;
        Flair = FlairRules.Effective(flair, Glyphs.HighContrast);
        Tones = FlairTones.For(Flair, Palette);
        return new FlairScope(previous);
    }

    /// <summary>Restores the level that was in effect before <see cref="PushFlair"/>. Dispose exactly once.</summary>
    public readonly struct FlairScope(Flair previous) : IDisposable
    {
        public void Dispose()
        {
            Flair = previous;
            Tones = FlairTones.For(previous, Palette);
        }
    }

    /// <summary>
    /// An ornament's alpha as drawn this frame: <paramref name="designed"/> (the proposal's 0.55–0.8), or 1 under the
    /// high-contrast palette, whose ornament lines are opaque VeilLine so the structure stays and the shimmer goes (§10.2).
    /// </summary>
    public static float OrnamentAlpha(float designed) => Glyphs.HighContrast ? 1f : designed;

    /// <summary>The host style's window background alpha as of the last <see cref="Refresh"/> (the user's Dalamud opacity).</summary>
    public static float WindowAlpha => hostWindowAlpha;

    /// <summary>
    /// The glyph palette in effect this frame (Settings › Display › Glyph palette), resolved against the palette's window
    /// colour: Standard, or the high-contrast variant for a dark or a light host (<see cref="GlyphPalette.Resolve"/>).
    /// <see cref="MoonGlyph"/>, <see cref="Marks"/> and the table stripe paint from it.
    /// </summary>
    public static GlyphPalette Glyphs { get; private set; } = GlyphPalette.Standard;

    /// <summary>
    /// Gold for text and small ink (pill labels, the active tab icon, gold headings): the palette's
    /// <see cref="UiPalette.Accent"/>, Moon on Night; under "Follow Dalamud colours" Moon pushed towards the palette's
    /// text colour until it reads at 4.5 : 1 on the window, so a light Dalamud style gets a deep gold instead of Moon's
    /// 1.4 : 1. Fills, rims and glows use <see cref="Gold"/>; the glyphs keep Moon.
    /// </summary>
    public static Vector4 Accent { get; private set; } = Moon;

    /// <summary><see cref="Accent"/> packed for ImDrawList calls.</summary>
    public static uint AccentU32 { get; private set; } = MoonU32;

    /// <summary><see cref="MoonDim"/> as text, made to read on the palette the way <see cref="Accent"/> is.</summary>
    public static Vector4 AccentDim { get; private set; } = MoonDim;

    /// <summary>The host style's window background alpha as of the last <see cref="Refresh"/>; <see cref="PushNightWindow"/> keeps it.</summary>
    private static float hostWindowAlpha = 1f;

    private static readonly Vector4 Transparent = Vector4.Zero;

    /// <summary>
    /// Picks this frame's palette. Call once per frame before any window draws (nothing is pushed then, so the style
    /// read is the user's own): the palette the appearance names (<paramref name="palette"/>, from
    /// <see cref="Themes.GlyphSeam.Refresh"/>, called first) out of the registry (<see cref="UiPalettes.Get(PaletteId)"/>),
    /// or with <paramref name="followDalamud"/> (or <see cref="PaletteId.FollowDalamud"/>) the Follow Dalamud palette,
    /// mapped from the Dalamud style (<see cref="UiPalettes.FollowDalamud"/>, rebuilt only when the host colours change).
    /// The glyph palette <paramref name="glyphPalette"/> is resolved against the palette's window colour
    /// (<see cref="Glyphs"/>), so the high-contrast glyphs switch to their light variant on a light palette. Under high
    /// contrast the palette's high-contrast form is drawn (<see cref="UiPalette.HighContrast"/>) and <paramref name="flair"/>
    /// is capped at Quiet (<see cref="Flair"/>). The palette's tokens are re-packed only when the palette changes, so a
    /// frame where nothing changed allocates nothing.
    /// </summary>
    public static void Refresh(bool followDalamud, GlyphPaletteKind glyphPalette = GlyphPaletteKind.Standard, Flair flair = Flair.Full, PaletteId palette = PaletteId.Night)
    {
        var colors = ImGui.GetStyle().Colors;
        var windowBg = colors[(int)ImGuiCol.WindowBg];
        hostWindowAlpha = float.IsFinite(windowBg.W) ? Math.Clamp(windowBg.W, 0f, 1f) : 1f;
        FollowingDalamud = followDalamud;
        var chosen = followDalamud
            ? FollowDalamudPalette(
                windowBg,
                colors[(int)ImGuiCol.FrameBg],
                colors[(int)ImGuiCol.FrameBgHovered],
                colors[(int)ImGuiCol.Border],
                colors[(int)ImGuiCol.Text],
                colors[(int)ImGuiCol.TextDisabled])
            : UiPalettes.Get(palette);
        Glyphs = GlyphPalette.Resolve(glyphPalette, chosen.Surface.Window);
        ApplyPalette(Glyphs.HighContrast ? chosen.HighContrast : chosen);

        Flair = FlairRules.Effective(flair, Glyphs.HighContrast);
        FlairSetting = FlairRules.Effective(flair, highContrast: false);
        Tones = FlairTones.For(Flair, Palette);
    }

    /// <summary>
    /// Makes <paramref name="palette"/> the palette in effect: resolves and packs every palette-driven token once. Does
    /// nothing when it already is (the usual frame), so a frame allocates and packs nothing.
    /// </summary>
    private static void ApplyPalette(UiPalette palette)
    {
        if (ReferenceEquals(palette, Palette))
        {
            return;
        }

        Palette = palette;
        var s = Surface = palette.Surface;
        Scene = palette.Scene;
        Brass = palette.Brass;
        Accent = palette.Accent;
        AccentU32 = Pack(Accent);
        AccentDim = palette.AccentDim;
        var inks = palette.Inks;
        Gold = inks.Gold;
        GoldU32 = Pack(Gold);
        GoldHigh = inks.GoldHigh;
        GoldHighU32 = Pack(GoldHigh);
        GoldDeep = inks.GoldDeep;
        GoldDeepU32 = Pack(GoldDeep);
        GoldDim = inks.GoldDim;
        GoldDimU32 = Pack(GoldDim);
        OnGold = inks.OnGold;
        OnGoldU32 = Pack(OnGold);
        Danger = inks.Danger;
        DangerU32 = Pack(Danger);
        DangerText = inks.DangerText;
        DangerTextU32 = Pack(DangerText);
        OnDanger = inks.OnDanger;
        UnknownText = inks.UnknownText;
        UnknownTextU32 = Pack(UnknownText);
        GoldLine = inks.GoldLine;
        ToggleOn = inks.ToggleOn;
        ToggleKnob = inks.ToggleKnob;
        OrnamentLight = palette.OrnamentLight;
        Plate = palette.Plate;
        Gauges = palette.Gauges;
        GaugeArc = inks.GaugeArc;
        GaugeDone = inks.GaugeDone;
        DeepU32 = Pack(s.Deep);
        TopU32 = Pack(s.Top);
        OrnamentU32 = Pack(s.Ornament);
        OrnamentHighU32 = Pack(s.OrnamentHigh);
        CoolU32 = Pack(s.Cool);
    }

    // The Follow Dalamud palette and the host colours it was built from: rebuilt only when the user's style changes.
    private static UiPalette? followPalette;
    private static Vector4 followWindow, followFrame, followFrameHovered, followBorder, followText, followTextDisabled;

    private static UiPalette FollowDalamudPalette(Vector4 windowBg, Vector4 frameBg, Vector4 frameBgHovered, Vector4 border, Vector4 text, Vector4 textDisabled)
    {
        if (followPalette is { } cached
            && windowBg == followWindow && frameBg == followFrame && frameBgHovered == followFrameHovered
            && border == followBorder && text == followText && textDisabled == followTextDisabled)
        {
            return cached;
        }

        (followWindow, followFrame, followFrameHovered, followBorder, followText, followTextDisabled) = (windowBg, frameBg, frameBgHovered, border, text, textDisabled);
        return followPalette = UiPalettes.FollowDalamud(windowBg, frameBg, frameBgHovered, border, text, textDisabled);
    }

    /// <summary>
    /// Marks the pane backdrop as painted by the caller (the main window paints Night and the pane gradient behind a
    /// column) until the returned scope is disposed: meanwhile <see cref="PushNightPanel"/> leaves the child background
    /// clear, so the gradient shows through the detail pane and its cards. A struct; <c>using</c> allocates nothing.
    /// </summary>
    public static BackdropScope PushPaneBackdrop(bool condition = true)
    {
        if (condition)
        {
            backdropDepth++;
        }

        return new BackdropScope(condition);
    }

    /// <summary>Ends a <see cref="PushPaneBackdrop"/>. Dispose exactly once.</summary>
    public readonly struct BackdropScope(bool active) : IDisposable
    {
        public void Dispose()
        {
            if (active && backdropDepth > 0)
            {
                backdropDepth--;
            }
        }
    }

    private static int backdropDepth;

    /// <summary>
    /// Draws with <paramref name="palette"/> until the returned scope is disposed, then restores the frame's palette:
    /// the glyph window's side-by-side comparison. A struct; <c>using</c> allocates nothing.
    /// </summary>
    public static GlyphScope PushGlyphs(GlyphPalette palette)
    {
        var previous = Glyphs;
        Glyphs = palette;
        return new GlyphScope(previous);
    }

    /// <summary>
    /// Draws as <paramref name="palette"/> with the glyph palette <paramref name="glyphPalette"/> until the returned scope
    /// is disposed, then restores the frame's: the Themes page's cards and Preview, so a previewed palette's moons, gauges,
    /// washes and high-contrast ladder are the ones it will really draw. Resolved as <see cref="Refresh"/> resolves them
    /// (the glyph palette against <paramref name="palette"/>'s window, its high-contrast form under high contrast); Flair
    /// stays the frame's. A struct; <c>using</c> allocates nothing.
    /// </summary>
    public static PaletteScope PushPalette(UiPalette palette, GlyphPaletteKind glyphPalette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var scope = new PaletteScope(Palette, Glyphs);
        Glyphs = GlyphPalette.Resolve(glyphPalette, palette.Surface.Window);
        ApplyPalette(Glyphs.HighContrast ? palette.HighContrast : palette);
        return scope;
    }

    /// <summary>Restores the palette and glyph palette in effect before <see cref="PushPalette"/>. Dispose exactly once.</summary>
    public readonly struct PaletteScope(UiPalette previous, GlyphPalette previousGlyphs) : IDisposable
    {
        public void Dispose()
        {
            if (previous is not null)
            {
                ApplyPalette(previous);
                Glyphs = previousGlyphs;
            }
        }
    }

    /// <summary>Restores the glyph palette that was in effect before <see cref="PushGlyphs"/>. Dispose exactly once.</summary>
    public readonly struct GlyphScope(GlyphPalette previous) : IDisposable
    {
        public void Dispose()
        {
            if (previous is not null)
            {
                Glyphs = previous;
            }
        }
    }

    /// <summary>
    /// A state's tone in the palette (<see cref="StateInks.Tone"/>): a stripe or badge beside a glyph. On Night: gold for
    /// Completed, In journal and Ready; Silver for the other-job and Done states; Dusk for Blocked; Eclipse for Locked out;
    /// Veil for Not checked. Locked out's and Not checked's tones are not text; <see cref="StateText"/> is.
    /// </summary>
    public static Vector4 StateColor(QuestState state) => Palette.States.Tone(state);

    public static uint StateColorU32(QuestState state) => Pack(StateColor(state));

    /// <summary>A state's readable text ink in the palette (<see cref="StateInks.Text"/>): at least 4.5 : 1 on the window.</summary>
    public static Vector4 StateText(QuestState state) => Palette.States.Text(state);

    /// <summary>The token with a different alpha, packed for ImDrawList calls.</summary>
    public static uint WithAlpha(Vector4 color, float alpha) => Pack(color with { W = Math.Clamp(alpha, 0f, 1f) });

    public static Vector4 WithAlphaVector(Vector4 color, float alpha) => color with { W = Math.Clamp(alpha, 0f, 1f) };

    /// <summary>A colour packed for ImDrawList calls (IM_COL32, no style alpha applied); allocation-free.</summary>
    public static uint U32(Vector4 color) => Pack(color);

    /// <summary>The outline behind text drawn over game scenes (<c>Chrome.OutlinedText</c>): the palette's window colour, opaque.</summary>
    public static uint OutlineU32 => Pack(Surface.Window with { W = 1f });

    /// <summary>
    /// Night panel colours for the detail pane, path view and similar: child background, text, secondary text, borders
    /// and separators from <see cref="Surface"/> (Night unless following Dalamud's colours). Inside a
    /// <see cref="PushPaneBackdrop"/> the child background is clear: the backdrop is already painted. Dispose to pop.
    /// </summary>
    public static ImRaii.ColorDisposable PushNightPanel(bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.ChildBg, backdropDepth > 0 ? Transparent : Surface.Window, condition)
              .Push(ImGuiCol.Text, Surface.Text, condition)
              .Push(ImGuiCol.TextDisabled, Surface.TextSecondary, condition)
              .Push(ImGuiCol.Border, Surface.Line, condition)
              .Push(ImGuiCol.Separator, Surface.Line, condition)
              .Push(ImGuiCol.TableBorderLight, Surface.Line, condition)
              .Push(ImGuiCol.TableBorderStrong, Surface.StrongLine, condition);

    /// <summary>
    /// A count of pushed style colours and variables, popped on <see cref="Dispose"/>. A struct, so <c>using</c> on it
    /// allocates nothing; <c>default</c> pops nothing. Dispose exactly once.
    /// </summary>
    public readonly struct StyleScope(int colors, int vars) : IDisposable
    {
        public int Colors { get; } = colors;

        public int Vars { get; } = vars;

        public void Dispose()
        {
            if (Vars > 0)
            {
                ImGui.PopStyleVar(Vars);
            }

            if (Colors > 0)
            {
                ImGui.PopStyleColor(Colors);
            }
        }
    }

    /// <summary>
    /// Window-wide Night chrome, for <c>Window.PreDraw</c> (dispose the returned scope in <c>PostDraw</c>): window,
    /// title bar, child, frame, button, header, tab, table, scrollbar, separator and resize-grip colours plus text,
    /// check marks, slider grabs and the nav highlight, all from <see cref="Surface"/>. The window background keeps the
    /// host style's alpha and <c>Window.BgAlpha</c> is never touched, so the user's Dalamud opacity still applies
    /// (Dalamud panel §4).
    ///
    /// Popups and tooltips, decided explicitly: a colour pushed before Begin is still on the stack while Draw runs, so
    /// every popup, combo and tooltip begun inside the window inherits it. <c>PopupBg</c> is therefore pushed as well,
    /// so they read as Night as a whole rather than half-Night by accident; <see cref="Tooltip"/> (every
    /// <see cref="UiMetrics.Tooltip(string)"/>) and <see cref="PushPopup"/> restyle them explicitly on top, which is
    /// also what windows without this push (the Todo overlay's menus) get. The title bar and
    /// Dalamud's own title-bar buttons take <c>TitleBg*</c> from here. With <see cref="FollowingDalamud"/> nothing is
    /// pushed: the host style already is the palette.
    /// </summary>
    public static StyleScope PushNightWindow()
    {
        if (FollowingDalamud)
        {
            return default;
        }

        var s = Surface;
        var count = 0;
        Push(ImGuiCol.WindowBg, s.Window with { W = hostWindowAlpha }, ref count);
        Push(ImGuiCol.ChildBg, Transparent, ref count);
        Push(ImGuiCol.PopupBg, s.Window with { W = 0.97f }, ref count);
        Push(ImGuiCol.Border, s.Line, ref count);
        Push(ImGuiCol.BorderShadow, Transparent, ref count);
        Push(ImGuiCol.Text, s.Text, ref count);
        Push(ImGuiCol.TextDisabled, s.TextSecondary, ref count);
        Push(ImGuiCol.TextSelectedBg, WithAlphaVector(s.Text, 0.20f), ref count);
        Push(ImGuiCol.TitleBg, s.Sunken, ref count);
        Push(ImGuiCol.TitleBgActive, s.Raised, ref count);
        Push(ImGuiCol.TitleBgCollapsed, WithAlphaVector(s.Sunken, 0.75f), ref count);
        Push(ImGuiCol.MenuBarBg, s.Raised, ref count);
        Push(ImGuiCol.FrameBg, s.Sunken, ref count);
        Push(ImGuiCol.FrameBgHovered, s.Hover, ref count);
        Push(ImGuiCol.FrameBgActive, Vector4.Lerp(s.Hover, s.Text, 0.08f) with { W = 1f }, ref count);
        Push(ImGuiCol.Button, s.Raised, ref count);
        Push(ImGuiCol.ButtonHovered, s.Hover, ref count);
        Push(ImGuiCol.ButtonActive, Vector4.Lerp(s.Hover, s.Text, 0.12f) with { W = 1f }, ref count);
        // Selection is not a call to action: a neutral wash, never gold (the tree keeps its own gold rule).
        Push(ImGuiCol.Header, SelectionWash, ref count);
        Push(ImGuiCol.HeaderHovered, s.Hover, ref count);
        Push(ImGuiCol.HeaderActive, SelectionWashActive, ref count);
        Push(ImGuiCol.Tab, s.Sunken, ref count);
        Push(ImGuiCol.TabHovered, s.Hover, ref count);
        Push(ImGuiCol.TabActive, s.Raised, ref count);
        Push(ImGuiCol.TabUnfocused, s.Sunken, ref count);
        Push(ImGuiCol.TabUnfocusedActive, s.Raised, ref count);
        Push(ImGuiCol.TableHeaderBg, s.Raised, ref count);
        Push(ImGuiCol.TableBorderStrong, s.StrongLine, ref count);
        Push(ImGuiCol.TableBorderLight, s.Line, ref count);
        Push(ImGuiCol.TableRowBg, Transparent, ref count);
        Push(ImGuiCol.TableRowBgAlt, ZebraRow, ref count);
        Push(ImGuiCol.ScrollbarBg, WithAlphaVector(s.Sunken, 0.5f), ref count);
        Push(ImGuiCol.ScrollbarGrab, WithAlphaVector(s.StrongLine, 0.7f), ref count);
        Push(ImGuiCol.ScrollbarGrabHovered, s.StrongLine, ref count);
        Push(ImGuiCol.ScrollbarGrabActive, s.TextTertiary, ref count);
        Push(ImGuiCol.Separator, s.Line, ref count);
        Push(ImGuiCol.SeparatorHovered, s.StrongLine, ref count);
        Push(ImGuiCol.SeparatorActive, s.TextTertiary, ref count);
        Push(ImGuiCol.ResizeGrip, WithAlphaVector(s.StrongLine, 0.35f), ref count);
        Push(ImGuiCol.ResizeGripHovered, s.StrongLine, ref count);
        Push(ImGuiCol.ResizeGripActive, s.TextTertiary, ref count);
        Push(ImGuiCol.CheckMark, s.Text, ref count);
        Push(ImGuiCol.SliderGrab, s.StrongLine, ref count);
        Push(ImGuiCol.SliderGrabActive, s.Text, ref count);
        Push(ImGuiCol.NavHighlight, s.Text, ref count);
        return new StyleScope(count, 0);
    }

    /// <summary>
    /// A tooltip in the active palette and Decoration level (ui-revamp §3 "Tooltip", docs/design/flair-v13 §1
    /// "Tooltips"): primary text and the secondary tone for <c>TextDisabled</c> lines, in one of three frames. Full: the
    /// raised tone at 0.97, radius 4, padding 12 × 10, and the cards' brass frame with corner marks at the top left and
    /// bottom right (<see cref="TooltipScope"/> draws it just before the tooltip ends). Quiet: flat, a 1 px neutral
    /// border, radius 6. Plain: a plain box, radius 2, padding 7 × 5. Begins the tooltip itself; its contents follow, and
    /// disposing the scope ends it (<see cref="UiMetrics.Tooltip(string)"/> does all three). The tooltip fades in
    /// (<see cref="PopupFade"/>), once per item rather than on every frame the pointer moves within it.
    /// </summary>
    public static TooltipScope Tooltip()
    {
        PopupFade.NoteTooltip();
        var s = Surface;
        var style = FlairRules.Tooltip(Flair);
        var (fill, border, rounding, padding) = style switch
        {
            TooltipStyle.Brass => (s.Raised with { W = 0.97f }, Transparent, 4f, new Vector2(12f, 10f)),
            TooltipStyle.Plain => (Tones.HeaderBand with { W = 0.98f }, Glyphs.HighContrast ? s.StrongLine : Tones.HeaderLine, 2f, new Vector2(7f, 5f)),
            _ => (Vector4.Lerp(s.Window, s.Raised, 0.6f) with { W = 0.97f }, Glyphs.HighContrast ? s.StrongLine : s.Line, 6f, new Vector2(10f, 8f)),
        };

        var count = 0;
        Push(ImGuiCol.PopupBg, fill, ref count);
        Push(ImGuiCol.Border, border, ref count);
        Push(ImGuiCol.Text, s.Text, ref count);
        Push(ImGuiCol.TextDisabled, s.TextSecondary, ref count);
        Push(ImGuiCol.Separator, s.Line, ref count);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, UiMetrics.Px(rounding));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(padding.X), UiMetrics.Px(padding.Y)));
        ImGui.BeginTooltip();
        return new TooltipScope(count, 3, style == TooltipStyle.Brass ? UiMetrics.Px(rounding) : -1f);
    }

    /// <summary>
    /// The scope <see cref="Tooltip"/> returns. On dispose, at Full, it lays the brass frame over the tooltip's edge
    /// (the cards' lit brass with corner marks at the top left and bottom right) into the tooltip's own draw list, so
    /// the frame fades in with the tooltip (<see cref="PopupFade"/>); not while the window is hidden or still sizing
    /// itself to its contents, when its rectangle is not yet the one shown. Then it ends the tooltip and pops the style.
    /// A struct; <c>using</c> allocates nothing. Dispose exactly once.
    /// </summary>
    public readonly struct TooltipScope(int colors, int vars, float brassRounding) : IDisposable
    {
        public void Dispose()
        {
            if (brassRounding >= 0f)
            {
                DrawBrass();
            }

            ImGui.EndTooltip();
            new StyleScope(colors, vars).Dispose();
        }

        private void DrawBrass()
        {
            var window = ImGuiP.GetCurrentWindow();
            if (window.IsNull || window.Hidden || window.HiddenFramesCannotSkipItems > 0 || window.AutoFitFramesX > 0 || window.AutoFitFramesY > 0)
            {
                return;
            }

            // The frame straddles the window's edge, past its clip rectangle: draw with the whole screen as the clip.
            var min = window.Pos;
            var max = min + window.Size;
            var dl = ImGui.GetWindowDrawList();
            dl.PushClipRectFullScreen();
            Ornament.BrassBorder(dl, min, max, brassRounding, UiMetrics.Hairline);
            Ornament.CornerMarks(dl, min, max, MathF.Round(UiMetrics.Px(7f)), twoOnly: true);
            dl.PopClipRect();
        }
    }

    /// <summary>
    /// A popup or context menu in the active palette: the tooltip's surface plus menu-item, frame, button and check
    /// colours, rounding 6. Push before <c>BeginPopup…</c>, dispose after it ends; the items inside draw with it.
    /// </summary>
    public static StyleScope PushPopup()
    {
        var s = Surface;
        var count = 0;
        Push(ImGuiCol.PopupBg, s.Window with { W = 0.98f }, ref count);
        Push(ImGuiCol.Border, s.Line, ref count);
        Push(ImGuiCol.Text, s.Text, ref count);
        Push(ImGuiCol.TextDisabled, s.TextSecondary, ref count);
        Push(ImGuiCol.Separator, s.Line, ref count);
        Push(ImGuiCol.Header, SelectionWash, ref count);
        Push(ImGuiCol.HeaderHovered, s.Hover, ref count);
        Push(ImGuiCol.HeaderActive, SelectionWashActive, ref count);
        Push(ImGuiCol.FrameBg, s.Sunken, ref count);
        Push(ImGuiCol.FrameBgHovered, s.Hover, ref count);
        Push(ImGuiCol.Button, s.Raised, ref count);
        Push(ImGuiCol.ButtonHovered, s.Hover, ref count);
        Push(ImGuiCol.CheckMark, s.Text, ref count);
        Push(ImGuiCol.NavHighlight, s.Text, ref count);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, UiMetrics.Px(6f));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        return new StyleScope(count, 2);
    }

    /// <summary>Danger-toned button for destructive actions (Forget character, Delete all data): Eclipse on Night. Dispose to pop.</summary>
    public static ImRaii.ColorDisposable PushDestructiveButton(bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.Button, WithAlphaVector(Danger, PaletteInks.DangerButtonAlpha), condition)
              .Push(ImGuiCol.ButtonHovered, Danger, condition)
              .Push(ImGuiCol.ButtonActive, Vector4.Lerp(Danger, Surface.Window, 0.25f) with { W = 1f }, condition)
              .Push(ImGuiCol.Text, OnDanger, condition);

    /// <summary>Text in the token color, for badges. Dispose to pop.</summary>
    public static ImRaii.ColorDisposable PushText(Vector4 color, bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.Text, color, condition);

    /// <summary>Selected rows and menu items: a neutral wash of the text colour (never gold).</summary>
    private static Vector4 SelectionWash => WithAlphaVector(Surface.Text, 0.10f);

    private static Vector4 SelectionWashActive => WithAlphaVector(Surface.Text, 0.16f);

    private static void Push(ImGuiCol idx, Vector4 color, ref int count)
    {
        ImGui.PushStyleColor(idx, color);
        count++;
    }

    private static Vector4 Rgb(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8) & 0xFF) / 255f,
        (hex & 0xFF) / 255f,
        1f);

    /// <summary>IM_COL32 layout (0xAABBGGRR). Done here rather than through ImGui so the static fields need no ImGui context.</summary>
    private static uint Pack(Vector4 c)
    {
        static uint Channel(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
        return Channel(c.X) | (Channel(c.Y) << 8) | (Channel(c.Z) << 16) | (Channel(c.W) << 24);
    }
}
