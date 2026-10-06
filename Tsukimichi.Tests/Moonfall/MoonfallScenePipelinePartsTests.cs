using System.Numerics;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The level pipeline's parts of the scene format (docs/design/v9/scene-recipe.md, "From the level pipeline"): erase
/// boxes, a squeezed crop, the poly, line and land mask terms, the palette's spared places, tone, plates and a shaft that
/// is only its beams. The converter (tools/moonfall-levels/mflkit/convert.py) writes them; its gate holds the game's
/// scene to the pipeline's, and these hold each part's arithmetic and the loader's limits.
/// </summary>
public sealed class MoonfallScenePipelinePartsTests
{
    private static string Shipped(string name) =>
        File.ReadAllText(Path.Combine(Tsukimichi.Tests.Ui.OrnamentLayoutTests.RepoRoot(), "Tsukimichi.Core", "Moonfall", "Levels", "scenes", name + ".json"));

    private static string Edited(string name, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(Shipped(name))!.AsObject();
        edit(node);
        return node.ToJsonString();
    }

    private static void Refused(string json, string reason)
    {
        var load = MoonfallSceneRecipeLoader.Parse(json);
        Assert.Null(load.Recipe);
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    private static MoonfallSceneRecipe Read(string json)
    {
        var load = MoonfallSceneRecipeLoader.Parse(json);
        Assert.True(load.Ok, string.Join("; ", load.Errors));
        return load.Recipe!;
    }

    /// <summary>A recipe on our own flat picture with these extra top-level fields (raw JSON, each with a leading comma).</summary>
    private static MoonfallSceneRecipe Flat(string fields) =>
        Read($$"""{ "format": "moonfall-scene", "version": 1, "name": "test-flat", "source": { "picture": "test-flat" } {{fields}} }""");

    private static MoonfallImage Grey(int w, int h, float v)
    {
        var image = new MoonfallImage(w, h);
        Array.Fill(image.R.Data, v);
        Array.Fill(image.G.Data, v);
        Array.Fill(image.B.Data, v);
        return image;
    }

    private static MoonfallLevel Level => MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];

    /// <summary>The red of the base layer at board (x, y) (the base covers the opening, 75-725 by 41-594).</summary>
    private static float BaseAt(MoonfallSceneLayers layers, float x, float y)
    {
        var b = layers.Base;
        var px = (int)((x - b.Board.X) * layers.Scale);
        var py = (int)((y - b.Board.Y) * layers.Scale);
        return b.Pixels[((py * b.Width) + px) * 4] / 255f;
    }

    // ---- Reading ----

    [Fact]
    public void A_converted_game_painting_reads_its_pipeline_parts()
    {
        var r = Read(Shipped("thanalan-road-chart"));
        Assert.Equal(4, r.Source.Erase.Count);
        Assert.Equal(new Vector4(1280, 860, 110, 135), r.Source.Erase[0]);
        var tone = Assert.Single(r.Paint.OfType<MoonfallTone>());
        var land = Assert.Single(tone.Where);
        Assert.Equal(MoonfallMaskKind.Land, land.Kind);
        Assert.True(land.Invert);
        Assert.Equal(2, r.Paint.OfType<MoonfallPlate>().Count());
        Assert.Contains(r.Light, static l => l is MoonfallShafts { Moving: true, BeamsOnly: true });
        Assert.Single(r.Light.OfType<MoonfallPlate>(), static p => p.Cover && p.Blend == MoonfallPlateBlend.Multiply);
        Assert.Single(r.Light.OfType<MoonfallPlate>(), static p => p.Rim && p.Blend == MoonfallPlateBlend.Screen);
        Assert.Equal(2, r.Motion.Glints.Count);
        Assert.Equal("moon-road-night", r.Fallback);
        Assert.Equal(
            ["thanalan-road-chart-route-cut", "thanalan-road-chart-route", "thanalan-road-chart-light", "thanalan-road-chart-ticks", "thanalan-road-chart-cover", "thanalan-road-chart-cloth", "thanalan-road-chart-rim"],
            MoonfallSceneBuilder.PlateNames(r));
    }

