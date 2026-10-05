using System.Collections.Concurrent;
using System.Numerics;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Diagnostics;
using Tsukimichi.Tests.Localization;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The in-play runtime art (plan v9 rich pass 2, spec-rich2.md §4–§6): the fuller-board checker on shapes it must and
/// must not flag, the shipped scenes at the 1x tier, the motion that keeps off the pegs, the peg marks, the style
/// ribbons' placement, the game art's fallbacks when a texture is missing or changed, the texture budget, the per-frame
/// motion allocating nothing, and the art sources writing no file and reaching no network.
/// </summary>
public sealed class MoonfallRuntimeArtTests(ITestOutputHelper output)
{
    private const float MiB = 1024 * 1024;

    // ---- The checker itself (framecheck.selftest, round 3: the checker is tested) ----

    private static MoonfallPlane Mask(Func<float, float, bool> inside)
    {
        var p = new MoonfallPlane(800, 600);
        for (var y = 0; y < 600; y++)
        {
            for (var x = 0; x < 800; x++)
            {
                p.Data[(y * 800) + x] = inside(x + 0.5f, y + 0.5f) ? 1f : 0f;
            }
        }

        return p;
    }

    public static IEnumerable<object[]> CheckerShapes()
    {
        foreach (var w in new[] { 4, 8, 11, 20, 30, 40, 60 })
        {
            yield return [$"post {w} wide", true];
        }

        foreach (var name in new[] { "slab at 45 deg", "disc r 9", "hole r 9 in a slab", "post 6 wide, after the thin exemption" })
        {
            yield return [name, true];
        }

        foreach (var name in new[] { "wavy stem", "straight rope 3.2 wide (exempt)", "post 4 wide, after the thin exemption (exempt)", "ellipse 60 x 44" })
        {
            yield return [name, false];
        }
    }

