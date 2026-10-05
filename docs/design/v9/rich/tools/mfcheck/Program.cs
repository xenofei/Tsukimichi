// mfcheck: validates Moonfall level files with the shipped loader and plays them with the shipped engine.
//   validate <file.json>...                 the loader's verdict, counts, and the layout checks below
//   sweep <file.json> [level] [seed] [step] one fresh shot at every aim angle: what each angle reaches
//   play <file.json> [games] [level]        a greedy one-shot-lookahead player (aim error +-1.5 deg) and a random player
// Everything runs the real MoonfallLevelLoader and MoonfallGame from Tsukimichi.Core; nothing here changes them.
using System.Collections.Concurrent;
using System.Globalization;
using Tsukimichi.Core.Moonfall;

var inv = CultureInfo.InvariantCulture;
if (args.Length == 0)
{
    Console.WriteLine("usage: mfcheck validate|sweep|play <file.json> ...");
    return 2;
}

switch (args[0])
{
    case "validate":
    {
        var bad = 0;
        foreach (var file in args.Skip(1))
        {
            var load = MoonfallLevelLoader.Parse(File.ReadAllText(file));
            if (!load.Ok)
            {
                bad++;
                Console.WriteLine($"FAIL {Path.GetFileName(file)}");
                foreach (var e in load.Errors)
                {
                    Console.WriteLine("  " + e);
                }

                continue;
            }

            var level = load.Level!;
            var round = level.Pegs.Count(p => p.Shape == PegShape.Round);
            var bricks = level.Pegs.Count - round;
            var movers = level.Pegs.Count(p => p.Mover.Kind != MoverKind.None);
            Console.WriteLine($"OK   {Path.GetFileName(file)}  id={level.Id} \"{level.Name}\"  pieces={level.Pegs.Count} (round {round}, bricks {bricks}, movers {movers})  mayBeOrange={level.OrangeCandidates}");
            Layout(level);
        }

        return bad == 0 ? 0 : 1;
    }

    case "sweep":
    {
        var level = Load(args[1]);
        var number = args.Length > 2 ? int.Parse(args[2], inv) : 5;
        var seed = args.Length > 3 ? ulong.Parse(args[3], inv) : 1UL;
        var step = args.Length > 4 ? double.Parse(args[4], inv) : 1.0;
        Sweep(level, number, seed, step);
        return 0;
    }

    case "play":
    {
        var level = Load(args[1]);
        var games = args.Length > 2 ? int.Parse(args[2], inv) : 4;
        var number = args.Length > 3 ? int.Parse(args[3], inv) : 5;
        Play(level, games, number);
        return 0;
    }

    case "colours":
    {
        // The colours the engine picks for a level number and seed, in file order (pegs, then bricks), as JSON.
        var level = Load(args[1]);
        var number = args.Length > 2 ? int.Parse(args[2], inv) : 5;
        var seed = args.Length > 3 ? ulong.Parse(args[3], inv) : 1UL;
        var g = new MoonfallGame(level, number, seed);
        var names = Enumerable.Range(0, g.PegCount).Select(i => "\"" + g.Peg(i).Colour.ToString().ToLowerInvariant() + "\"");
        Console.WriteLine("[" + string.Join(",", names) + "]");
        return 0;
    }

    case "trace":
    {
        // trace <file> <angle> [level] [seed] [maxTicks]: the ball's flight from the muzzle, one point per game tick,
        // as JSON {"points":[[x,y],...],"hits":[tickIndex,...]} (hits: the tick index of each peg hit, in order).
        var level = Load(args[1]);
        var angle = double.Parse(args[2], inv);
        var number = args.Length > 3 ? int.Parse(args[3], inv) : 5;
        var seed = args.Length > 4 ? ulong.Parse(args[4], inv) : 1UL;
        var max = args.Length > 5 ? int.Parse(args[5], inv) : 400;
        var g = new MoonfallGame(level, number, seed);
        g.Shoot(angle);
        var pts = new List<string> { $"[{g.BallX.ToString("0.##", inv)},{g.BallY.ToString("0.##", inv)}]" };
        var hits = new List<int>();
        for (var i = 0; i < max && g.Phase == MoonfallPhase.Flying; i++)
        {
            g.Tick();
            while (g.TryReadEvent(out var e))
            {
                if (e.Kind == MoonfallEventKind.PegHit)
                {
                    hits.Add(pts.Count);
                }
            }

            pts.Add($"[{g.BallX.ToString("0.##", inv)},{g.BallY.ToString("0.##", inv)}]");
        }

        Console.WriteLine("{\"points\":[" + string.Join(",", pts) + "],\"hits\":[" + string.Join(",", hits) + "]}");
        return 0;
    }

    default:
        Console.WriteLine("unknown command " + args[0]);
        return 2;
}

MoonfallLevel Load(string file)
{
    var load = MoonfallLevelLoader.Parse(File.ReadAllText(file));
    if (!load.Ok)
    {
        foreach (var e in load.Errors)
        {
            Console.WriteLine("  " + e);
        }

        throw new InvalidOperationException("the level does not load");
    }

    return load.Level!;
}

// Layout checks the loader does not make: gaps where a ball could wedge, and pegs packed so close no ball passes.
void Layout(MoonfallLevel level)
{
    var ball = 2 * MoonfallRules.BallRadius;
    var round = level.Pegs.Where(p => p.Shape == PegShape.Round && p.Mover.Kind == MoverKind.None).ToList();
    var minGap = double.MaxValue;
    var wedges = 0;
    var walls = 0;
    for (var i = 0; i < round.Count; i++)
    {
        for (var j = i + 1; j < round.Count; j++)
        {
            var a = round[i];
            var b = round[j];
            var gap = Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y))) - a.Radius - b.Radius;
            minGap = Math.Min(minGap, gap);
            // A gap a little narrower than the ball cradles it: it rests on both pegs and waits for the stuck rule.
            if (gap > 0.5 && gap < ball * 0.98 && Math.Abs(a.Y - b.Y) < (a.Radius + b.Radius) * 0.9)
            {
                wedges++;
            }
        }

        // A peg so near a wall the ball cannot pass between them (and may wedge there).
        var p = round[i];
        var wallGap = Math.Min(p.X - p.Radius - MoonfallRules.LeftWall, MoonfallRules.RightWall - (p.X + p.Radius));
        if (wallGap > 0.5 && wallGap < ball * 0.98)
        {
            walls++;
        }
    }

    Console.WriteLine($"     still round pegs {round.Count}: closest surfaces {minGap.ToString("0.0", inv)} px; cradling gaps (0.5 to {ball * 0.98:0.0} px, side by side) {wedges}; wall pinches {walls}");
    var ys = level.Pegs.Select(p => p.Y).ToList();
    Console.WriteLine($"     rows used y {ys.Min().ToString("0", inv)}..{ys.Max().ToString("0", inv)}");
}

