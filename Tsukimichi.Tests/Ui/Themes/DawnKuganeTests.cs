using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Dawn and Kugane Lacquer (plan v7 T16, docs/design/v7/ui/spec-1.17.md §E): every role is the hex the approved design
/// computed (<c>docs/design/v7/ui/1.17/palettes17.json</c>), both are dark palettes that keep the stars, glows and night
/// grades, Kugane's sky is half as dense with warmer far stars, Ready's row halo is the gated .45 halo, and the
/// high-contrast forms push every ink to 7 : 1 on the window. <c>PaletteContrastTests</c> holds every pair to WCAG.
/// </summary>
public sealed class DawnKuganeTests
{
    private static readonly UiPalette Dawn = UiPalettes.Dawn;
    private static readonly UiPalette Kugane = UiPalettes.KuganeLacquer;

    private static void Hex(uint expected, Vector4 actual, string role) =>
        Assert.True(ColorMath.ToHex(actual) == expected, $"{role}: expected #{expected:X6}, got #{ColorMath.ToHex(actual):X6}");

    private static UiPalette Find(string key) => key switch
    {
        "dawn" => Dawn,
        "dawn-hc" => Dawn.HighContrast,
        "kugane" => Kugane,
        "kugane-hc" => Kugane.HighContrast,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    [Theory]
    [InlineData("dawn")]
    [InlineData("dawn-hc")]
    [InlineData("kugane")]
    [InlineData("kugane-hc")]
    public void Every_role_is_the_designed_hex(string key)
    {
        var palette = Find(key);
        var failures = new List<string>();
        foreach (var (role, hex) in PaletteDesign.Read("1.17/palettes17.json", key))
        {
            // The design keeps the base palette's horizon in its high-contrast row; the form has no sky to put it in.
            if (role == "Horizon" && palette.IsHighContrast)
            {
                Assert.Null(palette.Scene.SkyStops);
                continue;
            }

            var color = PaletteDesign.Role(palette, role);
            Assert.True(color.HasValue, $"palettes17.json role {role} has no palette role");
            if (ColorMath.ToHex(color.Value) != hex)
            {
                failures.Add($"{key} {role}: expected #{hex:X6}, got #{ColorMath.ToHex(color.Value):X6}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Both_are_dark_registered_and_offered()
    {
        foreach (var (palette, id, key) in new[] { (Dawn, PaletteId.Dawn, "dawn"), (Kugane, PaletteId.KuganeLacquer, "kugane-lacquer") })
        {
            Assert.False(palette.IsLight);
            Assert.False(palette.IsHighContrast);
            Assert.Equal(id, palette.Id);
            Assert.Equal(key, palette.Key);
            Assert.True(PaletteChoices.Get(id).Offered);
            Assert.False(PaletteChoices.Get(id).Light);
            Assert.True(UiPalettes.IsRegistered(id));
            Assert.Same(palette, UiPalettes.Get(id));
            Assert.Same(palette.HighContrast, UiPalettes.Get(id).HighContrast);
            Assert.True(palette.HighContrast.IsHighContrast);
            Assert.Equal(key + "-hc", palette.HighContrast.Key);
        }
    }

    [Fact]
    public void Both_keep_every_dark_path_rule()
    {
        // spec-1.17 §E1: the star field, moving sky and meteor stay; glows stay light; portraits and banners take the
        // night grade. The light-palette rules (no stars, washes for glows) are Ishgard Snow's alone.
        foreach (var palette in new[] { Dawn, Kugane })
        {
            var c = palette.Scene;
            Assert.True(c.StarField, palette.Key);
            Assert.True(c.NightGrade, palette.Key);
            Assert.False(c.WashInsteadOfGlow, palette.Key);
            Assert.Equal(1f, c.GlowStrength);
            Assert.Equal(1f, c.ShadowStrength);
            Assert.Equal(0f, c.TopHighlightAlpha);
            Assert.True(palette.Gauges.Glows, palette.Key);
            Assert.Equal(palette.Surface.Deep, c.Shadow);
            Assert.Equal(palette.Surface.Deep, c.TextHalo);
            Assert.Equal(palette.Inks.Gold, c.GlowWash);

            // Medals are never recoloured: a dark window keeps the standard glyph palette and the medallion's tokens.
            Assert.Same(GlyphPalette.Standard, GlyphPalette.Resolve(GlyphPaletteKind.Standard, palette.Surface.Window));
            Assert.Same(GlyphPalette.HighContrastDark, GlyphPalette.Resolve(GlyphPaletteKind.HighContrast, palette.Surface.Window));
            Assert.Same(MedalTokens.Standard, MedalTokens.For(GlyphPalette.Standard, MedalFinish.Gilt, false, palette.MedalRimGap));
        }
    }

    [Fact]
    public void Dawn_sky_is_plum_night_with_a_rose_horizon()
    {
        var stops = Assert.IsType<(float At, Vector4 Color)[]>(Dawn.Scene.SkyStops);
        Assert.Equal([0f, 0.14f, 0.30f, 0.40f, 0.46f, 0.58f, 0.76f, 1f], stops.Select(static s => s.At));
        uint[] hexes = [0x3A2746, 0x352444, 0x2B1F3A, 0x3E2A44, 0x5A3448, 0x2B1F3A, 0x1A1526, 0x1D1729];
        for (var i = 0; i < hexes.Length; i++)
        {
            Hex(hexes[i], stops[i].Color, $"Dawn sky stop {i}");
        }

        // The rose horizon is the brightest band, between 34 and 46 % (spec-1.17 §E2).
        var horizon = stops.MaxBy(static s => ColorMath.Luminance(s.Color));
        Assert.InRange(horizon.At, 0.34f, 0.46f);
        Hex(UiPalettes.DawnHorizonHex, horizon.Color, "Dawn horizon");
        Hex(0x1E1830, Dawn.Scene.StatusTop, "Dawn status bar top");
        Hex(0x140F20, Dawn.Scene.StatusFoot, "Dawn status bar foot");
        Assert.Null(Dawn.Scene.RailEdge);
    }

    [Fact]
    public void Kugane_sky_has_a_vermilion_dusk_band_and_a_lacquer_edge_but_no_vermilion_ink()
    {
        var stops = Assert.IsType<(float At, Vector4 Color)[]>(Kugane.Scene.SkyStops);
        Assert.Equal([0f, 0.12f, 0.26f, 0.40f, 0.45f, 0.56f, 0.74f, 1f], stops.Select(static s => s.At));
        uint[] hexes = [0x3A1A14, 0x33170F, 0x2A1613, 0x3B1D14, 0x4E2218, 0x2A1613, 0x16100F, 0x1A1210];
        for (var i = 0; i < hexes.Length; i++)
        {
            Hex(hexes[i], stops[i].Color, $"Kugane sky stop {i}");
        }

        var horizon = stops.MaxBy(static s => ColorMath.Luminance(s.Color));
        Assert.InRange(horizon.At, 0.40f, 0.46f);
        Hex(UiPalettes.KuganeHorizonHex, horizon.Color, "Kugane dusk band");

        // The rail's 1 px vermilion lacquer edge at Full (#B23422 at .45); gone under high contrast with the sky.
        var edge = Assert.NotNull(Kugane.Scene.RailEdge);
        Hex(UiPalettes.VermilionHex, edge, "Lacquer edge");
        Assert.Equal(0.45f, edge.W);
        Assert.Null(Kugane.HighContrast.Scene.RailEdge);
        Assert.Null(UiPalettes.Night.Scene.RailEdge);

        // Vermilion is a surface, never an ink: red already means Locked out and destructive.
        var vermilion = ColorMath.FromHex(UiPalettes.VermilionHex);
        var inks = new[] { Kugane.Accent, Kugane.AccentDim, Kugane.Inks.Danger, Kugane.Inks.DangerText, Kugane.Surface.Cool, Kugane.OrnamentLight }
            .Concat(StateInks.Order.SelectMany(state => new[] { Kugane.States.Text(state), Kugane.States.Stripe(state) }));
        Assert.All(inks, ink => Assert.NotEqual(ColorMath.ToHex(vermilion), ColorMath.ToHex(ink)));
        Hex(0x1E1513, Kugane.Scene.StatusTop, "Kugane status bar top");
        Hex(0x120D0C, Kugane.Scene.StatusFoot, "Kugane status bar foot");
    }

    [Fact]
    public void Kugane_sky_is_half_as_dense_with_warmer_far_stars()
    {
        // The supervisor's accepted option (spec-1.17 decision 6): a hazier sky over a lantern-lit port.
        var stars = Kugane.Scene.Stars;
        Assert.Equal(0.5f, stars.Density);
        Hex(0xE9E2DA, stars.FarCool, "Kugane far cool star");
        Hex(0xF1E3CC, stars.FarMoon, "Kugane far moon-white star");

        // Warmer than Night's far stars (more red than blue), and the near and mid stars keep Night's temperatures.
        Assert.True(stars.FarCool.X - stars.FarCool.Z > 0f && stars.FarMoon.X - stars.FarMoon.Z > 0f);
        var night = UiPalettes.Night.Scene.Stars;
        Assert.Equal(night.Cool, stars.Cool);
        Assert.Equal(night.MoonWhite, stars.MoonWhite);
        Assert.Equal(night.Gold, stars.Gold);
        Assert.Equal(night.Ember, stars.Ember);

        // Night and Dawn keep the full sky in Night's far inks.
        foreach (var full in new[] { night, Dawn.Scene.Stars })
        {
            Assert.Equal(1f, full.Density);
            Assert.Equal(full.Cool, full.FarCool);
            Assert.Equal(full.MoonWhite, full.FarMoon);
        }

        // A sky of 24 shows 12 on Kugane, the first 12, so it is the same sky with fewer stars.
        Assert.Equal(12, StarField.Thinned(24, stars.Density));
    }

    [Fact]
    public void Ready_halo_is_the_gated_halo_and_a_palette_role()
    {
        // spec-1.17 §E4.1: Ready's row halo on Dawn and Kugane is #F2D27A at .45 within 3 px (the wash's footprint),
        // raised to .60 only if the build records that a set needs it there.
        foreach (var palette in new[] { Dawn, Kugane })
        {
            Assert.True(palette.Scene.ReadyHaloWash, palette.Key);
            var halo = palette.Scene.Washes.ReadyHalo;
            Hex(WashTokens.WashHex, halo, $"{palette.Key} Ready halo");
            Assert.Contains(halo.W, new[] { WashTokens.DarkReadyHaloAlpha, WashTokens.DarkReadyHaloRaisedAlpha });
            Assert.Equal(WashTokens.ReadyHaloReachLogical, palette.Scene.Washes.ReadyHaloReach);
            Hex(ColorMath.ToHex(palette.Inks.Gold), palette.Scene.Washes.ReadyBadge, $"{palette.Key} Ready badge");
            Assert.Equal(0.16f, palette.Scene.Washes.ReadyBadge.W);
        }

        Assert.Equal(UiPalettes.DawnReadyHaloAlpha, Dawn.Scene.Washes.ReadyHalo.W);
        Assert.Equal(UiPalettes.KuganeReadyHaloAlpha, Kugane.Scene.Washes.ReadyHalo.W);
        Assert.Equal(0.45f, WashTokens.DarkReadyHaloAlpha);
        Assert.Equal(0.60f, WashTokens.DarkReadyHaloRaisedAlpha);

        // The one-line change the build may call for keeps the footprint.
        var raised = WashTokens.Dark(Kugane.Inks.Gold, WashTokens.DarkReadyHaloRaisedAlpha);
        Assert.Equal(0.60f, raised.ReadyHalo.W);
        Assert.Equal(Kugane.Scene.Washes.ReadyHaloReach, raised.ReadyHaloReach);

        // Night keeps its 1.12 glow; Snow keeps its .75 wash.
        Assert.False(UiPalettes.Night.Scene.ReadyHaloWash);
        Assert.Equal(WashTokens.ReadyHaloAlpha, UiPalettes.IshgardSnow.Scene.Washes.ReadyHalo.W);
    }

    [Theory]
    [InlineData("dawn")]
    [InlineData("kugane-lacquer")]
    public void Ready_halo_draws_the_halo_the_build_gated_on_that_palette(string ground)
    {
        // The build's G2D gate (tools/themes/build_themes.py, dark_halo_for) records in every set's metrics.json the least
        // halo each dark palette must draw: dark.halo[palette] names one of dark.halos ("default" .45, "raised" .60), or
        // "none" for no halo (alpha 0). The palette draws exactly that halo, so a rebuild that raises it fails here until
        // UiPalettes follows. metrics.json is not packaged, so it is read from the repo's assets.
        var drawn = ground == "dawn" ? UiPalettes.DawnReadyHaloAlpha : UiPalettes.KuganeReadyHaloAlpha;
        var palette = ground == "dawn" ? Dawn : Kugane;
        var sets = Directory.GetDirectories(Path.Combine(OrnamentLayoutTests.AssetsDir(), "themes"));
        Assert.NotEmpty(sets);
        foreach (var folder in sets)
        {
            using var metrics = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "metrics.json")));
            var dark = metrics.RootElement.GetProperty("dark");
            var name = dark.GetProperty("halo").GetProperty(ground).GetString();
            float gated;
            if (name == "none")
            {
                gated = 0f;
            }
            else
            {
                Assert.True(dark.GetProperty("halos").TryGetProperty(name!, out var halo), $"{folder}: dark.halos has no '{name}'");
                Assert.Equal("#F2D27A", halo.GetProperty("color").GetString());
                Assert.Equal(3, halo.GetProperty("radiusPx").GetInt32());
                gated = halo.GetProperty("alpha").GetSingle();
            }

            Assert.True(MathF.Abs(gated - drawn) < 1e-4f, $"{Path.GetFileName(folder)}: the build gated the '{name}' halo ({gated}) on {ground}; UiPalettes draws {drawn}");
            Assert.True(MathF.Abs(gated - palette.Scene.Washes.ReadyHalo.W) < 1e-4f, $"{ground}: the scene's halo alpha");
        }
    }

    [Fact]
    public void Designed_quiet_tones_are_the_spec_hexes()
    {
        var dawn = FlairTones.For(Flair.Quiet, Dawn);
        Hex(0x17121F, dawn.Rail, "Dawn Quiet rail");
        Hex(0x1A1524, dawn.Tree, "Dawn Quiet tree");
        Hex(0x1E1829, dawn.Table, "Dawn Quiet table");
        Hex(0x221B30, dawn.Detail, "Dawn Quiet detail");
        Hex(0x2A2338, dawn.Card, "Dawn Quiet card");
        Hex(0x3A3049, dawn.Rule, "Dawn Quiet rule");
        var kugane = FlairTones.For(Flair.Quiet, Kugane);
        Hex(0x140E0D, kugane.Rail, "Kugane Quiet rail");
        Hex(0x171110, kugane.Tree, "Kugane Quiet tree");
        Hex(0x1B1412, kugane.Table, "Kugane Quiet table");
        Hex(0x201715, kugane.Detail, "Kugane Quiet detail");
        Hex(0x281D1A, kugane.Card, "Kugane Quiet card");
        Hex(0x3A2B24, kugane.Rule, "Kugane Quiet rule");

        // Quiet's medal rim lies on the palette's tree tone, as Night's does on its own.
        Assert.Equal(dawn.Tree, Dawn.MedalRimGap);
        Assert.Equal(kugane.Tree, Kugane.MedalRimGap);

        // The quieter inks still read on every designed Quiet pane, the cards the lightest of them.
        foreach (var (palette, tones) in new[] { (Dawn, dawn), (Kugane, kugane) })
        {
            foreach (var pane in new[] { tones.Rail, tones.Tree, tones.Table, tones.Detail, tones.Card })
            {
                foreach (var ink in new[] { palette.Surface.TextTertiary, palette.States.Text(QuestState.Unknown), palette.Surface.Cool, palette.Accent })
                {
                    Assert.True(ColorMath.Contrast(ink, pane) >= ColorMath.AaText, $"{palette.Key} #{ColorMath.ToHex(ink):X6} on #{ColorMath.ToHex(pane):X6}");
                }
            }
        }
    }

    [Fact]
    public void Gold_stays_gold_and_the_pills_and_gauges_take_the_palette_gold()
    {
        Hex(0xF5C47C, Dawn.Inks.Gold, "Dawn gold");
        Hex(0xFFE6B8, Dawn.Inks.GoldHigh, "Dawn gold high");
        Hex(0xD9A55E, Dawn.Inks.GoldDeep, "Dawn gold deep");
        var dawnPills = Assert.NotNull(Dawn.Pills);
        Hex(0xFFE2AE, dawnPills.GoldTop, "Dawn gold pill top");
        Hex(0xD9A55E, dawnPills.GoldFoot, "Dawn gold pill foot");
        Hex(0xF8D9A4, dawnPills.GoldEdge, "Dawn gold pill edge");
        Hex(0x30283F, dawnPills.RaisedTop, "Dawn raised pill top");
        Hex(0x262036, dawnPills.RaisedFoot, "Dawn raised pill foot");

        Hex(0xF0CC72, Kugane.Inks.Gold, "Kugane gold");
        var kuganePills = Assert.NotNull(Kugane.Pills);
        Assert.Equal(UiPalettes.NightPills.GoldTop, kuganePills.GoldTop);
        Hex(0x2D211E, kuganePills.RaisedTop, "Kugane raised pill top");

        foreach (var (palette, shade) in new[] { (Dawn, 0xD9A55Eu), (Kugane, 0xD4AE55u) })
        {
            var g = palette.Gauges;
            Assert.Equal(palette.Inks.Gold, g.OuterSlope[0].Color);
            Hex(shade, g.OuterSlope[^1].Color, $"{palette.Key} arc shade");
            Assert.Equal(g.OuterSlope[^1].Color, g.InnerSlope[0].Color);
            Assert.Equal(palette.Surface.Deep, g.Keyline);
            Assert.Equal(palette.Surface.StrongLine with { W = 0.55f }, g.Track);
            foreach (var arc in g.ArcColors())
            {
                Assert.True(ColorMath.Contrast(arc, g.Groove) >= ColorMath.AaNonText, $"{palette.Key} arc #{ColorMath.ToHex(arc):X6} on its groove");
            }
        }
    }

    [Theory]
    [InlineData("dawn")]
    [InlineData("kugane")]
    public void High_contrast_inks_reach_seven_to_one_on_the_window(string key)
    {
        var hc = Find(key + "-hc");
        var s = hc.Surface;
        var inks = new List<(string, Vector4)>
        {
            ("Text", s.Text), ("TextSecondary", s.TextSecondary), ("TextTertiary", s.TextTertiary), ("Cool", s.Cool),
            ("Accent", hc.Accent), ("AccentDim", hc.AccentDim), ("OrnamentLight", hc.OrnamentLight),
            ("DangerText", hc.Inks.DangerText), ("UnknownText", hc.Inks.UnknownText),
        };
        inks.AddRange(StateInks.Order.Select(state => ($"{state} word", hc.States.Text(state))));
        var failures = inks
            .Select(i => (i.Item1, Ratio: ColorMath.Contrast(i.Item2, s.Window)))
            .Where(static i => i.Ratio < ColorMath.AaaText)
            .Select(i => $"{hc.Key} {i.Item1}: {i.Ratio:0.00} : 1")
            .ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

        foreach (var state in StateInks.Order)
        {
            Assert.True(ColorMath.Contrast(hc.States.Stripe(state), s.Window) >= ColorMath.AaNonText, $"{hc.Key} {state} stripe");
        }

        // No sky (spec-1.17 §E3), the opaque strong-line ornament, and the warm inks kept: secondary text is the
        // palette's own, not Night's.
        Assert.Equal(s.Window, s.Top);
        Assert.Equal(s.Window, hc.Scene.Zenith);
        Assert.Null(hc.Scene.SkyStops);
        Assert.Equal(s.StrongLine, s.Ornament);
        Assert.Equal(Find(key).Surface.TextSecondary, s.TextSecondary);
        Assert.Null(hc.QuietTones);
    }

    [Fact]
    public void The_spec_contrast_figures_hold()
    {
        // spec-1.17 §E4 (contrast17.md), worst surface, to the table's one decimal.
        static float Worst(Vector4 ink, params Vector4[] grounds) => grounds.Min(g => ColorMath.Contrast(ink, g));
        static void Near(float expected, float actual, string what) => Assert.True(MathF.Abs(expected - actual) <= 0.051f, $"{what}: expected {expected:0.0}, got {actual:0.00}");
        foreach (var (palette, text, tertiary, notChecked, strong, stripeLocked) in new[]
        {
            (Dawn, 11.6f, 5.0f, 5.0f, 3.1f, 4.3f),
            (Kugane, 13.0f, 5.3f, 5.6f, 3.1f, 4.3f),
            (Dawn.HighContrast, 11.6f, 5.5f, 5.5f, 4.0f, 4.3f),
            (Kugane.HighContrast, 13.0f, 5.9f, 5.9f, 4.1f, 4.3f),
        })
        {
            var s = palette.Surface;
            Near(text, Worst(s.Text, s.Window, s.Raised, s.Sunken, s.Hover), $"{palette.Key} Text");
            Near(tertiary, Worst(s.TextTertiary, s.Window, s.Raised, s.Sunken, s.Hover), $"{palette.Key} TextTertiary");
            Near(notChecked, Worst(palette.States.Text(QuestState.Unknown), s.Window, s.Raised, s.Hover), $"{palette.Key} Not checked");
            Near(strong, Worst(s.StrongLine, s.Window, s.Raised), $"{palette.Key} StrongLine");
            Near(stripeLocked, Worst(palette.States.Stripe(QuestState.Foreclosed), s.Window, s.Raised), $"{palette.Key} Locked out stripe");
        }

        // The re-tuned strong line sits at the bar on cards (spec-1.17 §E2: 3.0 : 1 on Raised), the tertiary text and the
        // Not checked word clear it on hovered rows.
        foreach (var palette in new[] { Dawn, Kugane })
        {
            var s = palette.Surface;
            Assert.InRange(ColorMath.Contrast(s.StrongLine, s.Raised), 3.0f, 3.2f);
            Assert.True(ColorMath.Contrast(s.TextTertiary, s.Hover) >= ColorMath.AaText, $"{palette.Key} tertiary on Hover");
            Assert.True(ColorMath.Contrast(palette.States.Text(QuestState.Unknown), s.Hover) >= ColorMath.AaText, $"{palette.Key} Not checked on Hover");
        }
    }

    [Fact]
    public void Gold_and_locked_out_stay_apart_under_colour_vision_deficiency()
    {
        // research §8.2, spec-1.17 §E4: the accent against the Locked out word, worst OKLab ΔE under Machado protanopia,
        // deuteranopia and tritanopia, at least 0.08 (Dawn 0.103 and Kugane 0.128 in the design).
        foreach (var (palette, design) in new[] { (Dawn, 0.103f), (Dawn.HighContrast, 0.103f), (Kugane, 0.128f), (Kugane.HighContrast, 0.128f) })
        {
            var distance = PaletteDesign.WorstColourVisionDistance(palette.Accent, palette.States.Text(QuestState.Foreclosed));
            Assert.True(distance >= 0.08f, $"{palette.Key}: ΔE {distance:0.000}");
            Assert.True(MathF.Abs(distance - design) <= 0.002f, $"{palette.Key}: ΔE {distance:0.000}, the design's {design:0.000}");
        }
    }

    [Fact]
    public void The_destructive_button_ink_reads()
    {
        // Dawn's plum button (#9A5577) under its pearl text is 4.4 : 1, so it takes white; Kugane keeps its washi text.
        Assert.Equal(Vector4.One, Dawn.Inks.OnDanger);
        Assert.Equal(Kugane.Surface.Text, Kugane.Inks.OnDanger);
        foreach (var palette in new[] { Dawn, Kugane })
        {
            var button = ColorMath.Over(palette.Inks.Danger with { W = PaletteInks.DangerButtonAlpha }, palette.Surface.Window);
            Assert.True(ColorMath.Contrast(palette.Inks.OnDanger, button) >= ColorMath.AaText, palette.Key);
        }
    }
}
