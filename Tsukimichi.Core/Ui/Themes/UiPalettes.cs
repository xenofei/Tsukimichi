using System.Numerics;
using Tsukimichi.Core.Model;
using M = Tsukimichi.Core.Ui.GlyphTokens.Medallion;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The palette registry (plan v7 T2 and T8, theme-system §8, docs/design/v7/ui/spec-1.16.md §A): the designed palettes
/// by key, and the "Follow Dalamud" palette derived from the host style. 1.16 ships Night and Ishgard Snow, the first
/// light palette, each with a designed high-contrast form; Dawn and Kugane Lacquer join in 1.17 (T16). Every role is the
/// hex in <c>docs/design/v7/ui/1.16/palettes.json</c>: <c>UiPaletteTests</c> holds each to it, and
/// <c>PaletteContrastTests</c> every pair to WCAG.
/// </summary>
public static class UiPalettes
{
    // ------------------------------------------------------------------ Night

    /// <summary>Night's scene (the Full sky, stars, Abyss shadows, the night grades). Initialised before <see cref="Night"/>.</summary>
    public static readonly SceneTokens NightScene = new(
        Zenith: ColorMath.FromHex(SceneTokens.ZenithHex),
        SkyStops: null,
        StatusTop: ColorMath.FromHex(SceneTokens.StatusTopHex),
        StatusFoot: ColorMath.FromHex(SceneTokens.StatusFootHex),
        Shadow: GlyphTokens.Abyss,
        ShadowInk: new Vector4(0f, 0f, 0f, 1f),
        ShadowStrength: 1f,
        GlowStrength: 1f,
        WashInsteadOfGlow: false,
        GlowWash: GlyphTokens.Moon,
        TopHighlight: GlyphTokens.MoonHigh,
        TextHalo: GlyphTokens.Abyss,
        Scrim: GlyphTokens.Night,
        Moonlight: GlyphTokens.MoonHigh,
        BannerTitle: ColorMath.FromHex(SceneTokens.BannerTitleHex),
        StarField: true,
        NightGrade: true,
        Stars: new StarInks(
            ColorMath.FromHex(StarInks.CoolHex),
            ColorMath.FromHex(StarInks.MoonWhiteHex),
            ColorMath.FromHex(StarInks.GoldHex),
            ColorMath.FromHex(StarInks.EmberHex),
            ColorMath.FromHex(StarInks.FigureHex),
            ColorMath.FromHex(StarInks.BandHex)),
        TopHighlightAlpha: 0f,
        Washes: WashTokens.Night);

    /// <summary>Night's chrome inks.</summary>
    public static readonly PaletteInks NightInks = new(
        Gold: GlyphTokens.Moon,
        GoldHigh: GlyphTokens.MoonHigh,
        GoldDeep: GlyphTokens.MoonDeep,
        GoldDim: GlyphTokens.MoonDim,
        GoldLine: GlyphTokens.Moon,
        OnGold: GlyphTokens.Night,
        Danger: GlyphTokens.Eclipse,
        DangerText: GlyphTokens.EclipseText,
        OnDanger: GlyphTokens.Silver,
        UnknownText: GlyphTokens.VeilText,
        GaugeArc: ColorMath.FromHex(PaletteInks.GaugeArcHex),
        GaugeDone: ColorMath.FromHex(PaletteInks.GaugeDoneHex),
        ToggleOn: GlyphTokens.Moon,
        ToggleKnob: GlyphTokens.MoonHigh);

    /// <summary>Night's brass.</summary>
    public static readonly BrassTokens NightBrass = new(
        ColorMath.FromHex(BrassTokens.HighHex),
        GlyphTokens.Gilt,
        ColorMath.FromHex(BrassTokens.ShadowHex),
        ColorMath.FromHex(BrassTokens.ReflectedHex),
        ColorMath.FromHex(BrassTokens.DeepHex),
        ColorMath.FromHex(BrassTokens.CornerLitHex),
        ColorMath.FromHex(BrassTokens.CornerShadedHex));

