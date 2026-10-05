using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Fever (plan v9 G3; peg-measurements.md row 8): the approach's slow motion and zoom, the hit, the speed back up to
/// half, the five Fever buckets and the tally.
/// </summary>
public sealed class MoonfallFeverTests
{
    /// <summary>One orange straight under the launcher (every other peg may not be orange), so a straight shot is the last orange.</summary>
    private static MoonfallGame LastOrangeBelow(params MoonfallPeg[] others) =>
        new(Board([Orange(400, 300), .. others]), 1, 1);

    [Fact]
    public void The_approach_slows_to_a_tenth_and_zooms_to_2x_in_048_s_about_011_s_of_game_time_before_the_touch()
    {
        var game = LastOrangeBelow();
        Assert.Equal(1, game.OrangesLeft);
        Assert.True(game.Shoot(0));
        long approachGame = -1, approachReal = -1;
        while (!game.Fever)
        {
            game.Tick();
            if (approachGame < 0 && game.Approaching)
            {
                approachGame = game.GameTick;
                approachReal = game.RealTick;
                Assert.Equal(0.1, game.Speed, 9);
            }

            if (approachReal >= 0 && game.RealTick - approachReal == 24 && !game.Fever)
            {
                Assert.Equal(1.5, game.Zoom, 2);
            }

            Assert.True(game.RealTick < 5000);
        }

        // [M 8]: the slow motion starts 0.08–0.13 s of game time before the contact, and 1.10 ± 0.18 s of real time.
        Assert.InRange(game.GameTick - approachGame, 8, 13);
        Assert.InRange((game.RealTick - approachReal) / 100.0, 1.10 - 0.3, 1.10 + 0.3);
        Assert.Equal(2.0, game.Zoom, 2);
    }

    [Fact]
    public void After_the_hit_the_zoom_returns_at_030_a_second_the_banner_shows_295_s_and_the_speed_ramps_to_half()
    {
        var game = LastOrangeBelow();
        game.Shoot(0);
        RunUntil(game, static g => g.Fever);
        var hit = game.RealTick;
        var bannerFrom = -1L;
        var bannerTo = -1L;
        var zoomAt1 = -1L;
        while (game.Phase == MoonfallPhase.Flying && game.RealTick - hit < 2000)
        {
            game.Tick();
            if (game.BannerVisible && bannerFrom < 0)
            {
                bannerFrom = game.RealTick - hit;
            }

            if (!game.BannerVisible && bannerFrom >= 0 && bannerTo < 0)
            {
                bannerTo = game.RealTick - hit;
            }

            if (zoomAt1 < 0 && game.Zoom <= 1.0)
            {
                zoomAt1 = game.RealTick - hit;
            }

            if (game.RealTick - hit == 333)
            {
                Assert.Equal(0.5, game.Speed, 9);
            }
        }

        // [M 8]: banner 0.05–0.11 s after the hit, for 2.95 s; zoom back to 1 at −0.30/s from 2 (3.27–3.32 s).
        Assert.InRange(bannerFrom, 5, 11);
        Assert.Equal(295, bannerTo - bannerFrom);
        Assert.InRange(zoomAt1 / 100.0, 3.27, 3.35);
    }

    [Fact]
    public void A_perfect_clear_pays_100000_in_any_Fever_bucket_and_the_tally_adds_10000_a_ball()
    {
        var game = LastOrangeBelow();
        game.Shoot(0);
        var events = RunUntil(game, static g => g.Phase == MoonfallPhase.Won);
        Assert.True(game.Perfect);
        var hit = events.Single(e => e.Event.Kind == MoonfallEventKind.FeverHit);
        Assert.Equal(1, hit.Event.Value);
        var landed = events.Single(e => e.Event.Kind == MoonfallEventKind.FeverLanded);
        Assert.Equal(100_000, landed.Event.Value);

        // The last orange at ×10 (1 left when touched): 1,000 × 1 peg.
        var tally = Assert.NotNull(game.Tally);
        Assert.Equal(1_000, tally.LevelScore);
        Assert.Equal(100_000, tally.FeverBonus);
        Assert.Equal(9, tally.BallsLeft);
        Assert.Equal(90_000, tally.BallBonus);
        Assert.Equal(191_000, tally.Total);
        Assert.Equal(191_000, game.Score);
        RunUntil(game, static g => g.ShownScore == g.Score);
        Assert.Equal(191_000, game.ShownScore);
    }

    [Fact]
    public void Without_a_perfect_clear_the_bucket_pays_its_own_value()
    {
        // A blue peg left standing far to the side: the clear is not perfect.
        var game = LastOrangeBelow(Blue(650, 450));
        game.Shoot(0);
        var events = RunUntil(game, static g => g.Phase == MoonfallPhase.Won);
        Assert.False(game.Perfect);
        var landed = events.Single(e => e.Event.Kind == MoonfallEventKind.FeverLanded);
        int[] values = [10_000, 50_000, 100_000, 50_000, 10_000];
        Assert.Equal(values[landed.Event.Peg], landed.Event.Value);
        Assert.Equal(landed.Event.Peg, MoonfallBucket.FeverBucketAt(landed.Event.X));
    }

    [Theory]
    [InlineData(80, 0)]
    [InlineData(205, 0)]
    [InlineData(206, 1)]
    [InlineData(400, 2)]
    [InlineData(530, 3)]
    [InlineData(720, 4)]
    public void The_five_Fever_buckets_split_the_board_evenly(double x, int bucket)
    {
        Assert.Equal(bucket, MoonfallBucket.FeverBucketAt(x));
    }

    [Fact]
    public void A_ball_that_misses_the_last_orange_leaves_the_approach_at_full_speed()
    {
        // The orange off to the side: a straight shot never comes near it, so no approach.
        var game = new MoonfallGame(Board(Orange(650, 300)), 1, 1);
        game.Shoot(0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
        Assert.DoesNotContain(events, e => e.Event.Kind == MoonfallEventKind.FeverApproach);
        Assert.Equal(1.0, game.Speed);
    }
}
