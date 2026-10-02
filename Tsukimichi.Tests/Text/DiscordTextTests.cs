using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Text;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Text;

/// <summary>
/// Copy for Discord (1.8.0): <see cref="DiscordText"/>'s escaping, masked links and lists, and the route's Discord form
/// (<see cref="RouteMarkdown.WriteDiscord"/>): plain bullets, headings a split part repeats, links only where given,
/// parts of at most 2,000 characters.
/// </summary>
public class DiscordTextTests
{
    [Fact]
    public void Escape_backslashes_what_Discord_reads_as_formatting()
    {
        Assert.Equal("Plain name", DiscordText.Escape("Plain name"));
        Assert.Equal("A \\*starred\\* \\_name\\_ \\~\\~x\\~\\~ \\|\\|spoiler\\|\\| \\`code\\` \\[a\\] \\<b\\> \\#1 \\\\", DiscordText.Escape("A *starred* _name_ ~~x~~ ||spoiler|| `code` [a] <b> #1 \\"));
    }

    [Fact]
    public void A_link_is_masked_in_angle_brackets_and_its_target_never_breaks_the_markup()
    {
        Assert.Equal("[Close to Home](<https://example.org/q>)", DiscordText.Link("Close to Home", "https://example.org/q"));
        Assert.Equal("Close to Home", DiscordText.Link("Close to Home", null));
        Assert.Equal("\\[Odd\\] name", DiscordText.Link("[Odd] name", string.Empty));

        // Parentheses, spaces and angle brackets in the URL are percent-encoded so the link target cannot end early.
        Assert.Equal(
            "[A Bone to Pick](<https://ffxiv.consolegameswiki.com/wiki/A_Bone_to_Pick_%28Quest%29>)",
            DiscordText.Link("A Bone to Pick", "https://ffxiv.consolegameswiki.com/wiki/A_Bone_to_Pick_(Quest)"));
        Assert.Equal("[x](<https://e.org/a%20b%3Cc%3E>)", DiscordText.Link("x", "https://e.org/a b<c>"));
    }

    [Fact]
    public void A_list_has_a_bold_title_and_one_plain_bullet_per_line()
    {
        var text = DiscordText.List("Only on Alice · 2 quests", ["Close to Home (3)", " ", "The *Ultimate* Weapon (1)"]);

        Assert.Equal("**Only on Alice · 2 quests**\n- Close to Home (3)\n- The \\*Ultimate\\* Weapon (1)", text);
        Assert.DoesNotContain("[ ]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_long_list_splits_into_parts_of_at_most_2000_characters()
    {
        var lines = Enumerable.Range(1, 200).Select(i => $"Quest number {i} with a reasonably long name ({i})").ToArray();
        var parts = DiscordText.Parts(DiscordText.List("Title", lines));

        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.Length <= MessageSplitter.DiscordLimit, $"{p.Length} characters"));
        Assert.Equal(200, parts.Sum(p => p.Split('\n').Count(l => l.StartsWith(DiscordText.Bullet, StringComparison.Ordinal))));
        Assert.Empty(DiscordText.Parts(string.Empty));
    }

    [Fact]
    public void The_route_for_Discord_uses_bullets_and_headings_and_links_only_what_it_is_given()
    {
        var catalog = Catalog(
            Needs(A, 49) with { Name = "The *Secret* Finale", Journal = new JournalRef(0, "Main Scenario", 2, "Seventh Umbral Era Main Scenario Quests", 1, "Genre", 1) },
            Needs(Target, 50, A) with { Name = "Out of the Blue" });
        var route = UnlockRoute.Build(RouteTarget.ForJob("Blue Mage", Target), catalog, States(catalog));

        string Name(QuestRecord q) => q.RowId == A ? "Main scenario quest (Lv 49)" : q.Name;
        var plain = RouteMarkdown.WriteDiscord(route, catalog, Name);
        Assert.Equal(
            "**Route to Blue Mage** · 2 quests · Lv 49–50 · MSQ: Seventh Umbral Era\n"
            + "\n### Main scenario: Seventh Umbral Era\n"
            + "- Lv 49 · Main scenario quest (Lv 49) (MSQ)\n"
            + "\n### After the main scenario\n"
            + "- Lv 50 · Out of the Blue — target",
            plain);

        // The masked quest gets no link (the plugin's function answers null for it); the other one does.
        var linked = RouteMarkdown.WriteDiscord(route, catalog, Name, q => q.RowId == A ? null : "https://example.org/" + q.RowId);
        Assert.Contains("- Lv 49 · Main scenario quest (Lv 49) (MSQ)", linked, StringComparison.Ordinal);
        Assert.Contains("- Lv 50 · [Out of the Blue](<https://example.org/" + Target + ">) — target", linked, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", linked, StringComparison.Ordinal);

        // The Markdown copy is unchanged.
        Assert.StartsWith("**Route to Blue Mage**", RouteMarkdown.Write(route, catalog, Name), StringComparison.Ordinal);
        Assert.Contains("1. Lv 49", RouteMarkdown.Write(route, catalog, Name), StringComparison.Ordinal);
    }

    private static QuestRecord Needs(uint rowId, byte level, params uint[] prereqs) =>
        Quest(rowId) with
        {
            Level = level,
            PreviousQuests = new Prereq(prereqs, JoinKind.All),
            Journal = new JournalRef(3, "Side", 60, "Sidequests", 1, "Genre", (int)rowId),
        };

    private static Dictionary<uint, QuestEvaluation> States(QuestCatalog catalog) =>
        catalog.All.ToDictionary(q => q.RowId, _ => new QuestEvaluation(QuestState.Blocked, [], null, null, null));
}
