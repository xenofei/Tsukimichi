using System.Numerics;
using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Ishgard Snow and the designed high-contrast forms (plan v7 T8, docs/design/v7/ui/spec-1.16.md §A): every role is the
/// hex the approved design computed (<c>docs/design/v7/ui/1.16/palettes.json</c>, read from the repository), the light
/// scene has no stars at all and washes where Night glows, the gauges have their own ink, and the high-contrast forms
/// push every ink to 7 : 1 on the window.
/// </summary>
public sealed class IshgardSnowTests
{
    private static readonly UiPalette Snow = UiPalettes.IshgardSnow;

    private static void Hex(uint expected, Vector4 actual, string role) =>
        Assert.True(ColorMath.ToHex(actual) == expected, $"{role}: expected #{expected:X6}, got #{ColorMath.ToHex(actual):X6}");

    private static Dictionary<string, uint> Design(string key)
    {
        var path = Path.Combine(ResxFiles.RepositoryRoot(), "docs", "design", "v7", "ui", "1.16", "palettes.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var roles = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var role in json.RootElement.GetProperty("palettes").GetProperty(key).EnumerateObject())
        {
            roles[role.Name] = Convert.ToUInt32(role.Value.GetString()!.TrimStart('#'), 16);
        }

        return roles;
    }

    /// <summary>The palette's colour for a role name in palettes.json.</summary>
    private static Vector4? Role(UiPalette p, string role)
    {
        var s = p.Surface;
        return role switch
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
            "Zenith" => p.Scene.Zenith,
            "Ornament" => s.Ornament,
            "OrnamentHigh" => s.OrnamentHigh,
            "OrnamentLight" => p.OrnamentLight,
            "Cool" => s.Cool,
            "Accent" => p.Accent,
            "Ready" => p.States.Text(QuestState.Ready),
            "InJournal" => p.States.Text(QuestState.Accepted),
            "Completed" => p.States.Text(QuestState.Completed),
            "ReadyOnOtherJob" => p.States.Text(QuestState.ReadyOnOtherJob),
            "DoneThisCycle" => p.States.Text(QuestState.DoneThisCycle),
            "Blocked" => p.States.Text(QuestState.Blocked),
            "LockedOut" => p.States.Text(QuestState.Foreclosed),
            "NotChecked" => p.States.Text(QuestState.Unknown),
            "GaugeArc" => p.Gauges.Arc,
            "GaugeArcShade" => p.Gauges.OuterSlope[^1].Color,
            "Groove" => p.Gauges.Groove,
            "StripeGold" => p.States.Stripe(QuestState.Ready),
            "StripeCompleted" => p.States.Stripe(QuestState.Completed),
            "StripeSilver" => p.States.Stripe(QuestState.ReadyOnOtherJob),
            "StripeLocked" => p.States.Stripe(QuestState.Foreclosed),
            "StripeVeil" => p.States.Stripe(QuestState.Unknown),
            _ => null,
        };
    }

    /// <summary>
    /// Night's design row models a few graphics Night draws in its own 1.15 material (the medal gauges' lapis groove and
    /// gilt bezel, the MoonDim and VeilText stripes): those stay 1.15's, held by <c>UiPaletteTests</c>.
    /// </summary>
    private static readonly HashSet<string> NightKeeps = new(StringComparer.Ordinal) { "GaugeArcShade", "Groove", "StripeCompleted", "StripeVeil" };

