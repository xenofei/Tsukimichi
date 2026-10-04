using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Text;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Text;

/// <summary>
/// <c>/tsuki msq</c>, <c>next</c> and <c>go</c> (plan v7, 1.21.0 P8 and P2) never shadow a quest name: text after
/// <c>msq</c> or <c>next</c> is the search it was, and a <c>go</c> line that begins a quest's own name ("Go West,
/// Craftsman", "Go with the Flow") searches; over the frozen catalog. Also the pick order behind <c>next</c>.
/// </summary>
public sealed class GuidanceRoutingTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static bool NoQuestBegins(string text) => false;

    [Theory]
    [InlineData("msq", Subcommand.Msq)]
    [InlineData("MSQ", Subcommand.Msq)]
    [InlineData("next", Subcommand.Next)]
    [InlineData("go", Subcommand.Go)]
    [InlineData("Go", Subcommand.Go)]
    public void The_words_name_their_subcommands(string line, Subcommand expected)
    {
        var parsed = CommandLine.Parse(line);
        Assert.Equal(expected, parsed.Kind);
        Assert.True(CommandLine.RunsGuidance(parsed, NoQuestBegins));
        Assert.Contains(line.ToLowerInvariant(), CommandLine.ListedWords);
    }

    [Theory]
    [InlineData("next stop is Limsa")]
    [InlineData("msq quests")]
    public void Text_after_msq_or_next_is_a_search(string line)
    {
        Assert.False(CommandLine.RunsGuidance(CommandLine.Parse(line), NoQuestBegins));
    }

    [Fact]
    public void Go_takes_a_quest_name()
    {
        var parsed = CommandLine.Parse("go Caught in the Act");
        Assert.True(CommandLine.RunsGuidance(parsed, NoQuestBegins));
        Assert.Equal("Caught in the Act", parsed.Rest);
    }

    [Fact]
    public void Other_subcommands_are_not_guidance()
    {
        Assert.False(CommandLine.RunsGuidance(CommandLine.Parse("look abc"), NoQuestBegins));
        Assert.False(CommandLine.RunsGuidance(CommandLine.Parse("Go West"), _ => true));
    }

    [Theory]
    [InlineData("go west")]
    [InlineData("Go West, Craftsman")]
    [InlineData("go with the flow")]
    [InlineData("go forth")]
    public void A_go_line_that_begins_a_quest_name_stays_a_search(string line)
    {
        var catalog = fixture.Bundle.Catalog;
        var parsed = CommandLine.Parse(line);
        Assert.Equal(Subcommand.Go, parsed.Kind);
        Assert.False(CommandLine.RunsGuidance(parsed, text => CommandLine.BeginsQuestName(catalog, null, text)), line);
    }

    [Theory]
    [InlineData("go Caught in the Act")]
    [InlineData("go The Long Road to Xak Tural")]
    public void A_go_line_naming_another_quest_goes(string line)
    {
        var catalog = fixture.Bundle.Catalog;
        Assert.True(CommandLine.RunsGuidance(CommandLine.Parse(line), text => CommandLine.BeginsQuestName(catalog, null, text)), line);
    }

    [Fact]
    public void No_quest_is_named_msq_next_or_go()
    {
        foreach (var quest in fixture.Bundle.Catalog.All)
        {
            Assert.False(quest.Name.Equals("msq", StringComparison.OrdinalIgnoreCase), quest.Name);
            Assert.False(quest.Name.Equals("next", StringComparison.OrdinalIgnoreCase), quest.Name);
            Assert.False(quest.Name.Equals("go", StringComparison.OrdinalIgnoreCase), quest.Name);
            Assert.False(quest.Name.StartsWith("next ", StringComparison.OrdinalIgnoreCase) || quest.Name.StartsWith("msq ", StringComparison.OrdinalIgnoreCase), quest.Name);
        }
    }

    // ------------------------------------------------------------------ the pick

    private static QuestEvaluation State(QuestState state) => new(state, [], null, null, null);

    [Fact]
    public void The_pick_follows_up_nexts_order()
    {
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = State(QuestState.Ready),
            [2] = State(QuestState.Accepted),
            [3] = State(QuestState.Ready),
            [4] = State(QuestState.Ready),
            [5] = State(QuestState.Blocked),
        };

        Assert.Equal((1u, GuidanceReason.Route), GuidancePick.Pick(states, 1, 2, [3], [4]));
        Assert.Equal((2u, GuidanceReason.MainScenario), GuidancePick.Pick(states, 5, 2, [3], [4]));
        Assert.Equal((3u, GuidanceReason.Pinned), GuidancePick.Pick(states, null, 5, [5, 3], [4]));
        Assert.Equal((4u, GuidanceReason.ClosestStop), GuidancePick.Pick(states, null, null, [5], [5, 4]));
        Assert.Null(GuidancePick.Pick(states, null, 5, [5], [5]));
    }

    [Fact]
    public void Ready_on_another_job_is_counted()
    {
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = State(QuestState.ReadyOnOtherJob),
            [2] = State(QuestState.ReadyOnOtherJob),
            [3] = State(QuestState.Ready),
        };

        Assert.Equal(2, GuidancePick.ReadyOnOtherJob(states));
    }

    [Fact]
    public void Msq_left_counts_what_is_left_in_the_part_and_to_the_latest_story()
    {
        var catalog = fixture.Bundle.Catalog;
        var story = MsqGraph.For(catalog).Story;
        var first = story.First(q => q.Expansion == 5);
        var at = story.ToList().IndexOf(first);
        var states = new Dictionary<uint, QuestEvaluation>();
        foreach (var quest in catalog.All)
        {
            states[quest.RowId] = State(QuestState.Blocked);
        }

        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = State(i < at ? QuestState.Completed : i == at ? QuestState.Ready : QuestState.Blocked);
        }

        var left = MsqLeft.For(catalog, states);
        Assert.NotNull(left);
        Assert.Equal(first.RowId, left.Next.RowId);
        Assert.Equal(first.Journal.GenreName, left.Part);
        Assert.Equal(story.Count - at, left.LeftToLatest);
        Assert.InRange(left.LeftInPart, 1, left.LeftToLatest);
        Assert.True(left.MinLevel <= left.MaxLevel);

        // Everything done: caught up.
        foreach (var quest in story)
        {
            states[quest.RowId] = State(QuestState.Completed);
        }

        Assert.Null(MsqLeft.For(catalog, states));
    }

    [Fact]
    public void Msq_left_leaves_out_the_grand_companies_spare_alternatives()
    {
        // 1.21.0 review: before choosing a Grand Company, "The Company You Keep" stands three times in the story; the
        // two the character will not take are spare alternatives and leave the totals, as Your story counts them.
        uint[] companies = [66216, 66217, 66218];
        var catalog = fixture.Bundle.Catalog;
        var story = MsqGraph.For(catalog).Story;
        var at = story.ToList().FindIndex(q => companies.Contains(q.RowId));
        Assert.True(at > 0);
        var states = new Dictionary<uint, QuestEvaluation>();
        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = State(i < at ? QuestState.Completed : QuestState.Blocked);
        }

        states[companies[0]] = State(QuestState.Ready);
        states[companies[1]] = State(QuestState.Ready) with { IsSpareAlternative = true };
        states[companies[2]] = State(QuestState.Ready) with { IsSpareAlternative = true };

        var position = MsqProgress.Compute(catalog, states);
        var left = MsqLeft.For(catalog, states);
        Assert.NotNull(position);
        Assert.NotNull(left);
        Assert.Equal(position.Total - position.Done, left.LeftToLatest);
        Assert.Equal(MsqGraph.For(catalog).QuestsLeft(states).Count, left.LeftToLatest);
        Assert.DoesNotContain(MsqGraph.For(catalog).QuestsLeft(states), q => q.RowId is 66217 or 66218);
    }
}