    /// <summary>Night's portrait plate (1.15 spec A4).</summary>
    public static readonly PlateTokens NightPlate = new(
        WellTop: GlyphTokens.Medallion.Enamel,
        WellFoot: GlyphTokens.Medallion.EnamelDeep,
        PlainWell: ColorMath.FromHex(PlateTokens.PlainWellHex),
        PlainKeyline: ColorMath.FromHex(PlateTokens.PlainKeylineHex),
        QuietKeyline: ColorMath.FromHex(PlateTokens.QuietKeylineHex),
        Initials: ColorMath.FromHex(PortraitPlate.InitialsHex),
        OuterRing: GlyphTokens.Abyss,
        KeylineLit: GlyphTokens.Medallion.GiltHigh,
        KeylineMid: GlyphTokens.Medallion.GiltMid,
        KeylineLow: GlyphTokens.Medallion.GiltShade,
        KeylineDark: GlyphTokens.Medallion.GiltDeep);

    /// <summary>Night's lit pills: the raised gradient and the gold pill material.</summary>
    public static readonly PillSurfaces NightPills = new(
        ColorMath.FromHex(PillSurfaces.RaisedTopHex),
        ColorMath.FromHex(PillSurfaces.RaisedFootHex),
        ColorMath.FromHex(PillSurfaces.GoldTopHex),
        ColorMath.FromHex(PillSurfaces.GoldFootHex),
        ColorMath.FromHex(PillSurfaces.GoldEdgeHex),
        ColorMath.FromHex(PillSurfaces.GoldInkHex));

    /// <summary>
    /// Night's gauges (feature plan v6 G5, as 1.12 drew them): a lapis groove between Abyss keylines, a gilt arc lit as the
    /// medal's bezel is (two slopes meeting at a crest), a moonstone pearl, the filling moon in moonstone over its dark side.
    /// </summary>
    public static readonly GaugeInks NightGauges = new(
        Groove: M.LapisSeaBottom,
        Keyline: M.Keyline,
        ArcBase: M.Gilt,
        ArcDim: M.GiltShade,
        OuterSlope: [(0f, M.GiltHigh), (0.42f, M.GiltMid), (0.78f, M.GiltShade), (1f, M.GiltDeep)],
        InnerSlope: [(0f, M.GiltDeep), (0.55f, M.GiltShade), (1f, M.Gilt)],
        Knob: M.MoonstoneSpecular,
        KnobRim: M.Keyline,
        MoonGlint: M.MoonstoneSpecular,
        MoonLit: M.MoonstoneHigh,
        MoonBody: M.Moonstone,
        MoonDim: M.MoonstoneMid,
        DarkSide: GlyphTokens.MedallionDetail.DarkSide,
        Track: SurfaceColors.Night.StrongLine with { W = 0.55f },
        Arc: GlyphTokens.Moon,
        Glows: true);

    /// <summary>#97A0BA – Night high contrast's Not checked word (7 : 1 on the window; spec-1.16 §A8).</summary>
    public const uint NightHighContrastNotCheckedHex = 0x97A0BA;

    /// <summary>
    /// Night (ui-revamp §4.3, the Moon Road, flair-v13): the default palette, and the colours every release up to 1.15
    /// drew with, but for spec-1.16 §A2's two contrast fixes. Its high-contrast form is the spec's (§A3, §A8):
    /// <see cref="NightHighContrast"/>.
    /// </summary>
    public static readonly UiPalette Night = new()
    {
        Id = PaletteId.Night,
        Key = PaletteChoices.Night.Key,
        Name = PaletteChoices.Night.Name,
        Surface = SurfaceColors.Night,
        Accent = GlyphTokens.Moon,
        AccentDim = GlyphTokens.MoonDim,
        OrnamentLight = SurfaceColors.Night.OrnamentLight,
        Inks = NightInks,
        States = StateInks.Compose(GlyphTokens.Moon, GlyphTokens.MoonDim, GlyphTokens.Moon, GlyphTokens.Silver, GlyphTokens.Dusk, GlyphTokens.Mist, GlyphTokens.Eclipse, GlyphTokens.EclipseText, GlyphTokens.Veil, GlyphTokens.VeilText),
        Scene = NightScene,
        Brass = NightBrass,
        Plate = NightPlate,
        Gauges = NightGauges,
        Pills = NightPills,
        QuietTones = FlairTones.NightQuiet,
        PlainTones = FlairTones.NightPlain,
        DrawerTones = Ui.DrawerTones.NightSet,
        HighContrastBuilder = NightHighContrast,
    };

