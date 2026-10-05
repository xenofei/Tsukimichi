using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>The engine as a whole (plan v9 G1): determinism, no allocations per step, and the shipped levels played through.</summary>
public sealed class MoonfallGameTests
{
    private static readonly double[] Angles = [-40, 25, -70, 5, 60, -15, 80, -55, 35, -5, 45, -80];

    /// <summary>
    /// The same build on the same machine: the engine uses Math.Sin, Cos and Atan2, whose last bit may differ between
    /// CPUs or runtimes, so a replay is promised to match on one machine, not across them.
    /// </summary>
    [Fact]
    public void The_same_calls_give_the_same_game_bit_for_bit()
    {
        // Frame times as a game at an uneven frame rate would pass them; the two games see the same ones.
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[2];
        var a = new MoonfallGame(level, 3, 1234);
        var b = new MoonfallGame(level, 3, 1234);
        var frames = new MoonfallRandom(99);
        for (var frame = 0; frame < 6000; frame++)
        {
            var dt = (8 + frames.Next(25)) / 1000.0;
            if (a.Phase == MoonfallPhase.Aiming)
            {
                var angle = Angles[frame % Angles.Length];
                Assert.Equal(a.Shoot(angle), b.Shoot(angle));
            }

            a.Advance(dt);
            b.Advance(dt);
            Assert.Equal(a.Fingerprint(), b.Fingerprint());
        }

        Assert.True(a.Score > 0);
    }

    [Fact]
    public void Stepping_a_ball_through_a_level_allocates_nothing()
    {
        // The repo's allocation check (GC.GetAllocatedBytesForCurrentThread round the measured calls, after a warm-up
        // that runs the same calls): shots, flight, bounces, the stuck watch, clearing, events and the guide.
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[3];
        var dots = new (double X, double Y)[MoonfallRules.GuideMaxDots];

        void Play(MoonfallGame game, int frames)
        {
            for (var frame = 0; frame < frames; frame++)
            {
                if (game.Phase == MoonfallPhase.Aiming)
                {
                    game.Guide(Angles[frame % Angles.Length], dots);
                    game.Shoot(Angles[frame % Angles.Length]);
                }

                game.Advance(1.0 / 60);
                while (game.TryReadEvent(out _))
                {
                }
            }
        }

        Play(new MoonfallGame(level, 4, 5), 3000);
        var measured = new MoonfallGame(level, 4, 5);
        Play(measured, 60);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Play(measured, 2400);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(measured.GameTick > 3000);
    }

    [Fact]
    public void The_approach_Full_Moon_and_the_tally_allocate_nothing()
    {
        // One orange straight below: the shot runs the approach, the hit, the speed ramp, the stuck watch on the lit
        // orange, a Fever bucket, the clearing and the won tally, with the window's reads every frame.
        var dots = new (double X, double Y)[MoonfallRules.GuideMaxDots];

        void Play(MoonfallGame game)
        {
            game.Guide(0, dots);
            game.Shoot(0);
            for (var frame = 0; frame < 3000; frame++)
            {
                game.Advance(1.0 / 60);
                var alpha = game.Alpha;
                _ = game.BallAt(alpha);
                _ = game.BucketXAt(alpha);
                for (var i = 0; i < game.PegCount; i++)
                {
                    _ = game.Peg(i, alpha);
                }

                while (game.TryReadEvent(out _))
                {
                }
            }
        }

        static MoonfallGame Fresh() => new(Board(Orange(400, 300), MoonfallPeg.Round(200, 400, canBeOrange: false, mover: new PegMover(MoverKind.Orbit, 240, 400, 5, true))), 1, 3);

        Play(Fresh());
        var measured = Fresh();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Play(measured);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(MoonfallPhase.Won, measured.Phase);
        Assert.NotNull(measured.Tally);
    }

    [Fact]
    public void The_fingerprint_sees_the_clocks_the_counter_and_the_events()
    {
        var a = new MoonfallGame(Grid(40), 1, 9);
        var b = new MoonfallGame(Grid(40), 1, 9);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());

        // Half a tick of wall-clock time moves no ball, but it is state.
        a.Advance(0.005);
        Assert.NotEqual(a.Fingerprint(), b.Fingerprint());
        b.Advance(0.005);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());

        // An unread event is state too.
        a.PlaceBall(90, 580, 0, 300);
        b.PlaceBall(90, 580, 0, 300);
        a.LightForTest(0);
        b.LightForTest(0);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        a.TryReadEvent(out _);
        Assert.NotEqual(a.Fingerprint(), b.Fingerprint());
    }

    [Fact]
    public void Every_shipped_level_plays_to_an_end_and_lights_pegs()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        Assert.Empty(campaigns.Errors);
        for (var n = 0; n < campaigns.Base.Levels.Count; n++)
        {
            var game = new MoonfallGame(campaigns.Base.Levels[n], n + 1, (ulong)(n + 17));
            var lit = 0;
            var shots = 0;
            while (game.Phase is not (MoonfallPhase.Won or MoonfallPhase.Lost) && shots < 30)
            {
                RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost);
                if (game.Phase != MoonfallPhase.Aiming)
                {
                    break;
                }

                game.Shoot(Angles[shots++ % Angles.Length]);
                lit += RunUntil(game, static g => g.Phase != MoonfallPhase.Flying).Count(e => e.Event.Kind == MoonfallEventKind.PegHit);
            }

            Assert.True(lit > 10, $"{campaigns.Base.Levels[n].Id}: only {lit} pegs lit in {shots} shots");
            Assert.True(game.Phase is MoonfallPhase.Won or MoonfallPhase.Lost, $"{campaigns.Base.Levels[n].Id} did not end");
        }
    }

    [Fact]
    public void Shooting_is_refused_while_a_ball_is_in_play_or_the_level_is_over()
    {
        var game = new MoonfallGame(Grid(40), 1, 1);
        Assert.True(game.Shoot(0));
        Assert.False(game.Shoot(10));
        Assert.Equal(9, game.BallsLeft);
        Assert.False(game.Shoot(double.NaN));
    }

    [Fact]
    public void The_event_ring_keeps_the_latest_256()
    {
        var game = new MoonfallGame(Grid(60), 1, 1);
        game.PlaceBall(90, 580, 0, 300);
        for (var i = 0; i < 60; i++)
        {
            game.LightForTest(i);
        }

        var count = 0;
        while (game.TryReadEvent(out _))
        {
            count++;
        }

        Assert.InRange(count, 60, 256);
        Assert.False(game.TryReadEvent(out _));
    }
}
