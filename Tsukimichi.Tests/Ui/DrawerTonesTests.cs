using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The filter drawer's surfaces per Decoration level (plan v7 UI-2, spec §2.2): the design's hexes on Night, the same
/// roles mixed from a host palette, and pills that never turn gold.
/// </summary>
public class DrawerTonesTests
{
    [Fact]
    public void Full_is_a_gradient_sheet_lit_from_the_top_with_giltlight_heads()
    {
        var t = DrawerTones.For(Flair.Full, SurfaceColors.Night);
        Assert.Equal(ColorMath.FromHex(DrawerTones.FullTopHex), t.SheetTop);
        Assert.Equal(ColorMath.FromHex(DrawerTones.FullFootHex), t.SheetFoot);
        Assert.True(FlairTones.Lightness(t.SheetTop) > FlairTones.Lightness(t.SheetFoot));
        Assert.Equal(ColorMath.FromHex(DrawerTones.GiltLightHex), t.Heading);
    }

    [Fact]
    public void Quiet_is_a_flat_sheet_one_tone_above_the_tree()
    {
        var t = DrawerTones.For(Flair.Quiet, SurfaceColors.Night);
        var tree = FlairTones.For(Flair.Quiet, SurfaceColors.Night).Tree;
        Assert.Equal(t.SheetTop, t.SheetFoot);
        Assert.Equal(ColorMath.FromHex(DrawerTones.QuietSheetHex), t.SheetTop);
        Assert.True(FlairTones.Lightness(t.SheetTop) > FlairTones.Lightness(tree));
        Assert.Equal(ColorMath.FromHex(DrawerTones.QuietEdgeHex), t.Edge);
        Assert.Equal(SurfaceColors.Night.Text, t.Heading);
    }

    [Fact]
    public void Plain_is_the_ledger_with_a_raised_header_band_and_a_sunken_footer()
    {
        var t = DrawerTones.For(Flair.Plain, SurfaceColors.Night);
        Assert.Equal(ColorMath.FromHex(DrawerTones.PlainSheetHex), t.SheetTop);
        Assert.Equal(ColorMath.FromHex(DrawerTones.PlainEdgeHex), t.Edge);
        Assert.True(FlairTones.Lightness(t.HeaderBand) > FlairTones.Lightness(t.SheetTop));
        Assert.True(FlairTones.Lightness(t.FooterBand) < FlairTones.Lightness(t.SheetTop));
    }

    [Theory]
    [InlineData(Flair.Full)]
    [InlineData(Flair.Quiet)]
    [InlineData(Flair.Plain)]
    public void The_count_pills_are_neutral_at_every_level(Flair flair)
    {
        // Gold means act now; a filter sheet asks for nothing.
        var pill = DrawerTones.For(flair, SurfaceColors.Night).Pill;
        Assert.Equal(ColorMath.FromHex(DrawerTones.PillHex), pill);
        Assert.True(MathF.Abs(pill.X - pill.Y) < 0.08f, "the pill leans gold");
    }

    [Theory]
    [InlineData(Flair.Full)]
    [InlineData(Flair.Quiet)]
    [InlineData(Flair.Plain)]
    public void A_light_host_gets_a_light_sheet(Flair flair)
    {
        var light = SurfaceColors.Night with
        {
            Window = new Vector4(0.95f, 0.95f, 0.96f, 1f),
            Raised = new Vector4(0.98f, 0.98f, 0.99f, 1f),
            Text = new Vector4(0.1f, 0.1f, 0.12f, 1f),
            Light = true,
        };

        var t = DrawerTones.For(flair, light);
        Assert.True(FlairTones.Lightness(t.SheetTop) > 80f);
        Assert.True(FlairTones.Lightness(t.SheetFoot) > 80f);
    }
}