    /// <summary>
    /// Night's high-contrast form (spec-1.16 §A3, palettes.json "night-hc"): no sky, the ornament in the strong line
    /// #7C86A8 (opaque), secondary text #C3CBDF, tertiary #A0A9C4, Cool #86A1D7 and the Not checked word #97A0BA, so every
    /// ink reads at 7 : 1 on the window; the rest is Night's.
    /// </summary>
    public static UiPalette NightHighContrast(UiPalette night)
    {
        ArgumentNullException.ThrowIfNull(night);
        var form = night.ToHighContrast(pushInks: false);
        var strong = ColorMath.FromHex(0x7C86A8);
        var notChecked = ColorMath.FromHex(NightHighContrastNotCheckedHex);
        return form with
        {
            Surface = form.Surface with
            {
                StrongLine = strong,
                Ornament = strong,
                TextSecondary = ColorMath.FromHex(0xC3CBDF),
                TextTertiary = ColorMath.FromHex(0xA0A9C4),
                Cool = ColorMath.FromHex(0x86A1D7),
            },
            Inks = form.Inks with { UnknownText = notChecked },
            States = form.States.WithText(QuestState.Unknown, notChecked),
        };
    }

    // ------------------------------------------------------------------ Ishgard Snow

    /// <summary>#EEF1F6 – Ishgard Snow's window.</summary>
    public const uint SnowWindowHex = 0xEEF1F6;

    /// <summary>#1A2136 – Ishgard Snow's navy ink: text, and the colour its shadows are cast in.</summary>
    public const uint SnowInkHex = 0x1A2136;

    /// <summary>#755308 – Ishgard Snow's deep gold: Moon as text (Accent, the Ready and In journal words).</summary>
    public const uint SnowAccentHex = 0x755308;

    /// <summary>#962A6A – Ishgard Snow's plum: the Locked out word, stripe and danger tone.</summary>
    public const uint SnowLockedOutHex = 0x962A6A;

    /// <summary>
    /// A light palette's shadows: navy at about a third of Night's alpha (spec-1.16 §A4, decision 2).
    /// </summary>
    public const float SnowShadowStrength = 1f / 3f;

    /// <summary>Ishgard Snow's surface and text roles (spec-1.16 §A2).</summary>
    public static readonly SurfaceColors SnowSurface = new(
        Window: ColorMath.FromHex(SnowWindowHex),
        Sunken: ColorMath.FromHex(0xE1E6EE),
        Raised: ColorMath.FromHex(0xF9FAFC),
        Hover: ColorMath.FromHex(0xDCE3ED),
        Line: ColorMath.FromHex(0xCAD2DF),
        StrongLine: ColorMath.FromHex(0x7A859C),
        Text: ColorMath.FromHex(SnowInkHex),
        TextSecondary: ColorMath.FromHex(0x434D6A),
        TextTertiary: ColorMath.FromHex(0x56607C),
        TextDisabled: ColorMath.FromHex(0x8A93AA),
        Light: true,
        Deep: ColorMath.FromHex(0xD9DFE9),
        Top: ColorMath.FromHex(0xF8FAFD),
        Ornament: ColorMath.FromHex(0x7C8498),
        OrnamentHigh: ColorMath.FromHex(0x59627A),
        Cool: ColorMath.FromHex(0x2C569E),
        CoolDeep: ColorMath.FromHex(0xD3DEF0));

    /// <summary>
    /// Ishgard Snow's dawn over the Coerthas snowfield (spec-1.16 §A5): zenith #D3DEF0, a faint rose horizon at 42 %, the
    /// snow brighter low down because it reflects the sky.
    /// </summary>
    public static readonly (float At, Vector4 Color)[] SnowSky =
    [
        (0f, ColorMath.FromHex(0xD3DEF0)),
        (0.14f, ColorMath.FromHex(0xDCE5F3)),
        (0.30f, ColorMath.FromHex(0xE7ECF4)),
        (0.42f, ColorMath.FromHex(0xEFEAEC)),
        (0.56f, ColorMath.FromHex(SnowWindowHex)),
        (0.80f, ColorMath.FromHex(0xF1F3F7)),
        (1f, ColorMath.FromHex(0xF4F6F9)),
    ];

