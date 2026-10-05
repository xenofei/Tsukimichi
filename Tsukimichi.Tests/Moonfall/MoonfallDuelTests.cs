using System.Collections;
using System.Reflection;
using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>The duel (plan v9 G7) [R §6 l.127–128, §2 l.47, §3 l.92], its opponent, and the engine's copy that both the opponent's tools and the playability gate lean on.</summary>
public sealed class MoonfallDuelTests
{
    private static readonly double[] Angles = [-35, 12, 48, -60, 3, 27, -14, 66, -48, 20];

    private static MoonfallLevel Shipped(int index) => MoonfallCampaigns.LoadBuiltIn().Base.Levels[index];

    /// <summary>Plays the duel to its end: the player shoots <see cref="Angles"/> in turn, the opponent its own; checks the sides' balls every turn.</summary>
    private static List<MoonfallDuelTurn> PlayDuel(MoonfallDuel duel, int maxTicks = 400_000)
    {
        var turns = new List<MoonfallDuelTurn>();
        MoonfallDuelTurn? last = null;
        var shot = 0;
        for (var t = 0; t < maxTicks && duel.Outcome == MoonfallDuelOutcome.Undecided; t++)
        {
            if (duel.PlayersTurn)
            {
                Assert.True(duel.Shoot(Angles[shot++ % Angles.Length]));
            }

            duel.Tick();
            while (duel.Game.TryReadEvent(out _))
            {
            }

            if (duel.LastTurn != last)
            {
                last = duel.LastTurn;
                turns.Add(last!.Value);
                if (duel.Outcome == MoonfallDuelOutcome.Undecided)
                {
                    Assert.Equal(duel.Game.BallsLeft, duel.BallsLeft(0) + duel.BallsLeft(1));
                }
            }
        }

        return turns;
    }

    [Fact]
    public void The_sides_take_turns_the_player_first_and_each_keeps_its_own_score_and_balls()
    {
        var duel = new MoonfallDuel(Shipped(3), 5, 42, MoonfallCompanion.Minfilia, MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept);
        Assert.True(duel.PlayersTurn);
        Assert.Equal(MoonfallRules.DuelBallsPerSide, duel.BallsLeft(MoonfallDuel.PlayerSide));
        Assert.Equal(MoonfallRules.DuelBallsPerSide, duel.BallsLeft(MoonfallDuel.OpponentSide));
        Assert.Equal(MoonfallPower.SuperGuide, duel.Game.Power);

        var turns = PlayDuel(duel);
        Assert.NotEqual(MoonfallDuelOutcome.Undecided, duel.Outcome);
        Assert.Equal(MoonfallDuel.PlayerSide, turns[0].Side);

        // While both have balls they alternate; the scores are the turns' kept scores; the board's score is every turn's.
        for (var k = 1; k < turns.Count && k < 2 * MoonfallRules.DuelBallsPerSide - 2; k++)
        {
            Assert.NotEqual(turns[k - 1].Side, turns[k].Side);
        }

        Assert.Equal(turns.Where(static t => t.Side == 0).Sum(static t => t.Kept), duel.Score(0));
        Assert.Equal(turns.Where(static t => t.Side == 1).Sum(static t => t.Kept), duel.Score(1));
        Assert.Equal(turns.Sum(static t => t.Scored), duel.Game.Score);
        var expected = duel.Score(0) > duel.Score(1) ? MoonfallDuelOutcome.Won : duel.Score(0) < duel.Score(1) ? MoonfallDuelOutcome.Lost : MoonfallDuelOutcome.Drawn;
        Assert.Equal(expected, duel.Outcome);
        if (duel.Game.Tally is { } tally)
        {
            Assert.Equal(0, tally.BallBonus);
        }
    }

