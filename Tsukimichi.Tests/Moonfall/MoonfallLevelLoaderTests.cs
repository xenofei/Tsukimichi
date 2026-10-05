using System.Globalization;
using System.Text;
using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>The level format and its checks (plan v9 G1, G6), and the shipped starter levels.</summary>
public sealed class MoonfallLevelLoaderTests
{
    /// <summary>A valid level: 30 pegs in rows, then whatever <paramref name="extra"/> adds to the root object.</summary>
    private static string Level(string pegs = "", string bricks = "", string root = "", int count = 30)
    {
        var text = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            text.Append(CultureInfo.InvariantCulture, $"{{ \"x\": {120 + ((i % 10) * 55)}, \"y\": {250 + ((i / 10) * 60)} }}");
        }

        if (pegs.Length > 0)
        {
            text.Append(',').Append(pegs);
        }

        return $$"""
            {
              "format": "moonfall-level",
              "version": 1,
              "id": "test-01",
              "name": "Test Level",
              "playfield": { "width": 800, "height": 600 },
              "pegs": [ {{text}} ],
              "bricks": [ {{bricks}} ]
              {{root}}
            }
            """;
    }

    [Fact]
    public void A_valid_level_reads_with_its_pegs_bricks_and_movers()
    {
        var load = MoonfallLevelLoader.Parse(Level(
            pegs: """{ "x": 400, "y": 480, "r": 8, "canBeOrange": false, "move": { "kind": "orbit", "x": 400, "y": 450, "period": 5, "clockwise": false } }""",
            bricks: """
                { "kind": "line", "x1": 150, "y1": 450, "x2": 180, "y2": 455 },
                // a comment, and a trailing comma
                { "kind": "arc", "x": 600, "y": 470, "r": 60, "start": 200, "sweep": 40, "thickness": 16 },
                """));
        Assert.True(load.Ok, string.Join("\n", load.Errors));
        var level = load.Level!;
        Assert.Equal("test-01", level.Id);
        Assert.Equal("Test Level", level.Name);
        Assert.Equal(33, level.Pegs.Count);
        Assert.Equal(32, level.OrangeCandidates);
        var mover = level.Pegs[30];
        Assert.Equal(PegShape.Round, mover.Shape);
        Assert.Equal(8, mover.Radius);
        Assert.Equal(MoverKind.Orbit, mover.Mover.Kind);
        Assert.False(mover.Mover.Clockwise);
        Assert.Equal(PegShape.Line, level.Pegs[31].Shape);
        Assert.Equal(MoonfallRules.BrickThickness, level.Pegs[31].Thickness);
        Assert.Equal(PegShape.Arc, level.Pegs[32].Shape);
        Assert.Equal(16, level.Pegs[32].Thickness);
    }

    [Theory]
    [InlineData("", "the file is empty")]
    [InlineData("[1, 2]", "not a JSON object")]
    [InlineData("{ nope", "not JSON")]
    [InlineData("""{ "format": "peg-level", "version": 1 }""", "format must be")]
    [InlineData("""{ "format": "moonfall-level", "version": 2 }""", "newer Moonfall")]
    [InlineData("""{ "format": "moonfall-level", "version": "1" }""", "version is missing")]
    public void A_file_that_is_no_level_is_refused(string json, string reason)
    {
        var load = MoonfallLevelLoader.Parse(json);
        Assert.False(load.Ok);
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "y": 300 }""", "pegs[30]: x is missing")]
    [InlineData("""{ "x": 400, "y": 300, "r": 30 }""", "r must be 6 to 20")]
    [InlineData("""{ "x": 70, "y": 300 }""", "off the board")]
    [InlineData("""{ "x": 400, "y": 555 }""", "off the board")]
    [InlineData("""{ "x": 400, "y": 150 }""", "off the board")]
    [InlineData("""{ "x": 125, "y": 252 }""", "pegs[0] overlaps pegs[30]")]
    [InlineData("""{ "x": 400, "y": 480, "move": { "kind": "spin", "x": 0, "y": 0, "period": 3 } }""", "kind must be \"orbit\" or \"slide\"")]
    [InlineData("""{ "x": 400, "y": 480, "move": { "kind": "slide", "x": 450, "y": 480, "period": 0.5 } }""", "period must be 1 to 60")]
    [InlineData("""{ "x": 600, "y": 480, "move": { "kind": "slide", "x": 740, "y": 480, "period": 4 } }""", "moves off the board")]
    [InlineData("""{ "x": 400, "y": 480, "canBeOrange": "yes" }""", "canBeOrange must be true or false")]
    public void A_bad_peg_is_named(string peg, string reason)
    {
        var load = MoonfallLevelLoader.Parse(Level(pegs: peg));
        Assert.False(load.Ok);
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "kind": "box", "x": 1 }""", "kind must be \"line\" or \"arc\"")]
    [InlineData("""{ "kind": "line", "x1": 200, "y1": 450, "x2": 201, "y2": 450 }""", "at least 4 long")]
    [InlineData("""{ "kind": "line", "x1": 200, "y1": 450, "x2": 260, "y2": 450, "thickness": 40 }""", "thickness must be 8 to 30")]
    [InlineData("""{ "kind": "arc", "x": 400, "y": 450, "r": 60, "start": 0, "sweep": 400 }""", "sweep must be above 0")]
    [InlineData("""{ "kind": "arc", "x": 400, "y": 450, "r": 10, "start": 0, "sweep": 90 }""", "r must be 20 to 400")]
    [InlineData("""{ "kind": "line", "x1": 600, "y1": 540, "x2": 700, "y2": 556 }""", "off the board")]
    [InlineData("""{ "kind": "line", "x1": 100, "y1": 250, "x2": 160, "y2": 250 }""", "pegs[0] overlaps bricks[0]")]
    public void A_bad_brick_is_named(string brick, string reason)
    {
        var load = MoonfallLevelLoader.Parse(Level(bricks: brick));
        Assert.False(load.Ok);
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void A_level_needs_25_pegs_that_may_be_orange()
    {
        var json = Level().Replace("\"y\": 250 }", "\"y\": 250, \"canBeOrange\": false }", StringComparison.Ordinal);
        var load = MoonfallLevelLoader.Parse(json);
        Assert.Contains(load.Errors, e => e.Contains("20 pegs may be orange; a level needs 25", StringComparison.Ordinal));
    }

    [Fact]
    public void A_level_needs_enough_pegs_and_not_too_many()
    {
        Assert.Contains(MoonfallLevelLoader.Parse(Level(count: 20)).Errors, e => e.Contains("29 to 400", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"id\": \"test-01\"", "\"id\": \"Test 01\"", "id must be")]
    [InlineData("\"name\": \"Test Level\"", "\"name\": \"\"", "name must be")]
    [InlineData("\"width\": 800", "\"width\": 1024", "playfield must be")]
    public void The_header_is_checked(string from, string to, string reason)
    {
        var load = MoonfallLevelLoader.Parse(Level().Replace(from, to, StringComparison.Ordinal));
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void An_oversized_level_is_refused_unread_with_one_error()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var load = MoonfallLevelLoader.Parse(Level(count: 5000));
        Assert.False(load.Ok);
        Assert.Equal(["a level holds 29 to 400 pegs and bricks; this one has 5000"], load.Errors);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
    }

    [Fact]
    public void The_error_list_is_capped()
    {
        // 300 pegs all missing x: the first 40 errors are listed, the rest counted.
        var bad = string.Join(",", Enumerable.Repeat("""{ "y": 300 }""", 300));
        var load = MoonfallLevelLoader.Parse(Level(pegs: bad));
        Assert.False(load.Ok);
        Assert.InRange(load.Errors.Count, MoonfallLevelLoader.MaxErrors, MoonfallLevelLoader.MaxErrors + 2);
    }

    [Fact]
    public void A_mover_that_passes_through_a_still_peg_is_refused()
    {
        // A still peg at (450, 440); a mover sliding from (450, 480) to (450, 400) runs straight through it.
        var load = MoonfallLevelLoader.Parse(Level(pegs: """{ "x": 450, "y": 440 }, { "x": 450, "y": 480, "move": { "kind": "slide", "x": 450, "y": 400, "period": 4 } }"""));
        Assert.Contains("pegs[31] moves into pegs[30]", load.Errors);
    }

    [Fact]
    public void A_mover_that_pinches_a_brick_is_refused()
    {
        var load = MoonfallLevelLoader.Parse(Level(
            pegs: """{ "x": 600, "y": 470, "move": { "kind": "orbit", "x": 560, "y": 470, "period": 6 } }""",
            bricks: """{ "kind": "line", "x1": 540, "y1": 500, "x2": 560, "y2": 520 }"""));
        Assert.Contains("pegs[30] moves into bricks[0]", load.Errors);
    }

    [Fact]
    public void A_mover_faster_than_the_cap_is_refused()
    {
        // An orbit of radius 80 once a second runs at 503 px/s.
        var load = MoonfallLevelLoader.Parse(Level(pegs: """{ "x": 480, "y": 450, "move": { "kind": "orbit", "x": 400, "y": 450, "period": 1 } }"""));
        Assert.Contains(load.Errors, e => e.Contains("moves faster than 420 px/s", StringComparison.Ordinal));
    }

    [Fact]
    public void A_long_arc_is_checked_along_its_length_so_its_middle_cannot_bulge_past_the_floor()
    {
        // r = 400 from 60.9 to 120.9 degrees about (400, 150.03): its lowest point, between where 33 even samples would fall,
        // reaches 0.03 px past the lowest edge a brick may reach. Sampled every 2 px of length, it is caught.
        var load = MoonfallLevelLoader.Parse(Level(bricks: """{ "kind": "arc", "x": 400, "y": 150.03, "r": 400, "start": 60.9375, "sweep": 60 }"""));
        Assert.Contains(load.Errors, e => e.StartsWith("bricks[0]: is off the board", StringComparison.Ordinal));
        Assert.True(MoonfallLevelLoader.Parse(Level(bricks: """{ "kind": "arc", "x": 400, "y": 149.9, "r": 400, "start": 60.9375, "sweep": 60 }""")).Ok);
    }

    [Fact]
    public void Unknown_properties_are_ignored()
    {
        Assert.True(MoonfallLevelLoader.Parse(Level(root: ", \"author\": \"someone\", \"editor\": { \"grid\": 10 }")).Ok);
    }

    [Fact]
    public void The_shipped_base_levels_all_load_with_original_names()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        Assert.Empty(campaigns.Errors);
        Assert.InRange(campaigns.Base.Levels.Count, 3, 55);
        Assert.Empty(campaigns.Expansion.Levels);
        Assert.Equal(campaigns.Base.Levels.Count, campaigns.Base.Levels.Select(l => l.Id).Distinct().Count());
        Assert.Equal(campaigns.Base.Levels.Count, campaigns.Base.Levels.Select(l => l.Name).Distinct().Count());
        Assert.All(campaigns.Base.Levels, l => Assert.StartsWith("base-", l.Id, StringComparison.Ordinal));
        Assert.All(campaigns.Base.Levels, l => Assert.True(l.OrangeCandidates >= 25));
        Assert.Contains(campaigns.Base.Levels, l => l.Pegs.Any(p => p.Shape != PegShape.Round));
        Assert.Contains(campaigns.Base.Levels, l => l.Pegs.Any(p => p.Mover.Kind != MoverKind.None));
    }
}
