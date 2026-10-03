using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The appearance model (feature plan v7 T1; theme-system §5.3, §7.2 "AppearanceMigrationTests"): migration from the 1.15
/// look settings and back, sanitising, the resolver's rules, the cache, and the edits the 1.15 controls make.
/// </summary>
public sealed class AppearanceTests
{
    public static TheoryData<MoonStyle, GlyphPaletteKind, bool> LegacyCombinations()
    {
        var data = new TheoryData<MoonStyle, GlyphPaletteKind, bool>();
        foreach (var style in Enum.GetValues<MoonStyle>())
        {
            foreach (var palette in Enum.GetValues<GlyphPaletteKind>())
            {
                data.Add(style, palette, false);
                data.Add(style, palette, true);
            }
        }

        return data;
    }

    // ------------------------------------------------------------------ migration

    [Theory]
    [MemberData(nameof(LegacyCombinations))]
    public void Every_1_15_look_migrates_and_writes_back_unchanged(MoonStyle style, GlyphPaletteKind palette, bool follow)
    {
        var legacy = new LegacyAppearance(style, palette, follow);
        var config = AppearanceMigration.FromLegacy(legacy);
        Assert.Equal(legacy, AppearanceMigration.ToLegacy(config));

        var resolved = AppearanceResolver.Resolve(config);
        Assert.Equal(style, resolved.MoonStyle);
        Assert.Equal(palette, resolved.GlyphPalette);
        Assert.Equal(follow, resolved.FollowDalamud);
        Assert.Empty(resolved.Unknown);
    }

    [Fact]
    public void Classic_becomes_the_classic_theme_high_contrast_the_flag_and_follow_dalamud_the_palette()
    {
        var config = AppearanceMigration.FromLegacy(new LegacyAppearance(MoonStyle.Classic, GlyphPaletteKind.HighContrast, true));
        Assert.Equal("classic", config.Theme);
        Assert.True(config.HighContrast);
        Assert.Equal("dalamud", config.Palette);
        Assert.Null(config.Glyphs);
        Assert.Null(config.Frames);

        var plain = AppearanceMigration.FromLegacy(default);
        Assert.Equal("medallion", plain.Theme);
        Assert.False(plain.HighContrast);
        Assert.Null(plain.Palette);
        Assert.True(plain.SameAs(new AppearanceConfig()));
    }

    [Fact]
    public void An_unknown_1_15_moon_style_migrates_to_the_default_theme()
    {
        Assert.Equal("medallion", AppearanceMigration.FromLegacy(new LegacyAppearance((MoonStyle)7, GlyphPaletteKind.Standard, false)).Theme);
    }

    [Fact]
    public void A_new_theme_writes_back_as_medallion_with_its_palette_and_contrast()
    {
        var config = new AppearanceConfig { Theme = "ishgard-glass", HighContrast = true, Palette = "dalamud" };
        Assert.Equal(new LegacyAppearance(MoonStyle.Medallion, GlyphPaletteKind.HighContrast, true), AppearanceMigration.ToLegacy(config));
    }

    [Fact]
    public void Sanitize_tidies_keys_and_keeps_and_reports_unknown_ones()
    {
        var config = new AppearanceConfig
        {
            Version = 0,
            Theme = "  ",
            Palette = " Ishgard-Snow ",
            Frames = "",
            Glyphs = new Dictionary<string, string> { [" Ready "] = "AETHER-CRYSTAL", ["blocked"] = " ", ["moonlit"] = "ishgard-glass", ["completed"] = "prism-of-tomorrow" },
        };

        var unknown = AppearanceMigration.Sanitize(config);

        Assert.Equal(AppearanceConfig.CurrentVersion, config.Version);
        Assert.Equal("medallion", config.Theme);
        Assert.Equal("ishgard-snow", config.Palette);
        Assert.Null(config.Frames);
        Assert.Equal(new Dictionary<string, string> { ["ready"] = "aether-crystal", ["moonlit"] = "ishgard-glass", ["completed"] = "prism-of-tomorrow" }, config.Glyphs);
        Assert.Equal(["state: moonlit", "completed: prism-of-tomorrow"], unknown);
    }

    [Fact]
    public void Sanitize_drops_an_empty_mix()
    {
        var config = new AppearanceConfig { Glyphs = new Dictionary<string, string> { ["ready"] = "" } };
        Assert.Empty(AppearanceMigration.Sanitize(config));
        Assert.Null(config.Glyphs);
    }

