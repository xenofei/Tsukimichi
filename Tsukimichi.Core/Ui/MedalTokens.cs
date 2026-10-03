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
    private MedalTokens(GlyphPalette palette) => Palette = palette;

    /// <summary>The medallion as designed.</summary>
    public static readonly MedalTokens Standard = new(GlyphPalette.Standard);

    /// <summary>High contrast on Night and dark hosts.</summary>
    public static readonly MedalTokens HighContrastDark = new(GlyphPalette.HighContrastDark);

    /// <summary>High contrast on light hosts.</summary>
    public static readonly MedalTokens HighContrastLight = new(GlyphPalette.HighContrastLight);

    /// <summary>The tokens for the glyph palette in effect.</summary>
    public static MedalTokens For(GlyphPalette palette) =>
        !palette.HighContrast ? Standard : palette.Light ? HighContrastLight : HighContrastDark;

    /// <summary>The glyph palette these tokens swap in.</summary>
    public GlyphPalette Palette { get; }

    /// <summary>Flat colours on the palette's ground (high contrast): no gradient, glow, sheen or scene.</summary>
    public bool Flat => Palette.HighContrast;

    /// <summary>A short name for the glyph window.</summary>
    public string Name => Palette.Name;

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
