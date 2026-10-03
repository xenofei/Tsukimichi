using System.Numerics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The colours a medal is drawn with in one glyph palette (feature plan v6 G2). Standard paints the medallion as
/// designed, from <see cref="GlyphTokens.Medallion"/> and <see cref="GlyphTokens.MedallionDetail"/>. High contrast is a
/// token swap on the same shapes: everything flat, the keyline and well in the palette's ground (so the medal carries
/// its own background, as the old high-contrast moons did), the rim in the dim rung, each state's emblem in its
/// <see cref="GlyphStyle.Identity"/> (the palette's luminance ladder) and its second mark — the ribbon, the clouds, the
/// arrow, the check — in the bright ink, keylined in the ground.
/// </summary>
public sealed class MedalTokens
{
    private MedalTokens(GlyphPalette palette, MedalFinish finish = MedalFinish.Gilt)
    {
        Palette = palette;
        Finish = finish;
    }

    /// <summary>The medallion as designed.</summary>
    public static readonly MedalTokens Standard = new(GlyphPalette.Standard);

    /// <summary>High contrast on Night and dark hosts.</summary>
    public static readonly MedalTokens HighContrastDark = new(GlyphPalette.HighContrastDark);

    /// <summary>High contrast on light hosts.</summary>
    public static readonly MedalTokens HighContrastLight = new(GlyphPalette.HighContrastLight);

    /// <summary>
    /// Decoration Quiet (docs/design/flair-v13 §1, "Glyph style"): the same medal face, with the brass rim swapped for
    /// a 1 px silver hairline (<see cref="LightRimInk"/> at <see cref="LightRimAlpha"/>) one pixel outside the well, on a
    /// pane-coloured gap (<see cref="LightRimGap"/>) so the medal reads as laid on top. "Medals, lighter rim."
    /// </summary>
    public static readonly MedalTokens LightRim = new(GlyphPalette.Standard, MedalFinish.LightRim);

    /// <summary>
    /// Decoration Plain (spec §1.1): flat like high contrast but on the standard palette, with a brightness ladder of
    /// its own (<see cref="PlainDisc"/>, <see cref="PlainEmblem"/>, <see cref="PlainRim"/>): Ready reads first and
    /// Completed recedes. <see cref="MedalArt.Medal"/> builds it as a row-tier glyph. It is not a contrast ladder; the
    /// high-contrast palette keeps its own tokens.
    /// </summary>
    public static readonly MedalTokens Plain = new(GlyphPalette.Standard, MedalFinish.Plain);

    /// <summary>The tokens for the glyph palette in effect.</summary>
    public static MedalTokens For(GlyphPalette palette) =>
        !palette.HighContrast ? Standard : palette.Light ? HighContrastLight : HighContrastDark;

    /// <summary>
    /// The tokens for the glyph palette and a Decoration level's medal finish: the high-contrast palette always wins
    /// (its ladder is for low vision); otherwise Quiet's light rim, Plain's own ladder, or the medallion as designed.
    /// </summary>
    public static MedalTokens For(GlyphPalette palette, MedalFinish finish) => palette.HighContrast
        ? For(palette)
        : finish switch
        {
            MedalFinish.LightRim => LightRim,
            MedalFinish.Plain => Plain,
            _ => Standard,
        };

    /// <summary>The glyph palette these tokens swap in.</summary>
    public GlyphPalette Palette { get; }

    /// <summary>The Decoration finish these tokens draw: Gilt (as designed, and high contrast), LightRim or Plain.</summary>
    public MedalFinish Finish { get; }

    /// <summary>Flat colours on the palette's ground (high contrast): no gradient, glow, sheen or scene.</summary>
    public bool Flat => Palette.HighContrast;

    /// <summary>Plain's flat ladder (<see cref="Plain"/>).</summary>
    public bool IsPlain => Finish == MedalFinish.Plain;

    /// <summary>Quiet's silver hairline rim (<see cref="LightRim"/>).</summary>
    public bool IsLightRim => Finish == MedalFinish.LightRim;

    /// <summary>A short name for the glyph window.</summary>
    public string Name => Finish switch
    {
        MedalFinish.LightRim => "Light rim",
        MedalFinish.Plain => "Plain",
        _ => Palette.Name,
    };

    // ------------------------------------------------------------------ Quiet's light rim

    /// <summary>The light rim's hairline: Silver, a touch deeper (#C3CBDF).</summary>
    public static readonly Vector4 LightRimInk = ColorMath.FromHex(0xC3CBDF);

    /// <summary>The light rim's alpha.</summary>
    public const float LightRimAlpha = 0.62f;

    /// <summary>The pane-coloured gap between the well and the hairline (Quiet's darkest tone, so the medal reads as laid on top).</summary>
    public static readonly Vector4 LightRimGap = ColorMath.FromHex(0x0E1322);