    [Fact]
    public void A_converted_painting_reads_its_masks_and_spared_places()
    {
        var r = Read(Shipped("sagolii-cactuar"));
        Assert.Equal(MoonfallSourceKind.Picture, r.Source.Kind);
        Assert.Null(r.Grade);
        var spare = Assert.Single(r.Palette!.Spare);
        Assert.Contains(spare, static t => t.Kind == MoonfallMaskKind.Poly && t.Points.Length >= 3);
        Assert.Contains(r.Palette.Regions.SelectMany(static g => g.Where), static t => t is { Kind: MoonfallMaskKind.Poly, Invert: true });
        Assert.NotNull(r.Moon);
        var crystal = Read(Shipped("crystal-call"));
        Assert.Contains(crystal.Palette!.Regions.SelectMany(static g => g.Where), static t => t.Kind == MoonfallMaskKind.Line && t.Points.Length >= 2);
    }

    [Fact]
    public void The_loader_refuses_the_parts_out_of_range()
    {
        Refused(Edited("thanalan-road-chart", static n => n["source"]!["erase"] = JsonNode.Parse("[[10, 10, 0, 5]]")), "w and h must be 1 to 1024");
        Refused(Edited("thanalan-road-chart", static n => n["source"]!["erase"] = new JsonArray([.. Enumerable.Range(0, 17).Select(static _ => JsonNode.Parse("[1, 1, 2, 2]"))])), "at most 16");
        Refused(Edited("thanalan-road-chart", static n => n["paint"]![0]!["where"] = JsonNode.Parse("""[{ "land": [1.5, 5] }]""")), ".land must be");
        Refused(Edited("thanalan-road-chart", static n => n["paint"]![0]!["where"] = JsonNode.Parse("""[{ "land": [0.7, 2.5] }]""")), ".land must be");
        Refused(Edited("thanalan-road-chart", static n => n["paint"]![0]!["where"] = JsonNode.Parse("""[{ "poly": [[0, 0], [10, 10]] }]""")), ".poly must be a list of 3 to 64");
        Refused(Edited("thanalan-road-chart", static n => n["paint"]![0]!["where"] = JsonNode.Parse("""[{ "line": [40, 10] }]""")), ".line needs points");
        Refused(Edited("thanalan-road-chart", static n => n["paint"]![0]!["where"] = JsonNode.Parse("[]")), ".where needs at least one term");
        Refused(Edited("thanalan-road-chart", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "tone", "where": [{ "y": [0, 100] }], "mul": 0.8 }"""))), "a tone belongs in paint");
        Refused(Edited("thanalan-road-chart", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "plate", "picture": "thanalan-road-chart-rim", "blend": "over" }"""))), ".blend must be");
        Refused(Edited("thanalan-road-chart", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "plate", "picture": "../rim", "blend": "screen" }"""))), ".picture must name");
        Refused(Edited("thanalan-road-chart", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "plate", "picture": "thanalan-road-chart-rim", "blend": "screen", "cover": true }"""))), "cover must multiply");
        Refused(Edited("thanalan-road-chart", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "shafts", "angles": [50], "widths": [30], "beamsOnly": true }"""))), "beamsOnly must be moving");
        Refused(Edited("thanalan-road-chart", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "plate", "picture": "thanalan-road-chart-rim", "blend": "add", "rim": true }"""))), "rim must screen");
        Refused(Edited("sagolii-cactuar", static n => n["motion"]!["glints"] = JsonNode.Parse("""[{ "points": [[1, 2]] }]""")), ".points must be a list of 2 to 256");
        Refused(Edited("sagolii-cactuar", static n => n["motion"]!["starWhere"] = JsonNode.Parse("""[{ "sky": [1, 2] }]""")), "must name one of");
        Refused(Edited("horizon-by-night", static n => n["palette"]!["regions"]![0]!["chroma"] = 0.31), "chroma must be");
        Refused(Edited("sagolii-cactuar", static n => n["palette"]!["spare"] = JsonNode.Parse("[[], [], [], [], []]")), "at most 4 masks");

        // 1-2's water region pushes 0.252, over the old limit of 0.2 (its approved green, kept in the shipped gamut).
        Assert.Equal(0.252f, Read(Shipped("horizon-by-night")).Palette!.Regions[0].Chroma, 3);
    }

    [Fact]
    public void A_crop_is_the_boards_shape_unless_squeezed_and_then_within_a_quarter()
    {
        var limsa = Read(Shipped("limsa-across-water"));
        Assert.Equal((60f, 330f, 1660f, 1530f), limsa.Source.Crop);
        Refused(Edited("limsa-across-water", static n => n["source"]!.AsObject().Remove("squeeze")), "or set squeeze");
        Refused(Edited("limsa-across-water", static n => n["source"]!["crop"] = new JsonArray(0, 0, 1000, 1100)), "within 25% of 4:3");
        Read(Edited("limsa-across-water", static n => n["source"]!["crop"] = new JsonArray(0, 0, 1000, 1000 * 3 / 4 / 1.2)));
    }

    // ---- Building ----

    [Fact]
    public void Erase_fills_a_box_from_the_ring_round_it_and_leaves_the_rest()
    {
        var image = Grey(200, 200, 0.3f);
        for (var y = 80; y < 120; y++)
        {
            for (var x = 80; x < 120; x++)
            {
                image.R.Data[(y * 200) + x] = 0.9f;
            }
        }

        var erased = MoonfallSceneBuilder.Erase(image, [new Vector4(80, 80, 40, 40)]);
        Assert.Equal(0.3f, erased.R.Data[(100 * 200) + 100], 3);
        Assert.Equal(0.3f, erased.R.Data[(10 * 200) + 10], 6);
        Assert.Equal(0.3f, erased.G.Data[(100 * 200) + 100], 6);

        // The source is left as it was.
        Assert.Equal(0.9f, image.R.Data[(100 * 200) + 100], 6);
    }

    [Fact]
    public void The_land_is_what_a_flood_from_the_corners_cannot_reach()
    {
        // A light sheet with a dark ring round (300, 200)-(500, 400): inside it is land, outside sea.
        var map = Grey(800, 600, 0.9f);
        for (var y = 200; y <= 400; y++)
        {
            for (var x = 300; x <= 500; x++)
            {
                if (y < 204 || y > 396 || x < 304 || x > 496)
                {
                    map.R.Data[(y * 800) + x] = map.G.Data[(y * 800) + x] = map.B.Data[(y * 800) + x] = 0.05f;
                }
            }
        }

        var land = MoonfallSceneBuilder.LandMask(map, 0.75f, 5, 1);
        Assert.Equal(1f, land[400, 300]);
        Assert.Equal(1f, land[302, 202]);
        Assert.Equal(0f, land[100, 100]);
        Assert.Equal(0f, land[700, 500]);
        var twice = MoonfallSceneBuilder.LandMask(map, 0.75f, 5, 2);
        Assert.Equal((1600, 1200), (twice.Width, twice.Height));
        Assert.Equal(1f, twice[800, 600]);
        Assert.Equal(0f, twice[100, 100]);
        Assert.Throws<ArgumentException>(() => MoonfallSceneBuilder.LandMask(Grey(10, 10, 0.5f), 0.75f, 5, 1));
    }

    [Fact]
    public void A_polygon_fills_its_area_and_a_polyline_measures_its_distance()
    {
        var square = MoonfallSceneBuilder.PolygonFill([new(100, 100), new(200, 100), new(200, 300), new(100, 300)], 800, 600, 1);
        Assert.Equal(20_000f, square.Data.Sum(), 200f);
        Assert.Equal(1f, square[150, 200]);
        Assert.Equal(0f, square[50, 50]);
        var twice = MoonfallSceneBuilder.PolygonFill([new(100, 100), new(200, 100), new(200, 300), new(100, 300)], 1600, 1200, 2);
        Assert.Equal(80_000f, twice.Data.Sum(), 400f);

        var line = MoonfallSceneBuilder.PolylineDistance([new(100, 100), new(300, 100)], 800, 600, 1);
        // Measured from each pixel's centre.
        Assert.Equal(20.5f, line[200, 120], 3);
        Assert.Equal(0.5f, line[200, 100], 3);
        Assert.Equal(50.5f, line[350, 100], 2);
    }

    [Fact]
    public void A_tone_shades_the_painting_where_its_mask_is()
    {
        var recipe = Flat(""", "paint": [{ "kind": "tone", "where": [{ "y": [300, 200] }], "mul": 0.5 }]""");
        var layers = MoonfallSceneBuilder.Build(recipe, Level, Grey(800, 600, 0.6f), 1);
        Assert.Equal(0.3f, BaseAt(layers, 400, 100), 2);
        Assert.Equal(0.6f, BaseAt(layers, 400, 500), 2);
    }

    [Fact]
    public void Plates_screen_multiply_and_add_and_a_cover_plate_is_the_framings_cover()
    {
        const string Plates = """
            , "light": [{ "kind": "plate", "picture": "p-light", "blend": "screen" }, { "kind": "plate", "picture": "p-cover", "blend": "multiply", "cover": true },
              { "kind": "plate", "picture": "p-cloth", "blend": "add" }]
            """;
        var recipe = Flat(Plates);
        var cover = Grey(1600, 1200, 1f);
        var cloth = Grey(1600, 1200, 0f);
        for (var y = 0; y < 400; y++)
        {
            for (var x = 0; x < 400; x++)
            {
                cover.R.Data[(y * 1600) + x] = cover.G.Data[(y * 1600) + x] = cover.B.Data[(y * 1600) + x] = 0f;
                cloth.R.Data[(y * 1600) + x] = cloth.G.Data[(y * 1600) + x] = cloth.B.Data[(y * 1600) + x] = 0.1f;
            }
        }

        var plates = new Dictionary<string, MoonfallImage> { ["p-light"] = Grey(1600, 1200, 0.2f), ["p-cover"] = cover, ["p-cloth"] = cloth };
        foreach (var tier in new[] { 1, 2 })
        {
            var layers = MoonfallSceneBuilder.Build(recipe, Level, Grey(1600, 1200, 0.5f), tier, plates: plates);
            Assert.Equal(1 - (0.5f * 0.8f), BaseAt(layers, 400, 500), 2);
            Assert.Equal(0.1f, BaseAt(layers, 100, 60), 2);

            // Fever's sky lifts nowhere the framing covers.
            var sky = layers.SkyMask;
            Assert.Equal(0, sky.Pixels[((((int)((70 - sky.Board.Y) / 2) * sky.Width) + (int)((110 - sky.Board.X) / 2)) * 4) + 3]);
            Assert.True(sky.Pixels[((((int)((70 - sky.Board.Y) / 2) * sky.Width) + (int)((600 - sky.Board.X) / 2)) * 4) + 3] > 200);
        }

        // A plate the caller could not read is left out, and counted.
        var without = MoonfallSceneBuilder.Build(recipe, Level, Grey(800, 600, 0.5f), 1);
        Assert.Equal(0.5f, BaseAt(without, 400, 500), 2);
        Assert.Equal(3, without.Dropped);
    }

    [Fact]
    public void A_fallback_picture_and_the_story_safe_recipe_leave_the_paint_plates_out()
    {
        // A plate under the palette is drawn on the painting (a chart's roads): over a fallback picture it would show the
        // place, and cross the pegs (critic runtime round 1, m8). Light plates (the framing) stay.
        var recipe = Flat(""", "paint": [{ "kind": "plate", "picture": "p-road", "blend": "screen" }], "light": [{ "kind": "plate", "picture": "p-light", "blend": "screen" }]""") with { Fallback = "test-night" };
        var plates = new Dictionary<string, MoonfallImage> { ["p-road"] = Grey(800, 600, 0.5f), ["p-light"] = Grey(800, 600, 0.2f) };
        var game = MoonfallSceneBuilder.Build(recipe, Level, Grey(800, 600, 0.3f), 1, plates: plates);
        var fallback = MoonfallSceneBuilder.Build(recipe, Level, Grey(800, 600, 0.3f), 1, fallback: true, plates: plates);
        Assert.Equal(1 - (0.7f * 0.5f * 0.8f), BaseAt(game, 400, 500), 2);
        Assert.Equal(1 - (0.7f * 0.8f), BaseAt(fallback, 400, 500), 2);
        Assert.Equal(0, fallback.Dropped);

        var safe = MoonfallSceneBuilder.StorySafe(recipe)!;
        Assert.Equal("test-flat~safe", safe.Name);
        Assert.Equal((MoonfallSourceKind.Picture, "test-night"), (safe.Source.Kind, safe.Source.Path));
        Assert.Null(safe.Grade);
        Assert.Empty(safe.Paint);
        Assert.Single(safe.Light);
        Assert.Null(MoonfallSceneBuilder.StorySafe(recipe with { Fallback = null }));
    }

    [Fact]
    public void A_star_never_sits_out_of_its_sky_or_on_a_moon()
    {
        // A field of bright points everywhere: the stars keep to the sky the recipe names (y above 200) and off its moon.
        var px = Grey(800, 600, 0.05f);
        for (var y = 50; y < 590; y += 9)
        {
            for (var x = 80; x < 720; x += 9)
            {
                px.R.Data[(y * 800) + x] = px.G.Data[(y * 800) + x] = px.B.Data[(y * 800) + x] = 0.6f;
            }
        }

        var recipe = Flat(""", "light": [{ "kind": "moon", "x": 200, "y": 120, "r": 20 }], "motion": { "stars": 60, "starRegion": [75, 41, 725, 590], "starWhere": [{ "y": [210, 190] }] }""");
        var level = new MoonfallLevel("base-99", "Low", [MoonfallPeg.Round(600, 560)]);
        var layers = MoonfallSceneBuilder.Build(recipe, level, px, 1);
        Assert.Equal(60, layers.Stars.Count);
        Assert.All(layers.Stars, static s => Assert.True(s.Y < 200, $"a star at y {s.Y}"));
        Assert.All(layers.Stars, static s => Assert.True(Vector2.Distance(new(s.X, s.Y), new(200, 120)) >= 30, $"a star on the moon at ({s.X}, {s.Y})"));
        Assert.Equal([new Vector3(200, 120, 30)], MoonfallSceneBuilder.MoonDiscs(recipe));
    }

    [Fact]
    public void A_beams_only_shaft_bakes_nothing_and_its_beams_keep_behind_the_framing()
    {
        const string Shaft = """{ "kind": "shafts", "origin": [-60, -160], "angles": [40, 50, 60], "widths": [60, 60, 60], "k": 0.08, "moving": true, "beamsOnly": true }""";
        var bare = MoonfallSceneBuilder.Build(Flat(""), Level, Grey(800, 600, 0.3f), 1);
        var beams = MoonfallSceneBuilder.Build(Flat($", \"light\": [{Shaft}]"), Level, Grey(800, 600, 0.3f), 1);
        Assert.Equal(bare.Base.Pixels, beams.Base.Pixels);
        Assert.NotNull(beams.BeamsA);
        Assert.Contains(beams.BeamsA!.Pixels.Where(static (_, i) => i % 4 == 3), static a => a > 0);

        // Under a cover plate the whole opening is framing: no beam shows anywhere.
        var covered = MoonfallSceneBuilder.Build(Flat($", \"light\": [{Shaft}, {{ \"kind\": \"plate\", \"picture\": \"p-cover\", \"blend\": \"multiply\", \"cover\": true }}]"), Level, Grey(800, 600, 0.3f), 1,
            plates: new Dictionary<string, MoonfallImage> { ["p-cover"] = Grey(800, 600, 0f) });
        Assert.All(covered.BeamsA!.Pixels.Where(static (_, i) => i % 4 == 3), static a => Assert.Equal(0, a));
    }

    [Fact]
    public void The_moons_front_layer_is_only_the_framing_in_front_of_it()
    {
        // Our three moons stand clear of framing on the Sagolii and behind the ferry's rigging and the bay's palms.
        var recipe = Flat(""", "feverMoon": [400, 100, 20]""");
        var level = new MoonfallLevel("base-99", "Low", [MoonfallPeg.Round(400, 500)]);
        var open = MoonfallSceneBuilder.Build(recipe, level, Grey(800, 600, 0.3f), 1);
        Assert.NotNull(open.Moon);
        Assert.Null(open.MoonFront);

        var cover = Grey(800, 600, 1f);
        for (var y = 120; y < 150; y++)
        {
            for (var x = 420; x < 440; x++)
            {
                cover.R.Data[(y * 800) + x] = cover.G.Data[(y * 800) + x] = cover.B.Data[(y * 800) + x] = 0f;
            }
        }

        var framed = MoonfallSceneBuilder.Build(recipe with { Light = [new MoonfallPlate("p-cover", MoonfallPlateBlend.Multiply, true)] }, level, Grey(800, 600, 0.3f), 1,
            plates: new Dictionary<string, MoonfallImage> { ["p-cover"] = cover });
        var front = framed.MoonFront!;
        Assert.Equal(new Vector4(420, 120, 440, 150), front.Board);
        Assert.Equal((20, 30), (front.Width, front.Height));
    }
}