    [Theory]
    [MemberData(nameof(CheckerShapes))]
    public void The_fuller_board_checker_flags_what_it_must_and_nothing_else(string shape, bool flagged)
    {
        bool Straight(MoonfallPlane m) => MoonfallFramingCheck.StraightRuns(m) is not null;
        bool Round(MoonfallPlane m) => MoonfallFramingCheck.RoundShapes(m).Count > 0;
        var got = shape switch
        {
            _ when shape.StartsWith("post ", StringComparison.Ordinal) && !shape.Contains("thin", StringComparison.Ordinal) =>
                Straight(Mask((x, y) => MathF.Abs(x - 300) < int.Parse(shape.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture) / 2f && y > 200 && y < 300)),
            "slab at 45 deg" => Straight(Mask(static (x, y) => MathF.Abs((x - 300) - (y - 300)) < 10 && MathF.Abs((x - 300) + (y - 300)) < 60)),
            "disc r 9" => Round(Mask(static (x, y) => MathF.Sqrt(((x - 300) * (x - 300)) + ((y - 300) * (y - 300))) < 9)),
            "hole r 9 in a slab" => Round(Mask(static (x, y) => MathF.Abs(x - 300) < 40 && MathF.Abs(y - 300) < 40 && !(MathF.Sqrt(((x - 300) * (x - 300)) + ((y - 300) * (y - 300))) < 9))),
            "wavy stem" => Straight(Mask(static (x, y) => MathF.Abs(x - 300 - (6 * MathF.Sin(y / 7f))) < 5 && y > 200 && y < 300)),
            "straight rope 3.2 wide (exempt)" => Straight(MoonfallFramingCheck.WithoutThin(Mask(static (x, y) => MathF.Abs(x - 300) < 1.6f && y > 200 && y < 300))),
            "post 4 wide, after the thin exemption (exempt)" => Straight(MoonfallFramingCheck.WithoutThin(Mask(static (x, y) => MathF.Abs(x - 300) < 2 && y > 200 && y < 300))),
            "post 6 wide, after the thin exemption" => Straight(MoonfallFramingCheck.WithoutThin(Mask(static (x, y) => MathF.Abs(x - 300) < 3 && y > 200 && y < 300))),
            "ellipse 60 x 44" => Straight(Mask(Ellipse)) || Round(Mask(Ellipse)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        Assert.Equal(flagged, got);

        static bool Ellipse(float x, float y) => MathF.Sqrt((((x - 300) / 30) * ((x - 300) / 30)) + (((y - 300) / 22) * ((y - 300) / 22))) < 1;
    }

    [Fact]
    public void Framing_near_a_peg_or_a_light_close_to_one_fails_the_check()
    {
        // F3a and F5 at runtime: a framing blob 3 units from a peg, and a small light 5 units from it, are both caught.
        var pegs = new[] { new MoonfallPeg { X = 300, Y = 300, Radius = 10 } };
        var clearance = MoonfallClearance.For(pegs);
        var cover = Mask(static (x, y) => MathF.Sqrt(((x - 330) * (x - 330)) + ((y - 300) * (y - 300))) < 17);
        var rim = new MoonfallPlane(800, 600);
        var report = MoonfallFramingCheck.Check(cover, rim, [new Vector3(315, 300, 2)], clearance, 1);
        Assert.Contains("F3a", report.Fails);
        Assert.Contains("F5", report.Fails);
        Assert.False(report.Ok);
        var clear = MoonfallFramingCheck.Check(new MoonfallPlane(800, 600), rim, [new Vector3(360, 300, 2)], clearance, 1);
        Assert.True(clear.Ok, string.Join(", ", clear.Fails));
    }

    // ---- The shipped scenes at the 1x tier, and what moves on them ----

    private static readonly ConcurrentDictionary<string, (MoonfallLevel Level, MoonfallSceneLayers Layers)> Built1x = new(StringComparer.Ordinal);

    public static IEnumerable<object[]> Levels() => MoonfallSceneKit.ShippedScenes().Select(static s => new object[] { s.Level.Id });

    private static (MoonfallLevel Level, MoonfallSceneLayers Layers) Scene1x(string id) => Built1x.GetOrAdd(id, static key =>
    {
        var (level, recipe) = MoonfallSceneKit.ShippedScenes().First(s => s.Level.Id == key);
        var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
        Assert.NotNull(painting);
        return (level, MoonfallSceneBuilder.Build(recipe, level, painting, 1, fallback, check: true));
    });

    [Theory]
    [MemberData(nameof(Levels))]
    public void The_scene_keeps_the_fuller_board_rules_at_the_1x_tier(string id)
    {
        var (_, layers) = Scene1x(id);
        var r = layers.Report!;
        output.WriteLine($"{id} 1x: {layers.Cost.TotalMilliseconds:0} ms, clearance {r.MinClearance?.ToString("0.0") ?? "-"}, dropped {layers.Dropped}, {layers.Bytes / MiB:0.00} MiB");
        Assert.True(r.Ok, $"{id}: {string.Join(", ", r.Fails)}");
        Assert.Equal((650, 553), (layers.Base.Width, layers.Base.Height));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Nothing_moves_within_2_5_units_of_a_piece(string id)
    {
        var (level, layers) = Scene1x(id);
        var clearance = MoonfallClearance.For(level);
        // The beams' moving share: transparent wherever a texel's centre is within the keep-out (plus a texel, so the
        // bilinear sample never reaches in).
        foreach (var beams in new[] { layers.BeamsA, layers.BeamsB })
        {
            if (beams is null)
            {
                continue;
            }

            var s = beams.Width / (beams.Board.Z - beams.Board.X);
            for (var y = 0; y < beams.Height; y++)
            {
                for (var x = 0; x < beams.Width; x++)
                {
                    if (beams.Pixels[(((y * beams.Width) + x) * 4) + 3] == 0)
                    {
                        continue;
                    }

                    var bx = beams.Board.X + ((x + 0.5f) / s);
                    var by = beams.Board.Y + ((y + 0.5f) / s);
                    Assert.True(clearance.At(bx, by) >= MoonfallMotion.PegKeepOut + (1f / s), $"{id}: a moving beam at ({bx:0}, {by:0}) is {clearance.At(bx, by):0.0} units from a piece");
                }
            }
        }

        // The mist's bands, the stars' twinkle and the lamps' flicker keep off the pieces too.
        foreach (var m in layers.Mist)
        {
            Assert.True(MoonfallMotion.MistBandClear(clearance, m.Tile.Board.Y, m.Tile.Board.W), $"{id}: a mist band crosses a piece");
        }

        foreach (var star in layers.Stars)
        {
            Assert.True(clearance.At(star.X, star.Y) >= MoonfallMotion.PegKeepOut + (star.Radius * 1.8f), $"{id}: a star at ({star.X:0}, {star.Y:0})");
        }

        foreach (var lamp in layers.Flickers)
        {
            Assert.True(clearance.At(lamp.X, lamp.Y) >= MoonfallMotion.ParticleKeepOut, $"{id}: a lamp at ({lamp.X:0}, {lamp.Y:0})");
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Fireflies_and_dust_keep_8_units_from_every_piece(string id)
    {
        var (level, layers) = Scene1x(id);
        var clearance = MoonfallClearance.For(level);
        foreach (var f in layers.Fireflies)
        {
            // Its whole loop, core and halo, at 96 points of the 6 s loop.
            for (var k = 0; k < 96; k++)
            {
                var at = MoonfallMotion.FireflyAt(f, k * MoonfallMotion.Loop / 96.0, still: false);
                var gap = clearance.At(at.X, at.Y) - MoonfallMotion.HaloReach(f.Size);
                Assert.True(gap >= MoonfallMotion.ParticleKeepOut - 0.75f, $"{id}: a firefly's halo at ({at.X:0}, {at.Y:0}) is {gap:0.0} units from a piece");
            }

            Assert.True(MoonfallMotion.FireflyClearance(clearance, f.X, f.Y, f.Size, 24) >= 0);
        }

        // Dust drawn only where it keeps 8 units clear: over a minute of frames, every visible mote does.
        var shown = 0;
        for (var frame = 0; frame < 60 * 60; frame += 7)
        {
            for (var i = 0; i < layers.Dust; i++)
            {
                var (at, _) = MoonfallMotion.Dust(i, frame / 60.0);
                if (MoonfallMotion.DustVisible(clearance, at))
                {
                    shown++;
                    Assert.True(clearance.At(at.X, at.Y) >= MoonfallMotion.ParticleKeepOut);
                }
            }
        }

        Assert.True(layers.Dust == 0 || shown > 0, $"{id}: no dust ever shows");
    }

    [Fact]
    public void The_mist_wraps_at_its_tile_without_a_seam()
    {
        var withMist = MoonfallSceneKit.ShippedScenes().Select(static s => s.Level.Id).Select(Scene1x).Where(static s => s.Layers.Mist.Count > 0).ToList();
        Assert.NotEmpty(withMist);
        foreach (var (_, layers) in withMist)
        {
            foreach (var m in layers.Mist)
            {
                var t = m.Tile;
                int worstSeam = 0, worstInside = 0;
                for (var y = 0; y < t.Height; y++)
                {
                    int A(int x) => t.Pixels[(((y * t.Width) + x) * 4) + 3];
                    worstSeam = Math.Max(worstSeam, Math.Abs(A(0) - A(t.Width - 1)));
                    for (var x = 1; x < t.Width; x++)
                    {
                        worstInside = Math.Max(worstInside, Math.Abs(A(x) - A(x - 1)));
                    }
                }

                // The step across the wrap is no bigger than a step inside the tile.
                Assert.True(worstSeam <= worstInside + 1, $"seam {worstSeam}, inside {worstInside}");
                Assert.InRange(MoonfallMotion.MistOffset(12345.6, m.Speed, t.Board.Z - t.Board.X), 0f, t.Board.Z - t.Board.X);
            }
        }
    }

    // ---- Still, Simple and Full ----

    [Fact]
    public void Reduce_motion_and_plain_hold_everything_still()
    {
        Assert.Equal(MoonfallMotionLevel.Still, MoonfallMotion.For(Flair.Full, reduceMotion: true));
        Assert.Equal(MoonfallMotionLevel.Still, MoonfallMotion.For(Flair.Plain, reduceMotion: false));
        Assert.Equal(MoonfallMotionLevel.Simple, MoonfallMotion.For(Flair.Quiet, reduceMotion: false));
        Assert.Equal(MoonfallMotionLevel.Full, MoonfallMotion.For(Flair.Full, reduceMotion: false));

        var f = new MoonfallFirefly(300, 200, 1f, 0.7f, 2.5f);
        var star = new MoonfallStar(200, 100, 1.2f, 0.3f, 2f);
        foreach (var t in new[] { 0.0, 0.37, 1.9, 4.2, 123.4 })
        {
            Assert.Equal(new Vector2(300, 200), MoonfallMotion.FireflyAt(f, t, still: true));
            Assert.Equal(0.8f, MoonfallMotion.FireflyPulse(f, t, still: true));
            Assert.Equal((0.5f, 0f), MoonfallMotion.Beams(t, still: true));
            Assert.Equal(0f, MoonfallMotion.Twinkle(star, t, still: true));
            Assert.Equal(1f, MoonfallMotion.Flicker(t, 0.4f, still: true));
            Assert.Equal(1f, MoonfallMotion.Breath(t, 3f, 0.1f, still: true));
            Assert.Equal(-1f, MoonfallMotion.Glint(t, 6f, 0f, still: true));
        }

        // In motion the beams' two layers always add up to their breath, and the lantern stays within ±10%.
        for (var t = 0.0; t < 12; t += 0.05)
        {
            var (a, b) = MoonfallMotion.Beams(t, still: false);
            Assert.InRange(a + b, 0f, 1.0001f);
            Assert.InRange(MoonfallMotion.Flicker(t, 0.4f, still: false), 0.9f, 1.1f);
        }
    }

    // ---- Peg marks ----

    [Fact]
    public void Peg_marks_put_a_shape_on_every_kind_but_blue_inside_the_face()
    {
        Assert.Equal(MoonfallPegMark.None, MoonfallPegMarks.For(PegColour.Blue));
        Assert.Equal(MoonfallPegMark.Crescent, MoonfallPegMarks.For(PegColour.Orange));
        Assert.Equal(MoonfallPegMark.Leaf, MoonfallPegMarks.For(PegColour.Green));
        Assert.Equal(MoonfallPegMark.Star, MoonfallPegMarks.For(PegColour.Purple));

        foreach (var mark in new[] { MoonfallPegMark.Crescent, MoonfallPegMark.Leaf, MoonfallPegMark.Star, MoonfallPegMark.StarRim })
        {
            int inside = 0, total = 0;
            for (var v = -1f; v <= 1f; v += 0.01f)
            {
                for (var u = -1f; u <= 1f; u += 0.01f)
                {
                    total++;
                    if (MoonfallPegMarks.Inside(mark, u, v))
                    {
                        inside++;
                        // Engraved on the face: the marks stay well inside it; the star's light rim reaches its edge at
                        // the four tips, as the design draws it (play2.mark: st < 1.02).
                        var reach = mark == MoonfallPegMark.StarRim ? 1.05f : 0.9f;
                        Assert.True((u * u) + (v * v) < reach * reach, $"{mark} reaches the rim at ({u:0.00}, {v:0.00})");
                    }
                }
            }

            // Each mark is a real shape: between 3% and 40% of the peg's box.
            Assert.InRange(inside / (float)total, 0.03f, 0.40f);
            Assert.False(MoonfallPegMarks.Inside(mark, mark == MoonfallPegMark.StarRim ? 1.05f : 0.95f, 0f));
        }

        // The star and its rim never overlap; the leaf has its midrib cut.
        Assert.False(MoonfallPegMarks.Inside(MoonfallPegMark.Leaf, 0f, 0f));
        Assert.True(MoonfallPegMarks.Inside(MoonfallPegMark.Star, 0f, 0f));
        Assert.False(MoonfallPegMarks.Inside(MoonfallPegMark.StarRim, 0f, 0f));
    }

    [Fact]
    public void The_peg_marks_sheet_holds_each_mark_in_its_own_cell()
    {
        var sheet = MoonfallPegMarks.Sheet(32);
        Assert.Equal((32 * MoonfallPegMarks.Cells, 32), (sheet.Width, sheet.Height));
        for (var k = 0; k < MoonfallPegMarks.Cells; k++)
        {
            var mark = (MoonfallPegMark)(k + 1);
            var (uv0, uv1) = MoonfallPegMarks.Uv(mark);
            Assert.Equal(k / 4f, uv0.X);
            Assert.Equal((k + 1) / 4f, uv1.X);
            var ink = 0;
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var o = ((y * sheet.Width) + (k * 32) + x) * 4;
                    Assert.Equal(255, sheet.Pixels[o]);
                    ink += sheet.Pixels[o + 3] > 40 ? 1 : 0;
                }

                // Nothing at the cell's edge but the star rim's tip: a mark never bleeds into its neighbour.
                Assert.True(sheet.Pixels[(((y * sheet.Width) + (k * 32)) * 4) + 3] < (mark == MoonfallPegMark.StarRim ? 128 : 16));
            }

            Assert.True(ink > 30, $"{mark} has {ink} inked pixels");
        }
    }

    // ---- Style ribbons ----

    [Fact]
    public void A_style_ribbon_keeps_6_units_from_every_live_piece_and_off_the_launcher()
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[2];
        var pieces = level.Pegs.Select(static p => new Vector3((float)p.X, (float)p.Y, (float)p.Radius)).ToArray();
        var size = new Vector2(110, 34);
        var first = MoonfallRibbons.Place(pieces, size, []);
        Assert.NotNull(first);
        var c = first.Value;
        var half = size / 2;
        foreach (var p in pieces)
        {
            Assert.True(MoonfallRibbons.RectDistance(c, half, new Vector2(p.X, p.Y)) - p.Z >= MoonfallRibbons.Keep);
        }

        Assert.True(MoonfallRibbons.RectDistance(c, half, MoonfallFramingCheck.Pivot) >= MoonfallRibbons.SwingReach);
        Assert.True(c.X - half.X > 75 && c.X + half.X < 725 && c.Y - half.Y > 41 && c.Y + half.Y < 594);

        // A second ribbon does not land on the first.
        Vector4[] taken = [new(c.X - half.X, c.Y - half.Y, c.X + half.X, c.Y + half.Y)];
        var second = MoonfallRibbons.Place(pieces, size, taken);
        Assert.NotNull(second);
        Assert.False(MathF.Abs(second.Value.X - c.X) < size.X && MathF.Abs(second.Value.Y - c.Y) < size.Y);

        // A board with no room places none (the ribbon is skipped, never forced onto the pegs).
        var crowded = new List<Vector3>();
        for (var y = 41f; y < 600; y += 14)
        {
            for (var x = 75f; x < 725; x += 14)
            {
                crowded.Add(new Vector3(x, y, 4));
            }
        }

        Assert.Null(MoonfallRibbons.Place(crowded.ToArray(), size, []));
    }

    // ---- The game art's fallbacks ----

    private sealed class Tex(string name, long bytes)
    {
        public string Name { get; } = name;

        public long Bytes { get; } = bytes;
    }

    /// <summary>A host with the game's textures it is given (by path), Moonfall's shipped pictures, and a record of what it did.</summary>
    private sealed class FakeHost(IReadOnlyDictionary<string, MoonfallImage> game, bool pictures) : IMoonfallGameArtHost<Tex>
    {
        public int Frame { get; set; }

        public ConcurrentBag<string> Warnings { get; } = [];

        public ConcurrentBag<Tex> Uploaded { get; } = [];

        public List<Tex> Disposed { get; } = [];

        public ConcurrentBag<string> Read { get; } = [];

        public Task<MoonfallImage?> ReadGameTexture(string path)
        {
            Read.Add(path);
            return Task.FromResult(game.TryGetValue(path, out var image) ? image : null);
        }

        public Task<MoonfallImage?> ReadPicture(string name) => Task.FromResult(pictures ? MoonfallSceneKit.Picture(name, twoX: false) : null);

        public Task<Tex> Upload(MoonfallRgba pixels, string name)
        {
            var t = new Tex(name, pixels.Bytes);
            Uploaded.Add(t);
            return Task.FromResult(t);
        }

        public void Dispose(Tex texture) => Disposed.Add(texture);

        public void Warn(string message) => Warnings.Add(message);
    }

    private static MoonfallImage Flat(int w, int h, float grey)
    {
        var image = new MoonfallImage(w, h, true);
        Array.Fill(image.R.Data, grey);
        Array.Fill(image.G.Data, grey * 0.9f);
        Array.Fill(image.B.Data, grey * 0.6f);
        Array.Fill(image.A!.Data, 1f);
        return image;
    }

    private static Dictionary<string, MoonfallImage> AllUiTextures(params string[] except)
    {
        var d = new Dictionary<string, MoonfallImage>(StringComparer.Ordinal);
        foreach (var (path, w, h) in MoonfallChromeArt.Textures)
        {
            if (!except.Contains(path))
            {
                d[path] = Flat(w, h, 0.7f);
            }
        }

        return d;
    }

    /// <summary>Frames until <paramref name="done"/> (the work runs on the thread pool), at most 20 s.</summary>
    private static void Until(MoonfallGameArt<Tex> art, FakeHost host, MoonfallLevel level, Func<bool> done, bool twoX = false)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!done() && DateTime.UtcNow < until)
        {
            host.Frame++;
            art.Frame(level, twoX, pegMarks: false);
            Thread.Sleep(2);
        }

        Assert.True(done(), "the game art never settled");
    }

