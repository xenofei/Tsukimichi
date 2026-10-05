using System.Text.Json.Nodes;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The scene recipe format (docs/design/v9/scene-recipe.md): the shipped recipes read cleanly, and the loader refuses,
/// with a reason, everything the format rules out (a recipe is data a level author writes, so it is checked like a level).
/// </summary>
public sealed class MoonfallSceneRecipeLoaderTests
{
    private static string Shipped(string name) =>
        File.ReadAllText(Path.Combine(Tsukimichi.Tests.Ui.OrnamentLayoutTests.RepoRoot(), "Tsukimichi.Core", "Moonfall", "Levels", "scenes", name + ".json"));

    /// <summary>A shipped recipe with <paramref name="edit"/> applied (comments dropped).</summary>
    private static string Edited(string name, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(Shipped(name), documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        edit(node);
        return node.ToJsonString();
    }

    private static void Refused(string json, string reason, string? expectedName = null)
    {
        var load = MoonfallSceneRecipeLoader.Parse(json, expectedName);
        Assert.Null(load.Recipe);
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_shipped_recipe_reads_and_the_set_agrees()
    {
        var errors = new List<string>();
        var recipes = MoonfallSceneRecipeLoader.LoadBuiltIn(errors);
        Assert.Empty(errors);
        Assert.Equal(["airship-road", "holy-see", "lantern-night", "moon-road-night"], recipes.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(MoonfallSceneRecipeLoader.CheckSet(recipes));
        foreach (var recipe in recipes.Values)
        {
            Assert.True(recipe.Motion.Dust + recipe.Motion.Stars + (recipe.Fireflies?.Count ?? 0) <= MoonfallSceneRecipeLoader.MaxParticles, recipe.Name);
        }
    }

    [Fact]
    public void A_recipe_reads_its_fields()
    {
        var load = MoonfallSceneRecipeLoader.Parse(Shipped("airship-road"), "airship-road");
        Assert.True(load.Errors.Count == 0, string.Join("; ", load.Errors));
        var r = load.Recipe!;
        Assert.Equal(MoonfallSourceKind.Game, r.Source.Kind);
        Assert.Equal("ui/loadingimage/-nowloading_base05.tex", r.Source.Path);
        Assert.Equal("moon-road-night", r.Fallback);
        Assert.Equal(0.40f, r.Veil, 3);
        Assert.Equal(["base-03"], r.Levels);
        Assert.False(r.Default);
        Assert.Contains(r.Light, static l => l is MoonfallShafts { Moving: true });
        Assert.Equal(40, r.Motion.Dust);
    }

    [Fact]
    public void Empty_oversized_and_broken_text_is_refused()
    {
        Assert.Null(MoonfallSceneRecipeLoader.Parse(null).Recipe);
        Assert.Null(MoonfallSceneRecipeLoader.Parse("   ").Recipe);
        Refused("{ not json", "not JSON");
        Refused(new string(' ', MoonfallSceneRecipeLoader.MaxRecipeBytes) + "{}", "longer than");
    }

    [Fact]
    public void The_header_is_checked()
    {
        Refused(Edited("airship-road", static n => n["format"] = "moonfall-level"), "format must be");
        Refused(Edited("airship-road", static n => n["version"] = MoonfallSceneRecipeLoader.Version + 1), "newer Moonfall");
        Refused(Edited("airship-road", static n => n["version"] = 0), "version");
        Refused(Edited("airship-road", static n => n["name"] = "Airship Road"), "name must be");
        Refused(Edited("airship-road", static n => n["name"] = "../airship"), "name must be");
        Refused(Shipped("airship-road"), "does not match the file", expectedName: "holy-see");
    }

    [Theory]
    [InlineData("ui/uld/Journal_Frame_hr1.tex")]
    [InlineData("../../ffxiv/ui/loadingimage/x.tex")]
    [InlineData("C:/Windows/notepad.exe")]
    [InlineData("https://example.com/x.tex")]
    [InlineData("ui/loadingimage/x.png")]
    [InlineData("ui/loadingimage/../uld/x.tex")]
    [InlineData("ui/map//world01_m.tex")]
    public void A_painting_is_only_ever_a_loading_image_or_a_map(string path)
    {
        // A recipe can never name an arbitrary file: only the game's loading paintings and maps, read through Dalamud.
        Refused(Edited("airship-road", n => n["source"]!["game"] = path), "source.game");
    }

    [Fact]
    public void The_source_and_its_crop_are_checked()
    {
        Refused(Edited("airship-road", static n => n["source"] = new JsonObject { ["game"] = "ui/map/world/01/world01_m.tex", ["picture"] = "moon-road-night" }), "exactly one");
        Refused(Edited("airship-road", static n => n.Remove("source")), "source must be");
        Refused(Edited("airship-road", static n => n["source"]!["crop"] = new JsonArray(0, 0, 800, 800)), "4:3");
        Refused(Edited("airship-road", static n => n["source"]!["padMode"] = "wrap"), "padMode");
        Refused(Edited("airship-road", static n => n["source"] = new JsonObject { ["picture"] = "Moon Road" }), "source.picture");
        Refused(Edited("airship-road", static n => n["fallback"] = "../moon"), "fallback");
    }

    [Fact]
    public void The_light_keeps_to_the_rules()
    {
        // F4: a shaft's strength is at most 0.08; a moving shaft belongs in light; one moon at most.
        Refused(Edited("airship-road", static n => n["light"]![1]!["k"] = 0.09), "k must be");
        Refused(Edited("airship-road", static n => n["paint"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "shafts", "origin": [0, 0], "angles": [50], "widths": [30], "k": 0.05, "moving": true }"""))), "moving shaft belongs in light");
        Refused(Edited("airship-road", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "lantern" }"""))), ".kind must be");
        Refused(Edited("moon-road-night", static n => n["light"]!.AsArray().Add(JsonNode.Parse("""{ "kind": "moon", "x": 600, "y": 100, "r": 20 }"""))), "more than one moon");
    }

    [Fact]
    public void The_motion_budget_and_its_light_are_checked()
    {
        Refused(Edited("moon-road-night", static n => n["motion"]!["dust"] = 100), "motion budget");
        // Dust drifts in the moving beams: a recipe with dust and no moving shaft is refused.
        Refused(Edited("airship-road", static n => n["light"]![1]!["moving"] = false), "no shaft is moving");
        Refused(Edited("lantern-night", static n => n["motion"]!["mist"] = JsonNode.Parse("""[{ "y": [590, 560] }]""")), "must run down the board");
    }

    [Fact]
    public void The_levels_list_and_the_default_are_checked()
    {
        Refused(Edited("airship-road", static n => n["levels"] = new JsonArray("Base 03")), "level id");
        Refused(Edited("airship-road", static n => n["levels"] = "base-03"), "levels must be");
        Refused(Edited("airship-road", static n => n["default"] = "yes"), "true or false");
    }

    [Fact]
    public void A_level_takes_its_own_scene_its_listed_recipe_or_the_default()
    {
        var recipes = MoonfallSceneRecipeLoader.LoadBuiltIn();
        var pegs = new[] { new MoonfallPeg { X = 400, Y = 300, Radius = 10 } };
        MoonfallLevel Level(string id, string? scene) => new(id, id, pegs) { Scene = scene };

        // The file's own scene, when a recipe has that name.
        Assert.Equal("holy-see", MoonfallSceneRecipeLoader.Pick(recipes, Level("base-01", "holy-see"))?.Name);
        // A scene that is a picture with no recipe keeps its picture.
        Assert.Null(MoonfallSceneRecipeLoader.Pick(recipes, Level("base-01", "some-picture")));
        // No scene: the recipe that lists the level, else the default.
        Assert.Equal("airship-road", MoonfallSceneRecipeLoader.Pick(recipes, Level("base-03", null))?.Name);
        Assert.Equal("moon-road-night", MoonfallSceneRecipeLoader.Pick(recipes, Level("base-99", null))?.Name);
        Assert.Null(MoonfallSceneRecipeLoader.Pick(recipes, null));

        // Every shipped level takes a scene.
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        Assert.All(campaigns.Base.Levels.Concat(campaigns.Expansion.Levels), l => Assert.NotNull(MoonfallSceneRecipeLoader.Pick(recipes, l)));
    }

    [Fact]
    public void Two_defaults_or_a_level_listed_twice_is_reported()
    {
        var recipes = MoonfallSceneRecipeLoader.LoadBuiltIn().ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal);
        recipes["holy-see"] = recipes["holy-see"] with { Default = true, Levels = ["base-02", "base-03"] };
        var problems = MoonfallSceneRecipeLoader.CheckSet(recipes);
        Assert.Contains(problems, static p => p.StartsWith("more than one default", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.StartsWith("base-03 is listed by airship-road and holy-see", StringComparison.Ordinal));
    }
}
