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
        return (level, MoonfallSceneBuilder.Build(recipe, level, painting, 1, fallback, check: true, plates: MoonfallSceneKit.Plates(recipe)));
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
            Assert.True(clearance.At(star.X, star.Y) >= MoonfallMotion.ParticleKeepOut + 2, $"{id}: a star at ({star.X:0}, {star.Y:0})");
        }

        foreach (var lamp in layers.Flickers)
        {
            Assert.True(clearance.At(lamp.X, lamp.Y) >= MoonfallMotion.ParticleKeepOut + (0.6f * lamp.Halo), $"{id}: a lamp at ({lamp.X:0}, {lamp.Y:0})");
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

    // ---- F6: every peg reads against its scene (readcheck, level-method.md §8) ----

    private static readonly Vector3 Lum = new(0.2126f, 0.7152f, 0.0722f);

    /// <summary>Each kind's face (the median over its variants of the 80th-percentile luma in 0.85 r, unlit, at 1x).</summary>
    /// <summary>The 640 × 480 window's board scale against 1x: its content height, 441 px, over the board's 600 units.</summary>
    private const float SmallWindowScale = 0.735f;

    /// <summary>
    /// Each kind's brick face: the 80th-percentile luma of its unlit sprite's middle (the part that repeats along a brick,
    /// between its two caps), opaque pixels only, the median over nothing (a brick has one sprite a kind).
    /// </summary>
    private static Dictionary<PegColour, float> BrickFaces()
    {
        var folder = MoonfallArtTests.ArtFolder();
        var atlas = MoonfallAtlas.Parse(File.ReadAllText(Path.Combine(folder, "atlas.json"))).Atlas!;
        var png = MoonfallPng.Decode(File.ReadAllBytes(Path.Combine(folder, atlas.OneXFile)), out _)!.Value;
        var faces = new Dictionary<PegColour, float>();
        foreach (var kind in new[] { PegColour.Blue, PegColour.Orange, PegColour.Green, PegColour.Purple })
        {
            var r = atlas.Brick(kind, false);
            var lum = new List<float>();
            for (var y = (int)r.Y; y < (int)(r.Y + r.H); y++)
            {
                for (var x = (int)(r.X + (r.H / 2)); x < (int)(r.X + r.W - (r.H / 2)); x++)
                {
                    var o = ((y * png.Width) + x) * 4;
                    if (png.Rgba[o + 3] >= 128)
                    {
                        lum.Add(Vector3.Dot(new Vector3(png.Rgba[o], png.Rgba[o + 1], png.Rgba[o + 2]) / 255f, Lum));
                    }
                }
            }

            lum.Sort();
            faces[kind] = lum[(int)(lum.Count * 0.8)];
        }

        return faces;
    }

    private static Dictionary<PegColour, float> Faces()
    {
        var folder = MoonfallArtTests.ArtFolder();
        var atlas = MoonfallAtlas.Parse(File.ReadAllText(Path.Combine(folder, "atlas.json"))).Atlas!;
        var png = MoonfallPng.Decode(File.ReadAllBytes(Path.Combine(folder, atlas.OneXFile)), out _)!.Value;
        var faces = new Dictionary<PegColour, float>();
        foreach (var kind in new[] { PegColour.Blue, PegColour.Orange, PegColour.Green, PegColour.Purple })
        {
            var perVariant = new List<float>();
            for (var v = 0; v < atlas.PegVariants; v++)
            {
                var r = atlas.Peg(kind, v, false);
                var cx = r.X + r.AnchorX;
                var cy = r.Y + r.AnchorY;
                var lum = new List<float>();
                for (var y = (int)(cy - 10); y <= (int)(cy + 10); y++)
                {
                    for (var x = (int)(cx - 10); x <= (int)(cx + 10); x++)
                    {
                        var dx = x + 0.5f - cx;
                        var dy = y + 0.5f - cy;
                        if ((dx * dx) + (dy * dy) >= 8.5f * 8.5f)
                        {
                            continue;
                        }

                        var o = ((y * png.Width) + x) * 4;
                        lum.Add(Vector3.Dot(new Vector3(png.Rgba[o], png.Rgba[o + 1], png.Rgba[o + 2]) / 255f, Lum));
                    }
                }

                lum.Sort();
                perVariant.Add(lum[(int)(lum.Count * 0.8)]);
            }

            perVariant.Sort();
            faces[kind] = perVariant[perVariant.Count / 2];
        }

        return faces;
    }

    /// <summary>The opening's luma at 1x as the player sees it at the deal: the scene with every static round peg's veil under it.</summary>
    private static MoonfallPlane VeiledLuma(MoonfallLevel level, MoonfallSceneLayers layers)
    {
        var b = layers.Base;
        Assert.Equal(1f, b.Width / (b.Board.Z - b.Board.X), 3);
        var y0 = new MoonfallPlane(b.Width, b.Height);
        for (var i = 0; i < y0.Data.Length; i++)
        {
            y0.Data[i] = Vector3.Dot(new Vector3(b.Pixels[i * 4], b.Pixels[(i * 4) + 1], b.Pixels[(i * 4) + 2]) / 255f, Lum);
        }

        foreach (var peg in level.Pegs.Where(static p => p.Shape == PegShape.Round && p.Mover.Kind == MoverKind.None))
        {
            VeilAt(y0, b.Board, 1f, (float)peg.X, (float)peg.Y, (float)peg.Radius, layers.VeilK);
        }

        // The bricks' veil, as the window draws it: a row of sprites along each brick.
        Span<Vector2> spots = stackalloc Vector2[64];
        foreach (var brick in level.Pegs.Where(static p => p.Shape != PegShape.Round))
        {
            var n = MoonfallVeil.BrickSpots(brick.Shape, (float)brick.X, (float)brick.Y, (float)brick.X2, (float)brick.Y2, (float)brick.Radius,
                (float)(brick.StartDegrees * Math.PI / 180), (float)(brick.SweepDegrees * Math.PI / 180), spots);
            for (var j = 0; j < n; j++)
            {
                VeilAt(y0, b.Board, 1f, spots[j].X, spots[j].Y, (float)MoonfallRules.PegRadius, MoonfallVeil.BrickAlpha(layers.VeilK));
            }
        }

        return y0;
    }

    private static readonly Lazy<MoonfallRgba> VeilSprite = new(() => MoonfallVeil.Sprite());

    /// <summary>Darkens <paramref name="y"/> (at <paramref name="s"/> px a unit over <paramref name="board"/>) by a peg's veil at (px, py).</summary>
    private static void VeilAt(MoonfallPlane y, Vector4 board, float s, float px, float py, float r, float k)
    {
        var sprite = VeilSprite.Value;
        var reach = r * MoonfallVeil.Reach;
        for (var yy = (int)MathF.Max(0, (py - reach - board.Y) * s); yy < MathF.Min(y.Height, (py + reach - board.Y) * s); yy++)
        {
            for (var xx = (int)MathF.Max(0, (px - reach - board.X) * s); xx < MathF.Min(y.Width, (px + reach - board.X) * s); xx++)
            {
                var u = (int)(((((xx + 0.5f) / s) + board.X - (px - reach)) / (2 * reach)) * sprite.Width);
                var v = (int)(((((yy + 0.5f) / s) + board.Y - (py - reach)) / (2 * reach)) * sprite.Height);
                if ((uint)u < (uint)sprite.Width && (uint)v < (uint)sprite.Height)
                {
                    y.Data[(yy * y.Width) + xx] *= 1 - (k * sprite.Pixels[(((v * sprite.Width) + u) * 4) + 3] / 255f);
                }
            }
        }
    }

    /// <summary>The 90th percentile of <paramref name="y"/> within <paramref name="reach"/> of a brick's spot, 3-11 units outside the brick's edge (<paramref name="alone"/>: its own clearance).</summary>
    private static float BrickRingP90(MoonfallPlane y, Vector4 board, float s, MoonfallClearance alone, Vector2 spot, float reach)
    {
        var values = new List<float>();
        for (var py = (int)((spot.Y - reach - board.Y) * s); py <= (int)((spot.Y + reach - board.Y) * s); py++)
        {
            for (var px = (int)((spot.X - reach - board.X) * s); px <= (int)((spot.X + reach - board.X) * s); px++)
            {
                if ((uint)px >= (uint)y.Width || (uint)py >= (uint)y.Height)
                {
                    continue;
                }

                var bx = ((px + 0.5f) / s) + board.X;
                var by = ((py + 0.5f) / s) + board.Y;
                var d = alone.At(bx, by);
                if (d > 3 && d < 11 && Vector2.Distance(new Vector2(bx, by), spot) <= reach)
                {
                    values.Add(y.Data[(py * y.Width) + px]);
                }
            }
        }

        if (values.Count == 0)
        {
            return 0f;
        }

        values.Sort();
        return values[(int)(values.Count * 0.9)];
    }

    private static float RingP90(MoonfallPlane y, Vector4 board, float s, float x, float yc, float r)
    {
        var values = new List<float>();
        for (var py = (int)((yc - r - 10 - board.Y) * s); py <= (int)((yc + r + 10 - board.Y) * s); py++)
        {
            for (var px = (int)((x - r - 10 - board.X) * s); px <= (int)((x + r + 10 - board.X) * s); px++)
            {
                if ((uint)px >= (uint)y.Width || (uint)py >= (uint)y.Height)
                {
                    continue;
                }

                var d = MathF.Sqrt(MathF.Pow(((px + 0.5f) / s) + board.X - x, 2) + MathF.Pow(((py + 0.5f) / s) + board.Y - yc, 2));
                if (d > r + 2 && d < r + 9)
                {
                    values.Add(y.Data[(py * y.Width) + px]);
                }
            }
        }

        if (values.Count == 0)
        {
            return 0f;
        }

        values.Sort();
        return values[(int)(values.Count * 0.9)];
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Every_peg_reads_against_its_scene_and_the_scene_keeps_under_its_ceiling(string id)
    {
        var (level, layers) = Scene1x(id);
        ReadsAndKeepsUnderTheCeiling(id, level, layers);
    }

    /// <summary>Levels whose scene the spoiler shield can swap for its story-safe variant (a recipe with a fallback).</summary>
    public static IEnumerable<object[]> SafeLevels() =>
        MoonfallSceneKit.ShippedScenes().Where(static s => s.Recipe.Fallback is not null).Select(static s => new object[] { s.Level.Id });

    [Theory]
    [MemberData(nameof(SafeLevels))]
    public void Every_peg_reads_against_its_story_safe_scene(string id)
    {
        // The shield's own variant (MoonfallSceneBuilder.StorySafe, as MoonfallGameArt draws it): the recipe's dress over
        // its placeless fallback picture, whole and ungraded (UX runtime round 1, m4).
        var (level, recipe) = MoonfallSceneKit.ShippedScenes().First(s => s.Level.Id == id);
        var safe = MoonfallSceneBuilder.StorySafe(recipe)!;
        Assert.DoesNotContain(safe.Paint, static l => l is MoonfallPlate);
        var picture = MoonfallSceneKit.Picture(safe.Source.Path);
        Assert.NotNull(picture);
        var layers = MoonfallSceneBuilder.Build(safe, level, picture, 1, check: true, plates: MoonfallSceneKit.Plates(safe));
        Assert.True(layers.Report!.Ok, $"{id} safe: {string.Join(", ", layers.Report.Fails)}");
        ReadsAndKeepsUnderTheCeiling(id + " safe", level, layers);
    }

    private void ReadsAndKeepsUnderTheCeiling(string id, MoonfallLevel level, MoonfallSceneLayers layers)
    {
        // F6: each kind's face stands at least 0.20 above the 90th percentile of the veiled scene 2-9 units round every
        // place it can be dealt to (movers along their path, with their own veil), and each brick's (every kind it may
        // be dealt) 3-11 units outside its edge, at 1x and at 0.735x (the 640 window's board).
        // The value ceiling: the scene behind the board keeps its 99th-percentile luma at 0.46 or less, and no piece
        // comes within a moon's radius and 18 units.
        var faces = Faces();
        var veiled = VeiledLuma(level, layers);
        var board = layers.Base.Board;
        var small = new MoonfallImage(new MoonfallPlane(veiled.Width, veiled.Height), veiled.Copy(), new MoonfallPlane(veiled.Width, veiled.Height));
        var atSmall = MoonfallFilters.Resize(small, (int)(veiled.Width * SmallWindowScale), (int)(veiled.Height * SmallWindowScale)).G;
        var brickFaces = BrickFaces();
        var bricks = level.Pegs.Where(static p => p.Shape != PegShape.Round).Select(static p => (Piece: p, Alone: MoonfallClearance.For(new MoonfallLevel("t", "t", [p])))).ToList();
        var worst = new Dictionary<string, (float Margin, float X, float Y)>(StringComparer.Ordinal);
        Span<Vector2> spots = stackalloc Vector2[64];
        foreach (var (plane, s, tier) in new[] { (veiled, 1f, "1x"), (atSmall, SmallWindowScale, "0.735x") })
        {
            // A brick: every kind it may be dealt, against the veiled scene 3-11 units outside its edge, round each spot
            // along its middle (UX runtime round 1, m3).
            foreach (var (brick, alone) in bricks)
            {
                var n = MoonfallVeil.BrickSpots(brick.Shape, (float)brick.X, (float)brick.Y, (float)brick.X2, (float)brick.Y2, (float)brick.Radius,
                    (float)(brick.StartDegrees * Math.PI / 180), (float)(brick.SweepDegrees * Math.PI / 180), spots);
                for (var j = 0; j < n; j++)
                {
                    var p90 = BrickRingP90(plane, board, s, alone, spots[j], (float)(brick.Thickness / 2) + 11);
                    foreach (var kind in brickFaces.Keys)
                    {
                        if ((kind == PegColour.Orange && !brick.CanBeOrange) || (kind == PegColour.Green && !brick.CanBeGreen))
                        {
                            continue;
                        }

                        var key = $"{kind} brick {tier}";
                        var margin = brickFaces[kind] - p90;
                        if (!worst.TryGetValue(key, out var w) || margin < w.Margin)
                        {
                            worst[key] = (margin, spots[j].X, spots[j].Y);
                        }
                    }
                }
            }

            foreach (var peg in level.Pegs.Where(static p => p.Shape == PegShape.Round))
            {
                foreach (var (x, y) in MoonfallClearance.Path(peg))
                {
                    var ring = plane;
                    if (peg.Mover.Kind != MoverKind.None)
                    {
                        ring = plane.Copy();
                        VeilAt(ring, board, s, x, y, (float)peg.Radius, layers.VeilK);
                    }

                    var p90 = RingP90(ring, board, s, x, y, (float)peg.Radius);
                    foreach (var kind in faces.Keys)
                    {
                        if ((kind == PegColour.Orange && !peg.CanBeOrange) || (kind == PegColour.Green && !peg.CanBeGreen))
                        {
                            continue;
                        }

                        var key = $"{kind} {tier}";
                        var margin = faces[kind] - p90;
                        if (!worst.TryGetValue(key, out var w) || margin < w.Margin)
                        {
                            worst[key] = (margin, x, y);
                        }
                    }
                }
            }
        }

        foreach (var (key, w) in worst.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"{id} {key}: worst margin {w.Margin:0.000} at ({w.X:0}, {w.Y:0})");
        }

        Assert.All(worst, p => Assert.True(p.Value.Margin >= 0.20f, $"{id} {p.Key}: margin {p.Value.Margin:0.000} at ({p.Value.X:0}, {p.Value.Y:0})"));

        // The ceiling, on the veiled scene with no pieces (readcheck's piece-free composite; the moon excepted: it keeps
        // clear of the pieces instead).
        var lums = new List<float>(veiled.Data.Length);
        for (var i = 0; i < veiled.Data.Length; i++)
        {
            var bx = board.X + (i % layers.Base.Width) + 0.5f;
            var by = board.Y + (i / layers.Base.Width) + 0.5f;
            if (layers.Moon is { } moon && Vector2.Distance(new Vector2(bx, by), new Vector2(moon.X, moon.Y)) < moon.R * 3.2f)
            {
                continue;
            }

            lums.Add(veiled.Data[i]);
        }

        lums.Sort();
        var p99 = lums[(int)(lums.Count * 0.99)];
        output.WriteLine($"{id}: scene p99 luma {p99:0.000}");
        Assert.True(p99 <= 0.46f, $"{id}: the scene's 99th-percentile luma is {p99:0.000}");
        if (layers.Moon is { } m)
        {
            Assert.True(MoonfallClearance.For(level).At(m.X, m.Y) >= m.R + MoonfallSceneBuilder.MoonKeep);
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void What_the_recipe_asks_for_survives_its_level(string id)
    {
        // A silent drop passed before (critic runtime round 1, m4): every Fever moon a recipe names, every mist band (with
        // some cover in it), and at least half the stars it asks for are built; every star sits in the recipe's sky
        // (MotionRecipe.StarWhere) and out of every moon's disc (game designer runtime round 1, M1); no part is dropped.
        var (level, recipe) = MoonfallSceneKit.ShippedScenes().First(s => s.Level.Id == id);
        var (_, layers) = Scene1x(id);
        output.WriteLine($"{id}: moon {layers.Moon is not null}/{recipe.Moon is not null}, mist {layers.Mist.Count}/{recipe.Motion.Mist.Count}, stars {layers.Stars.Count}/{recipe.Motion.Stars}, dropped {layers.Dropped}");
        Assert.Equal(recipe.Moon is not null, layers.Moon is not null);
        Assert.Equal(recipe.Motion.Mist.Count, layers.Mist.Count);
        foreach (var m in layers.Mist)
        {
            var cover = 0.0;
            for (var i = 3; i < m.Tile.Pixels.Length; i += 4)
            {
                cover += m.Tile.Pixels[i] / 255.0;
            }

            Assert.True(cover / (m.Tile.Width * m.Tile.Height) >= 0.12, $"{id}: a mist band with almost no mist ({cover / (m.Tile.Width * m.Tile.Height):0.000})");
        }

        Assert.True(layers.Stars.Count * 2 >= recipe.Motion.Stars, $"{id}: {layers.Stars.Count} of {recipe.Motion.Stars} stars");
        Assert.Equal(0, layers.Dropped);
        var moons = MoonfallSceneBuilder.MoonDiscs(recipe);
        MoonfallPlane? sky = null;
        if (recipe.Motion.StarWhere.Count > 0)
        {
            // The scene as it stood when the stars were picked: the base layer over the opening (the sky is inside it).
            var px = new MoonfallImage(800, 600);
            var b = layers.Base;
            for (var y = 0; y < b.Height; y++)
            {
                for (var x = 0; x < b.Width; x++)
                {
                    var i = ((y + (int)b.Board.Y) * 800) + x + (int)b.Board.X;
                    var o = ((y * b.Width) + x) * 4;
                    (px.R.Data[i], px.G.Data[i], px.B.Data[i]) = (b.Pixels[o] / 255f, b.Pixels[o + 1] / 255f, b.Pixels[o + 2] / 255f);
                }
            }

            sky = MoonfallSceneBuilder.EvaluateMask(recipe.Motion.StarWhere, layers.Clearance!, px, 1);
        }

        foreach (var star in layers.Stars)
        {
            Assert.All(moons, m => Assert.True(Vector2.Distance(new Vector2(star.X, star.Y), new Vector2(m.X, m.Y)) >= m.Z, $"{id}: a star at ({star.X:0}, {star.Y:0}) on a moon"));
            if (sky is not null)
            {
                Assert.True(sky.Sample(star.X, star.Y) >= MoonfallSceneBuilder.StarMaskLeast - 0.05f, $"{id}: a star at ({star.X:0}, {star.Y:0}) outside its sky");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Every_star_keeps_off_the_framing_at_both_tiers(string id)
    {
        // A sky pixel beside a dark rope or branch is a bright local maximum, so stars lined the framing's edges at 2x
        // (critic runtime round 2, m9): no star within 4 units of the framing's cover (over 0.05), at either tier.
        var (level, recipe) = MoonfallSceneKit.ShippedScenes().First(s => s.Level.Id == id);
        var plates = MoonfallSceneKit.Plates(recipe);
        var coverPlate = recipe.Light.OfType<MoonfallPlate>().FirstOrDefault(static p => p.Cover);
        if (recipe.Motion.Stars == 0 || coverPlate is null)
        {
            return;
        }

        var (painting, fallback) = MoonfallSceneKit.Painting(recipe);
        foreach (var tier in new[] { 1, 2 })
        {
            var layers = tier == 1 ? Scene1x(id).Layers : MoonfallSceneBuilder.Build(recipe, level, painting!, 2, fallback, plates: plates);
            var cover = plates[coverPlate.Picture];
            cover = cover.Width == 800 * tier ? cover : MoonfallFilters.Resize(cover, 800 * tier, 600 * tier);
            Assert.True(layers.Stars.Count * 2 >= recipe.Motion.Stars, $"{id} {tier}x: {layers.Stars.Count} of {recipe.Motion.Stars} stars");
            var reach = (int)MathF.Ceiling((MoonfallSceneBuilder.StarFramingKeep * tier) - 1);
            foreach (var star in layers.Stars)
            {
                int cx = (int)(star.X * tier), cy = (int)(star.Y * tier);
                for (var y = Math.Max(0, cy - reach); y <= Math.Min((600 * tier) - 1, cy + reach); y++)
                {
                    for (var x = Math.Max(0, cx - reach); x <= Math.Min((800 * tier) - 1, cx + reach); x++)
                    {
                        var i = (y * cover.Width) + x;
                        var c = 1 - ((cover.R.Data[i] + cover.G.Data[i] + cover.B.Data[i]) / 3f);
                        Assert.True(c <= 0.06f, $"{id} {tier}x: a star at ({star.X:0}, {star.Y:0}) on the framing's edge ({x / (float)tier:0}, {y / (float)tier:0})");
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void A_glint_shows_only_where_its_light_keeps_clear_of_every_piece(string id)
    {
        var (level, layers) = Scene1x(id);
        var clearance = MoonfallClearance.For(level);
        foreach (var track in layers.Glints)
        {
            var shown = 0;
            for (var t = 0.0; t < 60; t += 0.05)
            {
                var d = MoonfallMotion.GlintAlong(t, track.Length, track.Speed, track.Every, track.Offset, still: false);
                if (d < 0)
                {
                    continue;
                }

                var at = track.At(d);
                if (MoonfallMotion.GlintClear(clearance, at) * MoonfallMotion.GlintFade(d, track.Length) <= 0)
                {
                    continue;
                }

                shown++;
                Assert.True(clearance.At(at.X, at.Y) >= MoonfallMotion.PegKeepOut + MoonfallMotion.GlintReach, $"{id}: a glint at ({at.X:0}, {at.Y:0})");
            }

            Assert.True(shown > 0, $"{id}: a glint that never shows");
            Assert.Equal(-1f, MoonfallMotion.GlintAlong(3, track.Length, track.Speed, track.Every, track.Offset, still: true));
        }
    }

    [Fact]
    public void The_lamps_flicker_at_two_hertz_or_less_and_join_at_their_loop()
    {
        // GD m3 / UX n2: the flicker read as fast jitter at 2 and 3.5 Hz. Two sines at 1.3 and 1.9 Hz: its rate of change
        // stays under 2 pi (0.06 x 1.3 + 0.04 x 1.9) a second, and it joins without a step at its 10 s loop.
        var worst = 0f;
        for (var t = 0.0; t < 20; t += 0.001)
        {
            worst = MathF.Max(worst, MathF.Abs(MoonfallMotion.Flicker(t + 0.001, 0.7f, false) - MoonfallMotion.Flicker(t, 0.7f, false)) / 0.001f);
        }

        Assert.True(worst <= 2 * MathF.PI * ((0.06f * 1.3f) + (0.04f * 1.9f)) + 0.01f, $"flicker rate {worst:0.000}");
        Assert.Equal(MoonfallMotion.Flicker(0, 0.7f, false), MoonfallMotion.Flicker(10, 0.7f, false), 3);
        Assert.Equal(9.5f, MoonfallMotion.BeamLoop);
        Assert.Equal(0.30f, MoonfallSceneBuilder.BeamBreath);
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
    public void Peg_marks_sit_on_every_brick_inside_its_face()
    {
        // A brick takes purple and green as a peg does (and orange where it may): its mark sits once, at its middle,
        // inside its thickness, so the mark (which keeps within 0.9 of its box, the star's rim 1.05) stays on the brick.
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var bricks = 0;
        foreach (var level in campaigns.Base.Levels)
        {
            var game = new MoonfallGame(level, 5, 1);
            for (var i = 0; i < game.PegCount; i++)
            {
                var piece = game.Peg(i);
                var (centre, half) = MoonfallPegMarks.Place(piece);
                if (piece.Shape == PegShape.Round)
                {
                    Assert.Equal(new Vector2((float)piece.X, (float)piece.Y), centre);
                    Assert.Equal((float)piece.Radius, half);
                    continue;
                }

                bricks++;
                Assert.Equal((float)(piece.Thickness / 2), half, 4);
                var clearance = MoonfallClearance.For(new MoonfallLevel("t", "t", [level.Pegs[i]]));
                Assert.True(clearance.At(centre.X, centre.Y) <= -half * 0.9f, $"{level.Id} brick {i}: its mark's centre is not on its middle line");
                foreach (var a in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
                {
                    var r = half * 0.9f;
                    var x = centre.X + (r * MathF.Cos(a * MathF.PI / 180));
                    var y = centre.Y + (r * MathF.Sin(a * MathF.PI / 180));
                    Assert.True(clearance.At(x, y) < 0.75f, $"{level.Id} brick {i}: its mark leaves the brick at ({x:0.0}, {y:0.0})");
                }
            }
        }

        Assert.True(bricks >= 30, $"only {bricks} bricks");

        // At the 640 window (0.735 px a unit at 1x's board) a brick's mark spans at least 10 px, so its shape reads
        // (UX runtime round 2, m1); a round peg's is unchanged.
        var level2 = campaigns.Base.Levels.First(static l => l.Pegs.Any(static p => p.Shape != PegShape.Round));
        var g = new MoonfallGame(level2, 5, 1);
        for (var i = 0; i < g.PegCount; i++)
        {
            var piece = g.Peg(i);
            var (_, small) = MoonfallPegMarks.Place(piece, SmallWindowScale);
            Assert.True(small * 2 * SmallWindowScale >= (piece.Shape == PegShape.Round ? 2 * piece.Radius * SmallWindowScale - 0.01 : 10), $"{level2.Id} piece {i}: a {small * 2 * SmallWindowScale:0.0} px mark");
            if (piece.Shape == PegShape.Round)
            {
                Assert.Equal((float)piece.Radius, small);
            }
        }
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
        var level = LevelTaking("horizon-by-night");
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
        var level = LevelTaking("horizon-by-night");
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
            var level = LevelTaking("rhotano-wonders");
            Until(art, host, level, () => art.SceneState is MoonfallSceneState.Ready or MoonfallSceneState.Failed);
            Assert.Equal(MoonfallSceneState.Ready, art.SceneState);
            Assert.Equal("rhotano-wonders", art.Scene!.Layers.Name);
        }

        // Neither the painting nor any picture: the scene fails (logged once) and the board shows the night sky.
        var bare = new FakeHost(AllUiTextures(), pictures: false);
        using var art2 = new MoonfallGameArt<Tex>(bare, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level2 = LevelTaking("rhotano-wonders");
        Until(art2, bare, level2, () => art2.SceneState is MoonfallSceneState.Failed);
        for (var i = 0; i < 10; i++)
        {
            bare.Frame++;
            art2.Frame(level2, false, false);
        }

        Assert.Null(art2.Scene);
        Assert.Single(bare.Warnings, static w => w.Contains("scene rhotano-wonders", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_card_shows_the_companion_as_a_plain_ring_and_is_logged_once()
    {
        var host = new FakeHost(AllUiTextures(), pictures: true);
        using var art = new MoonfallGameArt<Tex>(host, MoonfallSceneRecipeLoader.LoadBuiltIn());
        var level = LevelTaking("horizon-by-night");
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
        var level = LevelTaking("horizon-by-night");
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
            var layers = MoonfallSceneBuilder.Build(recipe, level, painting!, 2, fallback, plates: MoonfallSceneKit.Plates(recipe));
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
