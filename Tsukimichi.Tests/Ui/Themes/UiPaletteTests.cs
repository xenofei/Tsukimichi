using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The palette record and registry (plan v7 T2, theme-system §8): Night is the 1.15 colours exactly, role by role, so
/// moving the chrome onto the palette changed nothing on screen; its high-contrast form is 1.15's; the registry follows
/// the appearance's <see cref="PaletteId"/>; Follow Dalamud is derived from the host style; a light host switches the
/// scene's dark-background tokens.
/// </summary>
public sealed class UiPaletteTests
{
    private static readonly UiPalette Night = UiPalettes.Night;

    private static void Hex(uint expected, Vector4 actual, string role) =>
        Assert.True(ColorMath.ToHex(actual) == expected && actual.W == 1f, $"{role}: expected #{expected:X6}, got #{ColorMath.ToHex(actual):X6} (alpha {actual.W})");

    // ---- Night is 1.15's colours (the zero-visual-change port), but for the two spec-1.16 §A2 contrast fixes:
    // TextTertiary #7C86A8 → #8B94B3 and StrongLine #5C6584 → #646D8A.

    [Theory]
    [InlineData("Window", 0x0F1424u)]
    [InlineData("Sunken", 0x0B0F1Cu)]
    [InlineData("Raised", 0x1E2437u)]
    [InlineData("Hover", 0x262D45u)]
    [InlineData("Line", 0x2A3149u)]
    [InlineData("StrongLine", 0x646D8Au)]
    [InlineData("Text", 0xDDE3F0u)]
    [InlineData("TextSecondary", 0xA9B2CCu)]
    [InlineData("TextTertiary", 0x8B94B3u)]
    [InlineData("TextDisabled", 0x4A5270u)]
    [InlineData("Deep", 0x080B16u)]
    [InlineData("Top", 0x151C33u)]
    [InlineData("Ornament", 0xA88B52u)]
    [InlineData("OrnamentHigh", 0xD9BE82u)]
    [InlineData("Cool", 0x6F8FD0u)]
    [InlineData("CoolDeep", 0x24345Cu)]
    public void Night_surface_roles_are_the_1_15_hexes_with_the_1_16_contrast_fixes(string role, uint hex)
    {
        var s = Night.Surface;
        var color = role switch
        {
            "Window" => s.Window,
            "Sunken" => s.Sunken,
            "Raised" => s.Raised,
            "Hover" => s.Hover,
            "Line" => s.Line,
            "StrongLine" => s.StrongLine,
            "Text" => s.Text,
            "TextSecondary" => s.TextSecondary,
            "TextTertiary" => s.TextTertiary,
            "TextDisabled" => s.TextDisabled,
            "Deep" => s.Deep,
            "Top" => s.Top,
            "Ornament" => s.Ornament,
            "OrnamentHigh" => s.OrnamentHigh,
            "Cool" => s.Cool,
            _ => s.CoolDeep,
        };
        Hex(hex, color, role);
        Assert.False(Night.IsLight);
    }

    [Fact]
    public void Night_text_and_ink_roles_are_the_1_15_tokens()
    {
        Assert.Equal(GlyphTokens.Moon, Night.Accent);
        Assert.Equal(GlyphTokens.MoonDim, Night.AccentDim);
        Hex(0xE6CF98, Night.OrnamentLight, "OrnamentLight");

        var i = Night.Inks;
        Hex(0xF2D27A, i.Gold, "Gold");
        Hex(0xFFF0BE, i.GoldHigh, "GoldHigh");
        Hex(0xD6B25A, i.GoldDeep, "GoldDeep");
        Assert.Equal(GlyphTokens.MoonDim, i.GoldDim);
        Hex(0xF2D27A, i.GoldLine, "GoldLine");
        Hex(0x0F1424, i.OnGold, "OnGold");
        Hex(0xB25C7F, i.Danger, "Danger");
        Hex(0xD68AA8, i.DangerText, "DangerText");
        Hex(0xDDE3F0, i.OnDanger, "OnDanger");
        Hex(0x8A93B0, i.UnknownText, "UnknownText");
        Hex(0xCDB57A, i.GaugeArc, "GaugeArc");
        Hex(0xB8933F, i.GaugeDone, "GaugeDone");
        Hex(0xF2D27A, i.ToggleOn, "ToggleOn");
        Hex(0xFFF0BE, i.ToggleKnob, "ToggleKnob");
    }

