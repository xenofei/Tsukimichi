using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Text;

/// <summary>
/// Splits journal text and search queries into the words the journal index (<see cref="JournalTextIndex"/>) stores:
/// runs of letters and digits, lowercased, with accents folded ("Éorzéa" and "eorzea" are one word) and an apostrophe
/// kept inside a word ("y'shtola", "ul'dah"). Words shorter than <see cref="MinWordLength"/> are dropped, so "a", "of"
/// and "to" never reach the index. Scripts written without spaces (Japanese, Chinese, Korean) are cut into overlapping
/// two-character pairs, which a query of the same script is cut into too, so a phrase matches wherever its pairs all
/// appear. Pure and allocation-light; the same rules run at index time and at query time.
/// </summary>
public static class JournalTokenizer
{
    /// <summary>Shortest word kept for a spaced script.</summary>
    public const int MinWordLength = 3;

    /// <summary>Longest word kept; longer runs (a URL, a line of digits) are cut here.</summary>
    public const int MaxWordLength = 32;

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

            FlushCjk(cjk, into);
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
                continue;
            }

            // An apostrophe joins two letters of one word: Y'shtola, Ul'dah, G'raha. Anything else ends the word.
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
        if (word.Length >= MinWordLength)
        {
            var folded = Fold(word.ToString());
            if (folded.Length >= MinWordLength)
            {
                into.Add(folded.Length > MaxWordLength ? folded[..MaxWordLength] : folded);
            }
        }

        word.Clear();
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

    /// <summary>Lowercase, accents removed ("É" to "e"), compatibility forms folded (full-width letters to ASCII).</summary>
    public static string Fold(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (System.Text.Ascii.IsValid(word))
        {
            return word.ToLowerInvariant();
        }

        var decomposed = word.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsApostrophe(char c) => c is '\'' or '’' or 'ʼ';

    /// <summary>Han, Hiragana, Katakana (with the prolonged sound mark) and Hangul: the scripts the game writes without spaces.</summary>
    private static bool IsCjk(char c) =>
        c is (>= '぀' and <= 'ヿ')   // Hiragana, Katakana
            or (>= '㐀' and <= '䶿') // CJK extension A
            or (>= '一' and <= '鿿') // CJK unified ideographs
            or (>= '가' and <= '힯') // Hangul syllables
            or (>= '豈' and <= '﫿') // CJK compatibility ideographs
            or (>= 'ｦ' and <= 'ﾟ'); // half-width Katakana
}
