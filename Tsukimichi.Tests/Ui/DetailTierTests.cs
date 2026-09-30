using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The detail pane's width tiers (feature plan v4 L5, design v4 §8.2): D1 from 340, D2 from 320, D3 down to the floor of 260.</summary>
public class DetailTierTests
{
    [Theory]
    [InlineData(360f, DetailTier.Full)]
    [InlineData(340f, DetailTier.Full)]
    [InlineData(339f, DetailTier.Medium)]
    [InlineData(320f, DetailTier.Medium)]
    [InlineData(319f, DetailTier.Narrow)]
    [InlineData(260f, DetailTier.Narrow)]
    [InlineData(259f, DetailTier.Compact)]
    [InlineData(0f, DetailTier.Compact)]
    [InlineData(float.NaN, DetailTier.Compact)]
    [InlineData(float.PositiveInfinity, DetailTier.Full)]
    public void The_width_picks_the_tier(float logical, DetailTier expected)
    {
        Assert.Equal(expected, DetailTiers.For(logical));
    }

    [Fact]
    public void The_breakpoints_are_the_layout_budgets()
    {
        Assert.Equal(DetailTier.Full, DetailTiers.For(LayoutBudgets.DetailFullLogical));
        Assert.Equal(DetailTier.Medium, DetailTiers.For(LayoutBudgets.DetailMediumLogical));
        Assert.Equal(DetailTier.Narrow, DetailTiers.For(PaneLayout.DetailFloorLogical));
    }

    [Theory]
    [InlineData(DetailTier.Full, false)]
    [InlineData(DetailTier.Medium, false)]
    [InlineData(DetailTier.Narrow, true)]
    [InlineData(DetailTier.Compact, true)]
    public void Below_320_the_pane_stacks(DetailTier tier, bool stacks)
    {
        Assert.Equal(stacks, DetailTiers.Stacks(tier));
    }
}
