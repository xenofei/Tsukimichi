using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Copper, "it needs you" (spec-1.18, "Colour language"): the spec's hexes per palette, 4.5 : 1 on the window and on
/// cards (the "Needs you" eyebrow is copper text at Quiet and Plain) for every palette and high-contrast form, 3 : 1 as
/// a bar or dot on every surface, and apart from gold and from the Locked out plum. The pairs also run in
/// <see cref="PaletteContrastTests"/> through <see cref="UiPalette.TextPairs"/> and <see cref="UiPalette.LinePairs"/>.
/// </summary>
public sealed class CopperRoleTests
{
    public static IEnumerable<object[]> Forms()
    {
        foreach (var palette in UiPalettes.All)
        {
            yield return [palette.Key, false];
            yield return [palette.Key, true];
        }
    }

    private static UiPalette Find(string key, bool highContrast)
    {
        var palette = UiPalettes.All.Single(p => p.Key == key);
        return highContrast ? palette.HighContrast : palette;
    }

    [Fact]
    public void Each_palette_carries_the_spec_hex()
    {
        Assert.Equal(0xD08654u, ColorMath.ToHex(UiPalettes.Night.Copper));
        Assert.Equal(0xA8582Au, ColorMath.ToHex(UiPalettes.IshgardSnow.Copper));
        Assert.Equal(0xD08654u, ColorMath.ToHex(UiPalettes.Dawn.Copper));
        Assert.Equal(0xD08654u, ColorMath.ToHex(UiPalettes.KuganeLacquer.Copper));
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Copper_reads_as_text_on_the_window_and_cards(string key, bool highContrast)
    {
        var palette = Find(key, highContrast);
        Assert.True(ColorMath.Contrast(palette.Copper, palette.Surface.Window) >= SurfaceColors.TextMinContrast, $"{palette.Key} copper on Window");
        Assert.True(ColorMath.Contrast(palette.Copper, palette.Surface.Raised) >= SurfaceColors.TextMinContrast, $"{palette.Key} copper on Raised");
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Copper_reads_as_a_mark_on_every_surface(string key, bool highContrast)
    {
        var palette = Find(key, highContrast);
        foreach (var (name, ground) in palette.Surfaces())
        {
            Assert.True(ColorMath.Contrast(palette.Copper, ground) >= 3f, $"{palette.Key} copper on {name}");
        }
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Copper_is_its_own_hue_apart_from_gold_and_plum(string key, bool highContrast)
    {
        var palette = Find(key, highContrast);
        Assert.NotEqual(ColorMath.ToHex(palette.Inks.Gold), ColorMath.ToHex(palette.Copper));
        Assert.NotEqual(ColorMath.ToHex(palette.Inks.DangerText), ColorMath.ToHex(palette.Copper));
        Assert.NotEqual(ColorMath.ToHex(palette.Accent), ColorMath.ToHex(palette.Copper));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Follow_Dalamud_pushes_copper_until_it_reads(bool light)
    {
        var palette = UiPalettes.FollowDalamud(
            ColorMath.FromHex(light ? 0xF0F0F0u : 0x202020u),
            ColorMath.FromHex(light ? 0xE0E0E0u : 0x2A2A2Au),
            ColorMath.FromHex(light ? 0xD0D0D0u : 0x3A3A3Au),
            ColorMath.FromHex(light ? 0xB0B0B0u : 0x444444u),
            ColorMath.FromHex(light ? 0x101010u : 0xF0F0F0u),
            ColorMath.FromHex(0x808080));
        foreach (var (name, ground) in palette.Surfaces())
        {
            Assert.True(ColorMath.Contrast(palette.Copper, ground) >= SurfaceColors.TextMinContrast, $"copper on {name}");
        }
    }
}
