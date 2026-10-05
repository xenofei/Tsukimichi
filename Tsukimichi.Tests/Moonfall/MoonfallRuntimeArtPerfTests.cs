using System.Diagnostics;
using System.Numerics;
using Tsukimichi.Core.Moonfall.Art;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// What the in-play art costs (Perf: not in the gate; run with <c>--filter Category=Perf</c>): each shipped scene's
/// build at both tiers, stage by stage (spec-rich2.md §6 asks under 120 ms a level on the CPU), the chrome's grade, and
/// the dressing's motion per frame.
/// </summary>
public sealed class MoonfallRuntimeArtPerfTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Perf")]
    public void Scene_builds_and_chrome_grading_cost()
    {
        foreach (var (level, recipe) in MoonfallSceneKit.ShippedScenes())
        {
            var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
            Assert.NotNull(painting);
            foreach (var tier in new[] { 1, 2 })
            {
                MoonfallSceneLayers? best = null;
                double bestMs = double.MaxValue;
                for (var run = 0; run < 3; run++)
                {
                    var cpu = Process.GetCurrentProcess().TotalProcessorTime;
                    var wall = Stopwatch.StartNew();
                    var layers = MoonfallSceneBuilder.Build(recipe, level, painting, tier, fallback);
                    var ms = wall.Elapsed.TotalMilliseconds;
                    if (ms < bestMs)
                    {
                        bestMs = ms;
                        best = layers;
                        output.WriteLine($"  run {run}: {ms:0} ms wall, {(Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds:0} ms CPU (all threads)");
                    }
                }

                output.WriteLine($"{level.Id} ({recipe.Name}) at {tier}x: {bestMs:0} ms; " + string.Join(", ", best!.Timings.Select(static t => $"{t.Stage} {t.Ms:0}")));
                Assert.True(bestMs < 5000, $"{level.Id} at {tier}x took {bestMs:0} ms");
            }
        }

        var textures = MoonfallChromeArt.Textures.ToDictionary(static t => t.Path, static t => MoonfallSceneKit.GameTexture(t.Path) ?? Grey(t.Width, t.Height), StringComparer.Ordinal);
        var sw = Stopwatch.StartNew();
        var chrome = MoonfallChromeArt.Build(textures);
        output.WriteLine($"chrome: {sw.Elapsed.TotalMilliseconds:0} ms ({chrome.Cost.TotalMilliseconds:0} ms in Build), {chrome.Sheet.Bytes / 1048576.0:0.00} MiB");
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void The_motion_costs_microseconds_a_frame()
    {
        var (level, recipe) = MoonfallSceneKit.ShippedScenes().First();
        var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
        var layers = MoonfallSceneBuilder.Build(recipe, level, painting!, 1, fallback);
        var clearance = layers.Clearance!;
        var fireflies = layers.Fireflies.ToArray();
        var stars = layers.Stars.ToArray();
        var sink = 0f;
        void Frame(double t)
        {
            var (a, b) = MoonfallMotion.Beams(t, false);
            sink += a + b + MoonfallMotion.Flicker(t, 0.2f, false) + MoonfallMotion.Glint(t, 6, 3, false);
            foreach (var f in fireflies)
            {
                sink += MoonfallMotion.FireflyAt(f, t, false).X + MoonfallMotion.FireflyPulse(f, t, false);
            }

            foreach (var s in stars)
            {
                sink += MoonfallMotion.Twinkle(s, t, false);
            }

            for (var i = 0; i < MoonfallSceneRecipeLoader.MaxParticles; i++)
            {
                var (at, fade) = MoonfallMotion.Dust(i, t);
                sink += MoonfallMotion.DustVisible(clearance, at) ? fade : 0;
            }
        }

        for (var n = 0; n < 2000; n++)
        {
            Frame(n / 60.0);
        }

        var sw = Stopwatch.StartNew();
        const int Frames = 20000;
        for (var n = 0; n < Frames; n++)
        {
            Frame(n / 60.0);
        }

        var us = sw.Elapsed.TotalMilliseconds * 1000 / Frames;
        output.WriteLine($"motion for {fireflies.Length} fireflies, {stars.Length} stars and {MoonfallSceneRecipeLoader.MaxParticles} dust motes: {us:0.0} µs a frame");
        Assert.True(us < 200, $"{us:0.0} µs a frame");
        Assert.NotEqual(0f, sink);
    }

    private static MoonfallImage Grey(int w, int h)
    {
        var image = new MoonfallImage(w, h, true);
        Array.Fill(image.R.Data, 0.6f);
        Array.Fill(image.G.Data, 0.55f);
        Array.Fill(image.B.Data, 0.4f);
        Array.Fill(image.A!.Data, 1f);
        return image;
    }
}