    /// <summary>
    /// Ishgard Snow's scene (spec-1.16 §A4–A5): a still dawn with no stars at all (no star field, moving sky, meteor, Milky
    /// Way or constellations), navy shadows at a third of Night's alpha, warm washes where Night glows and nothing else
    /// glowing, a white top highlight, navy titles on art and the daylight grade.
    /// </summary>
    public static readonly SceneTokens SnowScene = NightScene with
    {
        Zenith = SnowSky[0].Color,
        SkyStops = SnowSky,
        StatusTop = ColorMath.FromHex(0xE6EBF2),
        StatusFoot = ColorMath.FromHex(0xDDE3EC),
        Shadow = ColorMath.FromHex(SnowInkHex),
        ShadowInk = ColorMath.FromHex(SnowInkHex),
        ShadowStrength = SnowShadowStrength,
        GlowStrength = 0f,
        WashInsteadOfGlow = true,
        GlowWash = ColorMath.FromHex(WashTokens.WashHex),
        TopHighlight = Vector4.One,
        TopHighlightAlpha = SceneTokens.LightTopHighlightAlpha,
        TextHalo = Vector4.One,
        Scrim = ColorMath.FromHex(SnowInkHex),
        Moonlight = Vector4.One,
        BannerTitle = ColorMath.FromHex(SnowInkHex),
        StarField = false,
        NightGrade = false,
        Washes = WashTokens.Light(ColorMath.FromHex(WashTokens.LeadGoldHex)),
    };

    /// <summary>
    /// Ishgard Snow's chrome inks: gold stays gold, its lightness shifted to read on snow (spec-1.16 §A1, §A6). Gold fills
    /// and marks in lead gold #AC8324 (3.1 : 1), its highlight Moon itself, the deep gold #755308, plum for danger.
    /// </summary>
    public static readonly PaletteInks SnowInks = new(
        Gold: ColorMath.FromHex(WashTokens.LeadGoldHex),
        GoldHigh: GlyphTokens.Moon,
        GoldDeep: ColorMath.FromHex(SnowAccentHex),
        GoldDim: ColorMath.FromHex(0xB9A06A),
        GoldLine: ColorMath.FromHex(WashTokens.LeadGoldHex),
        OnGold: ColorMath.FromHex(0x2A1E05),
        Danger: ColorMath.FromHex(SnowLockedOutHex),
        DangerText: ColorMath.FromHex(SnowLockedOutHex),
        OnDanger: Vector4.One,
        UnknownText: ColorMath.FromHex(0x56607C),
        GaugeArc: ColorMath.FromHex(GaugeInks.LightArcHighHex),
        GaugeDone: ColorMath.FromHex(GaugeInks.LightArcShadeHex),
        ToggleOn: ColorMath.FromHex(0xEDD48C),
        ToggleKnob: ColorMath.FromHex(SnowAccentHex));

    /// <summary>
    /// Ishgard Snow's state inks (spec-1.16 §A2, palettes.json "snow"): the gold words in deep gold, Completed #6B5420,
    /// the other-job and Done words in navy, Blocked #434D6A, Locked out plum #962A6A, Not checked #56607C; the gold
    /// stripe #A07B25 (3 : 1), Completed's faded #B9A06A, the silver states' lead #59627A, Locked out plum (dashed) and the
    /// grey #8A93AA (dotted). The tones (a badge beside a glyph) follow the stripes.
    /// </summary>
    public static readonly StateInks SnowStates = StateInks.From(
    [
        (QuestState.Completed, ColorMath.FromHex(0xB9A06A), ColorMath.FromHex(0x6B5420), ColorMath.FromHex(0xB9A06A)),
        (QuestState.Accepted, ColorMath.FromHex(0xA07B25), ColorMath.FromHex(SnowAccentHex), ColorMath.FromHex(0xA07B25)),
        (QuestState.Ready, ColorMath.FromHex(0xA07B25), ColorMath.FromHex(SnowAccentHex), ColorMath.FromHex(0xA07B25)),
        (QuestState.ReadyOnOtherJob, ColorMath.FromHex(0x59627A), ColorMath.FromHex(SnowInkHex), ColorMath.FromHex(0x59627A)),
        (QuestState.DoneThisCycle, ColorMath.FromHex(0x59627A), ColorMath.FromHex(0x2A3454), ColorMath.FromHex(0x59627A)),
        (QuestState.Blocked, ColorMath.FromHex(0x8A93AA), ColorMath.FromHex(0x434D6A), ColorMath.FromHex(0x8A93AA)),
        (QuestState.Foreclosed, ColorMath.FromHex(SnowLockedOutHex), ColorMath.FromHex(SnowLockedOutHex), ColorMath.FromHex(SnowLockedOutHex)),
        (QuestState.Unknown, ColorMath.FromHex(0x8A93AA), ColorMath.FromHex(0x56607C), ColorMath.FromHex(0x8A93AA)),
    ]);

