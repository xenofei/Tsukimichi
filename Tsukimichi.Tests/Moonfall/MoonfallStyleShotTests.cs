using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The style shots (plan v9 G4; peg-mechanics.md §3, lines 75–87): each one's trigger and bonus, paid once a shot,
/// added to the shot's score unmultiplied and raised as an event.
/// </summary>
public sealed class MoonfallStyleShotTests
{
    private static List<MoonfallEvent> Drain(MoonfallGame game)
    {
        var seen = new List<MoonfallEvent>();
        while (game.TryReadEvent(out var e))
        {
            seen.Add(e);
        }

        return seen;
    }

    private static IEnumerable<MoonfallStyleShot> Styles(IEnumerable<MoonfallEvent> events) =>
        events.Where(e => e.Kind == MoonfallEventKind.StyleShot).Select(e => (MoonfallStyleShot)e.Count);

    private static IEnumerable<MoonfallStyleShot> Styles(IEnumerable<(long Tick, MoonfallEvent Event)> events) =>
        Styles(events.Select(e => e.Event));

    /// <summary>
    /// A ball in play high in the middle, out of everything's way, to touch pegs with. The first level has no purple peg
    /// (<see cref="MoonfallRules.FirstPurpleLevel"/>) and no greens, so the test's own blues stay blue.
    /// </summary>
    private static MoonfallGame InPlay(params MoonfallPeg[] pegs)
    {
        var game = new MoonfallGame(Board(pegs), 1, 1);
        Assert.Equal(-1, game.PurplePeg);
        game.PlaceBall(400, 30, 0, 0);
        return game;
    }

    /// <summary>Oranges far down the board that no test touches, so a few lit ones are not an Orange Sweep.</summary>
    private static MoonfallPeg[] SpareOranges(int count) =>
        [.. Enumerable.Range(0, count).Select(k => Orange(100 + (k * 45), 540))];

    [Theory]
    [InlineData(MoonfallStyleShot.OnePegCatch, 5_000)]
    [InlineData(MoonfallStyleShot.LongShot, 25_000)]
    [InlineData(MoonfallStyleShot.SuperLongShot, 50_000)]
    [InlineData(MoonfallStyleShot.DoubleLongShot, 25_000)]
    [InlineData(MoonfallStyleShot.OffTheWall, 25_000)]
    [InlineData(MoonfallStyleShot.RimShot, 25_000)]
    [InlineData(MoonfallStyleShot.LuckyBounce, 25_000)]
    [InlineData(MoonfallStyleShot.OrangeSweep, 50_000)]
    [InlineData(MoonfallStyleShot.LongSlide, 50_000)]
    [InlineData(MoonfallStyleShot.ClearNight, 50_000)]
    [InlineData(MoonfallStyleShot.LiveWire, 25_000)]
    public void Each_style_shot_pays_the_researched_bonus(MoonfallStyleShot kind, int bonus)
    {
        Assert.Equal(bonus, MoonfallGame.StyleShotBonus(kind));
    }

    // ---- Long Shot [O] and long legs [R §3 l.76–78] ----

    [Fact]
    public void A_long_shot_is_the_shots_first_two_pegs_both_coloured_more_than_200_px_apart()
    {
        // The original's rule (owner decision, 9 October 2026; it was any coloured leg of 267 px anywhere in a shot):
        // oranges 201 px apart are a Long Shot, 200 apart are not.
        var exact = InPlay(Orange(100, 200), Orange(300, 200));
        exact.TouchForTest(0, 0);
        exact.TouchForTest(0, 1);
        Assert.Empty(Styles(Drain(exact)));

        var game = InPlay(Orange(100, 200), Orange(301, 200));
        var before = game.ShotScore;
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        var events = Drain(game);
        Assert.Equal([MoonfallStyleShot.LongShot], Styles(events));
        var style = Assert.Single(events, e => e.Kind == MoonfallEventKind.StyleShot);
        Assert.Equal(MoonfallRules.LongShotBonus, style.Value);
        Assert.Equal(1, style.Peg);

        // The bonus is added after the peg score, unmultiplied.
        Assert.Equal(MoonfallRules.LongShotBonus, game.StyleBonus);
        Assert.Equal((game.ShotValue * game.ShotPegs) + MoonfallRules.LongShotBonus, game.ShotScore);
        Assert.True(game.ShotScore > before);
    }

