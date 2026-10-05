using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Small boards and helpers for the Moonfall tests.</summary>
internal static class MoonfallTestKit
{
    /// <summary>A board with only <paramref name="pegs"/> (no loader checks: tests place pegs where they need them).</summary>
    public static MoonfallLevel Board(params MoonfallPeg[] pegs) => new("test", "Test", pegs);

    /// <summary>A peg that may not be orange (so a test board picks no oranges unless it asks for them).</summary>
    public static MoonfallPeg Blue(double x, double y, double r = MoonfallRules.PegRadius) => MoonfallPeg.Round(x, y, r, canBeOrange: false);

    /// <summary>A peg that will be orange: on a board with fewer than 25 candidates every candidate is orange.</summary>
    public static MoonfallPeg Orange(double x, double y, double r = MoonfallRules.PegRadius) => MoonfallPeg.Round(x, y, r, canBeOrange: true);

    /// <summary>A board of <paramref name="count"/> pegs in a grid, all of which may be orange.</summary>
    public static MoonfallLevel Grid(int count)
    {
        var pegs = new MoonfallPeg[count];
        for (var i = 0; i < count; i++)
        {
            pegs[i] = MoonfallPeg.Round(110 + ((i % 15) * 40), 220 + ((i / 15) * 45));
        }

        return Board(pegs);
    }

    /// <summary>Runs real ticks until <paramref name="until"/> holds or <paramref name="limit"/> ticks pass, collecting events with the game tick they came on.</summary>
    public static List<(long Tick, MoonfallEvent Event)> RunUntil(MoonfallGame game, Func<MoonfallGame, bool> until, int limit = 20_000)
    {
        var seen = new List<(long, MoonfallEvent)>();
        for (var i = 0; i < limit && !until(game); i++)
        {
            game.Tick();
            while (game.TryReadEvent(out var e))
            {
                seen.Add((game.GameTick, e));
            }
        }

        return seen;
    }

    /// <summary>Plays the level to its end, shooting at <paramref name="angles"/> in turn.</summary>
    public static void PlayOut(MoonfallGame game, double[] angles, int maxShots = 40)
    {
        var shot = 0;
        while (game.Phase is not (MoonfallPhase.Won or MoonfallPhase.Lost) && shot < maxShots)
        {
            RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost);
            if (game.Phase != MoonfallPhase.Aiming)
            {
                break;
            }

            Assert.True(game.Shoot(angles[shot % angles.Length]));
            shot++;
        }

        RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost);
    }
}