    /// <summary>
    /// Ishgard Snow's frame metal: the Came kit's lead (spec-1.16 §A6), a 1 px ramp lit from the upper left
    /// #B8C0D0 → #7C8498 → #5A6278 → #7C8498 → #4A5268, the corner marks #59627A on top and #4A5268 below. Brass frames on
    /// Snow (deep-gilt headings #6E5320) come with frames as a choice (T11, 1.17).
    /// </summary>
    public static readonly BrassTokens SnowLead = new(
        ColorMath.FromHex(0xB8C0D0),
        ColorMath.FromHex(0x7C8498),
        ColorMath.FromHex(0x5A6278),
        ColorMath.FromHex(0x7C8498),
        ColorMath.FromHex(0x4A5268),
        ColorMath.FromHex(0x59627A),
        ColorMath.FromHex(0x4A5268));

    /// <summary>
    /// Ishgard Snow's portrait plate (spec-1.16 §A7): a pale well #DCE3EE → #C8D1E0, a lead keyline ramp with a white outer
    /// line at .9, Quiet's hairline #7A859C at .8, fallback initials in #56607C.
    /// </summary>
    public static readonly PlateTokens SnowPlate = new(
        WellTop: ColorMath.FromHex(0xDCE3EE),
        WellFoot: ColorMath.FromHex(0xC8D1E0),
        PlainWell: ColorMath.FromHex(0xE1E6EE),
        PlainKeyline: ColorMath.FromHex(0x9AA4B8),
        QuietKeyline: ColorMath.FromHex(0x7A859C) with { W = 0.8f },
        Initials: ColorMath.FromHex(0x56607C),
        OuterRing: Vector4.One with { W = 0.9f },
        KeylineLit: ColorMath.FromHex(0xB8C0D0),
        KeylineMid: ColorMath.FromHex(0x7C8498),
        KeylineLow: ColorMath.FromHex(0x5A6278),
        KeylineDark: ColorMath.FromHex(0x4A5268));

    /// <summary>
    /// Ishgard Snow's lit pills (spec-1.16 §A6): white to snow for the raised pill, and the gold pill #F8DE96 → #D6AE52
    /// with a #A88437 edge and #2A1E05 ink (10 : 1).
    /// </summary>
    public static readonly PillSurfaces SnowPills = new(
        Vector4.One,
        ColorMath.FromHex(SnowWindowHex),
        ColorMath.FromHex(0xF8DE96),
        ColorMath.FromHex(0xD6AE52),
        ColorMath.FromHex(0xA88437),
        ColorMath.FromHex(0x2A1E05));

