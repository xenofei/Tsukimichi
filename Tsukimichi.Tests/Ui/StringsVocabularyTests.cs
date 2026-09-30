using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// String lint for the plugin's English text (feature plan v3 T23): no user-facing string spells a state or a renamed
/// label the retired way. Since V2-19 the text lives in <c>Tsukimichi/Localization/Strings.resx</c> (English, the
/// source every translation follows), so the lint reads its values; the few literals left in
/// <c>Tsukimichi/Ui/Strings*.cs</c> (separators, markers, ImGui ids, resource keys) are linted too, keys excepted. The
/// test project references Core and GameData only, so it reads the files from the repository the test assembly was
/// built in; the display names themselves are unit-tested in <see cref="StateNamesTests"/>. The moon-phase names live
/// in <c>StateNames.GlyphSubtitle</c> (Core) and in docs/glossary.md, which this lint does not read. Translations are
/// not linted: the retired words are English ones.
/// </summary>
public class StringsVocabularyTests
{
    /// <summary>Spellings that must not appear inside any string, in any letter case.</summary>
    private static readonly string[] RetiredFragments =
    [
        "Foreclosed",
        "Veiled",
        "Done this cycle",
        "Done cycle",
        "Feature Unlocks",
        "Feature quests",
        "Feature quest",
        "Next step",
        "Level band",
        "Around my level",
        "Tribal",
        "Unlisted",
    ];

    /// <summary>
    /// Whole strings that contain a retired fragment on purpose. "veiled" is the item hint's word for an obtained state
    /// that cannot be read for a stored character, the one meaning docs/glossary.md keeps for it (never a quest state).
    /// </summary>
    private static readonly HashSet<string> Sanctioned = new(StringComparer.Ordinal)
    {
        "veiled",
    };

    /// <summary>
    /// Core's own names, mirrored into the resx for translators and owned by <c>StateNames</c>: the moon-phase names
    /// ("eclipsed", "veiled") and the glossary's fallback "Done this cycle".
    /// </summary>
    private static bool IsSanctionedKey(string key) =>
        key.StartsWith("Core.Glyph.", StringComparison.Ordinal) || key == "Core.State.DoneThisCycle";

    /// <summary>Spellings that are retired as a whole label but may still open a longer sentence about something else.</summary>
    private static readonly string[] RetiredLabels =
    [
        "Unknown",
        "Accepted",
        "Other job",
        "Presets",
        "Features",
    ];

    // A regular or verbatim C# string literal; group 1 is its content.
    private static readonly Regex Literal = new("@\"((?:[^\"]|\"\")*)\"|\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    // A literal that is a resource key: Loc.Get("…") or Loc.Array("…").
    private static readonly Regex KeyCall = new("Loc\\.(?:Get|Array)\\(\\s*$", RegexOptions.Compiled);

    public static IEnumerable<object[]> StringsFiles() =>
        Directory.GetFiles(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui"), "Strings*.cs")
            .OrderBy(static f => f, StringComparer.Ordinal)
            .Select(static f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void The_strings_files_are_found()
    {
        Assert.NotEmpty(StringsFiles());
        Assert.True(ResxFiles.Load(string.Empty).Count > 1000, "expected the English resource file");
    }

    [Fact]
    public void No_English_string_spells_a_state_or_label_the_retired_way()
    {
        var offenders = new List<string>();
        foreach (var (key, value) in ResxFiles.Load(string.Empty))
        {
            if (IsSanctionedKey(key))
            {
                continue;
            }

            Check(value, $"Strings.resx {key}", offenders);
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [MemberData(nameof(StringsFiles))]
    public void No_literal_spells_a_state_or_label_the_retired_way(string fileName)
    {
        var path = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", fileName);
        var offenders = new List<string>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in Literal.Matches(line))
            {
                if (KeyCall.IsMatch(line[..match.Index]))
                {
                    continue;
                }

                var text = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                Check(text, $"{fileName}:{lineNumber}", offenders);
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_state_name_table_remains_in_the_plugin()
    {
        // The plugin's Strings delegate to Core's StateNames; a second table would drift.
        foreach (var file in Directory.GetFiles(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui"), "Strings*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("QuestState.Foreclosed =>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("QuestState.Unknown =>", source, StringComparison.Ordinal);
        }
    }

    private static void Check(string text, string where, List<string> offenders)
    {
        if (Sanctioned.Contains(text))
        {
            return;
        }

        foreach (var fragment in RetiredFragments)
        {
            // Case-insensitive: "feature quests" in a sentence is as retired as "Feature quests" as a label.
            if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{where} contains \"{fragment}\": {text}");
            }
        }

        foreach (var label in RetiredLabels)
        {
            if (string.Equals(text, label, StringComparison.Ordinal))
            {
                offenders.Add($"{where} is the retired label \"{label}\"");
            }
        }
    }
}
