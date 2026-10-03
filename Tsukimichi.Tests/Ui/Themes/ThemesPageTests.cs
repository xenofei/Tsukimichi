using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Settings › Themes (plan v7 T9; spec-1.16 §B): what the page offers (cards, palette tiles, the frames choice), the card
/// grid's columns, the edits it makes and their Undo, and what the live preview draws.
/// </summary>
public sealed class ThemesPageTests
{
    private static bool OnlyNight(PaletteId id) => id == PaletteId.Night;

    private static bool NightAndSnow(PaletteId id) => id is PaletteId.Night or PaletteId.IshgardSnow;

    // ------------------------------------------------------------------ what the page offers

    [Fact]
    public void The_cards_are_the_offered_themes_with_Classic_last()
    {
        // The approved Themes design's order: Medallion, Ishgard Glass second, Aether Crystal, then 1.17's Orrery and Sumi to
        // Kinpaku, and Classic last.
        Assert.Equal(
            [ThemeId.Medallion, ThemeId.IshgardGlass, ThemeId.AetherCrystal, ThemeId.Orrery, ThemeId.Sumi, ThemeId.Classic],
            ThemesPage.Themes.Select(static t => t.Id));
        Assert.True(ThemesPage.Themes[^1].Legacy);

        // Sumi to Kinpaku comes as designed: its crests in the Kirikane kit on Kugane Lacquer.
        var sumi = ThemePresets.Get(ThemeId.Sumi);
        Assert.Equal((GlyphSetId.Sumi, FrameKitId.Kirikane, PaletteId.KuganeLacquer), (sumi.Glyphs, sumi.Frames, sumi.Palette));
        Assert.Equal((FrameKitId.Kirikane, PaletteId.KuganeLacquer), (GlyphSets.Sumi.DefaultFrames, GlyphSets.Sumi.DefaultPalette));
    }

    [Fact]
    public void A_palette_tile_shows_once_its_palette_is_registered_and_Follow_Dalamud_always()
    {
        Assert.Equal([PaletteId.Night, PaletteId.FollowDalamud], ThemesPage.Palettes(OnlyNight).Select(static p => p.Id));
        Assert.Equal([PaletteId.Night, PaletteId.IshgardSnow, PaletteId.FollowDalamud], ThemesPage.Palettes(NightAndSnow).Select(static p => p.Id));

        // 1.17 (T16): Dawn and Kugane Lacquer join the row once registered, after Ishgard Snow (spec-1.16 §B6's order).
        Assert.Equal(
            [PaletteId.Night, PaletteId.IshgardSnow, PaletteId.Dawn, PaletteId.KuganeLacquer, PaletteId.FollowDalamud],
            ThemesPage.Palettes(static _ => true).Select(static p => p.Id));
        Assert.Equal(
            [PaletteId.Night, PaletteId.IshgardSnow, PaletteId.Dawn, PaletteId.KuganeLacquer, PaletteId.FollowDalamud],
            ThemesPage.Palettes(UiPalettes.IsRegistered).Select(static p => p.Id));
    }

    [Fact]
    public void A_palette_that_cannot_draw_yet_draws_as_Night()
    {
        Assert.Equal(PaletteId.Night, ThemesPage.Drawable(PaletteId.IshgardSnow, OnlyNight));
        Assert.Equal(PaletteId.IshgardSnow, ThemesPage.Drawable(PaletteId.IshgardSnow, NightAndSnow));
        Assert.Equal(PaletteId.FollowDalamud, ThemesPage.Drawable(PaletteId.FollowDalamud, OnlyNight));
    }

    [Fact]
    public void Frames_are_a_choice_only_once_two_kits_have_a_metal_of_their_own()
    {
        Assert.False(ThemesPage.FramesChoosable(static id => id == FrameKitId.Brass));
        Assert.True(ThemesPage.FramesChoosable(static id => id is FrameKitId.Brass or FrameKitId.Silver));

        // A kit that is not offered does not count.
        Assert.False(ThemesPage.FramesChoosable(static id => id is FrameKitId.Brass or (FrameKitId)7));

        // 1.17 T11 and T15: Brass, Silver, Lead came, Astrolabe and Kirikane each draw their own metal, so the row is a choice.
        Assert.True(ThemesPage.FramesChoosable(FrameKitMetals.HasOwnMetal));
        Assert.Equal([FrameKitId.Brass, FrameKitId.Silver, FrameKitId.Came, FrameKitId.Astrolabe, FrameKitId.Kirikane], ThemesPage.Kits.Select(static k => k.Id));
        Assert.All(ThemesPage.Kits, static k => Assert.True(FrameKitMetals.HasOwnMetal(k.Id), k.Key));
    }

