using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The one safety table (feature plan v6 S1/S2, owner point 9): which gate each action goes through, which ones offer
/// Undo, the hold length Settings may store, and source checks that keep the plugin on the table (no Enter path into a
/// verdict, no plain button confirming an irreversible change).
/// </summary>
public class SafetyRulesTests
{
    [Theory]
    [InlineData(GuardedAction.MarkUnique)]
    [InlineData(GuardedAction.MarkNotUnique)]
    [InlineData(GuardedAction.RestoreVerdict)]
    [InlineData(GuardedAction.DontTrackCharacter)]
    public void Single_changes_you_can_undo_need_a_held_key(GuardedAction action)
    {
        Assert.Equal(SafetyTier.Armed, SafetyRules.TierOf(action));
        Assert.True(SafetyRules.OffersUndo(action));
    }

    [Theory]
    [InlineData(GuardedAction.QuestionableReplace)]
    [InlineData(GuardedAction.CompanionApply)]
    [InlineData(GuardedAction.ForgetCharacter)]
    [InlineData(GuardedAction.ForgetCharacters)]
    [InlineData(GuardedAction.DeleteAllData)]
    public void Irreversible_changes_need_a_press_and_hold_and_offer_no_undo(GuardedAction action)
    {
        Assert.Equal(SafetyTier.Hold, SafetyRules.TierOf(action));
        Assert.False(SafetyRules.OffersUndo(action));
    }

    [Theory]
    [InlineData(GuardedAction.RestoreAllVerdicts)]
    [InlineData(GuardedAction.PinAll)]
    public void Bulk_changes_need_a_press_and_hold_and_still_offer_undo(GuardedAction action)
    {
        Assert.Equal(SafetyTier.Hold, SafetyRules.TierOf(action));
        Assert.True(SafetyRules.OffersUndo(action));
    }

    [Theory]
    [InlineData(GuardedAction.EditVerdictNote)]
    [InlineData(GuardedAction.HideCharacter)]
    [InlineData(GuardedAction.Unpin)]
    [InlineData(GuardedAction.ResetFilters)]
    public void Small_changes_are_one_click_with_undo(GuardedAction action)
    {
        Assert.Equal(SafetyTier.None, SafetyRules.TierOf(action));
        Assert.True(SafetyRules.OffersUndo(action));
    }

    [Fact]
    public void Every_action_has_a_row_in_the_table()
    {
        foreach (var action in Enum.GetValues<GuardedAction>())
        {
            var tier = SafetyRules.TierOf(action);
            Assert.True(Enum.IsDefined(tier), $"{action} maps to an unknown tier");

            // Whatever cannot be undone is never a one-click action.
            if (!SafetyRules.OffersUndo(action))
            {
                Assert.Equal(SafetyTier.Hold, tier);
            }
        }
    }

    [Fact]
    public void An_unknown_action_has_no_tier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SafetyRules.TierOf((GuardedAction)999));
    }

    [Theory]
    [InlineData(0.6f, 0.6f)]
    [InlineData(1.26f, 1.3f)]
    [InlineData(0.04f, SafetyRules.MinHoldSeconds)]
    [InlineData(-3f, SafetyRules.MinHoldSeconds)]
    [InlineData(9f, SafetyRules.MaxHoldSeconds)]
    [InlineData(float.NaN, SafetyRules.DefaultHoldSeconds)]
    [InlineData(float.PositiveInfinity, SafetyRules.DefaultHoldSeconds)]
    public void The_saved_hold_length_is_clamped_to_whole_tenths(float saved, float expected)
    {
        Assert.Equal(expected, SafetyRules.ClampHoldSeconds(saved), 4);
    }

    [Fact]
    public void Countdown_labels_cover_the_longest_hold()
    {
        Assert.Equal(SafetyRules.MaxHoldTenths, (int)MathF.Round(SafetyRules.MaxHoldSeconds * 10f));
        Assert.Equal(SafetyRules.DefaultHoldSeconds, SafetySettings.Default.HoldSeconds);
        Assert.False(SafetySettings.Default.TwoClick);
    }

    [Fact]
    public void Two_click_timing_leaves_room_between_a_double_click_and_the_window()
    {
        Assert.True(SafetyRules.SecondClickMinGapSeconds > 0.0);
        Assert.True(SafetyRules.SecondClickWindowSeconds > SafetyRules.SecondClickMinGapSeconds * 4);
    }

    // ---- The plugin's sources stay on the table ----

    private static string UiFile(string name) => File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", name));

    private static IEnumerable<string> UiFiles() =>
        Directory.GetFiles(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui"), "*.cs", SearchOption.AllDirectories);

    [Fact]
    public void Enter_in_the_verdict_note_never_confirms()
    {
        // Owner point 9: Enter in the note field used to save the verdict past the Shift/hold gate.
        Assert.DoesNotContain("EnterReturnsTrue", UiFile("VerdictPrompt.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Verdicts_are_set_or_cleared_only_through_the_verdict_actions()
    {
        var call = new Regex(@"\boverrides\.(Set|Clear)\(", RegexOptions.Compiled);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "VerdictPrompt.cs", "MoonlitPane.cs", "IUniqueOverrides.cs" };
        var offenders = UiFiles()
            .Where(f => !allowed.Contains(Path.GetFileName(f)) && call.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(offenders.Count == 0, "A verdict changed outside VerdictPrompt (armed, with Undo): " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("ConfigDeleteConfirm")]
    [InlineData("CharactersForgetConfirm")]
    [InlineData("AltsForgetBulkConfirm")]
    [InlineData("CompanionSetupApplyConfirm")]
    [InlineData("QuestionableReplaceConfirm")]
    public void Irreversible_confirms_are_never_plain_buttons(string label)
    {
        var plain = new Regex(@"ImGui\.(Small)?Button\(\s*Strings\." + label + @"\b", RegexOptions.Compiled);
        var offenders = UiFiles().Where(f => plain.IsMatch(File.ReadAllText(f))).Select(Path.GetFileName).ToList();
        Assert.True(offenders.Count == 0, $"Strings.{label} is a plain button in: {string.Join(", ", offenders)}");

        // And it is a hold button somewhere (its label with the hold id).
        var hold = "Strings." + label + " + Chrome.HoldIdSuffix";
        Assert.Contains(UiFiles(), f => File.ReadAllText(f).Contains(hold, StringComparison.Ordinal));
    }
}
