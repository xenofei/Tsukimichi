using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The frames axis (feature plan v7 T11; docs/design/v7/ui/spec-1.17.md §B; ATLAS-CONTRACT §7): which part each state
/// draws, the faces and frames layouts, when the appearance composes a set from its faces and the kit's frames, and each
/// kit's metal outside the medals.
/// </summary>
public sealed class FramePartsTests
{
    // ------------------------------------------------------------------ parts

    [Fact]
    public void Each_state_wears_its_urgency_tiers_frame_and_its_badge()
    {
        Assert.Equal(FrameUrgency.ActNow, FrameParts.UrgencyOf(QuestState.Ready));
        Assert.Equal(FrameUrgency.Finished, FrameParts.UrgencyOf(QuestState.Completed));
        Assert.Equal(FrameUrgency.Ghost, FrameParts.UrgencyOf(QuestState.Unknown));
        foreach (var state in new[] { QuestState.ReadyOnOtherJob, QuestState.Accepted, QuestState.Blocked, QuestState.DoneThisCycle, QuestState.Foreclosed })
        {
            Assert.Equal(FrameUrgency.Resting, FrameParts.UrgencyOf(state));
        }

        Assert.Equal("frame-act-now-full", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Frame(QuestState.Ready, quiet: false)));
        Assert.Equal("frame-ghost-quiet", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Frame(QuestState.Unknown, quiet: true)));
        Assert.Equal("frame-finished-full", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Frame(QuestState.Completed, quiet: false)));
        Assert.Equal("badge-open", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Badge(QuestState.Ready, JobSeat.Tank)));
        Assert.Equal("badge-closed", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Badge(QuestState.Blocked, JobSeat.Tank)));
        Assert.Equal("badge-journal", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Badge(QuestState.Accepted, JobSeat.Tank)));
        Assert.Equal("badge-seat-healer", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Badge(QuestState.ReadyOnOtherJob, JobSeat.Healer)));
        Assert.Equal("badge-seat-hand", FrameParts.Key(PartAtlasKind.Frames, FrameParts.Badge(QuestState.ReadyOnOtherJob, JobSeat.Hand)));
        Assert.Equal(-1, FrameParts.Badge(QuestState.Completed, JobSeat.Tank));
        Assert.Equal("in-journal", FrameParts.Key(PartAtlasKind.Faces, FrameParts.Face(QuestState.Accepted, over: false)));
        Assert.Equal("completed-over", FrameParts.Key(PartAtlasKind.Faces, FrameParts.Face(QuestState.Completed, over: true)));

        // Every sprite slot has its own key.
        foreach (var kind in Enum.GetValues<PartAtlasKind>())
        {
            var keys = Enumerable.Range(0, FrameParts.Count(kind)).Select(i => FrameParts.Key(kind, i)).ToArray();
            Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void On_a_light_palette_every_medal_keeps_the_Abyss_outer_keyline_at_Full()
    {
        // spec-1.17 §B2, the supervisor's condition for Silver on Ishgard Snow: 1 px Abyss at .6 round every kit's medals.
        Assert.True(FrameParts.LightKeyline(lightPalette: true, highContrast: false, MedalFinish.Gilt));
        Assert.False(FrameParts.LightKeyline(lightPalette: false, highContrast: false, MedalFinish.Gilt));
        Assert.False(FrameParts.LightKeyline(lightPalette: true, highContrast: true, MedalFinish.Gilt));
        Assert.False(FrameParts.LightKeyline(lightPalette: true, highContrast: false, MedalFinish.LightRim));
        Assert.False(FrameParts.LightKeyline(lightPalette: true, highContrast: false, MedalFinish.Plain));
        Assert.Equal(0.6f, FrameParts.LightKeylineAlpha);

        // Half a pixel outside the medal's own keyline (r 63.2 of 128), so the 1 px line sits just outside the rim.
        Assert.Equal((128f * MedalArt.KeylineRadius / 128f) + 0.5f, FrameParts.LightKeylineRadius(128f));
        Assert.True(FrameParts.LightKeylineRadius(48f) > 48f * MedalArt.KeylineRadius / 128f);
    }

    [Fact]
    public void A_part_is_placed_at_its_box_of_the_medal()
    {
        Assert.Equal((10f + 48f, 20f + 48f, 48f, 48f), FrameParts.Place(FrameParts.BadgeBox, 10f, 20f, 96f));
        Assert.Equal((10f, 20f, 96f, 96f), FrameParts.Place(FrameParts.FullBox, 10f, 20f, 96f));
        Assert.Equal((3f, 3f, 42f, 42f), FrameParts.Place(new AtlasRect(8, 8, 112, 112), 0f, 0f, 48f));
    }

    [Fact]
    public void A_faces_layout_needs_every_under_layer_and_takes_its_over_layers_as_they_come()
    {
        Assert.True(PartAtlasLayout.TryParse(FacesJson(withOver: true).ToJsonString(), PartAtlasKind.Faces, out var faces, out var error), error);
        Assert.True(faces!.Has(FrameParts.Face(QuestState.Completed, over: true)));
        Assert.False(faces.Has(FrameParts.Face(QuestState.Ready, over: true)));
        Assert.True(faces.TryRect(FrameParts.Face(QuestState.Ready, over: false), 48, out var rect));
        Assert.Equal((42, 42), (rect.Width, rect.Height));
        Assert.False(faces.TryRect(FrameParts.Face(QuestState.Ready, over: true), 48, out _));
        Assert.Equal(new AtlasRect(8, 8, 112, 112), faces.Box(FrameParts.Face(QuestState.Ready, over: false)));
        Assert.Equal((96, true), faces.Pick(190f));
        Assert.Equal((48, false), faces.Pick(32f));

        // Missing an under layer, a box, or a cell of the wrong size is refused.
        var noReady = FacesJson(withOver: false);
        noReady["sprites"]!.AsObject().Remove("ready");
        Assert.False(PartAtlasLayout.TryParse(noReady.ToJsonString(), PartAtlasKind.Faces, out _, out error));
        Assert.Contains("no sprite ready", error);

        var noBox = FacesJson(withOver: false);
        noBox["boxes"]!.AsObject().Remove("blocked");
        Assert.False(PartAtlasLayout.TryParse(noBox.ToJsonString(), PartAtlasKind.Faces, out _, out error));
        Assert.Contains("no box", error);

        var oddBox = FacesJson(withOver: false);
        oddBox["boxes"]!["blocked"] = new JsonArray(4, 8, 112, 112);
        oddBox["boxes"]!["blocked"]![2] = 110;
        Assert.False(PartAtlasLayout.TryParse(oddBox.ToJsonString(), PartAtlasKind.Faces, out _, out error));
    }

    [Fact]
    public void A_row_parts_strip_takes_whole_pixel_cells_and_no_badges()
    {
        var sprites = new JsonObject();
        var y = 2;
        for (var s = 0; s < 8; s++)
        {
            var cells = new JsonObject();
            var x = 2;
            for (var px = 12; px <= 31; px++)
            {
                cells[px.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new JsonArray(x, y, px, px);
                x += px + 2;
            }

            sprites[FrameParts.Key(PartAtlasKind.Frames, s)] = cells;
            y += 33;
        }

        var json = new JsonObject
        {
            ["size"] = new JsonArray(472, y + 1),
            ["sizes"] = new JsonArray(Enumerable.Range(12, 20).Select(static s => (JsonNode)s).ToArray()),
            ["sprites"] = sprites,
        };
        Assert.True(PartAtlasLayout.TryParse(json.ToJsonString(), PartAtlasKind.Frames, out var row, out var error), error);
        Assert.True(row!.Row);
        Assert.Equal((31, false), row.Pick(40f));
        Assert.Equal((12, false), row.Pick(9f));
        Assert.Equal((17, false), row.Pick(17.2f));
        Assert.False(row.Has(FrameParts.Badge(QuestState.Ready, JobSeat.Tank)));
        Assert.Equal(0, row.Bytes2x);
    }

    private static JsonObject FacesJson(bool withOver)
    {
        int[] tiers = [48, 64, 96, 128];
        var sprites = new JsonObject();
        var boxes = new JsonObject();
        var x = 2;
        var y = 2;
        var keys = Enumerable.Range(0, 8).Select(i => FrameParts.Key(PartAtlasKind.Faces, i)).ToList();
        if (withOver)
        {
            keys.Add("completed-over");
        }

        foreach (var key in keys)
        {
            boxes[key] = new JsonArray(8, 8, 112, 112);
            var cells = new JsonObject();
            foreach (var tier in tiers)
            {
                var side = 112 * tier / 128;
                cells[tier.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new JsonArray(x, y, side, side);
                x += side + 2;
            }

            sprites[key] = cells;
            x = 2;
            y += 114;
        }

        return new JsonObject
        {
            ["size"] = new JsonArray(2 + (42 + 56 + 84 + 112) + 4 * 2 + 1, y + 1),
            ["tiers"] = new JsonArray(tiers.Select(static t => (JsonNode)t).ToArray()),
            ["boxes"] = boxes,
            ["sprites"] = sprites,
        };
    }

    // ------------------------------------------------------------------ when a medal is composed

    [Fact]
    public void A_set_is_composed_only_in_a_kit_that_is_not_its_own()
    {
        // The default look composes nothing: Medallion draws as shipped.
        Assert.False(ResolvedAppearance.Default.ComposesAny);
        Assert.False(ResolvedAppearance.Default.Composes(GlyphSetId.Medallion));

        // A set the appearance does not use would be composed in Brass, were it mixed in: it is not its kit.
        Assert.True(ResolvedAppearance.Default.Composes(GlyphSetId.AetherCrystal));
        Assert.False(ResolvedAppearance.Default.Composes(GlyphSetId.Classic));

        var silver = AppearanceResolver.Resolve(new AppearanceConfig { Frames = "silver" });
        Assert.True(silver.Composes(GlyphSetId.Medallion));
        Assert.False(silver.Composes(GlyphSetId.AetherCrystal));
        Assert.True(silver.ComposesAny);

        // A state mixed in from another set takes the column's kit; the theme's own set stays as designed.
        var mixed = AppearanceResolver.Resolve(new AppearanceConfig { Theme = "ishgard-glass", Glyphs = new Dictionary<string, string> { ["ready"] = "astrologian-orrery" } });
        Assert.True(mixed.Composes(GlyphSetId.Orrery));
        Assert.False(mixed.Composes(GlyphSetId.IshgardGlass));
        Assert.True(mixed.ComposesAny);

        // High contrast is one shared set, and Classic is whole theme only: neither composes.
        Assert.False(AppearanceResolver.Resolve(new AppearanceConfig { Frames = "silver", HighContrast = true }).ComposesAny);
        Assert.False(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "classic", Frames = "silver" }).ComposesAny);
        Assert.False(silver.Composes((GlyphSetId)200));
    }

    // ------------------------------------------------------------------ each kit's metal

    [Fact]
    public void Brass_draws_the_palettes_own_metal_and_the_other_kits_their_spec_ramps_on_a_dark_palette()
    {
        foreach (var palette in new[] { UiPalettes.Night, UiPalettes.IshgardSnow, UiPalettes.Night.HighContrast })
        {
            Assert.Equal(palette.Brass, FrameKitMetals.Ornament(palette, FrameKitId.Brass));
            Assert.Equal(palette.Gauges, FrameKitMetals.Gauges(palette, FrameKitId.Brass));
            Assert.Equal(palette.Brass, FrameKitMetals.Ornament(palette, FrameKitId.Kirikane));
        }

        // spec-1.17 §B1: each kit's resting ramp, highlight → body → shadow → deep.
        AssertRamp(FrameKitMetals.Silver, 0xE2E8F4, 0xA9B5D0, 0x7B8AAF, 0x5E6E97);
        AssertRamp(FrameKitMetals.Came, 0xB8C0D0, 0x8C95B0, 0x5A6278, 0x323950);
        AssertRamp(FrameKitMetals.Astrolabe, 0xEAD3A0, 0xB8924E, 0x7C6034, 0x4E3B1E);

        // On Night a kit's metal replaces the ornament and the gauge arc, and nothing else of the gauge.
        var night = UiPalettes.Night;
        var gauges = FrameKitMetals.Gauges(night, FrameKitId.Silver);
        Assert.Equal(FrameKitMetals.Silver.Ornament, FrameKitMetals.Ornament(night, FrameKitId.Silver));
        Assert.Equal(FrameKitMetals.Silver.ArcBase, gauges.ArcBase);
        Assert.Equal(night.Gauges.Groove, gauges.Groove);
        Assert.Equal(night.Gauges.MoonLit, gauges.MoonLit);
        Assert.Equal(night.Gauges.Knob, gauges.Knob);

        // A light palette keeps its own inks (designed to read at 3 : 1 on snow), and so does high contrast.
        Assert.Equal(UiPalettes.IshgardSnow.Brass, FrameKitMetals.Ornament(UiPalettes.IshgardSnow, FrameKitId.Astrolabe));
        Assert.Equal(UiPalettes.IshgardSnow.Gauges, FrameKitMetals.Gauges(UiPalettes.IshgardSnow, FrameKitId.Came));
        Assert.Equal(night.HighContrast.Gauges, FrameKitMetals.Gauges(night.HighContrast, FrameKitId.Silver));
    }

    [Fact]
    public void Every_kits_ornament_and_gauge_arc_read_on_the_dark_windows()
    {
        // Theme-system §7.2 "Frame kit tests": ornament ink at least 3 : 1 on each palette window it recolours; the arc
        // at least 3 : 1 on the window too, as Night's gilt is.
        var windows = new[] { UiPalettes.Night.Surface.Window, ColorMath.FromHex(0x1A1526), ColorMath.FromHex(0x16100F) };
        foreach (var metal in new[] { FrameKitMetals.Silver, FrameKitMetals.Came, FrameKitMetals.Astrolabe })
        {
            foreach (var window in windows)
            {
                Assert.True(ColorMath.Contrast(metal.Ornament.Body, window) >= 3.0, $"{metal.Ornament.Body} on {window}");
                Assert.True(ColorMath.Contrast(metal.Ornament.High, window) >= 3.0, $"{metal.Ornament.High} on {window}");
                Assert.True(ColorMath.Contrast(metal.ArcBase, window) >= 3.0, $"{metal.ArcBase} on {window}");
            }
        }
    }

    [Fact]
    public void Kit_metals_are_resolved_without_allocating()
    {
        _ = FrameKitMetals.Gauges(UiPalettes.Night, FrameKitId.Came);
        _ = FrameKitMetals.Ornament(UiPalettes.Night, FrameKitId.Astrolabe);
        _ = ResolvedAppearance.Default.Composes(GlyphSetId.Medallion);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            _ = FrameKitMetals.Gauges(UiPalettes.Night, FrameKitId.Came);
            _ = FrameKitMetals.Ornament(UiPalettes.Night, FrameKitId.Astrolabe);
            _ = ResolvedAppearance.Default.Composes(GlyphSetId.Medallion);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertRamp(KitMetal metal, uint high, uint body, uint shadow, uint deep)
    {
        Assert.Equal(ColorMath.FromHex(high), metal.Ornament.High);
        Assert.Equal(ColorMath.FromHex(body), metal.Ornament.Body);
        Assert.Equal(ColorMath.FromHex(shadow), metal.Ornament.Shadow);
        Assert.Equal(ColorMath.FromHex(deep), metal.Ornament.Deep);
        Assert.Equal([ColorMath.FromHex(high), ColorMath.FromHex(body), ColorMath.FromHex(shadow), ColorMath.FromHex(deep)], metal.OuterSlope.Select(static s => s.Color));
    }
}
