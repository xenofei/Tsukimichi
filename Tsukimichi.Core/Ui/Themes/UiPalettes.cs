using System.Numerics;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The palette registry (plan v7 T2, theme-system §8): the designed palettes by key, and the "Follow Dalamud" palette
/// derived from the host style. Today it holds Night alone; Ishgard Snow, Dawn and Kugane Lacquer join it as they are
/// designed (T8, T16). <see cref="Night"/> is the 1.15 colours exactly: <c>UiPaletteTests</c> holds every role to its hex.
/// </summary>
public static class UiPalettes
{
    /// <summary>Night's key, the default palette.</summary>
    public const string NightKey = "night";

    /// <summary>The Follow Dalamud palette's key: mapped from the user's Dalamud style each time it changes.</summary>
    public const string FollowDalamudKey = "follow-dalamud";

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
        MorningStar: false,
        NightGrade: true,
        Stars: new StarInks(
            ColorMath.FromHex(StarInks.CoolHex),
            ColorMath.FromHex(StarInks.MoonWhiteHex),
            ColorMath.FromHex(StarInks.GoldHex),
            ColorMath.FromHex(StarInks.EmberHex),
            ColorMath.FromHex(StarInks.FigureHex),
            ColorMath.FromHex(StarInks.BandHex)));

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
    /// Night (ui-revamp §4.3, the Moon Road, flair-v13): the default palette, and the colours every release up to 1.15
    /// drew with. Its high-contrast form is the surface's <see cref="SurfaceColors.ForHighContrast"/> alone, as before.
    /// </summary>
    public static readonly UiPalette Night = new()
    {
        Key = NightKey,
        Name = "Night",
        Surface = SurfaceColors.Night,
        Accent = GlyphTokens.Moon,
        AccentDim = GlyphTokens.MoonDim,
        OrnamentLight = SurfaceColors.Night.OrnamentLight,
        Inks = NightInks,
        States = StateInks.Compose(GlyphTokens.Moon, GlyphTokens.MoonDim, GlyphTokens.Moon, GlyphTokens.Silver, GlyphTokens.Dusk, GlyphTokens.Mist, GlyphTokens.Eclipse, GlyphTokens.EclipseText, GlyphTokens.Veil, GlyphTokens.VeilText),
        Scene = NightScene,
        Brass = NightBrass,
        Plate = NightPlate,
        Pills = NightPills,
        QuietTones = FlairTones.NightQuiet,
        PlainTones = FlairTones.NightPlain,
        DrawerTones = Ui.DrawerTones.NightSet,
        HighContrastBuilder = static p => p.ToHighContrast(pushInks: false),
    };

    private static readonly UiPalette[] Designed = [Night];

    /// <summary>The designed palettes, in the order the Themes page lists them (Follow Dalamud is offered after them).</summary>
    public static IReadOnlyList<UiPalette> All => Designed;

    /// <summary>The designed palette saved as <paramref name="key"/>; Night for an unknown or empty key (a share code from a newer build).</summary>
    public static UiPalette Get(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            foreach (var palette in Designed)
            {
                if (string.Equals(palette.Key, key, StringComparison.Ordinal))
                {
                    return palette;
                }
            }
        }

        return Night;
    }

    /// <summary>Whether <paramref name="key"/> names a designed palette or Follow Dalamud.</summary>
    public static bool IsKnown(string? key) =>
        string.Equals(key, FollowDalamudKey, StringComparison.Ordinal) || Designed.Any(p => string.Equals(p.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The Follow Dalamud palette (the hook, not a designed palette): the surface roles mapped from the host style
    /// (<see cref="SurfaceColors.FromHost"/>), gold as text pushed until it reads at 4.5 : 1 on the window (a light host
    /// gets a deep gold), the state text inks taken from the surface, the danger and Unknown text pushed until they read,
    /// and the scene from the window's lightness (<see cref="SceneTokens.Derived"/>). Gold fills, the brass and the gold
    /// pill material keep Night's. No designed tones, so Quiet, Plain and the drawer are mixed from the surface, and no
    /// lit pills. Allocates one palette; <c>Theme</c> builds it only when the host colours change.
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
            Key = FollowDalamudKey,
            Name = "Follow Dalamud",
            Surface = s,
            Accent = accent,
            AccentDim = Read(GlyphTokens.MoonDim),
            OrnamentLight = s.OrnamentLight,
            Inks = inks,
            States = StateInks.Compose(GlyphTokens.Moon, GlyphTokens.MoonDim, accent, s.Text, s.TextTertiary, s.TextSecondary, GlyphTokens.Eclipse, inks.DangerText, s.TextDisabled, inks.UnknownText),
            Scene = SceneTokens.Derived(s),
            Brass = NightBrass,
            Plate = NightPlate,

            // As 1.15: the high-contrast surface roles alone; the accent already reads at 4.5 : 1 on the host.
            HighContrastBuilder = static p => p.ToHighContrast(pushInks: false),
        };
    }
}