    [Fact]
    public void The_same_seed_and_shots_give_the_same_duel()
    {
        var a = new MoonfallDuel(Shipped(2), 4, 9, MoonfallCompanion.Cid, MoonfallCompanion.Yshtola, MoonfallAiDifficulty.Novice);
        var b = new MoonfallDuel(Shipped(2), 4, 9, MoonfallCompanion.Cid, MoonfallCompanion.Yshtola, MoonfallAiDifficulty.Novice);
        var shot = 0;
        for (var t = 0; t < 400_000 && a.Outcome == MoonfallDuelOutcome.Undecided; t++)
        {
            Assert.Equal(a.PlayersTurn, b.PlayersTurn);
            if (a.PlayersTurn)
            {
                var angle = Angles[shot++ % Angles.Length];
                Assert.True(a.Shoot(angle));
                Assert.True(b.Shoot(angle));
            }

            a.Tick();
            b.Tick();
            Assert.Equal(a.Game.Fingerprint(), b.Game.Fingerprint());
        }

        Assert.NotEqual(MoonfallDuelOutcome.Undecided, a.Outcome);
        Assert.Equal(a.Outcome, b.Outcome);
        Assert.Equal(a.Score(1), b.Score(1));
    }

    [Fact]
    public void A_turn_that_lights_no_orange_keeps_three_quarters_of_its_score()
    {
        // One blue under the launcher (never green), three oranges high on the right: a shot straight down lights only the blue.
        var level = Board(
            MoonfallPeg.Round(404, 300, canBeOrange: false, canBeGreen: false),
            MoonfallPeg.Round(395, 420, canBeOrange: false, canBeGreen: false),
            Orange(700, 180),
            Orange(700, 230),
            Orange(700, 280));
        var duel = new MoonfallDuel(level, 3, 1, MoonfallCompanion.None, MoonfallCompanion.Raubahn, MoonfallAiDifficulty.Novice);
        Assert.True(duel.Shoot(0));
        for (var t = 0; t < 20_000 && duel.LastTurn is null; t++)
        {
            duel.Tick();
        }

        var turn = duel.LastTurn!.Value;
        Assert.Equal(MoonfallDuel.PlayerSide, turn.Side);
        Assert.Equal(0, turn.Oranges);
        Assert.True(turn.Scored > 0);
        Assert.Equal(turn.Scored * MoonfallRules.DuelNoOrangeKeepPercent / 100, turn.Kept);
        Assert.Equal(turn.Kept, duel.Score(MoonfallDuel.PlayerSide));
        Assert.Equal(MoonfallDuel.OpponentSide, duel.Turn);
    }

    [Fact]
    public void A_duel_shows_one_green_at_a_time_and_the_next_comes_the_turn_after()
    {
        var level = Shipped(3);
        var game = new MoonfallGame(level, 5, 21, balls: 40, ruleSet: MoonfallRuleSet.Duel);
        var dots = new (double X, double Y)[MoonfallRules.GuideMaxDots];
        List<int> Standing() => Enumerable.Range(0, game.PegCount).Where(i => game.Peg(i).Colour == PegColour.Green && !game.Peg(i).Lit && !game.Peg(i).Cleared).ToList();

        // The angle whose guide ends on peg `index` (the first thing the ball would touch), or null.
        double? AimAt(int index)
        {
            var peg = game.Peg(index);
            for (var angle = -84.0; angle <= 84.0; angle += 0.1)
            {
                var n = game.Guide(angle, dots);
                var (x, y) = dots[n - 1];
                if (Math.Sqrt(((x - peg.X) * (x - peg.X)) + ((y - peg.Y) * (y - peg.Y))) <= peg.Radius + MoonfallRules.BallRadius + 1)
                {
                    return angle;
                }
            }

            return null;
        }

        var everGreen = new HashSet<int>();
        Assert.Single(Standing());
        for (var shot = 0; shot < 40 && game.Phase == MoonfallPhase.Aiming; shot++)
        {
            var standing = Standing();
            Assert.InRange(standing.Count, 0, 1);
            everGreen.UnionWith(Enumerable.Range(0, game.PegCount).Where(i => game.Peg(i).Colour == PegColour.Green));
            var aim = standing.Count == 1 ? AimAt(standing[0]) : null;
            Assert.True(game.Shoot(aim ?? Angles[shot % Angles.Length]));
            RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost);
        }

