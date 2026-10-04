using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The atlas runtime's pure half (feature plan v7 T5; theme-system §6.3, §7.2 "ThemeAtlasLayoutTests";
/// docs/design/v7/themes/ATLAS-CONTRACT.md): layout parsing and checks, the tier choice, the residency rules (what loads,
/// what is kept, what is let go), and every shipped theme set folder against the contract and its budgets.
/// </summary>
public sealed class ThemeAtlasRuntimeTests
{
    private static string ThemesDir() => Path.Combine(OrnamentLayoutTests.AssetsDir(), "themes");

    // ------------------------------------------------------------------ the contract is Medallion's schema

    [Fact]
    public void Medallions_own_layout_parses_as_a_theme_hero_atlas_and_matches_the_compiled_table()
    {
        var text = File.ReadAllText(Path.Combine(OrnamentLayoutTests.AssetsDir(), "medals.json"));
        Assert.True(HeroAtlasLayout.TryParse(text, out var layout, out var error), error);
        Assert.Equal(MedalLayout.Width, layout!.Width);
        Assert.Equal(MedalLayout.Height, layout.Height);
        Assert.Equal(MedalLayout.Tiers, layout.Tiers);
        foreach (var sprite in Enum.GetValues<MedalSprite>())
        {
            foreach (var tier in MedalLayout.Tiers)
            {
                Assert.Equal(MedalLayout.Rect(sprite, tier), layout.Rect(sprite, tier));
                Assert.Equal(MedalLayout.Uv(MedalLayout.Rect(sprite, tier)), layout.Uv(layout.Rect(sprite, tier)));
            }
        }

        Assert.Equal(default, layout.Rect(MedalSprite.Ready, 50));
        Assert.Equal(782L * 574 * 4, layout.Bytes1x);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(32f)]
    [InlineData(48f)]
    [InlineData(49f)]
    [InlineData(72f)]
    [InlineData(96f)]
    [InlineData(97f)]
    [InlineData(128f)]
    [InlineData(129f)]
    [InlineData(192f)]
    [InlineData(256f)]
    [InlineData(400f)]
    public void The_generalised_tier_pick_is_medallions(float size)
    {
        Assert.Equal(MedalLayout.Pick(size), ThemeAtlasRules.PickTier(MedalLayout.Tiers, size));
    }

    [Fact]
    public void A_tier_is_never_shrunk_more_than_one_and_a_half_times_from_32_px()
    {
        for (var size = 32f; size <= 256f; size += 1f)
        {
            var (tier, twoX) = ThemeAtlasRules.PickTier(MedalLayout.Tiers, size);
            var cell = twoX ? tier * 2 : tier;
            Assert.True(cell >= size && cell <= size * 1.5f + 0.001f, $"{size} px draws from a {cell} px cell");
        }
    }

    // ------------------------------------------------------------------ hero layout checks

    [Fact]
    public void A_hero_layout_missing_a_sprite_or_with_a_bad_cell_is_refused()
    {
        var good = HeroJson();
        Assert.True(HeroAtlasLayout.TryParse(good.ToJsonString(), out _, out var error), error);

        var missing = HeroJson();
        missing["sprites"]!.AsObject().Remove("other-job-hand");
        Assert.False(HeroAtlasLayout.TryParse(missing.ToJsonString(), out _, out error));
        Assert.Contains("other-job-hand", error);

        var notSquare = HeroJson();
        notSquare["sprites"]!["ready"]!["48"] = new JsonArray(2, 2, 48, 47);
        Assert.False(HeroAtlasLayout.TryParse(notSquare.ToJsonString(), out _, out _));

        var touching = HeroJson();
        touching["sprites"]!["in-journal"]!["48"] = new JsonArray(51, 2, 48, 48);
        Assert.False(HeroAtlasLayout.TryParse(touching.ToJsonString(), out _, out error));
        Assert.Contains("closer than", error);

        var outside = HeroJson();
        outside["size"] = new JsonArray(100, 100);
        Assert.False(HeroAtlasLayout.TryParse(outside.ToJsonString(), out _, out _));

        var descending = HeroJson();
        descending["tiers"] = new JsonArray(64, 48);
        Assert.False(HeroAtlasLayout.TryParse(descending.ToJsonString(), out _, out _));

        Assert.False(HeroAtlasLayout.TryParse("{ not json", out _, out _));
        Assert.False(HeroAtlasLayout.TryParse("[]", out _, out _));
    }

    // ------------------------------------------------------------------ row strips

    [Fact]
    public void A_row_strip_parses_and_gives_the_exact_whole_pixel_cell()
    {
        Assert.True(RowStripLayout.TryParse(RowJson("full", "plain").ToJsonString(), out var row, out var error), error);
        Assert.Equal(12, row!.MinSize);
        Assert.Equal(31, row.MaxSize);
        Assert.True(row.Has(RowFinish.Full) && row.Has(RowFinish.Plain) && !row.Has(RowFinish.Quiet));

        foreach (var state in AppearanceStates.All)
        {
            for (var px = 12; px <= 31; px++)
            {
                Assert.True(row.TryRect(state, RowFinish.Full, px, out var rect));
                Assert.Equal((px, px), (rect.Width, rect.Height));
            }
        }

        Assert.True(row.TryRect(QuestState.Ready, RowFinish.Full, 6, out var small));
        Assert.Equal(12, small.Width);
        Assert.True(row.TryRect(QuestState.Ready, RowFinish.Full, 40, out var big));
        Assert.Equal(31, big.Width);
        Assert.False(row.TryRect(QuestState.Ready, RowFinish.Quiet, 20, out _));

        Assert.True(row.TryRect(QuestState.Blocked, RowFinish.Plain, 16, out var plain));
        Assert.True(row.TryRect(QuestState.Blocked, RowFinish.Full, 16, out var full));
        Assert.NotEqual(full, plain);
    }

