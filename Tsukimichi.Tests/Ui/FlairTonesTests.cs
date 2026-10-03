using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The surfaces each Decoration level paints (docs/design/flair-v13/spec.md §1, "Pane background"): Quiet's panes told
/// apart by tone alone, about 3 % of lightness apart; Plain's one flat tone and its bands; the same roles mixed from a
/// host palette under "Follow Dalamud colours".
/// </summary>
public class FlairTonesTests
{
    [Fact]
    public void Quiets_panes_step_about_three_percent_of_lightness_apart()
    {
        var t = FlairTones.For(Flair.Quiet, SurfaceColors.Night);
        var tree = FlairTones.Lightness(t.Tree);
        var table = FlairTones.Lightness(t.Table);
        var detail = FlairTones.Lightness(t.Detail);
        var card = FlairTones.Lightness(t.Card);
        Assert.InRange(table - tree, 2f, 5f);
        Assert.InRange(detail - table, 2f, 5f);
        Assert.InRange(card - detail, 2f, 5f);
        Assert.Equal(ColorMath.FromHex(FlairTones.QuietTableHex), t.Table);
    }

    [Fact]
    public void Plain_is_one_flat_tone_with_raised_bands()
    {
        var t = FlairTones.For(Flair.Plain, SurfaceColors.Night);
        Assert.Equal(t.Table, t.Tree);
        Assert.Equal(t.Table, t.Detail);
        Assert.Equal(ColorMath.FromHex(FlairTones.PlainWindowHex), t.Table);
        Assert.True(FlairTones.Lightness(t.HeaderBand) > FlairTones.Lightness(t.Table));
        Assert.True(FlairTones.Lightness(t.Band) > FlairTones.Lightness(t.Table));
        Assert.Equal(SurfaceColors.Night.Deep, t.Rail);
    }

    [Fact]
    public void Full_paints_the_palettes_own_roles()
    {
        var t = FlairTones.For(Flair.Full, SurfaceColors.Night);
        Assert.Equal(SurfaceColors.Night.Window, t.Table);
        Assert.Equal(SurfaceColors.Night.Deep, t.Rail);
    }

    [Theory]
    [InlineData(0x1E1E1E, false)]
    [InlineData(0xF4F4F4, true)]
    public void A_host_palette_gets_tones_of_its_own_that_still_step(uint window, bool light)
    {
        var host = SurfaceColors.FromHost(
            ColorMath.FromHex(window),
            ColorMath.FromHex(light ? 0xE0E0E0u : 0x2A2A2Au),
            ColorMath.FromHex(light ? 0xD0D0D0u : 0x3A3A3Au),
            ColorMath.FromHex(light ? 0xB0B0B0u : 0x444444u),
            ColorMath.FromHex(light ? 0x101010u : 0xF0F0F0u),
            ColorMath.FromHex(0x808080));
        Assert.Equal(light, host.Light);
        var t = FlairTones.For(Flair.Quiet, host);
        Assert.Equal(host.Window, t.Table);
        Assert.NotEqual(t.Table, t.Tree);
        Assert.NotEqual(t.Table, t.Detail);

        // The card is lifted from the detail tone toward the text, so it reads as raised on either kind of host.
        var toward = light ? -1f : 1f;
        Assert.True(toward * (FlairTones.Lightness(t.Card) - FlairTones.Lightness(t.Detail)) > 0f);
    }

    [Fact]
    public void High_contrast_quiet_mixes_from_its_own_palette()
    {
        var hc = SurfaceColors.Night.ForHighContrast();
        var t = FlairTones.For(Flair.Quiet, hc);
        Assert.Equal(hc.Window, t.Table);
        Assert.Equal(hc.Line, t.Rule);
        Assert.NotEqual(ColorMath.FromHex(FlairTones.QuietTreeHex), t.Tree);
    }
}
