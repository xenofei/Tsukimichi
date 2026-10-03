using System.Numerics;
using System.Text.Json;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The giver portrait plate in the plugin (feature plan v7 F2, F5; docs/design/v7/ui/spec-1.15.md A3–A7): the night grade
/// against the spec's matrices, the keep mask and the small copy, the atlas against its generator's JSON and PNGs, and
/// the plate's rules: which face or fallback a plate shows at each size, initials, the spoiler rule, the tooltip size,
/// the plate geometry and the fade.
/// </summary>
public sealed class PortraitPlateTests
{
    // ------------------------------------------------------------------ the grade (A3)

    [Fact]
    public void The_colour_matrix_is_the_specs_rebuilt_from_its_four_steps()
    {
        AssertMatrix(
            PortraitGrade.For(PortraitGradeFamily.Colour),
            [.6162f, .1372f, .0138f, .0147f, .0413f, .7224f, .0140f, .0196f, .0435f, .1462f, .6279f, .0353f]);
    }

    [Fact]
    public void The_battle_dialogue_matrix_is_the_specs_rebuilt_from_its_four_steps()
    {
        AssertMatrix(
            PortraitGrade.For(PortraitGradeFamily.BattleTalk),
            [.4842f, .2856f, .0288f, .0147f, .0858f, .6923f, .0291f, .0196f, .0893f, .3003f, .4502f, .0353f]);
    }

    [Fact]
    public void The_tint_until_the_copy_lands_is_the_night_multiply_alone()
    {
        AssertClose(new Vector3(.816f, .827f, .870f), PortraitGrade.TintFor(PortraitSource.TripleTriadCard), 0.0015f);
        AssertClose(new Vector3(.850f, .859f, .893f), PortraitGrade.TintFor(PortraitSource.BattleTalk), 0.0015f);
        Assert.Equal(PortraitGrade.TintFor(PortraitSource.TrustBust), PortraitGrade.TintFor(PortraitSource.Delivery));
        Assert.Equal(Vector3.One, PortraitGrade.TintFor(PortraitSource.TrustBust, nightMultiply: false));
    }

    [Fact]
    public void A_light_palette_skips_the_night_multiply_but_keeps_the_desaturation_and_the_lift()
    {
        var light = PortraitGrade.For(PortraitGradeFamily.Colour, nightMultiply: false);
        var night = PortraitGrade.For(PortraitGradeFamily.Colour);
        Assert.Equal(night.Ro, light.Ro);
        Assert.Equal(night.Bo, light.Bo);

        // A grey stays grey (the desaturation leaves it be), scaled by .94 and lifted.
        var grey = light.Apply(new Vector3(0.5f));
        Assert.Equal(grey.X - light.Ro, grey.Y - light.Go, 3);
        Assert.Equal(0.5f * PortraitGrade.Scale, grey.Y - light.Go, 3);

        // Pure red loses a quarter of its saturation toward luma.
        var red = light.Apply(Vector3.UnitX);
        Assert.Equal(((1f - .25f) + (.25f * .2126f)) * .94f, red.X - light.Ro, 3);
    }

    [Fact]
    public void Grading_applies_the_matrix_and_keeps_alpha()
    {
        byte[] pixels = [255, 255, 255, 200, 0, 0, 0, 255];
        var m = PortraitGrade.For(PortraitGradeFamily.Colour);
        PortraitGrade.GradeInPlace(pixels, 2, 1, 8, bgra: false, m);
        var white = m.Apply(Vector3.One);
        Assert.Equal((byte)Math.Round(white.X * 255f), pixels[0]);
        Assert.Equal((byte)Math.Round(white.Z * 255f), pixels[2]);
        Assert.Equal(200, pixels[3]);

        // Black is lifted by Night × .25 (#0F1424 × .25 = 4, 5, 9).
        Assert.Equal(4, pixels[4]);
        Assert.Equal(5, pixels[5]);
        Assert.Equal(9, pixels[6]);
        Assert.Equal(255, pixels[7]);
    }