    /// <summary>
    /// A light palette's gauges (spec-1.16 §A6, the supervisor's ruling): a gilt ramp #8A6A1C (highlight, upper left) →
    /// #755308 (shade, lower right) on both slopes, a #CAD2DF groove between #7A859C keylines, a #8A6A1C knob with a
    /// #F9FAFC rim, the filling moon in moonstone #C3CEE4 over a #59627A dark side, and no glow.
    /// </summary>
    public static readonly GaugeInks LightGauges = new(
        Groove: ColorMath.FromHex(GaugeInks.LightGrooveHex),
        Keyline: ColorMath.FromHex(GaugeInks.LightKeylineHex),
        ArcBase: ColorMath.FromHex(GaugeInks.LightArcHighHex),
        ArcDim: ColorMath.FromHex(GaugeInks.LightArcShadeHex),
        OuterSlope: [(0f, ColorMath.FromHex(GaugeInks.LightArcHighHex)), (1f, ColorMath.FromHex(GaugeInks.LightArcShadeHex))],
        InnerSlope: [(0f, ColorMath.FromHex(GaugeInks.LightArcShadeHex)), (1f, ColorMath.FromHex(GaugeInks.LightArcHighHex))],
        Knob: ColorMath.FromHex(GaugeInks.LightArcHighHex),
        KnobRim: ColorMath.FromHex(GaugeInks.LightKnobRimHex),
        MoonGlint: M.MoonstoneHigh,
        MoonLit: ColorMath.FromHex(GaugeInks.LightMoonstoneHex),
        MoonBody: ColorMath.FromHex(GaugeInks.LightMoonstoneHex),
        MoonDim: M.MoonstoneMid,
        DarkSide: ColorMath.FromHex(GaugeInks.LightDarkSideHex),
        Track: ColorMath.FromHex(GaugeInks.LightKeylineHex),
        Arc: ColorMath.FromHex(GaugeInks.LightArcHighHex),
        Glows: false);

    /// <summary>
    /// Quiet on Ishgard Snow (spec-1.16 §A6, research §8.2): rail #E3E8F0, tree #E8ECF2, table #EEF1F6, detail #F3F5F9,
    /// cards #FAFBFD, rule #D3DAE5.
    /// </summary>
    public static readonly FlairTones SnowQuiet = new(
        Rail: ColorMath.FromHex(0xE3E8F0),
        Tree: ColorMath.FromHex(0xE8ECF2),
        Table: ColorMath.FromHex(SnowWindowHex),
        Detail: ColorMath.FromHex(0xF3F5F9),
        Card: ColorMath.FromHex(0xFAFBFD),
        Rule: ColorMath.FromHex(0xD3DAE5),
        Band: ColorMath.FromHex(0xFAFBFD),
        HeaderBand: ColorMath.FromHex(0xFAFBFD),
        HeaderLine: ColorMath.FromHex(0xD3DAE5),
        Status: ColorMath.FromHex(SnowWindowHex));

    /// <summary>
    /// Plain on Ishgard Snow (spec-1.16 §A6, the mock): the flat snow, band #E3E8F0, lines #D3DAE5, the header band's
    /// dividers #C3CBDA, the rail and status bar on #E1E6EE.
    /// </summary>
    public static readonly FlairTones SnowPlain = new(
        Rail: ColorMath.FromHex(0xE1E6EE),
        Tree: ColorMath.FromHex(SnowWindowHex),
        Table: ColorMath.FromHex(SnowWindowHex),
        Detail: ColorMath.FromHex(SnowWindowHex),
        Card: ColorMath.FromHex(SnowWindowHex),
        Rule: ColorMath.FromHex(0xD3DAE5),
        Band: ColorMath.FromHex(0xE3E8F0),
        HeaderBand: ColorMath.FromHex(0xE3E8F0),
        HeaderLine: ColorMath.FromHex(0xC3CBDA),
        Status: ColorMath.FromHex(0xE1E6EE));

    /// <summary>
    /// The filter drawer on Ishgard Snow (the mock, snow-*-drawer): at Full a snow sheet #FCFDFE → #F4F6FA under the lead
    /// edge with Section heads in #3F4862; at Quiet a flat #F7F8FB sheet with a #C3CBDA edge; at Plain #F3F5F9 with a
    /// #E1E6EE footer band. Neutral pills and hovers #E3E8F0.
    /// </summary>
    public static readonly DrawerToneSet SnowDrawer = new(
        Full: new DrawerTones(ColorMath.FromHex(0xFCFDFE), ColorMath.FromHex(0xF4F6FA), ColorMath.FromHex(0xCAD2DF), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(0xFCFDFE), ColorMath.FromHex(0xF4F6FA), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(0x3F4862)),
        Quiet: new DrawerTones(ColorMath.FromHex(0xF7F8FB), ColorMath.FromHex(0xF7F8FB), ColorMath.FromHex(0xC3CBDA), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(0xF7F8FB), ColorMath.FromHex(0xF7F8FB), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(SnowInkHex)),
        Plain: new DrawerTones(ColorMath.FromHex(0xF3F5F9), ColorMath.FromHex(0xF3F5F9), ColorMath.FromHex(0xC3CBDA), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(0xE1E6EE), ColorMath.FromHex(0xE3E8F0), ColorMath.FromHex(SnowInkHex)));

