using Tsukimichi.Core.Companions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// Companion setup: reading a setting out of another plugin's configuration file (<see cref="CompanionConfigJson"/>),
/// "✓ / ✕ / ?" per recommended setting, a setting another one covers, the summary line, the blocking reason a hand-off
/// button gives, and the catalog of recommended settings itself (<see cref="CompanionSetupCatalog"/>).
/// </summary>
public class CompanionSetupTests
{
    // Shaped like Questionable.json: Newtonsoft with "$type" members and enums as numbers.
    private const string QuestionableJson = """
        {
          "$type": "Questionable.Configuration, Questionable",
          "Version": 2,
          "PluginSetupCompleteVersion": 5,
          "General": { "$type": "Questionable.Configuration+GeneralConfiguration, Questionable", "CombatModule": 0, "ConfigureTextAdvance": false },
          "Advanced": { "PreventQuestCompletion": true },
          "Stop": { "Enabled": false, "CommandAfterStop": "/li auto" },
        }
        """;

    private static readonly SetupRequirement Combat = CompanionSetupCatalog.Get("questionable.combat-module")!;
    private static readonly SetupRequirement Configure = CompanionSetupCatalog.Get("questionable.configure-textadvance")!;
    private static readonly SetupRequirement Prevent = CompanionSetupCatalog.Get("questionable.prevent-completion")!;
    private static readonly SetupRequirement Accept = CompanionSetupCatalog.Get("textadvance.quest-accept")!;

    private static CompanionStatus Loaded(CompanionPlugin plugin, string? internalName = null)
    {
        var definition = CompanionCatalog.Get(plugin);
        return CompanionResolver.Resolve(definition, [new InstalledPlugin(internalName ?? definition.Primary.InternalName, definition.Primary.KnownBuild, true)]);
    }

    private static CompanionStatus Missing(CompanionPlugin plugin) => CompanionResolver.Resolve(CompanionCatalog.Get(plugin), []);

    [Theory]
    [InlineData("General.CombatModule", "0")]
    [InlineData("general.configuretextadvance", "false")]
    [InlineData("PluginSetupCompleteVersion", "5")]
    [InlineData("Stop.CommandAfterStop", "/li auto")]
    public void A_setting_is_read_by_its_dotted_path_ignoring_case(string path, string expected)
    {
        Assert.Equal(SetupReading.Of(expected), CompanionConfigJson.Read(QuestionableJson, path));
    }

    [Fact]
    public void A_setting_the_file_lacks_reads_as_missing_and_bad_json_as_unread()
    {
        Assert.Equal(SetupReading.Missing, CompanionConfigJson.Read(QuestionableJson, "Duties.RunInstancedContentWithAutoDuty"));
        Assert.Equal(SetupReading.Missing, CompanionConfigJson.Read(QuestionableJson, "Version.Nested"));
        Assert.Equal(SetupReading.Unread, CompanionConfigJson.Read("{ not json", "General.CombatModule"));
        Assert.Equal(SetupReading.Unread, CompanionConfigJson.Read(string.Empty, "General.CombatModule"));
    }

    [Fact]
    public void One_setting_reads_tick_cross_or_question_mark()
    {
        Assert.Equal(SetupCheck.NeedsChange, CompanionSetupEvaluator.Evaluate(Combat, SetupReading.Of("0")));
        Assert.Equal(SetupCheck.Ok, CompanionSetupEvaluator.Evaluate(Combat, SetupReading.Of("1")));
        Assert.Equal(SetupCheck.Ok, CompanionSetupEvaluator.Evaluate(Configure, SetupReading.Of("True")));
        Assert.Equal(SetupCheck.Unknown, CompanionSetupEvaluator.Evaluate(Combat, SetupReading.Unread));
    }

    [Fact]
    public void A_setting_the_file_lacks_takes_the_plugins_default()
    {
        // Questionable's defaults: no combat module, TextAdvance configured.
        Assert.Equal(SetupCheck.NeedsChange, CompanionSetupEvaluator.Evaluate(Combat, SetupReading.Missing));
        Assert.Equal(SetupCheck.Ok, CompanionSetupEvaluator.Evaluate(Configure, SetupReading.Missing));

        // A gate's setting has no default: missing stays unknown.
        Assert.Equal(SetupCheck.Unknown, CompanionSetupEvaluator.Evaluate(Accept, SetupReading.Missing));
    }

    [Fact]
    public void A_plugin_that_is_not_loaded_is_not_checked()
    {
        var setup = CompanionSetupEvaluator.Evaluate(Missing(CompanionPlugin.Questionable), CompanionSetupCatalog.For(CompanionPlugin.Questionable), static _ => SetupReading.Of("0"));
        Assert.Equal(PluginSetupState.NotLoaded, setup.State);
        Assert.Empty(setup.Results);
    }

