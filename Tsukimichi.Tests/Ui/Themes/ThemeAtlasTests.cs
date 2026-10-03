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
public sealed class ThemeAtlasTests
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

    // ------------------------------------------------------------------ tier choice

    [Fact]
    public void Hero_sizes_draw_from_the_hero_atlas_and_rows_from_the_strip()
    {
        var (medals, plain, row) = Layouts();

        var hero = ThemeAtlasRules.Pick(48f, MedalFinish.Gilt, medals, plain, row);
        Assert.Equal(new AtlasPick(AtlasSource.Medals, 48, false, RowFinish.Full), hero);

        Assert.Equal(new AtlasPick(AtlasSource.Medals, 64, true, RowFinish.Full), ThemeAtlasRules.Pick(200f, MedalFinish.Gilt, medals, plain, row));
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
        Assert.Equal(AtlasPick.StandIn, ThemeAtlasRules.Pick(16f, MedalFinish.Plain, medals, null, row));
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
    public void Every_theme_folder_is_a_registered_atlas_set_that_meets_the_contract_and_its_budgets()
    {
        var dir = ThemesDir();
        var folders = Directory.Exists(dir) ? Directory.GetDirectories(dir) : [];
        long worstCase1x = (long)MedalLayout.Width * MedalLayout.Height * 4;
        foreach (var folder in folders)
        {
            var key = Path.GetFileName(folder);
            Assert.True(GlyphSets.TryGet(key, out var set), $"assets/ui/themes/{key} is not a registered glyph set");
            Assert.Equal(key, set.Key);
            Assert.Equal(GlyphRenderKind.Atlas, set.Kind);

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
            var onDisk = Directory.GetFiles(folder, "*.png").Sum(static f => new FileInfo(f).Length);
            Assert.True(onDisk <= 2_621_440, $"{key}: {onDisk} bytes of PNG (budget 2.5 MB)");
            worstCase1x += bytes1x;
        }

        Assert.True(worstCase1x <= 12L * 1024 * 1024, $"every set at once is {worstCase1x} bytes at 1x (budget 12 MB)");
    }

    [Fact]
    public void Theme_folders_are_packaged_as_content_files()
    {
        var csproj = File.ReadAllText(Path.Combine(OrnamentLayoutTests.RepoRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        Assert.Contains(@"<Content Include=""assets\ui\themes\**\*.png;assets\ui\themes\**\*.json"" CopyToOutputDirectory=""PreserveNewest"" />", csproj);
        Assert.Equal(Path.Combine("assets", "ui", "themes", "ishgard-glass", "row.png"), ThemeAtlasRules.RelativePath(GlyphSetId.IshgardGlass, "row.png"));
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
