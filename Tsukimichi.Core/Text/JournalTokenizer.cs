using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Text;

/// <summary>
/// Splits journal text and search queries into the words the journal index (<see cref="JournalTextIndex"/>) stores:
/// runs of letters and digits, lowercased, with accents and ligatures folded ("Éorzéa" is "eorzea", "cœur" is "coeur",
/// "straße" is "strasse") and an apostrophe kept inside a word ("y'shtola", "ul'dah"). A word with an apostrophe is
/// also stored without it ("uldah"), and a French elision is stored with and without its article ("d'ishgard" and
/// "ishgard"). Words shorter than <see cref="MinWordLength"/> are dropped, so "a", "of" and "to" never reach the index.
/// Scripts written without spaces (Japanese, Chinese, Korean) are cut into overlapping two-character pairs, which a
/// query of the same script is cut into too, so a phrase matches wherever its pairs all appear; the katakana middle
/// dot inside a name ("ヤ・シュトラ") is skipped, so the name reads as one run. Pure and allocation-light; the same rules
/// run at index time and at query time.
/// </summary>
public static class JournalTokenizer
{
    /// <summary>Shortest word kept for a spaced script.</summary>
    public const int MinWordLength = 3;

    /// <summary>Longest word kept; longer runs (a URL, a line of digits) are cut here.</summary>
    public const int MaxWordLength = 32;

    /// <summary>The French elided words longer than two letters ("jusqu'à", "lorsqu'il"); any one or two letters also count.</summary>
    private static readonly HashSet<string> LongElisions = new(StringComparer.Ordinal) { "jusqu", "lorsqu", "puisqu", "quoiqu", "presqu" };

    /// <summary>Adds every word of <paramref name="text"/> to <paramref name="into"/>.</summary>
    public static void AddWords(ReadOnlySpan<char> text, ISet<string> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        var word = new StringBuilder(MaxWordLength);
        var cjk = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsCjk(c))
            {
                Flush(word, into);
                cjk.Append(c);
                continue;
            }

            // The middle dot between the parts of a name (ヤ・シュトラ) joins them: the name is one run, so a query
            // typed with or without the dot cuts into the same pairs.
            if (IsCjkSeparator(c) && cjk.Length > 0 && i + 1 < text.Length && IsCjk(text[i + 1]))
            {
                continue;
            }

            FlushCjk(cjk, into);
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
                continue;
            }

            // An apostrophe joins two letters of one word: Y'shtola, Ul'dah, d'Ishgard. Anything else ends the word.
            if (IsApostrophe(c) && word.Length > 0 && i + 1 < text.Length && char.IsLetter(text[i + 1]))
            {
                word.Append('\'');
                continue;
            }

            Flush(word, into);
        }

        Flush(word, into);
        FlushCjk(cjk, into);
    }

    /// <summary>The words of <paramref name="text"/>, distinct, in no particular order.</summary>
    public static IReadOnlySet<string> Words(string? text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(text))
        {
            AddWords(text, set);
        }

        return set;
    }

    /// <summary>
    /// The terms of a search query, in the index's form: every one must match. Empty when nothing in the query is long
    /// enough to search the journal for (a lone quest id, "a", "of").
    /// </summary>
    public static IReadOnlyList<string> QueryTerms(string? query)
    {
        var set = Words(query);
        if (set.Count == 0)
        {
            return [];
        }

        var list = new List<string>(set);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>True for a word the tokenizer made from a script without spaces (a pair of CJK characters or a lone one).</summary>
    public static bool IsCjkWord(string word) => word.Length > 0 && IsCjk(word[0]);

    private static void Flush(StringBuilder word, ISet<string> into)
    {
        if (word.Length == 0)
        {
            return;
        }

        // A trailing apostrophe cannot happen (it needs a letter after it), so the word is complete as built.
        AddForms(Fold(word.ToString()), into);
        word.Clear();
    }

    /// <summary>
    /// Adds a folded word and, when it holds an apostrophe, the word without it ("ul'dah" also as "uldah") and, after
    /// an elided article or pronoun ("d'", "l'", "qu'", "jusqu'"), the rest on its own ("d'ishgard" also as "ishgard").
    /// </summary>
    private static void AddForms(string folded, ISet<string> into)
    {
        AddWord(folded, into);
        var apostrophe = folded.IndexOf('\'', StringComparison.Ordinal);
        if (apostrophe < 0)
        {
            return;
        }

        AddWord(folded.Replace("'", string.Empty, StringComparison.Ordinal), into);
        var head = folded[..apostrophe];
        if (head.Length <= 2 || LongElisions.Contains(head))
        {
            AddForms(folded[(apostrophe + 1)..], into);
        }
    }

    private static void AddWord(string folded, ISet<string> into)
    {
        if (folded.Length >= MinWordLength)
        {
            into.Add(folded.Length > MaxWordLength ? folded[..MaxWordLength] : folded);
        }
    }

    private static void FlushCjk(StringBuilder run, ISet<string> into)
    {
        if (run.Length == 0)
        {
            return;
        }

        if (run.Length == 1)
        {
            into.Add(run.ToString());
        }
        else
        {
            for (var i = 0; i + 1 < run.Length; i++)
            {
                into.Add(string.Concat(run[i].ToString(), run[i + 1].ToString()));
            }
        }

        run.Clear();
    }

    /// <summary>
    /// Lowercase, accents removed ("É" to "e"), compatibility forms folded (full-width letters to ASCII) and the
    /// ligatures and sharp s spelled out ("œ" to "oe", "æ" to "ae", "ß" to "ss").
    /// </summary>
    public static string Fold(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (System.Text.Ascii.IsValid(word))
        {
            return word.ToLowerInvariant();
        }

        var decomposed = word.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(decomposed.Length + 2);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            switch (char.ToLowerInvariant(c))
            {
                case 'œ':
                    sb.Append("oe");
                    break;
                case 'æ':
                    sb.Append("ae");
                    break;
                case 'ß' or 'ẞ':
                    sb.Append("ss");
                    break;
                case var lower:
                    sb.Append(lower);
                    break;
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsApostrophe(char c) => c is '\'' or '’' or 'ʼ';

    /// <summary>The katakana middle dot (・, and its half-width form) and the double hyphen (゠) between the parts of a name.</summary>
    private static bool IsCjkSeparator(char c) => c is '・' or '゠' or '･';

    /// <summary>Han, Hiragana, Katakana (with the prolonged sound mark) and Hangul: the scripts the game writes without spaces.</summary>
    private static bool IsCjk(char c) =>
        c is ((>= '぀' and <= 'ヿ') and not ('・' or '゠')) // Hiragana, Katakana, but not the name separators
            or (>= '㐀' and <= '䶿') // CJK extension A
            or (>= '一' and <= '鿿') // CJK unified ideographs
            or (>= '가' and <= '힯') // Hangul syllables
            or (>= '豈' and <= '﫿') // CJK compatibility ideographs
            or (>= 'ｦ' and <= 'ﾟ'); // half-width Katakana
}