    [Fact]
    public void A_loaded_plugin_sums_its_settings_and_a_manual_one_stays_unknown()
    {
        var questionable = CompanionSetupEvaluator.Evaluate(
            Loaded(CompanionPlugin.Questionable),
            CompanionSetupCatalog.For(CompanionPlugin.Questionable),
            r => CompanionConfigJson.Read(QuestionableJson, r.Path!));
        Assert.Equal(PluginSetupState.NeedsSetup, questionable.State);
        Assert.Equal(SetupCheck.Ok, questionable.Results.Single(r => r.Requirement.Id == "questionable.setup").Check);
        Assert.Equal(SetupCheck.NeedsChange, questionable.Results.Single(r => r.Requirement == Prevent).Check);

        var artisan = CompanionSetupEvaluator.Evaluate(Loaded(CompanionPlugin.Artisan), CompanionSetupCatalog.For(CompanionPlugin.Artisan), static _ => SetupReading.Missing);
        Assert.Equal(PluginSetupState.Ready, artisan.State);
        Assert.Equal(SetupCheck.Unknown, artisan.Results.Single(r => r.Requirement.Id == "artisan.gearsets").Check);
        Assert.Equal(1, artisan.UnknownCount);
    }

    [Fact]
    public void A_setting_of_one_build_applies_to_that_build_only()
    {
        var original = CompanionSetupEvaluator.Evaluate(Loaded(CompanionPlugin.GatherBuddy), CompanionSetupCatalog.For(CompanionPlugin.GatherBuddy), static _ => SetupReading.Missing);
        var reborn = CompanionSetupEvaluator.Evaluate(Loaded(CompanionPlugin.GatherBuddy, "GatherBuddyReborn"), CompanionSetupCatalog.For(CompanionPlugin.GatherBuddy), static _ => SetupReading.Missing);
        Assert.Contains(original.Results, static r => r.Requirement.Id == "gatherbuddy.set-names");
        Assert.DoesNotContain(reborn.Results, static r => r.Requirement.Id == "gatherbuddy.set-names");
        Assert.Contains(reborn.Results, static r => r.Requirement.Id == "gatherbuddy.teleport");
    }

    private static IReadOnlyList<PluginSetup> QuestionableAndTextAdvance(string configureTextAdvance, string questAccept)
    {
        var questionable = CompanionSetupEvaluator.Evaluate(
            Loaded(CompanionPlugin.Questionable),
            CompanionSetupCatalog.For(CompanionPlugin.Questionable),
            r => r == Configure ? SetupReading.Of(configureTextAdvance) : SetupReading.Missing);
        var textAdvance = CompanionSetupEvaluator.Evaluate(
            Loaded(CompanionPlugin.TextAdvance),
            CompanionSetupCatalog.For(CompanionPlugin.TextAdvance),
            r => r == Accept ? SetupReading.Of(questAccept) : SetupReading.Of(r.Id == "textadvance.not-paused" ? "false" : "true"));
        return CompanionSetupEvaluator.ApplyCoverage([questionable, textAdvance]);
    }

    [Fact]
    public void Questionable_configuring_TextAdvance_itself_covers_TextAdvances_own_switches()
    {
        var setups = QuestionableAndTextAdvance(configureTextAdvance: "true", questAccept: "false");
        var accept = setups[1].Results.Single(r => r.Requirement == Accept);
        Assert.Equal(SetupCheck.Ok, accept.Check);
        Assert.True(accept.Covered);
        Assert.Equal(PluginSetupState.Ready, setups[1].State);
        Assert.Null(CompanionSetupEvaluator.BlockingFor(CompanionPlugin.Questionable, setups.Where(s => s.Plugin == CompanionPlugin.TextAdvance).ToList()));
    }

    [Fact]
    public void Without_that_TextAdvances_quest_accept_off_blocks_the_Questionable_hand_off()
    {
        var setups = QuestionableAndTextAdvance(configureTextAdvance: "false", questAccept: "false");
        Assert.Equal(PluginSetupState.NeedsSetup, setups[1].State);
        Assert.Equal(Accept, CompanionSetupEvaluator.BlockingFor(CompanionPlugin.Questionable, setups)?.Requirement);
        Assert.Null(CompanionSetupEvaluator.BlockingFor(CompanionPlugin.AutoDuty, setups));
    }

    [Fact]
    public void A_recommended_setting_set_otherwise_never_blocks()
    {
        var setups = QuestionableAndTextAdvance(configureTextAdvance: "false", questAccept: "true");
        Assert.Equal(PluginSetupState.NeedsSetup, setups[0].State);
        Assert.Null(CompanionSetupEvaluator.BlockingFor(CompanionPlugin.Questionable, setups));
    }

    [Fact]
    public void The_summary_counts_plugins_to_set_up_and_automation_plugins_not_loaded()
    {
        var setups = new List<PluginSetup>(QuestionableAndTextAdvance(configureTextAdvance: "false", questAccept: "true"))
        {
            CompanionSetupEvaluator.Evaluate(Missing(CompanionPlugin.AutoDuty), CompanionSetupCatalog.For(CompanionPlugin.AutoDuty), static _ => SetupReading.Unread),
            CompanionSetupEvaluator.Evaluate(Missing(CompanionPlugin.QuestMap), [], static _ => SetupReading.Unread),
            CompanionSetupEvaluator.Evaluate(Missing(CompanionPlugin.RotationPlugin), [], static _ => SetupReading.Unread),
        };
        var summary = CompanionSetupEvaluator.Summarize(setups);
        Assert.False(summary.Ready);
        Assert.Equal([CompanionPlugin.Questionable], summary.NeedSetup);
        Assert.Equal([CompanionPlugin.AutoDuty], summary.NotLoaded);

        var ready = CompanionSetupEvaluator.Summarize(QuestionableAndTextAdvance(configureTextAdvance: "true", questAccept: "true")
            .Where(s => s.Plugin == CompanionPlugin.TextAdvance).ToList());
        Assert.True(ready.Ready);
    }