    [Fact]
    public void The_saved_form_round_trips_through_json()
    {
        var config = new AppearanceConfig { Theme = "aether-crystal", Glyphs = new Dictionary<string, string> { ["ready"] = "ishgard-glass" }, Palette = "night", Frames = "came", HighContrast = true };
        var back = JsonSerializer.Deserialize<AppearanceConfig>(JsonSerializer.Serialize(config));
        Assert.True(config.SameAs(back));
        Assert.False(config.SameAs(new AppearanceConfig()));
        Assert.True(config.SameAs(config.Clone()));
        Assert.NotSame(config.Glyphs, config.Clone().Glyphs);
    }

    // ------------------------------------------------------------------ resolution

    [Fact]
    public void The_default_is_medallion_on_night_with_brass()
    {
        var resolved = AppearanceResolver.Resolve(null);
        Assert.Equal(ThemeId.Medallion, resolved.Theme.Id);
        Assert.Equal(PaletteId.Night, resolved.Palette);
        Assert.Equal(FrameKitId.Brass, resolved.Frames);
        Assert.False(resolved.HighContrast);
        Assert.False(resolved.Classic);
        Assert.False(resolved.IsMixed);
        Assert.All(AppearanceStates.All, state => Assert.Equal(GlyphSetId.Medallion, resolved.SetFor(state)));
        Assert.True(resolved.Uses(GlyphSetId.Medallion));
        Assert.False(resolved.Uses(GlyphSetId.AetherCrystal));
        Assert.Equal(MedalFinish.Gilt, resolved.FinishFor(Flair.Full));
        Assert.Equal(MedalFinish.LightRim, resolved.FinishFor(Flair.Quiet));
        Assert.Equal(MedalFinish.Plain, resolved.FinishFor(Flair.Plain));
    }

    [Fact]
    public void A_mix_takes_each_state_from_its_set()
    {
        var resolved = AppearanceResolver.Resolve(new AppearanceConfig
        {
            Theme = "ishgard-glass",
            Glyphs = new Dictionary<string, string> { ["ready"] = "aether-crystal", ["not-checked"] = "medallion" },
        });

        Assert.True(resolved.IsMixed);
        Assert.Equal(GlyphSetId.AetherCrystal, resolved.SetFor(QuestState.Ready));
        Assert.Equal(GlyphSetId.Medallion, resolved.SetFor(QuestState.Unknown));
        Assert.Equal(GlyphSetId.IshgardGlass, resolved.SetFor(QuestState.Blocked));
        Assert.True(resolved.Uses(GlyphSetId.AetherCrystal) && resolved.Uses(GlyphSetId.IshgardGlass) && resolved.Uses(GlyphSetId.Medallion));
        Assert.Equal(FrameKitId.Came, resolved.Frames);
        Assert.Equal(PaletteId.IshgardSnow, resolved.Palette);
        Assert.Equal(MoonStyle.Medallion, resolved.MoonStyle);
    }

    [Fact]
    public void Classic_is_whole_theme_only()
    {
        var resolved = AppearanceResolver.Resolve(new AppearanceConfig
        {
            Theme = "classic",
            HighContrast = true,
            Glyphs = new Dictionary<string, string> { ["ready"] = "aether-crystal" },
        });

        Assert.True(resolved.Classic);
        Assert.All(AppearanceStates.All, state => Assert.Equal(GlyphSetId.Classic, resolved.SetFor(state)));
        Assert.Equal(MedalFinish.Classic, resolved.FinishFor(Flair.Full));
        Assert.Equal(GlyphPaletteKind.HighContrast, resolved.GlyphPalette);
    }

    [Fact]
    public void Classic_cannot_be_mixed_into_another_theme()
    {
        var resolved = AppearanceResolver.Resolve(new AppearanceConfig { Glyphs = new Dictionary<string, string> { ["blocked"] = "classic" } });
        Assert.Equal(GlyphSetId.Medallion, resolved.SetFor(QuestState.Blocked));
        Assert.False(resolved.IsMixed);
        Assert.Equal(["blocked: classic"], resolved.Unknown);
    }

