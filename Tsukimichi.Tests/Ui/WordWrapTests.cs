using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Word wrapping, never letters (feature plan v4 L2, UI audit §4), measured with the advance widths of Dalamud's
/// default UI font at 16 px (<see cref="FontAdvances"/>, the fixture the layout tests use).
/// </summary>
public class WordWrapTests
{
    private const float Px = LayoutBudgets.BodyFontPx;
    private static readonly MeasureText Measure = FontAdvances.At(Px);

    private static List<WrapLine> Wrap(string text, float width)
    {
        var lines = new List<WrapLine>();
        WordWrap.Break(text, width, Measure, lines);
        return lines;
    }

    private static string[] Texts(string text, float width) =>
        Wrap(text, width).Select(l => text.Substring(l.Start, l.Length)).ToArray();

    [Fact]
    public void A_text_that_fits_is_one_line()
    {
        var lines = Wrap("Requirements", 300f);
        var line = Assert.Single(lines);
        Assert.Equal(0, line.Start);
        Assert.Equal("Requirements".Length, line.Length);
        Assert.False(line.Cut);
        Assert.Equal(FontAdvances.Width("Requirements", Px), line.Width, 2);
    }

    [Fact]
    public void Lines_break_between_words_and_never_inside_one()
    {
        const string text = "Complete the quest Lady of the Vortex before you speak to Cid in Gridania";
        var lines = Texts(text, 180f);
        Assert.True(lines.Length > 2);
        Assert.Equal(text.Split(' '), lines.SelectMany(static l => l.Split(' ')).ToArray());
        foreach (var line in Wrap(text, 180f))
        {
            Assert.True(line.Width <= 180.5f, $"line {line} is wider than 180");
        }
    }

    [Fact]
    public void A_line_takes_as_many_words_as_fit()
    {
        // Greedy: each line but the last could not have taken the next word.
        const string text = "one two three four five six seven eight nine ten eleven twelve";
        var width = 120f;
        var lines = Wrap(text, width);
        for (var i = 0; i < lines.Count - 1; i++)
        {
            var next = lines[i + 1];
            var nextWord = text.Substring(next.Start, text.IndexOf(' ', next.Start) is var sp and >= 0 ? sp - next.Start : text.Length - next.Start);
            var with = lines[i].Width + FontAdvances.Width(" ", Px) + FontAdvances.Width(nextWord, Px);
            Assert.True(with > width, $"line {i} could have taken \"{nextWord}\"");
        }
    }

    [Fact]
    public void A_word_wider_than_the_line_is_cut_on_a_line_of_its_own()
    {
        const string text = "See Chocobokoenigsberggesellschaft today";
        var lines = Wrap(text, 120f);
        Assert.Equal(3, lines.Count);
        Assert.Equal("See", text.Substring(lines[0].Start, lines[0].Length));
        Assert.False(lines[0].Cut);
        Assert.Equal("Chocobokoenigsberggesellschaft", text.Substring(lines[1].Start, lines[1].Length));
        Assert.True(lines[1].Cut);
        Assert.True(lines[1].Width > 120f);
        Assert.Equal("today", text.Substring(lines[2].Start, lines[2].Length));
        Assert.False(lines[2].Cut);
    }

    [Fact]
    public void A_hyphen_or_slash_inside_a_word_is_a_break()
    {
        Assert.Equal(new[] { "Allied-", "Societies" }, Texts("Allied-Societies", 90f));
        Assert.Equal(new[] { "Dungeon/", "Trial" }, Texts("Dungeon/Trial", 80f));

        // A leading hyphen or one at the end of the text is not a break of its own.
        Assert.Equal(new[] { "-5" }, Texts("-5", 5f));
    }

    [Fact]
    public void Newlines_are_hard_breaks_and_a_blank_line_is_kept()
    {
        Assert.Equal(new[] { "First", "", "Third" }, Texts("First\n\nThird", 500f));
        Assert.Equal(new[] { "First" }, Texts("First\n", 500f));
        Assert.Equal(new[] { "First", "Second" }, Texts("First\r\nSecond", 500f));
    }

    [Fact]
    public void Spaces_at_the_start_and_end_of_a_line_are_dropped()
    {
        Assert.Equal(new[] { "indented" }, Texts("   indented   ", 500f));
        var lines = Texts("alpha beta gamma", FontAdvances.Width("alpha beta", Px) + 1f);
        Assert.Equal(new[] { "alpha beta", "gamma" }, lines);
    }

    [Fact]
    public void Japanese_breaks_between_characters()
    {
        const string text = "エオルゼアの冒険者になって世界を救う";
        var lines = Texts(text, 5f * Px);
        Assert.True(lines.Length >= 3);
        Assert.Equal(text, string.Concat(lines));
        Assert.All(Wrap(text, 5f * Px), static l => Assert.False(l.Cut));
        Assert.All(lines, static l => Assert.True(l.Length <= 5));
    }

