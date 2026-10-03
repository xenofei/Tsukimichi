using System.Numerics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The chrome inks a palette carries beyond its surface roles (Night values in brackets). Gold stays gold in every
/// palette: a palette may only shift its lightness for contrast (theme-system §8.1, spec-1.16 §A1).
/// </summary>
/// <param name="Gold">Gold fills, outlines and progress (Moon).</param>
/// <param name="GoldHigh">Gold's highlight: glints, the selected bead (MoonHigh).</param>
/// <param name="GoldDeep">Gold's deep stop: a pressed gold pill, a ring's shade (MoonDeep).</param>
/// <param name="GoldDim">The quieter gold of finished things as a fill or stroke (MoonDim).</param>
/// <param name="GoldLine">Gold as a hairline: the Ready road, the selected row's rules, the drawer's set mark (Moon; Snow's lead gold #AC8324).</param>
/// <param name="OnGold">Text and marks on a gold fill (Night).</param>
/// <param name="Danger">Destructive actions and Locked out as a fill or rule (Eclipse).</param>
/// <param name="DangerText">Locked out and error text (EclipseText).</param>
/// <param name="OnDanger">Text on a destructive button (Silver).</param>
/// <param name="UnknownText">Not checked text and captions (VeilText).</param>
/// <param name="GaugeArc">A tree node's progress ring arc (#CDB57A).</param>
/// <param name="GaugeDone">A finished tree node's ring (#B8933F).</param>
/// <param name="ToggleOn">The gold an "on" toggle's track is mixed toward (Moon; spec-1.16 §A6 gives Snow #EDD48C).</param>
/// <param name="ToggleKnob">An "on" toggle's crescent (MoonHigh; Snow #755308).</param>
public readonly record struct PaletteInks(
    Vector4 Gold,
    Vector4 GoldHigh,
    Vector4 GoldDeep,
    Vector4 GoldDim,
    Vector4 GoldLine,
    Vector4 OnGold,
    Vector4 Danger,
    Vector4 DangerText,
    Vector4 OnDanger,
    Vector4 UnknownText,
    Vector4 GaugeArc,
    Vector4 GaugeDone,
    Vector4 ToggleOn,
    Vector4 ToggleKnob)
{
    /// <summary>#CDB57A – the tree ring's arc on Night.</summary>
    public const uint GaugeArcHex = 0xCDB57A;

    /// <summary>#B8933F – a finished tree node's ring on Night.</summary>
    public const uint GaugeDoneHex = 0xB8933F;

    /// <summary>The destructive button's fill alpha at rest (<c>Theme.PushDestructiveButton</c>).</summary>
    public const float DangerButtonAlpha = 0.75f;
}

/// <summary>
/// The quest states' colours in a palette (spec-1.16 §A2, "State inks and stripes"): <see cref="Tone"/> is the 1.15
/// <c>Theme.StateColor</c> (a badge beside a glyph); <see cref="Text"/> is the status word, at least 4.5 : 1 on the
/// window and on cards; <see cref="Stripe"/> is the quest table's state stripe (the dash and dot pattern, not the
/// colour, carries the state).
/// </summary>
public sealed class StateInks
{
    /// <summary>The states in <see cref="QuestState"/> order.</summary>
    public static readonly QuestState[] Order =
    [
        QuestState.Completed, QuestState.Accepted, QuestState.Ready, QuestState.ReadyOnOtherJob,
        QuestState.DoneThisCycle, QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown,
    ];

    private readonly Vector4[] tones;
    private readonly Vector4[] texts;
    private readonly Vector4[] stripes;

    private StateInks(Vector4[] tones, Vector4[] texts, Vector4[] stripes)
    {
        this.tones = tones;
        this.texts = texts;
        this.stripes = stripes;
    }