    [Fact]
    public void A_row_strip_without_the_full_finish_or_with_gaps_in_its_sizes_is_refused()
    {
        Assert.False(RowStripLayout.TryParse(RowJson("plain").ToJsonString(), out _, out var error));
        Assert.Contains("full", error);

        var gappy = RowJson("full");
        gappy["sizes"] = new JsonArray(12, 13, 15);
        Assert.False(RowStripLayout.TryParse(gappy.ToJsonString(), out _, out _));

        var missing = RowJson("full");
        missing["sprites"]!["full"]!.AsObject().Remove("locked-out");
        Assert.False(RowStripLayout.TryParse(missing.ToJsonString(), out _, out error));
        Assert.Contains("locked-out", error);
    }

    [Fact]
    public void A_cell_far_outside_the_image_is_refused_however_large_its_position()
    {
        // x + width past int.MaxValue wrapped to a negative right edge and passed the bounds check in int arithmetic.
        foreach (var (index, value) in new[] { (0, int.MaxValue - 5), (1, int.MaxValue - 5), (0, int.MaxValue) })
        {
            var huge = RowJson("full");
            var cell = huge["sprites"]!["full"]!["ready"]!["12"]!.AsArray();
            cell[index] = value;
            Assert.False(RowStripLayout.TryParse(huge.ToJsonString(), out _, out var error));
            Assert.Contains("not inside the image", error);
        }
    }

    [Fact]
    public void Per_frame_registry_and_residency_checks_allocate_nothing()
    {
        var residency = new AtlasResidency();
        var appearance = Mix("ishgard-glass");
        residency.Retain(appearance);
        _ = UiPalettes.IsRegistered(PaletteId.IshgardSnow);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            residency.Retain(appearance);
            _ = UiPalettes.IsRegistered(PaletteId.IshgardSnow);
            _ = UiPalettes.IsRegistered(PaletteId.Dawn);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(residency.IsWanted(GlyphSetId.IshgardGlass));
    }

    // ------------------------------------------------------------------ tier choice

    [Fact]
    public void Hero_sizes_draw_from_the_hero_atlas_and_rows_from_the_strip()
    {
        var (medals, plain, row) = Layouts();

        var hero = ThemeAtlasRules.Pick(48f, MedalFinish.Gilt, medals, plain, row);
        Assert.Equal(new AtlasPick(AtlasSource.Medals, 48, false, RowFinish.Full), hero);

        Assert.Equal(new AtlasPick(AtlasSource.Medals, 96, true, RowFinish.Full), ThemeAtlasRules.Pick(190f, MedalFinish.Gilt, medals, plain, row));
        Assert.Equal(new AtlasPick(AtlasSource.Medals, 48, false, RowFinish.Full), ThemeAtlasRules.Pick(32f, MedalFinish.LightRim, medals, plain, row));
        Assert.Equal(new AtlasPick(AtlasSource.Plain, 64, false, RowFinish.Full), ThemeAtlasRules.Pick(60f, MedalFinish.Plain, medals, plain, row));

        Assert.Equal(new AtlasPick(AtlasSource.Row, 18, false, RowFinish.Full), ThemeAtlasRules.Pick(18f, MedalFinish.Gilt, medals, plain, row));
        Assert.Equal(new AtlasPick(AtlasSource.Row, 31, false, RowFinish.Full), ThemeAtlasRules.Pick(31f, MedalFinish.Gilt, medals, plain, row));
        Assert.Equal(new AtlasPick(AtlasSource.Row, 12, false, RowFinish.Full), ThemeAtlasRules.Pick(9f, MedalFinish.Gilt, medals, plain, row));

        // Quiet takes the full cells when the strip has no Quiet finish; Plain takes its own.
        Assert.Equal(new AtlasPick(AtlasSource.Row, 16, false, RowFinish.Full), ThemeAtlasRules.Pick(16f, MedalFinish.LightRim, medals, plain, row));
        Assert.Equal(new AtlasPick(AtlasSource.Row, 16, false, RowFinish.Plain), ThemeAtlasRules.Pick(16f, MedalFinish.Plain, medals, plain, row));

        Assert.True(RowStripLayout.TryParse(RowJson("full", "quiet").ToJsonString(), out var quietRow, out _));
        Assert.Equal(RowFinish.Quiet, ThemeAtlasRules.Pick(16f, MedalFinish.LightRim, medals, plain, quietRow).Finish);
    }