    /// <summary>
    /// Ishgard Snow (spec-1.16 §A): the first light palette, snow under a still dawn, navy ink, deep gold for "act now"
    /// as text, the Came kit's lead for the ornament. Medals keep their own enamel and are never recoloured. Its
    /// high-contrast form is the spec's (<see cref="SnowHighContrast"/>).
    /// </summary>
    public static readonly UiPalette IshgardSnow = new()
    {
        Id = PaletteId.IshgardSnow,
        Key = PaletteChoices.IshgardSnow.Key,
        Name = PaletteChoices.IshgardSnow.Name,
        Surface = SnowSurface,
        Accent = ColorMath.FromHex(SnowAccentHex),
        AccentDim = ColorMath.FromHex(0x6B5420),
        OrnamentLight = ColorMath.FromHex(0x3F4862),
        Inks = SnowInks,
        States = SnowStates,
        Scene = SnowScene,
        Brass = SnowLead,
        Plate = SnowPlate,
        Gauges = LightGauges,
        Zebra = ColorMath.FromHex(SnowInkHex) with { W = 0.03f },
        Pills = SnowPills,
        QuietTones = SnowQuiet,
        PlainTones = SnowPlain,
        DrawerTones = SnowDrawer,
        HighContrastBuilder = SnowHighContrast,
    };

    /// <summary>
    /// Ishgard Snow's high-contrast form (spec-1.16 §A3, palettes.json "snow-hc"): no sky; lines #9AA4B8, the strong line
    /// and the opaque ornament #4A5470; text #0B1020, secondary #2A3350, tertiary #3A4462; Cool #294F91, the accent
    /// #694C0B and every state word pushed to 7 : 1 on the window (Completed #624E20, Locked out #8E2866, Not checked
    /// #47506A); the faint stripes to 3 : 1 (Completed #9D895E, the grey #828BA2).
    /// </summary>
    public static UiPalette SnowHighContrast(UiPalette snow)
    {
        ArgumentNullException.ThrowIfNull(snow);
        var form = snow.ToHighContrast(pushInks: false);
        var strong = ColorMath.FromHex(0x4A5470);
        var accent = ColorMath.FromHex(0x694C0B);
        var completed = ColorMath.FromHex(0x624E20);
        var locked = ColorMath.FromHex(0x8E2866);
        var notChecked = ColorMath.FromHex(0x47506A);
        var grey = ColorMath.FromHex(0x828BA2);
        var states = snow.States;
        return form with
        {
            Surface = form.Surface with
            {
                Line = ColorMath.FromHex(0x9AA4B8),
                StrongLine = strong,
                Ornament = strong,
                Text = ColorMath.FromHex(0x0B1020),
                TextSecondary = ColorMath.FromHex(0x2A3350),
                TextTertiary = ColorMath.FromHex(0x3A4462),
                Cool = ColorMath.FromHex(0x294F91),
            },
            Accent = accent,
            AccentDim = completed,
            Inks = form.Inks with { DangerText = locked, UnknownText = notChecked },
            States = StateInks.From(
            [
                (QuestState.Completed, states.Tone(QuestState.Completed), completed, ColorMath.FromHex(0x9D895E)),
                (QuestState.Accepted, states.Tone(QuestState.Accepted), accent, states.Stripe(QuestState.Accepted)),
                (QuestState.Ready, states.Tone(QuestState.Ready), accent, states.Stripe(QuestState.Ready)),
                (QuestState.ReadyOnOtherJob, states.Tone(QuestState.ReadyOnOtherJob), states.Text(QuestState.ReadyOnOtherJob), states.Stripe(QuestState.ReadyOnOtherJob)),
                (QuestState.DoneThisCycle, states.Tone(QuestState.DoneThisCycle), states.Text(QuestState.DoneThisCycle), states.Stripe(QuestState.DoneThisCycle)),
                (QuestState.Blocked, states.Tone(QuestState.Blocked), states.Text(QuestState.Blocked), grey),
                (QuestState.Foreclosed, states.Tone(QuestState.Foreclosed), locked, states.Stripe(QuestState.Foreclosed)),
                (QuestState.Unknown, states.Tone(QuestState.Unknown), notChecked, grey),
            ]),
        };
    }