    /// <summary>The inks from per-state (tone, word, stripe) triples, in <see cref="Order"/>.</summary>
    public static StateInks From(ReadOnlySpan<(QuestState State, Vector4 Tone, Vector4 Text, Vector4 Stripe)> states)
    {
        var tones = new Vector4[Order.Length];
        var texts = new Vector4[Order.Length];
        var stripes = new Vector4[Order.Length];
        foreach (var (state, tone, text, stripe) in states)
        {
            tones[(int)state] = tone;
            texts[(int)state] = text;
            stripes[(int)state] = stripe;
        }

        return new StateInks(tones, texts, stripes);
    }

    /// <summary>
    /// The inks from their roles, as Night composes them: gold states take <paramref name="gold"/> as tone and stripe
    /// (Completed's stripe <paramref name="goldDim"/>) and <paramref name="accent"/> as the word; the silver states
    /// <paramref name="silver"/>; Blocked the tone <paramref name="dusk"/> and the word <paramref name="blockedText"/>;
    /// Locked out the danger tone and its text; Not checked <paramref name="veil"/> and its text.
    /// </summary>
    public static StateInks Compose(Vector4 gold, Vector4 goldDim, Vector4 accent, Vector4 silver, Vector4 dusk, Vector4 blockedText, Vector4 danger, Vector4 dangerText, Vector4 veil, Vector4 unknownText) =>
        From(
        [
            (QuestState.Completed, gold, accent, goldDim),
            (QuestState.Accepted, gold, accent, gold),
            (QuestState.Ready, gold, accent, gold),
            (QuestState.ReadyOnOtherJob, silver, silver, silver),
            (QuestState.DoneThisCycle, silver, silver, silver),
            (QuestState.Blocked, dusk, blockedText, unknownText),
            (QuestState.Foreclosed, danger, dangerText, danger),
            (QuestState.Unknown, veil, unknownText, unknownText),
        ]);

    /// <summary>The state's tone (the 1.15 <c>Theme.StateColor</c>); an unknown value reads as Blocked's.</summary>
    public Vector4 Tone(QuestState state) => tones[Index(state)];

    /// <summary>The state's status word ink; an unknown value reads as Blocked's.</summary>
    public Vector4 Text(QuestState state) => texts[Index(state)];

    /// <summary>The state's table stripe; an unknown value reads as Not checked's.</summary>
    public Vector4 Stripe(QuestState state) => stripes[(uint)state < (uint)stripes.Length ? (int)state : (int)QuestState.Unknown];

    /// <summary>The same inks with every word passed through <paramref name="text"/> and every stripe through <paramref name="stripe"/> (the tones are kept).</summary>
    public StateInks Map(Func<Vector4, Vector4> text, Func<Vector4, Vector4> stripe)
    {
        var mappedTexts = new Vector4[texts.Length];
        var mappedStripes = new Vector4[stripes.Length];
        for (var i = 0; i < texts.Length; i++)
        {
            mappedTexts[i] = text(texts[i]);
            mappedStripes[i] = stripe(stripes[i]);
        }

        return new StateInks(tones, mappedTexts, mappedStripes);
    }

    /// <summary>The same inks with <paramref name="state"/>'s word in <paramref name="text"/> (a designed high-contrast form's one change).</summary>
    public StateInks WithText(QuestState state, Vector4 text)
    {
        var mappedTexts = (Vector4[])texts.Clone();
        mappedTexts[Index(state)] = text;
        return new StateInks(tones, mappedTexts, stripes);
    }

    private int Index(QuestState state) => (uint)state < (uint)tones.Length ? (int)state : (int)QuestState.Blocked;
}