static (List<int> Hits, int Stuck, bool Caught, long Ticks, int Walls) Fly(MoonfallGame g, double angle, int limit = 30_000)
{
    var hits = new List<int>();
    var stuck = 0;
    var caught = false;
    var walls = 0;
    var start = g.GameTick;
    if (!g.Shoot(angle))
    {
        return (hits, 0, false, 0, 0);
    }

    for (var i = 0; i < limit && g.Phase == MoonfallPhase.Flying; i++)
    {
        g.Tick();
        while (g.TryReadEvent(out var e))
        {
            switch (e.Kind)
            {
                case MoonfallEventKind.PegHit: hits.Add(e.Peg); break;
                case MoonfallEventKind.StuckClear: stuck++; break;
                case MoonfallEventKind.BucketCatch: caught = true; break;
                case MoonfallEventKind.WallBounce: walls++; break;
            }
        }
    }

    var ticks = g.GameTick - start;
    for (var i = 0; i < 20_000 && g.Phase is not (MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost); i++)
    {
        g.Tick();
        while (g.TryReadEvent(out _))
        {
        }
    }

    return (hits, stuck, caught, ticks, walls);
}

void Sweep(MoonfallLevel level, int number, ulong seed, double step)
{
    var reached = new int[level.Pegs.Count];
    var rows = new List<string>();
    int zero = 0, stuckShots = 0, catches = 0, shots = 0, longFlights = 0;
    var counts = new List<int>();
    var oranges = new List<int>();
    for (var a = -MoonfallRules.AimLimitDegrees; a <= MoonfallRules.AimLimitDegrees + 1e-9; a += step)
    {
        var g = new MoonfallGame(level, number, seed);
        var (hits, stuck, caught, ticks, walls) = Fly(g, a);
        shots++;
        var distinct = hits.Distinct().ToList();
        foreach (var h in distinct)
        {
            reached[h]++;
        }

        var o = distinct.Count(h => g.Peg(h).Colour == PegColour.Orange);
        counts.Add(distinct.Count);
        oranges.Add(o);
        zero += distinct.Count == 0 ? 1 : 0;
        stuckShots += stuck > 0 ? 1 : 0;
        catches += caught ? 1 : 0;
        longFlights += ticks > 1500 ? 1 : 0;
        rows.Add($"{a,6:0.0}  pegs {distinct.Count,3}  oranges {o,2}  walls {walls}  flight {ticks / 100.0,5:0.0}s{(stuck > 0 ? $"  STUCK x{stuck}" : string.Empty)}{(caught ? "  bucket" : string.Empty)}");
    }

    foreach (var r in rows)
    {
        Console.WriteLine(r);
    }

    counts.Sort();
    Console.WriteLine();
    Console.WriteLine($"angles {shots}; pegs per first shot: median {counts[counts.Count / 2]}, mean {counts.Average():0.0}, max {counts.Max()}; oranges per first shot mean {oranges.Average():0.00}");
    Console.WriteLine($"no peg hit: {zero}; stuck rule fired: {stuckShots}; flights over 15 s: {longFlights}; bucket catches: {catches}");
    var never = Enumerable.Range(0, level.Pegs.Count).Where(i => reached[i] == 0).ToList();
    Console.WriteLine($"pieces no first shot reaches: {never.Count} of {level.Pegs.Count}" + (never.Count > 0 ? ": " + string.Join(", ", never.Take(60)) : string.Empty));
}

