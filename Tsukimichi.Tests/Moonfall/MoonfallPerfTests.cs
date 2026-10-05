using System.Diagnostics;
using Tsukimichi.Core.Moonfall;
using Xunit.Abstractions;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Moonfall's costs on the framework thread (run with <c>--filter Category=Perf</c>).</summary>
public sealed class MoonfallPerfTests
{
    private readonly ITestOutputHelper output;

    public MoonfallPerfTests(ITestOutputHelper output) => this.output = output;

    /// <summary>
    /// A full board: the loader's most pegs (<see cref="MoonfallLevelLoader.MaxPegs"/>), staggered rows across the whole
    /// playfield with gaps narrower than the ball, so every flight is a long run of contacts (the search's worst case).
    /// </summary>
    internal static MoonfallLevel FullBoard()
    {
        var pegs = new MoonfallPeg[MoonfallLevelLoader.MaxPegs];
        for (var i = 0; i < pegs.Length; i++)
        {
            var row = i / 20;
            var col = i % 20;
            pegs[i] = MoonfallPeg.Round(96 + (col * 32) + (row % 2 == 1 ? 16 : 0), 180 + (row * 19), canBeOrange: true);
        }

        return Board(pegs);
    }

    /// <summary>A game on <paramref name="level"/> whose next shot carries Sage's Path.</summary>
    internal static MoonfallGame PathReady(MoonfallLevel level)
    {
        var game = new MoonfallGame(level, 4, 11, power: MoonfallPower.Path);
        var green = Enumerable.Range(0, game.PegCount).First(i => game.Peg(i).Colour == PegColour.Green);
        game.PlaceBall(MoonfallRules.LeftWall + 10, 598, 0, 300);
        game.LightForTest(green);
        RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost);
        Assert.Equal(MoonfallPhase.Aiming, game.Phase);
        Assert.Equal(1, game.PowerShotsLeft(MoonfallPower.Path));
        return game;
    }

    /// <summary>
    /// Sage's Path on a full board: the whole search is tens of milliseconds there (about 2 ms on the largest shipped
    /// level), too long for one frame, so it runs one line a game tick while the ball waits in the barrel. A tick's
    /// share is one line's budget of sub-steps: about 1.5 ms on the full board, a tenth of a millisecond on a shipped one.
    /// </summary>
    [Fact]
    [Trait("Category", "Perf")]
    public void Sages_Path_takes_one_line_a_tick_on_a_full_board()
    {
        var full = FullBoard();
        var shipped = MoonfallCampaigns.LoadBuiltIn().Base.Levels[3];
        double[] angles = [-60, -30, -10, 0, 12, 35, 70];

        // Warm up the JIT (the same calls, enough for the hot loop to reach its optimised tier).
        for (var k = 0; k < 4; k++)
        {
            Measure(PathReady(full), 5);
            Measure(PathReady(shipped), 5);
        }

        var worstMedian = 0.0;
        foreach (var (name, level) in new[] { ("full board, 400 pegs", full), ("base-04, the largest shipped", shipped) })
        {
            var ticks = new List<double>();
            var worstSearch = 0.0;
            foreach (var angle in angles)
            {
                var game = PathReady(level);
                var (each, search) = Measure(game, angle);
                ticks.AddRange(each);
                worstSearch = Math.Max(worstSearch, search);
                Assert.InRange(game.LastPathSubSteps, 1, MoonfallRules.PathSubStepBudget);
                Assert.InRange(game.LastPathTickSubSteps, 1, MoonfallGame.PathSubStepsPerLine);
            }

            ticks.Sort();
            output.WriteLine(
                $"{name}: a tick of the search {ticks[ticks.Count / 2]:0.000} ms median, {ticks[^1]:0.000} ms worst; " +
                $"the whole search {worstSearch:0.000} ms at worst, spread over {MoonfallGame.PathCandidates} ticks");
            worstMedian = Math.Max(worstMedian, ticks[ticks.Count / 2]);
        }

        // The median, so another process's moment on the CPU does not fail the run (the worst tick is printed above).
        Assert.True(worstMedian < 3, $"a tick of the search took {worstMedian:0.000} ms (median)");
    }

    /// <summary>Shoots at <paramref name="angle"/> and times each game tick of the search: every tick and the sum (ms).</summary>
    private static (List<double> Ticks, double Search) Measure(MoonfallGame game, double angle)
    {
        Assert.True(game.Shoot(angle));
        var ticks = new List<double>(MoonfallGame.PathCandidates);
        while (game.ChoosingPath)
        {
            var watch = Stopwatch.StartNew();
            game.Tick();
            watch.Stop();
            ticks.Add(watch.Elapsed.TotalMilliseconds);
        }

        Assert.Equal(MoonfallGame.PathCandidates, ticks.Count);
        return (ticks, ticks.Sum());
    }
}
