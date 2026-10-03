using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The surfaces one <see cref="Flair"/> level paints the main window with (docs/design/flair-v13/spec.md §1, "Pane
/// background"): the rail, the three panes, a card, a rule, the bands of the ledger and the status bar. On the Night
/// palette they are the design's own hexes: Quiet's panes in steps about 3 % of lightness apart, Plain's flat
/// <c>#11151F</c> with its bands. Under "Follow Dalamud colours" and the high-contrast palette the same roles are mixed
/// from the palette in effect (<see cref="SurfaceColors"/>), so a light host gets light tones.
/// </summary>
/// <param name="Rail">The tab rail's column.</param>
/// <param name="Tree">The navigation column (the Journal tree).</param>
/// <param name="Table">The centre column (the quest table) and the window.</param>
/// <param name="Detail">The detail column.</param>
/// <param name="Card">A card's fill on the detail tone (Quiet); <see cref="SurfaceColors.Raised"/> otherwise.</param>
/// <param name="Rule">A structure line between panes and under headings.</param>
/// <param name="Band">A raised band: Plain's group and tree headers, the selected station.</param>
/// <param name="HeaderBand">The quest table's header band (Plain).</param>
/// <param name="HeaderLine">The 1 px dividers between the header band's columns and under it (Plain).</param>
/// <param name="Status">The status bar's ground.</param>
public readonly record struct FlairTones(
    Vector4 Rail,
    Vector4 Tree,
    Vector4 Table,
    Vector4 Detail,
    Vector4 Card,
    Vector4 Rule,
    Vector4 Band,
    Vector4 HeaderBand,
    Vector4 HeaderLine,
    Vector4 Status)
{
    /// <summary>Quiet on Night: the rail.</summary>
    public const uint QuietRailHex = 0x10151F;

    /// <summary>Quiet on Night: the tree, the darkest pane.</summary>
    public const uint QuietTreeHex = 0x0E1323;

    /// <summary>Quiet on Night: the window and the table.</summary>
    public const uint QuietTableHex = 0x131929;

    /// <summary>Quiet on Night: the detail pane, the lightest.</summary>
    public const uint QuietDetailHex = 0x182033;

    /// <summary>Quiet on Night: a card on the detail tone.</summary>
    public const uint QuietCardHex = 0x1F273C;

    /// <summary>Quiet on Night: the hairline.</summary>
    public const uint QuietRuleHex = 0x262D42;

    /// <summary>Plain on Night: the one flat tone, a touch less blue than Night.</summary>
    public const uint PlainWindowHex = 0x11151F;

    /// <summary>Plain on Night: group and tree header bands.</summary>
    public const uint PlainBandHex = 0x1A1F2C;

    /// <summary>Plain on Night: the table's header band.</summary>
    public const uint PlainHeaderBandHex = 0x1E2330;

    /// <summary>Plain on Night: the header band's dividers.</summary>
    public const uint PlainHeaderLineHex = 0x303648;

    /// <summary>Plain on Night: the 1 px line.</summary>
    public const uint PlainLineHex = 0x262B38;

    /// <summary>
    /// The tones <paramref name="flair"/> paints with on <paramref name="surface"/>. The design's hexes when the surface
    /// is the Night palette itself; otherwise the same roles mixed from it (a dark host's tree a step darker than its
    /// window and its detail pane a step lighter; a light host's the other way round, never past its own text).
    /// </summary>
    public static FlairTones For(Flair flair, in SurfaceColors surface)
    {
        var s = surface;
        var night = s == SurfaceColors.Night;
        switch (flair)
        {
            case Flair.Quiet when night:
                return new FlairTones(
                    ColorMath.FromHex(QuietRailHex),
                    ColorMath.FromHex(QuietTreeHex),
                    ColorMath.FromHex(QuietTableHex),
                    ColorMath.FromHex(QuietDetailHex),
                    ColorMath.FromHex(QuietCardHex),
                    ColorMath.FromHex(QuietRuleHex),
                    ColorMath.FromHex(QuietCardHex),
                    ColorMath.FromHex(QuietCardHex),
                    ColorMath.FromHex(QuietRuleHex),
                    ColorMath.FromHex(QuietTableHex));

            case Flair.Quiet:
            {
                var down = s.Light ? s.Text : new Vector4(0f, 0f, 0f, 1f);
                var tree = ColorMath.Mix(s.Window, down, s.Light ? 0.03f : 0.22f);
                var detail = ColorMath.Mix(s.Window, s.Text, 0.04f);
                var card = ColorMath.Mix(s.Window, s.Text, 0.08f);
                return new FlairTones(
                    ColorMath.Mix(s.Window, down, s.Light ? 0.02f : 0.15f),
                    tree,
                    s.Window,
                    detail,
                    card,
                    s.Line,
                    card,
                    card,
                    s.Line,
                    s.Window);
            }

            case Flair.Plain when night:
                return new FlairTones(
                    s.Deep,
                    ColorMath.FromHex(PlainWindowHex),
                    ColorMath.FromHex(PlainWindowHex),
                    ColorMath.FromHex(PlainWindowHex),
                    ColorMath.FromHex(PlainWindowHex),
                    ColorMath.FromHex(PlainLineHex),
                    ColorMath.FromHex(PlainBandHex),
                    ColorMath.FromHex(PlainHeaderBandHex),
                    ColorMath.FromHex(PlainHeaderLineHex),
                    s.Deep);

            case Flair.Plain:
            {
                var band = ColorMath.Mix(s.Window, s.Text, 0.05f);
                return new FlairTones(s.Deep, s.Window, s.Window, s.Window, s.Window, s.Line, band, s.Raised, s.StrongLine with { W = 1f }, s.Deep);
            }

            default:
                return new FlairTones(s.Deep, s.Window, s.Window, s.Window, s.Raised, s.Ornament, s.Raised, s.Raised, s.Line, s.Deep);
        }
    }

    /// <summary>CIE L* (0–100) of an opaque colour: the lightness the design's "about 3 % apart" is measured in.</summary>
    public static float Lightness(Vector4 color)
    {
        var y = ColorMath.Luminance(color);
        return y > 0.008856f ? (116f * MathF.Cbrt(y)) - 16f : 903.3f * y;
    }
}
