namespace Tsukimichi.Core.Text;

/// <summary>What a <c>/tsukimichi</c> line asks for, by its first word (<see cref="CommandLine.Parse"/>).</summary>
public enum Subcommand
{
    /// <summary>No arguments: toggle the main window.</summary>
    Toggle,

    /// <summary><c>search &lt;text&gt;</c>, or any line whose first word is not a subcommand.</summary>
    Search,

    /// <summary><c>glyphs</c>: the glyph sheet. Works, but is not listed to players.</summary>
    Glyphs,

    /// <summary><c>settings</c> or <c>config</c>.</summary>
    Settings,
    Help,

    /// <summary><c>tour</c>: starts the interactive tour.</summary>
    Tour,
    Zone,
    Which,
    Why,
    Nearby,
    Todo,
    Report,
    Export,

    /// <summary><c>route [quest name]</c>: the unlock route to the named (or selected) quest.</summary>
    Route,

    /// <summary><c>journal</c>: the Journal tab.</summary>
    Journal,

    /// <summary><c>moonlit</c>: the Moonlit tab.</summary>
    Moonlit,

    /// <summary><c>characters</c>: the Characters tab.</summary>
    Characters,

    /// <summary><c>flight</c>: the Flight tab.</summary>
    Flight,

    /// <summary><c>blues</c>: the My blues tab.</summary>
    Blues,
}

/// <summary>A parsed <c>/tsukimichi</c> line.</summary>
/// <param name="Kind">The subcommand; <see cref="Subcommand.Search"/> for a line that names none.</param>
/// <param name="Word">The first word as typed (empty with no arguments).</param>
/// <param name="Rest">What follows the first word, trimmed (empty when nothing does).</param>
/// <param name="Arguments">The whole argument line, trimmed: what an unknown first word searches for.</param>
public readonly record struct ParsedCommand(Subcommand Kind, string Word, string Rest, string Arguments)
{
    /// <summary>The text a search runs on: <see cref="Rest"/> after <c>search</c>, the whole line otherwise.</summary>
    public string SearchText => Kind == Subcommand.Search && string.Equals(Word, "search", StringComparison.OrdinalIgnoreCase) ? Rest : Arguments;
}

/// <summary>
/// The <c>/tsukimichi</c> (<c>/tsuki</c>) command line (feature plan v5, 1.7.0): the first word picks a subcommand,
/// case-insensitively; any other first word makes the whole line a search. When such a search finds nothing and its
/// first word is a near miss of a subcommand, <see cref="DidYouMean"/> names the subcommand ("Did you mean /tsuki
/// nearby?"). Pure, so the plugin's handler and the tests agree.
/// </summary>
public static class CommandLine
{
    private static readonly (string Word, Subcommand Kind, bool Listed)[] Words =
    [
        ("search", Subcommand.Search, true),
        ("glyphs", Subcommand.Glyphs, false),
        ("settings", Subcommand.Settings, true),
        ("config", Subcommand.Settings, true),
        ("help", Subcommand.Help, true),
        ("tour", Subcommand.Tour, true),
        ("zone", Subcommand.Zone, true),
        ("which", Subcommand.Which, true),
        ("why", Subcommand.Why, true),
        ("nearby", Subcommand.Nearby, true),
        ("todo", Subcommand.Todo, true),
        ("report", Subcommand.Report, true),
        ("export", Subcommand.Export, true),
        ("route", Subcommand.Route, true),
        ("journal", Subcommand.Journal, true),
        ("moonlit", Subcommand.Moonlit, true),
        ("characters", Subcommand.Characters, true),
        ("flight", Subcommand.Flight, true),
        ("blues", Subcommand.Blues, true),
    ];

    /// <summary>The subcommand words players are shown (and offered by <see cref="DidYouMean"/>), in help order; <c>glyphs</c> is not one.</summary>
    public static IEnumerable<string> ListedWords => Words.Where(static w => w.Listed).Select(static w => w.Word);

    /// <summary>Splits <paramref name="arguments"/> at its first space and names the subcommand.</summary>
    public static ParsedCommand Parse(string? arguments)
    {
        var args = (arguments ?? string.Empty).Trim();
        if (args.Length == 0)
        {
            return new ParsedCommand(Subcommand.Toggle, string.Empty, string.Empty, string.Empty);
        }

        var split = args.IndexOf(' ', StringComparison.Ordinal);
        var word = split < 0 ? args : args[..split];
        var rest = split < 0 ? string.Empty : args[(split + 1)..].Trim();
        foreach (var (name, kind, _) in Words)
        {
            if (string.Equals(word, name, StringComparison.OrdinalIgnoreCase))
            {
                return new ParsedCommand(kind, word, rest, args);
            }
        }

        return new ParsedCommand(Subcommand.Search, word, rest, args);
    }

    /// <summary>
    /// The listed subcommand <paramref name="word"/> was probably meant to be, or null: one edit away for words of up
    /// to five letters, two for longer ones (a typo, a missing or doubled letter, two letters swapped), never the word
    /// itself, and never for words under three letters. Ties go to the earlier word in <see cref="ListedWords"/>.
    /// </summary>
    public static string? DidYouMean(string? word)
    {
        var typed = (word ?? string.Empty).Trim().ToLowerInvariant();
        if (typed.Length < 3)
        {
            return null;
        }

        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var (name, _, listed) in Words)
        {
            if (!listed)
            {
                continue;
            }

            if (name == typed)
            {
                return null;
            }

            var limit = Math.Max(typed.Length, name.Length) <= 5 ? 1 : 2;
            var distance = Distance(typed, name);
            if (distance <= limit && distance < bestDistance)
            {
                best = name;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Optimal string alignment distance: insertions, deletions, substitutions and adjacent swaps each cost one.</summary>
    public static int Distance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, d[i - 2, j - 2] + 1);
                }

                d[i, j] = value;
            }
        }

        return d[a.Length, b.Length];
    }
}
