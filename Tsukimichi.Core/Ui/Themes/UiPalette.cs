using System.Numerics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// One UI colour palette (plan v7 T2, docs/research/plan-v7/theme-system.md §8.1): every colour the chrome draws with,
/// by role. The plugin's <c>Ui.Theme</c> resolves the palette in effect once per palette change and every pane reads its
/// roles from there, so a palette (Night, a light Ishgard Snow, Follow Dalamud) recolours the whole window.
///
/// <para>What is not here stays fixed, on purpose: the glyphs and medals (<see cref="GlyphTokens"/>,
/// <see cref="GlyphPalette"/>, the atlases), which carry their own wells and keylines so they read on any palette; the
/// gold of the "act now" pill material, which is a meaning, not a mood; and picture pixels (game art and its tints).</para>
///
/// <para>Anything that assumes a dark background reads <see cref="IsLight"/> or a <see cref="Scene"/> token rather than
/// a literal: shadows, glows, text halos, the star field and the banner and portrait night grades.</para>
/// </summary>
public sealed record UiPalette
{
    /// <summary>The stable key the palette is saved and shared as ("night", "follow-dalamud", …).</summary>
    public required string Key { get; init; }

    /// <summary>The palette's name in English (the Themes page localises its own label).</summary>
    public required string Name { get; init; }

    /// <summary>The 17 surface and text roles (Window … CoolDeep), with the <see cref="SurfaceColors.Light"/> flag.</summary>
    public required SurfaceColors Surface { get; init; }

    /// <summary>Gold as text and small ink (pill labels, the active tab icon): Moon on Night, deepened on a light palette until it reads.</summary>
    public required Vector4 Accent { get; init; }

    /// <summary>The quieter gold of finished things, as text (MoonDim on Night).</summary>
    public required Vector4 AccentDim { get; init; }

    /// <summary>The chrome inks beyond the surface roles: gold fills, the danger tone, the Unknown text, the gauge golds.</summary>
    public required PaletteInks Inks { get; init; }

    /// <summary>The eight quest states' tones and readable text colours.</summary>
    public required StateInks States { get; init; }

    /// <summary>The drawn scene: sky, stars, shadows, glows, halos and the night grades.</summary>
    public required SceneTokens Scene { get; init; }

    /// <summary>The Decoration brass: the card frame's ramp and the corner marks.</summary>
    public required BrassTokens Brass { get; init; }

    /// <summary>Full's lit raised pills, top and foot; null where the palette has none designed (flat pills, as under Follow Dalamud).</summary>
    public PillSurfaces? Pills { get; init; }

    /// <summary>Quiet's designed pane tones; null mixes them from <see cref="Surface"/> (<see cref="FlairTones.For(Flair, UiPalette)"/>).</summary>
    public FlairTones? QuietTones { get; init; }

    /// <summary>Plain's designed ledger tones; null mixes them from <see cref="Surface"/>.</summary>
    public FlairTones? PlainTones { get; init; }

    /// <summary>The filter drawer's designed tones per level; null mixes them from <see cref="Surface"/>.</summary>
    public DrawerToneSet? DrawerTones { get; init; }

    /// <summary>Whether this is a palette's high-contrast form (<see cref="HighContrast"/>).</summary>
    public bool IsHighContrast { get; init; }

    /// <summary>Whether the window is light (dark ink on a light ground): shadows, glows and halos go light-aware.</summary>
    public bool IsLight => Surface.Light;

    /// <summary>
    /// The high-contrast form, built once and kept (allocation-free after the first call): see
    /// <see cref="ToHighContrast"/>. A high-contrast form is its own high-contrast form.
    /// </summary>
    public UiPalette HighContrast
    {
        get
        {
            if (IsHighContrast)
            {
                return this;
            }

            return highContrast ??= HighContrastBuilder?.Invoke(this) ?? ToHighContrast(pushInks: true);
        }
    }

    /// <summary>
    /// How this palette builds its high-contrast form when it is not the generic transform (Night's is today's
    /// <see cref="SurfaceColors.ForHighContrast"/> alone, so it draws exactly as 1.15 did).
    /// </summary>
    public Func<UiPalette, UiPalette>? HighContrastBuilder { get; init; }

    private UiPalette? highContrast;