/// <summary>
/// The drawn scene of a palette (Night values in brackets; spec-1.16 §A4–A5 for a light palette): the Full sky, the
/// status bar's ground, the star field, and every token that assumes a dark window. A light palette turns the stars and
/// the night grade off, casts lighter navy shadows, and lays warm washes where Night glows.
/// </summary>
/// <param name="Zenith">Full's sky at the top of a pane (#1B2552); the window's top colour where none is designed.</param>
/// <param name="SkyStops">A designed sky gradient as (fraction of the pane's height, colour) stops, top to foot (Snow's dawn over snow); null draws the zenith-and-water sky.</param>
/// <param name="StatusTop">The status bar's ground at Full, top (#0E1329).</param>
/// <param name="StatusFoot">The status bar's ground at Full, foot (#0A0E1C).</param>
/// <param name="Shadow">The colour of the drop shadows under medals, plates, pills and glyph wells (Abyss; Snow's navy #1A2136).</param>
/// <param name="ShadowInk">spec-1.16's ShadowInk: the colour of the soft cast shadows of cards and the drawer (black; Snow #1A2136).</param>
/// <param name="ShadowStrength">Multiplies every shadow's designed alpha (1 on a dark palette).</param>
/// <param name="GlowStrength">Multiplies every gold glow's and sheen's designed alpha (1 on a dark palette).</param>
/// <param name="WashInsteadOfGlow">A light palette's rule (§A4): gold glows are drawn as a warm wash in <see cref="GlowWash"/>, never additive light.</param>
/// <param name="GlowWash">The warm wash a light palette draws where Night glows (Moon; Snow #F2D27A).</param>
/// <param name="TopHighlight">The 1 px lit edge inside the top of a raised surface (MoonHigh, drawn at about .06; white on Snow).</param>
/// <param name="TextHalo">The halo behind text drawn over brass or sky (Abyss).</param>
/// <param name="Scrim">The tour's dimming veil over the window (Night).</param>
/// <param name="Moonlight">The banner grade's wash and moon road, and the meteors (MoonHigh).</param>
/// <param name="BannerTitle">A quest title drawn on banner art (#F4F1E8 warm near-white; Snow's navy ink).</param>
/// <param name="StarField">
/// Whether Full draws the star field, the moving sky, the meteors, the Milky Way and the constellations. Off on a light
/// palette, with nothing drawn in their place (spec-1.16 §A5, the supervisor's ruling: no stars at all on a light palette).
/// </param>
/// <param name="NightGrade">Whether banners and giver portraits take the night multiply (off on a light palette: the daylight grade).</param>
/// <param name="Stars">The star field's inks.</param>
/// <param name="TopHighlightAlpha">
/// The alpha every raised surface's 1 px top highlight is drawn at, or 0 for each caller's designed alpha (Night's MoonHigh
/// at about .06). A light palette's white lit edge is drawn near opaque (spec-1.16 §A4).
/// </param>
/// <param name="Washes">The warm washes a light palette lays where Night glows (spec-1.16 §A4), and the Ready badge's fill.</param>
public readonly record struct SceneTokens(
    Vector4 Zenith,
    (float At, Vector4 Color)[]? SkyStops,
    Vector4 StatusTop,
    Vector4 StatusFoot,
    Vector4 Shadow,
    Vector4 ShadowInk,
    float ShadowStrength,
    float GlowStrength,
    bool WashInsteadOfGlow,
    Vector4 GlowWash,
    Vector4 TopHighlight,
    Vector4 TextHalo,
    Vector4 Scrim,
    Vector4 Moonlight,
    Vector4 BannerTitle,
    bool StarField,
    bool NightGrade,
    StarInks Stars,
    float TopHighlightAlpha,
    WashTokens Washes)
{
    /// <summary>#1B2552 – Full's sky zenith on Night (docs/design/flair-v13 §1).</summary>
    public const uint ZenithHex = 0x1B2552;

    /// <summary>#0E1329 – the status bar's Deep gradient at Full, top.</summary>
    public const uint StatusTopHex = 0x0E1329;

    /// <summary>#0A0E1C – the status bar's Deep gradient at Full, foot.</summary>
    public const uint StatusFootHex = 0x0A0E1C;

    /// <summary>#F4F1E8 – the title on banner art, a warm near-white over the night grade.</summary>
    public const uint BannerTitleHex = 0xF4F1E8;

    /// <summary>A light palette's shadows, as a share of the designed (dark) alpha.</summary>
    public const float LightShadowStrength = 0.45f;

    /// <summary>A light palette's gold glows and sheens, as a share of the designed alpha.</summary>
    public const float LightGlowStrength = 0.6f;

    /// <summary>The white 1 px top highlight of a raised surface on a light palette, near opaque (the mock's inset white line).</summary>
    public const float LightTopHighlightAlpha = 0.85f;

    /// <summary>
    /// The scene a derived palette (Follow Dalamud) draws on <paramref name="surface"/>: on a dark window Night's (stars,
    /// night grade, Abyss shadows), with the sky and status bar from the surface; on a light window the daylight hook
    /// (no stars, no night multiply, navy shadows and washes at about half strength, a light text halo, a white top
    /// highlight, navy titles on art).
    /// </summary>
    public static SceneTokens Derived(in SurfaceColors surface)
    {
        var night = UiPalettes.NightScene with { Zenith = surface.Top, StatusTop = surface.Deep, StatusFoot = surface.Deep };
        if (!surface.Light)
        {
            return night;
        }

        return night with
        {
            Shadow = ColorMath.Mix(surface.Text, GlyphTokens.Abyss, 0.5f),
            ShadowInk = ColorMath.Mix(surface.Text, GlyphTokens.Abyss, 0.5f),
            ShadowStrength = LightShadowStrength,
            GlowStrength = LightGlowStrength,
            WashInsteadOfGlow = true,
            TopHighlight = Vector4.One,
            TopHighlightAlpha = LightTopHighlightAlpha,
            TextHalo = surface.Window,
            BannerTitle = surface.Text,
            StarField = false,
            NightGrade = false,
            Washes = WashTokens.Light(ColorMath.EnsureContrast(ColorMath.FromHex(WashTokens.LeadGoldHex), surface.Text, surface.Window, SurfaceColors.LineMinContrast)),
        };
    }
}