    [Fact]
    public void Closing_punctuation_and_small_kana_never_start_a_line_and_opening_brackets_never_end_one()
    {
        // Four ems per line: "ちょっと" must not leave "ょ" at a line start, "。" and "」" stay with the word before.
        const string text = "今日はちょっと「冒険」に出る。明日も。";
        foreach (var width in new[] { 3f, 4f, 5f, 6f })
        {
            var lines = Texts(text, width * Px);
            Assert.Equal(text, string.Concat(lines));
            for (var i = 0; i < lines.Length; i++)
            {
                Assert.False(i > 0 && WordWrap.NoLineStart(lines[i][0]), $"line \"{lines[i]}\" starts with a closing mark at {width} em");
                Assert.False(WordWrap.NoLineEnd(lines[i][^1]), $"line \"{lines[i]}\" ends with an opening bracket at {width} em");
            }
        }
    }

    [Fact]
    public void Latin_and_Japanese_mixed_break_at_the_script_change()
    {
        var width = MathF.Max(FontAdvances.Width("Lv50の", Px), FontAdvances.Width("クエスト", Px)) + 1f;
        Assert.True(FontAdvances.Width("Lv50のク", Px) > width);
        var lines = Texts("Lv50のクエスト", width);
        Assert.Equal(new[] { "Lv50の", "クエスト" }, lines);
    }

    [Fact]
    public void Hangul_wraps_by_word()
    {
        const string text = "퀘스트 목록을 확인하세요";
        var lines = Texts(text, FontAdvances.Width("퀘스트 목록을", Px) + 1f);
        Assert.Equal(new[] { "퀘스트 목록을", "확인하세요" }, lines);
    }

    [Fact]
    public void An_unreadable_width_does_not_wrap_and_no_width_cuts_every_word()
    {
        Assert.Equal(new[] { "one two three" }, Texts("one two three", float.NaN));
        var cut = Wrap("one two", 0f);
        Assert.Equal(2, cut.Count);
        Assert.All(cut, static l => Assert.True(l.Cut));
        Assert.Empty(Wrap(string.Empty, 100f));
    }

    [Fact]
    public void Each_word_is_measured_once()
    {
        var calls = 0;
        MeasureText counting = text =>
        {
            calls++;
            return FontAdvances.Width(text, Px);
        };

        const string text = "one two three four five six seven eight nine ten";
        WordWrap.Break(text, 60f, counting, new List<WrapLine>());

        // A word and the space after it: at most two measurements per word, however many lines.
        Assert.True(calls <= 2 * 10, $"{calls} measurements for 10 words");
    }

    [Theory]
    [InlineData("", 260f)]
    [InlineData("", 180f)]
    [InlineData("de", 260f)]
    [InlineData("fr", 260f)]
    [InlineData("ja", 260f)]
    [InlineData("ja", 120f)]
    public void Every_shipped_string_wraps_within_its_width_without_splitting_a_word(string language, float width)
    {
        var lines = new List<WrapLine>();
        foreach (var (key, value) in ResxFiles.Load(language))
        {
            WordWrap.Break(value, width, Measure, lines);
            foreach (var line in lines)
            {
                var text = value.AsSpan(line.Start, line.Length);
                Assert.True(line.Cut || line.Width <= width + 0.5f, $"{language}/{key}: line \"{text}\" is {line.Width:0} px of {width}");
                Assert.True(line.Length == 0 || !char.IsWhiteSpace(text[0]), $"{language}/{key}: line \"{text}\" starts with a space");
                Assert.True(line.Length == 0 || !char.IsWhiteSpace(text[^1]), $"{language}/{key}: line \"{text}\" ends with a space");
                if (line.Cut)
                {
                    Assert.True(text.IndexOfAny(' ', '\n') < 0, $"{language}/{key}: a cut line \"{text}\" holds more than one word");
                }

                // A line ends where a word ends: at the text's end, before whitespace, after a hyphen or slash, or
                // next to a CJK character.
                var end = line.Start + line.Length;
                if (end < value.Length && line.Length > 0)
                {
                    var before = value[end - 1];
                    var after = value[end];
                    Assert.True(
                        char.IsWhiteSpace(after) || before is '-' or '/' or '‐' or '–' or '—' || WordWrap.IsCjk(before) || WordWrap.IsCjk(after) || WordWrap.NoLineStart(before),
                        $"{language}/{key}: a line ends inside a word at \"{text}|{value.AsSpan(end, Math.Min(6, value.Length - end))}\"");
                }
            }
        }
    }
}
