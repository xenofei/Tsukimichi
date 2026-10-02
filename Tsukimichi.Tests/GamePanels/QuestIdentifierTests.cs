using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.GamePanels;

public class QuestIdentifierTests
{
    [Theory]
    [InlineData("It's Probably Pirates", "It’s Probably Pirates")]
    [InlineData("  Close   to Home ", "close to home")]
    [InlineData("Ein Rätsel", "Ein Rät­sel")]
    [InlineData("Les Enfants d'Azys Lla !", "Les Enfants d’Azys Lla !")]
    [InlineData("新たなる冒険", "新たなる冒険")]
    [InlineData("ＡＢＣ　クエスト", "abc クエスト")]
    [InlineData("Close to Home", " Close to Home")]
    [InlineData("A “Hero” – Again", "a \"hero\" - again")]
    public void Normalize_makes_window_text_and_sheet_names_compare_equal(string a, string b)
    {
        Assert.Equal(QuestTitles.Normalize(a), QuestTitles.Normalize(b));
    }

    [Fact]
    public void Normalize_of_nothing_is_empty()
    {
        Assert.Equal(string.Empty, QuestTitles.Normalize(null));
        Assert.Equal(string.Empty, QuestTitles.Normalize(" ­ "));
    }

    [Fact]
    public void A_candidate_with_the_title_wins_over_the_catalog()
    {
        var npcQuest = Quest(A, "Close to Home");
        var elsewhere = Quest(B, "Close to Home");
        var index = QuestTitleIndex.Build(Catalog(elsewhere, npcQuest));

        var match = QuestIdentifier.Identify(["Close to Home"], [npcQuest], index);

        Assert.Same(npcQuest, match.Quest);
        Assert.Equal(TitleMatchSource.Candidate, match.Source);
        Assert.Equal(1, match.Ambiguity);
    }

    [Fact]
    public void Without_a_candidate_the_title_is_matched_across_the_catalog_in_any_language()
    {
        var quest = Quest(A, "Les Enfants d’Azys Lla");
        var index = QuestTitleIndex.Build(Catalog(Quest(B, "Other"), quest));

        var match = QuestIdentifier.Identify([" Les Enfants d'Azys Lla"], [], index);

        Assert.Same(quest, match.Quest);
        Assert.Equal(TitleMatchSource.Catalog, match.Source);
    }

    [Fact]
    public void A_shared_title_is_settled_by_the_preference_then_by_order()
    {
        var limsa = Quest(A, "Call of the Sea");
        var gridania = Quest(B, "Call of the Sea");
        var index = QuestTitleIndex.Build(Catalog(limsa, gridania));

        var preferred = QuestIdentifier.Identify(["Call of the Sea"], [], index, q => q.RowId == B);
        var first = QuestIdentifier.Identify(["Call of the Sea"], [], index);

        Assert.Same(gridania, preferred.Quest);
        Assert.Equal(2, preferred.Ambiguity);
        Assert.Same(limsa, first.Quest);
    }

    [Fact]
    public void A_candidate_title_inside_a_short_label_is_found_but_not_inside_a_word()
    {
        var quest = Quest(A, "Dancing Wind");
        var other = Quest(B, "Wind");

        var match = QuestIdentifier.Identify(["Lv. 50 Dancing Wind"], [other, quest], null);
        var none = QuestIdentifier.Identify(["Windmill keeper"], [other], null);

        Assert.Same(quest, match.Quest);
        Assert.Equal(TitleMatchSource.Candidate, match.Source);
        Assert.Null(none.Quest);
    }

    [Fact]
    public void An_exact_title_in_the_catalog_wins_over_a_candidate_whose_title_is_only_part_of_it()
    {
        // The window offers "The Ties That Bind Us"; the targeted NPC's candidate is "The Ties That Bind". The shorter
        // title sits inside the window's title, but the exact one names the quest shown.
        var shorter = Quest(A, "The Ties That Bind");
        var offered = Quest(B, "The Ties That Bind Us");
        var index = QuestTitleIndex.Build(Catalog(shorter, offered));

        var match = QuestIdentifier.Identify(["The Ties That Bind Us"], [shorter], index);

        Assert.Same(offered, match.Quest);
        Assert.Equal(TitleMatchSource.Catalog, match.Source);
    }

    [Fact]
    public void Among_candidates_the_exact_title_wins_over_a_contained_one_listed_first()
    {
        var shorter = Quest(A, "The Ties That Bind");
        var offered = Quest(B, "The Ties That Bind Us");

        var match = QuestIdentifier.Identify(["The Ties That Bind Us"], [shorter, offered], null);

        Assert.Same(offered, match.Quest);
        Assert.Equal(TitleMatchSource.Candidate, match.Source);
    }

    [Fact]
    public void A_title_inside_the_journal_text_is_not_a_match()
    {
        var quest = Quest(A, "Close to Home");
        var text = "You will want to stay close to home for a while, until the sentries have finished their rounds and the gates are shut for the night.";

        var match = QuestIdentifier.Identify([text], [quest], null);

        Assert.Equal(TitleMatchSource.None, match.Source);
    }

    [Fact]
    public void Nothing_readable_or_no_known_title_is_not_found()
    {
        var index = QuestTitleIndex.Build(Catalog(Quest(A, "Close to Home")));

        Assert.Equal(TitleMatch.NotFound, QuestIdentifier.Identify([string.Empty, ""], [Quest(A, "Close to Home")], index));
        Assert.Equal(TitleMatch.NotFound, QuestIdentifier.Identify(["Something Else"], [], index));
    }

    [Fact]
    public void Retired_quests_are_not_in_the_title_index()
    {
        var retired = Quest(A, "Gone Quest") with { IsRetired = true };
        var index = QuestTitleIndex.Build(Catalog(retired, Quest(B, "Live Quest")));

        Assert.Empty(index.Find(QuestTitles.Normalize("Gone Quest")));
        Assert.Single(index.Find(QuestTitles.Normalize("Live Quest")));
        Assert.Equal(1, index.Count);
    }
}
