using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for how the plugin's panes wire the wider spoiler shield (plan v7, 1.20.0 N6), from the 1.20.0 UI
/// review: no pane prints a journal genre or category raw (a node named after a hidden area must read its placeholder),
/// every surface that prints placeholders gives them the shield's hover and right-click, the shared placeholder menu
/// is drawn from each host window's root rather than by whichever placeholder reaches it first, and nothing flags a
/// place the story has not reached. The test project references Core and GameData only, so it reads the plugin's
/// sources from the repository.
/// </summary>
public sealed class ShieldWiringLintTests
{
    // A read of a journal genre or category name: ".GenreName" or ".CategoryName".
    private static readonly Regex JournalName = new(@"\.(GenreName|CategoryName)\b", RegexOptions.Compiled);

    /// <summary>
    /// Lines that read a journal name without printing it, by file and a fragment of the line, with the reason.
    /// </summary>
    private static readonly (string File, string Fragment, string Why)[] Allowed =
    [
        ("TreePane.cs", "new Node(", "the tree names its nodes raw and shields each label in ShieldNode"),
        ("FilterPanel.cs", "categories.Add(", "the per-category override list: journal categories are named after expansions, not areas"),
    ];

    private static string PluginFile(string name) =>
        File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", name));

    [Theory]
    [MemberData(nameof(ImGuiLintTests.PluginSources), MemberType = typeof(ImGuiLintTests))]
    public void No_pane_prints_a_journal_genre_or_category_past_the_shield(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        var offenders = new List<string>();
        var lines = File.ReadAllLines(Path.Combine(ResxFiles.RepositoryRoot(), relativePath));
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("//", StringComparison.Ordinal) || !JournalName.IsMatch(line))
            {
                continue;
            }

            var shielded = line.Contains("NodeName(", StringComparison.Ordinal) || line.Contains("ShieldRules.", StringComparison.Ordinal);
            var allowed = Allowed.Any(a => a.File == name && line.Contains(a.Fragment, StringComparison.Ordinal));
            if (!shielded && !allowed)
            {
                offenders.Add($"{relativePath}:{i + 1}: {line}");
            }
        }

        Assert.True(offenders.Count == 0, "Journal names printed raw (route them through SpoilerMask.NodeName or ShieldRules):" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData("TablePane.cs")]
    [InlineData("TreePane.cs")]
    [InlineData("CharactersPane.Duties.cs")]
    [InlineData("FlightPane.cs")]
    [InlineData("PlanPane.cs")]
    [InlineData("CharactersPane.Planning.cs")]
    [InlineData("TonightCard.Stops.cs")]
    [InlineData("RouteWindow.cs")]
    [InlineData("DetailPane.Companions.cs")]
    public void Every_surface_that_prints_placeholders_gives_them_the_shields_menu(string file)
    {
        var source = PluginFile(file);
        // ShieldItem is the detail pane's own wrapper of ShieldText.InteractItem.
        Assert.Matches(@"ShieldText\.(Interact|InteractItem|RevealItems)\(|\bShieldItem\(", source);
    }

    [Theory]
    [InlineData("MainWindow.cs", "nameof(MainWindow)")]
    [InlineData("RouteWindow.cs", "nameof(RouteWindow)")]
    public void Each_host_window_draws_the_placeholder_menu_from_its_root(string file, string host)
    {
        Assert.Contains($"ShieldText.DrawMenu({host}", PluginFile(file), StringComparison.Ordinal);
    }

    [Fact]
    public void A_placeholder_asks_for_the_menu_and_never_waits_for_another_to_close()
    {
        var source = PluginFile("ShieldText.cs");

        // One popup, drawn by the host (DrawMenu), not by the first placeholder to reach it.
        Assert.Single(Regex.Matches(source, @"ImRaii\.Popup\(MenuId\)"));
        Assert.Contains("public static void DrawMenu(", source, StringComparison.Ordinal);
        // A right-click on a second placeholder is not held back while a menu is open.
        Assert.DoesNotContain("IsPopupOpen(MenuId)", source, StringComparison.Ordinal);
        Assert.Contains("IsMouseReleased(ImGuiMouseButton.Right)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_flags_a_place_the_story_has_not_reached()
    {
        var source = PluginFile("GameLinks.cs");
        var canFlag = Regex.Match(source, @"public bool CanFlagMap\(QuestRecord quest\) =>[^;]*;");

        Assert.True(canFlag.Success);
        Assert.Contains("GiverPlaceHidden(quest)", canFlag.Value, StringComparison.Ordinal);
    }
}