    /// <summary>
    /// The generic high-contrast form (§8.3): the surface's high-contrast roles (no sky gradient, the strong line as an
    /// opaque ornament, Cool at 7 : 1), the designed Quiet, Plain and drawer tones dropped so they are mixed from that
    /// surface, and with <paramref name="pushInks"/> the accent, the danger and Unknown text and the state text inks
    /// pushed towards the text colour until they reach 7 : 1 on the window.
    /// </summary>
    public UiPalette ToHighContrast(bool pushInks)
    {
        var surface = Surface.ForHighContrast();
        if (!pushInks)
        {
            return this with { Key = Key + "-hc", Surface = surface, QuietTones = null, PlainTones = null, DrawerTones = null, IsHighContrast = true, HighContrastBuilder = null, highContrast = null };
        }

        const float min = SurfaceColors.HighContrastTextMinContrast;
        Vector4 Push(Vector4 ink) => ColorMath.EnsureContrast(ink, surface.Text, surface.Window, min);
        return this with
        {
            Key = Key + "-hc",
            Surface = surface,
            Accent = Push(Accent),
            AccentDim = Push(AccentDim),
            Inks = Inks with { DangerText = Push(Inks.DangerText), UnknownText = Push(Inks.UnknownText) },
            States = States.Map(Push),
            QuietTones = null,
            PlainTones = null,
            DrawerTones = null,
            IsHighContrast = true,
            HighContrastBuilder = null,
            highContrast = null,
        };
    }

    /// <summary>
    /// Every text-on-surface pair the palette promises reads at WCAG AA (4.5 : 1), as (role, ink, ground). Translucent
    /// grounds are composited over the window first. <c>PaletteContrastTests</c> holds every palette to this list.
    /// </summary>
    public IEnumerable<(string Role, Vector4 Ink, Vector4 Ground)> TextPairs()
    {
        var s = Surface;
        yield return ("Text on Window", s.Text, s.Window);
        yield return ("Text on Raised", s.Text, s.Raised);
        yield return ("Text on Sunken", s.Text, s.Sunken);
        yield return ("Text on Hover", s.Text, s.Hover);
        yield return ("Text on Deep", s.Text, s.Deep);
        yield return ("Text on Top", s.Text, s.Top);
        yield return ("TextSecondary on Window", s.TextSecondary, s.Window);
        yield return ("TextSecondary on Raised", s.TextSecondary, s.Raised);
        yield return ("TextTertiary on Window", s.TextTertiary, s.Window);
        yield return ("Cool on Window", s.Cool, s.Window);
        yield return ("Cool on Raised", s.Cool, s.Raised);
        yield return ("Accent on Window", Accent, s.Window);
        yield return ("Accent on Raised", Accent, s.Raised);
        yield return ("AccentDim on Window", AccentDim, s.Window);
        yield return ("DangerText on Window", Inks.DangerText, s.Window);
        yield return ("DangerText on Raised", Inks.DangerText, s.Raised);
        yield return ("UnknownText on Window", Inks.UnknownText, s.Window);
        yield return ("OnGold on Gold", Inks.OnGold, Inks.Gold);
        yield return ("OnDanger on the destructive button", Inks.OnDanger, ColorMath.Over(Inks.Danger with { W = PaletteInks.DangerButtonAlpha }, s.Window));
        foreach (var state in StateInks.Order)
        {
            yield return ($"{state} text on Window", States.Text(state), s.Window);
        }
    }

    /// <summary>
    /// The lines the palette promises reach WCAG 1.4.11 (3 : 1) on the window, as (role, ink, ground): the strong line,
    /// and the opaque ornament under high contrast.
    /// </summary>
    public IEnumerable<(string Role, Vector4 Ink, Vector4 Ground)> LinePairs()
    {
        var s = Surface;
        yield return ("StrongLine on Window", s.StrongLine, s.Window);
        yield return ("OrnamentHigh on Window", s.OrnamentHigh, s.Window);
        if (IsHighContrast)
        {
            yield return ("Ornament on Window", s.Ornament, s.Window);
        }
    }
}

