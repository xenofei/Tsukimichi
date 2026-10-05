using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The powers at their edges and in combination (the stage 2 review): the flippers and several balls under the Full
/// Moon, Moon Gate with twins, Fireball through the approach, a twin from a sloped brick, the camera's ball, and Sage's
/// Path spread over game ticks.
/// </summary>
public sealed class MoonfallPowerEdgeTests
{
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

    // ---- Flippers in the Full Moon ----

    /// <summary>Peg 0 the green (the flippers), peg 1 the last orange, far from them; the flippers out for the next shots.</summary>
    private static MoonfallGame FlippersOutWithLastOrange()
    {
        var game = new MoonfallGame(Board(Blue(120, 250), Orange(600, 250)), 1, 1, power: MoonfallPower.Flippers);
        game.MakeGreenForTest(0);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        FinishTurn(game);
        Assert.True(game.FlippersOut);
        Assert.Equal(1, game.OrangesLeft);
        return game;
    }

    [Fact]
    public void In_the_Full_Moon_the_flippers_are_gone_and_a_ball_falls_past_the_resting_tip_into_its_bucket()
    {
        // The resting left tip stands over the first Fever bucket.
        var probe = FlippersOutWithLastOrange();
        var tip = probe.Flipper(false, 1);
        Assert.InRange(tip.TipX, 160, 180);
        Assert.InRange(tip.TipY, 565, 580);
        Assert.Equal(0, MoonfallBucket.FeverBucketAt(tip.TipX));

        // Out of the Full Moon, a ball dropped onto the tip meets it.
        probe.PlaceBall(tip.TipX, 530, 0, 100);
        var met = false;
        for (var t = 0; t < 400 && probe.Phase == MoonfallPhase.Flying; t++)
        {
            probe.Tick();
            met |= probe.BallVelocityX != 0;
        }

        Assert.True(met, "the control ball never met the flipper");

        // In it, the same ball falls straight past the tip, held flippers and all, into the first bucket.
        var game = FlippersOutWithLastOrange();
        game.PlaceBall(tip.TipX, 530, 0, 100);
        game.SetFlippers(true);
        game.LightForTest(1);
        Assert.True(game.Fever);
        Assert.False(game.FlippersOut);
        var seen = new List<(long Tick, MoonfallEvent Event)>();
        while (game.Phase == MoonfallPhase.Flying)
        {
            game.Tick();
            seen.AddRange(Drain(game));
            Assert.Equal(0, game.BallVelocityX);
            Assert.Equal(tip.TipY, game.Flipper(false, 1).TipY, 9);
            Assert.True(game.RealTick < 20_000);
        }

        var landed = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.FeverLanded).Event;
        Assert.Equal(0, landed.Peg);
        Assert.Equal(tip.TipX, landed.X, 9);
    }

    // ---- Several balls in the Full Moon ----

    [Fact]
    public void In_the_Full_Moon_the_first_ball_to_land_ends_the_turn_for_every_ball()
    {
        // The first ball just above the Fever buckets; its twin springs from the green high up the board.
        var game = new MoonfallGame(Board(Blue(120, 250), Orange(650, 150)), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        game.PlaceBall(300, 560, 0, 100);
        game.TouchForTest(0, 0);
        Assert.Equal(2, game.BallsInPlay);
        game.LightForTest(1);
        Assert.True(game.Fever);

        var seen = new List<(long Tick, MoonfallEvent Event)>();
        while (game.Phase == MoonfallPhase.Flying)
        {
            game.Tick();
            seen.AddRange(Drain(game));
            if (game.Phase == MoonfallPhase.Flying)
            {
                Assert.Equal(2, game.BallsInPlay);
            }

            Assert.True(game.RealTick < 20_000);
        }

        // One landing, the first ball's; the twin goes with it, neither lost nor caught.
        var landed = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.FeverLanded).Event;
        Assert.Equal(MoonfallBucket.FeverBucketAt(300), landed.Peg);
        Assert.Equal(300, landed.X, 9);
        Assert.Equal(0, game.BallsInPlay);
        Assert.DoesNotContain(seen, e => e.Event.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch);

        // The level is won on that one landing, with one Fever line in the tally.
        seen = RunUntil(game, static g => g.Phase is MoonfallPhase.Won or MoonfallPhase.Lost);
        Assert.Equal(MoonfallPhase.Won, game.Phase);
        Assert.DoesNotContain(seen, e => e.Event.Kind == MoonfallEventKind.FeverLanded);
        Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.ShotScored);
        Assert.Equal(landed.Value, game.Tally!.Value.FeverBonus);
    }

    // ---- Moon Gate with twins ----

    [Fact]
    public void Moon_Gate_and_Multiball_together_share_the_shots_reentries()
    {
        // The first ball below the catch line between the bucket's rims, falling out; the drum's Moon Gate gives this shot
        // one re-entry, then the green (Multiball) a twin.
        var game = new MoonfallGame(Board(Blue(120, 250)), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        game.PlaceBall(MoonfallBucket.CentreAt(game.GameTick + 1) + 30, 590, 0, 200);
        game.TriggerForTest(MoonfallPower.Gate, 0);
        Assert.Equal(1, game.GateLeft);
        game.TouchForTest(0, 0);
        Assert.Equal(2, game.BallsInPlay);

        // The first ball out takes the one re-entry: both balls fly on, and the gate is spent for the other.
        var seen = RunUntil(game, static g => g.GateLeft == 0 || g.Phase != MoonfallPhase.Flying);
        Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.BallReentered);
        Assert.Equal(MoonfallPhase.Flying, game.Phase);
        Assert.Equal(2, game.BallsInPlay);

        seen = RunUntil(game, static g => g.Phase != MoonfallPhase.Flying);
        Assert.DoesNotContain(seen, e => e.Event.Kind == MoonfallEventKind.BallReentered);
        Assert.Equal(2, seen.Count(e => e.Event.Kind is MoonfallEventKind.BallLost or MoonfallEventKind.BucketCatch));
        Assert.Equal(0, game.GateLeft);
    }

    // ---- Fireball through the approach into the Full Moon ----

    [Fact]
    public void Fireball_through_the_last_orange_slows_for_the_approach_burns_it_and_lands_in_a_Fever_bucket()
    {
        // The last orange straight under the launcher; the green's turn charges the Fireball for the next shot.
        var game = new MoonfallGame(Board(Blue(120, 250), Orange(400, 400)), 1, 1, power: MoonfallPower.Fireball);
        game.MakeGreenForTest(0);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        FinishTurn(game);
        Assert.True(game.Shoot(0));
        Assert.True(game.PowerActive(MoonfallPower.Fireball));

        var seen = new List<(long Tick, MoonfallEvent Event)>();
        var approached = false;
        while (game.Phase == MoonfallPhase.Flying)
        {
            game.Tick();
            seen.AddRange(Drain(game));
            approached |= game.Approaching;
            Assert.Equal(0, game.BallVelocityX, 9);
            Assert.True(game.BallVelocityY > 0, "it never bounces");
            Assert.True(game.RealTick < 20_000);
        }

        // The approach, then the hit: the orange lit and burnt at once, the Full Moon on, and the ball on through it.
        Assert.True(approached, "no slow motion before the last orange");
        var kinds = seen.Select(static e => e.Event.Kind).ToList();
        var approach = kinds.IndexOf(MoonfallEventKind.FeverApproach);
        var hit = kinds.IndexOf(MoonfallEventKind.FeverHit);
        var burnt = seen.FindIndex(static e => e.Event.Kind == MoonfallEventKind.PegCleared && e.Event.Peg == 1);
        Assert.True(approach >= 0 && hit > approach && burnt > hit, string.Join(", ", kinds));
        Assert.Equal(seen[hit].Tick, seen[burnt].Tick);
        Assert.True(game.Peg(1).Cleared);

        var landed = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.FeverLanded).Event;
        Assert.Equal(MoonfallBucket.FeverBucketAt(400), landed.Peg);
        RunUntil(game, static g => g.Phase is MoonfallPhase.Won or MoonfallPhase.Lost);
        Assert.Equal(MoonfallPhase.Won, game.Phase);
        Assert.True(game.Perfect);
        Assert.Equal(MoonfallRules.PerfectFeverBucketValue, game.Tally!.Value.FeverBonus);
    }

    // ---- A twin from a sloped brick ----

    [Theory]
    [InlineData(0.0)]
    [InlineData(17.0)]
    [InlineData(40.0)]
    [InlineData(-65.0)]
    [InlineData(90.0)]
    public void A_twin_from_a_sloped_green_brick_starts_clear_of_it_on_its_upper_face(double degrees)
    {
        const double Cx = 400, Cy = 300, Half = 50;
        var radians = MoonfallGeometry.Radians(degrees);
        var (dx, dy) = (Math.Cos(radians) * Half, Math.Sin(radians) * Half);
        var brick = MoonfallPeg.Line(Cx - dx, Cy - dy, Cx + dx, Cy + dy, canBeOrange: false);
        var game = new MoonfallGame(Board(brick), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        var added = Assert.Single(Drain(game), e => e.Event.Kind == MoonfallEventKind.BallAdded).Event;

        // Half a unit clear of the brick's face, square off its middle, and above it (a vertical brick has no upper face).
        var gap = MoonfallGeometry.SurfaceDistance(brick, brick.X, brick.Y, added.X, added.Y) - MoonfallRules.BallRadius;
        Assert.Equal(0.5, gap, 6);
        if (Math.Abs(degrees) == 90)
        {
            Assert.Equal(Cy, added.Y, 6);
        }
        else
        {
            Assert.True(added.Y < Cy, $"the twin starts at ({added.X:0.0}, {added.Y:0.0})");
        }
    }

    [Theory]
    [InlineData(200.0, 60.0)]
    [InlineData(30.0, 60.0)]
    public void A_twin_from_a_curved_green_brick_starts_clear_of_it_on_its_upper_face(double start, double sweep)
    {
        var brick = MoonfallPeg.Arc(400, 350, 100, start, sweep, canBeOrange: false);
        var game = new MoonfallGame(Board(brick), 1, 1, power: MoonfallPower.Multiball);
        game.MakeGreenForTest(0);
        game.PlaceBall(600, 598, 0, 300);
        game.TouchForTest(0, 0);
        var added = Assert.Single(Drain(game), e => e.Event.Kind == MoonfallEventKind.BallAdded).Event;

        var mid = MoonfallGeometry.Radians(start + (sweep * 0.5));
        var gap = MoonfallGeometry.SurfaceDistance(brick, brick.X, brick.Y, added.X, added.Y) - MoonfallRules.BallRadius;
        Assert.Equal(0.5, gap, 6);
        Assert.True(added.Y < 350 + (100 * Math.Sin(mid)), "the twin starts above the brick's middle");
    }

    // ---- The camera's ball ----

    [Fact]
    public void In_the_approach_the_camera_follows_the_twin_heading_for_the_last_orange()
    {
        // The first ball falls far to the right; the twin leaves the green up and away. Where the twin is 30 ticks on is
        // where the last orange goes on the second board, so only the twin heads for it.
        static MoonfallGame Start(params MoonfallPeg[] more)
        {
            var game = new MoonfallGame(Board([Blue(400, 300), .. more]), 1, 1, power: MoonfallPower.Multiball);
            game.MakeGreenForTest(0);
            game.PlaceBall(700, 100, 0, 0);
            game.TouchForTest(0, 0);
            Assert.Equal(2, game.BallsInPlay);
            return game;
        }

        var probe = Start();
        for (var t = 0; t < 30; t++)
        {
            probe.Tick();
        }

        Assert.Equal(2, probe.BallsInPlay);
        var (ox, oy) = probe.BallAt(1, 1);

        var game = Start(Orange(ox, oy));
        Assert.Equal(0, game.CameraBall);
        var seen = RunUntil(game, static g => g.Approaching || g.Phase != MoonfallPhase.Flying, 200);
        Assert.True(game.Approaching, "no approach");
        Assert.Equal(2, game.BallsInPlay);
        Assert.Equal(1, game.CameraBall);
        var approach = Assert.Single(seen, e => e.Event.Kind == MoonfallEventKind.FeverApproach).Event;
        Assert.Equal(game.BallAt(1, 1).X, approach.X, 6);
    }

    // ---- Sage's Path over game ticks ----

    [Fact]
    public void Sages_Path_chooses_the_same_line_at_any_frame_rate()
    {
        static MoonfallGame Ready()
        {
            var game = new MoonfallGame(MoonfallCampaigns.LoadBuiltIn().Base.Levels[3], 4, 11, power: MoonfallPower.Path);
            var green = Enumerable.Range(0, game.PegCount).First(i => game.Peg(i).Colour == PegColour.Green);
            game.PlaceBall(600, 598, 0, 300);
            game.LightForTest(green);
            FinishTurn(game);
            Assert.True(game.Shoot(-20));
            Assert.True(game.ChoosingPath);
            return game;
        }

        // Real ticks one at a time, 60 Hz frames, and uneven frames of up to a quarter second.
        var ticked = Ready();
        RunUntil(ticked, static g => g.Phase != MoonfallPhase.Flying);
        var smooth = Ready();
        var uneven = Ready();
        var frames = new MoonfallRandom(3);
        for (var frame = 0; frame < 5000 && (smooth.Phase == MoonfallPhase.Flying || uneven.Phase == MoonfallPhase.Flying); frame++)
        {
            smooth.Advance(1.0 / 60);
            uneven.Advance((5 + frames.Next(250)) / 1000.0);
        }

        foreach (var game in new[] { smooth, uneven })
        {
            Assert.False(game.ChoosingPath);
            Assert.Equal(ticked.PathNudge, game.PathNudge);
            Assert.Equal(ticked.ShotPegs, game.ShotPegs);
            Assert.Equal(ticked.ShotValue, game.ShotValue);
            Assert.InRange(game.LastPathTickSubSteps, 1, MoonfallGame.PathSubStepsPerLine);
        }
    }
}
