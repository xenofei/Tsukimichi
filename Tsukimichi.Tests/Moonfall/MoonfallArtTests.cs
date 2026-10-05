using System.Numerics;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Moonfall's art pipeline (feature plan v9 G8): the shipped atlas and its manifest, the checks that refuse a broken
/// set, the fallback rules (primitives, the 1x sheet, the sky, the flat ground), the pegs' contrast on the board, the
/// texture budget per level, the board art's geometry and the plugin's packaging of the files.
/// </summary>
public sealed class MoonfallArtTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "tsukimichi-moonfall-art-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    internal static string ArtFolder() => Path.Combine(Ui.OrnamentLayoutTests.RepoRoot(), "Tsukimichi", "assets", "moonfall");

    private static MoonfallAtlas Shipped()
    {
        var load = MoonfallArtFiles.Load(ArtFolder());
        Assert.True(load.Ok, string.Join("\n", load.Errors));
        return load.Atlas!;
    }

    private static string ShippedJson() => File.ReadAllText(Path.Combine(ArtFolder(), MoonfallArtFiles.ManifestName));

    // ---- The manifest ----

    [Fact]
    public void The_shipped_set_loads_with_its_pictures_at_the_sizes_it_names()
    {
        var atlas = Shipped();
        Assert.True(atlas.PegVariants >= 1);
        Assert.Equal("atlas.png", atlas.OneXFile);
        Assert.Equal("atlas@2x.png", atlas.TwoXFile);
        Assert.True(MoonfallArtFiles.TryPngSize(Path.Combine(ArtFolder(), atlas.TwoXFile), out var w, out var h));
        Assert.Equal((atlas.Width * 2, atlas.Height * 2), (w, h));
    }

    [Fact]
    public void Every_sprite_the_drawing_code_names_is_in_the_shipped_atlas()
    {
        var atlas = Shipped();

        // The drawing code names fixed sprites only through the enum, and the enum and its names line up one to one.
        Assert.Equal(Enum.GetValues<MoonfallSprite>().Length, MoonfallSprites.Names.Length);
        Assert.Equal(MoonfallSprites.Names.Length, MoonfallSprites.Names.Distinct(StringComparer.Ordinal).Count());
        foreach (var sprite in Enum.GetValues<MoonfallSprite>())
        {
            Assert.True(atlas.TryGet(MoonfallSprites.Name(sprite), out var rect), MoonfallSprites.Name(sprite));
            Assert.Equal(rect, atlas[sprite]);
        }

        // Every peg variant and brick of every kind and state, as the board asks for them.
        foreach (var name in MoonfallSprites.Required(atlas.PegVariants))
        {
            Assert.True(atlas.TryGet(name, out _), name);
        }

        foreach (var colour in Enum.GetValues<PegColour>())
        {
            for (var v = 0; v < atlas.PegVariants * 2; v++)
            {
                Assert.True(atlas.TryGet(MoonfallSprites.Peg(colour, v % atlas.PegVariants, v % 2 == 1), out var named));
                Assert.Equal(named, atlas.Peg(colour, v % atlas.PegVariants, v % 2 == 1));
            }
        }

        // The drawing code takes a sprite's name from MoonfallSprites, never from a literal of its own.
        var art = File.ReadAllText(Path.Combine(Ui.OrnamentLayoutTests.RepoRoot(), "Tsukimichi", "Ui", "MoonfallWindow.Art.cs"));
        Assert.DoesNotContain("\"peg.", art, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGet(", art, StringComparison.Ordinal);
    }

    [Fact]
    public void Shipped_sprites_do_not_overlap_and_keep_a_margin_inside_the_sheet()
    {
        var atlas = Shipped();
        var names = MoonfallSprites.Required(atlas.PegVariants).ToList();
        var rects = names.Select(n => { atlas.TryGet(n, out var r); return (n, r); }).ToList();
        foreach (var (name, r) in rects)
        {
            Assert.True(r.X >= 1 && r.Y >= 1 && r.X + r.W <= atlas.Width - 1 && r.Y + r.H <= atlas.Height - 1, name);
        }

        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var (a, b) = (rects[i].r, rects[j].r);
                var apart = a.X + a.W <= b.X || b.X + b.W <= a.X || a.Y + a.H <= b.Y || b.Y + b.H <= a.Y;
                Assert.True(apart, $"{rects[i].n} overlaps {rects[j].n}");
            }
        }
    }

    [Fact]
    public void The_manifest_carries_the_art_choices_the_code_reads()
    {
        var atlas = Shipped();
        Assert.True(atlas.GaugeTo > atlas.GaugeFrom);
        Assert.True(atlas.OuterSlice > 0 && atlas.InnerSlice > 0);

        // Owner decision 7 is per campaign, in the files: today both campaigns draw the lantern boat (the owner turned
        // down the crescent cradle, style frame A); the cradle stays one manifest edit away.
        Assert.Equal(MoonfallBucketStyle.Boat, atlas.BucketFor(MoonfallCampaignKind.Base));
        Assert.Equal(MoonfallBucketStyle.Boat, atlas.BucketFor(MoonfallCampaignKind.Expansion));
        var cradle = Manifest(m => m["buckets"]!["base"] = "cradle");
        Assert.Equal(MoonfallBucketStyle.Cradle, MoonfallAtlas.Parse(cradle).Atlas!.BucketFor(MoonfallCampaignKind.Base));
    }

    /// <summary>The shipped manifest with <paramref name="edit"/> applied.</summary>
    private static string Manifest(Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(ShippedJson())!;
        edit(node);
        return node.ToJsonString();
    }

    [Theory]
    [InlineData("version", "2", "newer Moonfall")]
    [InlineData("format", "\"other\"", "format must be")]
    [InlineData("width", "0", "width and height")]
    [InlineData("pegVariants", "0", "pegVariants must be")]
    [InlineData("buckets", "{ \"base\": \"tub\", \"expansion\": \"boat\" }", "buckets.base")]
    [InlineData("gauge", "{ \"from\": 300, \"to\": 200 }", "gauge must")]
    [InlineData("points", "{}", "points.boat.lantern")]
    [InlineData("slices", "{ \"frame.bead.outer\": 30, \"frame.bead.inner\": 8 }", "slices.frame.bead.outer")]
    [InlineData("files", "{ \"1x\": \"../atlas.png\", \"2x\": \"atlas@2x.png\", \"sky\": \"sky.png\" }", "files.1x")]
    [InlineData("sky", "{ \"x\": 0, \"y\": 0, \"w\": 0.5, \"h\": 10 }", "sky must")]
    public void A_broken_manifest_is_refused_with_its_reason(string key, string value, string reason)
    {
        var load = MoonfallAtlas.Parse(Manifest(m => m[key] = JsonNode.Parse(value)));
        Assert.False(load.Ok);
        Assert.Contains(load.Errors, e => e.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_or_misplaced_sprite_or_ink_is_refused()
    {
        var missing = MoonfallAtlas.Parse(Manifest(m => m["sprites"]!.AsObject().Remove("launcher.tube")));
        Assert.Contains("sprite launcher.tube is missing", missing.Errors);

        var peg = MoonfallAtlas.Parse(Manifest(m => m["sprites"]!.AsObject().Remove("peg.purple.0.lit")));
        Assert.Contains("sprite peg.purple.0.lit is missing", peg.Errors);

        var outside = MoonfallAtlas.Parse(Manifest(m => m["sprites"]!["ball"]!["x"] = 5000));
        Assert.Contains(outside.Errors, e => e.StartsWith("sprite ball:", StringComparison.Ordinal));

        var ink = MoonfallAtlas.Parse(Manifest(m => m["inks"]!["glow.orange"] = "orange"));
        Assert.Contains("inks.glow.orange must be a #RRGGBB colour", ink.Errors);

        Assert.False(MoonfallAtlas.Parse("").Ok);
        Assert.False(MoonfallAtlas.Parse("{ nope").Ok);
        Assert.False(MoonfallAtlas.Parse("[]").Ok);
    }

    // ---- The files on disk, and the fallback ----

    /// <summary>A copy of the shipped set in a folder of its own, to break.</summary>
    private string CopyOfShipped()
    {
        Directory.CreateDirectory(temp);
        foreach (var file in Directory.GetFiles(ArtFolder()))
        {
            File.Copy(file, Path.Combine(temp, Path.GetFileName(file)));
        }

        return temp;
    }

    [Fact]
    public void A_missing_or_broken_file_refuses_the_whole_set_so_the_board_keeps_its_primitives()
    {
        Assert.Contains("atlas.json is missing", MoonfallArtFiles.Load(Path.Combine(temp, "nowhere")).Errors);

        var folder = CopyOfShipped();
        Assert.True(MoonfallArtFiles.Load(folder).Ok);

        File.Delete(Path.Combine(folder, "sky.png"));
        Assert.Contains("sky.png is missing or not a PNG", MoonfallArtFiles.Load(folder).Errors);

        // A truncated sheet, a sheet of the wrong size, a manifest that is not JSON.
        File.WriteAllBytes(Path.Combine(folder, "sky.png"), File.ReadAllBytes(Path.Combine(ArtFolder(), "sky.png")));
        File.WriteAllBytes(Path.Combine(folder, "atlas.png"), File.ReadAllBytes(Path.Combine(ArtFolder(), "atlas.png"))[..20]);
        Assert.Contains("atlas.png is missing or not a PNG", MoonfallArtFiles.Load(folder).Errors);

        File.WriteAllBytes(Path.Combine(folder, "atlas.png"), File.ReadAllBytes(Path.Combine(ArtFolder(), "atlas@2x.png")));
        Assert.Contains(MoonfallArtFiles.Load(folder).Errors, e => e.StartsWith("atlas.png is 2048", StringComparison.Ordinal));

        File.WriteAllText(Path.Combine(folder, "atlas.json"), "{ not json");
        var load = MoonfallArtFiles.Load(folder);
        Assert.False(load.Ok);
        Assert.Contains(load.Errors, e => e.StartsWith("not JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void A_scene_picture_of_the_wrong_size_or_missing_is_not_usable()
    {
        Directory.CreateDirectory(temp);
        var path = Path.Combine(temp, "scene.png");
        Assert.False(MoonfallArtFiles.SceneUsable(path, false, out var missing));
        Assert.Contains("missing", missing, StringComparison.Ordinal);

        File.WriteAllBytes(path, PackPng.EncodeRgba(new byte[16 * 12 * 4], 16, 12));
        Assert.False(MoonfallArtFiles.SceneUsable(path, false, out var small));
        Assert.Contains("a scene is 800 x 600", small, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(temp, "scenes", "night@2x.png"), MoonfallArtFiles.ScenePath(temp, "night", twoX: true));
    }

    [Fact]
    public void Without_a_manifest_or_a_sheet_the_board_draws_its_primitives()
    {
        var slots = new MoonfallArtSlots<string>();
        Assert.Null(slots.Sheet(false, out _));
        Assert.False(slots.ShouldLoad(MoonfallArtSlot.Sheet1x));

        // A refused manifest: nothing is ever asked for.
        slots.SetManifest(null);
        Assert.True(slots.ManifestFailed);
        Assert.False(slots.ShouldLoad(MoonfallArtSlot.Sheet1x));
        Assert.Null(slots.Sheet(true, out _));

        // A manifest, but the 1x sheet fails to decode: still the primitives.
        slots.SetManifest(Shipped());
        Assert.True(slots.ShouldLoad(MoonfallArtSlot.Sheet1x));
        slots.Begin(MoonfallArtSlot.Sheet1x);
        Assert.Null(slots.Sheet(false, out _));
        slots.Fail(MoonfallArtSlot.Sheet1x);
        Assert.False(slots.ShouldLoad(MoonfallArtSlot.Sheet1x));
        Assert.Null(slots.Sheet(false, out _));
    }

    [Fact]
    public void The_1x_sheet_stands_in_while_the_2x_loads_and_the_2x_is_used_once_ready()
    {
        var slots = new MoonfallArtSlots<string>();
        slots.SetManifest(Shipped());
        slots.Begin(MoonfallArtSlot.Sheet1x);
        Assert.Null(slots.Land(MoonfallArtSlot.Sheet1x, "one"));
        slots.Begin(MoonfallArtSlot.Sheet2x);
        Assert.Equal("one", slots.Sheet(true, out var twoX));
        Assert.False(twoX);

        Assert.Null(slots.Land(MoonfallArtSlot.Sheet2x, "two"));
        Assert.Equal("two", slots.Sheet(true, out twoX));
        Assert.True(twoX);
        Assert.Equal("one", slots.Sheet(false, out twoX));
        Assert.False(twoX);

        // Closing lets everything go, for disposal; a load that lands afterwards comes straight back for disposal too.
        var released = new List<string>();
        slots.ReleaseAll(released);
        Assert.Equal(new[] { "one", "two" }, released);
        Assert.Null(slots.Sheet(false, out _));
        Assert.Equal("late", slots.Land(MoonfallArtSlot.Sheet1x, "late"));
    }

    [Fact]
    public void The_ground_is_the_scene_then_the_sky_then_flat()
    {
        var slots = new MoonfallArtSlots<string>();
        slots.SetManifest(Shipped());
        var released = new List<string>();

        // A level without a scene: the sky, once loaded; flat until then.
        slots.WantScene(null, released);
        Assert.True(slots.NeedsSky);
        Assert.False(slots.ShouldLoad(MoonfallArtSlot.Scene1x));
        Assert.Null(slots.Ground(false, out var ground));
        Assert.Equal(MoonfallGround.Flat, ground);
        slots.Begin(MoonfallArtSlot.Sky);
        slots.Land(MoonfallArtSlot.Sky, "sky");
        Assert.Equal("sky", slots.Ground(false, out ground));
        Assert.Equal(MoonfallGround.Sky, ground);

        // A level with a scene: no sky needed while it loads; once in, the scene; the other tier stands in.
        slots.WantScene("moon-road-night", released);
        Assert.False(slots.NeedsSky);
        Assert.True(slots.ShouldLoad(MoonfallArtSlot.Scene1x));
        slots.Begin(MoonfallArtSlot.Scene1x);
        slots.Land(MoonfallArtSlot.Scene1x, "scene");
        Assert.Equal("scene", slots.Ground(false, out ground));
        Assert.Equal(MoonfallGround.Scene, ground);
        Assert.Equal("scene", slots.Ground(true, out ground));

        // The next level's scene is missing: the old one's picture goes, and the sky is needed again.
        slots.WantScene("missing-scene", released);
        Assert.Equal(new[] { "scene" }, released);
        slots.Fail(MoonfallArtSlot.Scene1x);
        Assert.True(slots.NeedsSky);
        Assert.Equal("sky", slots.Ground(false, out ground));
        Assert.Equal(MoonfallGround.Sky, ground);
    }

    // ---- Contrast ----

    /// <summary>The mean colour of a sprite's opaque pixels in the 1x sheet.</summary>
    private static Vector4 Mean(byte[] rgba, int width, in MoonfallSpriteRect r)
    {
        var sum = Vector4.Zero;
        var n = 0;
        for (var y = (int)r.Y; y < (int)(r.Y + r.H); y++)
        {
            for (var x = (int)r.X; x < (int)(r.X + r.W); x++)
            {
                var i = ((y * width) + x) * 4;
                if (rgba[i + 3] >= 242)
                {
                    sum += new Vector4(rgba[i], rgba[i + 1], rgba[i + 2], 255) / 255f;
                    n++;
                }
            }
        }

        Assert.True(n > 0);
        return sum / n;
    }

    [Fact]
    public void Every_peg_reads_at_3_to_1_on_the_board_on_every_palette()
    {
        var atlas = Shipped();
        Assert.True(PackPng.TryDecode(File.ReadAllBytes(Path.Combine(ArtFolder(), atlas.OneXFile)), out var width, out _, out var rgba));

        // The board is the art set's own night scene on every palette (spec-moonfall.md: Menphina's Medallion only), so
        // a peg's ground is the set's ground ink whatever the palette, light ones such as Ishgard Snow included.
        foreach (var palette in UiPalettes.All)
        {
            Assert.NotNull(palette);
            foreach (var colour in Enum.GetValues<PegColour>())
            {
                for (var v = 0; v < atlas.PegVariants; v++)
                {
                    foreach (var lit in new[] { false, true })
                    {
                        var face = Mean(rgba, width, atlas.Peg(colour, v, lit));
                        Assert.True(ColorMath.Contrast(face, atlas.Ground) >= PegInks.MinContrast, $"{palette.Id}: {MoonfallSprites.Peg(colour, v, lit)} {ColorMath.Contrast(face, atlas.Ground):0.00}");
                    }

                    // Lit is brighter than unlit, so the state reads without the halo.
                    Assert.True(ColorMath.Luminance(Mean(rgba, width, atlas.Peg(colour, v, true))) > ColorMath.Luminance(Mean(rgba, width, atlas.Peg(colour, v, false))));
                }

                // Plain's flat inks, and its lit ring.
                Assert.True(ColorMath.Contrast(atlas.Flat(colour), atlas.Ground) >= PegInks.MinContrast, $"flat {colour}");
                Assert.True(ColorMath.Contrast(atlas.Glow(colour), atlas.Ground) >= PegInks.MinContrast, $"glow {colour}");
                var brick = Mean(rgba, width, atlas.Brick(colour, lit: false));
                Assert.True(ColorMath.Contrast(brick, atlas.Ground) >= PegInks.MinContrast, $"brick {colour}");
            }

            Assert.True(ColorMath.Contrast(Mean(rgba, width, atlas[MoonfallSprite.Ball]), atlas.Ground) >= PegInks.MinContrast, "ball");
        }
    }

    // ---- The budget ----

    [Fact]
    public void Each_level_holds_a_few_megabytes_while_it_is_drawn()
    {
        var atlas = Shipped();
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        foreach (var level in campaigns.Base.Levels.Concat(campaigns.Expansion.Levels))
        {
            var oneX = MoonfallArtFiles.Budget(atlas, twoX: false, level.Scene is not null);
            var twoX = MoonfallArtFiles.Budget(atlas, twoX: true, level.Scene is not null);
            Assert.Equal((long)atlas.Width * atlas.Height * 4, oneX.Sheet1x);
            Assert.Equal(0, oneX.Sheet2x);
            Assert.Equal(oneX.Sheet1x * 4, twoX.Sheet2x);
            Assert.Equal(level.Scene is null ? atlas.SkyBytes : 800 * 600 * 4, oneX.Ground);
            Assert.Equal(level.Scene is null ? atlas.SkyBytes : 1600 * 1200 * 4, twoX.Ground);

            // A level's board at the 2x tier (its settled worst case) stays under the 12 MiB the rest of the art keeps to.
            Assert.True(twoX.Total < Budget, $"{level.Id}: {twoX.Total:N0} bytes");
        }
    }

    /// <summary>The budget the rest of Tsukimichi's art keeps to (What's new, the theme atlases).</summary>
    private const long Budget = 12L * 1024 * 1024;

    /// <summary>One frame of <c>MoonfallArtTextures.Frame</c>'s rules: land what decoded, drop the unused scene tier, start what is wanted.</summary>
    private static void Frame(MoonfallArtSlots<string> slots, bool twoX, List<string> released, params MoonfallArtSlot[] landing)
    {
        foreach (var slot in landing)
        {
            Assert.Null(slots.Land(slot, slot.ToString()));
        }

        if (slots.SceneToDrop(twoX, out var unused) && slots.Clear(unused) is { } gone)
        {
            released.Add(gone);
        }

        foreach (var slot in new[] { MoonfallArtSlot.Sheet1x, twoX ? MoonfallArtSlot.Sheet2x : MoonfallArtSlot.Sheet1x, twoX ? MoonfallArtSlot.Scene2x : MoonfallArtSlot.Scene1x })
        {
            if (slots.ShouldLoad(slot))
            {
                slots.Begin(slot);
            }
        }

        if (slots.NeedsSky && slots.ShouldLoad(MoonfallArtSlot.Sky))
        {
            slots.Begin(MoonfallArtSlot.Sky);
        }
    }

    [Fact]
    public void A_scene_level_stays_under_12_MiB_through_tier_changes_both_ways()
    {
        var atlas = Shipped();
        var slots = new MoonfallArtSlots<string>();
        slots.SetManifest(atlas);
        var released = new List<string>();
        slots.WantScene("moon-road-night", released);
        var settled = new List<long>();

        // Opened at 1x: the 1x sheet and the 1x scene, and no sky.
        Frame(slots, false, released);
        Frame(slots, false, released, MoonfallArtSlot.Sheet1x, MoonfallArtSlot.Scene1x);
        Assert.Equal(MoonfallSlotState.Idle, slots.State(MoonfallArtSlot.Sky));
        settled.Add(slots.HeldBytes(atlas));

        // The Full Moon zoom (or a larger window): the 2x tier loads while the 1x scene still draws.
        Frame(slots, true, released);
        Assert.Equal("Scene1x", slots.Ground(true, out _));
        Frame(slots, true, released, MoonfallArtSlot.Sheet2x, MoonfallArtSlot.Scene2x);
        Assert.Equal(new[] { "Scene1x" }, released);
        Assert.Equal(MoonfallSlotState.Idle, slots.State(MoonfallArtSlot.Scene1x));
        Assert.Equal("Scene2x", slots.Ground(true, out _));
        settled.Add(slots.HeldBytes(atlas));

        // Back out of the zoom (or a smaller window): the 1x scene comes back, and once it lands the 2x one goes.
        Frame(slots, false, released);
        Assert.Equal("Scene2x", slots.Ground(false, out _));
        Frame(slots, false, released, MoonfallArtSlot.Scene1x);
        Assert.Equal(new[] { "Scene1x", "Scene2x" }, released);
        settled.Add(slots.HeldBytes(atlas));

        // And in again: still one scene picture at a time.
        Frame(slots, true, released);
        Frame(slots, true, released, MoonfallArtSlot.Scene2x);
        settled.Add(slots.HeldBytes(atlas));

        Assert.All(settled, held => Assert.True(held < Budget, $"{held:N0} bytes"));
        Assert.Equal(MoonfallArtFiles.Budget(atlas, twoX: true, hasScene: true).Total, settled.Max());

        // A scene tier that fails keeps the other: nothing is dropped for a picture that never came.
        var failing = new MoonfallArtSlots<string>();
        failing.SetManifest(atlas);
        failing.WantScene("moon-road-night", released);
        Frame(failing, false, released);
        Frame(failing, false, released, MoonfallArtSlot.Sheet1x, MoonfallArtSlot.Scene1x);
        Frame(failing, true, released);
        failing.Fail(MoonfallArtSlot.Scene2x);
        Assert.False(failing.SceneToDrop(true, out _));
        Assert.Equal("Scene1x", failing.Ground(true, out _));
    }

    // ---- Geometry ----

    [Fact]
    public void The_gauge_fills_to_each_notch_at_its_threshold()
    {
        ReadOnlySpan<int> t = MoonfallRules.FreeBallThresholds;
        Assert.Equal(0f, MoonfallArtMath.GaugeShare(0, t));
        Assert.Equal(0.2f, MoonfallArtMath.GaugeShare(25_000, t), 4);
        Assert.Equal(0.1f, MoonfallArtMath.GaugeShare(12_500, t), 4);
        Assert.Equal(0.4f, MoonfallArtMath.GaugeShare(50_000, t), 4);
        Assert.Equal(0.6f, MoonfallArtMath.GaugeShare(75_000, t), 4);
        Assert.Equal(1f, MoonfallArtMath.GaugeShare(125_000, t), 4);
        Assert.Equal(1f, MoonfallArtMath.GaugeShare(900_000, t));
        Assert.Equal(0.2f, MoonfallArtMath.Notch(25_000, t), 4);
        Assert.Equal(0.6f, MoonfallArtMath.Notch(75_000, t), 4);
        Assert.Equal(1f, MoonfallArtMath.Notch(125_000, t), 4);
    }

    [Fact]
    public void A_ray_is_cut_to_the_box_it_crosses()
    {
        var (near, far) = MoonfallArtMath.RayInBox(new Vector2(0, 10), new Vector2(0, -1), new Vector2(-5, -5), new Vector2(5, 5));
        Assert.Equal(5f, near, 4);
        Assert.Equal(15f, far, 4);
        Assert.Equal((0f, 0f), MoonfallArtMath.RayInBox(new Vector2(0, 10), new Vector2(1, 0), new Vector2(-5, -5), new Vector2(5, 5)));
        var inside = MoonfallArtMath.RayInBox(Vector2.Zero, new Vector2(1, 0), new Vector2(-5, -5), new Vector2(5, 5));
        Assert.Equal((0f, 5f), inside);
    }

    [Theory]
    [InlineData(30f, 1)]
    [InlineData(48f, 2)]
    [InlineData(84f, 4)]
    public void A_brick_repeats_its_middle_mirrored_and_ends_on_the_matching_cap(float total, int repeats)
    {
        // The interim brick: 30 x 12, caps of 6; drawn 12 thick, so its middle is 18 long.
        var columns = new List<(float S, float U)>();
        MoonfallArtMath.BrickColumns(total, 30, 12, 12, float.MaxValue, columns);
        Assert.Equal((0f, 0f), columns[0]);
        Assert.Equal((6f, 6f), columns[1]);
        Assert.Equal(repeats + 3, columns.Count);
        Assert.Equal(total, columns[^1].S, 4);
        Assert.Equal(repeats % 2 == 1 ? 30f : 0f, columns[^1].U);

        // Each repeat ends where the next begins (no seam): the u alternates between the middle's two ends.
        for (var j = 0; j < repeats; j++)
        {
            Assert.Equal(j % 2 == 0 ? 24f : 6f, columns[2 + j].U, 4);
        }

        // A brick shorter than its caps is the start cap there and back.
        MoonfallArtMath.BrickColumns(8, 30, 12, 12, float.MaxValue, columns);
        Assert.Equal(new List<(float S, float U)> { (0f, 0f), (4f, 6f), (8f, 0f) }, columns);
    }

    // ---- Packaging ----

    [Fact]
    public void The_plugin_packages_the_art_folder()
    {
        var csproj = File.ReadAllText(Path.Combine(Ui.OrnamentLayoutTests.RepoRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        Assert.Contains("""<Content Include="assets\moonfall\*.png;assets\moonfall\atlas.json;assets\moonfall\scenes\*.png" CopyToOutputDirectory="PreserveNewest" />""", csproj, StringComparison.Ordinal);

        // What the build put beside the plugin (the gates build first; a run that did not build the plugin checks the
        // project file alone): every file of the set, byte for byte, where the runtime looks for it.
        if (Diagnostics.NoNetworkTests.PluginAssembly() is not { } plugin)
        {
            return;
        }

        var shipped = MoonfallArtFiles.Folder(Path.GetDirectoryName(plugin)!);
        var expected = Directory.GetFiles(ArtFolder(), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(ArtFolder(), f))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains("atlas.json", expected);
        Assert.Contains(Path.Combine("scenes", "moon-road-night@2x.png"), expected);
        Assert.Equal(expected, Directory.GetFiles(shipped, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(shipped, f)).Order(StringComparer.Ordinal).ToArray());
        foreach (var file in expected)
        {
            Assert.True(File.ReadAllBytes(Path.Combine(ArtFolder(), file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(shipped, file))), $"{file} differs in the build output");
        }

        Assert.True(MoonfallArtFiles.Load(shipped).Ok);
    }
}
