namespace Tsukimichi.Core.Ui;

/// <summary>Measures a run of text in pixels (the plugin passes the font's advance widths).</summary>
public delegate float MeasureText(ReadOnlySpan<char> text);

/// <summary>One line of wrapped text.</summary>
/// <param name="Start">Index of the line's first character in the text.</param>
/// <param name="Length">Characters on the line, trailing spaces left out.</param>
/// <param name="Width">The line's width, as measured.</param>
/// <param name="Cut">
/// The line is a single word wider than the wrap width: it is drawn ellipsised in the width, the full text going in
/// a tooltip.
/// </param>
public readonly record struct WrapLine(int Start, int Length, float Width, bool Cut);

/// <summary>
/// Where to break a text so it fits a width (feature plan v4 L2, UI audit §4 "word wrap, never letters"): lines break
/// between words, after a hyphen or a slash inside one, and at every newline. A single word wider than the width
/// gets a line of its own and is marked cut (drawn with an ellipsis), never broken between letters. Chinese and
/// Japanese have no spaces, so each of their characters is a break opportunity of its own, with the usual courtesies:
/// closing punctuation and small kana never start a line, and an opening bracket never ends one. Hangul is written
/// with spaces and wraps by word. Widths are summed per word, so a text is measured once per word, not once per
/// candidate line. The plugin's <c>TextFlow</c> caches the lines per text and width, so nothing is measured or
/// allocated on the frames in between.
/// </summary>
public static class WordWrap
{
    /// <summary>A little slack so a line measured exactly at the width is not pushed to the next one.</summary>
    private const float Slack = 0.5f;

    /// <summary>
    /// Breaks <paramref name="text"/> into lines no wider than <paramref name="width"/> and writes them to
    /// <paramref name="lines"/> (cleared first). A width that is not a number means no limit; a width of 0 or less
    /// puts every word on its own line, cut. A newline with nothing before it on its line gives a zero-length line; a
    /// newline at the very end adds nothing.
    /// </summary>
    /// <returns>The number of lines.</returns>
    public static int Break(ReadOnlySpan<char> text, float width, MeasureText measure, List<WrapLine> lines)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(lines);
        lines.Clear();
        var limit = float.IsNaN(width) ? float.PositiveInfinity : MathF.Max(0f, width);

        var lineStart = -1;
        var lineEnd = 0;
        var lineWidth = 0f;
        var pendingSpace = 0f;
        var hardLineHasLines = false;
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                if (lineStart >= 0)
                {
                    Emit(lines, ref lineStart, lineEnd, lineWidth);
                }
                else if (!hardLineHasLines)
                {
                    // A newline with nothing before it on its line: an empty line.
                    lines.Add(new WrapLine(i, 0, 0f, Cut: false));
                }

                hardLineHasLines = false;
                pendingSpace = 0f;
                i++;
                continue;
            }

            if (char.IsWhiteSpace(text[i]) && lineStart < 0)
            {
                // Spaces at the start of a wrapped line are dropped; at the start of the text or after a newline too.
                i++;
                continue;
            }

            var contentEnd = TokenEnd(text, i);
            var end = contentEnd;
            while (end < text.Length && text[end] != '\n' && char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            var contentWidth = measure(text[i..contentEnd]);
            var spaceWidth = end > contentEnd ? measure(text[contentEnd..end]) : 0f;

            if (lineStart >= 0 && lineWidth + pendingSpace + contentWidth > limit + Slack)
            {
                Emit(lines, ref lineStart, lineEnd, lineWidth);
            }

            hardLineHasLines = true;
            if (lineStart < 0)
            {
                if (contentWidth > limit + Slack)
                {
                    // One word wider than the line: a line of its own, drawn with an ellipsis.
                    lines.Add(new WrapLine(i, contentEnd - i, contentWidth, Cut: true));
                    pendingSpace = 0f;
                    i = end;
                    continue;
                }

                lineStart = i;
                lineWidth = contentWidth;
            }
            else
            {
                lineWidth += pendingSpace + contentWidth;
            }

            lineEnd = contentEnd;
            pendingSpace = spaceWidth;
            i = end;
        }

        if (lineStart >= 0)
        {
            Emit(lines, ref lineStart, lineEnd, lineWidth);
        }

        return lines.Count;
    }

    /// <summary>Whether <paramref name="c"/> is written without spaces between words (Chinese characters, kana, CJK punctuation).</summary>
    public static bool IsCjk(char c) =>
        (c >= '⺀' && c <= '鿿') || (c >= '豈' && c <= '﫿') || (c >= '︰' && c <= '﹏') || (c >= '＀' && c <= '￯');

    /// <summary>Characters that never start a line: closing punctuation, the prolonged sound mark, small kana.</summary>
    public static bool NoLineStart(char c) => NoStart.Contains(c, StringComparison.Ordinal);

    /// <summary>Characters that never end a line: opening brackets.</summary>
    public static bool NoLineEnd(char c) => NoEnd.Contains(c, StringComparison.Ordinal);

    private const string NoStart = "、。，．・：；？！ー）」』］｝〕〉》】〙〗ゝゞヽヾ々ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ…‥,.:;?!)]}%";
    private const string NoEnd = "（「『［｛〔〈《【〘〖([{";

    /// <summary>
    /// The end of the unbreakable run that starts at <paramref name="start"/> (not whitespace): a CJK character with
    /// any closing punctuation after it (and any opening bracket before it), or a word up to the next space, newline
    /// or CJK character, or just after a hyphen, dash or slash inside it.
    /// </summary>
    private static int TokenEnd(ReadOnlySpan<char> text, int start)
    {
        var i = start;

        // Opening brackets hold on to what follows them.
        while (i < text.Length && NoLineEnd(text[i]))
        {
            i++;
        }

        if (i < text.Length && IsCjk(text[i]))
        {
            i++;
        }
        else
        {
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsCjk(text[i]))
            {
                var c = text[i];
                i++;
                if (IsBreakAfter(c) && i - 1 > start && i < text.Length && char.IsLetterOrDigit(text[i]))
                {
                    break;
                }
            }
        }

        // Closing punctuation stays with what it closes.
        while (i < text.Length && NoLineStart(text[i]))
        {
            i++;
        }

        return i > start ? i : start + 1;
    }

    private static bool IsBreakAfter(char c) => c is '-' or '/' or '‐' or '–' or '—';

    private static void Emit(List<WrapLine> lines, ref int lineStart, int lineEnd, float lineWidth)
    {
        lines.Add(new WrapLine(lineStart, lineEnd - lineStart, lineWidth, Cut: false));
        lineStart = -1;
    }
}