    [Fact]
    public void High_contrast_draws_the_shared_set_whatever_the_theme_or_mix()
    {
        var config = new AppearanceConfig { Theme = "aether-crystal", HighContrast = true, Glyphs = new Dictionary<string, string> { ["ready"] = "ishgard-glass" } };
        var resolved = AppearanceResolver.Resolve(config);
        Assert.All(AppearanceStates.All, state => Assert.Equal(GlyphSetId.Medallion, resolved.SetFor(state)));
        Assert.Equal(FrameKitId.Silver, resolved.Frames);

        // The mix comes back when high contrast is turned off.
        config.HighContrast = false;
        resolved = AppearanceResolver.Resolve(config);
        Assert.Equal(GlyphSetId.IshgardGlass, resolved.SetFor(QuestState.Ready));
        Assert.Equal(GlyphSetId.AetherCrystal, resolved.SetFor(QuestState.Completed));
    }

    [Fact]
    public void Overrides_win_over_the_theme_and_unknown_or_unoffered_keys_fall_back()
    {
        var resolved = AppearanceResolver.Resolve(new AppearanceConfig { Theme = "aether-crystal", Palette = "ishgard-snow", Frames = "came" });
        Assert.Equal(PaletteId.IshgardSnow, resolved.Palette);
        Assert.Equal(FrameKitId.Came, resolved.Frames);

        resolved = AppearanceResolver.Resolve(new AppearanceConfig
        {
            Theme = "astrologian-orrery",
            Palette = "dawn",
            Frames = "pewter",
            Glyphs = new Dictionary<string, string> { ["ready"] = "sumi-to-kinpaku" },
        });

        Assert.Equal(ThemeId.Medallion, resolved.Theme.Id);
        Assert.Equal(PaletteId.Night, resolved.Palette);
        Assert.Equal(FrameKitId.Brass, resolved.Frames);
        Assert.Equal(GlyphSetId.Medallion, resolved.SetFor(QuestState.Ready));
        Assert.Equal(["theme: astrologian-orrery", "ready: sumi-to-kinpaku", "palette: dawn", "frames: pewter"], resolved.Unknown);
    }

    [Fact]
    public void The_cache_resolves_once_per_change_and_allocates_nothing_when_unchanged()
    {
        var cache = new AppearanceCache();
        var config = new AppearanceConfig { Theme = "ishgard-glass", Glyphs = new Dictionary<string, string> { ["ready"] = "aether-crystal" } };
        var first = cache.Get(config);
        Assert.Same(first, cache.Get(config));
        Assert.Equal(1, cache.Resolutions);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            cache.Get(config);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());

        config.Glyphs["ready"] = "medallion";
        var second = cache.Get(config);
        Assert.NotSame(first, second);
        Assert.Equal(GlyphSetId.Medallion, second.SetFor(QuestState.Ready));
        Assert.Equal(2, cache.Resolutions);

        // Editing the saved object in place is seen: the cache keeps a copy, not the reference.
        config.HighContrast = true;
        Assert.True(cache.Get(config).HighContrast);
    }

    // ------------------------------------------------------------------ edits

    [Fact]
    public void Moon_style_edits_switch_classic_and_keep_a_new_theme()
    {
        var config = new AppearanceConfig { Theme = "ishgard-glass" };
        AppearanceEdits.SetMoonStyle(config, MoonStyle.Medallion);
        Assert.Equal("ishgard-glass", config.Theme);

        AppearanceEdits.SetMoonStyle(config, MoonStyle.Classic);
        Assert.Equal("classic", config.Theme);

        AppearanceEdits.SetMoonStyle(config, MoonStyle.Medallion);
        Assert.Equal("medallion", config.Theme);
    }

    [Fact]
    public void Follow_dalamud_edits_only_undo_their_own_palette()
    {
        var config = new AppearanceConfig { Palette = "ishgard-snow" };
        AppearanceEdits.SetFollowDalamud(config, false);
        Assert.Equal("ishgard-snow", config.Palette);

        AppearanceEdits.SetFollowDalamud(config, true);
        Assert.Equal("dalamud", config.Palette);

        AppearanceEdits.SetFollowDalamud(config, false);
        Assert.Null(config.Palette);

        AppearanceEdits.SetHighContrast(config, true);
        Assert.True(config.HighContrast);
    }

    [Fact]
    public void Applying_a_theme_clears_the_overrides_but_not_high_contrast()
    {
        var config = new AppearanceConfig { Glyphs = new Dictionary<string, string> { ["ready"] = "aether-crystal" }, Palette = "dalamud", Frames = "silver", HighContrast = true };
        Assert.True(AppearanceEdits.IsCustom(config));

        AppearanceEdits.ApplyTheme(config, ThemePresets.IshgardGlass);
        Assert.Equal("ishgard-glass", config.Theme);
        Assert.False(AppearanceEdits.IsCustom(config));
        Assert.True(config.HighContrast);
    }
}
