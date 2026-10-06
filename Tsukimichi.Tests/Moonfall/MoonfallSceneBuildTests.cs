using System.Numerics;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Every shipped level's scene built from its recipe as the plugin builds it (the game's painting when
/// <c>TSUKIMICHI_GAME_PATH</c> is set, else the fallback picture), with the fuller-board rules measured on the result
/// (level-method.md §8: F2, F3a and its pixel backstop, F3b, F3c, F3d, F5), the layers' sizes and the build's cost.
/// </summary>
public sealed class MoonfallSceneBuildTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Levels() => MoonfallSceneKit.ShippedScenes().Select(static s => new object[] { s.Level.Id });

    private static (MoonfallLevel Level, MoonfallSceneRecipe Recipe) Scene(string id) => MoonfallSceneKit.ShippedScenes().First(s => s.Level.Id == id);

    [Fact]
    public void Every_shipped_level_has_a_scene_recipe()
    {
        // Apply to the existing levels: every shipped level gets the new look, from its own recipe or a default one.
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var recipes = MoonfallSceneKit.Recipes();
        foreach (var level in campaigns.Base.Levels.Concat(campaigns.Expansion.Levels))
        {
            Assert.True(MoonfallSceneRecipeLoader.Pick(recipes, level) is not null, $"{level.Id} takes no scene recipe");
        }

        Assert.Empty(MoonfallSceneRecipeLoader.CheckSet(recipes));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void The_scene_keeps_the_fuller_board_rules(string id)
    {
        var (level, recipe) = Scene(id);
        var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
        Assert.NotNull(painting);
        var layers = MoonfallSceneBuilder.Build(recipe, level, painting, 2, fallback, check: true);
        var r = layers.Report!;
        output.WriteLine($"{id} ({recipe.Name}{(fallback ? ", fallback picture" : string.Empty)}): built in {layers.Cost.TotalMilliseconds:0} ms; cover {r.CoverPercent:0.00}%, middle {r.MiddlePercent:0.000}%, " +
            $"clearance {r.MinClearance?.ToString("0.0") ?? "-"}, rim run {r.LongestRimRun:0.0}, dropped {layers.Dropped}, fireflies {layers.Fireflies.Count}, stars {layers.Stars.Count}, {layers.Bytes / 1048576.0:0.00} MiB");
        Assert.True(r.Ok, $"{id}: {string.Join(", ", r.Fails)}; straight {r.StraightRun}, discs {r.PegSizedDiscs.Count}, lights {r.LightsTooClose.Count}, darkened {r.DarkenedTooClose}");
        Assert.Equal((1300, 1106), (layers.Base.Width, layers.Base.Height));
    }
}