    // ------------------------------------------------------------------ the registry

    private static readonly UiPalette[] Designed = [Night, IshgardSnow];

    /// <summary>The designed palettes, in the order the Themes page lists them (Follow Dalamud is offered after them).</summary>
    public static IReadOnlyList<UiPalette> All => Designed;

    /// <summary>
    /// The designed palette for <paramref name="id"/> (the appearance's <see cref="ResolvedAppearance.Palette"/>); Night for
    /// an id with no palette registered yet (Dawn and Kugane Lacquer until 1.17) and for
    /// <see cref="PaletteId.FollowDalamud"/>, which is built from the host style (<see cref="FollowDalamud"/>).
    /// Allocates nothing.
    /// </summary>
    public static UiPalette Get(PaletteId id)
    {
        foreach (var palette in Designed)
        {
            if (palette.Id == id)
            {
                return palette;
            }
        }

        return Night;
    }

    /// <summary>Whether a designed palette is registered for <paramref name="id"/>.</summary>
    public static bool IsRegistered(PaletteId id) => Array.Exists(Designed, p => p.Id == id);

    /// <summary>
    /// The Follow Dalamud palette (the hook, not a designed palette): the surface roles mapped from the host style
    /// (<see cref="SurfaceColors.FromHost"/>), gold as text pushed until it reads at 4.5 : 1 on the window (a light host
    /// gets a deep gold), the state text inks taken from the surface, the danger and Unknown text pushed until they read,
    /// and the scene from the window's lightness (<see cref="SceneTokens.Derived"/>). Gold fills, the brass and the gold
    /// pill material keep Night's; the gauges are Night's on a dark host and the light gauges on a light one. No designed
    /// tones, so Quiet, Plain and the drawer are mixed from the surface, and no lit pills. Allocates one palette;
    /// <c>Theme</c> builds it only when the host colours change.
    /// </summary>
    public static UiPalette FollowDalamud(Vector4 windowBg, Vector4 frameBg, Vector4 frameBgHovered, Vector4 border, Vector4 text, Vector4 textDisabled)
    {
        var s = SurfaceColors.FromHost(windowBg, frameBg, frameBgHovered, border, text, textDisabled);
        const float min = SurfaceColors.TextMinContrast;
        Vector4 Read(Vector4 ink) => ColorMath.EnsureContrast(ink, s.Text, s.Window, min);
        var accent = Read(GlyphTokens.Moon);
        var inks = NightInks with
        {
            DangerText = Read(GlyphTokens.EclipseText),
            UnknownText = Read(GlyphTokens.VeilText),
            OnDanger = GlyphTokens.Silver,
        };
        return new UiPalette
        {
            Id = PaletteId.FollowDalamud,
            Key = PaletteChoices.FollowDalamud.Key,
            Name = PaletteChoices.FollowDalamud.Name,
            Surface = s,
            Accent = accent,
            AccentDim = Read(GlyphTokens.MoonDim),
            OrnamentLight = s.OrnamentLight,
            Inks = inks,
            States = StateInks.Compose(GlyphTokens.Moon, GlyphTokens.MoonDim, accent, s.Text, s.TextTertiary, s.TextSecondary, GlyphTokens.Eclipse, inks.DangerText, s.TextDisabled, inks.UnknownText),
            Scene = SceneTokens.Derived(s),
            Brass = NightBrass,
            Plate = NightPlate,
            Gauges = s.Light ? LightGauges : NightGauges with { Track = s.StrongLine with { W = 0.55f } },

            // As 1.15: the high-contrast surface roles alone; the accent already reads at 4.5 : 1 on the host.
            HighContrastBuilder = static p => p.ToHighContrast(pushInks: false),
        };
    }
}