    private static MoonfallLevel LevelTaking(string recipe) =>
        MoonfallCampaigns.LoadBuiltIn().Base.Levels.First(l => MoonfallSceneRecipeLoader.Pick(MoonfallSceneRecipeLoader.LoadBuiltIn(), l)?.Name == recipe);

    [Fact]
    public void Without_the_games_ui_art_the_frame_falls_back_and_nothing_crashes()
    {
        var host = new FakeHost(new Dictionary<string, MoonfallImage>(), pictures: true);
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level = LevelTaking("moon-road-night");
        Until(art, host, level, () => art.Chrome is not null && art.SceneState is not MoonfallSceneState.Building);
        Assert.Equal(MoonfallChromeArt.Textures.Count, art.Chrome!.Missing.Count);
        Assert.False(art.Chrome.FrameReady);
        Assert.Null(art.ChromeTexture);
        Assert.Equal(MoonfallChromeArt.Textures.Count, host.Warnings.Count(static w => w.Contains("missing or changed", StringComparison.Ordinal)));
        // The level's own picture still builds its scene.
        Assert.Equal(MoonfallSceneState.Ready, art.SceneState);
        Assert.NotNull(art.Scene);
        // Every read was a ui/ path of the install, never a file.
        Assert.All(host.Read, static p => Assert.StartsWith("ui/", p, StringComparison.Ordinal));
    }