        Assert.Equal(MoonfallRules.GreenCount, everGreen.Count);
    }

    [Fact]
    public void Each_side_keeps_its_own_powers_between_turns()
    {
        var level = Shipped(3);
        var game = new MoonfallGame(level, 5, 3, power: MoonfallPower.SuperGuide, ruleSet: MoonfallRuleSet.Duel);
        var peg = Enumerable.Range(0, game.PegCount).First(i => game.Peg(i).Colour == PegColour.Blue);
        game.TriggerForTest(MoonfallPower.SuperGuide, peg);
        Assert.Equal(MoonfallRules.SuperGuideShots, game.PowerShotsLeft(MoonfallPower.SuperGuide));

        Assert.True(game.HandOver(1, MoonfallPower.Fireball));
        Assert.Equal(1, game.Side);
        Assert.Equal(MoonfallPower.Fireball, game.Power);
        Assert.Equal(0, game.PowerShotsLeft(MoonfallPower.SuperGuide));
        Assert.False(game.GuideExtended);

        Assert.True(game.HandOver(0, MoonfallPower.SuperGuide));
        Assert.Equal(MoonfallRules.SuperGuideShots, game.PowerShotsLeft(MoonfallPower.SuperGuide));

        // Only a duel hands over, and only between shots.
        Assert.False(new MoonfallGame(level, 5, 3).HandOver(1, MoonfallPower.None));
        Assert.True(game.Shoot(10));
        Assert.False(game.HandOver(1, MoonfallPower.Fireball));
    }

    [Fact]
    public void The_duel_pays_its_own_smaller_values()
    {
        Assert.Equal(25_000, MoonfallGame.FeverBucketValue(2, perfect: false, duel: true));
        Assert.Equal(100_000, MoonfallGame.FeverBucketValue(2, perfect: false, duel: false));
        Assert.Equal(MoonfallRules.DuelPerfectFeverBucketValue, MoonfallGame.FeverBucketValue(0, perfect: true, duel: true));
        Assert.Equal(5_000, MoonfallGame.DuelStyleShotBonus(MoonfallStyleShot.LongShot));
        Assert.Equal(2_500, MoonfallGame.DuelStyleShotBonus(MoonfallStyleShot.OnePegCatch));
        foreach (var kind in Enum.GetValues<MoonfallStyleShot>())
        {
            Assert.InRange(MoonfallGame.DuelStyleShotBonus(kind), 1, MoonfallGame.StyleShotBonus(kind) / 2);
        }
    }

    [Fact]
    public void A_duel_allocates_nothing_per_tick()
    {
        void Play(MoonfallDuel duel, int frames)
        {
            for (var frame = 0; frame < frames && duel.Outcome == MoonfallDuelOutcome.Undecided; frame++)
            {
                if (duel.PlayersTurn)
                {
                    duel.Shoot(Angles[frame % Angles.Length]);
                }

                duel.Advance(1.0 / 60);
                while (duel.Game.TryReadEvent(out _))
                {
                }
            }
        }

        var level = Shipped(3);
        Play(new MoonfallDuel(level, 5, 8, MoonfallCompanion.Tataru, MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Master), 6000);
        var measured = new MoonfallDuel(level, 5, 8, MoonfallCompanion.Tataru, MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Master);
        Play(measured, 60);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Play(measured, 3000);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    // ---- The opponent ----

    [Fact]
    public void The_opponent_weighs_a_few_angles_a_tick_within_its_budget_and_shoots_on_the_tick_it_chose()
    {
        foreach (var difficulty in Enum.GetValues<MoonfallAiDifficulty>())
        {
            var profile = MoonfallAi.ProfileOf(difficulty);
            Assert.True(profile.AnglesPerTick * profile.SubStepsPerShot <= 12_000, $"{difficulty} may take too long a tick");
            var game = new MoonfallGame(Shipped(3), 5, 5);
            var ai = new MoonfallAi(difficulty, 77);
            ai.Begin(game);
            var launch = ai.LaunchTick;
            var worst = 0;
            double angle;
            while (!ai.Step(game, out angle))
            {
                worst = Math.Max(worst, ai.LastTickSubSteps);
                game.Tick();
                Assert.True(game.GameTick <= launch, "the opponent shoots on its launch tick");
            }

            Assert.Equal(launch, game.GameTick);
            Assert.InRange(angle, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees);
            Assert.InRange(worst, 1, profile.AnglesPerTick * profile.SubStepsPerShot);
            Assert.InRange(ai.LastTurnSubSteps, 1, profile.SubStepsPerTurn);
        }
    }

    [Fact]
    public void The_opponent_is_deterministic_and_weighing_changes_nothing_on_the_board()
    {
        var level = Shipped(1);
        var a = new MoonfallGame(level, 5, 13);
        var b = new MoonfallGame(level, 5, 13);
        var before = a.Fingerprint();
        _ = a.WeighShot(10, 4_000, 30);
        Assert.Equal(before, a.Fingerprint());

        double Decide(MoonfallGame game)
        {
            var ai = new MoonfallAi(MoonfallAiDifficulty.Master, 5);
            ai.Begin(game);
            double angle;
            while (!ai.Step(game, out angle))
            {
                game.Tick();
            }

            return angle;
        }

        Assert.Equal(Decide(a), Decide(b));
    }

    [Fact]
    public void A_better_opponent_scores_more_against_the_same_player()
    {
        // Over a few boards and seeds the master outscores the novice: the difficulties mean something.
        long Total(MoonfallAiDifficulty difficulty)
        {
            long sum = 0;
            for (var seed = 1UL; seed <= 3; seed++)
            {
                foreach (var index in new[] { 0, 2, 3 })
                {
                    var duel = new MoonfallDuel(Shipped(index), 5, seed, MoonfallCompanion.None, MoonfallCompanion.Raubahn, difficulty);
                    PlayDuel(duel);
                    sum += duel.Score(MoonfallDuel.OpponentSide);
                }
            }

            return sum;
        }

        Assert.True(Total(MoonfallAiDifficulty.Master) > Total(MoonfallAiDifficulty.Novice));
    }

    // ---- The copy ----

    [Fact]
    public void A_copy_plays_on_exactly_as_its_source()
    {
        var level = Shipped(3);
        var source = new MoonfallGame(level, 5, 31, power: MoonfallPower.Multiball);
        MoonfallTestKit.PlayOut(source, Angles, maxShots: 3);
        Assert.True(source.Shoot(22));
        for (var t = 0; t < 90; t++)
        {
            source.Tick();
        }

        var copy = new MoonfallGame(level, 5, 999, balls: 3, power: MoonfallPower.Multiball);
        copy.CopyFrom(source);
        var before = GC.GetAllocatedBytesForCurrentThread();
        copy.CopyFrom(source);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(source.Fingerprint(), copy.Fingerprint());
        for (var t = 0; t < 6000; t++)
        {
            if (source.Phase == MoonfallPhase.Aiming)
            {
                Assert.True(source.Shoot(Angles[t % Angles.Length]));
                Assert.True(copy.Shoot(Angles[t % Angles.Length]));
            }

            source.Tick();
            copy.Tick();
            Assert.Equal(source.Fingerprint(), copy.Fingerprint());
        }

        Assert.Throws<ArgumentException>(() => new MoonfallGame(Shipped(0), 5, 1).CopyFrom(source));
        Assert.Throws<ArgumentException>(() => new MoonfallGame(level, 4, 1).CopyFrom(source));
    }

    [Fact]
    public void A_copy_takes_every_field_of_the_game()
    {
        // Guards CopyFrom against a field added later and forgotten: every instance field, compared by reflection after
        // a copy from a game in the middle of a shot, with powers charged and the purple placed.
        var level = Shipped(3);
        var source = new MoonfallGame(level, 5, 4, power: MoonfallPower.Bolt, ruleSet: MoonfallRuleSet.Duel);
        MoonfallTestKit.PlayOut(source, Angles, maxShots: 2);
        Assert.True(source.Shoot(-20));
        for (var t = 0; t < 70; t++)
        {
            source.Tick();
        }

        var copy = new MoonfallGame(level, 5, 77, balls: 2, power: MoonfallPower.Bolt, ruleSet: MoonfallRuleSet.Duel);
        copy.CopyFrom(source);
        var fields = typeof(MoonfallGame).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.True(fields.Length > 60, "the reflection should see the game's fields");
        foreach (var field in fields)
        {
            var a = field.GetValue(source);
            var b = field.GetValue(copy);
            if (a is Array left && b is Array right)
            {
                Assert.False(ReferenceEquals(left, right) && left.Length > 0, $"{field.Name} is shared, not copied");
                Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(left, right), $"{field.Name} differs after a copy");
            }
            else
            {
                Assert.True(Equals(a, b), $"{field.Name} differs after a copy");
            }
        }
    }
}