    [Fact]
    public void A_long_shot_is_only_the_opening_of_a_shot()
    {
        // Two close oranges first: the shot's first two pegs are settled, so a far pair after them pays nothing.
        var late = InPlay(Orange(100, 200), Orange(150, 200), Orange(100, 400), Orange(640, 400));
        late.TouchForTest(0, 0);
        late.TouchForTest(0, 1);
        late.TouchForTest(0, 2);
        late.TouchForTest(0, 3);
        var lateStyles = Styles(Drain(late)).ToList();
        Assert.DoesNotContain(MoonfallStyleShot.LongShot, lateStyles);
        Assert.DoesNotContain(MoonfallStyleShot.SuperLongShot, lateStyles);

        // A blue first peg: no Long Shot that shot, however far apart the coloured pegs after it are.
        var blueFirst = InPlay(Blue(400, 300), Orange(100, 200), Orange(640, 200));
        blueFirst.TouchForTest(0, 0);
        blueFirst.TouchForTest(0, 1);
        blueFirst.TouchForTest(0, 2);
        Assert.Empty(Styles(Drain(blueFirst)));
    }

    [Fact]
    public void A_blue_grazed_just_before_the_second_coloured_peg_is_forgiven()
    {
        // Within 10 game ticks of the blue: still a Long Shot [O].
        var soon = InPlay(Orange(100, 200), Blue(200, 300), Orange(400, 200));
        soon.TouchForTest(0, 0);
        soon.TouchForTest(0, 1);
        for (var t = 0; t < MoonfallRules.LongShotBlueGraceTicks - 1; t++)
        {
            soon.Tick();
        }

        soon.TouchForTest(0, 2);
        Assert.Equal([MoonfallStyleShot.LongShot], Styles(Drain(soon)));

        // Ten ticks or more after it: the blue was the shot's second peg.
        var late = InPlay(Orange(100, 200), Blue(200, 300), Orange(400, 200));
        late.TouchForTest(0, 0);
        late.TouchForTest(0, 1);
        for (var t = 0; t < MoonfallRules.LongShotBlueGraceTicks; t++)
        {
            late.Tick();
        }

        late.TouchForTest(0, 2);
        Assert.Empty(Styles(Drain(late)));
    }

    [Fact]
    public void One_blue_on_the_way_is_still_a_long_shot_but_two_are_not()
    {
        var one = InPlay(Orange(100, 200), Blue(200, 300), Orange(400, 200));
        one.TouchForTest(0, 0);
        one.TouchForTest(0, 1);
        one.TouchForTest(0, 2);
        Assert.Equal([MoonfallStyleShot.LongShot], Styles(Drain(one)));

        var two = InPlay(Orange(100, 200), Blue(200, 300), Blue(250, 300), Orange(400, 200));
        two.TouchForTest(0, 0);
        two.TouchForTest(0, 1);
        two.TouchForTest(0, 2);
        two.TouchForTest(0, 3);
        Assert.Empty(Styles(Drain(two)));
    }

    [Fact]
    public void A_super_long_shot_is_more_than_450_px_and_pays_instead_of_a_long_shot()
    {
        // 451 px [O] (it was two thirds of the screen, 533 px).
        var game = InPlay(Orange(100, 200), Orange(551, 200));
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        Assert.Equal([MoonfallStyleShot.SuperLongShot], Styles(Drain(game)));
        Assert.Equal(MoonfallRules.SuperLongShotBonus, game.StyleBonus);

        // 450 px exactly is a Long Shot.
        var exact = InPlay(Orange(100, 200), Orange(550, 200));
        exact.TouchForTest(0, 0);
        exact.TouchForTest(0, 1);
        Assert.Equal([MoonfallStyleShot.LongShot], Styles(Drain(exact)));
    }

    [Fact]
    public void A_style_bonus_is_never_a_free_ball()
    {
        // A Long Shot's 25,000 takes the shot's score past the first free ball's 25,000, but only the peg score counts
        // [O] (owner decision, 9 October 2026; style bonuses used to count).
        var game = InPlay(Orange(100, 200), Orange(400, 200));
        var balls = game.BallsLeft;
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        var events = Drain(game);
        Assert.Contains(MoonfallStyleShot.LongShot, Styles(events));
        Assert.True(game.ShotScore >= MoonfallRules.FreeBallThresholds[0]);
        Assert.True(game.PegScore < MoonfallRules.FreeBallThresholds[0]);
        Assert.DoesNotContain(events, static e => e.Kind == MoonfallEventKind.FreeBall);
        Assert.Equal(0, game.FreeBallsThisShot);
        Assert.Equal(balls, game.BallsLeft);
    }