    [Fact]
    public void Apply_lists_only_settable_settings_set_otherwise()
    {
        var autoDuty = CompanionSetupEvaluator.Evaluate(
            Loaded(CompanionPlugin.AutoDuty),
            CompanionSetupCatalog.For(CompanionPlugin.AutoDuty),
            static r => r.Id switch
            {
                "autoduty.manage-rotation" => SetupReading.Of("False"),
                "autoduty.unsynced" => SetupReading.Of("True"),
                _ => SetupReading.Unread,
            });
        Assert.Equal(["autoduty.manage-rotation", "autoduty.unsynced"], autoDuty.Applicable.Select(static r => r.Requirement.Id));
        Assert.All(autoDuty.Applicable, static r => Assert.True(r.Requirement.Accepts(r.Requirement.ApplyValue!)));

        var questionable = CompanionSetupEvaluator.Evaluate(Loaded(CompanionPlugin.Questionable), CompanionSetupCatalog.For(CompanionPlugin.Questionable), static _ => SetupReading.Of("0"));
        Assert.NotEqual(0, questionable.NeedsChangeCount);
        Assert.Empty(questionable.Applicable);
    }

    [Fact]
    public void Apply_sets_only_the_confirmed_settings_still_set_otherwise()
    {
        // The confirmation listed Manage rotation and Unsynced. Read again at confirm time: Manage rotation was set by
        // hand meanwhile, and Leave duty turned up set otherwise (never confirmed).
        string[] confirmed = ["autoduty.manage-rotation", "autoduty.unsynced"];
        var now = CompanionSetupEvaluator.Evaluate(
            Loaded(CompanionPlugin.AutoDuty),
            CompanionSetupCatalog.For(CompanionPlugin.AutoDuty),
            static r => r.Id switch
            {
                "autoduty.manage-rotation" => SetupReading.Of("True"),
                "autoduty.unsynced" => SetupReading.Of("True"),
                "autoduty.leave-duty" => SetupReading.Of("False"),
                _ => SetupReading.Unread,
            });

        Assert.Equal(["autoduty.leave-duty", "autoduty.unsynced"], now.Applicable.Select(static r => r.Requirement.Id));
        Assert.Equal(["autoduty.unsynced"], now.ApplicableOf(confirmed).Select(static r => r.Requirement.Id));
        Assert.Empty(now.ApplicableOf([]));
    }

    [Fact]
    public void The_catalog_is_consistent()
    {
        var ids = CompanionSetupCatalog.All.Select(static r => r.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var requirement in CompanionSetupCatalog.All)
        {
            var definition = CompanionCatalog.Get(requirement.Plugin);
            Assert.NotEmpty(requirement.HandOffs);
            Assert.Equal(requirement.Source == SetupSource.Manual, requirement.Path is null);
            Assert.True(requirement.Source == SetupSource.Manual || requirement.Values.Count > 0, requirement.Id);

            // Only a plugin's own IPC sets a setting, and Apply sets a value the rule accepts.
            if (requirement.ApplyValue is { } apply)
            {
                Assert.Equal(SetupSource.Ipc, requirement.Source);
                Assert.True(requirement.Accepts(apply), requirement.Id);
            }

            if (requirement.Default is { } fallback)
            {
                Assert.Equal(SetupSource.ConfigFile, requirement.Source);
                Assert.NotNull(fallback);
            }

            if (requirement.CoveredBy is { } coveredBy)
            {
                Assert.NotNull(CompanionSetupCatalog.Get(coveredBy));
            }

            if (requirement.Variant is { } variant)
            {
                Assert.Contains(definition.Variants, v => v.InternalName == variant);
            }
        }
    }

    [Fact]
    public void Every_recommended_setting_has_its_words_in_English()
    {
        var english = ResxFiles.Load(string.Empty);
        foreach (var requirement in CompanionSetupCatalog.All)
        {
            foreach (var part in new[] { "Label", "Recommended", "Why" })
            {
                Assert.True(english.ContainsKey($"CompanionSetup.{requirement.Id}.{part}"), $"Strings.resx lacks CompanionSetup.{requirement.Id}.{part}");
            }

            Assert.Equal(requirement.Impact == SetupImpact.Blocking, english.ContainsKey($"CompanionSetup.{requirement.Id}.Need"));
        }

        foreach (var plugin in Enum.GetValues<CompanionPlugin>())
        {
            Assert.True(english.ContainsKey($"CompanionUnlocks.{plugin}"), $"Strings.resx lacks CompanionUnlocks.{plugin}");
        }
    }
}
