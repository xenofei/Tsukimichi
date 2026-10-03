using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The appearance registries (feature plan v7 T1; theme-system §3.2, §7.2): the id ↔ key tables are pinned because share
/// codes (1.17 T12) store the numbers and the configuration stores the keys, and every preset resolves to itself.
/// </summary>
public sealed class ThemeRegistryTests
{
    [Fact]
    public void Glyph_set_ids_and_keys_are_pinned()
    {
        Assert.Equal(
            [(1, "medallion"), (2, "classic"), (3, "aether-crystal"), (4, "ishgard-glass"), (5, "astrologian-orrery"), (6, "sumi-to-kinpaku")],
            GlyphSets.All.Select(static s => ((int)s.Id, s.Key)).ToArray());
    }

    [Fact]
    public void Frame_kit_ids_and_keys_are_pinned()
    {
        Assert.Equal(
            [(1, "brass"), (2, "silver"), (3, "came"), (4, "astrolabe"), (5, "kirikane")],
            FrameKits.All.Select(static k => ((int)k.Id, k.Key)).ToArray());
    }

    [Fact]
    public void Palette_ids_and_keys_are_pinned()
    {
        Assert.Equal(
            [(1, "night"), (2, "dawn"), (3, "ishgard-snow"), (4, "kugane-lacquer"), (5, "dalamud")],
            PaletteChoices.All.Select(static p => ((int)p.Id, p.Key)).ToArray());
    }

    [Fact]
    public void Theme_ids_and_keys_are_pinned()
    {
        Assert.Equal(
            [(1, "medallion"), (3, "aether-crystal"), (4, "ishgard-glass"), (5, "astrologian-orrery"), (6, "sumi-to-kinpaku"), (2, "classic")],
            ThemePresets.All.Select(static t => ((int)t.Id, t.Key)).ToArray());
    }

    [Fact]
    public void Ids_fit_a_share_code_nibble_and_zero_means_from_theme()
    {
        foreach (var id in Enum.GetValues<GlyphSetId>().Cast<int>()
                     .Concat(Enum.GetValues<FrameKitId>().Cast<int>())
                     .Concat(Enum.GetValues<PaletteId>().Cast<int>())
                     .Concat(Enum.GetValues<ThemeId>().Cast<int>()))
        {
            Assert.InRange(id, 1, 15);
        }
    }

    [Fact]
    public void Every_enum_value_is_registered_once()
    {
        Assert.Equal(Enum.GetValues<GlyphSetId>(), GlyphSets.All.Select(static s => s.Id).Order().ToArray());
        Assert.Equal(Enum.GetValues<FrameKitId>(), FrameKits.All.Select(static k => k.Id).Order().ToArray());
        Assert.Equal(Enum.GetValues<PaletteId>(), PaletteChoices.All.Select(static p => p.Id).Order().ToArray());
        Assert.Equal(Enum.GetValues<ThemeId>(), ThemePresets.All.Select(static t => t.Id).Order().ToArray());
    }

    [Fact]
    public void Lookups_are_case_insensitive_and_reject_the_unknown()
    {
        Assert.True(GlyphSets.TryGet(" Ishgard-Glass ", out var set));
        Assert.Equal(GlyphSetId.IshgardGlass, set.Id);
        Assert.False(GlyphSets.TryGet("ishgard glass", out _));
        Assert.False(GlyphSets.TryGet(null, out _));
        Assert.False(ThemePresets.TryGet("", out _));
        Assert.Equal(GlyphSets.Medallion, GlyphSets.Get((GlyphSetId)99));
        Assert.Equal(ThemePresets.Default, ThemePresets.Get((ThemeId)0));
    }

    [Fact]
    public void Every_offered_theme_resolves_to_its_own_axes()
    {
        foreach (var theme in ThemePresets.All.Where(static t => t.Offered))
        {
            var resolved = AppearanceResolver.Resolve(new AppearanceConfig { Theme = theme.Key });
            Assert.Same(theme, resolved.Theme);
            Assert.Empty(resolved.Unknown);
            Assert.False(resolved.IsMixed);
            Assert.Equal(theme.Frames, resolved.Frames);
            Assert.Equal(theme.Palette, resolved.Palette);
            Assert.All(Core.Ui.Themes.AppearanceStates.All, state => Assert.Equal(theme.Glyphs, resolved.SetFor(state)));
            Assert.True(GlyphSets.Get(theme.Glyphs).Offered, theme.Key);
            Assert.True(FrameKits.Get(theme.Frames).Offered, theme.Key);
            Assert.True(PaletteChoices.Get(theme.Palette).Offered, theme.Key);
        }
    }

    [Fact]
    public void Every_set_has_a_theme_of_its_own_and_only_classic_is_unmixable_and_legacy()
    {
        foreach (var set in GlyphSets.All)
        {
            Assert.Single(ThemePresets.All, t => t.Glyphs == set.Id);
            Assert.Equal(set.Id != GlyphSetId.Classic, set.Mixable);
        }

        Assert.Equal([ThemeId.Classic], ThemePresets.All.Where(static t => t.Legacy).Select(static t => t.Id));
        Assert.Equal(ThemeId.Medallion, ThemePresets.Default.Id);
    }

    [Fact]
    public void The_1_16_offer_is_medallion_classic_aether_crystal_and_ishgard_glass()
    {
        Assert.Equal(
            [GlyphSetId.Medallion, GlyphSetId.Classic, GlyphSetId.AetherCrystal, GlyphSetId.IshgardGlass],
            GlyphSets.All.Where(static s => s.Offered).Select(static s => s.Id));
        Assert.Equal([PaletteId.Night, PaletteId.IshgardSnow, PaletteId.FollowDalamud], PaletteChoices.All.Where(static p => p.Offered).Select(static p => p.Id));
        Assert.Equal(GlyphRenderKind.Procedural, GlyphSets.Medallion.Kind);
        Assert.Equal(GlyphRenderKind.Procedural, GlyphSets.Classic.Kind);
    }

    [Fact]
    public void State_keys_are_the_atlas_sprite_stems_and_round_trip()
    {
        Assert.Equal(
            ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"],
            AppearanceStates.All.Select(AppearanceStates.Key));
        foreach (var state in AppearanceStates.All)
        {
            Assert.Equal((int)state, AppearanceStates.Index(state));
            Assert.True(AppearanceStates.TryParse(AppearanceStates.Key(state).ToUpperInvariant(), out var parsed));
            Assert.Equal(state, parsed);
        }

        Assert.False(AppearanceStates.TryParse("accepted", out _));
        Assert.Equal(AppearanceStates.Count, Enum.GetValues<Core.Model.QuestState>().Length);
    }
}
