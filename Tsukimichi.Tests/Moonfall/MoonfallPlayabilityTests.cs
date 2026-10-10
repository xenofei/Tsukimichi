using System.Diagnostics;
using Tsukimichi.Core.Moonfall;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The playability gate (plan v9 decision 21): every shipped level wins at least 2 of the greedy player's 48 games (the owner's decision, 9 October 2026; 5 before).
/// Slow (the greedy player tries 85 shots before each of its own), so it carries its own category; the gates run it.
/// </summary>
public sealed class MoonfallPlayabilityTests
{
    private readonly ITestOutputHelper output;

    public MoonfallPlayabilityTests(ITestOutputHelper output) => this.output = output;

    /// <summary>Every shipped level's id, one test case each, so a failure names its level and a run can pick one.</summary>
    public static TheoryData<string> ShippedLevels()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var data = new TheoryData<string>();
        foreach (var level in campaigns.Base.Levels.Concat(campaigns.Expansion.Levels))
        {
            data.Add(level.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedLevels))]
    [Trait("Category", "Playability")]
    public void Every_shipped_level_passes_the_greedy_player(string id)
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        Assert.Empty(campaigns.Errors);
        var level = campaigns.Find(id);
        Assert.NotNull(level);
        var watch = Stopwatch.StartNew();
        var report = MoonfallPlayability.Check(level);
        output.WriteLine($"{id}: won {report.Wins} of {report.Games.Count} in {watch.Elapsed.TotalSeconds:0.00} s; Ace {MoonfallAces.For(id)?.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) ?? "none"}, the greedy player suggests {MoonfallAces.Suggest(report)?.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) ?? "none"} over these {report.Games.Count} games (set the Ace over {MoonfallAces.SuggestGames}); {level.OrangeCandidates} pegs may be orange");
        Assert.True(report.Passes, $"{id} won {report.Wins} of {report.Games.Count}; a level needs {MoonfallPlayability.WinsNeeded} (owner's decision, 9 October 2026)");
    }

    /// <summary>
    /// Re-suggests a shipped level's Ace over <see cref="MoonfallAces.SuggestGames"/> games (the rule's 576): every
    /// level's Ace is the score the top 40% of its wins reach (the owner's decision, 9 October 2026), so each shipped Ace
    /// must equal that suggestion. Slow, so it is a
    /// Perf test and runs on request: <c>dotnet test --filter "FullyQualifiedName~Shipped_Aces_are_unchanged"</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ShippedLevels))]
    [Trait("Category", "Perf")]
    public void Shipped_Aces_are_unchanged_by_the_hard_level_rule(string id)
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Find(id);
        Assert.NotNull(level);
        var report = MoonfallPlayability.Check(level, MoonfallAces.SuggestGames);
        var suggested = MoonfallAces.Suggest(report);
        output.WriteLine($"{id}: won {report.Wins} of {report.Games.Count}; Ace {MoonfallAces.For(id)}, suggested {suggested}");
        Assert.Equal(MoonfallAces.For(id), suggested);
    }

    /// <summary>
    /// The port plays the designer's tool's games move for move: these are <c>mfcheck play base-01.json 48 5</c>'s first
    /// two games (both lost after the 10 October re-tune), its fifth and its total, run on 9 October 2026 against this engine on the
    /// original's rules (the owner's Peggle decision: style bonuses no longer give free balls, the ±97° aim and the rest; on
    /// 6 October the totals were 30 of 48) and re-run the same day on base-01's Peggle refit (98 pieces), so a change that
    /// moves the greedy player's verdicts shows here before it moves a level's.
    /// </summary>
    [Fact]
    [Trait("Category", "Playability")]
    public void The_port_plays_the_design_tools_games()
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        Assert.Equal("base-01", level.Id);
        var first = MoonfallPlayability.PlayOne(level, 0);
        Assert.False(first.Won);
        Assert.Equal(13, first.Shots);
        Assert.Equal(1, first.OrangesLeft);
        Assert.Equal(57_990, first.Score);
        var second = MoonfallPlayability.PlayOne(level, 1);
        Assert.False(second.Won);
        Assert.Equal(12, second.Shots);
        Assert.Equal(2, second.OrangesLeft);
        Assert.Equal(56_090, second.Score);
        var fifth = MoonfallPlayability.PlayOne(level, 4);
        Assert.False(fifth.Won);
        Assert.Equal(11, fifth.Shots);
        Assert.Equal(1, fifth.OrangesLeft);
        Assert.Equal(118_700, fifth.Score);
        Assert.Equal(5, MoonfallPlayability.Check(level).Wins);
    }

    [Fact]
    public void A_level_passes_on_two_wins_of_forty_eight()
    {
        // The floor is 2 of 48 (owner's decision, 9 October 2026, "Follow Peggle, lower the floor"; it was 5).
        var won = new MoonfallGreedyGame(true, 12, 0, 200_000, 0);
        var lost = new MoonfallGreedyGame(false, 15, 3, 90_000, 0);
        var one = Enumerable.Repeat(won, 1).Concat(Enumerable.Repeat(lost, 47)).ToList();
        var two = Enumerable.Repeat(won, 2).Concat(Enumerable.Repeat(lost, 46)).ToList();
        Assert.False(new MoonfallPlayabilityReport("x", one).Passes);
        Assert.True(new MoonfallPlayabilityReport("x", two).Passes);
        Assert.Equal(2, MoonfallPlayability.WinsNeeded);
        Assert.Equal(48, MoonfallPlayability.Games);
    }
}