    [Theory]
    [InlineData(QuestState.Completed, 0xF2D27Au)]
    [InlineData(QuestState.Accepted, 0xF2D27Au)]
    [InlineData(QuestState.Ready, 0xF2D27Au)]
    [InlineData(QuestState.ReadyOnOtherJob, 0xDDE3F0u)]
    [InlineData(QuestState.DoneThisCycle, 0xDDE3F0u)]
    [InlineData(QuestState.Blocked, 0x7C86A8u)]
    [InlineData(QuestState.Foreclosed, 0xB25C7Fu)]
    [InlineData(QuestState.Unknown, 0x4A5270u)]
    public void Night_state_tones_are_the_1_15_StateColor(QuestState state, uint hex) =>
        Hex(hex, Night.States.Tone(state), $"{state} tone");

    [Fact]
    public void Night_state_stripes_are_the_1_15_table_stripes()
    {
        var s = Night.States;
        Assert.Equal(GlyphTokens.Moon, s.Stripe(QuestState.Ready));
        Assert.Equal(GlyphTokens.Moon, s.Stripe(QuestState.Accepted));
        Assert.Equal(GlyphTokens.MoonDim, s.Stripe(QuestState.Completed));
        Assert.Equal(GlyphTokens.Silver, s.Stripe(QuestState.ReadyOnOtherJob));
        Assert.Equal(GlyphTokens.Silver, s.Stripe(QuestState.DoneThisCycle));
        Assert.Equal(GlyphTokens.Eclipse, s.Stripe(QuestState.Foreclosed));
        Assert.Equal(GlyphTokens.VeilText, s.Stripe(QuestState.Blocked));
        Assert.Equal(GlyphTokens.VeilText, s.Stripe(QuestState.Unknown));
        Assert.Equal(GlyphTokens.VeilText, s.Stripe((QuestState)200));
    }

    [Fact]
    public void Night_status_words_are_the_spec_inks()
    {
        // spec-1.16 §A2: gold words in Moon, the silver states in Silver, Blocked in Mist, Locked out and Not checked in their text tones.
        var s = Night.States;
        Hex(0xF2D27A, s.Text(QuestState.Ready), "Ready word");
        Hex(0xF2D27A, s.Text(QuestState.Completed), "Completed word");
        Hex(0xDDE3F0, s.Text(QuestState.DoneThisCycle), "Done word");
        Hex(0xA9B2CC, s.Text(QuestState.Blocked), "Blocked word");
        Hex(0xD68AA8, s.Text(QuestState.Foreclosed), "Locked out word");
        Hex(0x8A93B0, s.Text(QuestState.Unknown), "Not checked word");
    }

    [Fact]
    public void Night_scene_is_the_1_15_sky_stars_shadows_and_grades()
    {
        var c = Night.Scene;
        Hex(0x1B2552, c.Zenith, "Zenith");
        Assert.Null(c.SkyStops);
        Hex(0x0E1329, c.StatusTop, "StatusTop");
        Hex(0x0A0E1C, c.StatusFoot, "StatusFoot");
        Hex(0x080B16, c.Shadow, "Shadow");
        Hex(0x000000, c.ShadowInk, "ShadowInk");
        Assert.Equal(1f, c.ShadowStrength);
        Assert.Equal(1f, c.GlowStrength);
        Assert.False(c.WashInsteadOfGlow);
        Hex(0xFFF0BE, c.TopHighlight, "TopHighlight");
        Hex(0x080B16, c.TextHalo, "TextHalo");
        Hex(0x0F1424, c.Scrim, "Scrim");
        Hex(0xFFF0BE, c.Moonlight, "Moonlight");
        Hex(0xF4F1E8, c.BannerTitle, "BannerTitle");
        Assert.True(c.StarField);
        Assert.False(c.MorningStar);
        Assert.True(c.NightGrade);
        Hex(0xDCE5FF, c.Stars.Cool, "Cool star");
        Hex(0xF4F2EA, c.Stars.MoonWhite, "Moon-white star");
        Hex(0xFFE2A8, c.Stars.Gold, "Gold star");
        Hex(0xFFC9AE, c.Stars.Ember, "Ember star");
        Hex(0xEEF1FA, c.Stars.Figure, "Figure star");
        Hex(0xC9D3F0, c.Stars.Band, "Milky Way");
    }

