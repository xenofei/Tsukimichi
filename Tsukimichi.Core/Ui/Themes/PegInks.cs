using System.Numerics;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// Moonfall's peg colours (feature plan v9, 1.23.0), a palette role so the board follows the palette rules like the rest
/// of the chrome. Placeholders until the art track's pegs (plan v9 G8). The orange peg's ink is a marigold (an amber on
/// light palettes) kept apart from <see cref="UiPalette.Copper"/>, which keeps its one meaning ("it needs you").
/// <para>
/// The board is drawn on the palette's window colour, so every peg, lit or not, reads as a mark on it: at least
/// <see cref="MinContrast"/> (WCAG 1.4.11), whatever the palette (<see cref="For"/>).
/// </para>
/// </summary>
/// <param name="Blue">The plain pegs.</param>
/// <param name="Orange">The pegs to clear.</param>
/// <param name="Green">The power pegs.</param>
/// <param name="Purple">The bonus peg.</param>
public readonly record struct PegInks(Vector4 Blue, Vector4 Orange, Vector4 Green, Vector4 Purple)
{
    /// <summary>A peg's least contrast on the board (the palette's window colour).</summary>
    public const float MinContrast = 3f;

    /// <summary>How far a lit peg's ink moves towards the palette's text colour: brighter on a dark board, deeper on a light one.</summary>
    public const float LitMix = 0.45f;

    public const uint BlueHex = 0x6F8FD0;
    public const uint OrangeHex = 0xF2A65A;
    public const uint GreenHex = 0x7FBF9A;
    public const uint PurpleHex = 0xA983D8;

    public const uint DayBlueHex = 0x2F5DB0;
    public const uint DayOrangeHex = 0xA65A00;
    public const uint DayGreenHex = 0x2B7349;
    public const uint DayPurpleHex = 0x6E44AA;

    /// <summary>The pegs on a dark board (Night, Dawn, Kugane Lacquer).</summary>
    public static readonly PegInks Night = new(
        ColorMath.FromHex(BlueHex),
        ColorMath.FromHex(OrangeHex),
        ColorMath.FromHex(GreenHex),
        ColorMath.FromHex(PurpleHex));

    /// <summary>The pegs on a light board (Ishgard Snow, a light host style).</summary>
    public static readonly PegInks Day = new(
        ColorMath.FromHex(DayBlueHex),
        ColorMath.FromHex(DayOrangeHex),
        ColorMath.FromHex(DayGreenHex),
        ColorMath.FromHex(DayPurpleHex));

    /// <summary>
    /// The pegs for <paramref name="palette"/>: its own designed inks, else <see cref="Day"/> on a light palette and
    /// <see cref="Night"/> on a dark one, each pushed towards the text colour until it reaches <see cref="MinContrast"/> on
    /// the window (Follow Dalamud and the high-contrast forms included). Allocation-free; the window computes it once per
    /// palette.
    /// </summary>
    public static PegInks For(UiPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var start = palette.Pegs ?? (palette.IsLight ? Day : Night);
        var s = palette.Surface;
        return new PegInks(
            ColorMath.EnsureContrast(start.Blue, s.Text, s.Window, MinContrast),
            ColorMath.EnsureContrast(start.Orange, s.Text, s.Window, MinContrast),
            ColorMath.EnsureContrast(start.Green, s.Text, s.Window, MinContrast),
            ColorMath.EnsureContrast(start.Purple, s.Text, s.Window, MinContrast));
    }

    /// <summary>
    /// The outline a lit peg takes: the palette's gold, pushed towards the text colour until it reaches
    /// <see cref="MinContrast"/> on the board (a light host style's gold may be too pale).
    /// </summary>
    public static Vector4 LitOutline(UiPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return ColorMath.EnsureContrast(palette.Inks.Gold, palette.Surface.Text, palette.Surface.Window, MinContrast);
    }

    /// <summary>A lit peg's ink: <paramref name="ink"/> moved <see cref="LitMix"/> of the way to the text colour.</summary>
    public static Vector4 Lit(Vector4 ink, in SurfaceColors surface) => ColorMath.Mix(ink, surface.Text, LitMix);

    /// <summary>The four inks in order (blue, orange, green, purple), for checks that walk them.</summary>
    public Vector4 this[int index] => index switch
    {
        1 => Orange,
        2 => Green,
        3 => Purple,
        _ => Blue,
    };
}
