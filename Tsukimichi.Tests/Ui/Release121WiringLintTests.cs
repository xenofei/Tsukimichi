using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for the wiring the 1.21.0 UI review found missing (the logic itself is tested in Core): the 1.21
/// placeholders take the shield's hover and right-click, <c>/tsuki go</c> never travels to a hidden place, <c>/tsuki
/// next</c> reads every input for one character, Next stops leave set-aside quests out, the current step is re-aimed
/// each frame without rebuilding it, and the per-frame paths of My blues and New chapters allocate nothing. The test
/// project references Core and GameData only, so it reads the plugin's sources from the repository.
/// </summary>
public sealed class Release121WiringLintTests
{
    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine([ResxFiles.RepositoryRoot(), "Tsukimichi", .. path]));

    private static string Ui(string name) => Source("Ui", name);

    /// <summary>The body of the first member whose declaration contains <paramref name="signature"/>, to its closing brace at the same indent.</summary>
    private static string Member(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf("\n    }", at, StringComparison.Ordinal);
        return source[at..(end < 0 ? source.Length : end)];
    }

    [Theory]
    [InlineData("TonightCard.UpNext.cs")]
    [InlineData("DetailPane.Step.cs")]
    [InlineData("DetailPane.Triad.cs")]
    [InlineData("PlanPane.DoFirst.cs")]
    [InlineData("PlanPane.Story.cs")]
    [InlineData("NewChaptersSource.cs")]
    [InlineData("EvercoldCardView.cs")]
    public void Each_121_placeholder_takes_the_shields_hover_and_right_click(string file)
    {
        // ShieldItem is the detail pane's own wrapper of ShieldText.InteractItem.
        Assert.Matches(@"ShieldText\.(Interact|InteractItem|InteractQuest|InteractQuestItem)\(|\bShieldItem\(", Ui(file));
    }

    [Fact]
    public void A_hidden_zones_hover_covers_its_label_never_the_groups_menu()
    {
        var label = Member(Ui("PlanPane.SetAside.cs"), "private void DrawZoneLabel(");
        Assert.Contains("ShieldZoneLabel(", label, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldText.InteractItem(session, Core.Query.SpoilerKind.Area, hidden, ZoneLabel(zone)", Ui("PlanPane.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Go_never_travels_to_or_flags_a_hidden_quest_or_place()
    {
        var go = Member(Source("Commands", "GuidanceCommand.cs"), "public void Go(string name)");
        var check = go.IndexOf("links.GiverPlaceHidden(target)", StringComparison.Ordinal);
        Assert.True(check >= 0, "Go must check GiverPlaceHidden before it starts anything");
        Assert.Contains("Spoilers.IsMasked(quest)", go, StringComparison.Ordinal);
        Assert.True(check < go.IndexOf("Start(target)", StringComparison.Ordinal));
    }

    [Fact]
    public void Next_reads_every_input_for_one_character()
    {
        var command = Source("Commands", "GuidanceCommand.cs");
        Assert.Contains("Func<ulong, QuestRecord?>? RouteNext", command, StringComparison.Ordinal);
        Assert.Contains("Func<ulong, IReadOnlyList<uint>>? Pins", command, StringComparison.Ordinal);
        Assert.Contains("Func<ulong, IEnumerable<uint>>? Closest", command, StringComparison.Ordinal);

        var wiring = Source("Plugin.Guidance.cs");
        Assert.DoesNotContain("routes.ViewedRoute", wiring, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"nextStops\.Stops\b"), wiring);
    }

    [Fact]
    public void Next_stops_leave_set_aside_quests_out()
    {
        var source = Ui("NextStopsSource.cs");
        Assert.Contains("session.ViewedSetAside", source, StringComparison.Ordinal);
        Assert.Contains("session.SetAsideOf(contentId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_current_step_is_aimed_each_frame_without_rebuilding_it()
    {
        var current = Member(Ui("GameLinks.Steps.cs"), "public StepView? CurrentStep(");
        Assert.DoesNotContain("CurrentStepResolver.Resolve(", current, StringComparison.Ordinal);
        Assert.Contains(".Pick(", current, StringComparison.Ordinal);

        // Up next follows the player's position as the detail pane does.
        Assert.Contains("FollowStep(session);", Member(Ui("TonightCard.UpNext.cs"), "private void DrawUpNext("), StringComparison.Ordinal);
    }

    [Fact]
    public void Up_nexts_route_reason_goes_through_the_wider_shield()
    {
        var reason = Member(Ui("TonightCard.UpNext.cs"), "private string RouteReason(");
        Assert.Contains(".ShownLabel(spoilers", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Routes?.Label", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_main_scenario_line_swaps_on_a_branch_too()
    {
        var msq = Member(Ui("TonightCard.cs"), "private void DrawMsq(");
        Assert.DoesNotContain("msq.Count == 1 ? MsqRowShown", msq, StringComparison.Ordinal);
        Assert.Contains("MsqRowShown(msq[i])", msq, StringComparison.Ordinal);
    }

    [Fact]
    public void My_blues_and_new_chapters_allocate_nothing_per_frame()
    {
        Assert.DoesNotContain(".Where(", Member(Ui("PlanPane.SetAside.cs"), "private uint[] GroupRows("), StringComparison.Ordinal);
        Assert.DoesNotMatch(@"new List<PlanTierGroup>", Member(Ui("PlanPane.DoFirst.cs"), "private void DrawDoFirst("));

        // An empty answer is kept for the view key too: a character with no new chapters costs nothing per frame.
        var lines = Member(Ui("NewChaptersSource.cs"), "private Line[]? Lines()");
        Assert.Contains("hasBuilt", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("SideStories.NewChapters", lines, StringComparison.Ordinal);
    }

    [Fact]
    public void Your_story_keys_each_line_by_the_line()
    {
        var line = Member(Ui("PlanPane.Story.cs"), "private void DrawStoryLine(");
        Assert.DoesNotMatch(new Regex(@"PushId\(\(int\)\(target\?\.RowId"), line);
        Assert.Contains("PushId(storyLine.Chain.Name)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_double_click_on_the_selected_row_never_flashes_tonight()
    {
        var table = Ui("TablePane.cs");
        Assert.Contains("secondClick.Click(quest.RowId", table, StringComparison.Ordinal);
        Assert.Contains("secondClick.Due(", table, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_kept_in_place_and_the_group_question_follow_the_character()
    {
        Assert.Contains("kept.Follow(session.ViewedContentId)", Ui("PlanPane.cs"), StringComparison.Ordinal);
        var confirm = Member(Ui("PlanPane.SetAside.cs"), "private void DrawGroupConfirm(");
        Assert.Contains("confirmOwner != session.ViewedContentId", confirm, StringComparison.Ordinal);
        Assert.Contains("SetAside(owner, confirmRows)", confirm, StringComparison.Ordinal);
    }
}
