using System.Collections.Concurrent;
using System.Diagnostics;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// A level never waits visibly for its scene (spec-rich2.md §6, "Load: under 120 ms"): the menus build the next level's
/// scene ahead (<see cref="MoonfallGameArt{T}.Warm"/>), so its board opens with it; started cold at the 2x tier, the 1x
/// tier is built and shown first and the 2x tier replaces it; the night sky stands in meanwhile (the board fades the
/// scene in over it). The Perf benchmark measures each shipped scene's build at both tiers (run with
/// <c>--filter Category=Perf</c>).
/// </summary>
public sealed class MoonfallSceneStartTests(ITestOutputHelper output)
{
    private sealed record Tex(string Name, long Bytes);

    private sealed class Host : IMoonfallGameArtHost<Tex>
    {
        public int Frame { get; set; }

        public ConcurrentBag<(string Name, long Bytes)> Uploaded { get; } = [];

        /// <summary>Holds every game-texture read until it completes (a build caught mid-way); done by default.</summary>
        public Task Gate { get; set; } = Task.CompletedTask;

        public ConcurrentBag<string> Pictures { get; } = [];

        public Task<MoonfallImage?> ReadGameTexture(string path) => Gate.ContinueWith(_ => MoonfallSceneKit.GameTexture(path), TaskScheduler.Default);

        public Task<MoonfallImage?> ReadPicture(string name)
        {
            Pictures.Add(name);
            return Task.FromResult(MoonfallSceneKit.Picture(name, twoX: false));
        }

        public Task<Tex> Upload(MoonfallRgba pixels, string name)
        {
            Uploaded.Add((name, pixels.Bytes));
            return Task.FromResult(new Tex(name, pixels.Bytes));
        }

        public void Dispose(Tex texture)
        {
        }

        public void Warn(string message)
        {
        }
    }

    private static (MoonfallLevel Level, MoonfallSceneRecipe Recipe) AScene() => MoonfallSceneKit.ShippedScenes().First();