    [Fact]
    public void Night_brass_plate_and_pills_are_the_1_15_hexes()
    {
        var b = Night.Brass;
        Hex(0xE2C78C, b.High, "BrassHigh");
        Hex(0xA88B52, b.Body, "Brass body (Gilt)");
        Hex(0x6E5732, b.Shadow, "BrassShadow");
        Hex(0x9C8049, b.Reflected, "BrassReflected");
        Hex(0x5A4729, b.Deep, "BrassDeep");
        Hex(0xF0D9A0, b.CornerLit, "CornerLit");
        Hex(0xB79755, b.CornerShaded, "CornerShaded");
        Hex(0xE2C78C, b.At(0f), "Brass at 0");
        Hex(0xA88B52, b.At(0.28f), "Brass at 28 %");
        Hex(0x5A4729, b.At(1f), "Brass at 1");

        var p = Night.Plate;
        Hex(0x1D2B5A, p.WellTop, "WellTop");
        Hex(0x131C40, p.WellFoot, "WellFoot");
        Hex(0x1C2237, p.PlainWell, "PlainWell");
        Hex(0x3A4050, p.PlainKeyline, "PlainKeyline");
        Hex(0xC3CBDF, p.QuietKeyline, "QuietKeyline");
        Hex(0xE9E4D2, p.Initials, "Initials");
        Hex(0x080B16, p.OuterRing, "OuterRing");
        Hex(0xE6CF98, p.KeylineAt(0f), "Keyline lit");
        Hex(0x9A7E4A, p.KeylineAt(0.45f), "Keyline mid");
        Hex(0x5C4724, p.KeylineAt(1f), "Keyline dark");

        var pills = Assert.NotNull(Night.Pills);
        Hex(0x252B45, pills.RaisedTop, "RaisedTop");
        Hex(0x1C2138, pills.RaisedFoot, "RaisedFoot");
        Hex(0xFFE6A3, pills.GoldTop, "GoldTop");
        Hex(0xD9B65F, pills.GoldFoot, "GoldFoot");
        Hex(0xF6DFA0, pills.GoldEdge, "GoldEdge");
        Hex(0x1A1406, pills.GoldInk, "GoldInk");
    }

    [Fact]
    public void Night_designed_tones_are_flair_v13s()
    {
        Assert.Equal(FlairTones.NightQuiet, FlairTones.For(Flair.Quiet, Night));
        Assert.Equal(FlairTones.NightPlain, FlairTones.For(Flair.Plain, Night));
        Hex(FlairTones.QuietTreeHex, FlairTones.NightQuiet.Tree, "Quiet tree");
        Hex(FlairTones.PlainWindowHex, FlairTones.NightPlain.Table, "Plain table");
        Assert.Equal(FlairTones.For(Flair.Full, SurfaceColors.Night), FlairTones.For(Flair.Full, Night));
        foreach (var flair in new[] { Flair.Full, Flair.Quiet, Flair.Plain })
        {
            Assert.Equal(DrawerTones.For(flair, SurfaceColors.Night), DrawerTones.For(flair, Night));
        }
    }

    // ---- The high-contrast form.

    [Fact]
    public void Night_high_contrast_is_the_1_15_form()
    {
        var hc = Night.HighContrast;
        Assert.True(hc.IsHighContrast);
        Assert.Same(hc, Night.HighContrast);
        Assert.Same(hc, hc.HighContrast);
        Assert.Equal(SurfaceColors.Night.ForHighContrast(), hc.Surface);
        Assert.Equal(Night.Accent, hc.Accent);
        Assert.Equal(Night.States.Text(QuestState.Foreclosed), hc.States.Text(QuestState.Foreclosed));
        Assert.Null(hc.QuietTones);
        Assert.Null(hc.PlainTones);
        Assert.Null(hc.DrawerTones);
        Assert.Equal(FlairTones.For(Flair.Quiet, SurfaceColors.Night.ForHighContrast()), FlairTones.For(Flair.Quiet, hc));
        Assert.Equal(hc.Surface.Window, hc.Scene.Zenith);
        Assert.Equal(Night.Scene.StatusTop, hc.Scene.StatusTop);
    }

