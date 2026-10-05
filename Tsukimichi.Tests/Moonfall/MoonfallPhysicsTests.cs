using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The engine against the footage (plan v9 G1; docs/research/plan-v9/peg-measurements.md): each test replays one of the
/// measurements the way the measuring scripts took it and checks the engine lands inside the measured tolerance. Row
/// numbers [M n] are the summary table's.
/// </summary>
public sealed class MoonfallPhysicsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(-60)]
    [InlineData(81)]
    public void A_free_flight_arc_has_the_measured_gravity_and_no_drag(double angle)
    {
        // [M 1]: g = 499.6 ± 0.5 over 144 arcs, horizontal acceleration 0 ± 2. The arc is fitted exactly as pegfit.py
        // fits one: x = x0 + vx·t, y = y0 + vy·t + g·t²/2 over the tick times, here 1.2 s of a shot into an empty board.
        var game = new MoonfallGame(Board(), 1, 1);
        Assert.True(game.Shoot(angle));
        var t = new List<double>();
        var xs = new List<double>();
        var ys = new List<double>();
        for (var tick = 0; tick <= 120 && game.BallY < 560 && game.BallX is > 90 and < 710; tick++)
        {
            t.Add(tick * MoonfallRules.TickSeconds);
            xs.Add(game.BallX);
            ys.Add(game.BallY);
            game.Tick();
        }

        Assert.True(t.Count > 40, $"the arc lasted only {t.Count} ticks");
        var (_, _, ay) = FitQuadratic(t, ys);
        var (_, _, ax) = FitQuadratic(t, xs);
        Assert.InRange(2 * ay, 499.6 - 0.5, 499.6 + 0.5);
        Assert.InRange(2 * ax, -2, 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(-81)]
    public void The_ball_leaves_the_barrel_at_the_measured_speed_and_place(double angle)
    {
        // [M 2]: 392 ± 5 px/s (393–398 on the best shots), 73 px from the pivot at (400, 87).
        var game = new MoonfallGame(Board(), 1, 1);
        Assert.True(game.Shoot(angle));
        var speed = Math.Sqrt((game.BallVelocityX * game.BallVelocityX) + (game.BallVelocityY * game.BallVelocityY));
        Assert.InRange(speed, 392 - 5, 398);
        var reach = Math.Sqrt(Math.Pow(game.BallX - 400, 2) + Math.Pow(game.BallY - 87, 2));
        Assert.Equal(73, reach, 6);
    }

    [Fact]
    public void Sixty_frames_a_second_see_the_100_Hz_clock_as_two_two_one()
    {
        // [M 0]: at 60 fps the ball's per-frame steps repeat 2, 2, 1 ticks: a fixed 100 Hz simulation.
        var game = new MoonfallGame(Board(), 1, 1);
        var perFrame = new List<long>();
        var last = game.GameTick;
        for (var frame = 0; frame < 60; frame++)
        {
            game.Advance(1.0 / 60);
            perFrame.Add(game.GameTick - last);
            last = game.GameTick;
        }

        Assert.All(perFrame, n => Assert.InRange(n, 1, 2));
        Assert.Equal(100, game.GameTick);
        for (var i = 0; i + 3 <= perFrame.Count; i += 3)
        {
            Assert.InRange(perFrame[i] + perFrame[i + 1] + perFrame[i + 2], 4, 6);
        }
    }

    [Fact]
    public void A_hitch_never_fast_forwards_more_than_a_quarter_second()
    {
        var game = new MoonfallGame(Board(), 1, 1);
        game.Advance(5.0);
        Assert.Equal(25, game.GameTick);
    }

    [Theory]
    [InlineData(-300, 150)]
    [InlineData(-200, 300)]
    [InlineData(250, 100)]
    [InlineData(350, 250)]
    public void A_wall_bounce_scales_both_components_by_three_quarters_and_turns_at_the_measured_line(double vx, double vy)
    {
        // [M 4]: both components × 0.75 ± 0.02; [M 3]: the ball's centre turns at x = 81.5 and 718.5 (± 0.5).
        var game = new MoonfallGame(Board(), 1, 1);
        var startX = vx < 0 ? 120 : 680;
        game.PlaceBall(startX, 250, vx, vy);
        RunUntil(game, static g => g.LastBounce is not null, 100);

        var bounce = Assert.NotNull(game.LastBounce);
        Assert.Equal(-1, bounce.Peg);
        Assert.Equal(0.75, -bounce.OutX / bounce.InX, 0.02);
        Assert.Equal(0.75, bounce.OutY / bounce.InY, 0.02);
        Assert.InRange(bounce.X, vx < 0 ? 81.0 : 718.0, vx < 0 ? 82.0 : 719.0);
    }

    [Fact]
    public void Peg_bounces_fit_the_measured_restitution_and_contact_distance()
    {
        // [M 4]: e_n = 0.80 ± 0.02 and e_t = 0.95 ± 0.05 from |v_out|² = e_n²(v·n)² + e_t²(v·t)² fitted over the bounces;
        // the overall speed ratio's median 0.82 (IQR 0.80–0.85); [M 3]: centre distance at contact 16.7 ± 1.0.
        // The ball comes in at 250–450 px/s with impact parameters spread evenly across the peg, as in play.
        var bounces = new List<MoonfallBounce>();
        for (var k = 0; k < 41; k++)
        {
            var b = -0.95 + (k * 1.9 / 40);
            var speed = 250 + (k % 5 * 50);
            var game = new MoonfallGame(Board(Blue(400, 300)), 1, 1);
            game.PlaceBall(400 + (b * 16), 230, 0, speed);
            RunUntil(game, static g => g.LastBounce is not null, 60);
            var bounce = Assert.NotNull(game.LastBounce);
            Assert.Equal(0, bounce.Peg);
            bounces.Add(bounce);
        }

        // Least squares for e_n² and e_t² over the two components of each bounce.
        double snn = 0, stt = 0, snt = 0, sny = 0, sty = 0;
        var ratios = new List<double>();
        foreach (var b in bounces)
        {
            var vn = (b.InX * b.Nx) + (b.InY * b.Ny);
            var vt = (b.InX * -b.Ny) + (b.InY * b.Nx);
            var out2 = (b.OutX * b.OutX) + (b.OutY * b.OutY);
            var n2 = vn * vn;
            var t2 = vt * vt;
            snn += n2 * n2;
            stt += t2 * t2;
            snt += n2 * t2;
            sny += n2 * out2;
            sty += t2 * out2;
            ratios.Add(Math.Sqrt(out2 / ((b.InX * b.InX) + (b.InY * b.InY))));
            var distance = Math.Sqrt(Math.Pow(b.X - 400, 2) + Math.Pow(b.Y - 300, 2));
            Assert.InRange(distance, 16.7 - 1.0, 16.7 + 1.0);
        }

        var det = (snn * stt) - (snt * snt);
        var en = Math.Sqrt(((sny * stt) - (sty * snt)) / det);
        var et = Math.Sqrt(((sty * snn) - (sny * snt)) / det);
        Assert.InRange(en, 0.80 - 0.02, 0.80 + 0.02);
        Assert.InRange(et, 0.95 - 0.05, 0.95 + 0.05);
        ratios.Sort();
        Assert.InRange(ratios[ratios.Count / 2], 0.80, 0.85);
    }

    [Fact]
    public void Head_on_and_grazing_bounces_match_the_measured_speed_ratios()
    {
        // [M 4, DX]: head-on (|cos| > 0.8) 0.81, grazing (|cos| < 0.5) 0.97, each within the e_n / e_t tolerances.
        static double Ratio(double offset)
        {
            var game = new MoonfallGame(Board(Blue(400, 300)), 1, 1);
            game.PlaceBall(400 + offset, 240, 0, 350);
            RunUntil(game, static g => g.LastBounce is not null, 60);
            var b = Assert.NotNull(game.LastBounce);
            return Math.Sqrt(((b.OutX * b.OutX) + (b.OutY * b.OutY)) / ((b.InX * b.InX) + (b.InY * b.InY)));
        }

        Assert.InRange(Ratio(0), 0.81 - 0.03, 0.81 + 0.03);
        Assert.InRange(Ratio(15), 0.97 - 0.05, 0.97 + 0.03);
    }

    [Fact]
    public void Bricks_bounce_the_ball_like_pegs()
    {
        // A straight brick under a falling ball sends it back up at e_n, and a curved one bounces it off its curve.
        var line = new MoonfallGame(Board(MoonfallPeg.Line(350, 300, 450, 300, canBeOrange: false)), 1, 1);
        line.PlaceBall(400, 230, 0, 300);
        RunUntil(line, static g => g.LastBounce is not null, 100);
        var straight = Assert.NotNull(line.LastBounce);
        Assert.Equal(0.80, -straight.OutY / straight.InY, 0.01);
        Assert.Equal(300 - 10 - 6, straight.Y, 0.5);

        var arc = new MoonfallGame(Board(MoonfallPeg.Arc(400, 400, 100, 200, 140, canBeOrange: false)), 1, 1);
        arc.PlaceBall(400, 230, 0, 300);
        RunUntil(arc, static g => g.LastBounce is not null, 100);
        Assert.Equal(400 - 100 - 10 - 6, Assert.NotNull(arc.LastBounce).Y, 0.5);
        Assert.True(arc.Peg(0).Lit);
    }

    [Fact]
    public void A_fast_ball_never_passes_through_a_peg()
    {
        var game = new MoonfallGame(Board(Blue(400, 500)), 1, 1);
        game.PlaceBall(400, 120, 0, 900);
        RunUntil(game, static g => g.Phase != MoonfallPhase.Flying || g.Peg(0).Lit, 200);
        Assert.True(game.Peg(0).Lit);
    }

    [Fact]
    public void A_moving_peg_moves_on_its_path_and_still_lights()
    {
        var orbit = MoonfallPeg.Round(460, 300, mover: new PegMover(MoverKind.Orbit, 400, 300, 4, true));
        var game = new MoonfallGame(Board(orbit), 1, 1);
        RunUntil(game, static _ => false, 100);
        var quarter = game.Peg(0);
        Assert.Equal(400, quarter.X, 3);
        Assert.Equal(360, quarter.Y, 3);
        RunUntil(game, static _ => false, 300);
        Assert.Equal(460, game.Peg(0).X, 3);
        Assert.Equal(300, game.Peg(0).Y, 3);
    }

    /// <summary>Least squares of y = a + b·t + c·t².</summary>
    private static (double A, double B, double C) FitQuadratic(IReadOnlyList<double> t, IReadOnlyList<double> y)
    {
        double s0 = t.Count, s1 = 0, s2 = 0, s3 = 0, s4 = 0, y0 = 0, y1 = 0, y2 = 0;
        for (var i = 0; i < t.Count; i++)
        {
            var ti = t[i];
            s1 += ti;
            s2 += ti * ti;
            s3 += ti * ti * ti;
            s4 += ti * ti * ti * ti;
            y0 += y[i];
            y1 += y[i] * ti;
            y2 += y[i] * ti * ti;
        }

        // Cramer's rule on the normal equations.
        double Det(double a, double b, double c, double d, double e, double f, double g, double h, double k) =>
            (a * ((e * k) - (f * h))) - (b * ((d * k) - (f * g))) + (c * ((d * h) - (e * g)));
        var det = Det(s0, s1, s2, s1, s2, s3, s2, s3, s4);
        var a = Det(y0, s1, s2, y1, s2, s3, y2, s3, s4) / det;
        var b = Det(s0, y0, s2, s1, y1, s3, s2, y2, s4) / det;
        var c = Det(s0, s1, y0, s1, s2, y1, s2, s3, y2) / det;
        return (a, b, c);
    }
}