    [Fact]
    public void A_ui_texture_a_patch_resized_drops_only_its_own_parts()
    {
        var game = AllUiTextures();
        game["ui/uld/TripleTriadResultCrown_hr1.tex"] = Flat(700, 256, 0.7f);
        var host = new FakeHost(game, pictures: true);
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level = LevelTaking("moon-road-night");
        Until(art, host, level, () => art.ChromeTexture is not null);
        var missing = Assert.Single(art.Chrome!.Missing);
        Assert.StartsWith("ui/uld/TripleTriadResultCrown_hr1.tex (700 x 256", missing, StringComparison.Ordinal);
        Assert.True(art.Chrome.FrameReady);
        Assert.Null(art.Chrome[MoonfallChromePart.Laurel]);
        Assert.NotNull(art.Chrome[MoonfallChromePart.CornerTop]);
        Assert.Single(host.Warnings, static w => w.Contains("TripleTriadResultCrown", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_painting_falls_back_to_its_picture_then_to_the_night_sky()
    {
        // The game's painting is missing: the recipe's shipped fallback picture is drawn instead, whole.
        var host = new FakeHost(AllUiTextures(), pictures: true);
        using (var art = new MoonfallGameArt<Tex>(host, MoonfallSceneRecipeLoader.LoadBuiltIn()))
        {
            var level = LevelTaking("airship-road");
            Until(art, host, level, () => art.SceneState is MoonfallSceneState.Ready or MoonfallSceneState.Failed);
            Assert.Equal(MoonfallSceneState.Ready, art.SceneState);
            Assert.Equal("airship-road", art.Scene!.Layers.Name);
        }

        // Neither the painting nor any picture: the scene fails (logged once) and the board shows the night sky.
        var bare = new FakeHost(AllUiTextures(), pictures: false);
        using var art2 = new MoonfallGameArt<Tex>(bare, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level2 = LevelTaking("airship-road");
        Until(art2, bare, level2, () => art2.SceneState is MoonfallSceneState.Failed);
        for (var i = 0; i < 10; i++)
        {
            bare.Frame++;
            art2.Frame(level2, false, false);
        }

        Assert.Null(art2.Scene);
        Assert.Single(bare.Warnings, static w => w.Contains("scene airship-road", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_card_shows_the_companion_as_a_plain_ring_and_is_logged_once()
    {
        var host = new FakeHost(AllUiTextures(), pictures: true);
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level = LevelTaking("moon-road-night");
        Until(art, host, level, () =>
        {
            art.Card(MoonfallPower.Wings);
            return host.Warnings.Any(static w => w.Contains("087058", StringComparison.Ordinal));
        });
        for (var i = 0; i < 10; i++)
        {
            host.Frame++;
            art.Frame(level, false, false);
            Assert.Null(art.Card(MoonfallPower.Wings));
        }

        Assert.Single(host.Warnings, static w => w.Contains("087058", StringComparison.Ordinal));
    }

    [Fact]
    public void Closing_the_window_lets_every_texture_go_and_reopening_only_uploads()
    {
        var game = AllUiTextures();
        game[MoonfallCards.CardPath(87058)] = Flat(MoonfallCards.CardWidth, MoonfallCards.CardHeight, 0.6f);
        var host = new FakeHost(game, pictures: true);
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level = LevelTaking("moon-road-night");
        Until(art, host, level, () => art.ChromeTexture is not null && art.Scene is not null && art.Card(MoonfallPower.Wings) is not null);
        var held = art.HeldBytes();
        Assert.True(held.Chrome > 0 && held.Cards > 0 && held.Scene > 0);
        var readsBefore = host.Read.Count;

        art.Release();
        for (var i = 0; i <= MoonfallGameArt<Tex>.RetireFrames + 1; i++)
        {
            host.Frame++;
            art.Tick();
        }

        Assert.Equal(0, art.HeldBytes().Total);
        Assert.Equal(0, art.Retiring);
        Assert.Equal(host.Uploaded.Count, host.Disposed.Count);

        // Reopened: the graded pixels were kept for the session, so nothing is read from the install again.
        Until(art, host, level, () => art.ChromeTexture is not null && art.Scene is not null);
        Assert.Equal(readsBefore, host.Read.Count);
    }

    // ---- The budget ----

    [Fact]
    public void The_in_play_art_keeps_to_the_texture_budget()
    {
        // spec-rich2.md §6: the graded UI art about 4 MiB, the cards 2.2 MiB, a scene 7.3 MiB at 2x, the peak about 21 MiB.
        var chrome = MoonfallChromeArt.Build(AllUiTextures());
        var cards = MoonfallCards.Cast.Count * (long)MoonfallCards.CardWidth * MoonfallCards.CardHeight * 4;
        var marks = MoonfallPegMarks.Sheet().Bytes;
        var atlas = MoonfallAtlas.Parse(File.ReadAllText(Path.Combine(MoonfallArtTests.ArtFolder(), "atlas.json"))).Atlas!;
        var sheets = atlas.SheetBytes(false) + atlas.SheetBytes(true);
        long worstScene = 0;
        foreach (var (level, recipe) in MoonfallSceneKit.ShippedScenes())
        {
            var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
            var layers = MoonfallSceneBuilder.Build(recipe, level, painting!, 2, fallback);
            var enamel = chrome.Grain is { } g ? (long)(g.Width / 2) * (g.Height / 2) * 4 : 0;
            output.WriteLine($"{level.Id} ({recipe.Name}) at 2x: {layers.Bytes / MiB:0.00} MiB of layers + {enamel / MiB:0.00} MiB enamel");
            worstScene = Math.Max(worstScene, layers.Bytes + enamel);
        }

        var peak = chrome.Sheet.Bytes + cards + marks + sheets + worstScene;
        output.WriteLine($"chrome {chrome.Sheet.Bytes / MiB:0.00} MiB, cards {cards / MiB:0.00} MiB, marks {marks / MiB:0.00} MiB, atlas sheets {sheets / MiB:0.00} MiB, worst scene {worstScene / MiB:0.00} MiB; peak {peak / MiB:0.00} MiB");
        Assert.True(chrome.Sheet.Bytes <= 4.5 * MiB, $"chrome {chrome.Sheet.Bytes / MiB:0.00} MiB");
        Assert.True(worstScene <= 7.3 * MiB, $"scene {worstScene / MiB:0.00} MiB");
        Assert.True(peak <= 21 * MiB, $"peak {peak / MiB:0.00} MiB");
    }

    [Fact]
    public void The_text_floors_hold_at_the_smallest_window()
    {
        // At 640 x 480 the board draws at about 0.55 px a unit: a label meets its floor or is left out; a number or a
        // name is raised to meet it.
        foreach (var pxPerUnit in new[] { 0.5f, 0.55f, 0.8f, 1f, 1.6f })
        {
            foreach (var (size, cap) in new[] { (9f, 0.583f), (10.5f, 0.583f), (16.5f, 0.62f), (24f, 0.62f), (23f, 0.51f) })
            {
                var label = MoonfallHud.LabelSize(size, pxPerUnit, cap);
                Assert.True(label == 0 || label * cap >= MoonfallHud.LabelFloor - 1e-3f);
                Assert.True(MoonfallHud.NumberSize(size, pxPerUnit, cap) * cap >= MoonfallHud.NumberFloor - 1e-3f);
                Assert.True(MoonfallHud.RequiredLabelSize(size, pxPerUnit, cap) * cap >= MoonfallHud.LabelFloor - 1e-3f);
            }
        }

        Assert.True(MoonfallHud.CardInMargin(96f));
        Assert.False(MoonfallHud.CardInMargin(95f));
        Assert.Equal(180f, MoonfallHud.MarginCardWidth(240f));
    }

    // ---- The cast agrees with Core's ----

    [Fact]
    public void Each_power_shows_the_card_of_the_companion_who_carries_it()
    {
        foreach (var card in MoonfallCards.Cast)
        {
            var companion = MoonfallCompanions.Get(MoonfallCompanions.Carrying(card.Power));
            Assert.Equal(companion.CardIcon, card.CardIcon);
            Assert.Matches(@"^ui/icon/087000/0870\d\d_hr1\.tex$", card.CardPath);
            Assert.Equal(MoonfallPowers.Shots(card.Power), card.Turns);
        }

        // The accents keep apart in OKLab hue. r2cast.py says "at least 25 degrees"; the approved accents come to 23.7
        // at the closest (Louisoix's #70D3BB and Minfilia's #5DDAE0), so this holds them at 20 and the table to the design's.
        var hues = MoonfallCards.Cast.Select(static c => MoonfallColor.ToOklab(c.Accent)).Select(static l => MathF.Atan2(l.Z, l.Y) * 180 / MathF.PI).ToList();
        for (var i = 0; i < hues.Count; i++)
        {
            for (var j = i + 1; j < hues.Count; j++)
            {
                var d = MathF.Abs(hues[i] - hues[j]) % 360;
                Assert.True(MathF.Min(d, 360 - d) >= 20f, $"{MoonfallCards.Cast[i].Power} and {MoonfallCards.Cast[j].Power}: {MathF.Min(d, 360 - d):0}°");
            }
        }
    }

    // ---- Per frame: nothing allocated ----

    [Fact]
    public void The_per_frame_motion_and_placement_allocate_nothing()
    {
        // The repo's allocation check: the same calls warmed up, then measured.
        var (level, layers) = Scene1x(MoonfallSceneKit.ShippedScenes().First().Level.Id);
        var clearance = layers.Clearance!;
        var pieces = level.Pegs.Select(static p => new Vector3((float)p.X, (float)p.Y, (float)p.Radius)).ToArray();
        var fireflies = layers.Fireflies.ToArray();
        var stars = layers.Stars.ToArray();
        var sink = 0f;

        void Frames(int count)
        {
            Span<Vector4> taken = stackalloc Vector4[4];
            for (var n = 0; n < count; n++)
            {
                var t = n / 60.0;
                var (a, b) = MoonfallMotion.Beams(t, still: false);
                sink += a + b + MoonfallMotion.Flicker(t, 0.3f, false) + MoonfallMotion.Breath(t, 3f, 0.1f, false) + MoonfallMotion.Glint(t, 6f, 3f, false);
                sink += MoonfallMotion.MistOffset(t, 4f, 650f);
                foreach (var f in fireflies)
                {
                    sink += MoonfallMotion.FireflyAt(f, t, false).X + MoonfallMotion.FireflyPulse(f, t, false);
                }

                foreach (var s in stars)
                {
                    sink += MoonfallMotion.Twinkle(s, t, false);
                }

                for (var i = 0; i < 40; i++)
                {
                    var (at, fade) = MoonfallMotion.Dust(i, t);
                    sink += MoonfallMotion.DustVisible(clearance, at) ? fade : 0;
                }

                if (n % 30 == 0)
                {
                    sink += MoonfallRibbons.Place(pieces, new Vector2(110, 34), taken[..1])?.X ?? 0;
                }
            }
        }

        Frames(120);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Frames(600);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotEqual(0f, sink);
    }

    // ---- Source lints ----

    private static IEnumerable<(string Path, string Source)> ArtSources()
    {
        var root = ResxFiles.RepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "Tsukimichi.Core", "Moonfall", "Art"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "Tsukimichi", "Ui"), "Moonfall*.cs"));
        return files.Select(f => (Path.GetRelativePath(root, f), File.ReadAllText(f)));
    }

    [Fact]
    public void The_runtime_art_writes_no_file_and_reaches_no_network()
    {
        // Game art is read from the player's install at runtime and kept in memory: nothing is cached to disk (least of
        // all into the plugin's folder), and nothing goes online.
        var writes = new Regex(@"\b(File\.(Write\w*|Create\w*|Append\w*|Copy|Move|Replace|Delete|Open(?!Read)\w*|SetAttributes)|FileStream\([^;]*(FileAccess\.(Write|ReadWrite)|FileMode\.(Create\w*|Append|OpenOrCreate|Truncate))|StreamWriter|BinaryWriter|Directory\.(Create\w*|Delete|Move)|IsolatedStorage)", RegexOptions.CultureInvariant);
        var sources = ArtSources().ToList();
        Assert.True(sources.Count > 20);
        var offenders = sources.Where(s => writes.IsMatch(Strip(s.Source))).Select(static s => s.Path).ToList();
        Assert.True(offenders.Count == 0, "writes a file: " + string.Join(", ", offenders));
        Assert.Empty(NoNetworkTests.Offenders(sources));
    }

    [Fact]
    public void The_lint_catches_a_file_write()
    {
        var writes = new Regex(@"\b(File\.(Write\w*|Create\w*)|FileStream\([^;]*FileAccess\.(Write|ReadWrite))", RegexOptions.CultureInvariant);
        Assert.Matches(writes, Strip("var x = 1; File.WriteAllBytes(path, bytes);"));
        Assert.Matches(writes, Strip("using var s = new FileStream(path, FileMode.Open, FileAccess.Write);"));
        Assert.DoesNotMatch(writes, Strip("using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);"));
        Assert.DoesNotMatch(writes, Strip("// File.WriteAllBytes is never called\nvar s = \"File.Create\";"));
    }

    /// <summary>The source without its comments and string literals (so a doc mention is not a call).</summary>
    private static string Strip(string source) =>
        Regex.Replace(source, @"//[^\n]*|/\*.*?\*/|@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""", " ", RegexOptions.Singleline);
}
