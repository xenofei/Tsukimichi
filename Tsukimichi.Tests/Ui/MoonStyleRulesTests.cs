using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>Settings › Display › Look › Moon style (feature plan v6 G3): Medallion by default, Classic on request.</summary>
public class MoonStyleRulesTests
{
    [Fact]
    public void Medallion_is_the_default()
    {
        // A configuration saved before the setting existed reads the enum's default.
        Assert.Equal(MoonStyle.Medallion, default(MoonStyle));
        Assert.Equal(MoonStyle.Medallion, MoonStyleRules.Effective(default));
    }

    [Theory]
    [InlineData(MoonStyle.Medallion)]
    [InlineData(MoonStyle.Classic)]
    public void A_saved_style_round_trips(MoonStyle style)
    {
        // Configuration saves the enum as its number: the values are fixed, and reading one back gives the same style.
        var saved = (int)style;
        Assert.Equal(style, MoonStyleRules.Effective((MoonStyle)saved));
        Assert.Equal(style == MoonStyle.Classic ? 1 : 0, saved);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void An_unknown_style_reads_as_medallion(int value)
    {
        Assert.Equal(MoonStyle.Medallion, MoonStyleRules.Effective((MoonStyle)value));
    }
}
