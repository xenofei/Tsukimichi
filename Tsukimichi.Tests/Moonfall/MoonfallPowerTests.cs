using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The eleven powers (plan v9 G5; peg-mechanics.md §5, lines 107–117): what triggers each, what it does, how many shots
/// it lasts, and how a second green adds to it.
/// </summary>
public sealed class MoonfallPowerTests
{
    /// <summary>A board whose peg 0 is the green, with <paramref name="others"/> after it.</summary>
    private static MoonfallGame WithGreen(MoonfallPower power, double gx = 120, double gy = 250, params MoonfallPeg[] others)
    {
        var game = new MoonfallGame(Board([Blue(gx, gy), .. others]), 1, 1, power: power);
        game.MakeGreenForTest(0);
        return game;
    }

    /// <summary>A ball in play, about to fall out of the board, touches the green; returns the events so far.</summary>
    private static List<(long Tick, MoonfallEvent Event)> HitGreenThisShot(MoonfallGame game, int green = 0)
    {
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, green);
        return Drain(game);
    }

    private static List<(long Tick, MoonfallEvent Event)> Drain(MoonfallGame game)
    {
        var seen = new List<(long, MoonfallEvent)>();
        while (game.TryReadEvent(out var e))
        {
            seen.Add((game.GameTick, e));
        }

        return seen;
    }

    private static List<(long Tick, MoonfallEvent Event)> FinishTurn(MoonfallGame game) =>
        RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost);

    /// <summary>A shot hard right, which touches no peg on these boards, played to the next ball.</summary>
    private static List<(long Tick, MoonfallEvent Event)> EmptyShot(MoonfallGame game)
    {
        Assert.True(game.Shoot(85));
        return FinishTurn(game);
    }

    /// <summary>
    /// The pegs' values as they are coloured now (a purple moves every turn), at the meter's multiplier now: these boards
    /// have three oranges or fewer, so it stays at ×10 all shot.
    /// </summary>
    private static long Values(MoonfallGame game, params int[] pegs)
    {
        Assert.Equal(10, game.Multiplier);
        return pegs.Sum(i => (long)MoonfallRules.BaseValue(game.Peg(i).Colour)) * game.Multiplier;
    }

    // ---- Who carries which power [R §6 l.126] ----

    [Theory]
    [InlineData(MoonfallCampaignKind.Base, 0, MoonfallPower.SuperGuide)]
    [InlineData(MoonfallCampaignKind.Base, 4, MoonfallPower.SuperGuide)]
    [InlineData(MoonfallCampaignKind.Base, 5, MoonfallPower.Multiball)]
    [InlineData(MoonfallCampaignKind.Base, 49, MoonfallPower.Path)]
    [InlineData(MoonfallCampaignKind.Base, 50, MoonfallPower.None)]
    [InlineData(MoonfallCampaignKind.Base, 54, MoonfallPower.None)]
    [InlineData(MoonfallCampaignKind.Expansion, 50, MoonfallPower.Bolt)]
    [InlineData(MoonfallCampaignKind.Expansion, 54, MoonfallPower.Bolt)]
    [InlineData(MoonfallCampaignKind.Expansion, 55, MoonfallPower.None)]
    [InlineData(MoonfallCampaignKind.Expansion, 59, MoonfallPower.None)]
    public void Adventure_gives_each_stage_of_five_levels_one_character_then_a_stage_of_choice(MoonfallCampaignKind campaign, int level, MoonfallPower power)
    {
        Assert.Equal(power, MoonfallCharacters.AdventurePower(campaign, level));
        Assert.Equal(power == MoonfallPower.None, MoonfallCharacters.PlayerPicks(campaign, level));
        Assert.Equal((level / 5) + 1, MoonfallCharacters.Stage(level));
        Assert.Equal((level % 5) + 1, MoonfallCharacters.LevelInStage(level));
    }

    [Fact]
    public void Every_power_has_one_stage_in_the_campaigns_the_bolt_only_in_the_expansion()
    {
        var baseOrder = MoonfallCharacters.BaseOrder.ToArray().Select(static b => (MoonfallPower)b).ToList();
        var expansion = MoonfallCharacters.ExpansionOrder.ToArray().Select(static b => (MoonfallPower)b).ToList();
        Assert.Equal(10, baseOrder.Distinct().Count());
        Assert.DoesNotContain(MoonfallPower.Bolt, baseOrder);
        Assert.Equal(11, expansion.Distinct().Count());
        Assert.Equal(55, (baseOrder.Count + 1) * MoonfallCharacters.LevelsPerStage);
        Assert.Equal(60, (expansion.Count + 1) * MoonfallCharacters.LevelsPerStage);
    }

    [Fact]
    public void A_power_out_of_range_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoonfallGame(Board(Blue(120, 250)), 1, 1, power: (MoonfallPower)12));
    }

    // ---- Trigger and timing [R §5 l.103] ----

    [Fact]
    public void A_green_triggers_the_levels_power_and_nothing_without_one()
    {
        var plain = WithGreen(MoonfallPower.None);
        var events = HitGreenThisShot(plain);
        Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.GreenHit);
        Assert.DoesNotContain(events, e => e.Event.Kind == MoonfallEventKind.PowerTriggered);

        var game = WithGreen(MoonfallPower.Burst);
        events = HitGreenThisShot(game);
        var trigger = Assert.Single(events, e => e.Event.Kind == MoonfallEventKind.PowerTriggered).Event;
        Assert.Equal((long)MoonfallPower.Burst, trigger.Value);
        Assert.Equal(0, trigger.Peg);
    }

    [Theory]
    [InlineData(MoonfallPower.SuperGuide, 3)]
    [InlineData(MoonfallPower.Fireball, 1)]
    [InlineData(MoonfallPower.Path, 1)]
    [InlineData(MoonfallPower.Bolt, 1)]
    public void Next_shot_powers_wait_for_the_next_shot_then_last_their_shots(MoonfallPower power, int shots)
    {
        var game = WithGreen(power);
        HitGreenThisShot(game);
        Assert.Equal(shots, game.PowerShotsLeft(power));
        Assert.False(game.PowerActive(power));

        // The green's own shot does not count.
        FinishTurn(game);
        Assert.Equal(shots, game.PowerShotsLeft(power));
        for (var k = shots; k > 0; k--)
        {
            Assert.True(game.Shoot(85));
            Assert.True(game.PowerActive(power));
            Assert.Equal(k, game.PowerShotsLeft(power));
            var events = FinishTurn(game);
            Assert.Equal(k - 1, game.PowerShotsLeft(power));
            Assert.Equal(k == 1, events.Any(e => e.Event.Kind == MoonfallEventKind.PowerEnded && e.Event.Value == (long)power));
        }

        Assert.True(game.Shoot(85));
        Assert.False(game.PowerActive(power));
    }

    [Theory]
    [InlineData(MoonfallPower.Wings, 5)]
    [InlineData(MoonfallPower.Flippers, 3)]
    public void At_once_powers_act_in_the_greens_shot_and_count_it(MoonfallPower power, int shots)
    {
        var game = WithGreen(power);
        HitGreenThisShot(game);
        Assert.True(game.PowerActive(power));
        Assert.Equal(shots, game.PowerShotsLeft(power));
        FinishTurn(game);
        for (var k = shots - 1; k > 0; k--)
        {
            Assert.Equal(k, game.PowerShotsLeft(power));
            EmptyShot(game);
        }

        Assert.Equal(0, game.PowerShotsLeft(power));
    }

    [Theory]
    [InlineData(MoonfallPower.SuperGuide, 6)]
    [InlineData(MoonfallPower.Wings, 10)]
    [InlineData(MoonfallPower.Fireball, 2)]
    public void A_second_green_adds_its_shots_to_those_left(MoonfallPower power, int total)
    {
        var game = new MoonfallGame(Board(Blue(120, 250), Blue(160, 250)), 1, 1, power: power);
        game.MakeGreenForTest(0);
        game.MakeGreenForTest(1);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        Assert.Equal(total, game.PowerShotsLeft(power));
    }

    // ---- Super Guide [R §1 l.41, §5 l.107] ----

    [Fact]
    public void Super_Guide_runs_the_guide_on_through_its_first_bounce()
    {
        // One peg just right of the line under the launcher: the plain guide stops on it; Super Guide's line carries on.
        var game = WithGreen(MoonfallPower.SuperGuide, 120, 250, Blue(410, 330));
        var line = new (double X, double Y)[MoonfallRules.SuperGuideMaxPoints];
        Assert.Equal(0, game.GuideBeyond(0, line));

        HitGreenThisShot(game);
        FinishTurn(game);
        Assert.True(game.GuideExtended);
        var dots = new (double X, double Y)[MoonfallRules.GuideMaxDots];
        var n = game.Guide(0, dots);
        var m = game.GuideBeyond(0, line);
        Assert.True(m >= 3, $"only {m} points past the bounce");

        // It starts where the plain guide stops, and the bounce turns it away from the peg (to the left of x = 410).
        Assert.True(MoonfallGeometry.Hypot(line[0].X - dots[n - 1].X, line[0].Y - dots[n - 1].Y) < 3);
        Assert.True(line[m - 1].X < line[0].X - 5);
        var length = 0.0;
        for (var k = 1; k < m; k++)
        {
            length += MoonfallGeometry.Hypot(line[k].X - line[k - 1].X, line[k].Y - line[k - 1].Y);
        }

        Assert.InRange(length, MoonfallRules.GuideDotSpacing, MoonfallRules.SuperGuideMaxLength + MoonfallRules.GuideDotSpacing);

        // Reading it changes nothing.
        var before = game.Fingerprint();
        game.GuideBeyond(0, line);
        Assert.Equal(before, game.Fingerprint());
    }

    // ---- Multiball [R §5 l.108] ----

    [Fact]
    public void Multiball_springs_a_twin_from_the_green_and_the_turn_waits_for_both()
    {
        // The green straight under the launcher; two blues low down, one each side.
        var game = new MoonfallGame(Board(Blue(400, 300), Blue(300, 470), Blue(500, 470)), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        Assert.True(game.Shoot(0));
        var seen = RunUntil(game, static g => g.BallsInPlay == 2 || g.Phase != MoonfallPhase.Flying);
        Assert.Equal(2, game.BallsInPlay);
        var added = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.BallAdded).Event;
        Assert.Equal(0, added.Peg);
        Assert.Equal(400, added.X, 6);
        Assert.True(added.Y < 300 - MoonfallRules.PegRadius, "the twin leaves from the top of the green");

        // The first ball came straight down, so the twin takes the least sideways speed, and goes its own way.
        for (var t = 0; t < 5; t++)
        {
            game.Tick();
        }

        Assert.True(Math.Abs(game.BallAt(1, 1).X - game.BallAt(0, 1).X) > 3);

        // The turn goes on until the last ball has left.
        var ends = 0;
        var alone = false;
        while (game.Phase == MoonfallPhase.Flying)
        {
            game.Tick();
            while (game.TryReadEvent(out var e))
            {
                ends += e.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch ? 1 : 0;
            }

            alone |= game.Phase == MoonfallPhase.Flying && game.BallsInPlay == 1;
            Assert.True(game.GameTick < 5000);
        }

        Assert.Equal(2, ends);
        Assert.True(alone, "one ball flew on alone after the other left");
    }

    [Fact]
    public void Multiball_twins_pegs_count_in_the_one_shot_score_and_clear_with_it()
    {
        // A third orange stays up, so the shot is not the Full Moon.
        var game = new MoonfallGame(Board(Blue(120, 250), Orange(700, 200), Orange(700, 250), Blue(650, 150), Orange(700, 450)), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        Assert.Equal(2, game.BallsInPlay);

        // The first ball lights an orange, the twin the other orange and a blue: one shot, four pegs.
        game.TouchForTest(0, 1);
        game.TouchForTest(1, 2);
        game.TouchForTest(1, 3);
        Assert.Equal(4, game.ShotPegs);
        var value = game.ShotValue;
        Assert.Equal(Values(game, 0, 1, 2, 3), value);

        var events = FinishTurn(game);
        var scored = Assert.Single(events, e => e.Event.Kind == MoonfallEventKind.ShotScored).Event;
        Assert.Equal((value * 4) + game.StyleBonus, scored.Value);
        Assert.Equal(scored.Value, game.Score);
        Assert.Equal(4, events.Count(e => e.Event.Kind == MoonfallEventKind.PegCleared));
        Assert.All(Enumerable.Range(0, 4), i => Assert.True(game.Peg(i).Cleared));
    }

    [Fact]
    public void A_ball_caught_is_a_free_ball_and_the_turn_goes_on_while_its_twin_flies()
    {
        var game = new MoonfallGame(Board(Blue(120, 250)), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        var balls = game.BallsLeft;
        game.PlaceBall(MoonfallBucket.CentreAt(game.GameTick + 1) - 20, MoonfallRules.CatchLine - 1, 0, 150);
        game.TouchForTest(0, 0);
        Assert.Equal(2, game.BallsInPlay);
        var seen = RunUntil(game, static g => g.BallsInPlay < 2);
        Assert.Contains(seen, e => e.Event.Kind == MoonfallEventKind.BucketCatch);
        Assert.Equal(MoonfallPhase.Flying, game.Phase);
        Assert.Equal(1, game.BallsInPlay);
        Assert.Equal(balls + 1, game.BallsLeft);

        var events = FinishTurn(game);
        Assert.Equal(balls + 1 + events.Count(e => e.Event.Kind == MoonfallEventKind.BucketCatch), game.BallsLeft);
    }

    [Fact]
    public void No_more_than_three_balls_fly_at_once()
    {
        var game = new MoonfallGame(Board(Blue(120, 250), Blue(160, 250), Blue(200, 250)), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        game.MakeGreenForTest(1);
        game.MakeGreenForTest(2);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        game.TouchForTest(0, 2);
        Assert.Equal(MoonfallRules.MaxBalls, game.BallsInPlay);
    }

    // ---- Brass Wings [R §5 l.109] ----

    private static List<(long, MoonfallEvent)> DropWide(MoonfallGame game)
    {
        // Just over the catch line, 80 px from the bucket's centre: past a plain rim (52–65.5), inside the winged mouth (104).
        game.PlaceBall(MoonfallBucket.CentreAt(game.GameTick + 1) + 80, MoonfallRules.CatchLine - 1, 0, 150);
        return RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
    }

    [Fact]
    public void Brass_Wings_widen_the_bucket_so_a_ball_wide_of_the_mouth_is_caught()
    {
        var plain = WithGreen(MoonfallPower.Wings);
        Assert.Equal(MoonfallRules.BucketMouth, plain.BucketMouth);
        Assert.DoesNotContain(DropWide(plain), e => e.Item2.Kind == MoonfallEventKind.BucketCatch);

        var game = WithGreen(MoonfallPower.Wings);
        HitGreenThisShot(game);
        Assert.True(game.WingsOpen);
        Assert.Equal(MoonfallRules.WingsMouth, game.BucketMouth);
        FinishTurn(game);
        Assert.Contains(DropWide(game), e => e.Item2.Kind == MoonfallEventKind.BucketCatch);
    }

    // ---- Lunar Burst [R §5 l.110] ----

    [Fact]
    public void Lunar_Burst_lights_every_peg_within_its_radius_nearest_first()
    {
        // Surfaces 60, 30 and 79 px from the green's centre are in; 81 and 120 are out.
        var game = WithGreen(
            MoonfallPower.Burst,
            400,
            300,
            Blue(400 + 70, 300),
            Blue(400, 300 + 40),
            Blue(400 - 89, 300),
            Blue(400, 300 - 91),
            Blue(400 + 130, 300));
        var events = HitGreenThisShot(game);
        var lit = events.Where(e => e.Event.Kind == MoonfallEventKind.PegHit).Select(e => e.Event.Peg).ToList();
        Assert.Equal([0, 2, 1, 3], lit);
        Assert.False(game.Peg(4).Lit);
        Assert.False(game.Peg(5).Lit);
        Assert.Equal(4, game.ShotPegs);
        Assert.Equal(1, game.PowerShotsLeft(MoonfallPower.Burst));
        FinishTurn(game);
        Assert.Equal(0, game.PowerShotsLeft(MoonfallPower.Burst));
    }

    [Fact]
    public void A_burst_that_lights_the_other_green_bursts_again_after_it()
    {
        // Peg 3 is beyond the first burst's reach and within the second's.
        var game = new MoonfallGame(Board(Blue(300, 300), Blue(330, 300), Blue(370, 300), Blue(440, 300)), 1, 1, power: MoonfallPower.Burst);
        game.MakeGreenForTest(0);
        game.MakeGreenForTest(2);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        var events = Drain(game);
        Assert.Equal([0, 2], events.Where(e => e.Event.Kind == MoonfallEventKind.PowerTriggered).Select(e => e.Event.Peg));
        Assert.All(Enumerable.Range(0, 4), i => Assert.True(game.Peg(i).Lit));
    }

    // ---- Flippers [R §5 l.111] ----

    /// <summary>
    /// A ball dropped onto the left flipper's middle, the flipper raised (or not) as it arrives; returns the fastest the
    /// ball went upwards while over the flipper.
    /// </summary>
    private static double Kick(MoonfallGame game, bool hold)
    {
        game.PlaceBall(MoonfallRules.FlipperPivotX + 45, 470, 0, 120);
        var best = 0.0;
        for (var t = 0; t < 80 && game.Phase == MoonfallPhase.Flying && game.BallX < 175 && game.BallY < 568; t++)
        {
            game.SetFlippers(hold && game.BallY > 505);
            game.Tick();
            best = Math.Min(best, game.BallVelocityY);
        }

        game.SetFlippers(false);
        return best;
    }

    private static MoonfallGame FlippersOut()
    {
        var game = WithGreen(MoonfallPower.Flippers);
        HitGreenThisShot(game);
        Assert.True(game.FlippersOut);
        FinishTurn(game);
        return game;
    }

    [Fact]
    public void Without_the_power_there_are_no_flippers_to_meet()
    {
        var game = WithGreen(MoonfallPower.Flippers);
        Assert.False(game.FlippersOut);
        game.PlaceBall(MoonfallRules.FlipperPivotX + 45, 470, 0, 120);
        game.SetFlippers(true);
        while (game.Phase == MoonfallPhase.Flying && game.BallY < 562)
        {
            game.Tick();
            Assert.True(game.BallVelocityY > 0);
        }
    }

    [Fact]
    public void Flippers_rise_while_held_and_bat_a_falling_ball_back_up()
    {
        var game = FlippersOut();
        var rest = game.Flipper(false, 1);
        Assert.True(rest.TipY > rest.PivotY, "at rest a flipper slopes down to the middle");
        Assert.True(game.Shoot(85));
        game.SetFlippers(true);
        for (var t = 0; t < 12; t++)
        {
            game.Tick();
        }

        var up = game.Flipper(false, 1);
        Assert.True(up.TipY < up.PivotY, "held, it points above level");
        var mirror = game.Flipper(true, 1);
        Assert.Equal(MoonfallRules.Width - up.TipX, mirror.TipX, 6);
        Assert.Equal(up.TipY, mirror.TipY, 6);
        game.SetFlippers(false);
        FinishTurn(game);

        var batted = Kick(game, true);
        Assert.True(batted < -300, $"batted only to {batted:0} px/s");
    }

    [Fact]
    public void A_resting_flipper_only_lets_the_ball_roll_towards_the_middle()
    {
        var game = FlippersOut();
        var rolled = Kick(game, false);
        Assert.True(rolled > -150, $"a resting flipper threw it up at {rolled:0} px/s");
        Assert.True(game.BallVelocityX > 0, "it rolls off towards the middle");
    }

    // ---- Moon Gate [R §5 l.112] ----

    [Fact]
    public void Moon_Gate_brings_a_falling_ball_back_in_at_the_top_once()
    {
        // Below the catch line, between the bucket's rims: nothing to touch on the way out.
        var game = WithGreen(MoonfallPower.Gate);
        game.PlaceBall(MoonfallBucket.CentreAt(game.GameTick + 1) + 30, 590, 0, 200);
        game.TouchForTest(0, 0);
        Assert.Equal(1, game.GateLeft);
        var seen = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying || g.GateLeft == 0);
        var back = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.BallReentered).Event;
        Assert.Equal(MoonfallRules.GateReentryY, back.Y, 6);
        Assert.Equal(MoonfallPhase.Flying, game.Phase);
        Assert.True(game.BallY < 50);
        Assert.True(game.BallVelocityY > 200, "it keeps the speed it fell out with");

        // The second time down is the end of the turn (out of the board, or into the bucket).
        seen = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
        Assert.Contains(seen, e => e.Event.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch);
        Assert.DoesNotContain(seen, e => e.Event.Kind == MoonfallEventKind.BallReentered);
    }

    [Fact]
    public void Moon_Gate_lasts_its_shot_only_and_each_green_adds_a_reentry()
    {
        var game = new MoonfallGame(Board(Blue(120, 250), Blue(160, 250)), 1, 1, power: MoonfallPower.Gate);
        game.MakeGreenForTest(0);
        game.MakeGreenForTest(1);
        game.PlaceBall(MoonfallBucket.CentreAt(game.GameTick + 1) + 30, 590, 0, 200);
        game.TouchForTest(0, 0);
        game.TouchForTest(0, 1);
        Assert.Equal(2, game.GateLeft);
        var seen = FinishTurn(game);

        // Each fall out of the board uses one, until none are left; a catch ends the turn first.
        var back = seen.Count(e => e.Event.Kind == MoonfallEventKind.BallReentered);
        var lost = seen.Count(e => e.Event.Kind == MoonfallEventKind.BallLost);
        Assert.Equal(lost == 1 ? 2 : back, back);
        Assert.InRange(back, 1, 2);
        Assert.Equal(0, game.GateLeft);
        Assert.Equal(0, game.PowerShotsLeft(MoonfallPower.Gate));
        EmptyShot(game);
        Assert.DoesNotContain(EmptyShot(game), e => e.Event.Kind == MoonfallEventKind.BallReentered);
    }

    // ---- Moonbloom [R §5 l.113] ----

    [Theory]
    [InlineData(7, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(25, 5)]
    [InlineData(1, 1)]
    public void Moonbloom_lights_the_nearest_fifth_rounded_up_of_the_oranges_left(int oranges, int lit)
    {
        var pegs = new List<MoonfallPeg> { Blue(400, 300) };
        for (var k = 0; k < oranges; k++)
        {
            // A spiral round the green at growing distances, so "nearest" is plain.
            var angle = k * 2.4;
            var distance = 40 + (k * 9);
            pegs.Add(Orange(400 + (distance * Math.Cos(angle)), 300 + (distance * Math.Sin(angle))));
        }

        var game = new MoonfallGame(Board([.. pegs]), 1, 1, power: MoonfallPower.Bloom);
        game.MakeGreenForTest(0);
        Assert.Equal(oranges, game.OrangesLeft);
        var events = HitGreenThisShot(game);
        var hit = events.Where(e => e.Event.Kind == MoonfallEventKind.PegHit && e.Event.Peg != 0).Select(e => e.Event.Peg).ToList();
        Assert.Equal(Enumerable.Range(1, lit), hit);
        Assert.Equal(oranges - lit, game.OrangesLeft);
    }

    // ---- Moon-Viewing Draw [R §5 l.114] ----

    [Fact]
    public void The_drum_draws_each_outcome_a_third_of_the_time_and_every_other_power_evenly()
    {
        var random = new MoonfallRandom(2026);
        var outcomes = new int[3];
        var powers = new int[MoonfallPowers.Count + 1];
        const int Draws = 30_000;
        for (var k = 0; k < Draws; k++)
        {
            var outcome = MoonfallPowers.Draw(ref random, out var power);
            outcomes[(int)outcome]++;
            if (outcome == MoonfallDrawOutcome.AnotherPower)
            {
                Assert.NotEqual(MoonfallPower.None, power);
                powers[(int)power]++;
            }
            else
            {
                Assert.Equal(MoonfallPower.None, power);
            }
        }

        // Each third within 2 points (about seven standard deviations at 30,000 draws).
        Assert.All(outcomes, n => Assert.InRange(n / (double)Draws, (1 / 3.0) - 0.02, (1 / 3.0) + 0.02));
        Assert.Equal(0, powers[(int)MoonfallPower.Draw]);
        Assert.Equal(0, powers[0]);
        var others = outcomes[(int)MoonfallDrawOutcome.AnotherPower];
        for (var p = 1; p <= MoonfallPowers.Count; p++)
        {
            if (p != (int)MoonfallPower.Draw)
            {
                Assert.InRange(powers[p] / (double)others, 0.1 - 0.02, 0.1 + 0.02);
            }
        }

        // The same seed draws the same.
        var a = new MoonfallRandom(7);
        var b = new MoonfallRandom(7);
        for (var k = 0; k < 100; k++)
        {
            Assert.Equal(MoonfallPowers.Draw(ref a, out var pa), MoonfallPowers.Draw(ref b, out var pb));
            Assert.Equal(pa, pb);
        }
    }

    [Fact]
    public void Each_draw_does_what_it_says_a_triple_score_lasting_this_shot_and_the_next()
    {
        var seen = new HashSet<MoonfallDrawOutcome>();
        for (ulong seed = 1; seen.Count < 3 && seed < 200; seed++)
        {
            var game = new MoonfallGame(Board(Blue(120, 250), Blue(160, 250)), 1, seed, power: MoonfallPower.Draw);
            game.MakeGreenForTest(0);
            game.PlaceBall(600, 598, 0, 300);
            var balls = game.BallsLeft;
            game.TouchForTest(0, 1);
            game.TouchForTest(0, 0);
            var events = Drain(game);
            var drawn = Assert.Single(events, e => e.Event.Kind == MoonfallEventKind.Drawn).Event;
            var outcome = (MoonfallDrawOutcome)drawn.Value;
            seen.Add(outcome);
            switch (outcome)
            {
                case MoonfallDrawOutcome.FreeBall:
                    Assert.Equal(balls + 1, game.BallsLeft);
                    Assert.False(game.TripleScore);
                    break;

                case MoonfallDrawOutcome.TripleScore:
                    Assert.True(game.TripleScore);
                    Assert.Equal(Values(game, 0, 1) * 2 * 3, game.ShotScore);
                    var tripled = game.ShotScore;
                    FinishTurn(game);
                    Assert.Equal(tripled, game.Score);
                    Assert.True(game.Shoot(85));
                    Assert.True(game.TripleScore);
                    FinishTurn(game);
                    Assert.True(game.Shoot(85));
                    Assert.False(game.TripleScore);
                    break;

                default:
                    var other = (MoonfallPower)drawn.Count;
                    Assert.NotEqual(MoonfallPower.Draw, other);
                    Assert.Contains(events, e => e.Event.Kind == MoonfallEventKind.PowerTriggered && e.Event.Value == (long)other);
                    Assert.True(game.PowerShotsLeft(other) > 0);
                    break;
            }
        }

        Assert.Equal(3, seen.Count);
    }

    // ---- Fireball [R §5 l.115] ----

    [Fact]
    public void Fireball_burns_through_the_pegs_it_meets_without_bouncing()
    {
        // A column straight under the launcher.
        MoonfallPeg[] column = [Blue(400, 260), Blue(400, 320), Blue(400, 380), Blue(400, 440)];
        var plain = new MoonfallGame(Board([Blue(120, 250), .. column]), 1, 1, power: MoonfallPower.Fireball);
        plain.Shoot(0);
        RunUntil(plain, static g => g.ShotPegs > 0);
        RunUntil(plain, static g => g.Phase != MoonfallPhase.Flying || g.BallVelocityY < 0, 40);
        Assert.True(plain.BallVelocityY < 0, "without the Fireball the ball bounces off the first peg");

        var game = new MoonfallGame(Board([Blue(120, 250), .. column]), 1, 1, power: MoonfallPower.Fireball);
        game.MakeGreenForTest(0);
        HitGreenThisShot(game);
        FinishTurn(game);
        Assert.True(game.Shoot(0));
        Assert.True(game.PowerActive(MoonfallPower.Fireball));
        var value = Values(game, 1, 2, 3, 4);
        var seen = new List<(long, MoonfallEvent)>();
        while (game.Phase == MoonfallPhase.Flying && game.BallY < 520)
        {
            game.Tick();
            while (game.TryReadEvent(out var e))
            {
                seen.Add((game.GameTick, e));
            }

            Assert.Equal(0, game.BallVelocityX, 9);
            Assert.True(game.BallVelocityY > 0, "it never bounces");
        }

        // Each peg lit once and burnt away as it was met; the shot scores them all.
        Assert.Equal(4, seen.Count(e => e.Item2.Kind == MoonfallEventKind.PegHit));
        Assert.Equal(4, seen.Count(e => e.Item2.Kind == MoonfallEventKind.PegCleared));
        Assert.All(Enumerable.Range(1, 4), i => Assert.True(game.Peg(i).Cleared));
        Assert.Equal(value, game.ShotValue);
        Assert.Equal(4, game.ShotPegs);
        FinishTurn(game);
        Assert.False(game.PowerActive(MoonfallPower.Fireball));
        Assert.Equal(0, game.PowerShotsLeft(MoonfallPower.Fireball));
    }

    // ---- Sage's Path [R §5 l.116] ----

    [Fact]
    public void Sages_Path_nudges_the_shot_onto_the_orange_a_straight_shot_misses()
    {
        // An orange 25 px right of the straight-down line: a straight shot misses it, a nudge within 4° meets it.
        var plain = new MoonfallGame(Board(Blue(120, 250), Orange(425, 400)), 1, 1);
        plain.Shoot(0);
        RunUntil(plain, static g => g.Phase != MoonfallPhase.Flying);
        Assert.Equal(1, plain.OrangesLeft);

        var game = new MoonfallGame(Board(Blue(120, 250), Orange(425, 400)), 1, 1, power: MoonfallPower.Path);
        game.MakeGreenForTest(0);
        HitGreenThisShot(game);
        FinishTurn(game);
        Assert.True(game.Shoot(0));

        // The ball waits in the barrel, at the aim, while the lines are weighed: one a game tick.
        var shotAt = game.GameTick;
        var (barrelX, barrelY) = (game.BallX, game.BallY);
        Assert.True(game.ChoosingPath);
        var seen = new List<(long Tick, MoonfallEvent Event)>();
        while (game.ChoosingPath)
        {
            Assert.Equal(1, game.BallsInPlay);
            Assert.Equal((barrelX, barrelY), (game.BallX, game.BallY));
            Assert.True(game.GameTick - shotAt < MoonfallGame.PathCandidates);
            game.Tick();
            seen.AddRange(Drain(game));
        }

        Assert.Equal(MoonfallGame.PathCandidates, game.GameTick - shotAt);
        Assert.Equal(MoonfallPhase.Flying, game.Phase);
        Assert.DoesNotContain(seen, e => e.Event.Kind == MoonfallEventKind.PegHit);
        var chosen = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.PathChosen).Event;
        Assert.Equal(MoonfallGame.PathCandidates, chosen.Count);
        Assert.InRange(game.PathNudge, 0.5, MoonfallRules.PathSpreadDegrees);
        Assert.Equal((long)Math.Round(game.PathNudge * 100), chosen.Value);
        RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
        Assert.Equal(0, game.OrangesLeft);
    }

    [Fact]
    public void Sages_Path_chooses_the_same_line_every_time_within_its_cost_cap()
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[3];
        double? first = null;
        for (var run = 0; run < 3; run++)
        {
            var game = new MoonfallGame(level, 4, 11, power: MoonfallPower.Path);
            var green = Enumerable.Range(0, game.PegCount).First(i => game.Peg(i).Colour == PegColour.Green);
            game.PlaceBall(600, 598, 0, 300);
            game.LightForTest(green);
            FinishTurn(game);
            Assert.True(game.Shoot(-20));
            RunUntil(game, static g => !g.ChoosingPath);
            Assert.InRange(game.LastPathSubSteps, 1, MoonfallRules.PathSubStepBudget);
            Assert.InRange(game.LastPathTickSubSteps, 1, MoonfallGame.PathSubStepsPerLine);
            first ??= game.PathNudge;
            Assert.Equal(first.Value, game.PathNudge);
        }

        Assert.InRange(first!.Value, -MoonfallRules.PathSpreadDegrees, MoonfallRules.PathSpreadDegrees);
    }

    // ---- Storm Post [R §5 l.117] ----

    [Fact]
    public void Storm_Post_bolts_from_the_first_peg_hit_to_the_bucket_lighting_the_pegs_on_its_line()
    {
        // Where the bucket will be when the bolt's shot begins: the green's turn played on a board with nothing else.
        var probe = WithGreen(MoonfallPower.Bolt);
        HitGreenThisShot(probe);
        FinishTurn(probe);
        var bucket = probe.BucketX;

        // The first peg is up and to the side; the bolt's line runs down to the bucket's centre through three pegs, and
        // two more lie well off it.
        var start = (X: bucket > 400 ? 200.0 : 600.0, Y: 200.0);
        var end = (X: bucket, Y: MoonfallRules.BucketTop);
        (double X, double Y) On(double t, double off) =>
            (start.X + ((end.X - start.X) * t) + off, start.Y + ((end.Y - start.Y) * t));
        var a = On(0.25, 10);
        var b = On(0.5, -20);
        var c = On(0.8, 0);
        var far1 = On(0.4, 60);
        var far2 = On(0.6, -45);
        var game = new MoonfallGame(
            Board(Blue(120, 250), Blue(start.X, start.Y), Blue(c.X, c.Y), Blue(a.X, a.Y), Blue(far1.X, far1.Y), Blue(b.X, b.Y), Blue(far2.X, far2.Y)),
            1,
            1,
            power: MoonfallPower.Bolt);
        game.MakeGreenForTest(0);
        HitGreenThisShot(game);
        FinishTurn(game);
        Assert.Equal(bucket, game.BucketX);
        Assert.Equal(1, game.PowerShotsLeft(MoonfallPower.Bolt));

        // The bolt acts in this shot; it leaves from the first peg the ball lights.
        game.PlaceBall(400, 100, 0, 0);
        Assert.True(game.PowerActive(MoonfallPower.Bolt));
        game.TouchForTest(0, 1);
        var events = Drain(game);
        var bolt = Assert.Single(events, e => e.Event.Kind == MoonfallEventKind.BoltStruck).Event;
        Assert.Equal(1, bolt.Peg);
        Assert.Equal(3, bolt.Value);
        Assert.Equal([1, 3, 5, 2], events.Where(e => e.Event.Kind == MoonfallEventKind.PegHit).Select(e => e.Event.Peg));
        Assert.False(game.Peg(4).Lit);
        Assert.False(game.Peg(6).Lit);
        var path = game.BoltPoints.ToArray();
        Assert.Equal(5, path.Length);
        Assert.Equal(start, path[0]);
        Assert.Equal((bucket, MoonfallRules.BucketTop), path[^1]);

        // Once a shot: the next peg the ball lights sends no bolt.
        game.TouchForTest(0, 4);
        Assert.DoesNotContain(Drain(game), e => e.Event.Kind == MoonfallEventKind.BoltStruck);
    }
}