    [Fact]
    public void The_generic_high_contrast_form_pushes_every_ink_to_seven_to_one()
    {
        var dim = Night with { Accent = ColorMath.FromHex(0x8A7A40), HighContrastBuilder = null };
        var hc = dim.ToHighContrast(pushInks: true);
        var window = hc.Surface.Window;
        Assert.True(ColorMath.Contrast(hc.Accent, window) >= ColorMath.AaaText);
        Assert.True(ColorMath.Contrast(hc.Inks.DangerText, window) >= ColorMath.AaaText);
        foreach (var state in StateInks.Order)
        {
            Assert.True(ColorMath.Contrast(hc.States.Text(state), window) >= ColorMath.AaaText, $"{state} word");
            Assert.True(ColorMath.Contrast(hc.States.Stripe(state), window) >= ColorMath.AaNonText, $"{state} stripe");
            Assert.Equal(dim.States.Tone(state), hc.States.Tone(state));
        }

        Assert.Null(hc.Scene.SkyStops);
    }

    // ---- The registry follows the appearance.

    [Fact]
    public void The_registry_reads_the_appearance_palette_id()
    {
        Assert.Same(Night, UiPalettes.Get(PaletteId.Night));
        Assert.Equal(PaletteChoices.Night.Key, Night.Key);
        Assert.True(UiPalettes.IsRegistered(PaletteId.Night));
        Assert.Contains(Night, UiPalettes.All);

        // Not designed yet (T8, 1.17): Night until they are; Follow Dalamud is built from the host instead.
        Assert.Same(Night, UiPalettes.Get(PaletteId.IshgardSnow));
        Assert.Same(Night, UiPalettes.Get(PaletteId.FollowDalamud));
        Assert.Same(Night, UiPalettes.Get((PaletteId)99));
        foreach (var palette in UiPalettes.All)
        {
            Assert.True(PaletteChoices.TryGet(palette.Key, out var info) && info.Id == palette.Id, palette.Key);
        }
    }

    // ---- Follow Dalamud, the hook.

    private static UiPalette Host(bool light) => UiPalettes.FollowDalamud(
        ColorMath.FromHex(light ? 0xF0F0F0u : 0x202020u),
        ColorMath.FromHex(light ? 0xE0E0E0u : 0x2A2A2Au),
        ColorMath.FromHex(light ? 0xD0D0D0u : 0x3A3A3Au),
        ColorMath.FromHex(light ? 0xB0B0B0u : 0x444444u),
        ColorMath.FromHex(light ? 0x101010u : 0xF0F0F0u),
        ColorMath.FromHex(0x808080));

    [Fact]
    public void Follow_Dalamud_maps_the_host_and_keeps_gold_gold()
    {
        foreach (var light in new[] { false, true })
        {
            var p = Host(light);
            Assert.Equal(PaletteId.FollowDalamud, p.Id);
            Assert.Equal(PaletteChoices.FollowDalamud.Key, p.Key);
            Assert.Equal(light, p.IsLight);
            Assert.Equal(GlyphTokens.Moon, p.Inks.Gold);
            Assert.Null(p.Pills);
            Assert.Null(p.QuietTones);
            Assert.True(ColorMath.Contrast(p.Accent, p.Surface.Window) >= ColorMath.AaText);
            Assert.Equal(p.Surface.Top, p.Scene.Zenith);
            Assert.Equal(p.Surface.Deep, p.Scene.StatusTop);
            Assert.Equal(p.Surface.ForHighContrast(), p.HighContrast.Surface);
            Assert.Equal(p.Accent, p.HighContrast.Accent);
        }
    }

    [Fact]
    public void A_light_palette_switches_every_dark_background_token()
    {
        var dark = Host(light: false).Scene;
        Assert.True(dark.StarField && dark.NightGrade && !dark.WashInsteadOfGlow);
        Assert.Equal(1f, dark.ShadowStrength);

        var light = Host(light: true);
        var c = light.Scene;
        Assert.False(c.StarField);
        Assert.False(c.NightGrade);
        Assert.True(c.WashInsteadOfGlow);
        Assert.True(c.ShadowStrength < 1f && c.GlowStrength < 1f);
        Assert.Equal(light.Surface.Window, c.TextHalo);
        Assert.Equal(light.Surface.Text, c.BannerTitle);
        Assert.Equal(Vector4.One, c.TopHighlight);
        Assert.True(ColorMath.Luminance(c.Shadow) < 0.1f);
        Assert.True(ColorMath.Contrast(c.BannerTitle, light.Surface.Window) >= ColorMath.AaText);
    }
}
