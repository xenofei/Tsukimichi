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

    /// <summary><c>ipc</c>: the IPC developer window (1.8.0). Works, but is not listed to players.</summary>
    Ipc,

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

    /// <summary><c>recap [quest name]</c>: the story recap ("Previously…") of the main scenario, or of the named quest's chain.</summary>
    Recap,

    /// <summary>
    /// <c>stop</c> (1.11.0, A1): stops every walk, flight, travel chain and hand-off Tsukimichi started, for a macro or a
    /// single key.
    /// </summary>
    Stop,

    /// <summary>
    /// <c>look &lt;code&gt;</c> (1.17, plan v7 T12): opens Settings › Themes with the share code pasted and previewed. It
    /// never applies the look on its own.
    /// </summary>
    Look,

    /// <summary><c>msq</c> (1.21, P8): where the character stands in the main scenario, as one plain sentence.</summary>
    Msq,

    /// <summary><c>next</c> (1.21, P8): what to do next and where, as plain sentences for text-to-speech.</summary>
    Next,

    /// <summary><c>go [quest name]</c> (1.21, P2): travel to the current step of the selected or named quest, else its giver.</summary>
    Go,
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
        ("ipc", Subcommand.Ipc, false),
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
        ("recap", Subcommand.Recap, true),
        ("journal", Subcommand.Journal, true),
        ("moonlit", Subcommand.Moonlit, true),
        ("characters", Subcommand.Characters, true),
        ("flight", Subcommand.Flight, true),
        ("blues", Subcommand.Blues, true),
        ("stop", Subcommand.Stop, true),
        ("look", Subcommand.Look, true),
        ("msq", Subcommand.Msq, true),
        ("next", Subcommand.Next, true),
        ("go", Subcommand.Go, true),
    ];

    /// <summary>The subcommand words players are shown (and offered by <see cref="DidYouMean"/>), in help order; <c>glyphs</c> and <c>ipc</c> are not.</summary>
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
    /// Whether <c>msq</c>, <c>next</c> or <c>go</c> runs, or the line stays the quest search it was before those were
    /// subcommands (1.21, P8; as <c>look</c> routes text that is no share code to search). <c>msq</c> and <c>next</c>
    /// take no text: with text after them the whole line searches. <c>go</c> takes a quest name, but a line that begins a
    /// quest's own name ("go west" of "Go West, Craftsman", "go with the flow") searches, so no quest name is shadowed;
    /// <paramref name="beginsQuestName"/> answers that for the whole argument line (null: no quest name is known).
    /// </summary>
    public static bool RunsGuidance(ParsedCommand parsed, Func<string, bool>? beginsQuestName)
    {
        if (parsed.Kind is not (Subcommand.Msq or Subcommand.Next or Subcommand.Go))
        {
            return false;
        }

        if (parsed.Rest.Length == 0)
        {
            return true;
        }

        return parsed.Kind == Subcommand.Go && beginsQuestName?.Invoke(parsed.Arguments) != true;
    }

    /// <summary>
    /// Whether the name of a quest still in the game, as <paramref name="spoilers"/> shows it, begins with
    /// <paramref name="text"/>, case-insensitively (<see cref="RunsGuidance"/>'s question). A name the shield hides is
    /// matched by its placeholder only, so a masked quest's name is never confirmed by the routing.
    /// </summary>
    public static bool BeginsQuestName(Model.QuestCatalog catalog, Query.SpoilerMask? spoilers, string? text)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        spoilers ??= Query.SpoilerMask.None;
        foreach (var quest in catalog.All)
        {
            if (!quest.IsRemoved && spoilers.DisplayName(quest).StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