    [Fact]
    public void A_delivery_portrait_draws_only_through_its_keep_mask()
    {
        // A 2 × 2 crop at (1, 1) of a 3 × 3 mask that keeps only (1, 1) and (2, 2).
        bool[] keep = [false, false, false, false, true, false, false, false, true];
        var pixels = Enumerable.Repeat((byte)255, 16).ToArray();
        PortraitGrade.GradeInPlace(pixels, 2, 2, 8, bgra: true, PortraitGrade.For(PortraitGradeFamily.Colour), keep, 3, 1, 1);
        Assert.Equal(255, pixels[3]);
        Assert.Equal([0, 0, 0, 0], pixels[4..8]);
        Assert.Equal([0, 0, 0, 0], pixels[8..12]);
        Assert.Equal(255, pixels[15]);

        // A pixel outside the mask is never kept.
        var outside = Enumerable.Repeat((byte)255, 4).ToArray();
        PortraitGrade.GradeInPlace(outside, 1, 1, 4, bgra: true, PortraitGrade.For(PortraitGradeFamily.Colour), keep, 3, 5, 5);
        Assert.Equal(0, outside[3]);
    }

    [Fact]
    public void The_small_copy_is_an_area_average_that_never_darkens_a_transparent_edge()
    {
        // 4 × 4: the left half opaque red, the right half transparent black. Down to 2 × 2: the left column red, the
        // right one clear; to 1 × 1: half-transparent red, not a dark red.
        var pixels = new byte[4 * 4 * 4];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                var o = ((y * 4) + x) * 4;
                pixels[o] = 255;
                pixels[o + 3] = 255;
            }
        }

        var half = PortraitGrade.Downsample(pixels, 4, 4, 16, 2, 2);
        Assert.Equal([255, 0, 0, 255], half[0..4]);
        Assert.Equal([0, 0, 0, 0], half[4..8]);

        var one = PortraitGrade.Downsample(pixels, 4, 4, 16, 1, 1);
        Assert.Equal(255, one[0]);
        Assert.InRange(one[3], 127, 128);

        // A 3 → 2 resample weighs the middle pixel half to each side.
        byte[] row = [0, 0, 0, 255, 90, 90, 90, 255, 180, 180, 180, 255];
        var two = PortraitGrade.Downsample(row, 3, 1, 12, 2, 1);
        Assert.Equal(30, two[0]);
        Assert.Equal(150, two[4]);
    }

    // ------------------------------------------------------------------ the atlas

    [Fact]
    public void Atlas_layout_matches_the_generated_json()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(OrnamentLayoutTests.AssetsDir(), "portraits.json")));
        var root = json.RootElement;
        Assert.Equal(PortraitAtlasLayout.Width, root.GetProperty("size")[0].GetInt32());
        Assert.Equal(PortraitAtlasLayout.Height, root.GetProperty("size")[1].GetInt32());
        Assert.Equal(PortraitAtlasLayout.Tiers, root.GetProperty("tiers").EnumerateArray().Select(static t => t.GetInt32()).ToArray());
        var sprites = root.GetProperty("sprites");
        Assert.Equal(PortraitAtlasLayout.SpriteCount, sprites.EnumerateObject().Count());
        foreach (var sprite in Enum.GetValues<PortraitSprite>())
        {
            Assert.True(sprites.TryGetProperty(PortraitAtlasLayout.Key(sprite), out var tiers), $"portraits.json has no sprite {PortraitAtlasLayout.Key(sprite)}");
            foreach (var tier in PortraitAtlasLayout.Tiers)
            {
                var r = tiers.GetProperty(tier.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32()), PortraitAtlasLayout.Rect(sprite, tier));
            }
        }
    }

    [Fact]
    public void Atlas_pngs_are_the_layout_size_and_2x_and_stay_small()
    {
        var one = Path.Combine(OrnamentLayoutTests.AssetsDir(), "portraits.png");
        var two = Path.Combine(OrnamentLayoutTests.AssetsDir(), "portraits@2x.png");
        Assert.Equal((PortraitAtlasLayout.Width, PortraitAtlasLayout.Height), OrnamentLayoutTests.PngSize(one));
        Assert.Equal((PortraitAtlasLayout.Width * 2, PortraitAtlasLayout.Height * 2), OrnamentLayoutTests.PngSize(two));
        Assert.True(new FileInfo(one).Length + new FileInfo(two).Length < 1024 * 1024, "the embedded portrait atlases stay under 1 MB");
    }

    [Fact]
    public void Atlas_sprites_fit_and_never_touch()
    {
        var rects = Enum.GetValues<PortraitSprite>().SelectMany(static s => PortraitAtlasLayout.Tiers.Select(t => PortraitAtlasLayout.Rect(s, t))).ToArray();
        foreach (var r in rects)
        {
            Assert.True(r.Width > 0 && r.X >= 1 && r.Y >= 1 && r.X + r.Width <= PortraitAtlasLayout.Width - 1 && r.Y + r.Height <= PortraitAtlasLayout.Height - 1, r.ToString());
        }

        for (var i = 0; i < rects.Length; i++)
        {
            for (var j = i + 1; j < rects.Length; j++)
            {
                var a = rects[i];
                var b = rects[j];
                Assert.True(a.X + a.Width + 1 <= b.X || b.X + b.Width + 1 <= a.X || a.Y + a.Height + 1 <= b.Y || b.Y + b.Height + 1 <= a.Y, $"{a} and {b} touch");
            }
        }
    }

    [Fact]
    public void Every_silhouette_comes_from_its_approved_svg()
    {
        var folder = Path.Combine(OrnamentLayoutTests.RepoRoot(), "docs", "design", "v7", "ui", "1.15", "silhouettes");
        foreach (var sprite in Enum.GetValues<PortraitSprite>().Where(static s => s != PortraitSprite.PlateShade))
        {
            Assert.True(File.Exists(Path.Combine(folder, PortraitAtlasLayout.Key(sprite) + ".svg")), PortraitAtlasLayout.Key(sprite));
        }
    }

    [Theory]
    [InlineData(1, 0, PortraitSprite.HyurMale)]
    [InlineData(1, 1, PortraitSprite.HyurFemale)]
    [InlineData(4, 1, PortraitSprite.MiqoteFemale)]
    [InlineData(6, 0, PortraitSprite.AuRaMale)]
    [InlineData(8, 1, PortraitSprite.VieraFemale)]
    [InlineData(0, 0, PortraitSprite.MoonDisc)]
    [InlineData(9, 1, PortraitSprite.MoonDisc)]
    public void Races_map_to_their_silhouette(byte race, byte gender, PortraitSprite sprite) =>
        Assert.Equal(sprite, PortraitAtlasLayout.Silhouette(race, gender));

    [Theory]
    [InlineData(18f, 24, false)]
    [InlineData(24f, 24, false)]
    [InlineData(64f, 72, false)]
    [InlineData(72f, 72, false)]
    [InlineData(120f, 128, false)]
    [InlineData(144f, 72, true)]
    [InlineData(256f, 128, true)]
    [InlineData(400f, 128, true)]
    public void The_tier_is_the_smallest_cell_at_or_above_the_plate(float px, int tier, bool twoX) =>
        Assert.Equal((tier, twoX), PortraitAtlasLayout.Pick(px));

    // ------------------------------------------------------------------ what a plate shows (A6)

    private static PortraitRef Ref(PortraitFallbackKind kind, byte race = 0, string initials = "", uint society = 0, uint icon = 87019, PortraitSource source = PortraitSource.TripleTriadCard, byte era = 0) =>
        new(1, icon, source, PortraitCrop.FromBox(source, 70, 62, 75), era, new PortraitFallback(kind, society, race, 1, initials));

    [Fact]
    public void A_face_shows_when_there_is_art_and_it_is_allowed()
    {
        Assert.Equal(PortraitShow.Face, PortraitPlate.Choose(Ref(PortraitFallbackKind.Initials, 3, "TT"), faceAllowed: true, 72f));
        Assert.Equal(PortraitShow.Face, PortraitPlate.Choose(Ref(PortraitFallbackKind.Initials, 3, "TT"), faceAllowed: true, 18f));
    }

    [Fact]
    public void A_hidden_face_falls_back_to_the_silhouette_or_to_initials_when_the_race_is_unknown()
    {
        Assert.Equal(PortraitShow.Silhouette, PortraitPlate.Choose(Ref(PortraitFallbackKind.Initials, 3, "TT"), faceAllowed: false, 72f));
        Assert.Equal(PortraitShow.Initials, PortraitPlate.Choose(Ref(PortraitFallbackKind.Initials, 0, "TT"), faceAllowed: false, 72f));
        Assert.Equal(PortraitShow.MoonDisc, PortraitPlate.Choose(Ref(PortraitFallbackKind.Initials, 0, "TT"), faceAllowed: false, 18f));
    }

    [Fact]
    public void Without_art_the_fallback_follows_the_index_and_the_size()
    {
        var none = Ref(PortraitFallbackKind.Initials, 3, "MM", icon: 0, source: PortraitSource.None);
        Assert.False(none.HasArt);
        Assert.Equal(PortraitShow.Initials, PortraitPlate.Choose(none, faceAllowed: true, 72f));
        Assert.Equal(PortraitShow.Initials, PortraitPlate.Choose(none, faceAllowed: true, 24f));
        Assert.Equal(PortraitShow.Initials, PortraitPlate.Choose(none, faceAllowed: true, 20f));
        Assert.Equal(PortraitShow.MoonDisc, PortraitPlate.Choose(none, faceAllowed: true, 18f));

        var society = Ref(PortraitFallbackKind.SocietyEmblem, 0, string.Empty, society: 65016, icon: 0, source: PortraitSource.None);
        Assert.Equal(PortraitShow.SocietyEmblem, PortraitPlate.Choose(society, faceAllowed: false, 18f));
        var generic = Ref(PortraitFallbackKind.Silhouette, 8, string.Empty, icon: 0, source: PortraitSource.None);
        Assert.Equal(PortraitShow.Silhouette, PortraitPlate.Choose(generic, faceAllowed: false, 24f));
        var moogle = Ref(PortraitFallbackKind.Moon, 0, string.Empty, icon: 0, source: PortraitSource.None);
        Assert.Equal(PortraitShow.MoonDisc, PortraitPlate.Choose(moogle, faceAllowed: false, 72f));
        Assert.Equal(PortraitShow.MoonDisc, PortraitPlate.Choose(default, faceAllowed: true, 72f));
    }

    [Theory]
    [InlineData("MM", 72f, 2)]
    [InlineData("MM", 32f, 2)]
    [InlineData("MM", 31f, 1)]
    [InlineData("MM", 24f, 1)]
    [InlineData("MM", 20f, 1)]
    [InlineData("MM", 18f, 0)]
    [InlineData("G", 72f, 1)]
    [InlineData("", 72f, 0)]
    public void Initials_are_two_letters_from_32_px_one_from_20_and_the_moon_below(string initials, float size, int letters) =>
        Assert.Equal(letters, PortraitPlate.InitialLetters(initials, size));

    [Fact]
    public void A_lone_initial_on_a_small_plate_has_caps_of_at_least_9_px()
    {
        Assert.True(PortraitPlate.InitialsFontLogical(1, 24f) * PortraitPlate.CapHeightEm >= PortraitPlate.InitialCapsMinLogical);
        Assert.True(PortraitPlate.InitialsFontLogical(1, 20f) * PortraitPlate.CapHeightEm >= PortraitPlate.InitialCapsMinLogical);
        Assert.Equal(26f, PortraitPlate.InitialsFontLogical(2, 72f), 3);
        Assert.Equal(30f, PortraitPlate.InitialsFontLogical(1, 72f), 3);
    }

    [Theory]
    [InlineData(2, false, false, 0, true)]
    [InlineData(2, true, false, 5, false)]
    [InlineData(2, false, true, 2, true)]
    [InlineData(3, false, true, 2, false)]
    [InlineData(3, false, false, 0, true)]
    [InlineData(5, false, true, 255, true)]
    public void A_face_shows_only_unmasked_and_never_from_beyond_the_story_while_the_shield_is_on(byte era, bool masked, bool shield, byte reach, bool allowed) =>
        Assert.Equal(allowed, PortraitPlate.FaceAllowed(era, masked, shield, reach));

    // ------------------------------------------------------------------ sizes, geometry and the fade (A4, A5, A7)

    [Fact]
    public void The_tooltip_is_128_px_but_never_more_than_1_6_times_a_faces_box()
    {
        // Tataru's 75 px card box shows at 120 px; a 140 px box at the full 128; a fallback at 128.
        Assert.Equal(120f, PortraitPlate.TooltipSize(Ref(PortraitFallbackKind.Initials, 3, "TT"), face: true));
        var bust = new PortraitRef(1, 72621, PortraitSource.TrustBust, PortraitCrop.FromBox(PortraitSource.TrustBust, 10, 107, 143), 0, default);
        Assert.Equal(PortraitPlate.TooltipMax, PortraitPlate.TooltipSize(bust, face: true));
        Assert.Equal(PortraitPlate.TooltipMax, PortraitPlate.TooltipSize(Ref(PortraitFallbackKind.Initials, 3, "TT"), face: false));
    }

    [Fact]
    public void The_card_plate_is_72_at_full_64_at_quiet_and_18_inline_at_plain()
    {
        Assert.Equal((72f, 14f), (PortraitPlate.CardSize(Flair.Full), PortraitPlate.CardGap(Flair.Full)));
        Assert.Equal((64f, 12f), (PortraitPlate.CardSize(Flair.Quiet), PortraitPlate.CardGap(Flair.Quiet)));
        Assert.Equal((18f, 6f), (PortraitPlate.CardSize(Flair.Plain), PortraitPlate.CardGap(Flair.Plain)));
    }

    [Fact]
    public void The_face_square_is_69_units_at_1_5_and_the_emblem_64_percent_centred()
    {
        var (min, max) = PortraitPlate.FaceRect(new Vector2(10f, 20f), 72f);
        Assert.Equal(new Vector2(11.5f, 21.5f), min);
        Assert.Equal(new Vector2(80.5f, 90.5f), max);

        var (tileMin, tileMax) = PortraitPlate.EmblemRect(Vector2.Zero, 100f);
        Assert.Equal(64f, tileMax.X - tileMin.X, 3);
        Assert.Equal(18f, tileMin.X, 3);
    }

    [Fact]
    public void Small_plates_take_the_small_copy()
    {
        Assert.True(PortraitPlate.UseSmallCopy(18f));
        Assert.True(PortraitPlate.UseSmallCopy(48f));
        Assert.False(PortraitPlate.UseSmallCopy(64f));
    }

    [Fact]
    public void The_face_fades_in_over_0_3_s_ease_out_cubic()
    {
        Assert.Equal(0.3f, MotionTokens.ArtFade);
        Assert.Equal(0f, PortraitPlate.FadeAlpha(0d));
        Assert.Equal(0f, PortraitPlate.FadeAlpha(-1d));
        Assert.Equal(1f - (8f / 27f), PortraitPlate.FadeAlpha(0.1d), 3);
        Assert.Equal(1f, PortraitPlate.FadeAlpha(0.3d), 4);
        Assert.Equal(1f, PortraitPlate.FadeAlpha(5d));
        var last = 0f;
        for (var t = 0.0; t <= 0.3; t += 0.01)
        {
            var a = PortraitPlate.FadeAlpha(t);
            Assert.True(a >= last);
            last = a;
        }
    }

    private static void AssertMatrix(ColorMatrix m, float[] expected)
    {
        float[] actual = [m.Rr, m.Rg, m.Rb, m.Ro, m.Gr, m.Gg, m.Gb, m.Go, m.Br, m.Bg, m.Bb, m.Bo];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(MathF.Abs(expected[i] - actual[i]) <= 0.0006f, $"entry {i}: expected {expected[i]}, got {actual[i]}");
        }
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance) =>
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"expected {expected}, got {actual}");
}
