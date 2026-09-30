using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The Machado 2009 simulation, checked against the accessibility panel's Appendix A.</summary>
public class ColorVisionTests
{
    [Theory]
    [InlineData(0xB25C7Fu, ColorVision.Deuteranopia, 0x7E7D7Du)] // Eclipse
    [InlineData(0xB25C7Fu, ColorVision.Protanopia, 0x676E80u)]
    [InlineData(0xB25C7Fu, ColorVision.Tritanopia, 0xBE5769u)]
    [InlineData(0xB25C7Fu, ColorVision.Greyscale, 0x787878u)]
    [InlineData(0xF2D27Au, ColorVision.Deuteranopia, 0xECD97Du)] // Moon
    [InlineData(0xF2D27Au, ColorVision.Tritanopia, 0xFFC5BDu)]
    [InlineData(0x7C86A8u, ColorVision.Greyscale, 0x878787u)] // Dusk
    public void Simulation_matches_the_panel_appendix(uint input, ColorVision mode, uint expected)
    {
        var simulated = ColorVisionSimulation.Simulate(ColorMath.FromHex(input), mode);
        var want = ColorMath.FromHex(expected);
        Assert.InRange(MathF.Abs(simulated.X - want.X) * 255f, 0f, 2f);
        Assert.InRange(MathF.Abs(simulated.Y - want.Y) * 255f, 0f, 2f);
        Assert.InRange(MathF.Abs(simulated.Z - want.Z) * 255f, 0f, 2f);
    }

    [Fact]
    public void None_and_alpha_pass_through()
    {
        var c = new Vector4(0.2f, 0.4f, 0.6f, 0.3f);
        Assert.Equal(c, ColorVisionSimulation.Simulate(c, ColorVision.None));
        Assert.Equal(0.3f, ColorVisionSimulation.Simulate(c, ColorVision.Protanopia).W);
    }

    [Fact]
    public void Greyscale_keeps_the_luminance()
    {
        var gold = ColorMath.FromHex(0xF2D27A);
        var grey = ColorVisionSimulation.Simulate(gold, ColorVision.Greyscale);
        Assert.Equal(ColorMath.Luminance(gold), ColorMath.Luminance(grey), 3);
        Assert.Equal(grey.X, grey.Y, 5);
        Assert.Equal(grey.Y, grey.Z, 5);
    }
}
