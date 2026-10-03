using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Settings rows' copy rules (feature plan v6 U7, owner point 5 "words meld together"): every row drawn with the
/// row helpers in <c>Tsukimichi/Ui/ConfigWindow*.cs</c> (<c>Setting</c>, <c>Toggle</c>, <c>ToggleSetting</c>,
/// <c>Choice</c>, <c>ButtonRow</c>, <c>Note</c>) has an English label of at most 40 characters and a hint of at most
/// 110, keeps the glossary (Decoration, not Flair; Moon colours, not Glyph palette; no hooks, catalog or poll), and
/// Settings draws no bare checkbox or radio button. The sources are read from the repository, the labels through the
/// <c>Strings</c> properties to their resource keys.
/// </summary>
public class SettingsCopyTests
{
    // A row helper called with a Strings label and a Strings (or null) hint as its first two arguments.
    private static readonly Regex RowCall = new(@"\b(?:Setting|Toggle|ToggleSetting|Choice|ButtonRow|Note)\(\s*Strings\.(\w+)\s*,\s*(?:Strings\.(\w+)|null)\b", RegexOptions.Compiled);

    // A Strings property that reads one resource key.
    private static readonly Regex Property = new(@"public static string (\w+) => Loc\.Get\(""([^""]+)""\)", RegexOptions.Compiled);

    /// <summary>Words the glossary retired from Settings' labels and hints, in any letter case.</summary>
    private static readonly string[] Retired = ["Flair", "Glyph palette", "hook", "catalog", "poll"];

    private static string UiDir => Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui");

    private static IEnumerable<string> ConfigFiles() => Directory.GetFiles(UiDir, "ConfigWindow*.cs").OrderBy(static f => f, StringComparer.Ordinal);

    private static Dictionary<string, string> Keys()
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(UiDir, "Strings*.cs"))
        {
            foreach (Match match in Property.Matches(File.ReadAllText(file)))
            {
                keys[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }

        return keys;
    }

    /// <summary>Every (label, hint) property pair the row helpers are called with.</summary>
    private static List<(string File, string Label, string? Hint)> Rows()
    {
        var rows = new List<(string, string, string?)>();
        foreach (var file in ConfigFiles())
        {
            foreach (Match match in RowCall.Matches(File.ReadAllText(file)))
            {
                rows.Add((Path.GetFileName(file), match.Groups[1].Value, match.Groups[2].Success ? match.Groups[2].Value : null));
            }
        }

        return rows;
    }

    [Fact]
    public void The_rows_are_found()
    {
        Assert.True(Rows().Count > 80, $"expected the Settings rows, found {Rows().Count}");
    }

    [Fact]
    public void Labels_keep_to_40_characters_and_hints_to_110()
    {
        var keys = Keys();
        var english = ResxFiles.Load(string.Empty);
        var offenders = new List<string>();
        foreach (var (file, label, hint) in Rows())
        {
            var labelText = english[keys[label]];
            if (labelText.Length > SettingsCopy.MaxLabel)
            {
                offenders.Add($"{file}: label {label} is {labelText.Length} characters: \"{labelText}\"");
            }

            if (hint is not null && english[keys[hint]] is { } hintText && hintText.Length > SettingsCopy.MaxHint)
            {
                offenders.Add($"{file}: hint {hint} is {hintText.Length} characters: \"{hintText}\"");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Labels_and_hints_keep_the_glossary()
    {
        var keys = Keys();
        var english = ResxFiles.Load(string.Empty);
        var offenders = new List<string>();
        foreach (var (file, label, hint) in Rows())
        {
            foreach (var property in hint is null ? [label] : new[] { label, hint })
            {
                var text = english[keys[property]];
                foreach (var word in Retired)
                {
                    if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add($"{file}: {property} says \"{word}\": \"{text}\"");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Settings_draws_toggles_and_pickers_not_checkboxes_or_radio_buttons()
    {
        foreach (var file in ConfigFiles())
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("ImGui.Checkbox(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ImGui.RadioButton(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_language_picker_stays_hidden_while_localization_is_frozen()
    {
        foreach (var file in ConfigFiles())
        {
            Assert.DoesNotContain("Strings.ConfigLanguage", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }
}