/// <summary>
/// The chrome inks a palette carries beyond its surface roles (Night values in brackets). Gold stays gold in every
/// palette: a palette may only shift its lightness for contrast (§8.1).
/// </summary>
/// <param name="Gold">Gold fills, rules, outlines and progress (Moon).</param>
/// <param name="GoldHigh">Gold's highlight: sheens, glints, the selected bead (MoonHigh).</param>
/// <param name="GoldDeep">Gold's deep stop: a pressed gold pill, a ring's shade (MoonDeep).</param>
/// <param name="GoldDim">The quieter gold of finished things as a fill or stroke (MoonDim).</param>
/// <param name="OnGold">Text and marks on a gold fill (Night).</param>
/// <param name="Danger">Destructive actions and Locked out as a fill or rule (Eclipse).</param>
/// <param name="DangerText">Locked out and error text (EclipseText).</param>
/// <param name="OnDanger">Text on a destructive button (Silver).</param>
/// <param name="UnknownText">Not checked text and captions (VeilText).</param>
/// <param name="GaugeArc">A tree node's progress ring arc (#CDB57A).</param>
/// <param name="GaugeDone">A finished tree node's ring (#B8933F).</param>
public readonly record struct PaletteInks(
    Vector4 Gold,
    Vector4 GoldHigh,
    Vector4 GoldDeep,
    Vector4 GoldDim,
    Vector4 OnGold,
    Vector4 Danger,
    Vector4 DangerText,
    Vector4 OnDanger,
    Vector4 UnknownText,
    Vector4 GaugeArc,
    Vector4 GaugeDone)
{
    /// <summary>#CDB57A – the tree ring's arc on Night.</summary>
    public const uint GaugeArcHex = 0xCDB57A;

    /// <summary>#B8933F – a finished tree node's ring on Night.</summary>
    public const uint GaugeDoneHex = 0xB8933F;

    /// <summary>The destructive button's fill alpha at rest (<c>Theme.PushDestructiveButton</c>).</summary>
    public const float DangerButtonAlpha = 0.75f;
}

/// <summary>
/// The quest states' colours in a palette: <see cref="Tone"/> is today's <c>Theme.StateColor</c> (a pill stripe, a
/// glyph-adjacent badge); <see cref="Text"/> is the readable text ink, at least 4.5 : 1 on the window.
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

    private StateInks(Vector4[] tones, Vector4[] texts)
    {
        this.tones = tones;
        this.texts = texts;
    }

    /// <summary>
    /// The state inks from their roles: gold states take <paramref name="gold"/> as the tone and <paramref name="accent"/>
    /// as text; the silver states <paramref name="silver"/>; Blocked <paramref name="dusk"/>; Locked out the danger tone
    /// and its text; Not checked <paramref name="veil"/> and its text.
    /// </summary>
    public static StateInks From(Vector4 gold, Vector4 accent, Vector4 silver, Vector4 dusk, Vector4 danger, Vector4 dangerText, Vector4 veil, Vector4 unknownText)
    {
        var tones = new Vector4[Order.Length];
        var texts = new Vector4[Order.Length];
        void Set(QuestState state, Vector4 tone, Vector4 text)
        {
            tones[(int)state] = tone;
            texts[(int)state] = text;
        }

        Set(QuestState.Completed, gold, accent);
        Set(QuestState.Accepted, gold, accent);
        Set(QuestState.Ready, gold, accent);
        Set(QuestState.ReadyOnOtherJob, silver, silver);
        Set(QuestState.DoneThisCycle, silver, silver);
        Set(QuestState.Blocked, dusk, dusk);
        Set(QuestState.Foreclosed, danger, dangerText);
        Set(QuestState.Unknown, veil, unknownText);
        return new StateInks(tones, texts);
    }

    /// <summary>The state's tone (today's <c>Theme.StateColor</c>); an unknown value reads as Blocked's.</summary>
    public Vector4 Tone(QuestState state) => (uint)state < (uint)tones.Length ? tones[(int)state] : tones[(int)QuestState.Blocked];

    /// <summary>The state's readable text ink; an unknown value reads as Blocked's.</summary>
    public Vector4 Text(QuestState state) => (uint)state < (uint)texts.Length ? texts[(int)state] : texts[(int)QuestState.Blocked];

    /// <summary>The same inks with every text ink passed through <paramref name="text"/> (the tones are kept).</summary>
    public StateInks Map(Func<Vector4, Vector4> text)
    {
        var mapped = new Vector4[texts.Length];
        for (var i = 0; i < texts.Length; i++)
        {
            mapped[i] = text(texts[i]);
        }

        return new StateInks(tones, mapped);
    }
}