/// <summary>
/// The warm washes of a palette (spec-1.16 §A4, §A4.1): where a dark palette glows (additive gold light), a light one
/// lays a warm wash, because light added to a light page is invisible or muddy. Each colour carries its designed alpha
/// in W. Drawn only where <see cref="SceneTokens.WashInsteadOfGlow"/> is set, but for the Ready badge's fill, which every
/// palette draws. The Ready wash's alpha and reach are roles, so a ruling can change them without code.
/// </summary>
/// <param name="ReadyHalo">The Ready-only wash within <paramref name="ReadyHaloReach"/> of a row medal (Snow #F2D27A at .75; the supervisor's final ruling).</param>
/// <param name="ReadyHaloReach">How far the Ready wash reaches past the row medal's edge, logical px (3).</param>
/// <param name="HeroHalo">The hero medal's halo at its edge, fading to 0 (Snow #E9C46A at .30).</param>
/// <param name="Selection">The selected table row's wash at its left edge (Snow #F2D27A at .30).</param>
/// <param name="SelectionFoot">The selected row wash's alpha at its far end (.05).</param>
/// <param name="SelectionRule">The selected table row's hairlines top and bottom (Snow #AC8324 at .45).</param>
/// <param name="TreeSelection">The selected tree row's wash from the left (Snow #AC8324 at .14, falling to a quarter of it).</param>
/// <param name="ReadyRoad">The Ready road's 1 px line under a Ready row, fading out (Snow's lead gold #AC8324 at .55); no glint and no glow on a light palette.</param>
/// <param name="ReadyBadge">The tree's Ready count badge fill (Night: Moon at .16; Snow #F2D27A at .38).</param>
public readonly record struct WashTokens(
    Vector4 ReadyHalo,
    float ReadyHaloReach,
    Vector4 HeroHalo,
    Vector4 Selection,
    float SelectionFoot,
    Vector4 SelectionRule,
    Vector4 TreeSelection,
    Vector4 ReadyRoad,
    Vector4 ReadyBadge)
{
    /// <summary>#F2D27A – the warm wash of a light palette (Moon, laid as a wash rather than light).</summary>
    public const uint WashHex = 0xF2D27A;

    /// <summary>#E9C46A – the hero halo's wash on a light palette.</summary>
    public const uint HeroHaloHex = 0xE9C46A;

    /// <summary>#AC8324 – lead gold: Snow's gold hairlines, set marks and the Ready road (3.1 : 1 on the window).</summary>
    public const uint LeadGoldHex = 0xAC8324;

    /// <summary>
    /// The Ready-only wash's alpha and reach (spec-1.16 §A4.1; the supervisor's final ruling: .75 within 3 px for every
    /// set, and no .90 / 4 px fallback).
    /// </summary>
    public const float ReadyHaloAlpha = 0.75f;

    /// <inheritdoc cref="ReadyHaloAlpha"/>
    public const float ReadyHaloReachLogical = 3f;

    /// <summary>
    /// Night's washes. Night glows instead, so the Ready badge's Moon at .16 is the one it draws; the rest mirror the light
    /// washes in Moon so a palette that turns the wash rule on has something sane to draw.
    /// </summary>
    public static readonly WashTokens Night = Light(GlyphTokens.Moon) with { ReadyBadge = GlyphTokens.Moon with { W = 0.16f } };

    /// <summary>
    /// A light palette's washes (spec-1.16 §A4) with <paramref name="leadGold"/> as its gold hairline: the Ready halo
    /// #F2D27A at .75 within 3 px, the hero halo #E9C46A at .30, the selected row #F2D27A .30 → .05 between lead-gold
    /// hairlines at .45, the selected tree row lead gold at .14, the Ready road lead gold at .55, the Ready badge #F2D27A
    /// at .38.
    /// </summary>
    public static WashTokens Light(Vector4 leadGold)
    {
        var wash = ColorMath.FromHex(WashHex);
        return new WashTokens(
            ReadyHalo: wash with { W = ReadyHaloAlpha },
            ReadyHaloReach: ReadyHaloReachLogical,
            HeroHalo: ColorMath.FromHex(HeroHaloHex) with { W = 0.30f },
            Selection: wash with { W = 0.30f },
            SelectionFoot: 0.05f,
            SelectionRule: leadGold with { W = 0.45f },
            TreeSelection: leadGold with { W = 0.14f },
            ReadyRoad: leadGold with { W = 0.55f },
            ReadyBadge: wash with { W = 0.38f });
    }
}

