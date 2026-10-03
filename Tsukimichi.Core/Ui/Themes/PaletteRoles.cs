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
/// <param name="StarField">Whether Full draws the star field, the moving sky and the meteors (off on a light palette).</param>
/// <param name="MorningStar">Whether Full draws Snow's single static morning star in the tree's sky (§A5; off on Night).</param>
/// <param name="NightGrade">Whether banners and giver portraits take the night multiply (off on a light palette: the daylight grade).</param>
/// <param name="Stars">The star field's inks.</param>
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
    bool MorningStar,
    bool NightGrade,
    StarInks Stars)
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
            TextHalo = surface.Window,
            BannerTitle = surface.Text,
            StarField = false,
            NightGrade = false,
        };
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
