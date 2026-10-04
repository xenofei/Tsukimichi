using System.Numerics;
using Tsukimichi.Core.Model;
using M = Tsukimichi.Core.Ui.GlyphTokens.Medallion;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The palette registry (plan v7 T2 and T8, theme-system §8, docs/design/v7/ui/spec-1.16.md §A): the designed palettes
/// by key, and the "Follow Dalamud" palette derived from the host style. 1.16 ships Night and Ishgard Snow, the first
/// light palette; 1.17 adds the dark Dawn and Kugane Lacquer (T16, spec-1.17 §E); each has a designed high-contrast
/// form. Every role is the hex in <c>docs/design/v7/ui/1.16/palettes.json</c> or <c>1.17/palettes17.json</c>:
/// <c>IshgardSnowTests</c> and <c>DawnKuganeTests</c> hold each to it, and <c>PaletteContrastTests</c> every pair to WCAG.
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
    /// #D08654 – copper, "it needs you" (spec-1.18): Night, Dawn and Kugane Lacquer (5.3 : 1 on Night's cards, 5.4 on
    /// Dawn's, 5.9 on Kugane's). A new hue in the brass family, apart from gold (act now), plum (Locked out) and the
    /// Settings hint's amber.
    /// </summary>
    public const uint CopperHex = 0xD08654;

    /// <summary>#A8582A – Ishgard Snow's copper, deep enough to read at 4.5 : 1 on snow.</summary>
    public const uint SnowCopperHex = 0xA8582A;

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
        Copper = ColorMath.FromHex(CopperHex),
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
        MedalRimGap = MedalTokens.LightRimGap,
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
        Copper = ColorMath.FromHex(SnowCopperHex),
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
        MedalRimGap = SnowQuiet.Table,
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

    // ------------------------------------------------------------------ Dawn and Kugane Lacquer: the dark path

    /// <summary>
    /// Kugane Lacquer's Ready halo alpha (spec-1.17 §E4.1): <see cref="WashTokens.DarkReadyHaloAlpha"/> (.45), or
    /// <see cref="WashTokens.DarkReadyHaloRaisedAlpha"/> (.60) if the build's Ready-lead gate on the Kugane window records
    /// the raised halo for some set. The one value to change.
    /// </summary>
    public const float KuganeReadyHaloAlpha = WashTokens.DarkReadyHaloAlpha;

    /// <summary>Dawn's Ready halo alpha (the same rule as Kugane's: .45, or .60 if the build records the raised halo).</summary>
    public const float DawnReadyHaloAlpha = WashTokens.DarkReadyHaloAlpha;

    /// <summary>
    /// A dark palette's scene beside Night's (spec-1.17 §E1): the star field, moving sky and meteor, glows and the night
    /// grades all stay; the sky is its own designed stops, the status bar its own gradient, shadows and the text halo in
    /// its <paramref name="deep"/>, the tour's veil its <paramref name="window"/>, the lit edge its <paramref name="goldHigh"/>
    /// and glows in its <paramref name="gold"/>; Ready's row halo is the gated #F2D27A halo at <paramref name="readyHalo"/>.
    /// </summary>
    private static SceneTokens DarkScene((float At, Vector4 Color)[] sky, uint statusTop, uint statusFoot, Vector4 deep, Vector4 window, Vector4 gold, Vector4 goldHigh, float readyHalo) => NightScene with
    {
        Zenith = sky[0].Color,
        SkyStops = sky,
        StatusTop = ColorMath.FromHex(statusTop),
        StatusFoot = ColorMath.FromHex(statusFoot),
        Shadow = deep,
        TextHalo = deep,
        Scrim = window,
        GlowWash = gold,
        TopHighlight = goldHigh,
        Washes = WashTokens.Dark(gold, readyHalo),
        ReadyHaloWash = true,
    };

    /// <summary>
    /// A dark palette's chrome inks (spec-1.17 §E2): gold as fill, line and toggle; its highlight and deep stops; the
    /// faded Completed gold as the dim gold; the window as the ink on gold; the Locked out stripe as the danger tone and
    /// its word as danger text; the Not checked word; the tree ring in the gauge shade, done in the faded gold.
    /// </summary>
    private static PaletteInks DarkInks(Vector4 gold, uint goldHigh, uint goldDeep, uint goldDim, Vector4 window, uint danger, uint dangerText, Vector4 onDanger, uint unknownText) => new(
        Gold: gold,
        GoldHigh: ColorMath.FromHex(goldHigh),
        GoldDeep: ColorMath.FromHex(goldDeep),
        GoldDim: ColorMath.FromHex(goldDim),
        GoldLine: gold,
        OnGold: window,
        Danger: ColorMath.FromHex(danger),
        DangerText: ColorMath.FromHex(dangerText),
        OnDanger: onDanger,
        UnknownText: ColorMath.FromHex(unknownText),
        GaugeArc: ColorMath.FromHex(goldDeep),
        GaugeDone: ColorMath.FromHex(goldDim),
        ToggleOn: gold,
        ToggleKnob: ColorMath.FromHex(goldHigh));

    /// <summary>
    /// A dark palette's state inks (spec-1.17 §E2, palettes17.json): the gold states in its gold (Completed's stripe the
    /// faded gold), the silver states in its text, Blocked's word in secondary text over the tertiary tone, Locked out in
    /// its plum, Not checked's word; Blocked and Not checked share the veil stripe (the dotted grey), as on Snow.
    /// </summary>
    private static StateInks DarkStates(Vector4 gold, uint completedStripe, Vector4 text, in SurfaceColors s, uint lockedStripe, uint lockedText, uint notChecked, uint veilStripe)
    {
        var veil = ColorMath.FromHex(veilStripe);
        var locked = ColorMath.FromHex(lockedStripe);
        return StateInks.From(
        [
            (QuestState.Completed, gold, gold, ColorMath.FromHex(completedStripe)),
            (QuestState.Accepted, gold, gold, gold),
            (QuestState.Ready, gold, gold, gold),
            (QuestState.ReadyOnOtherJob, text, text, text),
            (QuestState.DoneThisCycle, text, text, text),
            (QuestState.Blocked, s.TextTertiary, s.TextSecondary, veil),
            (QuestState.Foreclosed, locked, ColorMath.FromHex(lockedText), locked),
            (QuestState.Unknown, s.TextDisabled, ColorMath.FromHex(notChecked), veil),
        ]);
    }

    /// <summary>
    /// A dark palette's gauges (spec-1.17 §E2): Night's medal gauges (they glow, with the moonstone moon over its dark
    /// side) in the palette's own ink, as a light palette's are: a two-stop gilt ramp <paramref name="arc"/> (upper left)
    /// → <paramref name="shade"/> (lower right), its own <paramref name="groove"/> between keylines in its deep, and the
    /// flat track in the strong line at .55.
    /// </summary>
    private static GaugeInks DarkGauges(uint arc, uint shade, uint groove, Vector4 deep, Vector4 strongLine)
    {
        var lit = ColorMath.FromHex(arc);
        var shaded = ColorMath.FromHex(shade);
        return NightGauges with
        {
            Groove = ColorMath.FromHex(groove),
            Keyline = deep,
            ArcBase = lit,
            ArcDim = shaded,
            OuterSlope = [(0f, lit), (1f, shaded)],
            InnerSlope = [(0f, shaded), (1f, lit)],
            KnobRim = deep,
            Track = strongLine with { W = 0.55f },
            Arc = lit,
        };
    }

    /// <summary>
    /// A dark palette's high-contrast form (spec-1.17 §E3, research §8.3): the generic transform from its own warm inks
    /// (no sky, the opaque strong-line ornament, every ink pushed towards its text until it reads 7 : 1 on the window),
    /// with the designed hexes of palettes17.json for the strong line and ornament, tertiary text and the Not checked word.
    /// </summary>
    private static UiPalette DarkHighContrast(UiPalette palette, uint strongLine, uint textTertiary, uint notChecked)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var form = palette.ToHighContrast(pushInks: true);
        var strong = ColorMath.FromHex(strongLine);
        var unknown = ColorMath.FromHex(notChecked);
        return form with
        {
            Surface = form.Surface with { StrongLine = strong, Ornament = strong, TextTertiary = ColorMath.FromHex(textTertiary) },
            Inks = form.Inks with { UnknownText = unknown },
            States = form.States.WithText(QuestState.Unknown, unknown),
        };
    }

    // ------------------------------------------------------------------ Dawn

    /// <summary>#1A1526 – Dawn's window, plum night.</summary>
    public const uint DawnWindowHex = 0x1A1526;

    /// <summary>#F5C47C – Dawn's gold: the accent, the Ready, In journal and Completed words, the gold stripe and gauge arc.</summary>
    public const uint DawnGoldHex = 0xF5C47C;

    /// <summary>Dawn's surface and text roles (spec-1.17 §E2; StrongLine and TextTertiary re-tuned for Hover and Raised).</summary>
    public static readonly SurfaceColors DawnSurface = new(
        Window: ColorMath.FromHex(DawnWindowHex),
        Sunken: ColorMath.FromHex(0x120E1B),
        Raised: ColorMath.FromHex(0x262036),
        Hover: ColorMath.FromHex(0x30283F),
        Line: ColorMath.FromHex(0x352D46),
        StrongLine: ColorMath.FromHex(0x76698C),
        Text: ColorMath.FromHex(0xF2E8E6),
        TextSecondary: ColorMath.FromHex(0xC4B4C0),
        TextTertiary: ColorMath.FromHex(0xA495AC),
        TextDisabled: ColorMath.FromHex(0x5A4E66),
        Light: false,
        Deep: ColorMath.FromHex(0x0F0B17),
        Top: ColorMath.FromHex(0x2B1F3A),
        Ornament: ColorMath.FromHex(0xB98C6E),
        OrnamentHigh: ColorMath.FromHex(0xE9C4A4),
        Cool: ColorMath.FromHex(0x92A2E4),
        CoolDeep: GlyphTokens.TideDeep);

    /// <summary>#5A3448 – Dawn's rose horizon, the brightest band of its sky.</summary>
    public const uint DawnHorizonHex = 0x5A3448;

    /// <summary>
    /// Dawn's sky, the hour before sunrise (spec-1.17 §E2, the mock's dawn-full): zenith #3A2746, the rose horizon
    /// #5A3448 between 34 and 46 %, down to the top colour #2B1F3A and the window by 76 %, a faint lift at the foot.
    /// </summary>
    public static readonly (float At, Vector4 Color)[] DawnSky =
    [
        (0f, ColorMath.FromHex(0x3A2746)),
        (0.14f, ColorMath.FromHex(0x352444)),
        (0.30f, ColorMath.FromHex(0x2B1F3A)),
        (0.40f, ColorMath.FromHex(0x3E2A44)),
        (0.46f, ColorMath.FromHex(DawnHorizonHex)),
        (0.58f, ColorMath.FromHex(0x2B1F3A)),
        (0.76f, ColorMath.FromHex(DawnWindowHex)),
        (1f, ColorMath.FromHex(0x1D1729)),
    ];

    /// <summary>
    /// Quiet on Dawn (spec-1.17 §E5): rail #17121F, tree #1A1524, table #1E1829, detail #221B30, cards #2A2338, rule
    /// #3A3049.
    /// </summary>
    public static readonly FlairTones DawnQuiet = new(
        Rail: ColorMath.FromHex(0x17121F),
        Tree: ColorMath.FromHex(0x1A1524),
        Table: ColorMath.FromHex(0x1E1829),
        Detail: ColorMath.FromHex(0x221B30),
        Card: ColorMath.FromHex(0x2A2338),
        Rule: ColorMath.FromHex(0x3A3049),
        Band: ColorMath.FromHex(0x2A2338),
        HeaderBand: ColorMath.FromHex(0x2A2338),
        HeaderLine: ColorMath.FromHex(0x3A3049),
        Status: ColorMath.FromHex(0x1E1829));

    /// <summary>
    /// Dawn (spec-1.17 §E): the hour before sunrise, plum night with a rose horizon, warm pearl text, dawn gold for "act
    /// now". A dark palette: stars, glows and the night grades stay. Medals are never recoloured. Its high-contrast form is
    /// the spec's (<see cref="DawnHighContrast"/>).
    /// </summary>
    public static readonly UiPalette Dawn = BuildDawn();

    private static UiPalette BuildDawn()
    {
        var s = DawnSurface;
        var gold = ColorMath.FromHex(DawnGoldHex);
        return new UiPalette
        {
            Id = PaletteId.Dawn,
            Key = PaletteChoices.Dawn.Key,
            Name = PaletteChoices.Dawn.Name,
            Surface = s,
            Accent = gold,
            AccentDim = ColorMath.FromHex(0xB99A6A),
            OrnamentLight = ColorMath.FromHex(0xEDCBAA),
            Copper = ColorMath.FromHex(CopperHex),

            // The danger button #9A5577 under Dawn's pearl text is 4.4 : 1; white reads at 5.3.
            Inks = DarkInks(gold, 0xFFE6B8, 0xD9A55E, 0xB99A6A, s.Window, 0xC46A92, 0xE68FB4, Vector4.One, 0xA595AE),
            States = DarkStates(gold, 0xB99A6A, s.Text, s, 0xC46A92, 0xE68FB4, 0xA595AE, 0x76698C),
            Scene = DarkScene(DawnSky, 0x1E1830, 0x140F20, s.Deep, s.Window, gold, ColorMath.FromHex(0xFFE6B8), DawnReadyHaloAlpha),
            Brass = NightBrass,
            Plate = NightPlate,
            Gauges = DarkGauges(DawnGoldHex, 0xD9A55E, 0x3A3050, s.Deep, s.StrongLine),
            Zebra = ColorMath.FromHex(0xDCB4C8) with { W = 0.025f },
            Pills = new PillSurfaces(s.Hover, s.Raised, ColorMath.FromHex(0xFFE2AE), ColorMath.FromHex(0xD9A55E), ColorMath.FromHex(0xF8D9A4), ColorMath.FromHex(PillSurfaces.GoldInkHex)),
            QuietTones = DawnQuiet,
            MedalRimGap = DawnQuiet.Tree,
            HighContrastBuilder = DawnHighContrast,
        };
    }

    /// <summary>
    /// Dawn's high-contrast form (spec-1.17 §E3, palettes17.json "dawn-hc"): no sky; the strong line and the opaque
    /// ornament #877B99, tertiary #AD9EB2, the Not checked word #AD9DB4; every other ink already reads 7 : 1 or is pushed
    /// there from Dawn's own warm inks.
    /// </summary>
    public static UiPalette DawnHighContrast(UiPalette dawn) => DarkHighContrast(dawn, 0x877B99, 0xAD9EB2, 0xAD9DB4);

    // ------------------------------------------------------------------ Kugane Lacquer

    /// <summary>#16100F – Kugane Lacquer's window, black lacquer.</summary>
    public const uint KuganeWindowHex = 0x16100F;

    /// <summary>#F0CC72 – Kugane Lacquer's gold.</summary>
    public const uint KuganeGoldHex = 0xF0CC72;

    /// <summary>#B23422 – vermilion: Kugane's dusk band and the rail's lacquer edge, never an ink (red means Locked out).</summary>
    public const uint VermilionHex = 0xB23422;

    /// <summary>Kugane Lacquer's surface and text roles (spec-1.17 §E2; StrongLine and TextTertiary re-tuned).</summary>
    public static readonly SurfaceColors KuganeSurface = new(
        Window: ColorMath.FromHex(KuganeWindowHex),
        Sunken: ColorMath.FromHex(0x0E0A09),
        Raised: ColorMath.FromHex(0x231917),
        Hover: ColorMath.FromHex(0x2D211E),
        Line: ColorMath.FromHex(0x342620),
        StrongLine: ColorMath.FromHex(0x7A6458),
        Text: ColorMath.FromHex(0xF3E9DB),
        TextSecondary: ColorMath.FromHex(0xC6B6A2),
        TextTertiary: ColorMath.FromHex(0xA69482),
        TextDisabled: ColorMath.FromHex(0x5C4C42),
        Light: false,
        Deep: ColorMath.FromHex(0x0B0807),
        Top: ColorMath.FromHex(0x2A1613),
        Ornament: ColorMath.FromHex(0xB8913F),
        OrnamentHigh: ColorMath.FromHex(0xE7C87C),
        Cool: ColorMath.FromHex(0x7FA3DA),
        CoolDeep: GlyphTokens.TideDeep);

    /// <summary>#4E2218 – Kugane's vermilion dusk band, the brightest of its sky.</summary>
    public const uint KuganeHorizonHex = 0x4E2218;

    /// <summary>
    /// Kugane Lacquer's sky, dusk over the port (spec-1.17 §E2, the mock's kugane-full): zenith #3A1A14, the vermilion
    /// dusk band #4E2218 between 40 and 46 %, down to the top colour #2A1613 and the window by 74 %.
    /// </summary>
    public static readonly (float At, Vector4 Color)[] KuganeSky =
    [
        (0f, ColorMath.FromHex(0x3A1A14)),
        (0.12f, ColorMath.FromHex(0x33170F)),
        (0.26f, ColorMath.FromHex(0x2A1613)),
        (0.40f, ColorMath.FromHex(0x3B1D14)),
        (0.45f, ColorMath.FromHex(KuganeHorizonHex)),
        (0.56f, ColorMath.FromHex(0x2A1613)),
        (0.74f, ColorMath.FromHex(KuganeWindowHex)),
        (1f, ColorMath.FromHex(0x1A1210)),
    ];

    /// <summary>
    /// Kugane's stars (the supervisor's accepted option, spec-1.17 decision 6): a hazier sky over a lantern-lit port, half
    /// as dense, its far stars warmed slightly (#E9E2DA and #F1E3CC for Night's cool and moon white).
    /// </summary>
    public static readonly StarInks KuganeStars = NightScene.Stars with
    {
        FarCool = ColorMath.FromHex(0xE9E2DA),
        FarMoon = ColorMath.FromHex(0xF1E3CC),
        Density = 0.5f,
    };

    /// <summary>
    /// Quiet on Kugane Lacquer (spec-1.17 §E5): rail #140E0D, tree #171110, table #1B1412, detail #201715, cards #281D1A,
    /// rule #3A2B24.
    /// </summary>
    public static readonly FlairTones KuganeQuiet = new(
        Rail: ColorMath.FromHex(0x140E0D),
        Tree: ColorMath.FromHex(0x171110),
        Table: ColorMath.FromHex(0x1B1412),
        Detail: ColorMath.FromHex(0x201715),
        Card: ColorMath.FromHex(0x281D1A),
        Rule: ColorMath.FromHex(0x3A2B24),
        Band: ColorMath.FromHex(0x281D1A),
        HeaderBand: ColorMath.FromHex(0x281D1A),
        HeaderLine: ColorMath.FromHex(0x3A2B24),
        Status: ColorMath.FromHex(0x1B1412));

    /// <summary>
    /// Kugane Lacquer (spec-1.17 §E): black lacquer at dusk, washi text, brass on lacquer, an ai-zome indigo for links.
    /// Vermilion lives only in surfaces (the dusk band, the rail's lacquer edge), never in ink. A dark palette: stars
    /// (half as dense, warmer far stars), glows and the night grades stay. Medals are never recoloured. Its high-contrast
    /// form is the spec's (<see cref="KuganeHighContrast"/>).
    /// </summary>
    public static readonly UiPalette KuganeLacquer = BuildKugane();

    private static UiPalette BuildKugane()
    {
        var s = KuganeSurface;
        var gold = ColorMath.FromHex(KuganeGoldHex);
        return new UiPalette
        {
            Id = PaletteId.KuganeLacquer,
            Key = PaletteChoices.KuganeLacquer.Key,
            Name = PaletteChoices.KuganeLacquer.Name,
            Surface = s,
            Accent = gold,
            AccentDim = ColorMath.FromHex(0xB79A5E),
            OrnamentLight = ColorMath.FromHex(0xECD08A),
            Copper = ColorMath.FromHex(CopperHex),
            Inks = DarkInks(gold, 0xFFE9B0, 0xD4AE55, 0xB79A5E, s.Window, 0xC25E92, 0xE58AC0, s.Text, 0xA89888),
            States = DarkStates(gold, 0xB79A5E, s.Text, s, 0xC25E92, 0xE58AC0, 0xA89888, 0x7A6458),
            Scene = DarkScene(KuganeSky, 0x1E1513, 0x120D0C, s.Deep, s.Window, gold, ColorMath.FromHex(0xFFE9B0), KuganeReadyHaloAlpha) with
            {
                Stars = KuganeStars,
                RailEdge = ColorMath.FromHex(VermilionHex) with { W = 0.45f },
            },
            Brass = NightBrass,
            Plate = NightPlate,
            Gauges = DarkGauges(KuganeGoldHex, 0xD4AE55, 0x3A2A24, s.Deep, s.StrongLine),
            Zebra = ColorMath.FromHex(0xF0C8A0) with { W = 0.022f },
            Pills = NightPills with { RaisedTop = s.Hover, RaisedFoot = s.Raised },
            QuietTones = KuganeQuiet,
            MedalRimGap = KuganeQuiet.Tree,
            HighContrastBuilder = KuganeHighContrast,
        };
    }

    /// <summary>
    /// Kugane Lacquer's high-contrast form (spec-1.17 §E3, palettes17.json "kugane-hc"): no sky and no lacquer edge; the
    /// strong line and the opaque ornament #8C786C, tertiary #AD9C8A, the Not checked word #AC9C8C; every other ink
    /// already reads 7 : 1 or is pushed there from Kugane's own warm inks.
    /// </summary>
    public static UiPalette KuganeHighContrast(UiPalette kugane) => DarkHighContrast(kugane, 0x8C786C, 0xAD9C8A, 0xAC9C8C);

    // ------------------------------------------------------------------ the registry

    /// <summary>The designed palettes in the Themes page's order (spec-1.16 §B6): Night, Ishgard Snow, Dawn, Kugane Lacquer.</summary>
    private static readonly UiPalette[] Designed = [Night, IshgardSnow, Dawn, KuganeLacquer];

    /// <summary>The designed palettes, in the order the Themes page lists them (Follow Dalamud is offered after them).</summary>
    public static IReadOnlyList<UiPalette> All => Designed;

    /// <summary>
    /// The designed palette for <paramref name="id"/> (the appearance's <see cref="ResolvedAppearance.Palette"/>); Night for
    /// an id with no palette registered (one a newer build adds) and for
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
    public static bool IsRegistered(PaletteId id)
    {
        foreach (var palette in Designed)
        {
            if (palette.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The Follow Dalamud palette (the hook, not a designed palette), held to the designed palettes' bars
    /// (<c>PaletteContrastTests</c>, on a dark and a light host): the surface roles mapped from the host style
    /// (<see cref="SurfaceColors.FromHost"/>); every text ink (secondary, tertiary, Cool, gold as text, the danger and
    /// Unknown text) pushed towards the host text until it reads at 4.5 : 1 on the worst of the window, cards, hovered rows
    /// and wells (a light host gets a deep gold); the strong line and the state stripes pushed the same way to 3 : 1; the
    /// state words taken from those inks; Silver on the destructive button, or white or the host text where Silver does
    /// not read; and the scene from the window's lightness (<see cref="SceneTokens.Derived"/>). Gold fills, the brass and
    /// the gold pill material keep Night's; the gauges and the tree ring are Night's on a dark host and the light gauges'
    /// gilt on a light one. No designed tones, so Quiet, Plain and the drawer are mixed from the surface, and no lit pills.
    /// <c>Theme</c> builds it only when the host colours change.
    /// </summary>
    public static UiPalette FollowDalamud(Vector4 windowBg, Vector4 frameBg, Vector4 frameBgHovered, Vector4 border, Vector4 text, Vector4 textDisabled)
    {
        var host = SurfaceColors.FromHost(windowBg, frameBg, frameBgHovered, border, text, textDisabled);
        const float min = SurfaceColors.TextMinContrast;
        const float line = SurfaceColors.LineMinContrast;
        var s = host with
        {
            StrongLine = OnEverySurface(host.StrongLine, host, line),
            TextSecondary = OnEverySurface(host.TextSecondary, host, min),
            TextTertiary = OnEverySurface(host.TextTertiary, host, min),
            Cool = OnEverySurface(host.Cool, host, min),
        };
        Vector4 Read(Vector4 ink) => OnEverySurface(ink, s, min);
        var accent = Read(GlyphTokens.Moon);
        var dangerButton = ColorMath.Over(GlyphTokens.Eclipse with { W = PaletteInks.DangerButtonAlpha }, s.Window);
        var inks = NightInks with
        {
            DangerText = Read(GlyphTokens.EclipseText),
            UnknownText = Read(GlyphTokens.VeilText),
            OnDanger = OnDangerFor(dangerButton, s.Text),
            GaugeArc = s.Light ? ColorMath.FromHex(GaugeInks.LightArcHighHex) : NightInks.GaugeArc,
            GaugeDone = s.Light ? ColorMath.FromHex(GaugeInks.LightArcShadeHex) : NightInks.GaugeDone,
        };
        var states = StateInks.Compose(GlyphTokens.Moon, GlyphTokens.MoonDim, accent, s.Text, s.TextTertiary, s.TextSecondary, GlyphTokens.Eclipse, inks.DangerText, s.TextDisabled, inks.UnknownText)
            .Map(static word => word, stripe => OnEverySurface(stripe, s, line));
        return new UiPalette
        {
            Id = PaletteId.FollowDalamud,
            Key = PaletteChoices.FollowDalamud.Key,
            Name = PaletteChoices.FollowDalamud.Name,
            Surface = s,
            Accent = accent,
            AccentDim = Read(GlyphTokens.MoonDim),
            OrnamentLight = s.OrnamentLight,
            Copper = Read(ColorMath.FromHex(CopperHex)),
            Inks = inks,
            States = states,
            Scene = SceneTokens.Derived(s),
            Brass = NightBrass,
            Plate = NightPlate,
            Gauges = s.Light ? LightGauges : NightGauges with { Track = s.StrongLine with { W = 0.55f } },

            // A light host's medals sit on its own window, as Snow's sit on snow; a dark host keeps Night's gap.
            MedalRimGap = s.Light ? FlairTones.Mixed(Flair.Quiet, s).Table : MedalTokens.LightRimGap,

            // As 1.15: the high-contrast surface roles alone; the accent already reads at 4.5 : 1 on the host.
            HighContrastBuilder = static p => p.ToHighContrast(pushInks: false),
        };
    }

    /// <summary>
    /// <paramref name="ink"/> pushed towards the surface's text until it reaches <paramref name="minRatio"/> on every
    /// surface it is drawn on (<see cref="UiPalette.Surfaces"/>: the window, cards, wells and hovered rows), each pass
    /// against the one it reads worst on. The ink itself when it already does.
    /// </summary>
    internal static Vector4 OnEverySurface(Vector4 ink, in SurfaceColors s, float minRatio)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            var worst = s.Window;
            var worstRatio = ColorMath.Contrast(ink, worst);
            foreach (var ground in (ReadOnlySpan<Vector4>)[s.Raised, s.Hover, s.Sunken])
            {
                var ratio = ColorMath.Contrast(ink, ground);
                if (ratio < worstRatio)
                {
                    (worst, worstRatio) = (ground, ratio);
                }
            }

            if (worstRatio >= minRatio)
            {
                break;
            }

            ink = ColorMath.EnsureContrast(ink, s.Text, worst, minRatio);
        }

        return ink;
    }

    /// <summary>
    /// The ink on a derived palette's destructive button (<paramref name="button"/>, the danger fill over the window):
    /// Night's Silver where it reads as text, else white, else whichever of Silver, white and the host text reads best
    /// (on a light host the button is a light rose, and only a dark ink reads on it).
    /// </summary>
    internal static Vector4 OnDangerFor(Vector4 button, Vector4 text)
    {
        var silver = GlyphTokens.Silver;
        if (ColorMath.ReadsAsText(silver, button))
        {
            return silver;
        }

        var white = Vector4.One;
        if (ColorMath.ReadsAsText(white, button))
        {
            return white;
        }

        var best = ColorMath.Contrast(white, button) >= ColorMath.Contrast(silver, button) ? white : silver;
        return ColorMath.Contrast(ColorMath.Opaque(text), button) > ColorMath.Contrast(best, button) ? ColorMath.Opaque(text) : best;
    }
}
