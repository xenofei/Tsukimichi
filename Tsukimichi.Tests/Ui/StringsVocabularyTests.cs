using System.Text.RegularExpressions;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// String lint for the plugin's <c>Tsukimichi/Ui/Strings*.cs</c> (feature plan v3 T23): no user-facing literal spells a
/// state or a renamed label the retired way. The test project references Core and GameData only, so it reads the
/// plugin sources from the repository the test assembly was built in; the display names themselves are unit-tested in
/// <see cref="StateNamesTests"/>. The moon-phase names live in <c>StateNames.GlyphSubtitle</c> (Core) and in
/// docs/glossary.md, which this lint does not read.
/// </summary>
public class StringsVocabularyTests
{
    /// <summary>Spellings that must not appear inside any literal, in any letter case.</summary>
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
    /// Whole literals that contain a retired fragment on purpose. "veiled" is the item hint's word for an obtained state
    /// that cannot be read for a stored character, the one meaning docs/glossary.md keeps for it (never a quest state).
    /// </summary>
    private static readonly HashSet<string> Sanctioned = new(StringComparer.Ordinal)
    {
        "veiled",
    };

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

    public static IEnumerable<object[]> StringsFiles() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "Tsukimichi", "Ui"), "Strings*.cs")
            .OrderBy(static f => f, StringComparer.Ordinal)
            .Select(static f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void The_strings_files_are_found()
    {
        Assert.NotEmpty(StringsFiles());
    }

    [Theory]
    [MemberData(nameof(StringsFiles))]
    public void No_literal_spells_a_state_or_label_the_retired_way(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), "Tsukimichi", "Ui", fileName);
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
                var text = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (Sanctioned.Contains(text))
                {
                    continue;
                }

                foreach (var fragment in RetiredFragments)
                {
                    // Case-insensitive: "feature quests" in a sentence is as retired as "Feature quests" as a label.
                    if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add($"{fileName}:{lineNumber} contains \"{fragment}\": {text}");
                    }
                }

                foreach (var label in RetiredLabels)
                {
                    if (string.Equals(text, label, StringComparison.Ordinal))
                    {
                        offenders.Add($"{fileName}:{lineNumber} is the retired label \"{label}\"");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_state_name_table_remains_in_the_plugin()
    {
        // The plugin's Strings delegate to Core's StateNames; a second table would drift.
        foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot(), "Tsukimichi", "Ui"), "Strings*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("QuestState.Foreclosed =>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("QuestState.Unknown =>", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Walks up from the test assembly to the folder that holds the solution file.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Tsukimichi.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Tsukimichi.sln not found above " + AppContext.BaseDirectory);
    }
}