    [Fact]
    public void A_long_leg_straight_after_another_is_a_double_long_shot()
    {
        var game = InPlay([Orange(100, 200), Orange(400, 200), Orange(700, 220), .. SpareOranges(7)]);
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        game.TouchForTest(0, 2);
        Assert.Equal([MoonfallStyleShot.LongShot, MoonfallStyleShot.DoubleLongShot], Styles(Drain(game)));
        Assert.Equal(MoonfallRules.LongShotBonus + MoonfallRules.DoubleLongShotBonus, game.StyleBonus);
    }

    [Fact]
    public void Each_style_shot_is_paid_once_a_shot_and_again_the_next()
    {
        var game = InPlay(Orange(100, 200), Orange(400, 200), Orange(100, 400), Orange(400, 400), Orange(100, 500));
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        game.TouchForTest(0, 2);
        game.TouchForTest(0, 3);
        Assert.Equal(1, Styles(Drain(game)).Count(s => s == MoonfallStyleShot.LongShot));

        game.PlaceBall(400, 30, 0, 0);
        game.TouchForTest(0, 4);
        Assert.Equal(0, game.StyleBonus);
    }

    [Fact]
    public void Blue_pegs_make_no_long_shot()
    {
        var game = InPlay(Blue(100, 200), Blue(640, 200));
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        Assert.Empty(Styles(Drain(game)));
    }

    // ---- Off the Wall [R §3 l.79] ----

    [Fact]
    public void Off_the_wall_is_a_wall_bounce_then_a_coloured_peg_a_fifth_of_the_screen_away()
    {
        static MoonfallGame OffWall(params MoonfallPeg[] pegs)
        {
            var game = new MoonfallGame(Board(pegs), 1, 1);
            game.PlaceBall(MoonfallRules.LeftWall + 8, 200, -300, 0);
            RunUntil(game, static g => g.BallVelocityX > 0, 10);
            Assert.True(game.BallVelocityX > 0, "the ball came off the wall");
            return game;
        }

        var far = OffWall(Orange(300, 400), Orange(150, 400));
        far.TouchForTest(0, 0);
        Assert.Contains(MoonfallStyleShot.OffTheWall, Styles(Drain(far)));

        var near = OffWall(Orange(300, 400), Orange(150, 230));
        near.TouchForTest(0, 1);
        Assert.Empty(Styles(Drain(near)));

        // Only the first peg after the wall counts.
        var second = OffWall(Orange(300, 400), Blue(150, 230));
        second.TouchForTest(0, 1);
        second.TouchForTest(0, 0);
        Assert.Empty(Styles(Drain(second)));
    }

    // ---- Bucket shots [R §3 l.75, l.80, l.81] ----

