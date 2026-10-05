using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Pegs, scoring, the bucket, the end of a turn and the stuck ball (plan v9 G1–G3): the rules from peg-mechanics.md
/// and the timings from peg-measurements.md.
/// </summary>
public sealed class MoonfallRulesTests
{
    // ---- Pegs [R §2] ----

    [Fact]
    public void A_level_has_25_oranges_picked_from_the_pegs_that_may_be_orange()
    {
        var pegs = new List<MoonfallPeg>();
        for (var i = 0; i < 60; i++)
        {
            pegs.Add(MoonfallPeg.Round(110 + ((i % 15) * 40), 220 + ((i / 15) * 45), canBeOrange: i % 3 != 0));
        }

        var game = new MoonfallGame(Board([.. pegs]), 1, 7);
        Assert.Equal(25, game.OrangesLeft);
        var oranges = Enumerable.Range(0, game.PegCount).Where(i => game.Peg(i).Colour == PegColour.Orange).ToList();
        Assert.Equal(25, oranges.Count);
        Assert.All(oranges, i => Assert.True(pegs[i].CanBeOrange));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 2)]
    [InlineData(12, 2)]
    public void Two_greens_appear_from_the_third_level(int levelNumber, int greens)
    {
        var game = new MoonfallGame(Grid(60), levelNumber, 3);
        Assert.Equal(greens, Enumerable.Range(0, game.PegCount).Count(i => game.Peg(i).Colour == PegColour.Green));
    }

    [Fact]
    public void One_purple_moves_to_another_blue_peg_each_shot()
    {
        var game = new MoonfallGame(Grid(60), 3, 11);
        var seen = new List<int>();
        for (var shot = 0; shot < 5; shot++)
        {
            var purple = game.PurplePeg;
            Assert.True(purple >= 0);
            Assert.Single(Enumerable.Range(0, game.PegCount), i => game.Peg(i).Colour == PegColour.Purple);
            seen.Add(purple);

            // A shot that touches nothing (straight into the wall and out), then the next ball.
            game.PlaceBall(90, 580, 0, 300);
            RunUntil(game, static g => g.Phase == MoonfallPhase.Aiming);
            Assert.NotEqual(purple, game.PurplePeg);
            Assert.Equal(PegColour.Blue, game.Peg(purple).Colour);
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_board_and_another_seed_another()
    {
        static string Colours(MoonfallGame g) => string.Concat(Enumerable.Range(0, g.PegCount).Select(i => (char)('0' + (int)g.Peg(i).Colour)));
        Assert.Equal(Colours(new MoonfallGame(Grid(60), 3, 42)), Colours(new MoonfallGame(Grid(60), 3, 42)));
        Assert.NotEqual(Colours(new MoonfallGame(Grid(60), 3, 42)), Colours(new MoonfallGame(Grid(60), 3, 43)));
    }

    // ---- Scoring [R §3, M correction] ----

    [Theory]
    [InlineData(25, 1)]
    [InlineData(16, 1)]
    [InlineData(15, 2)]
    [InlineData(11, 2)]
    [InlineData(10, 3)]
    [InlineData(7, 3)]
    [InlineData(6, 5)]
    [InlineData(4, 5)]
    [InlineData(3, 10)]
    [InlineData(1, 10)]
    [InlineData(0, 10)]
    [InlineData(45, 1)]
    public void The_multiplier_steps_at_15_10_6_and_3_oranges_left(int left, int multiplier)
    {
        Assert.Equal(multiplier, MoonfallRules.Multiplier(left));
    }

    [Fact]
    public void Peg_values_are_10_10_100_and_500()
    {
        Assert.Equal(10, MoonfallRules.BaseValue(PegColour.Blue));
        Assert.Equal(10, MoonfallRules.BaseValue(PegColour.Green));
        Assert.Equal(100, MoonfallRules.BaseValue(PegColour.Orange));
        Assert.Equal(500, MoonfallRules.BaseValue(PegColour.Purple));
    }

    [Fact]
    public void A_shot_scores_its_peg_values_times_its_peg_count()
    {
        // The footage's Deluxe 1-1 first shot: 8 oranges and 10 blues at ×1, "900 × 18 PEGS", and the HUD rises 16,200.
        var game = new MoonfallGame(Grid(60), 1, 5);
        game.PlaceBall(90, 580, 0, 300);
        var oranges = Indices(game, PegColour.Orange).Take(8);
        var blues = Indices(game, PegColour.Blue).Take(10);
        foreach (var i in oranges.Concat(blues))
        {
            game.LightForTest(i);
        }

        Assert.Equal(900, game.ShotValue);
        Assert.Equal(18, game.ShotPegs);
        Assert.Equal(16_200, game.ShotScore);
        RunUntil(game, static g => g.Phase == MoonfallPhase.Aiming);
        Assert.Equal(16_200, game.Score);
    }

    [Fact]
    public void A_peg_scores_the_multiplier_in_force_when_it_is_touched()
    {
        // 16 oranges left: the 10th orange of the level scores ×1 and lifts the meter to ×2 for the next peg.
        var game = new MoonfallGame(Grid(60), 1, 5);
        game.PlaceBall(90, 580, 0, 300);
        var oranges = Indices(game, PegColour.Orange).ToList();
        for (var k = 0; k < 10; k++)
        {
            game.LightForTest(oranges[k]);
        }

        Assert.Equal(15, game.OrangesLeft);
        Assert.Equal(1000, game.ShotValue);
        Assert.Equal(2, game.Multiplier);
        game.LightForTest(Indices(game, PegColour.Purple).Single());
        Assert.Equal(1000 + (500 * 2), game.ShotValue);
    }

    [Fact]
    public void A_shot_earns_free_balls_at_25k_75k_and_125k()
    {
        // 25 oranges and a purple in one shot: the score passes all three thresholds as it climbs.
        var game = new MoonfallGame(Grid(60), 1, 9);
        var balls = game.BallsLeft;
        game.PlaceBall(90, 580, 0, 300);
        var thresholds = new List<long>();
        var order = Indices(game, PegColour.Orange).Take(24).Append(Indices(game, PegColour.Purple).Single()).ToList();
        foreach (var i in order)
        {
            game.LightForTest(i);
            while (game.TryReadEvent(out var e))
            {
                if (e.Kind == MoonfallEventKind.FreeBall)
                {
                    thresholds.Add(e.Value);
                    Assert.True(game.ShotScore >= e.Value);
                }
            }
        }

        Assert.Equal([25_000L, 75_000L, 125_000L], thresholds);
        Assert.Equal(balls + 3, game.BallsLeft);
        Assert.Equal(3, game.FreeBallsThisShot);
    }

    [Fact]
    public void Ten_balls_then_the_level_is_lost_with_oranges_left()
    {
        // Pegs only on the left; every shot goes hard right, off the wall and down the right side.
        var pegs = new List<MoonfallPeg>();
        for (var i = 0; i < 30; i++)
        {
            pegs.Add(MoonfallPeg.Round(100 + ((i % 5) * 30), 250 + ((i / 5) * 40)));
        }

        var game = new MoonfallGame(Board([.. pegs]), 1, 2);
        Assert.Equal(10, game.BallsLeft);
        var shots = 0;
        var catches = 0;
        while (game.Phase == MoonfallPhase.Aiming && shots < 40)
        {
            Assert.True(game.Shoot(85));
            shots++;
            foreach (var (_, e) in RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Lost))
            {
                catches += e.Kind == MoonfallEventKind.BucketCatch ? 1 : 0;
            }
        }

        Assert.Equal(MoonfallPhase.Lost, game.Phase);
        Assert.Equal(10 + catches, shots);
        Assert.Equal(0, game.BallsLeft);
        Assert.Equal(25, game.OrangesLeft);
        Assert.True(game.OutOfBalls);
    }

    // ---- The score counter [M 9] ----

    [Fact]
    public void The_counter_adds_1000_200_100_then_10_a_tick()
    {
        // Nights 1-1: 9,880 → 46,520 (36,640), +1,000 a tick until 10,640 remained, then 1,000 + 4 × 200, ... and
        // the tail +100, +100, +100, +10, +10 then +20. 0.87 s read off 60 fps frames; the rule gives 83 ticks.
        var counter = default(ScoreCounter);
        counter.Reset(9_880);
        counter.SetTarget(46_520);
        var steps = new List<long>();
        var last = counter.Shown;
        while (!counter.Settled)
        {
            counter.Tick();
            steps.Add(counter.Shown - last);
            last = counter.Shown;
        }

        Assert.Equal(83, steps.Count);
        Assert.All(steps.Take(27), s => Assert.Equal(1000, s));
        Assert.Equal(10_640 - 1000, 46_520 - (9_880 + (27 * 1000)));
        Assert.Equal(200, steps[27]);
        Assert.Equal([100L, 100L, 100L, 10L, 10L, 10L, 10L], steps.Skip(76));
    }

    [Fact]
    public void A_16200_shot_counts_up_in_57_ticks()
    {
        var counter = default(ScoreCounter);
        counter.SetTarget(16_200);
        var ticks = 0;
        while (!counter.Settled)
        {
            counter.Tick();
            ticks++;
        }

        Assert.Equal(7 + 42 + 8, ticks);
        Assert.Equal(16_200, counter.Shown);
    }

    [Fact]
    public void The_counter_waits_its_hold_before_counting()
    {
        var counter = default(ScoreCounter);
        counter.SetTarget(100, 3);
        counter.Tick();
        counter.Tick();
        counter.Tick();
        Assert.Equal(0, counter.Shown);
        counter.Tick();
        Assert.Equal(100, counter.Shown);
    }

    // ---- The bucket [M 5] ----

    [Fact]
    public void The_bucket_sweeps_a_six_second_sine_of_260_px_about_401_5()
    {
        for (var tick = 0L; tick <= 1200; tick += 7)
        {
            var expected = 401.5 + (260 * Math.Sin(2 * Math.PI * tick / 600.0));
            Assert.Equal(expected, MoonfallBucket.CentreAt(tick), 9);
        }

        Assert.Equal(661.5, MoonfallBucket.CentreAt(150), 9);
        Assert.Equal(141.5, MoonfallBucket.CentreAt(450), 9);
        Assert.Equal(MoonfallBucket.CentreAt(37), MoonfallBucket.CentreAt(637), 9);
        Assert.Equal(272.3, MoonfallBucket.VelocityAt(0), 1);
        Assert.Equal(104, MoonfallRules.BucketMouth);
        Assert.Equal(131, (2 * MoonfallBucket.RimOffset) + (2 * MoonfallBucket.RimRadius), 9);
    }

    [Fact]
    public void A_ball_over_the_mouth_is_caught_for_a_free_ball()
    {
        var game = new MoonfallGame(Board(), 1, 1);
        var balls = game.BallsLeft;
        // Drop the ball where the bucket will be when the ball comes down to the catch line.
        var fall = Math.Sqrt(2 * (MoonfallRules.CatchLine - 420) / MoonfallRules.Gravity);
        game.PlaceBall(MoonfallBucket.CentreAt((long)Math.Round(fall * 100)), 420, 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying, 200);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.BucketCatch);
        Assert.Equal(balls + 1, game.BallsLeft);
    }

    [Fact]
    public void A_ball_on_the_rim_bounces_off_it()
    {
        var game = new MoonfallGame(Board(), 1, 1);
        // Tick 0 to 60: the bucket's left rim post is at x ≈ 401.5 + 260·sin(2π·t/600) − 58.75; aim at it.
        var t = 0.6;
        var rimX = MoonfallBucket.CentreAt(60) - MoonfallBucket.RimOffset;
        game.PlaceBall(rimX - 3, 573 - (0.5 * 500 * t * t), 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying, 300);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.BucketBounce);
    }

    [Fact]
    public void A_ball_beside_the_bucket_is_lost()
    {
        var game = new MoonfallGame(Board(), 1, 1);
        var balls = game.BallsLeft;
        game.PlaceBall(100, 500, 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying, 200);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.BallLost);
        Assert.Equal(balls, game.BallsLeft);
    }

    // ---- The end of a turn [M 7] ----

    [Fact]
    public void Lit_pegs_clear_057_s_after_the_ball_leaves_then_one_every_50_ms_in_hit_order()
    {
        // [M 7]: first pop 0.57 s after the ball leaves, then one every 50 ms, first hit first; the next ball 0.8 s
        // after the last pop. The pegs are lit in a known order while a ball drops out of play.
        var game = new MoonfallGame(Board(Blue(300, 250), Blue(360, 330), Blue(420, 410), Blue(480, 490)), 1, 1);
        game.PlaceBall(90, 560, 0, 300);
        int[] order = [2, 0, 3, 1];

        // No oranges on this board, so the meter stands at ×10 (0 left).
        var expectedValue = order.Sum(i => MoonfallRules.BaseValue(game.Peg(i).Colour) * 10L);
        foreach (var i in order)
        {
            game.LightForTest(i);
        }

        var events = RunUntil(game, static g => g.Phase == MoonfallPhase.Aiming, 2000);
        var exit = events.Single(e => e.Event.Kind == MoonfallEventKind.BallLost).Tick;
        var clears = events.Where(e => e.Event.Kind == MoonfallEventKind.PegCleared).ToList();
        Assert.Equal(order, clears.Select(c => c.Event.Peg));
        Assert.Equal(exit + 57, clears[0].Tick);
        for (var k = 1; k < clears.Count; k++)
        {
            Assert.Equal(5, clears[k].Tick - clears[k - 1].Tick);
        }

        Assert.Equal(Enumerable.Range(1, clears.Count), clears.Select(c => c.Event.Count));
        var scored = events.Single(e => e.Event.Kind == MoonfallEventKind.ShotScored);
        Assert.Equal(clears[^1].Tick, scored.Tick);
        Assert.Equal(expectedValue * 4, scored.Event.Value);
        var next = events.Single(e => e.Event.Kind == MoonfallEventKind.NextBall);
        Assert.Equal(scored.Tick + 80, next.Tick);
    }

    // ---- The stuck ball (judgement, MoonfallRules.StuckTicks) ----

    [Fact]
    public void A_ball_wedged_between_two_pegs_clears_the_last_one_hit_after_1_5_s_and_falls_free()
    {
        // Two touching pegs make a notch; a ball dropped into it settles and stays.
        var game = new MoonfallGame(Board(Blue(390, 300), Blue(410, 300)), 1, 1);
        game.PlaceBall(400, 270, 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying, 2000);
        var stuck = events.Where(e => e.Event.Kind == MoonfallEventKind.StuckClear).ToList();
        Assert.Single(stuck);
        var hits = events.Where(e => e.Event.Kind == MoonfallEventKind.PegHit).Select(e => e.Event.Peg).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(hits[^1], stuck[0].Event.Peg);

        // It cleared 1.5 s after the ball last moved more than 15 px, and the ball then left the board.
        Assert.InRange(stuck[0].Tick, 150, 260);
        Assert.Contains(events, e => e.Event.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch);
        Assert.Equal(2, game.ShotPegs);

        // The early peg is not cleared twice: the end of turn pops only the other.
        var popped = RunUntil(game, static g => g.Phase == MoonfallPhase.Aiming).Where(e => e.Event.Kind == MoonfallEventKind.PegCleared).ToList();
        Assert.Single(popped);
        Assert.Equal(hits[0], popped[0].Event.Peg);
    }

    [Fact]
    public void A_ball_rolling_down_a_brick_is_never_called_stuck()
    {
        var game = new MoonfallGame(Board(MoonfallPeg.Line(300, 300, 500, 360, canBeOrange: false)), 1, 1);
        game.PlaceBall(320, 270, 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying, 2000);
        Assert.DoesNotContain(events, e => e.Event.Kind == MoonfallEventKind.StuckClear);
        Assert.Contains(events, e => e.Event.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch);
    }

    [Fact]
    public void A_ball_rocking_in_a_bowl_clears_the_bowl_after_3_s_without_sinking()
    {
        // A curved brick like a bowl: the ball rocks wider than 15 px but gets no lower; after 3 s it clears.
        var game = new MoonfallGame(Board(MoonfallPeg.Arc(400, 200, 200, 30, 120, canBeOrange: false)), 1, 1);
        game.PlaceBall(520, 300, 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying, 3000);
        var stuck = events.Single(e => e.Event.Kind == MoonfallEventKind.StuckClear);
        Assert.Equal(0, stuck.Event.Peg);
        Assert.InRange(stuck.Tick, 300, 600);
        Assert.Contains(events, e => e.Event.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch);
    }

    // ---- The aim guide [M 11] ----

    [Fact]
    public void The_guide_dots_are_17_px_apart_and_end_at_the_first_peg()
    {
        var game = new MoonfallGame(Board(Blue(400, 300)), 1, 1);
        Span<(double X, double Y)> dots = stackalloc (double, double)[MoonfallRules.GuideMaxDots];
        var n = game.Guide(0, dots);
        Assert.True(n >= 3);
        for (var k = 1; k < n - 1; k++)
        {
            var step = Math.Sqrt(Math.Pow(dots[k].X - dots[k - 1].X, 2) + Math.Pow(dots[k].Y - dots[k - 1].Y, 2));
            Assert.Equal(17, step, 0.6);
        }

        // The last dot is where the ball would touch the peg: 16 px above its centre.
        Assert.Equal(300 - 16, dots[n - 1].Y, 0.6);
    }

    [Fact]
    public void With_nothing_in_the_way_the_guide_stops_at_200_px()
    {
        var game = new MoonfallGame(Board(), 1, 1);
        Span<(double X, double Y)> dots = stackalloc (double, double)[MoonfallRules.GuideMaxDots];
        var n = game.Guide(30, dots);
        double length = 0;
        for (var k = 1; k < n; k++)
        {
            length += Math.Sqrt(Math.Pow(dots[k].X - dots[k - 1].X, 2) + Math.Pow(dots[k].Y - dots[k - 1].Y, 2));
        }

        Assert.InRange(length, 200 - 17, 200);
        Assert.InRange(n, 12, MoonfallRules.GuideMaxDots);
    }

    [Theory]
    [InlineData(600, 87, 85)]
    [InlineData(200, 50, -85)]
    [InlineData(400, 400, 0)]
    [InlineData(500, 187, 45)]
    public void The_aim_follows_the_pointer_within_85_degrees(double x, double y, double angle)
    {
        Assert.Equal(angle, MoonfallGame.AimAt(x, y), 6);
        var game = new MoonfallGame(Board(), 1, 1);
        Assert.True(game.Shoot(120));
        Assert.Equal(Math.Sin(85 * Math.PI / 180) * 395, game.BallVelocityX, 6);
    }

    private static IEnumerable<int> Indices(MoonfallGame game, PegColour colour) =>
        Enumerable.Range(0, game.PegCount).Where(i => game.Peg(i).Colour == colour);
}