    [Fact]
    public void The_frames_choice_is_From_theme_then_the_offered_kits()
    {
        Assert.Equal([FrameKitId.Brass, FrameKitId.Silver, FrameKitId.Came, FrameKitId.Astrolabe, FrameKitId.Kirikane], ThemesPage.Kits.Select(static k => k.Id));
        Assert.Null(ThemesPage.KitAt(0));
        Assert.Null(ThemesPage.KitAt(ThemesPage.Kits.Count + 1));

        var config = new AppearanceConfig();
        Assert.Equal(0, ThemesPage.FramesIndex(config));
        for (var i = 1; i <= ThemesPage.Kits.Count; i++)
        {
            config.Frames = ThemesPage.KitAt(i)!.Key;
            Assert.Equal(i, ThemesPage.FramesIndex(config));
        }

        // Kirikane joins with Sumi to Kinpaku (1.17 T15), last.
        config.Frames = "kirikane";
        Assert.Equal(5, ThemesPage.FramesIndex(config));
        config.Frames = "from-a-newer-build";
        Assert.Equal(0, ThemesPage.FramesIndex(config));
    }

    [Theory]
    [InlineData(660f, 3)]
    [InlineData(646f, 3)]
    [InlineData(645f, 2)]
    [InlineData(426f, 2)]
    [InlineData(425f, 1)]
    [InlineData(100f, 1)]
    [InlineData(5000f, 3)]
    [InlineData(float.NaN, 1)]
    public void The_grid_is_three_wide_and_wraps_by_width_only(float width, int columns)
    {
        Assert.Equal(columns, ThemesPage.Columns(width, 206f, 14f));
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 1)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 2)]
    [InlineData(4, 1, 4)]
    [InlineData(4, 0, 4)]
    public void Rows_hold_every_card(int count, int columns, int rows)
    {
        Assert.Equal(rows, ThemesPage.Rows(count, columns));
    }

    // ------------------------------------------------------------------ edits and Undo

    [Fact]
    public void Picking_a_theme_brings_its_palette_and_frames_and_keeps_high_contrast()
    {
        var saved = new AppearanceConfig { Palette = "dalamud", Frames = "silver", HighContrast = true };
        var what = ThemesPage.WhatIf(saved, ThemePresets.IshgardGlass);

        Assert.Equal("dalamud", saved.Palette);
        Assert.Equal(ThemePresets.Default.Key, saved.Theme);
        Assert.Equal(ThemePresets.IshgardGlass.Key, what.Theme);
        Assert.Null(what.Palette);
        Assert.Null(what.Frames);
        Assert.True(what.HighContrast);

        var resolved = AppearanceResolver.Resolve(what);
        Assert.Equal(PaletteId.IshgardSnow, resolved.Palette);
        Assert.Equal(FrameKitId.Came, resolved.Frames);
    }

    [Fact]
    public void A_palette_picked_by_hand_is_an_override_until_the_theme_s_own_is_picked()
    {
        var config = new AppearanceConfig();
        AppearanceEdits.SetPalette(config, PaletteChoices.FollowDalamud);
        Assert.Equal("dalamud", config.Palette);
        Assert.True(AppearanceEdits.IsCustom(config));

        AppearanceEdits.SetPalette(config, PaletteChoices.Night);
        Assert.Null(config.Palette);
        Assert.False(AppearanceEdits.IsCustom(config));

        // Ishgard Glass's own palette is Snow, so Night on it is an override.
        AppearanceEdits.ApplyTheme(config, ThemePresets.IshgardGlass);
        AppearanceEdits.SetPalette(config, PaletteChoices.Night);
        Assert.Equal("night", config.Palette);
        Assert.Equal(PaletteId.Night, AppearanceResolver.Resolve(config).Palette);

        AppearanceEdits.SetPalette(config, null);
        Assert.Null(config.Palette);
    }

    [Fact]
    public void Frames_picked_by_hand_are_an_override_until_From_theme()
    {
        var config = new AppearanceConfig();
        AppearanceEdits.SetFrames(config, FrameKits.Came);
        Assert.Equal("came", config.Frames);
        Assert.Equal(FrameKitId.Came, AppearanceResolver.Resolve(config).Frames);

        AppearanceEdits.SetFrames(config, FrameKits.Brass);
        Assert.Null(config.Frames);

        AppearanceEdits.SetFrames(config, FrameKits.Silver);
        AppearanceEdits.SetFrames(config, null);
        Assert.Null(config.Frames);
    }

    [Fact]
    public void Reset_returns_to_Medallion_on_Night_with_Brass_and_standard_contrast()
    {
        var config = new AppearanceConfig
        {
            Theme = ThemePresets.AetherCrystal.Key,
            Palette = "dalamud",
            Frames = "came",
            HighContrast = true,
            Glyphs = new Dictionary<string, string>(StringComparer.Ordinal) { ["ready"] = "ishgard-glass" },
        };
        Assert.False(AppearanceEdits.IsDefault(config));

        AppearanceEdits.Reset(config);

        Assert.True(AppearanceEdits.IsDefault(config));
        Assert.True(config.SameAs(new AppearanceConfig()));
        var resolved = AppearanceResolver.Resolve(config);
        Assert.Equal(ThemeId.Medallion, resolved.Theme.Id);
        Assert.Equal(PaletteId.Night, resolved.Palette);
        Assert.Equal(FrameKitId.Brass, resolved.Frames);
        Assert.False(resolved.HighContrast);
    }

    [Theory]
    [InlineData("medallion", null, false, true)]
    [InlineData("classic", null, false, false)]
    [InlineData("medallion", "dalamud", false, false)]
    [InlineData("medallion", null, true, false)]
    [InlineData("from-a-newer-build", null, false, true)]
    public void Reset_has_nothing_to_do_only_on_the_default_look(string theme, string? palette, bool highContrast, bool isDefault)
    {
        var config = new AppearanceConfig { Theme = theme, Palette = palette, HighContrast = highContrast };
        Assert.Equal(isDefault, AppearanceEdits.IsDefault(config));

        // The per-frame form, given the appearance already resolved, agrees.
        Assert.Equal(isDefault, AppearanceEdits.IsDefault(config, AppearanceResolver.Resolve(config)));
    }

    [Fact]
    public void Undo_puts_back_exactly_what_a_change_discarded()
    {
        var saved = new AppearanceConfig { Theme = ThemePresets.IshgardGlass.Key, Palette = "dalamud", HighContrast = true };
        var before = saved.Clone();

        AppearanceEdits.Reset(saved);
        Assert.False(saved.SameAs(before));

        var restored = before.Clone();
        Assert.True(restored.SameAs(new AppearanceConfig { Theme = ThemePresets.IshgardGlass.Key, Palette = "dalamud", HighContrast = true }));
    }

    [Fact]
    public void Reset_appearance_is_one_click_with_undo()
    {
        Assert.Equal(SafetyTier.None, SafetyRules.TierOf(GuardedAction.ResetAppearance));
        Assert.True(SafetyRules.OffersUndo(GuardedAction.ResetAppearance));
    }

    // ------------------------------------------------------------------ the preview

    [Fact]
    public void Each_card_is_resolved_once_and_again_only_when_high_contrast_changes()
    {
        var preview = new ThemesPreview();
        var saved = new AppearanceConfig();

        var glass = preview.Card(saved, ThemePresets.IshgardGlass);
        Assert.Same(glass, preview.Card(saved, ThemePresets.IshgardGlass));
        Assert.Equal(1, preview.Resolutions);
        Assert.All(AppearanceStates.All, s => Assert.Equal(GlyphSetId.IshgardGlass, glass.SetFor(s)));

        // Palette or frames picked by hand do not change a card: a click clears them.
        saved.Palette = "dalamud";
        Assert.Same(glass, preview.Card(saved, ThemePresets.IshgardGlass));

        saved.HighContrast = true;
        var shared = preview.Card(saved, ThemePresets.IshgardGlass);
        Assert.NotSame(glass, shared);
        Assert.True(shared.HighContrast);
        Assert.All(AppearanceStates.All, s => Assert.Equal(GlyphSetId.Medallion, shared.SetFor(s)));
        Assert.Equal(2, preview.Resolutions);
    }

    [Fact]
    public void Classic_s_card_keeps_its_moons_under_high_contrast()
    {
        var card = new ThemesPreview().Card(new AppearanceConfig { HighContrast = true }, ThemePresets.Classic);
        Assert.True(card.Classic);
        Assert.Equal(GlyphSetId.Classic, card.SetFor(QuestState.Ready));
    }

    [Fact]
    public void The_preview_shows_the_saved_look_until_a_different_card_is_hovered()
    {
        var preview = new ThemesPreview();
        var saved = new AppearanceConfig();
        var resolved = AppearanceResolver.Resolve(saved);

        var idle = preview.Target(saved, resolved, null);
        Assert.False(idle.Previewing);
        Assert.Same(resolved, idle.Appearance);
        Assert.Equal(ThemeId.Medallion, idle.Theme.Id);

        // Hovering the theme in use changes nothing, so nothing is previewed.
        var same = preview.Target(saved, resolved, ThemePresets.Medallion);
        Assert.False(same.Previewing);
        Assert.Same(resolved, same.Appearance);

        var glass = preview.Target(saved, resolved, ThemePresets.IshgardGlass);
        Assert.True(glass.Previewing);
        Assert.Equal(ThemeId.IshgardGlass, glass.Theme.Id);
        Assert.Equal(GlyphSetId.IshgardGlass, glass.Appearance.SetFor(QuestState.Accepted));
        Assert.Same(preview.Card(saved, ThemePresets.IshgardGlass), glass.Appearance);

        // The saved look is untouched by hovering.
        Assert.True(saved.SameAs(new AppearanceConfig()));
    }

    [Fact]
    public void Hovering_the_theme_in_use_previews_it_when_its_palette_was_picked_by_hand()
    {
        var preview = new ThemesPreview();
        var saved = new AppearanceConfig { Palette = "dalamud" };
        var resolved = AppearanceResolver.Resolve(saved);

        var target = preview.Target(saved, resolved, ThemePresets.Medallion);
        Assert.True(target.Previewing);
        Assert.Equal(PaletteId.Night, target.Appearance.Palette);
    }
}
