using System.Text.RegularExpressions;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// The translated strings ImGui reads as printf formats (a slider's or a drag's "%d days") keep English's specifiers
/// exactly, in order, and hold no other '%' unless written "%%": ImGui formats them with the value as a vararg, so a
/// translator's "100 %" or "%d %" would read garbage off the stack. Every other text ImGui draws literally
/// (TextUnformatted and friends), so only these keys matter. The keys are found in the plugin's sources, so a new
/// slider with a translated format is covered without touching this test.
/// </summary>
public class PrintfFormatTests
{
    /// <summary>The ImGui widgets that take a printf format after their label.</summary>
    private static readonly Regex FormattedWidget = new(
        @"ImGui\.(?:SliderInt\d?|SliderFloat\d?|SliderScalar|VSliderInt|VSliderFloat|DragInt\d?|DragFloat\d?|DragIntRange2|DragFloatRange2|DragScalar|InputFloat\d?|InputScalar)\(",
        RegexOptions.Compiled);

    private static readonly Regex StringsMember = new(@"Strings\.(?:\w+\.)*(\w+)", RegexOptions.Compiled);

    private static readonly Regex LocProperty = new(@"public static string (\w+) => Loc\.Get\(""([^""]+)""\)", RegexOptions.Compiled);

    /// <summary>
    /// A printf conversion: flags, width, precision, length and the conversion character. The space flag is left out
    /// on purpose: "100 % sans" must read as a stray '%', not as a "% s" conversion.
    /// </summary>
    private static readonly Regex Specifier = new(@"%[-+#0]*\d*(?:\.\d+)?(?:hh|h|ll|l|j|z|t|L)?[diuoxXfFeEgGaAcsp]", RegexOptions.Compiled);

    public static IEnumerable<object[]> AllLanguages() =>
        new[] { string.Empty }.Concat(ResxFiles.Translations).Select(static l => new object[] { l });

    [Fact]
    public void The_scan_finds_the_known_format_keys()
    {
        var keys = FormatKeys();
        // The filter drawer (1.14.0) fills "Stalled after" itself (a stepper, not a slider) and draws "to" between its level fields.
        foreach (var key in new[] { "LevelFormat", "WelcomeBackConfigDaysFormat", "WelcomeBackConfigOff" })
        {
            Assert.Contains(key, keys);
        }
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Printf_formats_keep_English_specifiers_and_no_stray_percent(string language)
    {
        var english = ResxFiles.Load(string.Empty);
        var values = ResxFiles.Load(language);
        var offenders = new List<string>();
        foreach (var key in FormatKeys())
        {
            Assert.True(english.ContainsKey(key), $"{key} is passed to ImGui as a format but Strings.resx lacks it");
            if (!values.TryGetValue(key, out var value))
            {
                continue;
            }

            var expected = Specifiers(english[key], out _);
            var actual = Specifiers(value, out var stray);
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                offenders.Add($"{key}: English [{string.Join(' ', expected)}], this file [{string.Join(' ', actual)}] in \"{value}\"");
            }

            if (stray)
            {
                offenders.Add($"{key}: a '%' that is not a specifier (write \"%%\" for a percent sign) in \"{value}\"");
            }
        }

        Assert.True(offenders.Count == 0, $"Strings{(language.Length == 0 ? string.Empty : "." + language)}.resx:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Theory]
    [InlineData("%d days", false, "%d")]
    [InlineData("Lv %d", false, "%d")]
    [InlineData("off", false)]
    [InlineData("100 %% sure, %d", false, "%d")]
    [InlineData("%d %", true, "%d")]
    [InlineData("100 % sans", true)]
    [InlineData("%.2f×", false, "%.2f")]
    public void Specifiers_are_read_like_printf(string value, bool stray, params string[] expected)
    {
        Assert.Equal(expected, Specifiers(value, out var foundStray));
        Assert.Equal(stray, foundStray);
    }

    /// <summary>
    /// The resx keys of every translated <c>Strings</c> member passed to a formatted ImGui widget after its label
    /// (constants such as <c>Strings.ScaleFormat</c> are code, not translations, and are left out).
    /// </summary>
    private static SortedSet<string> FormatKeys()
    {
        var root = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi");
        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        var keyOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in sources.Where(static f => Path.GetFileName(f).StartsWith("Strings", StringComparison.Ordinal)))
        {
            foreach (Match match in LocProperty.Matches(File.ReadAllText(file)))
            {
                keyOf.TryAdd(match.Groups[1].Value, match.Groups[2].Value);
            }
        }

        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (Match call in FormattedWidget.Matches(text))
            {
                // The label is the first argument; what follows it up to the end of the statement holds the format.
                var start = call.Index + call.Length;
                var end = text.IndexOf(';', start);
                var arguments = text[start..(end < 0 ? text.Length : end)];
                var comma = arguments.IndexOf(',', StringComparison.Ordinal);
                if (comma < 0)
                {
                    continue;
                }

                foreach (Match member in StringsMember.Matches(arguments[(comma + 1)..]))
                {
                    if (keyOf.TryGetValue(member.Groups[1].Value, out var key))
                    {
                        keys.Add(key);
                    }
                }
            }
        }

        return keys;
    }

    /// <summary>The printf specifiers of a value in order; <paramref name="stray"/> is true when a '%' is neither a specifier nor "%%".</summary>
    private static List<string> Specifiers(string value, out bool stray)
    {
        var found = new List<string>();
        stray = false;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%')
            {
                continue;
            }

            if (i + 1 < value.Length && value[i + 1] == '%')
            {
                i++;
                continue;
            }

            var match = Specifier.Match(value, i);
            if (match.Success && match.Index == i)
            {
                found.Add(match.Value);
                i += match.Length - 1;
            }
            else
            {
                stray = true;
            }
        }

        return found;
    }
}