/// <summary>
/// A palette's gauge inks (spec-1.16 §A6): the halo gauge, the tree's orbit ring, the rail's Journal station and foot
/// gauge, Quiet's ring and Flight's bead ring. On Night they are the medal's material (a lapis groove between Abyss
/// keylines, a gilt arc lit as the bezel is, a moonstone moon over its dark side), exactly as 1.12 drew them. A light
/// palette gives them their own ink so they read as UI graphics at 3 : 1: Snow's gilt ramp #8A6A1C → #755308 (the
/// supervisor's ruling, not #A07B25), a #CAD2DF groove between #7A859C keylines, a #8A6A1C knob with a #F9FAFC rim, and
/// moonstone over a #59627A dark side.
/// </summary>
/// <param name="Groove">The track the arc runs in (Night: the lapis sea bottom).</param>
/// <param name="Keyline">The keylines either side of the groove and round the moon (Night: the medal keyline).</param>
/// <param name="ArcBase">The arc's base stroke and thin arcs' caps (Night: Gilt).</param>
/// <param name="ArcDim">A finished gauge stepping back (the Journal tree's), and a new moon's fine outline (Night: the gilt's shade).</param>
/// <param name="OuterSlope">The arc's outer slope across the gauge's box, upper left to lower right, as (t, colour) stops.</param>
/// <param name="InnerSlope">The arc's inner slope, the same way.</param>
/// <param name="Knob">The bead at the arc's head and the full-moon pip (Night: moonstone's glint).</param>
/// <param name="KnobRim">The ring round the knob (Night: the keyline; Snow #F9FAFC).</param>
/// <param name="MoonGlint">The filling moon's highlight.</param>
/// <param name="MoonLit">The filling moon's lit face (Snow's moonstone #C3CEE4).</param>
/// <param name="MoonBody">The filling moon's body, where the lit face falls away from the light.</param>
/// <param name="MoonDim">A finished moon stepping back, flat.</param>
/// <param name="DarkSide">The filling moon's dark side (Snow #59627A).</param>
/// <param name="Track">A flat gauge's unlit track (the Classic ring, Flight's bead ring): the strong line at .55 on Night; Snow's keyline, opaque.</param>
/// <param name="Arc">A flat gauge's lit arc (the Classic ring, Flight's bead ring): Moon on Night; Snow's #8A6A1C.</param>
/// <param name="Glows">Whether a complete gauge glows (Night); a light palette never glows.</param>
public readonly record struct GaugeInks(
    Vector4 Groove,
    Vector4 Keyline,
    Vector4 ArcBase,
    Vector4 ArcDim,
    (float At, Vector4 Color)[] OuterSlope,
    (float At, Vector4 Color)[] InnerSlope,
    Vector4 Knob,
    Vector4 KnobRim,
    Vector4 MoonGlint,
    Vector4 MoonLit,
    Vector4 MoonBody,
    Vector4 MoonDim,
    Vector4 DarkSide,
    Vector4 Track,
    Vector4 Arc,
    bool Glows)
{
    /// <summary>#8A6A1C – a light palette's gauge arc at its highlight (upper left): 3.3 : 1 on the groove, 3.7 on the zenith, 4.5 on the window.</summary>
    public const uint LightArcHighHex = 0x8A6A1C;

    /// <summary>#755308 – a light palette's gauge arc at its shade (lower right): at least 4.6 : 1 everywhere.</summary>
    public const uint LightArcShadeHex = 0x755308;

    /// <summary>#CAD2DF – a light palette's groove.</summary>
    public const uint LightGrooveHex = 0xCAD2DF;

    /// <summary>#7A859C – a light palette's groove keylines (3.3 : 1 on the window), so a gauge has an edge whatever is behind it.</summary>
    public const uint LightKeylineHex = 0x7A859C;

    /// <summary>#F9FAFC – the light knob's rim.</summary>
    public const uint LightKnobRimHex = 0xF9FAFC;

    /// <summary>#C3CEE4 – a light palette's moonstone.</summary>
    public const uint LightMoonstoneHex = 0xC3CEE4;

    /// <summary>#59627A – a light palette's moon dark side.</summary>
    public const uint LightDarkSideHex = 0x59627A;

    /// <summary>The lightest and darkest colours the arc is drawn in: every stop of both slopes and the base.</summary>
    public IEnumerable<Vector4> ArcColors()
    {
        yield return ArcBase;
        foreach (var (_, color) in OuterSlope)
        {
            yield return color;
        }

        foreach (var (_, color) in InnerSlope)
        {
            yield return color;
        }
    }

    /// <summary>The colour of a stop ramp at <paramref name="t"/> (clamped; the end stops' colours held past the ends).</summary>
    public static Vector4 Stop(ReadOnlySpan<(float At, Vector4 Color)> stops, float t)
    {
        if (stops.IsEmpty)
        {
            return Vector4.Zero;
        }

        t = float.IsFinite(t) ? t : 0f;
        if (t <= stops[0].At)
        {
            return stops[0].Color;
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].At)
            {
                var (a0, c0) = stops[i - 1];
                var (a1, c1) = stops[i];
                return Vector4.Lerp(c0, c1, (t - a0) / MathF.Max(1e-6f, a1 - a0));
            }
        }

        return stops[^1].Color;
    }
}

