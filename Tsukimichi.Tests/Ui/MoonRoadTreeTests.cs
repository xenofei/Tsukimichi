using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The Moon Road tree and rail (feature plan v4 V2): the orbit fill's tween, the tree's blocks and the road's length.</summary>
public class MoonRoadTreeTests
{
    [Fact]
    public void An_orbit_seen_for_the_first_time_fills_from_empty_with_an_ease_out()
    {
        var store = new MotionStore();

        Assert.Equal(0f, store.Tween(1, 0.8f, MotionMath.OrbitFillSeconds, 10d, animate: true));
        var half = store.Tween(1, 0.8f, MotionMath.OrbitFillSeconds, 10d + (MotionMath.OrbitFillSeconds / 2d), animate: true);
        Assert.Equal(0.8f * MotionMath.EaseOutCubic(0.5f), half, 4);
        Assert.True(half > 0.4f, "ease-out: past the midpoint at half time");
        Assert.Equal(0.8f, store.Tween(1, 0.8f, MotionMath.OrbitFillSeconds, 11d, animate: true));
    }

    [Fact]
    public void The_tween_is_computed_from_its_start_time_not_accumulated_per_frame()
    {
        var sparse = new MotionStore();
        var dense = new MotionStore();
        sparse.Tween(1, 1f, 1f, 0d, animate: true);
        dense.Tween(1, 1f, 1f, 0d, animate: true);
        for (var t = 0.01d; t < 0.6d; t += 0.01d)
        {
            dense.Tween(1, 1f, 1f, t, animate: true);
        }

        Assert.Equal(sparse.Tween(1, 1f, 1f, 0.6d, animate: true), dense.Tween(1, 1f, 1f, 0.6d, animate: true), 5);
    }

    [Fact]
    public void A_new_fraction_moves_from_the_value_shown_now()
    {
        var store = new MotionStore();
        store.Tween(1, 0.5f, 1f, 0d, animate: true);
        Assert.Equal(0.5f, store.Tween(1, 0.5f, 1f, 2d, animate: true));

        Assert.Equal(0.5f, store.Tween(1, 0.75f, 1f, 3d, animate: true));
        var mid = store.Tween(1, 0.75f, 1f, 3.5d, animate: true);
        Assert.InRange(mid, 0.5f, 0.75f);
        Assert.Equal(0.75f, store.Tween(1, 0.75f, 1f, 4.1d, animate: true));
    }

    [Fact]
    public void Without_motion_the_fill_is_instant_and_does_not_replay_later()
    {
        var store = new MotionStore();

        Assert.Equal(0.6f, store.Tween(1, 0.6f, 1f, 0d, animate: false));
        Assert.Equal(0.6f, store.Tween(1, 0.6f, 1f, 0.1d, animate: true));
        Assert.Equal(0.3f, store.Tween(1, 0.3f, 1f, 0.2d, animate: false));
    }

    [Fact]
    public void Blocks_split_the_story_from_the_side_quests_and_the_virtual_nodes()
    {
        Assert.Equal(JournalBlock.Story, JournalBlocks.ForSection(BannerArts.MainScenarioSection));
        Assert.Equal(JournalBlock.Story, JournalBlocks.ForSection(BannerArts.MainScenarioDawntrailSection));
        Assert.Equal(JournalBlock.Story, JournalBlocks.ForSection(BannerArts.ChroniclesSection));
        Assert.Equal(JournalBlock.Side, JournalBlocks.ForSection(BannerArts.SidequestSection));
        Assert.Equal(JournalBlock.Side, JournalBlocks.ForSection(BannerArts.OtherQuestsSection));

        Assert.False(JournalBlocks.DividerBetween(JournalBlock.All, JournalBlock.Story));
        Assert.False(JournalBlocks.DividerBetween(JournalBlock.Story, JournalBlock.Story));
        Assert.True(JournalBlocks.DividerBetween(JournalBlock.Story, JournalBlock.Side));
        Assert.True(JournalBlocks.DividerBetween(JournalBlock.Side, JournalBlock.Virtual));
        Assert.False(JournalBlocks.DividerBetween(JournalBlock.Virtual, JournalBlock.Virtual));
    }

    [Theory]
    [InlineData(200f, 0f, 0f)]
    [InlineData(200f, float.NaN, 0f)]
    [InlineData(200f, 0.5f, 100f)]
    [InlineData(200f, 0.001f, 2f)]
    [InlineData(200f, 1f, 200f)]
    [InlineData(200f, 3f, 200f)]
    [InlineData(1f, 0.5f, 1f)]
    [InlineData(0f, 0.5f, 0f)]
    public void The_road_walks_its_fraction_and_a_start_always_shows(float length, float fraction, float walked)
    {
        Assert.Equal(walked, TreeRoad.Walked(length, fraction, minimum: 2f), 4);
    }
}
