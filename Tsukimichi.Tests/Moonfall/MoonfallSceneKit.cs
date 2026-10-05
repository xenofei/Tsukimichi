using Lumina;
using LuminaGameData = Lumina.GameData;
using Lumina.Data.Files;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The scene tests' sources: Moonfall's shipped pictures from <c>Tsukimichi/assets/moonfall/scenes/</c>, and the
/// game's textures through Lumina when <c>TSUKIMICHI_GAME_PATH</c> points at the install (else none: a recipe whose
/// painting is a game texture is built from its fallback picture, as the plugin does when the texture is missing).
/// </summary>
internal static class MoonfallSceneKit
{
    private static readonly Lazy<LuminaGameData?> Game = new(() =>
    {
        var path = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar);
        return string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ? null : new LuminaGameData(path, new LuminaOptions { PanicOnSheetChecksumMismatch = false });
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly object Gate = new();

    public static bool HasGame => Game.Value is not null;

    public static IReadOnlyDictionary<string, MoonfallSceneRecipe> Recipes()
    {
        var errors = new List<string>();
        var recipes = MoonfallSceneRecipeLoader.LoadBuiltIn(errors);
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return recipes;
    }

    /// <summary>A shipped scene picture (the 2x one when asked and present).</summary>
    public static MoonfallImage? Picture(string name, bool twoX = true)
    {
        var dir = Path.Combine(MoonfallArtTests.ArtFolder(), "scenes");
        var path = Path.Combine(dir, name + (twoX ? "@2x.png" : ".png"));
        if (!File.Exists(path))
        {
            path = Path.Combine(dir, name + ".png");
        }

        if (!File.Exists(path))
        {
            return null;
        }

        var decoded = MoonfallPng.Decode(File.ReadAllBytes(path), out _);
        return decoded is { } d ? MoonfallImage.FromBytes(d.Rgba, d.Width, d.Height, d.Width * 4, bgra: false, keepAlpha: false) : null;
    }

    /// <summary>A texture from the install (BGRA, as Lumina and Dalamud decode it), with alpha; null without the game or the file.</summary>
    public static MoonfallImage? GameTexture(string path)
    {
        if (Game.Value is not { } game)
        {
            return null;
        }

        lock (Gate)
        {
            var tex = game.GetFile<TexFile>(path);
            return tex is null ? null : MoonfallImage.FromBytes(tex.ImageData, tex.Header.Width, tex.Header.Height, tex.Header.Width * 4, bgra: true, keepAlpha: true);
        }
    }

    /// <summary>A recipe's painting as the plugin finds it: the game texture or the shipped picture, else the fallback (whole).</summary>
    public static (MoonfallImage? Painting, bool Fallback) Painting(MoonfallSceneRecipe recipe)
    {
        var painting = recipe.Source.Kind == MoonfallSourceKind.Game ? GameTexture(recipe.Source.Path) : Picture(recipe.Source.Path);
        if (painting is not null)
        {
            return (painting, false);
        }

        return recipe.Fallback is { } name ? (Picture(name), true) : (null, false);
    }

    /// <summary>Every shipped level that takes a recipe (<see cref="MoonfallSceneRecipeLoader.Pick"/>), with it.</summary>
    public static IEnumerable<(MoonfallLevel Level, MoonfallSceneRecipe Recipe)> ShippedScenes()
    {
        var recipes = Recipes();
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        foreach (var level in campaigns.Base.Levels.Concat(campaigns.Expansion.Levels))
        {
            if (MoonfallSceneRecipeLoader.Pick(recipes, level) is { } recipe)
            {
                yield return (level, recipe);
            }
        }
    }
}