/// <summary>The star field's inks (docs/design/flair-v13 §3.2–3.5).</summary>
/// <param name="Cool">Cool stars and constellation lines (#DCE5FF).</param>
/// <param name="MoonWhite">Moon-white stars (#F4F2EA).</param>
/// <param name="Gold">Gold stars (#FFE2A8).</param>
/// <param name="Ember">Ember stars (#FFC9AE).</param>
/// <param name="Figure">The constellations' stars (#EEF1FA).</param>
/// <param name="Band">The Milky Way's tint (#C9D3F0).</param>
public readonly record struct StarInks(Vector4 Cool, Vector4 MoonWhite, Vector4 Gold, Vector4 Ember, Vector4 Figure, Vector4 Band)
{
    public const uint CoolHex = 0xDCE5FF;
    public const uint MoonWhiteHex = 0xF4F2EA;
    public const uint GoldHex = 0xFFE2A8;
    public const uint EmberHex = 0xFFC9AE;
    public const uint FigureHex = 0xEEF1FA;
    public const uint BandHex = 0xC9D3F0;
}

/// <summary>
/// The Decoration frame metal of a palette (docs/design/flair-v13 §1, "Card frame"; spec-1.16 §A6): the 160° ramp, lit
/// from the upper left, and the corner marks. Frame kits (plan v7 T11) choose among metals (brass, lead came); until
/// then each palette carries one.
/// </summary>
/// <param name="High">The ramp's highlight (#E2C78C).</param>
/// <param name="Body">The ramp's body at 28 % (Gilt, #A88B52).</param>
/// <param name="Shadow">The ramp's shadow at 55 % (#6E5732).</param>
/// <param name="Reflected">The reflected lift at 78 % (#9C8049).</param>
/// <param name="Deep">The ramp's deep end (#5A4729).</param>
/// <param name="CornerLit">The top corner marks (#F0D9A0).</param>
/// <param name="CornerShaded">The bottom corner marks (#B79755).</param>
public readonly record struct BrassTokens(Vector4 High, Vector4 Body, Vector4 Shadow, Vector4 Reflected, Vector4 Deep, Vector4 CornerLit, Vector4 CornerShaded)
{
    public const uint HighHex = 0xE2C78C;
    public const uint ShadowHex = 0x6E5732;
    public const uint ReflectedHex = 0x9C8049;
    public const uint DeepHex = 0x5A4729;
    public const uint CornerLitHex = 0xF0D9A0;
    public const uint CornerShadedHex = 0xB79755;

    /// <summary>
    /// The ramp's colour at <paramref name="t"/> along its light (0 at the upper left, 1 at the lower right): highlight,
    /// body at 28 %, shadow at 55 %, the reflected lift at 78 %, deep at the end.
    /// </summary>
    public Vector4 At(float t)
    {
        t = float.IsFinite(t) ? Math.Clamp(t, 0f, 1f) : 0f;
        return t switch
        {
            < 0.28f => Vector4.Lerp(High, Body, t / 0.28f),
            < 0.55f => Vector4.Lerp(Body, Shadow, (t - 0.28f) / 0.27f),
            < 0.78f => Vector4.Lerp(Shadow, Reflected, (t - 0.55f) / 0.23f),
            _ => Vector4.Lerp(Reflected, Deep, (t - 0.78f) / 0.22f),
        };
    }
}

