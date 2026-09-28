using System.Text;

namespace Tsukimichi.Core.Model;

/// <summary>
/// Title-cases sheet names that the game stores in lower case ("magitek armor", "paladin"). Every word's first letter
/// is capitalised except small words (of, the, and, a, an, in, on, to, for) that are not the first word. A name that
/// already contains an uppercase letter is returned unchanged, so "CHL P-0005" or "Wind-up Cid" keep their spelling.
/// </summary>
public static class NameCase
{
    /// <summary>Lower-case words left as they are unless they open the name.</summary>
    private static readonly HashSet<string> SmallWords = new(StringComparer.Ordinal)
    {
        "of", "the", "and", "a", "an", "in", "on", "to", "for",
    };

    /// <summary>Title-cases <paramref name="name"/> as described on the class; null or empty comes back as is.</summary>
    public static string Title(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name ?? string.Empty;
        }

        foreach (var c in name)
        {
            if (char.IsUpper(c))
            {
                return name;
            }
        }

        var result = new StringBuilder(name.Length);
        var wordStart = 0;
        var first = true;
        for (var i = 0; i <= name.Length; i++)
        {
            if (i < name.Length && name[i] != ' ')
            {
                continue;
            }

            if (i > wordStart)
            {
                var word = name.AsSpan(wordStart, i - wordStart);
                if (!first && SmallWords.Contains(word.ToString()))
                {
                    result.Append(word);
                }
                else
                {
                    AppendCapitalised(result, word);
                }

                first = false;
            }

            if (i < name.Length)
            {
                result.Append(' ');
            }

            wordStart = i + 1;
        }

        return result.ToString();
    }

    /// <summary>The first letter of the word upper-cased, skipping any leading non-letters ("'tis" becomes "'Tis").</summary>
    private static void AppendCapitalised(StringBuilder result, ReadOnlySpan<char> word)
    {
        for (var i = 0; i < word.Length; i++)
        {
            if (char.IsLetter(word[i]))
            {
                result.Append(word[..i]);
                result.Append(char.ToUpperInvariant(word[i]));
                result.Append(word[(i + 1)..]);
                return;
            }
        }

        result.Append(word);
    }
}