    [Theory]
    [InlineData("snow", false)]
    [InlineData("snow-hc", false)]
    [InlineData("night", true)]
    [InlineData("night-hc", true)]
    public void Every_role_is_the_designed_hex(string key, bool night)
    {
        var palette = (night ? UiPalettes.Night : Snow) is var p && key.EndsWith("-hc", StringComparison.Ordinal) ? p.HighContrast : p;
        var failures = new List<string>();
        foreach (var (role, hex) in Design(key))
        {
            if (night && NightKeeps.Contains(role))
            {
                continue;
            }

            var color = Role(palette, role);
            Assert.True(color.HasValue, $"palettes.json role {role} has no palette role");
            if (ColorMath.ToHex(color.Value) != hex)
            {
                failures.Add($"{key} {role}: expected #{hex:X6}, got #{ColorMath.ToHex(color.Value):X6}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Snow_is_light_and_registered_as_offered()
    {
        Assert.True(Snow.IsLight);
        Assert.Equal(PaletteId.IshgardSnow, Snow.Id);
        Assert.Equal("ishgard-snow", Snow.Key);
        Assert.True(PaletteChoices.Get(PaletteId.IshgardSnow).Offered);
        Assert.True(PaletteChoices.Get(PaletteId.IshgardSnow).Light);
        Assert.Same(Snow, UiPalettes.Get(PaletteId.IshgardSnow));
    }

    [Fact]
    public void Snow_has_no_stars_and_a_still_dawn()
    {
        var c = Snow.Scene;
        Assert.False(c.StarField);
        Assert.False(c.NightGrade);
        Assert.True(c.WashInsteadOfGlow);
        Assert.Equal(0f, c.GlowStrength);
        var stops = Assert.IsType<(float At, Vector4 Color)[]>(c.SkyStops);
        Assert.Equal([0f, 0.14f, 0.30f, 0.42f, 0.56f, 0.80f, 1f], stops.Select(static s => s.At));
        uint[] hexes = [0xD3DEF0, 0xDCE5F3, 0xE7ECF4, 0xEFEAEC, 0xEEF1F6, 0xF1F3F7, 0xF4F6F9];
        for (var i = 0; i < hexes.Length; i++)
        {
            Hex(hexes[i], stops[i].Color, $"sky stop {i}");
        }

        Hex(0x1A2136, c.Shadow, "Shadow");
        Hex(0x1A2136, c.ShadowInk, "ShadowInk");
        Assert.InRange(c.ShadowStrength, 0.30f, 0.36f);
        Hex(0xFFFFFF, c.TopHighlight, "TopHighlight");
        Assert.True(c.TopHighlightAlpha > 0.5f);
        Hex(0x1A2136, c.BannerTitle, "BannerTitle");

        // The high-contrast form has no sky at all.
        Assert.Null(Snow.HighContrast.Scene.SkyStops);
        Assert.Equal(Snow.Surface.Window, Snow.HighContrast.Scene.Zenith);
    }

    [Fact]
    public void Snow_washes_are_the_supervisors_final_ruling()
    {
        var w = Snow.Scene.Washes;
        Hex(0xF2D27A, w.ReadyHalo, "Ready wash");
        Assert.Equal(0.75f, w.ReadyHalo.W);
        Assert.Equal(3f, w.ReadyHaloReach);
        Hex(0xE9C46A, w.HeroHalo, "Hero halo");
        Assert.Equal(0.30f, w.HeroHalo.W);
        Hex(0xF2D27A, w.Selection, "Selection wash");
        Assert.Equal(0.30f, w.Selection.W);
        Assert.Equal(0.05f, w.SelectionFoot);
        Hex(0xAC8324, w.SelectionRule, "Selection rule");
        Assert.Equal(0.45f, w.SelectionRule.W);
        Hex(0xAC8324, w.ReadyRoad, "Ready road");
        Assert.Equal(0.55f, w.ReadyRoad.W);
    }

    [Fact]
    public void Snow_gauges_have_their_own_ink()
    {
        var g = Snow.Gauges;
        Hex(0x8A6A1C, g.OuterSlope[0].Color, "arc highlight");
        Hex(0x755308, g.OuterSlope[^1].Color, "arc shade");
        Assert.All(g.ArcColors(), static c => Assert.NotEqual(0xA07B25u, ColorMath.ToHex(c)));
        Hex(0xCAD2DF, g.Groove, "groove");
        Hex(0x7A859C, g.Keyline, "keylines");
        Hex(0x8A6A1C, g.Knob, "knob");
        Hex(0xF9FAFC, g.KnobRim, "knob rim");
        Hex(0xC3CEE4, g.MoonLit, "moonstone");
        Hex(0x59627A, g.DarkSide, "dark side");
        Assert.False(g.Glows);
        Assert.True(UiPalettes.Night.Gauges.Glows);
        Assert.Equal(GaugeInks.Stop(g.OuterSlope, 0.5f), Vector4.Lerp(g.OuterSlope[0].Color, g.OuterSlope[^1].Color, 0.5f));
    }

    [Fact]
    public void Snow_keeps_gold_gold_and_draws_lead_frames()
    {
        Hex(0x755308, Snow.Accent, "Accent");
        Hex(0xAC8324, Snow.Inks.GoldLine, "GoldLine");
        Hex(0xF2D27A, Snow.Inks.GoldHigh, "GoldHigh (Moon)");
        var pills = Assert.NotNull(Snow.Pills);
        Hex(0xF8DE96, pills.GoldTop, "Gold pill top");
        Hex(0xD6AE52, pills.GoldFoot, "Gold pill foot");
        Hex(0xA88437, pills.GoldEdge, "Gold pill edge");
        Hex(0x2A1E05, pills.GoldInk, "Gold pill ink");
        Hex(0xB8C0D0, Snow.Brass.At(0f), "Lead at 0");
        Hex(0x7C8498, Snow.Brass.At(0.28f), "Lead at 28 %");
        Hex(0x4A5268, Snow.Brass.At(1f), "Lead at 1");
        Hex(0x59627A, Snow.Brass.CornerLit, "Corner lit");
        Hex(0x4A5268, Snow.Brass.CornerShaded, "Corner shaded");
        Hex(0xDCE3EE, Snow.Plate.WellTop, "Plate well top");
        Hex(0xC8D1E0, Snow.Plate.WellFoot, "Plate well foot");
        Hex(0x56607C, Snow.Plate.Initials, "Plate initials");
        Assert.Equal(0.8f, Snow.Plate.QuietKeyline.W);
        Assert.Equal(0.9f, Snow.Plate.OuterRing.W);
    }

    [Fact]
    public void Snow_designed_tones_are_the_spec_hexes()
    {
        var quiet = FlairTones.For(Flair.Quiet, Snow);
        Hex(0xE3E8F0, quiet.Rail, "Quiet rail");
        Hex(0xE8ECF2, quiet.Tree, "Quiet tree");
        Hex(0xEEF1F6, quiet.Table, "Quiet table");
        Hex(0xF3F5F9, quiet.Detail, "Quiet detail");
        Hex(0xFAFBFD, quiet.Card, "Quiet card");
        Hex(0xD3DAE5, quiet.Rule, "Quiet rule");
        var plain = FlairTones.For(Flair.Plain, Snow);
        Hex(0xE3E8F0, plain.Band, "Plain band");
        Hex(0xD3DAE5, plain.Rule, "Plain lines");
        var zebra = Assert.NotNull(Snow.Zebra);
        Hex(0x1A2136, zebra, "Plain zebra");
        Assert.Equal(0.03f, zebra.W);
        Hex(0x3F4862, DrawerTones.For(Flair.Full, Snow).Heading, "Drawer heads");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void High_contrast_inks_reach_seven_to_one_on_the_window(bool light)
    {
        var hc = (light ? Snow : UiPalettes.Night).HighContrast;
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

        Assert.Equal(s.Window, s.Top);
        Assert.Equal(s.StrongLine, s.Ornament);
    }

    [Fact]
    public void The_colour_vision_pair_of_accent_and_locked_out_stays_apart()
    {
        // spec-1.16 §A2: the deep gold against the plum, worst-case ΔE in OKLab under protan, deutan and tritan ≥ 0.08.
        foreach (var palette in new[] { Snow, Snow.HighContrast, UiPalettes.Night })
        {
            var accent = palette.Accent;
            var locked = palette.States.Text(QuestState.Foreclosed);
            foreach (var mode in new[] { ColorVision.Protanopia, ColorVision.Deuteranopia, ColorVision.Tritanopia })
            {
                var a = Oklab(ColorVisionSimulation.Simulate(accent, mode));
                var b = Oklab(ColorVisionSimulation.Simulate(locked, mode));
                var distance = Vector3.Distance(a, b);
                Assert.True(distance >= 0.08f, $"{palette.Key} {mode}: ΔE {distance:0.000}");
            }
        }
    }

    /// <summary>An sRGB colour in OKLab (Björn Ottosson's matrices).</summary>
    private static Vector3 Oklab(Vector4 srgb)
    {
        var r = ColorVisionSimulation.ToLinear(srgb.X);
        var g = ColorVisionSimulation.ToLinear(srgb.Y);
        var b = ColorVisionSimulation.ToLinear(srgb.Z);
        var l = MathF.Cbrt((0.4122214708f * r) + (0.5363325363f * g) + (0.0514459929f * b));
        var m = MathF.Cbrt((0.2119034982f * r) + (0.6806995451f * g) + (0.1073969566f * b));
        var s = MathF.Cbrt((0.0883024619f * r) + (0.2817188376f * g) + (0.6299787005f * b));
        return new Vector3(
            (0.2104542553f * l) + (0.7936177850f * m) - (0.0040720468f * s),
            (1.9779984951f * l) - (2.4285922050f * m) + (0.4505937099f * s),
            (0.0259040371f * l) + (0.7827717662f * m) - (0.8086757660f * s));
    }

    [Fact]
    public void Follow_Dalamud_on_a_light_host_takes_the_light_gauges()
    {
        var light = UiPalettes.FollowDalamud(
            ColorMath.FromHex(0xF0F0F0), ColorMath.FromHex(0xE0E0E0), ColorMath.FromHex(0xD0D0D0),
            ColorMath.FromHex(0xB0B0B0), ColorMath.FromHex(0x101010), ColorMath.FromHex(0x808080));
        Assert.Equal(UiPalettes.LightGauges, light.Gauges);
        Assert.True(light.Scene.WashInsteadOfGlow);
        Assert.True(light.Scene.TopHighlightAlpha > 0f);
    }

    [Fact]
    public void Quiet_medals_take_a_light_rim_on_a_light_palette()
    {
        var tokens = MedalTokens.For(GlyphPalette.Standard, MedalFinish.LightRim, onLight: true);
        Assert.Same(MedalTokens.LightRimOnLight, tokens);
        Hex(0x7A859C, tokens.RimInk, "light rim ink");
        Assert.Equal(0.8f, tokens.RimAlpha);
        Hex(0xEEF1F6, tokens.RimGap, "light rim gap");
        Assert.Same(MedalTokens.LightRim, MedalTokens.For(GlyphPalette.Standard, MedalFinish.LightRim));
        Assert.Same(MedalTokens.Standard, MedalTokens.For(GlyphPalette.Standard, MedalFinish.Gilt, onLight: true));
    }
}
