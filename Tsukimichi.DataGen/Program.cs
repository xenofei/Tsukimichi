namespace Tsukimichi.DataGen;

/// <summary>
/// Offline generator: local game files -> unique_quests.json (+ report).
/// Argument parsing only; sheet scanning is implemented in T2.1.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? game = null;
        string? output = null;
        string? curated = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game" when i + 1 < args.Length:
                    game = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--curated" when i + 1 < args.Length:
                    curated = args[++i];
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (game is null || output is null)
        {
            Console.Error.WriteLine("--game and --out are required.");
            PrintUsage();
            return 2;
        }

        Console.WriteLine($"game:    {game}");
        Console.WriteLine($"out:     {output}");
        Console.WriteLine($"curated: {curated ?? "(none)"}");
        Console.WriteLine("Generation is not implemented yet (T2.1).");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Tsukimichi.DataGen --game <sqpack path> --out <unique_quests.json> [--curated <curated dir>]");
    }
}