/// <summary>
/// The drawn scene of a palette (Night values in brackets): the Full sky, the status bar's ground, the star field and
/// its inks, and the tokens that assume a dark window. A light palette turns the stars and the night grades off and
/// draws lighter shadows and glows.
/// </summary>
/// <param name="Zenith">Full's sky at the top of a pane (#1B2552); the window's top colour where none is designed.</param>
/// <param name="StatusTop">The status bar's ground at Full, top (#0E1329).</param>
/// <param name="StatusFoot">The status bar's ground at Full, foot (#0A0E1C).</param>
/// <param name="Shadow">Drop shadows under cards, medals and plates (Abyss).</param>
/// <param name="ShadowStrength">Multiplies every shadow's designed alpha (1 on a dark palette).</param>
/// <param name="GlowStrength">Multiplies every gold glow's and sheen's designed alpha (1 on a dark palette).</param>
/// <param name="TextHalo">The halo behind text drawn over brass or sky (Abyss).</param>
/// <param name="Scrim">The tour's dimming veil over the window (Night).</param>
/// <param name="Moonlight">The banner grade's wash and moon road, and the meteors (MoonHigh).</param>
/// <param name="StarField">Whether Full draws the star field and the meteors (on for a dark palette, off for a light one).</param>
/// <param name="NightGrade">Whether banners and giver portraits take the night multiply (off for a light palette: a daylight grade).</param>
/// <param name="Stars">The star field's inks.</param>
public readonly record struct SceneTokens(
    Vector4 Zenith,
    Vector4 StatusTop,
    Vector4 StatusFoot,
    Vector4 Shadow,
    float ShadowStrength,
    float GlowStrength,
    Vector4 TextHalo,
    Vector4 Scrim,
    Vector4 Moonlight,
    bool StarField,
    bool NightGrade,
    StarInks Stars)
{
    /// <summary>#1B2552 – Full's sky zenith on Night (docs/design/flair-v13 §1).</summary>
    public const uint ZenithHex = 0x1B2552;

    /// <summary>#0E1329 – the status bar's Deep gradient at Full, top.</summary>
    public const uint StatusTopHex = 0x0E1329;

    /// <summary>#0A0E1C – the status bar's Deep gradient at Full, foot.</summary>
    public const uint StatusFootHex = 0x0A0E1C;

    /// <summary>
    /// The scene a derived palette (Follow Dalamud) draws on <paramref name="surface"/>: on a dark window Night's
    /// (stars, night grades, Abyss shadows), with the sky and status bar from the surface; on a light window the
    /// daylight hook (no stars, no night multiply, shadows and glows at about half strength, a light text halo).
    /// </summary>
    public static SceneTokens Derived(in SurfaceColors surface)
    {
        var night = UiPalettes.NightScene;
        if (!surface.Light)
        {
            return night with { Zenith = surface.Top, StatusTop = surface.Deep, StatusFoot = surface.Deep };
        }

        return night with
        {
            Zenith = surface.Top,
            StatusTop = surface.Deep,
            StatusFoot = surface.Deep,
            Shadow = ColorMath.Mix(surface.Text, GlyphTokens.Abyss, 0.5f),
            ShadowStrength = LightShadowStrength,
            GlowStrength = LightGlowStrength,
            TextHalo = surface.Window,
            StarField = false,
            NightGrade = false,
        };
    }

    /// <summary>A light palette's shadows, as a share of the designed (dark) alpha.</summary>
    public const float LightShadowStrength = 0.45f;

    /// <summary>A light palette's gold glows and sheens, as a share of the designed alpha.</summary>
    public const float LightGlowStrength = 0.6f;
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
/// The Decoration brass of a palette (docs/design/flair-v13 §1, "Card frame"): the 160° ramp, lit from the upper left,
/// and the corner marks. Frame kits (plan v7 T11) will choose among metals; until then each palette carries one.
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

/// <summary>Full's lit raised pills (docs/design/flair-v13 §1, "Action pills"), top to foot.</summary>
/// <param name="RaisedTop">The raised pill's top (#252B45).</param>
/// <param name="RaisedFoot">The raised pill's foot (#1C2138).</param>
public readonly record struct PillSurfaces(Vector4 RaisedTop, Vector4 RaisedFoot)
{
    public const uint RaisedTopHex = 0x252B45;
    public const uint RaisedFootHex = 0x1C2138;
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