/// <summary>
/// The giver portrait plate in a palette (1.15 design spec A4; spec-1.16 §A7 for Snow): the well, the keylines per
/// Decoration level, the fallback initials and the outer ring.
/// </summary>
/// <param name="WellTop">Full's and Quiet's well, top (#1D2B5A).</param>
/// <param name="WellFoot">The well, foot (#131C40).</param>
/// <param name="PlainWell">Plain's flat well (#1C2237).</param>
/// <param name="PlainKeyline">Plain's 1 px keyline (#3A4050).</param>
/// <param name="QuietKeyline">Quiet's silver hairline (#C3CBDF; Snow #7A859C at .8).</param>
/// <param name="Initials">The fallback initials (#E9E4D2; Snow #56607C).</param>
/// <param name="OuterRing">Full's outer ring around the brass keyline (Abyss; Snow's white line at .9).</param>
/// <param name="KeylineLit">The brass keyline at its lit end (#E6CF98).</param>
/// <param name="KeylineMid">The keyline at 45 % (#9A7E4A).</param>
/// <param name="KeylineLow">The keyline at 80 % (#7C6236).</param>
/// <param name="KeylineDark">The keyline at its dark end (#5C4724).</param>
public readonly record struct PlateTokens(
    Vector4 WellTop,
    Vector4 WellFoot,
    Vector4 PlainWell,
    Vector4 PlainKeyline,
    Vector4 QuietKeyline,
    Vector4 Initials,
    Vector4 OuterRing,
    Vector4 KeylineLit,
    Vector4 KeylineMid,
    Vector4 KeylineLow,
    Vector4 KeylineDark)
{
    public const uint PlainWellHex = 0x1C2237;
    public const uint PlainKeylineHex = 0x3A4050;
    public const uint QuietKeylineHex = 0xC3CBDF;

    /// <summary>The keyline's colour at <paramref name="t"/> along its light: lit, mid at 45 %, low at 80 %, dark at the end.</summary>
    public Vector4 KeylineAt(float t)
    {
        t = float.IsFinite(t) ? Math.Clamp(t, 0f, 1f) : 0f;
        return t < 0.45f ? Vector4.Lerp(KeylineLit, KeylineMid, t / 0.45f)
            : t < 0.8f ? Vector4.Lerp(KeylineMid, KeylineLow, (t - 0.45f) / 0.35f)
            : Vector4.Lerp(KeylineLow, KeylineDark, (t - 0.8f) / 0.2f);
    }
}

