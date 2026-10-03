using System.Collections.Frozen;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Chains;

/// <summary>
/// The Path card's chain line (feature plan v6 U5): what to do next in the chain seen from the quest shown, never the
/// quest itself, with the totals only in the bar's tooltip.
/// </summary>
public sealed class ChainLineTests
{
    private const uint A = 65600;
    private const uint B = 65601;
    private const uint C = 65602;
    private const uint D = 65603;
    private const uint E = 65604;

    private static IReadOnlyDictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] states) =>
        states.ToDictionary(s => s.RowId, s => new QuestEvaluation(s.State, [], null, null, null));

    [Fact]
    public void The_open_quest_points_at_the_next_one_after_it_not_at_itself()
    {
        // The old line read "next: C" on C's own page.
        var chain = new Chain("Newfound Adventure", [A, B, C, D, E]);

        var line = ChainLine.For(chain, C, States((A, QuestState.Completed), (B, QuestState.Completed), (C, QuestState.Ready)));

        Assert.Equal(new ChainLine(ChainNextKind.Next, D, 2, 5), line);
        Assert.Equal("Next in chain:", line!.Value.Label);
        Assert.Equal("2 of 5 quests done", line.Value.Tooltip);
        Assert.Equal(0.4f, line.Value.Fraction);
    }

    [Fact]
    public void A_skipped_earlier_quest_wins_over_the_next_one()
    {
        var chain = new Chain("Hildibrand", [A, B, C, D]);

        var line = ChainLine.For(chain, C, States((A, QuestState.Completed), (B, QuestState.Ready), (C, QuestState.Completed)));

        Assert.Equal(new ChainLine(ChainNextKind.EarlierOpen, B, 2, 4), line);
        Assert.Equal("Still open earlier:", line!.Value.Label);
    }

    [Fact]
    public void A_done_quest_points_at_the_next_open_one()
    {
        var chain = new Chain("Story", [A, B, C]);

        var line = ChainLine.For(chain, A, States((A, QuestState.Completed), (B, QuestState.Completed)));

        Assert.Equal(new ChainLine(ChainNextKind.Next, C, 2, 3), line);
    }

    [Fact]
    public void The_only_quest_left_links_nowhere()
    {
        var chain = new Chain("Story", [A, B, C]);

        var line = ChainLine.For(chain, C, States((A, QuestState.Completed), (B, QuestState.Completed), (C, QuestState.Accepted)));

        Assert.Equal(new ChainLine(ChainNextKind.Last, null, 2, 3), line);
        Assert.Equal("Last quest in this chain", line!.Value.Label);
    }

    [Fact]
    public void A_finished_chain_says_so_with_the_total_on_hover()
    {
        var chain = new Chain("Story", [A, B]);

        var line = ChainLine.For(chain, B, States((A, QuestState.Completed), (B, QuestState.Completed)));

        Assert.Equal(new ChainLine(ChainNextKind.Complete, null, 2, 2), line);
        Assert.Equal("Chain complete", line!.Value.Label);
        Assert.Equal("All 2 quests done", line.Value.Tooltip);
        Assert.Equal(1f, line.Value.Fraction);
    }

    [Fact]
    public void Repeatables_and_locked_out_quests_are_never_linked_or_counted()
    {
        var chain = new Chain("Relic", [A, B, C, D]) { Uncounted = new HashSet<uint> { B }.ToFrozenSet() };

        var line = ChainLine.For(chain, A, States((A, QuestState.Ready), (B, QuestState.Ready), (C, QuestState.Foreclosed), (D, QuestState.Blocked)));

        Assert.Equal(new ChainLine(ChainNextKind.Next, D, 0, 2), line);
    }

    [Fact]
    public void Nothing_counted_means_no_line()
    {
        var chain = new Chain("Another city's festival", [A, B]);

        Assert.Null(ChainLine.For(chain, A, States((A, QuestState.Foreclosed), (B, QuestState.Foreclosed))));
    }

    [Fact]
    public void A_quest_the_chain_does_not_list_sees_every_open_quest_as_next()
    {
        var chain = new Chain("Story", [A, B, C]);

        var line = ChainLine.For(chain, E, States((A, QuestState.Completed)));

        Assert.Equal(new ChainLine(ChainNextKind.Next, B, 1, 3), line);
    }
}
