using System.Text.RegularExpressions;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// Installing a <see cref="CoreText"/> provider changes what every other test sees, so the tests that do run alone,
/// after the parallel ones.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CoreTextCollection
{
    public const string Name = "CoreText provider";
}

/// <summary>
/// Core's phrases (V2-19): English without a provider (every other test relies on it), the provider's text with one,
/// English inside <see cref="CoreText.English"/> (the diagnostic block), caches that follow a switch, and every
/// <c>CoreText.T("key", "English")</c> pair in Core's sources mirrored verbatim in the English resource file, which is
/// what the translations follow.
/// </summary>
[Collection(CoreTextCollection.Name)]
public sealed class CoreTextTests : IDisposable
{
    // CoreText.T("key", "English"), and the F/T helpers some builders wrap it in: F("key", "English {0}", args).
    private static readonly Regex Pair = new("(?:CoreText\\.T|\\bT|\\bF)\\(\"(Core\\.[^\"]+)\",\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[,)]", RegexOptions.Compiled);

    // A CoreText.T call whose English is not a literal cannot be mirrored into the resource file.
    private static readonly Regex NonLiteral = new("CoreText\\.T\\(\"Core\\.[^\"]+\",\\s*[^\"\\s]", RegexOptions.Compiled);

    public CoreTextTests() => CoreText.Use(null);

    public void Dispose() => CoreText.Use(null);

    [Fact]
    public void Every_core_phrase_is_in_the_English_resource_file_verbatim()
    {
        var english = ResxFiles.Load(string.Empty);
        var core = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi.Core");
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(file);
            foreach (Match match in NonLiteral.Matches(source))
            {
                offenders.Add($"{Path.GetFileName(file)}: {match.Value}… passes its English as a non-literal; write the English out");
            }

            foreach (Match match in Pair.Matches(source))
            {
                var key = match.Groups[1].Value;
                var text = Regex.Unescape(match.Groups[2].Value);
                if (seen.TryGetValue(key, out var earlier) && earlier != text)
                {
                    offenders.Add($"{key}: \"{text}\" in {Path.GetFileName(file)} differs from \"{earlier}\" elsewhere");
                }

                seen[key] = text;
                if (!english.TryGetValue(key, out var resx))
                {
                    offenders.Add($"{key} ({Path.GetFileName(file)}) is not in Strings.resx");
                }
                else if (resx != text)
                {
                    offenders.Add($"{key}: Strings.resx has \"{resx}\", Core has \"{text}\"");
                }
            }
        }

        Assert.True(seen.Count > 100, $"expected Core's phrases, found {seen.Count}");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Without_a_provider_core_speaks_English()
    {
        Assert.True(CoreText.IsEnglish);
        Assert.Equal("Blocked", StateNames.Name(QuestState.Blocked));
        Assert.Equal("Main scenario quest (Lv 83)", string.Format(System.Globalization.CultureInfo.InvariantCulture, SpoilerMask.PlaceholderFormat, 83));
        Assert.Equal("step 3 of 7", BlockerText.StepText(3, 7));
    }

    [Fact]
    public void A_provider_changes_the_phrases_and_keeps_the_rest_English()
    {
        CoreText.Use(new Table(new()
        {
            ["Core.State.Blocked"] = "Gesperrt",
            ["Core.Blocker.StepOf"] = "Schritt {0} von {1}",
        }));

        Assert.False(CoreText.IsEnglish);
        Assert.Equal("Gesperrt", StateNames.Name(QuestState.Blocked));
        Assert.Equal("Schritt 3 von 7", BlockerText.StepText(3, 7));
        Assert.Equal("Ready", StateNames.Name(QuestState.Ready));
    }

    [Fact]
    public void Composed_tooltips_follow_a_switch()
    {
        var english = StateNames.Tooltip(QuestState.Blocked);
        CoreText.Use(new Table(new() { ["Core.State.Blocked"] = "ブロック", ["Core.Glyph.Blocked"] = "新月" }));
        Assert.Equal("ブロック · 新月", StateNames.Tooltip(QuestState.Blocked));
        CoreText.Use(null);
        Assert.Equal(english, StateNames.Tooltip(QuestState.Blocked));
    }

