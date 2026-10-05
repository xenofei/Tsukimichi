using System.Numerics;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// Moonfall's peg colours (feature plan v9, 1.23.0), a palette role so the board follows the palette rules like the rest
/// of the chrome. Placeholders until the art track's pegs (plan v9 G8). The orange peg's ink is a marigold well apart
/// from <see cref="UiPalette.Copper"/>, which keeps its one meaning ("it needs you").
/// </summary>
/// <param name="Blue">The plain pegs.</param>
/// <param name="Orange">The pegs to clear.</param>
/// <param name="Green">The power pegs.</param>
/// <param name="Purple">The bonus peg.</param>
public readonly record struct PegInks(Vector4 Blue, Vector4 Orange, Vector4 Green, Vector4 Purple)
{
    public const uint BlueHex = 0x6F8FD0;
    public const uint OrangeHex = 0xF2A65A;
    public const uint GreenHex = 0x7FBF9A;
    public const uint PurpleHex = 0xA983D8;

    /// <summary>Night's pegs, which every palette uses until it designs its own.</summary>
    public static readonly PegInks Night = new(
        ColorMath.FromHex(BlueHex),
        ColorMath.FromHex(OrangeHex),
        ColorMath.FromHex(GreenHex),
        ColorMath.FromHex(PurpleHex));
}