    /// <summary>The light rim's radius in the 128-unit box.</summary>
    public const float LightRimRadius = 55.6f;

    /// <summary>The badge's light rim and the gap under it, in the 128-unit box (the badge sits on the gap, laid on top).</summary>
    public const float LightRimBadgeRadius = 21.6f;

    public const float LightRimBadgeGap = 23.4f;

    // ------------------------------------------------------------------ Plain's ladder (spec §1.1)

    /// <summary>Plain's ground: the pane the glyphs sit on.</summary>
    public static readonly Vector4 PlainGround = ColorMath.FromHex(FlairTones.PlainWindowHex);

    /// <summary>Plain's night enamel, the disc of every state but Ready, Blocked, Locked out and Not checked.</summary>
    public static readonly Vector4 PlainNight = ColorMath.FromHex(0x1C2752);

    /// <summary>Plain's gilt, for Completed's check and Done's "comes back" arc.</summary>
    public static readonly Vector4 PlainGilt = ColorMath.FromHex(0xC9A65C);

    /// <summary>Locked out's two fractures.</summary>
    public static readonly Vector4 PlainCrack = GlyphTokens.Medallion.DalamudSocket;

    /// <summary>Blocked's grey cloud band.</summary>
    public static readonly Vector4 PlainCloud = ColorMath.FromHex(0x3A4260);

    /// <summary>Not checked's dark disc.</summary>
    public static readonly Vector4 PlainUnknownDisc = ColorMath.FromHex(0x151A28);

    /// <summary>The alpha of a Plain glyph's 1 px rim in its state's ink.</summary>
    public const float PlainRimAlpha = 0.6f;

    /// <summary>Plain: a state's disc.</summary>
    public static Vector4 PlainDisc(QuestState state) => state switch
    {
        QuestState.Ready => GlyphTokens.Medallion.LapisSeaHorizon,
        QuestState.Blocked => GlyphTokens.Medallion.MoonstoneDeep,
        QuestState.Foreclosed => GlyphTokens.Medallion.DalamudShade,
        QuestState.Unknown => PlainUnknownDisc,
        _ => PlainNight,
    };

    /// <summary>Plain: a state's emblem (the crescent, the moon, the half moon, the cloud, the cracks, the "?").</summary>
    public static Vector4 PlainEmblem(QuestState state) => state switch
    {
        QuestState.Ready => GlyphTokens.Medallion.MoonstoneSpecular,
        QuestState.ReadyOnOtherJob => GlyphTokens.Medallion.MoonstoneHigh,
        QuestState.Accepted => GlyphTokens.Medallion.Moonstone,
        QuestState.Completed or QuestState.DoneThisCycle => GlyphTokens.Medallion.MoonstoneMid,
        QuestState.Blocked => PlainCloud,
        QuestState.Foreclosed => PlainCrack,
        _ => GlyphTokens.Mist,
    };

    /// <summary>Plain: a state's rim ink (drawn at <see cref="PlainRimAlpha"/>), the state's own colour.</summary>
    public static Vector4 PlainRim(QuestState state) => state switch
    {
        QuestState.Ready or QuestState.Accepted => GlyphTokens.Moon,
        QuestState.Completed => GlyphTokens.MoonDeep,
        QuestState.ReadyOnOtherJob or QuestState.DoneThisCycle => GlyphTokens.Silver,
        QuestState.Blocked => GlyphTokens.Dusk,
        QuestState.Foreclosed => GlyphTokens.Eclipse,
        _ => GlyphTokens.VeilText,
    };

    /// <summary>High contrast: the keyline disc and the well.</summary>
    public Vector4 Ground => Palette.Ground;

    /// <summary>High contrast: the rim and the badge ring, the dim rung (4.5 : 1 on Night, 4.1 : 1 on white).</summary>
    public Vector4 Rim => GlyphTokens.HighContrastDim;

    /// <summary>High contrast: the bright ink for second marks (white on dark hosts, navy on light ones).</summary>
    public Vector4 Bright => Palette.Light ? GlyphTokens.HighContrastInkNavy : GlyphTokens.SilverHigh;

    /// <summary>High contrast: the gold rung (Ready, In journal, Completed and the warm badge glyphs).</summary>
    public Vector4 Gold => Palette.Light ? GlyphTokens.HighContrastInkGold : GlyphTokens.MoonHigh;

    /// <summary>High contrast: a state's emblem, its rung of the ladder.</summary>
    public Vector4 Ink(QuestState state) => Palette.Style(state).Identity;

    /// <summary>High contrast: every ink a flat medal draws over its ground, for the contrast checks.</summary>
    public IEnumerable<(string Part, Vector4 Color)> Inks()
    {
        yield return ("rim", Rim);
        yield return ("bright", Bright);
        yield return ("gold", Gold);
        foreach (var state in Enum.GetValues<QuestState>())
        {
            yield return ($"{state} emblem", Ink(state));
        }
    }
}
