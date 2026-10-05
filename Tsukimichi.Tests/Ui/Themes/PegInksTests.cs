using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Moonfall's peg inks (1.23.0): every peg colour, lit or not, reads as a mark (3 : 1) on the board, which is drawn in
/// the palette's window colour, in every designed palette, its high-contrast form and Follow Dalamud light and dark.
/// </summary>
public sealed class PegInksTests
{
    public static IEnumerable<object[]> Forms()
    {
        foreach (var palette in UiPalettes.All)
        {
            yield return [palette.Key, "designed"];
            yield return [palette.Key, "high-contrast"];
        }

        yield return ["dalamud", "light"];
        yield return ["dalamud", "dark"];
    }

    private static UiPalette Find(string key, string form)
    {
        if (key == "dalamud")
        {
            var light = form == "light";
            return UiPalettes.FollowDalamud(
                ColorMath.FromHex(light ? 0xF0F0F0u : 0x202020u),
                ColorMath.FromHex(light ? 0xE0E0E0u : 0x2A2A2Au),
                ColorMath.FromHex(light ? 0xD0D0D0u : 0x3A3A3Au),
                ColorMath.FromHex(light ? 0xB0B0B0u : 0x444444u),
                ColorMath.FromHex(light ? 0x101010u : 0xF0F0F0u),
                ColorMath.FromHex(0x808080));
        }

        var palette = UiPalettes.All.Single(p => p.Key == key);
        return form == "high-contrast" ? palette.HighContrast : palette;
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Every_peg_reads_on_the_board_lit_or_not(string key, string form)
    {
        var palette = Find(key, form);
        var inks = PegInks.For(palette);
        var board = palette.Surface.Window;
        string[] names = ["blue", "orange", "green", "purple"];
        for (var k = 0; k < 4; k++)
        {
            var ink = inks[k];
            Assert.True(ColorMath.Contrast(ink, board) >= PegInks.MinContrast, $"{palette.Key} {names[k]} peg: {ColorMath.Contrast(ink, board):0.00} : 1");
            var lit = PegInks.Lit(ink, palette.Surface);
            Assert.True(ColorMath.Contrast(lit, board) >= PegInks.MinContrast, $"{palette.Key} lit {names[k]} peg: {ColorMath.Contrast(lit, board):0.00} : 1");
        }

        // The lit peg's gold outline reads on the board too.
        Assert.True(ColorMath.Contrast(PegInks.LitOutline(palette), board) >= PegInks.MinContrast, $"{palette.Key} lit outline");
    }

    [Fact]
    public void Ishgard_Snow_designs_its_own_pegs_and_dark_palettes_take_Nights()
    {
        Assert.Equal(PegInks.Day, UiPalettes.IshgardSnow.Pegs);
        Assert.Equal(PegInks.Day.Orange, PegInks.For(UiPalettes.IshgardSnow).Orange);
        Assert.Equal(PegInks.Night.Blue, PegInks.For(UiPalettes.Night).Blue);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void The_orange_peg_is_not_copper(string key, string form)
    {
        var palette = Find(key, form);
        Assert.NotEqual(ColorMath.ToHex(palette.Copper), ColorMath.ToHex(PegInks.For(palette).Orange));
    }
}
