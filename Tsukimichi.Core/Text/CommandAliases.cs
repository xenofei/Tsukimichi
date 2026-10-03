namespace Tsukimichi.Core.Text;

/// <summary>What the Settings field of extra aliases holds: the valid aliases, normalized, and the words that are not.</summary>
/// <param name="Aliases">Valid aliases in the order typed, lowercase, each once, without the built-in ones.</param>
/// <param name="Invalid">The words that are not an alias (no leading slash, a character other than a letter or digit, too long), as typed.</param>
public readonly record struct AliasParse(IReadOnlyList<string> Aliases, IReadOnlyList<string> Invalid);

/// <summary>What to change so the registered aliases match the wanted ones (<see cref="CommandAliases.Plan"/>).</summary>
/// <param name="Remove">Aliases registered now that are no longer wanted.</param>
/// <param name="Add">Wanted aliases to register.</param>
/// <param name="Skipped">Wanted aliases that Dalamud, the game or another plugin already uses; never registered.</param>
public readonly record struct AliasPlan(IReadOnlyList<string> Remove, IReadOnlyList<string> Add, IReadOnlyList<string> Skipped);

/// <summary>
/// The other names of the <c>/tsuki</c> command (1.11.0, A12): <c>/ts</c> and <c>/moon</c> always, plus the player's own
/// from Settings. Each opens Tsukimichi exactly as <c>/tsuki</c> does, subcommands included. An alias is a slash and
/// then 1 to <see cref="MaxLetters"/> ASCII letters or digits; it is kept lowercase. An alias that something else
/// already answers to is skipped, never taken over. Pure, so the plugin and the tests agree.
/// </summary>
public static class CommandAliases
{
    /// <summary>The primary command, which stays Tsukimichi's whatever the aliases.</summary>
    public const string Primary = "/tsuki";

    /// <summary>The long form, registered beside <see cref="Primary"/>.</summary>
    public const string FullName = "/tsukimichi";

    /// <summary>The most letters and digits an alias may have after its slash.</summary>
    public const int MaxLetters = 24;

    /// <summary>The aliases every player gets, in the order the help lists them.</summary>
    public static IReadOnlyList<string> BuiltIn { get; } = ["/ts", "/moon"];

    /// <summary>
    /// True when <paramref name="alias"/> is a slash followed by 1 to <see cref="MaxLetters"/> ASCII letters or digits,
    /// with no space anywhere.
    /// </summary>
    public static bool IsValid(string? alias)
    {
        if (alias is null || alias.Length < 2 || alias.Length > MaxLetters + 1 || alias[0] != '/')
        {
            return false;
        }

        for (var i = 1; i < alias.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(alias[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the Settings field: words separated by spaces or commas. Valid ones are kept lowercase, each once, in
    /// order; <see cref="Primary"/>, <see cref="FullName"/> and the <see cref="BuiltIn"/> aliases are dropped, as they
    /// are registered anyway. Every other word is listed as invalid, as typed.
    /// </summary>
    public static AliasParse Parse(string? text)
    {
        var aliases = new List<string>();
        var invalid = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { Primary, FullName };
        foreach (var alias in BuiltIn)
        {
            seen.Add(alias);
        }

        foreach (var word in (text ?? string.Empty).Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsValid(word))
            {
                if (!invalid.Contains(word, StringComparer.Ordinal))
                {
                    invalid.Add(word);
                }

                continue;
            }

            var normal = word.ToLowerInvariant();
            if (seen.Add(normal))
            {
                aliases.Add(normal);
            }
        }

        return new AliasParse(aliases, invalid);
    }

    /// <summary>Every alias the player wants registered: the <see cref="BuiltIn"/> ones, then the valid ones of <paramref name="text"/>.</summary>
    public static IReadOnlyList<string> Wanted(string? text) => [.. BuiltIn, .. Parse(text).Aliases];

    /// <summary>
    /// Compares the aliases registered now with the <paramref name="wanted"/> ones: those no longer wanted are removed,
    /// and a wanted one not registered yet is added unless <paramref name="takenElsewhere"/> says Dalamud, the game or
    /// another plugin answers to it, when it is skipped. An alias Tsukimichi already holds stays; it is never asked about.
    /// <see cref="Primary"/> and <see cref="FullName"/> are never aliases and are left out.
    /// </summary>
    public static AliasPlan Plan(IEnumerable<string> registered, IEnumerable<string> wanted, Func<string, bool> takenElsewhere)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(takenElsewhere);
        var held = new HashSet<string>(registered, StringComparer.Ordinal);
        var want = new List<string>();
        var wantSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alias in wanted)
        {
            if (IsValid(alias) && wantSet.Add(alias.ToLowerInvariant()))
            {
                want.Add(alias.ToLowerInvariant());
            }
        }

        var remove = held.Where(alias => !wantSet.Contains(alias)).Order(StringComparer.Ordinal).ToList();
        var add = new List<string>();
        var skipped = new List<string>();
        foreach (var alias in want)
        {
            // The primary command and its long form are registered on their own, never as aliases.
            if (held.Contains(alias) || alias is Primary or FullName)
            {
                continue;
            }

            if (takenElsewhere(alias))
            {
                skipped.Add(alias);
            }
            else
            {
                add.Add(alias);
            }
        }

        return new AliasPlan(remove, add, skipped);
    }
}