    private static void Until(Func<bool> done, Action frame)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!done() && DateTime.UtcNow < until)
        {
            frame();
            Thread.Sleep(2);
        }

        Assert.True(done(), "never settled");
    }

    [Fact]
    public void A_level_built_ahead_opens_with_its_scene_within_two_frames()
    {
        var host = new Host();
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneKit.Recipes());
        var (level, _) = AScene();

        // Level select: the menus' frame, and the level the player is about to start built ahead.
        Until(() => { art.Warm(level, twoX: true); return !art.Warming && art.Chrome is not null; }, () => { host.Frame++; art.Menu(); });

        // Play: the scene is uploaded from the cache, never built on the way in.
        var frames = 0;
        while (art.Scene is null && frames < 10)
        {
            host.Frame++;
            art.Frame(level, twoX: true, pegMarks: false);
            frames++;
        }

        Assert.NotNull(art.Scene);
        Assert.InRange(frames, 1, 2);
        Assert.True(art.SceneSettled);
        Assert.Equal(2, art.Scene!.Layers.Base.Width / (int)(MoonfallSceneBuilder.OpeningRect.Z - MoonfallSceneBuilder.OpeningRect.X));
    }

    [Fact]
    public void A_cold_start_at_the_2x_tier_shows_the_1x_tier_first_then_the_2x()
    {
        var host = new Host();
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneKit.Recipes());
        var (level, _) = AScene();
        Until(() => art.Scene is not null, () => { host.Frame++; art.Frame(level, twoX: true, pegMarks: false); });

        // The first scene on screen is the 1x tier, standing in (not settled: the 2x is still to come).
        var first = art.Scene!;
        Assert.Equal(1, first.Layers.Base.Width / (int)(MoonfallSceneBuilder.OpeningRect.Z - MoonfallSceneBuilder.OpeningRect.X));
        Until(() => art.SceneSettled, () => { host.Frame++; art.Frame(level, twoX: true, pegMarks: false); });
        Assert.NotSame(first, art.Scene);
        Assert.Equal(2, art.Scene!.Layers.Base.Width / (int)(MoonfallSceneBuilder.OpeningRect.Z - MoonfallSceneBuilder.OpeningRect.X));
    }

    [Fact]
    public void The_menus_thumbnail_is_the_levels_own_scene_scaled_down()
    {
        var host = new Host();
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneKit.Recipes());
        var (level, _) = AScene();
        Until(() => art.Thumb(level, out _) is not null, () => { host.Frame++; art.Menu(); });
        Assert.Contains(host.Uploaded, static u => u.Name.StartsWith("Moonfall thumbnail", StringComparison.Ordinal) && u.Bytes <= MoonfallGameArt<Tex>.ThumbWidth * 300 * 4);
        Assert.Equal(0, art.ThumbsPending);

        // Left alone (its screen closed), the thumbnail's texture goes back.
        for (var i = 0; i <= MoonfallGameArt<Tex>.BackdropKeepFrames + 2; i++)
        {
            host.Frame++;
            art.Menu();
        }

        Assert.Equal(0, art.ThumbsPending);
    }

    [Fact]
    public void A_thumbnail_the_veil_overtakes_mid_build_is_never_landed_and_is_rebuilt_story_safe()
    {
        // Lantern night (Kugane) is built for a thumbnail with its scene shown; while its painting is still being read,
        // the shield comes to hide the scene's place. The finished Kugane build is dropped unlanded, and the thumbnail is
        // built again over the recipe's story-safe fallback.
        var gate = new TaskCompletionSource();
        var host = new Host { Gate = gate.Task };
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneKit.Recipes());
        var (level, recipe) = MoonfallSceneKit.ShippedScenes().First(s => s.Recipe.Name == "lantern-night");
        var hide = MoonfallSceneHide.Shown;
        art.HidesScene = (_, _) => hide;

        // The thumbnail's build starts and waits on the painting.
        for (var i = 0; i < 5; i++)
        {
            host.Frame++;
            _ = art.Thumb(level, out _);
            art.Menu();
        }

        Assert.Equal(0, host.Uploaded.Count(static u => u.Name.StartsWith("Moonfall thumbnail", StringComparison.Ordinal)));

        // The veil falls, then the Kugane read completes.
        hide = MoonfallSceneHide.Fallback;
        art.VeilChanged();
        gate.SetResult();
        Until(() => art.Thumb(level, out _) is not null, () => { host.Frame++; art.Menu(); });

        // One thumbnail landed, and it was built from the fallback picture, never from the Kugane build.
        Assert.Single(host.Uploaded, static u => u.Name.StartsWith("Moonfall thumbnail", StringComparison.Ordinal));
        Assert.Contains(recipe.Fallback!, host.Pictures);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void Scene_build_benchmark()
    {
        // Each shipped scene at each tier: one build to warm the code, then the median of five, with its stages.
        var report = new List<string>();
        foreach (var (level, recipe) in MoonfallSceneKit.ShippedScenes())
        {
            var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
            Assert.NotNull(painting);
            foreach (var tier in new[] { 1, 2 })
            {
                _ = MoonfallSceneBuilder.Build(recipe, level, painting!, tier, fallback);
                var runs = new List<(double Ms, MoonfallSceneLayers Layers)>();
                for (var run = 0; run < 5; run++)
                {
                    var sw = Stopwatch.StartNew();
                    var layers = MoonfallSceneBuilder.Build(recipe, level, painting!, tier, fallback);
                    runs.Add((sw.Elapsed.TotalMilliseconds, layers));
                }

                runs.Sort(static (a, b) => a.Ms.CompareTo(b.Ms));
                var median = runs[2];
                var line = $"{level.Id} ({recipe.Name}) at {tier}x: median {median.Ms:0} ms (min {runs[0].Ms:0}, max {runs[^1].Ms:0}); "
                    + string.Join(", ", median.Layers.Timings.Select(static t => $"{t.Stage} {t.Ms:0}"));
                report.Add(line);
                output.WriteLine(line);
            }
        }

        // The first scene on screen at a cold start is the 1x tier's; a level built ahead in the menus only uploads.
        output.WriteLine($"workers: {MoonfallParallel.Workers} of {Environment.ProcessorCount} cores");
        Assert.NotEmpty(report);
    }
}
