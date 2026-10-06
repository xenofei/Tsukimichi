using System.Globalization;
using System.Text;
using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Level format version 2 (plan v9 decision 16): <c>canBeGreen</c> per piece, <c>scene</c> official, and the deal that keeps them.</summary>
public sealed class MoonfallLevelFormatV2Tests
{
    /// <summary>A level file of <paramref name="rows"/> rows of 15 round pegs, with <paramref name="extra"/> written into each peg (",\"canBeGreen\": false").</summary>
    private static string File(int version, int rows = 4, Func<int, string>? extra = null, string? scene = null)
    {
        var json = new StringBuilder();
        json.Append(CultureInfo.InvariantCulture, $$"""{ "format": "moonfall-level", "version": {{version}}, "id": "v2-test", "name": "Format two", """);
        if (scene is not null)
        {
            json.Append(CultureInfo.InvariantCulture, $"\"scene\": \"{scene}\", ");
        }

        json.Append("""  "playfield": { "width": 800, "height": 600 }, "pegs": [""");
        for (var i = 0; i < rows * 15; i++)
        {
            json.Append(i == 0 ? string.Empty : ",");
            json.Append(CultureInfo.InvariantCulture, $$"""{ "x": {{110 + ((i % 15) * 40)}}, "y": {{220 + ((i / 15) * 45)}}{{extra?.Invoke(i) ?? string.Empty}} }""");
        }

        json.Append("] }");
        return json.ToString();
    }

    [Fact]
    public void Version_2_reads_canBeGreen_per_peg_and_brick()
    {
        var load = MoonfallLevelLoader.Parse(File(2, extra: i => i % 3 == 0 ? ", \"canBeGreen\": false" : string.Empty));
        Assert.True(load.Ok, string.Join("; ", load.Errors));
        var pegs = load.Level!.Pegs;
        Assert.All(pegs.Where((_, i) => i % 3 == 0), p => Assert.False(p.CanBeGreen));
        Assert.All(pegs.Where((_, i) => i % 3 != 0), p => Assert.True(p.CanBeGreen));

        var brick = MoonfallLevelLoader.Parse(File(2).Replace("] }", """], "bricks": [ { "kind": "line", "x1": 120, "y1": 420, "x2": 200, "y2": 420, "canBeGreen": false } ] }""", StringComparison.Ordinal));
        Assert.True(brick.Ok, string.Join("; ", brick.Errors));
        Assert.False(brick.Level!.Pegs[^1].CanBeGreen);
    }

    [Fact]
    public void Version_1_ignores_canBeGreen_and_scene_as_unknown_properties()
    {
        var load = MoonfallLevelLoader.Parse(File(1, extra: static _ => ", \"canBeGreen\": false", scene: "moon-road-night"));
        Assert.True(load.Ok, string.Join("; ", load.Errors));
        Assert.All(load.Level!.Pegs, static p => Assert.True(p.CanBeGreen));
        Assert.Null(load.Level.Scene);
    }

    [Fact]
    public void Scene_is_official_in_version_2()
    {
        var load = MoonfallLevelLoader.Parse(File(2, scene: "moon-road-night"));
        Assert.Equal("moon-road-night", load.Level!.Scene);
        Assert.False(MoonfallLevelLoader.Parse(File(2, scene: "../escape")).Ok);
        Assert.Equal(2, MoonfallRules.LevelFormatVersion);
    }

    [Fact]
    public void A_canBeGreen_that_is_not_true_or_false_is_refused()
    {
        var load = MoonfallLevelLoader.Parse(File(2, extra: static i => i == 4 ? ", \"canBeGreen\": \"no\"" : string.Empty));
        Assert.False(load.Ok);
        Assert.Contains(load.Errors, static e => e.Contains("pegs[4]", StringComparison.Ordinal) && e.Contains("canBeGreen", StringComparison.Ordinal));
    }

