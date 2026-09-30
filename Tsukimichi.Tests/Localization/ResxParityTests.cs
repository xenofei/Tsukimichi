using System.Globalization;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// The resource files agree with English (V2-19): no translation carries a key English lacks, every translated format
/// string keeps English's placeholders (composite items, printf specifiers, ImGui ids) and still formats, the machine
/// keys hold what code can read, and the drafts say they are drafts. A key a translation lacks is not a failure (drafts
/// may lag; the plugin reads it in English); <see cref="Missing_translations_are_listed"/> writes them to the test
/// output instead.
/// </summary>
public class ResxParityTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Languages() => ResxFiles.Translations.Select(static l => new object[] { l });

    [Fact]
    public void English_is_the_source_and_has_every_key_once()
    {
        var english = ResxFiles.Load(string.Empty);
        Assert.True(english.Count > 1000, $"expected the whole UI in Strings.resx, found {english.Count} keys");
        Assert.Equal("source", english["Meta.TranslationStatus"]);
        Assert.All(english, static e => Assert.False(string.IsNullOrWhiteSpace(e.Key)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_language_file_exists(string language)
    {
        Assert.True(File.Exists(ResxFiles.PathFor(language)), $"{ResxFiles.PathFor(language)} is missing");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_language_file_has_keys_English_lacks(string language)
    {
        var english = ResxFiles.Load(string.Empty);
        var extra = ResxFiles.Load(language).Keys.Where(k => !english.ContainsKey(k)).ToList();
        Assert.True(extra.Count == 0, $"Strings.{language}.resx has keys English lacks (renamed or removed there?):{Environment.NewLine}{string.Join(Environment.NewLine, extra)}");
    }

    /// <summary>A warning, not a failure: lists what each draft still reads in English.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void Missing_translations_are_listed(string language)
    {
        var english = ResxFiles.Load(string.Empty);
        var translated = ResxFiles.Load(language);
        var missing = english.Keys.Where(k => !translated.ContainsKey(k)).ToList();
        var coverage = 100.0 * (english.Count - missing.Count) / english.Count;
        output.WriteLine($"{language}: {english.Count - missing.Count} of {english.Count} keys translated ({coverage:0.0}%)");
        foreach (var key in missing)
        {
            output.WriteLine($"  missing: {key}");
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_format_string_keeps_its_placeholders(string language)
    {
        var english = ResxFiles.Load(string.Empty);
        var offenders = new List<string>();
        foreach (var (key, value) in ResxFiles.Load(language))
        {
            if (!english.TryGetValue(key, out var source))
            {
                continue;
            }

            var expected = ResxFiles.Placeholders(source);
            var actual = ResxFiles.Placeholders(value);
            if (!expected.SequenceEqual(actual))
            {
                offenders.Add($"{key}: English [{string.Join(' ', expected)}], {language} [{string.Join(' ', actual)}]");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ja")]
    [InlineData("de")]
    [InlineData("fr")]
    public void Every_composite_format_string_formats(string language)
    {
        // A stray "{" or "}" in a translation throws in string.Format at draw time; ten arguments cover every format.
        var args = Enumerable.Range(0, 10).Select(static i => (object)(i + 1)).ToArray();
        var offenders = new List<string>();
        foreach (var (key, value) in ResxFiles.Load(language))
        {
            if (!value.Contains('{', StringComparison.Ordinal) && !value.Contains('}', StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                _ = string.Format(CultureInfo.InvariantCulture, value, args);
            }
            catch (FormatException ex)
            {
                offenders.Add($"{key}: {ex.Message} in \"{value}\"");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Machine_keys_hold_what_code_reads(string language)
    {
        var values = ResxFiles.Load(language);
        if (values.TryGetValue("Core.Culture", out var culture))
        {
            var info = CultureInfo.GetCultureInfo(culture);
            Assert.StartsWith(language, info.Name, StringComparison.OrdinalIgnoreCase);
        }

        var date = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);
        foreach (var key in new[] { "Core.Seasonal.DateFormat", "Core.Seasonal.DateYearFormat" })
        {
            if (values.TryGetValue(key, out var format))
            {
                var text = date.ToString(format, CultureInfo.GetCultureInfo(culture ?? "en-US"));
                Assert.Contains("28", text, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Drafts_say_they_are_drafts(string language)
    {
        var values = ResxFiles.Load(language);
        Assert.Equal("draft", values["Meta.TranslationStatus"]);
        var header = File.ReadAllText(ResxFiles.PathFor(language));
        Assert.Contains("DRAFT", header, StringComparison.Ordinal);
        Assert.Contains("CONTRIBUTING.md", header, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Window_ids_are_never_translated(string language)
    {
        // "Tsukimichi Help###TsukimichiHelp": ImGui keeps a window's position and size by the part after "###".
        var english = ResxFiles.Load(string.Empty);
        foreach (var (key, value) in ResxFiles.Load(language))
        {
            if (english.TryGetValue(key, out var source) && source.Contains("###", StringComparison.Ordinal))
            {
                Assert.EndsWith(source[source.IndexOf("###", StringComparison.Ordinal)..], value, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Every_english_key_the_plugin_reads_exists()
    {
        // Loc.Get("…") and Loc.Array("…") in the plugin's sources name keys the English file must hold.
        var english = ResxFiles.Load(string.Empty);
        var call = new Regex("Loc\\.(Get|Array)\\(\"([^\"]+)\"\\)", RegexOptions.Compiled);
        var missing = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in call.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[2].Value;
                var found = match.Groups[1].Value == "Array" ? english.ContainsKey(key + ".0") : english.ContainsKey(key);
                if (!found)
                {
                    missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }
}