    [Fact]
    public void The_English_scope_ignores_the_provider_and_nests()
    {
        CoreText.Use(new Table(new() { ["Core.State.Completed"] = "Terminée" }));
        using (CoreText.English())
        {
            Assert.Equal("Completed", StateNames.Name(QuestState.Completed));
            using (CoreText.English())
            {
                Assert.Equal("Completed", StateNames.Name(QuestState.Completed));
            }

            Assert.Equal("Completed", StateNames.Name(QuestState.Completed));
        }

        Assert.Equal("Terminée", StateNames.Name(QuestState.Completed));
    }

    [Fact]
    public void The_diagnostic_block_stays_English_under_a_translation()
    {
        var quest = Fixture.Quest(66754, "Brotherhood of Ash") with { Level = 24 };
        var catalog = QuestCatalog.Build([quest]);
        var names = new BlockerNames { Catalog = catalog };
        var level = new RequirementResult(new LevelRequirement(24, 10), false, "needs level 24, you are 10");
        var evaluation = new QuestEvaluation(QuestState.Blocked, [level], level, null, null);
        var inputs = new DiagnosticInputs { Quest = quest, Names = names, Evaluation = evaluation };
        var english = QuestDiagnostic.Compose(inputs);

        CoreText.Use(new Table(new() { ["Core.State.Blocked"] = "ブロック中", ["Core.Blocker.Level"] = "Lv{0}" }));
        Assert.Equal("ブロック中", StateNames.Name(QuestState.Blocked));
        Assert.Equal(english, QuestDiagnostic.Compose(inputs));
        Assert.Contains("state: Blocked", english, StringComparison.Ordinal);
    }

    [Fact]
    public void The_spoiler_placeholder_follows_the_language()
    {
        var quest = Fixture.Quest(70000, "Hidden") with { Level = 83 };
        var english = SpoilerMask.Placeholder(quest);
        CoreText.Use(new Table(new() { ["Core.Spoiler.Placeholder"] = "メインクエスト（Lv{0}）" }));
        Assert.Equal("メインクエスト（Lv83）", SpoilerMask.Placeholder(quest));
        CoreText.Use(null);
        Assert.Equal(english, SpoilerMask.Placeholder(quest));
        Assert.Equal("Main scenario quest (Lv 83)", english);
    }

    [Fact]
    public void Filter_identities_stay_English_while_their_labels_translate()
    {
        CoreText.Use(new Table(new() { ["Core.Filter.HideCompleted"] = "Abgeschlossene ausblenden" }));
        Assert.Equal("Hide completed", FilterNames.HideCompleted);
        Assert.Equal("Abgeschlossene ausblenden", FilterNames.Display(FilterNames.HideCompleted));
        Assert.Equal("Search", FilterNames.Display(FilterNames.Search));
        Assert.Equal("anything", FilterNames.Display("anything"));
    }

    [Fact]
    public void Requirement_details_in_English_are_the_evaluator_s_own()
    {
        // In English the detail pane shows RequirementResult.Detail untouched; Render, which a translation uses,
        // says the same thing from the record: checked over a whole resolve of the fixture-shaped catalog.
        var level = new RequirementResult(new LevelRequirement(50, 42), false, "needs level 50, you are 42");
        Assert.Same(level.Detail, RequirementDetail.Text(level, BlockerNames.Default));
        Assert.Equal(level.Detail, RequirementDetail.Render(level, BlockerNames.Default));

        CoreText.Use(new Table(new() { ["Core.Req.NeedsLevel"] = "Stufe {0} nötig, du bist {1}" }));
        Assert.Equal("Stufe 50 nötig, du bist 42", RequirementDetail.Text(level, BlockerNames.Default));
    }

    /// <summary>A provider over a fixed table.</summary>
    private sealed class Table(Dictionary<string, string> values) : ITextProvider
    {
        public string? Find(string key) => values.TryGetValue(key, out var value) ? value : null;
    }
}