/// <summary>
/// Full's lit pills (docs/design/flair-v13 §1, "Action pills"; spec-1.16 §A6): the raised gradient of the ordinary
/// pills, and the gold material of the primary, which stays gold in every palette.
/// </summary>
/// <param name="RaisedTop">The raised pill's top (#252B45).</param>
/// <param name="RaisedFoot">The raised pill's foot (#1C2138).</param>
/// <param name="GoldTop">The gold pill's top (#FFE6A3).</param>
/// <param name="GoldFoot">The gold pill's foot (#D9B65F).</param>
/// <param name="GoldEdge">The gold pill's edge (#F6DFA0).</param>
/// <param name="GoldInk">The dark ink on the gold pill (#1A1406).</param>
public readonly record struct PillSurfaces(Vector4 RaisedTop, Vector4 RaisedFoot, Vector4 GoldTop, Vector4 GoldFoot, Vector4 GoldEdge, Vector4 GoldInk)
{
    public const uint RaisedTopHex = 0x252B45;
    public const uint RaisedFootHex = 0x1C2138;
    public const uint GoldTopHex = 0xFFE6A3;
    public const uint GoldFootHex = 0xD9B65F;
    public const uint GoldEdgeHex = 0xF6DFA0;
    public const uint GoldInkHex = 0x1A1406;
}

/// <summary>The filter drawer's designed tones at each Decoration level (<see cref="Ui.DrawerTones"/>).</summary>
public readonly record struct DrawerToneSet(DrawerTones Full, DrawerTones Quiet, DrawerTones Plain)
{
    /// <summary>The tones at <paramref name="flair"/>.</summary>
    public DrawerTones For(Flair flair) => flair switch
    {
        Flair.Full => Full,
        Flair.Quiet => Quiet,
        _ => Plain,
    };
}