    [Fact]
    public void A_level_whose_oranges_could_take_every_green_is_refused()
    {
        // 60 pegs: 26 may be green, all of them may be orange too, so 25 oranges could leave 1 green: too few.
        var tooFew = MoonfallLevelLoader.Parse(File(2, extra: static i => i < 26 ? string.Empty : ", \"canBeGreen\": false"));
        Assert.False(tooFew.Ok);
        Assert.Contains(tooFew.Errors, static e => e.Contains("green", StringComparison.Ordinal));

        // Two pegs that may be green but never orange always leave two greens.
        var enough = MoonfallLevelLoader.Parse(File(2, extra: static i => i < 2 ? ", \"canBeOrange\": false" : i < 4 ? string.Empty : ", \"canBeGreen\": false"));
        Assert.True(enough.Ok, string.Join("; ", enough.Errors));
        Assert.Equal(2, enough.Level!.GreenCandidatesAtWorst);
    }

    [Fact]
    public void Greens_never_land_on_a_peg_that_may_not_be_green()
    {
        var level = MoonfallLevelLoader.Parse(File(2, extra: static i => i % 4 == 0 ? ", \"canBeOrange\": false" : ", \"canBeGreen\": false")).Level!;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var game = new MoonfallGame(level, 5, seed);
            var greens = Enumerable.Range(0, game.PegCount).Where(i => game.Peg(i).Colour == PegColour.Green).ToList();
            Assert.Equal(MoonfallRules.GreenCount, greens.Count);
            Assert.All(greens, static i => Assert.Equal(0, i % 4));
        }
    }

    [Fact]
    public void The_deal_is_deterministic_and_unchanged_for_a_level_whose_pegs_all_may_be_green()
    {
        // A version 1 file and the same pegs in version 2 with canBeGreen written out true deal the same colours, seed
        // for seed: the flag's default changes no shipped board.
        var v1 = MoonfallLevelLoader.Parse(File(1)).Level!;
        var v2 = MoonfallLevelLoader.Parse(File(2, extra: static _ => ", \"canBeGreen\": true")).Level!;
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var a = new MoonfallGame(v1, 5, seed);
            var b = new MoonfallGame(v2, 5, seed);
            var again = new MoonfallGame(v2, 5, seed);
            for (var i = 0; i < a.PegCount; i++)
            {
                Assert.Equal(a.Peg(i).Colour, b.Peg(i).Colour);
                Assert.Equal(b.Peg(i).Colour, again.Peg(i).Colour);
            }
        }
    }

    [Fact]
    public void The_purple_never_takes_a_green_and_moves_among_the_blues()
    {
        var level = MoonfallLevelLoader.Parse(File(2, extra: static i => i % 5 == 0 ? ", \"canBeOrange\": false" : ", \"canBeGreen\": false")).Level!;
        var game = new MoonfallGame(level, 5, 77);
        Assert.True(game.PurplePeg >= 0);
        Assert.Equal(PegColour.Purple, game.Peg(game.PurplePeg).Colour);
        Assert.Equal(MoonfallRules.GreenCount, Enumerable.Range(0, game.PegCount).Count(i => game.Peg(i).Colour == PegColour.Green));
    }

    [Fact]
    public void The_shipped_levels_deal_their_greens_only_where_canBeGreen_lets_them()
    {
        // The level pipeline marks each subject's crowns and key features never green (format v2): the engine's own deal,
        // at every level number that deals greens, puts its greens on the pieces the file lets be green and nowhere else.
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var levels = campaigns.Base.Levels.Concat(campaigns.Expansion.Levels).ToList();
        Assert.Contains(levels, static l => l.Pegs.Any(static p => !p.CanBeGreen));
        foreach (var level in levels)
        {
            for (ulong seed = 1; seed <= 120; seed++)
            {
                var game = new MoonfallGame(level, MoonfallRules.FirstGreenLevel + (int)(seed % 3), seed);
                var greens = Enumerable.Range(0, game.PegCount).Where(i => game.Peg(i).Colour == PegColour.Green).ToList();
                Assert.Equal(MoonfallRules.GreenCount, greens.Count);
                Assert.All(greens, i => Assert.True(level.Pegs[i].CanBeGreen, $"{level.Id} seed {seed}: green on piece {i}"));
            }
        }
    }

    [Fact]
    public void Every_shipped_level_has_its_greens_whichever_oranges_are_picked()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        Assert.All(campaigns.Base.Levels.Concat(campaigns.Expansion.Levels), static l => Assert.True(l.GreenCandidatesAtWorst >= MoonfallRules.GreenCount, l.Id));
    }
}
