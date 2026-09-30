using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>What each Flair level draws (moon-road proposal P5, §9, §10.2; feature plan v4 V1).</summary>
public class FlairRulesTests
{
    [Fact]
    public void Full_draws_every_ornament_and_the_moon_road_motion()
    {
        Assert.True(FlairRules.Rules(Flair.Full));
        Assert.True(FlairRules.PaneGradient(Flair.Full));
        Assert.True(FlairRules.CornerMarks(Flair.Full));
        Assert.True(FlairRules.Glow(Flair.Full));
        Assert.True(FlairRules.Motion(Flair.Full, reduceMotion: false));
    }

    [Fact]
    public void Quiet_keeps_rules_and_dividers_only()
    {
        Assert.True(FlairRules.Rules(Flair.Quiet));
        Assert.False(FlairRules.PaneGradient(Flair.Quiet));
        Assert.False(FlairRules.CornerMarks(Flair.Quiet));
        Assert.False(FlairRules.Glow(Flair.Quiet));
        Assert.False(FlairRules.Motion(Flair.Quiet, reduceMotion: false));
    }

    [Fact]
    public void Plain_draws_no_moon_road_ornament()
    {
        Assert.False(FlairRules.Rules(Flair.Plain));
        Assert.False(FlairRules.PaneGradient(Flair.Plain));
        Assert.False(FlairRules.CornerMarks(Flair.Plain));
        Assert.False(FlairRules.Glow(Flair.Plain));
        Assert.False(FlairRules.Motion(Flair.Plain, reduceMotion: false));
    }

    [Fact]
    public void Reduce_motion_stops_the_moon_road_motion_at_any_level()
    {
        Assert.False(FlairRules.Motion(Flair.Full, reduceMotion: true));
    }

    [Theory]
    [InlineData(Flair.Full, false, Flair.Full)]
    [InlineData(Flair.Quiet, false, Flair.Quiet)]
    [InlineData(Flair.Plain, false, Flair.Plain)]
    [InlineData(Flair.Full, true, Flair.Quiet)]
    [InlineData(Flair.Quiet, true, Flair.Quiet)]
    [InlineData(Flair.Plain, true, Flair.Plain)]
    [InlineData((Flair)42, false, Flair.Full)]
    public void High_contrast_draws_at_most_quiet(Flair setting, bool highContrast, Flair expected)
    {
        Assert.Equal(expected, FlairRules.Effective(setting, highContrast));
    }

    [Theory]
    [InlineData(Flair.Full, true, true)]
    [InlineData(Flair.Quiet, true, true)]
    [InlineData(Flair.Plain, true, false)]
    [InlineData(Flair.Full, false, false)]
    public void Game_heading_fonts_follow_the_toggle_and_are_off_under_plain(Flair flair, bool toggle, bool expected)
    {
        Assert.Equal(expected, FlairRules.GameHeadingFonts(flair, toggle));
    }
}
