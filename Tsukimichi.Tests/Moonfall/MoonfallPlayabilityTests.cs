using System.Diagnostics;
using Tsukimichi.Core.Moonfall;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The playability gate (plan v9 decision 21): every shipped level wins at least 5 of the greedy player's 48 games.
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
        Assert.True(report.Passes, $"{id} won {report.Wins} of {report.Games.Count}; a level needs {MoonfallPlayability.WinsNeeded} (plan v9 decision 21)");
    }

    /// <summary>
    /// The port plays the designer's tool's games move for move: these are <c>mfcheck play base-01.json 48 5</c>'s first
    /// two games, its tenth (the first it wins) and its total (run on 6 October 2026 against this engine, on the level
    /// pipeline's Road to Horizon), so a change that moves the greedy player's verdicts shows here before it moves a level's.
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
        Assert.Equal(89_760, first.Score);
        var second = MoonfallPlayability.PlayOne(level, 1);
        Assert.False(second.Won);
        Assert.Equal(17, second.Shots);
        Assert.Equal(1, second.OrangesLeft);
        Assert.Equal(172_800, second.Score);
        var tenth = MoonfallPlayability.PlayOne(level, 9);
        Assert.True(tenth.Won);
        Assert.Equal(11, tenth.Shots);
        Assert.Equal(328_750, tenth.Score);
        Assert.Equal(30, MoonfallPlayability.Check(level).Wins);
    }

    [Fact]
    public void A_level_passes_on_five_wins_of_forty_eight()
    {
        var won = new MoonfallGreedyGame(true, 12, 0, 200_000, 0);
        var lost = new MoonfallGreedyGame(false, 15, 3, 90_000, 0);
        var four = Enumerable.Repeat(won, 4).Concat(Enumerable.Repeat(lost, 44)).ToList();
        var five = Enumerable.Repeat(won, 5).Concat(Enumerable.Repeat(lost, 43)).ToList();
        Assert.False(new MoonfallPlayabilityReport("x", four).Passes);
        Assert.True(new MoonfallPlayabilityReport("x", five).Passes);
        Assert.Equal(48, MoonfallPlayability.Games);
    }
}