    [Fact]
    public void Medallion_stands_in_for_anything_missing()
    {
        var (medals, _, row) = Layouts();
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(20f, MedalFinish.Gilt, medals, null, null));
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(64f, MedalFinish.Gilt, null, null, row));
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(64f, MedalFinish.Plain, medals, null, row));
        Assert.True(RowStripLayout.TryParse(RowJson("full").ToJsonString(), out var fullOnly, out _));
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(16f, MedalFinish.Plain, medals, null, fullOnly));
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(64f, MedalFinish.Classic, medals, medals, row));
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(0f, MedalFinish.Gilt, medals, medals, row));
        Assert.Equal(AtlasSource.StandIn, AtlasPick.StandIn.Source);
    }

    // ------------------------------------------------------------------ residency

    [Fact]
    public void Only_the_appearances_atlas_sets_are_wanted_and_their_rows_preloaded()
    {
        var residency = new AtlasResidency();
        residency.Retain(Mix("ishgard-glass", ("ready", "aether-crystal")));
        Assert.True(residency.IsWanted(GlyphSetId.IshgardGlass));
        Assert.True(residency.IsWanted(GlyphSetId.AetherCrystal));
        Assert.False(residency.IsWanted(GlyphSetId.Medallion));
        Assert.False(residency.IsWanted(GlyphSetId.Orrery));
        Assert.True(residency.ShouldPreload(GlyphSetId.IshgardGlass, AtlasPart.Row));
        Assert.False(residency.ShouldPreload(GlyphSetId.IshgardGlass, AtlasPart.Medals));
        Assert.False(residency.ShouldPreload(GlyphSetId.IshgardGlass, AtlasPart.Medals2x));
        Assert.False(residency.ShouldPreload(GlyphSetId.Orrery, AtlasPart.Row));

        residency.Touch(GlyphSetId.IshgardGlass, AtlasPart.Row, 0);
        Assert.False(residency.ShouldPreload(GlyphSetId.IshgardGlass, AtlasPart.Row));

        // The default appearance and high contrast use no atlas set at all.
        residency.Retain(ResolvedAppearance.Default);
        Assert.All(GlyphSets.All, set => Assert.False(residency.IsWanted(set.Id)));
        residency.Retain(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "ishgard-glass", HighContrast = true }));
        Assert.False(residency.IsWanted(GlyphSetId.IshgardGlass));
    }

    [Fact]
    public void A_wanted_sets_1x_parts_stay_while_2x_and_dropped_sets_are_released_after_idle()
    {
        var residency = new AtlasResidency();
        residency.Retain(Mix("ishgard-glass"));
        residency.Touch(GlyphSetId.IshgardGlass, AtlasPart.Row, 0);
        residency.Touch(GlyphSetId.IshgardGlass, AtlasPart.Medals, 0);
        residency.Touch(GlyphSetId.IshgardGlass, AtlasPart.Medals2x, 0);

        var later = AtlasResidency.IdleSeconds + 1;
        Assert.False(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Row, later));
        Assert.False(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Medals, later));
        Assert.False(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Medals2x, AtlasResidency.IdleSeconds - 1));
        Assert.True(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Medals2x, later));

        residency.Released(GlyphSetId.IshgardGlass, AtlasPart.Medals2x);
        Assert.False(residency.IsLoaded(GlyphSetId.IshgardGlass, AtlasPart.Medals2x));
        Assert.False(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Medals2x, later * 2));

        // Switching theme drops the set: it goes once idle, not at once (a hover preview does not thrash).
        residency.Retain(Mix("aether-crystal"));
        Assert.False(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Row, 1));
        Assert.True(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Row, later));
        Assert.True(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Medals, later));

        // A preview that keeps drawing a set outside the appearance keeps it.
        residency.Touch(GlyphSetId.IshgardGlass, AtlasPart.Medals, later);
        Assert.False(residency.ShouldRelease(GlyphSetId.IshgardGlass, AtlasPart.Medals, later + 1));
    }

    [Fact]
    public void A_set_in_another_kit_wants_its_faces_and_the_kit_its_frames_never_its_composites()
    {
        var residency = new AtlasResidency();

        // Ishgard Glass in Silver frames, Ready from Medallion: both are composed (neither's own kit), so their faces and
        // the Silver kit's frames are wanted, and the row strips of both preload; Glass's medals and row are not.
        residency.Retain(AppearanceResolver.Resolve(new AppearanceConfig
        {
            Theme = "ishgard-glass",
            Frames = "silver",
            Glyphs = new Dictionary<string, string> { ["ready"] = "medallion" },
        }));
        Assert.True(residency.WantsFaces(GlyphSetId.IshgardGlass));
        Assert.True(residency.WantsFaces(GlyphSetId.Medallion));
        Assert.False(residency.WantsComposites(GlyphSetId.IshgardGlass));
        Assert.True(residency.IsWanted(FrameKitId.Silver));
        Assert.False(residency.IsWanted(FrameKitId.Came));
        Assert.True(residency.ShouldPreload(GlyphSetId.IshgardGlass, AtlasPart.FacesRow));
        Assert.False(residency.ShouldPreload(GlyphSetId.IshgardGlass, AtlasPart.Row));
        Assert.True(residency.ShouldPreload(FrameKitId.Silver, AtlasPart.FramesRow));
        Assert.False(residency.ShouldPreload(FrameKitId.Silver, AtlasPart.Frames));

        // The kit's 1x frames stay while wanted; its 2x goes once idle; a dropped kit goes once idle.
        residency.Touch(FrameKitId.Silver, AtlasPart.Frames, 0);
        residency.Touch(FrameKitId.Silver, AtlasPart.Frames2x, 0);
        var later = AtlasResidency.IdleSeconds + 1;
        Assert.False(residency.ShouldRelease(FrameKitId.Silver, AtlasPart.Frames, later));
        Assert.True(residency.ShouldRelease(FrameKitId.Silver, AtlasPart.Frames2x, later));
        residency.Retain(ResolvedAppearance.Default);
        Assert.True(residency.ShouldRelease(FrameKitId.Silver, AtlasPart.Frames, later));
        residency.Released(FrameKitId.Silver, AtlasPart.Frames);
        Assert.False(residency.IsLoaded(FrameKitId.Silver, AtlasPart.Frames));

        // The default look composes nothing: no faces, no kit.
        Assert.All(GlyphSets.All, set => Assert.False(residency.WantsFaces(set.Id)));
        Assert.All(FrameKits.All, kit => Assert.False(residency.IsWanted(kit.Id)));
        Assert.False(residency.IsWanted((FrameKitId)0));
        Assert.False(residency.IsWanted((FrameKitId)200));
    }

    [Fact]
    public void Residency_ignores_ids_it_has_no_slot_for()
    {
        var residency = new AtlasResidency();
        residency.Touch((GlyphSetId)0, AtlasPart.Row, 0);
        residency.Touch((GlyphSetId)200, AtlasPart.Row, 0);
        Assert.False(residency.IsLoaded((GlyphSetId)200, AtlasPart.Row));
        Assert.False(residency.ShouldRelease((GlyphSetId)0, AtlasPart.Row, 1000));
        Assert.False(residency.IsWanted((GlyphSetId)200));
    }

    // ------------------------------------------------------------------ shipped sets

    [Fact]
    public void Every_theme_folder_meets_the_contract_and_its_budgets_and_the_worst_reachable_appearance_fits_12_MB()
    {
        // A missing folder fails: an empty list would pass every budget below without measuring anything.
        var dir = ThemesDir();
        Assert.True(Directory.Exists(dir), $"{dir} is missing");
        var folders = Directory.GetDirectories(dir);
        Assert.NotEmpty(folders);
        Assert.True(Directory.Exists(KitsDir()), $"{KitsDir()} is missing");
        Assert.NotEmpty(Directory.GetDirectories(KitsDir()));

        // Each set's 1x bytes on its two paths (Medallion as designed is its embedded atlas), and each kit's 1x frames and
        // ornaments, for the reachable worst case below.
        var sets = new List<SetBytes>();
        foreach (var folder in folders)
        {
            var key = Path.GetFileName(folder);
            Assert.True(GlyphSets.TryGet(key, out var set), $"assets/ui/themes/{key} is not a registered glyph set");
            Assert.Equal(key, set.Key);

            var (faces1x, faces2x) = AssertParts(folder, "faces", PartAtlasKind.Faces);
            Assert.True(faces1x <= 4L * 1024 * 1024, $"{key}: {faces1x} bytes of 1x faces textures (budget 4 MB)");
            Assert.True(faces2x <= 12L * 1024 * 1024, $"{key}: {faces2x} bytes of 2x faces textures (budget 12 MB)");
            var facesOnDisk = Directory.GetFiles(folder, "faces*.png").Sum(static f => new FileInfo(f).Length);
            Assert.True(facesOnDisk <= 1_572_864, $"{key}: {facesOnDisk} bytes of faces PNG (budget 1.5 MB)");

            if (set.Kind == GlyphRenderKind.Procedural)
            {
                // Medallion's folder holds its faces for the frames axis and the build's metrics: its own atlas stays
                // embedded at assets/ui/.
                Assert.Equal(["faces-row.png", "faces.png", "faces@2x.png"], Directory.GetFiles(folder, "*.png").Select(Path.GetFileName).Order(StringComparer.Ordinal));
                sets.Add(new SetBytes(set.Id, set.DefaultFrames, (long)MedalLayout.Width * MedalLayout.Height * 4, faces1x));
                continue;
            }

            var medals = Path.Combine(folder, "medals.json");
            Assert.True(File.Exists(medals), $"{key}: medals.json is required");
            Assert.True(HeroAtlasLayout.TryParse(File.ReadAllText(medals), out var hero, out var error), $"{key}: {error}");
            AssertPngs(folder, "medals", hero!.Width, hero.Height, twoX: true);
            var bytes1x = hero.Bytes1x;
            var bytes2x = hero.Bytes2x;

            var plainPath = Path.Combine(folder, "plain.json");
            if (File.Exists(plainPath))
            {
                Assert.True(HeroAtlasLayout.TryParse(File.ReadAllText(plainPath), out var plain, out error), $"{key} plain: {error}");
                AssertPngs(folder, "plain", plain!.Width, plain.Height, twoX: true);
                bytes1x += plain.Bytes1x;
                bytes2x += plain.Bytes2x;
            }

            var rowPath = Path.Combine(folder, "row.json");
            if (File.Exists(rowPath))
            {
                Assert.True(RowStripLayout.TryParse(File.ReadAllText(rowPath), out var row, out error), $"{key} row: {error}");
                Assert.Equal((12, 31), (row!.MinSize, row.MaxSize));
                AssertPngs(folder, "row", row.Width, row.Height, twoX: false);
                bytes1x += row.Bytes;
            }

            Assert.True(bytes1x <= 4L * 1024 * 1024, $"{key}: {bytes1x} bytes of 1x textures (budget 4 MB)");
            Assert.True(bytes2x <= 12L * 1024 * 1024, $"{key}: {bytes2x} bytes of 2x textures (budget 12 MB)");
            var onDisk = Directory.GetFiles(folder, "*.png").Where(static f => !Path.GetFileName(f).StartsWith("faces", StringComparison.Ordinal)).Sum(static f => new FileInfo(f).Length);
            Assert.True(onDisk <= 2_621_440, $"{key}: {onDisk} bytes of PNG as designed (budget 2.5 MB)");
            sets.Add(new SetBytes(set.Id, set.DefaultFrames, bytes1x, faces1x));
        }

        var kits = new Dictionary<FrameKitId, long>();
        foreach (var folder in Directory.GetDirectories(KitsDir()))
        {
            var key = Path.GetFileName(folder);
            Assert.True(FrameKits.TryGet(key, out var kit) && kit.Key == key, $"assets/ui/kits/{key} is not a registered frame kit");
            var (frames1x, frames2x) = AssertParts(folder, "frames", PartAtlasKind.Frames);
            Assert.True(frames1x <= 4L * 1024 * 1024, $"{key}: {frames1x} bytes of 1x frames textures (budget 4 MB)");
            Assert.True(frames2x <= 12L * 1024 * 1024, $"{key}: {frames2x} bytes of 2x frames textures (budget 12 MB)");
            var onDisk = Directory.GetFiles(folder, "*.png").Sum(static f => new FileInfo(f).Length);
            Assert.True(onDisk <= 1_572_864, $"{key}: {onDisk} bytes of PNG (budget 1.5 MB)");
            var ornaments1x = 0L;
            var ornamentsPath = Path.Combine(folder, "ornaments.json");
            if (File.Exists(ornamentsPath))
            {
                Assert.True(KitOrnamentLayout.TryParse(File.ReadAllText(ornamentsPath), out var ornaments, out var error), $"{key} ornaments: {error}");
                Assert.Equal((ornaments!.Width, ornaments.Height), OrnamentLayoutTests.PngSize(Path.Combine(folder, "ornaments.png")));
                ornaments1x = ornaments.Bytes;
            }

            kits[kit.Id] = frames1x + ornaments1x;
        }

        var (worst, inKit) = WorstReachable1x(sets, kits);
        Assert.True(worst <= ReachableBudget1x, $"the worst reachable appearance (in {inKit}) is {worst} bytes at 1x (budget 12 MB)");
    }

    /// <summary>The 1x RGBA every reachable appearance may hold at once (ATLAS-CONTRACT §6).</summary>
    private const long ReachableBudget1x = 12L * 1024 * 1024;

    /// <summary>A set's 1x bytes as designed (medals, plain, row) and composed (faces, faces-row), and its own kit.</summary>
    private readonly record struct SetBytes(GlyphSetId Set, FrameKitId OwnKit, long Designed, long Faces);

    /// <summary>
    /// The most 1x RGBA a reachable appearance holds, and the kit it is in (ATLAS-CONTRACT §6). The resolver composes every
    /// set whose own kit is not the appearance's (<see cref="ResolvedAppearance.Composes"/>), so in kit K only the sets
    /// whose own kit is K draw as designed (one per kit today) and every other set draws from its faces; K's frames and
    /// ornaments are the only kit parts loaded. The worst case is every set in the column at once, taken over every kit.
    /// </summary>
    private static (long Bytes, FrameKitId Kit) WorstReachable1x(IReadOnlyList<SetBytes> sets, IReadOnlyDictionary<FrameKitId, long> kits)
    {
        var worst = (Bytes: 0L, Kit: FrameKitId.Brass);
        foreach (var (kit, kitBytes) in kits)
        {
            var total = kitBytes + sets.Sum(s => s.OwnKit == kit ? s.Designed : s.Faces);
            if (total > worst.Bytes)
            {
                worst = (total, kit);
            }
        }

        return worst;
    }

    [Fact]
    public void The_reachable_budget_still_fails_when_one_real_look_would_go_over_12_MB()
    {
        const long mb = 1024 * 1024;
        var kits = new Dictionary<FrameKitId, long> { [FrameKitId.Brass] = 2 * mb, [FrameKitId.Silver] = 2 * mb };
        SetBytes[] fits =
        [
            new(GlyphSetId.Medallion, FrameKitId.Brass, 3 * mb, 2 * mb),
            new(GlyphSetId.AetherCrystal, FrameKitId.Silver, 3 * mb, 2 * mb),
            new(GlyphSetId.IshgardGlass, FrameKitId.Came, 3 * mb, 2 * mb),
        ];

        // In Brass: Medallion as designed (3), the others' faces (2 + 2) and Brass's frames (2) are 9 MB. Counting every
        // set at its larger path (3 + 3 + 3 + 2) would be 11, a look nothing draws.
        Assert.Equal((9 * mb, FrameKitId.Brass), WorstReachable1x(fits, kits));

        // One set's faces growing by 4 MB makes every look that composes it 13 MB: the check catches it.
        SetBytes[] over = [fits[0], fits[1], fits[2] with { Faces = 6 * mb }];
        Assert.Equal((13 * mb, FrameKitId.Brass), WorstReachable1x(over, kits));
        Assert.True(WorstReachable1x(over, kits).Bytes > ReachableBudget1x);

        // So does a set as designed in its own kit going over, or a kit's frames and ornaments.
        Assert.True(WorstReachable1x([fits[0] with { Designed = 7 * mb }, fits[1], fits[2]], kits).Bytes > ReachableBudget1x);
        Assert.True(WorstReachable1x(fits, new Dictionary<FrameKitId, long> { [FrameKitId.Came] = 6 * mb }).Bytes > ReachableBudget1x);
    }

    [Fact]
    public void Every_offered_kit_ships_its_frames_and_every_mixable_offered_set_its_faces()
    {
        foreach (var kit in FrameKits.All.Where(static k => k.Offered))
        {
            Assert.True(File.Exists(Path.Combine(KitsDir(), kit.Key, "frames.json")), kit.Key);
            Assert.True(File.Exists(Path.Combine(KitsDir(), kit.Key, "frames-row.json")), kit.Key);
        }

        foreach (var set in GlyphSets.All.Where(static s => s.Offered && s.Mixable))
        {
            Assert.True(File.Exists(Path.Combine(ThemesDir(), set.Key, "faces.json")), set.Key);
            Assert.True(File.Exists(Path.Combine(ThemesDir(), set.Key, "faces-row.json")), set.Key);
        }

        Assert.Equal(Path.Combine("assets", "ui", "kits", "came", "frames.png"), ThemeAtlasRules.RelativePath(FrameKitId.Came, "frames.png"));
    }

    [Fact]
    public void Every_sets_faces_and_every_kits_frames_cover_the_same_sizes_so_composing_never_misses_one()
    {
        // TryCompose picks the cell from the faces atlas and looks the frame up at that same cell: a row size or hero
        // tier one side has and the other lacks would fall back to Medallion's procedural medal at that size only.
        var parts = new List<(string Name, IReadOnlyList<int> Row, IReadOnlyList<int> Tiers)>();
        foreach (var set in GlyphSets.All.Where(static s => s.Offered && s.Mixable))
        {
            parts.Add(Cells(Path.Combine(ThemesDir(), set.Key), "faces", PartAtlasKind.Faces));
        }

        foreach (var kit in FrameKits.All.Where(static k => k.Offered))
        {
            parts.Add(Cells(Path.Combine(KitsDir(), kit.Key), "frames", PartAtlasKind.Frames));
        }

        Assert.True(parts.Count >= 2);
        var (first, row, tiers) = parts[0];
        foreach (var (name, otherRow, otherTiers) in parts.Skip(1))
        {
            Assert.True(row.SequenceEqual(otherRow), $"{name}'s row sizes [{string.Join(',', otherRow)}] are not {first}'s [{string.Join(',', row)}]");
            Assert.True(tiers.SequenceEqual(otherTiers), $"{name}'s hero tiers [{string.Join(',', otherTiers)}] are not {first}'s [{string.Join(',', tiers)}]");
        }

        // And the row strips reach from the smallest row medal to just under the hero tiers, whole pixel by whole pixel.
        Assert.Equal(Enumerable.Range(row[0], row.Count), row);
        Assert.Equal((int)MedalLayout.RowTierMaxPx - 1, row[^1]);
    }

    private static (string Name, IReadOnlyList<int> Row, IReadOnlyList<int> Tiers) Cells(string folder, string stem, PartAtlasKind kind)
    {
        Assert.True(PartAtlasLayout.TryParse(File.ReadAllText(Path.Combine(folder, stem + "-row.json")), kind, out var row, out var error), $"{folder} {stem}-row: {error}");
        Assert.True(PartAtlasLayout.TryParse(File.ReadAllText(Path.Combine(folder, stem + ".json")), kind, out var hero, out error), $"{folder} {stem}: {error}");
        return ($"{Path.GetFileName(folder)}/{stem}", row!.Cells, hero!.Cells);
    }

    private static string KitsDir() => Path.Combine(OrnamentLayoutTests.AssetsDir(), "kits");

    /// <summary>A faces or frames pair (hero atlas at 1x and 2x, row strip) that parses and is the PNGs' size; its texture bytes.</summary>
    private static (long OneX, long TwoX) AssertParts(string folder, string stem, PartAtlasKind kind)
    {
        Assert.True(PartAtlasLayout.TryParse(File.ReadAllText(Path.Combine(folder, stem + ".json")), kind, out var hero, out var error), $"{folder} {stem}: {error}");
        Assert.False(hero!.Row);
        Assert.Equal(MedalLayout.Tiers, hero.Cells);
        AssertPngs(folder, stem, hero.Width, hero.Height, twoX: true);
        Assert.True(PartAtlasLayout.TryParse(File.ReadAllText(Path.Combine(folder, stem + "-row.json")), kind, out var row, out error), $"{folder} {stem}-row: {error}");
        Assert.True(row!.Row);
        Assert.Equal(Enumerable.Range(12, 20), row.Cells);
        AssertPngs(folder, stem + "-row", row.Width, row.Height, twoX: false);
        return (hero.Bytes1x + row.Bytes1x, hero.Bytes2x);
    }

    [Theory]
    [InlineData("ishgard-glass")]
    [InlineData("aether-crystal")]
    [InlineData("astrologian-orrery")]
    [InlineData("sumi-to-kinpaku")]
    public void The_shipped_revived_sets_are_drawable_from_hero_to_row(string key)
    {
        var folder = Path.Combine(ThemesDir(), key);
        Assert.True(HeroAtlasLayout.TryParse(File.ReadAllText(Path.Combine(folder, "medals.json")), out var medals, out var error), error);
        Assert.True(RowStripLayout.TryParse(File.ReadAllText(Path.Combine(folder, "row.json")), out var row, out error), error);
        Assert.True(row!.Has(RowFinish.Full));
        Assert.True(GlyphSets.TryGet(key, out var set) && set.Offered && set.Kind == GlyphRenderKind.Atlas);

        // Every size a medal is drawn at has a source: the strip's exact cell below 32 px, a hero tier from there.
        for (var px = 8f; px <= 300f; px += 1f)
        {
            var pick = ThemeAtlasRules.Pick(px, MedalFinish.Gilt, medals, null, row);
            Assert.Equal(px < MedalLayout.RowTierMaxPx ? AtlasSource.Row : AtlasSource.Medals, pick.Source);
            if (pick.Source == AtlasSource.Row)
            {
                Assert.Equal(Math.Clamp((int)px, 12, 31), pick.Cell);
            }
        }
    }

    [Fact]
    public void Sumi_draws_its_own_flat_finish_at_Plain_below_32_px_and_every_other_set_Medallions()
    {
        // theme-system §3.5: a set's own flat finish where it has one (HasPlainFinish: Sumi to Kinpaku's row strip carries
        // a 'plain' finish), otherwise Medallion's Plain ladder stands in. No set ships a hero 'plain' atlas (one would
        // break the 4 MB per-set budget beside medals and the row strip), so from 32 px Medallion's Plain stands in for all.
        foreach (var set in GlyphSets.All.Where(static s => s.Offered && s.Kind == GlyphRenderKind.Atlas))
        {
            var folder = Path.Combine(ThemesDir(), set.Key);
            Assert.True(RowStripLayout.TryParse(File.ReadAllText(Path.Combine(folder, "row.json")), out var row, out var error), error);
            Assert.True(HeroAtlasLayout.TryParse(File.ReadAllText(Path.Combine(folder, "medals.json")), out var medals, out error), error);
            Assert.False(File.Exists(Path.Combine(folder, "plain.json")), set.Key);
            Assert.Equal(set.HasPlainFinish, row!.Has(RowFinish.Plain));
            for (var px = 8f; px <= 300f; px += 1f)
            {
                var pick = ThemeAtlasRules.Pick(px, MedalFinish.Plain, medals, null, row);
                if (px < MedalLayout.RowTierMaxPx && set.HasPlainFinish)
                {
                    Assert.Equal((AtlasSource.Row, RowFinish.Plain, Math.Clamp((int)px, 12, 31)), (pick.Source, pick.Finish, pick.Cell));
                    Assert.True(row.TryRect(QuestState.Ready, RowFinish.Plain, pick.Cell, out var plain) && row.TryRect(QuestState.Ready, RowFinish.Full, pick.Cell, out var full) && plain != full);
                }
                else
                {
                    Assert.Equal(AtlasSource.StandIn, pick.Source);
                }
            }
        }

        Assert.True(GlyphSets.Sumi.HasPlainFinish);
    }

    [Fact]
    public void The_ornament_kits_strip_is_kept_while_its_metal_is_the_ornaments()
    {
        var residency = new AtlasResidency();
        residency.Retain(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "sumi-to-kinpaku" }));
        Assert.True(residency.WantsOrnaments(FrameKitId.Kirikane));
        Assert.False(residency.WantsOrnaments(FrameKitId.Brass));
        Assert.True(residency.ShouldPreload(FrameKitId.Kirikane, AtlasPart.Ornaments));

        // Sumi in its own kit composes nothing, so the kit's frames are not wanted; its ornaments are.
        Assert.False(residency.IsWanted(FrameKitId.Kirikane));
        residency.Touch(FrameKitId.Kirikane, AtlasPart.Ornaments, 0);
        Assert.False(residency.ShouldPreload(FrameKitId.Kirikane, AtlasPart.Ornaments));
        var later = AtlasResidency.IdleSeconds + 1;
        Assert.False(residency.ShouldRelease(FrameKitId.Kirikane, AtlasPart.Ornaments, later));

        // Another kit's frames (or high contrast, which draws the palette's ornament) let the strip go once idle.
        residency.Retain(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "sumi-to-kinpaku", HighContrast = true }));
        Assert.False(residency.WantsOrnaments(FrameKitId.Kirikane));
        Assert.True(residency.ShouldRelease(FrameKitId.Kirikane, AtlasPart.Ornaments, later));
        residency.Retain(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "sumi-to-kinpaku", Frames = "brass" }));
        Assert.False(residency.WantsOrnaments(FrameKitId.Kirikane));
        Assert.True(residency.WantsOrnaments(FrameKitId.Brass));
    }

    [Fact]
    public void Theme_folders_are_packaged_as_content_files()
    {
        var csproj = File.ReadAllText(Path.Combine(OrnamentLayoutTests.RepoRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        Assert.Contains(@"<Content Include=""assets\ui\themes\**\*.png;assets\ui\themes\**\*.json"" Exclude=""assets\ui\themes\**\metrics.json"" CopyToOutputDirectory=""PreserveNewest"" />", csproj);
        Assert.Contains(@"<Content Include=""assets\ui\kits\**\*.png;assets\ui\kits\**\*.json"" Exclude=""assets\ui\kits\**\metrics.json"" CopyToOutputDirectory=""PreserveNewest"" />", csproj);
        Assert.Equal(Path.Combine("assets", "ui", "themes", "ishgard-glass", "row.png"), ThemeAtlasRules.RelativePath(GlyphSetId.IshgardGlass, "row.png"));

        // What the build actually put beside the plugin (the gates build the solution first; a test run that did not
        // build the plugin checks the project file alone): every atlas and layout of every set, byte for byte, where the
        // runtime looks for it, and no metrics.json, which is the build's record and not the plugin's.
        if (Diagnostics.NoNetworkTests.PluginAssembly() is not { } plugin)
        {
            return;
        }

        var output = Path.GetDirectoryName(plugin)!;
        var expected = Directory.GetFiles(ThemesDir(), "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(KitsDir(), "*", SearchOption.AllDirectories))
            .Where(static f => Path.GetFileName(f) != "metrics.json")
            .Select(f => Path.GetRelativePath(OrnamentLayoutTests.AssetsDir(), f))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains(Path.Combine("themes", "ishgard-glass", "row.png"), expected);
        Assert.Contains(Path.Combine("kits", "astrolabe", "frames@2x.png"), expected);
        var shipped = Directory.GetFiles(Path.Combine(output, "assets", "ui", "themes"), "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(output, "assets", "ui", "kits"), "*", SearchOption.AllDirectories))
            .Select(f => Path.GetRelativePath(Path.Combine(output, "assets", "ui"), f))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, shipped);
        foreach (var file in expected)
        {
            Assert.True(
                File.ReadAllBytes(Path.Combine(OrnamentLayoutTests.AssetsDir(), file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(output, "assets", "ui", file))),
                $"{file} in the build output differs from the repo's");
        }

        Assert.True(File.Exists(Path.Combine(output, ThemeAtlasRules.RelativePath(GlyphSetId.IshgardGlass, "row.png"))));
    }

    // ------------------------------------------------------------------ helpers

    private static void AssertPngs(string folder, string stem, int width, int height, bool twoX)
    {
        var one = Path.Combine(folder, stem + ".png");
        Assert.True(File.Exists(one), one);
        Assert.Equal((width, height), OrnamentLayoutTests.PngSize(one));
        if (twoX)
        {
            var two = Path.Combine(folder, stem + "@2x.png");
            Assert.True(File.Exists(two), two);
            Assert.Equal((width * 2, height * 2), OrnamentLayoutTests.PngSize(two));
        }
    }

    private static ResolvedAppearance Mix(string theme, params (string State, string Set)[] mix) => AppearanceResolver.Resolve(new AppearanceConfig
    {
        Theme = theme,
        Glyphs = mix.Length == 0 ? null : mix.ToDictionary(static m => m.State, static m => m.Set),
    });

    private static (HeroAtlasLayout Medals, HeroAtlasLayout Plain, RowStripLayout Row) Layouts()
    {
        Assert.True(HeroAtlasLayout.TryParse(HeroJson().ToJsonString(), out var medals, out _));
        Assert.True(HeroAtlasLayout.TryParse(HeroJson().ToJsonString(), out var plain, out _));
        Assert.True(RowStripLayout.TryParse(RowJson("full", "plain").ToJsonString(), out var row, out _));
        return (medals!, plain!, row!);
    }

    /// <summary>A hero layout packed as the generator packs Medallion's: a band of rows per tier, 2 px apart.</summary>
    private static JsonObject HeroJson()
    {
        int[] tiers = [48, 64, 96, 128];
        const int width = 782;
        var sprites = new JsonObject();
        var y = 2;
        var cells = new Dictionary<string, JsonObject>();
        foreach (var tier in tiers)
        {
            var perRow = Math.Min(MedalLayout.SpriteCount, (width - 2) / (tier + 2));
            for (var i = 0; i < MedalLayout.SpriteCount; i++)
            {
                var key = MedalLayout.Key((MedalSprite)i);
                if (!cells.TryGetValue(key, out var byTier))
                {
                    cells[key] = byTier = [];
                }

                byTier[tier.ToString(CultureInfo.InvariantCulture)] = new JsonArray(2 + (i % perRow) * (tier + 2), y + (i / perRow) * (tier + 2), tier, tier);
            }

            y += ((MedalLayout.SpriteCount + perRow - 1) / perRow) * (tier + 2);
        }

        foreach (var (key, byTier) in cells)
        {
            sprites[key] = byTier;
        }

        return new JsonObject
        {
            ["size"] = new JsonArray(width, y + 2),
            ["tiers"] = new JsonArray(tiers.Select(static t => (JsonNode)t).ToArray()),
            ["sprites"] = sprites,
        };
    }

    /// <summary>A row strip with the given finishes: one band per finish and state, the 20 sizes side by side.</summary>
    private static JsonObject RowJson(params string[] finishes)
    {
        var sprites = new JsonObject();
        var y = 2;
        var width = 2;
        foreach (var finish in finishes)
        {
            var states = new JsonObject();
            foreach (var state in AppearanceStates.All)
            {
                var bySize = new JsonObject();
                var x = 2;
                for (var px = 12; px <= 31; px++)
                {
                    bySize[px.ToString(CultureInfo.InvariantCulture)] = new JsonArray(x, y, px, px);
                    x += px + 2;
                }

                width = Math.Max(width, x);
                states[AppearanceStates.Key(state)] = bySize;
                y += 31 + 2;
            }

            sprites[finish] = states;
        }

        return new JsonObject
        {
            ["size"] = new JsonArray(width + 1, y + 1),
            ["sizes"] = new JsonArray(Enumerable.Range(12, 20).Select(static s => (JsonNode)s).ToArray()),
            ["finishes"] = new JsonArray(finishes.Select(static f => (JsonNode)f).ToArray()),
            ["sprites"] = sprites,
        };
    }
}
