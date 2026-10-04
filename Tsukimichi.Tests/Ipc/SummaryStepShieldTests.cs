using System.Text.RegularExpressions;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Query;
using Tsukimichi.Tests.Localization;
using Tsukimichi.Tests.Query;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// The summary's Up next step (plan v8 M1 and M2) speaks for the logged-in character: its zone goes through that
/// character's shield, never the viewed character's, so the server info bar's tooltip and IPC <c>GetUpNext</c> never
/// name a place the logged-in story has not reached, whichever character a pane shows (<c>Ui/SummarySource.cs</c>).
/// </summary>
public sealed partial class SummaryStepShieldTests
{
    private const string StepFormat = "Step {0}: {1}";
    private const string NoObjective = "Follow the quest";
    private const string Separator = " · ";

    // A character before Kugane (the story's next quest is the second) and one past it (the fifth).
    private static readonly SpoilerMask Before = SpoilerNamesTests.At(SpoilerNamesTests.Path);
    private static readonly SpoilerMask Past = SpoilerNamesTests.At(SpoilerNamesTests.Far);

    [Fact]
    public void The_fixture_shields_differ_on_the_steps_zone()
    {
        Assert.True(Before.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.False(Past.IsNameMasked(SpoilerKind.Area, "Kugane"));
    }

    [Fact]
    public void The_steps_zone_goes_through_the_logged_in_characters_shield_not_the_viewed_ones()
    {
        // Logged in before Kugane while a pane shows a character past it: the summary still hides Kugane.
        var line = SummaryText.StepLine(3, "Speak with Hancock.", "Kugane", Before, StepFormat, NoObjective, Separator);
        Assert.DoesNotContain("Kugane", line, StringComparison.Ordinal);
        Assert.Equal("Step 3: Speak with Hancock." + Separator + Before.Name(SpoilerKind.Area, "Kugane"), line);
    }

    [Fact]
    public void After_a_character_switch_the_step_follows_the_new_logged_in_character()
    {
        var before = SummaryText.StepLine(3, "Speak with Hancock.", "Kugane", Before, StepFormat, NoObjective, Separator);
        var past = SummaryText.StepLine(3, "Speak with Hancock.", "Kugane", Past, StepFormat, NoObjective, Separator);
        Assert.Equal("Step 3: Speak with Hancock. · Kugane", past);
        Assert.NotEqual(before, past);

        // And back: the same raw zone, the earlier character's placeholder again.
        Assert.Equal(before, SummaryText.StepLine(3, "Speak with Hancock.", "Kugane", Before, StepFormat, NoObjective, Separator));
    }

    [Fact]
    public void A_step_without_objective_or_place_keeps_its_words()
    {
        Assert.Equal("Step 2: Follow the quest", SummaryText.StepLine(2, string.Empty, null, Before, StepFormat, NoObjective, Separator));
        Assert.Equal("Step 2: Go.", SummaryText.StepLine(2, "Go.", string.Empty, Past, StepFormat, NoObjective, Separator));
    }

    [Fact]
    public void SummarySource_names_the_step_zone_through_the_live_shield_never_the_step_views_zone()
    {
        var source = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "SummarySource.cs"));

        // StepView.Zone is named through the viewed character's shield for the panes.
        Assert.DoesNotMatch(StepViewZone(), source);
        Assert.Contains("var spoilers = session.LiveSpoilers;", source, StringComparison.Ordinal);
        Assert.Contains("guidance.StepZone(pick.Step)", source, StringComparison.Ordinal);
        Assert.Contains("SummaryText.StepLine(step.Step.Step, step.Objective, zone, spoilers,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cached_step_view_is_keyed_on_the_pane_shield_that_named_its_zone()
    {
        var source = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "GameLinks.Steps.cs"));
        Assert.Contains("int Spoilers, int PaneSpoilers, int Language", source, StringComparison.Ordinal);
        Assert.Contains("PaneSpoilers?.Fingerprint ?? 0", source, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\bstep\??\.Zone\b")]
    private static partial Regex StepViewZone();
}