void Play(MoonfallLevel level, int games, int number)
{
    // The greedy player: before each shot it tries every angle 2 deg apart on a replay of the game so far and takes the
    // one that lights the most (oranges worth 6 pegs, a bucket catch worth 5), then misses its aim by up to 1.5 deg.
    var results = new ConcurrentBag<string>();
    var summary = new ConcurrentBag<(bool Won, int Shots, int OrangesLeft, int PegsLeft, int Stuck)>();
    Parallel.For(0, games, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, gi =>
    {
        var seed = (ulong)(gi + 1) * 7919UL;
        var rng = new Random(gi * 31 + 7);
        var shotsSoFar = new List<double>();
        var stuckTotal = 0;
        while (true)
        {
            var g = Replay(level, number, seed, shotsSoFar);
            if (g.Phase is MoonfallPhase.Won or MoonfallPhase.Lost || shotsSoFar.Count > 40)
            {
                var pegsLeft = Enumerable.Range(0, g.PegCount).Count(i => !g.Peg(i).Cleared);
                summary.Add((g.Phase == MoonfallPhase.Won, shotsSoFar.Count, g.OrangesLeft, pegsLeft, stuckTotal));
                var left = Enumerable.Range(0, g.PegCount).Where(i => g.Peg(i).Colour == PegColour.Orange && !g.Peg(i).Cleared && !g.Peg(i).Lit)
                    .Select(i => $"#{i}({g.Peg(i).X:0},{g.Peg(i).Y:0})");
                results.Add($"game {gi + 1}: {(g.Phase == MoonfallPhase.Won ? "WON" : "lost")} in {shotsSoFar.Count} shots, oranges left {g.OrangesLeft}, pieces left {pegsLeft}, balls left {g.BallsLeft}, score {g.Score:N0}, stuck-rule fires {stuckTotal}  {string.Join(" ", left)}");
                break;
            }

            var best = 0.0;
            var bestScore = double.MinValue;
            for (var a = -84.0; a <= 84.0; a += 2.0)
            {
                var trial = Replay(level, number, seed, shotsSoFar);
                var (hits, stuck, caught, _, _) = Fly(trial, a);
                var d = hits.Distinct().ToList();
                var o = d.Count(h => trial.Peg(h).Colour == PegColour.Orange);
                var score = d.Count + (o * 6) + (caught ? 5 : 0) - (stuck * 0.5);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }

            var aim = best + ((rng.NextDouble() * 3.0) - 1.5);
            var check = Replay(level, number, seed, shotsSoFar);
            stuckTotal += Fly(check, aim).Stuck;
            shotsSoFar.Add(aim);
        }
    });
    foreach (var r in results.OrderBy(r => r, StringComparer.Ordinal))
    {
        Console.WriteLine(r);
    }

    var s = summary.ToList();
    Console.WriteLine($"greedy: won {s.Count(x => x.Won)} of {s.Count}; shots mean {s.Average(x => x.Shots):0.0}; oranges left when lost {string.Join(",", s.Where(x => !x.Won).Select(x => x.OrangesLeft))}; stuck-rule fires {s.Sum(x => x.Stuck)}");

    // The random player: uniform angles, 40 games.
    var wins = 0;
    var orangeCleared = new List<int>();
    for (var gi = 0; gi < 40; gi++)
    {
        var rng = new Random(1000 + gi);
        var g = new MoonfallGame(level, number, (ulong)(gi + 101));
        for (var k = 0; k < 60 && g.Phase == MoonfallPhase.Aiming; k++)
        {
            Fly(g, (rng.NextDouble() * 170) - 85);
        }

        wins += g.Phase == MoonfallPhase.Won ? 1 : 0;
        orangeCleared.Add(25 - g.OrangesLeft);
    }

    Console.WriteLine($"random: won {wins} of 40; oranges cleared mean {orangeCleared.Average():0.0}");
}

static MoonfallGame Replay(MoonfallLevel level, int number, ulong seed, List<double> shots)
{
    var g = new MoonfallGame(level, number, seed);
    foreach (var a in shots)
    {
        if (g.Phase != MoonfallPhase.Aiming)
        {
            break;
        }

        Fly(g, a);
    }

    return g;
}