    [Fact]
    public void One_peg_then_the_bucket_is_a_one_peg_catch()
    {
        var game = new MoonfallGame(Board(Blue(120, 250)), 1, 1);
        game.PlaceBall(MoonfallBucket.CentreAt(1), MoonfallRules.CatchLine - 1, 0, 150);
        game.TouchForTest(0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.BucketCatch);
        Assert.Equal([MoonfallStyleShot.OnePegCatch], Styles(events));
        Assert.Equal((game.ShotValue * 1) + MoonfallRules.OnePegCatchBonus, game.ShotScore);

        // Two pegs then the bucket is not.
        var two = new MoonfallGame(Board(Blue(120, 250), Blue(160, 250)), 1, 1);
        two.PlaceBall(MoonfallBucket.CentreAt(1), MoonfallRules.CatchLine - 1, 0, 150);
        two.TouchForTest(0, 0);
        two.TouchForTest(0, 1);
        events = RunUntil(two, static g => g.Phase != MoonfallPhase.Flying);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.BucketCatch);
        Assert.Empty(Styles(events));
    }

    /// <summary>A ball dropped onto the bucket's left rim, run until it bounces off it.</summary>
    private static MoonfallGame OffTheRim(params MoonfallPeg[] pegs)
    {
        var game = new MoonfallGame(Board(pegs), 1, 1);
        var rimX = MoonfallBucket.CentreAt(1) - MoonfallBucket.RimOffset;
        game.PlaceBall(rimX - 3, MoonfallRules.BucketTop + MoonfallBucket.RimRadius - MoonfallBucket.RimRadius - MoonfallRules.BallRadius - 1, 0, 200);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying || g.BallVelocityY < 0, 20);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.BucketBounce);
        return game;
    }

    [Fact]
    public void Off_the_bucket_into_the_last_orange_is_a_rim_shot()
    {
        var game = OffTheRim(Orange(400, 300));
        Assert.Equal(1, game.OrangesLeft);
        game.TouchForTest(0, 0);
        var events = Drain(game);
        Assert.Contains(events, e => e.Kind == MoonfallEventKind.FeverHit);
        Assert.Equal([MoonfallStyleShot.RimShot], Styles(events));

        // Not when another peg came between.
        var between = OffTheRim(Orange(400, 300), Blue(200, 300));
        between.TouchForTest(0, 1);
        between.TouchForTest(0, 0);
        Assert.DoesNotContain(MoonfallStyleShot.RimShot, Styles(Drain(between)));

        // Not for an orange that is not the last.
        var notLast = OffTheRim(Orange(400, 300), Orange(200, 300));
        notLast.TouchForTest(0, 0);
        Assert.Empty(Styles(Drain(notLast)));
    }

    [Fact]
    public void Lucky_bounce_is_paid_exactly_when_a_caught_ball_was_off_the_rim_long_enough_or_high_enough()
    {
        // Shots on an empty board at every angle and several moments of the bucket's sweep: whatever happens, the
        // rule holds: a catch after a rim bounce (the first since the last peg) 0.5 s or more before, or 150 px below
        // the ball's highest point since, is a Lucky Bounce, and nothing else is.
        var lucky = 0;
        var plain = 0;
        var rimCatches = 0;
        for (var wait = 0; wait < 600; wait += 15)
        {
            for (var angle = -85.0; angle <= 85; angle += 5)
            {
                var game = new MoonfallGame(Board(), 1, 1);
                for (var t = 0; t < wait; t++)
                {
                    game.Tick();
                }

                Assert.True(game.Shoot(angle));
                long rimTick = -1;
                double rimY = 0, peak = 0;
                var caught = false;
                var paid = false;
                while (game.Phase == MoonfallPhase.Flying)
                {
                    game.Tick();
                    while (game.TryReadEvent(out var e))
                    {
                        switch (e.Kind)
                        {
                            case MoonfallEventKind.BucketBounce when rimTick < 0:
                                rimTick = game.GameTick;
                                rimY = e.Y;
                                peak = e.Y;
                                break;
                            case MoonfallEventKind.BucketCatch:
                                caught = true;
                                break;
                            case MoonfallEventKind.StyleShot when (MoonfallStyleShot)e.Count == MoonfallStyleShot.LuckyBounce:
                                paid = true;
                                break;
                        }
                    }

                    if (rimTick >= 0 && game.Phase == MoonfallPhase.Flying)
                    {
                        peak = Math.Min(peak, game.BallY);
                    }
                }

                var expected = caught && rimTick >= 0;
                rimCatches += expected ? 1 : 0;
                if (expected && paid)
                {
                    lucky++;
                }
                else if (caught)
                {
                    plain++;
                }

                // The bounce tick is read once per tick, so it may be a tick late: allow that tick at the boundary.
                if (expected && paid != (game.GameTick - rimTick >= MoonfallRules.LuckyBounceTicks || rimY - peak >= MoonfallRules.LuckyBounceRise))
                {
                    Assert.InRange(game.GameTick - rimTick, MoonfallRules.LuckyBounceTicks - 1, MoonfallRules.LuckyBounceTicks);
                }

                if (!expected)
                {
                    Assert.False(paid, $"a Lucky Bounce with no rim bounce and catch at {angle}° after {wait}");
                }
            }
        }

        Assert.True(lucky > 0, $"no Lucky Bounce in the sweep ({rimCatches} catches after a rim bounce, {plain} others)");
        Assert.True(plain > 0, "no plain catch in the sweep");
    }

    // ---- Orange Sweep [R §3 l.82, O] ----

    [Theory]
    [InlineData(35, 15)]
    [InlineData(31, 15)]
    [InlineData(30, 10)]
    [InlineData(25, 10)]
    [InlineData(21, 10)]
    [InlineData(20, 9)]
    [InlineData(16, 9)]
    [InlineData(15, 8)]
    [InlineData(11, 8)]
    [InlineData(10, 8)]
    [InlineData(7, 5)]
    [InlineData(6, 5)]
    [InlineData(5, 4)]
    public void An_orange_sweep_needs_the_originals_count_for_the_oranges_dealt(int oranges, int needs)
    {
        // The original's table [O] (it was a third of the oranges left, at least three).
        Assert.Equal(needs, MoonfallRules.OrangeSweepNeeds(oranges));
    }

    [Theory]
    [InlineData(25, 10)]
    [InlineData(10, 8)]
    [InlineData(6, 5)]
    public void An_orange_sweep_pays_when_one_shot_lights_that_many(int oranges, int needs)
    {
        var pegs = new MoonfallPeg[oranges + 5];
        for (var k = 0; k < pegs.Length; k++)
        {
            pegs[k] = k < oranges ? Orange(100 + ((k % 10) * 60), 150 + ((k / 10) * 60)) : Blue(100 + ((k % 10) * 60), 400);
        }

        var game = InPlay(pegs);
        for (var k = 0; k < needs - 1; k++)
        {
            game.LightForTest(k);
        }

        Assert.DoesNotContain(MoonfallStyleShot.OrangeSweep, Styles(Drain(game)));
        game.LightForTest(needs - 1);
        Assert.Contains(MoonfallStyleShot.OrangeSweep, Styles(Drain(game)));
    }

    [Fact]
    public void A_late_shot_needs_the_full_count_however_few_oranges_are_left()
    {
        // 25 dealt: 18 lit in one shot (a sweep at the 10th), then the next shot lights 6 of the 7 left: no sweep, where
        // the old rule (a third of those left) paid one at 3.
        var pegs = new MoonfallPeg[30];
        for (var k = 0; k < pegs.Length; k++)
        {
            pegs[k] = k < 25 ? Orange(100 + ((k % 10) * 60), 150 + ((k / 10) * 60)) : Blue(100 + ((k % 10) * 60), 400);
        }

        var game = InPlay(pegs);
        Assert.Equal(10, game.OrangeSweepNeeds);
        for (var k = 0; k < 18; k++)
        {
            game.LightForTest(k);
        }

        Assert.Contains(MoonfallStyleShot.OrangeSweep, Styles(Drain(game)));
        game.PlaceBall(400, 30, 0, 0);
        for (var k = 18; k < 24; k++)
        {
            game.LightForTest(k);
        }

        Assert.DoesNotContain(MoonfallStyleShot.OrangeSweep, Styles(Drain(game)));
    }

    [Fact]
    public void Two_oranges_left_can_make_no_sweep()
    {
        var game = InPlay(Orange(100, 200), Orange(200, 200));
        game.LightForTest(0);
        game.LightForTest(1);
        Assert.DoesNotContain(MoonfallStyleShot.OrangeSweep, Styles(Drain(game)));
    }

    // ---- Long Slide [R §3 l.83] ----

    [Fact]
    public void Twelve_pegs_in_one_slide_is_a_long_slide()
    {
        // Fourteen short bricks end to end down a gentle slope; a ball set on the top one slides down them all.
        var bricks = new MoonfallPeg[14];
        for (var k = 0; k < bricks.Length; k++)
        {
            var x = 150 + (k * 32.0);
            bricks[k] = MoonfallPeg.Line(x, 200 + (k * 32.0 * 0.4), x + 30, 200 + (((k * 32.0) + 30) * 0.4), canBeOrange: false);
        }

        var game = new MoonfallGame(Board(bricks), 1, 1);
        game.PlaceBall(155, 200 - MoonfallRules.BrickThickness, 0, 0);
        var events = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying || g.BallX > 600);
        Assert.True(game.ShotPegs >= MoonfallRules.LongSlidePegs, $"the slide lit only {game.ShotPegs}");
        Assert.Contains(MoonfallStyleShot.LongSlide, Styles(events));
    }

    [Fact]
    public void Pegs_touched_with_gaps_between_are_no_slide()
    {
        // Off to the left, out of the falling ball's way.
        var pegs = new MoonfallPeg[13];
        for (var k = 0; k < pegs.Length; k++)
        {
            pegs[k] = Blue(110 + ((k % 4) * 40), 150 + ((k / 4) * 40));
        }

        var game = InPlay(pegs);
        for (var k = 0; k < pegs.Length; k++)
        {
            game.TouchForTest(0, k);
            for (var t = 0; t <= MoonfallRules.SlideGapTicks; t++)
            {
                game.Tick();
            }
        }

        Assert.DoesNotContain(MoonfallStyleShot.LongSlide, Styles(Drain(game)));

        // The same twelve touches with no gap are a slide.
        game = InPlay(pegs);
        for (var k = 0; k < MoonfallRules.LongSlidePegs; k++)
        {
            game.TouchForTest(0, k);
        }

        Assert.Contains(MoonfallStyleShot.LongSlide, Styles(Drain(game)));
    }

    // ---- Clear Night [R §3 l.84] ----

    [Fact]
    public void Every_peg_lit_by_the_end_of_the_full_moon_shot_is_a_clear_night()
    {
        static List<(long Tick, MoonfallEvent Event)> FullMoonShot(params MoonfallPeg[] others)
        {
            // The last orange straight under the launcher; the blues are lit first in the same shot.
            var game = new MoonfallGame(Board([Orange(400, 300), .. others]), 1, 1);
            Assert.True(game.Shoot(0));
            for (var k = 1; k <= others.Length && k < 2; k++)
            {
                game.LightForTest(k);
            }

            return RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
        }

        var clear = FullMoonShot(Blue(150, 150));
        Assert.Contains(clear, e => e.Event.Kind == MoonfallEventKind.FeverLanded);
        Assert.Contains(MoonfallStyleShot.ClearNight, Styles(clear));

        // A peg left standing, or a shot of one peg, is not.
        var standing = FullMoonShot(Blue(150, 150), Blue(650, 150));
        Assert.DoesNotContain(MoonfallStyleShot.ClearNight, Styles(standing));
        var single = FullMoonShot();
        Assert.Contains(single, e => e.Event.Kind == MoonfallEventKind.FeverLanded);
        Assert.DoesNotContain(MoonfallStyleShot.ClearNight, Styles(single));
    }

    // ---- Live Wire [R §3 l.85] ----

    [Fact]
    public void A_bolt_that_lights_twelve_pegs_is_a_live_wire()
    {
        static List<MoonfallEvent> Bolt(int pegs)
        {
            // The bucket's place when the bolt's shot begins, from a probe played the same way.
            var probe = new MoonfallGame(Board(Blue(120, 250)), 1, 1, power: MoonfallPower.Bolt);
            Arm(probe);
            var x = probe.BucketX;

            // A column above the bucket: the first peg, then the ones the bolt will cross.
            var column = new List<MoonfallPeg> { Blue(120, 250) };
            for (var k = 0; k <= pegs; k++)
            {
                column.Add(Blue(x, 140 + (k * 30)));
            }

            var game = new MoonfallGame(Board([.. column]), 1, 1, power: MoonfallPower.Bolt);
            Arm(game);
            Assert.Equal(x, game.BucketX);
            game.PlaceBall(400, 30, 0, 0);
            game.TouchForTest(0, 1);
            return Drain(game);
        }

        static void Arm(MoonfallGame game)
        {
            game.MakeGreenForTest(0);
            game.PlaceBall(600, 598, 0, 300);
            game.TouchForTest(0, 0);
            RunUntil(game, static g => g.Phase == MoonfallPhase.Aiming);
            Drain(game);
        }

        var twelve = Bolt(12);
        Assert.Equal(12, Assert.Single(twelve, e => e.Kind == MoonfallEventKind.BoltStruck).Value);
        Assert.Contains(MoonfallStyleShot.LiveWire, Styles(twelve));

        var eleven = Bolt(11);
        Assert.Equal(11, Assert.Single(eleven, e => e.Kind == MoonfallEventKind.BoltStruck).Value);
        Assert.DoesNotContain(MoonfallStyleShot.LiveWire, Styles(eleven));
    }
}
