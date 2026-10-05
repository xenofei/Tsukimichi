using Tsukimichi.DataGen;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The portrait pack builder's framing (Tsukimichi.DataGen, the portrait audit's changes C3 and C9): the head finder on
/// synthetic silhouettes (a plain figure, and the same figure with a raised staff, a lance beside the head or a staff
/// held overhead, none of which may pass as the crown), the hold at the 72 px minimum, and the per-NPC boxes that
/// replace the head finder's square. No network, no game, no Garland photo.
/// </summary>
public sealed class PortraitPackBuildTests
{
    private const int Width = 380;
    private const int Height = 706;
    private const byte Hyur = 1;
    private const int Axis = 190;
    private const int HeadTop = 60;
    private const int Feet = 660;

    // ------------------------------------------------------------------ the head finder (C3)

    [Fact]
    public void A_plain_figure_is_framed_with_the_eyes_on_the_eye_line_and_the_chin_on_the_chin_line()
    {
        var box = PortraitHeadCrop.Find(Figure(), Width, Height, Hyur);

        Assert.NotNull(box);
        var head = PortraitHeadCrop.HeadShare(Hyur) * (Feet - HeadTop);
        var eyes = box.Value.Top + (PortraitHeadCrop.EyeLine * box.Value.Side);
        var chin = box.Value.Top + (PortraitHeadCrop.ChinLine * box.Value.Side);

        // The synthetic head is an ellipse from HeadTop down a head and a little: the eyes sit in its upper middle,
        // the chin at its bottom, the square centred on it and about a head across.
        Assert.InRange(eyes, HeadTop + (0.4 * head), HeadTop + (0.65 * head));
        Assert.InRange(chin, HeadBottom - (0.1 * head), HeadBottom + (0.1 * head));
        Assert.InRange(box.Value.Left + (box.Value.Side / 2), Axis - 2, Axis + 2);
        Assert.InRange(box.Value.Side, 0.9 * head, 1.2 * head);
    }

    [Theory]
    [InlineData("staff raised beside the head")]
    [InlineData("lance beside the head")]
    [InlineData("staff held overhead")]
    public void A_weapon_above_or_beside_the_head_does_not_move_the_frame(string weapon)
    {
        var plain = PortraitHeadCrop.Find(Figure(), Width, Height, Hyur);
        var armed = Figure();
        switch (weapon)
        {
            case "staff raised beside the head":
                // A tall staff in the right hand, its wide head ornament (wider than a head) high above the head and
                // to one side: by row widths alone it is the first head-wide row, and it makes the figure taller.
                Fill(armed, 127, 5, 134, 420);
                Ellipse(armed, 130, 25, 34, 20);
                break;
            case "lance beside the head":
                // A lance held upright at the hip beside the head, a few px clear of the hair, its blade above the head.
                Fill(armed, Axis + 37, 0, Axis + 44, 300);
                Fill(armed, Axis + 25, 0, Axis + 56, 30);
                break;
            default:
                // A staff straight up from the head, a head-wide ornament on top of a thin shaft.
                Ellipse(armed, Axis, 12, 22, 10);
                Fill(armed, Axis - 3, 12, Axis + 3, HeadTop + 8);
                break;
        }

        var box = PortraitHeadCrop.Find(armed, Width, Height, Hyur);

        Assert.NotNull(plain);
        Assert.NotNull(box);
        Assert.InRange(box.Value.Left, plain.Value.Left - 1.5, plain.Value.Left + 1.5);
        Assert.InRange(box.Value.Top, plain.Value.Top - 1.5, plain.Value.Top + 1.5);
        Assert.InRange(box.Value.Side, plain.Value.Side - 1.5, plain.Value.Side + 1.5);
    }

    [Fact]
    public void An_empty_photo_has_no_head()
    {
        Assert.Null(PortraitHeadCrop.Find(new byte[Width * Height * 4], Width, Height, Hyur));
    }

    [Fact]
    public void A_head_under_the_minimum_is_held_at_72_px_with_its_eye_line_and_centre_kept()
    {
        var small = new PhotoBox(100, 50, 40);

        var held = small.AtLeast(PortraitPackBuilder.MinBox);

        Assert.Equal(PortraitPackBuilder.MinBox, held.Side);
        Assert.Equal(small.Top + (PortraitHeadCrop.EyeLine * small.Side), held.Top + (PortraitHeadCrop.EyeLine * held.Side), 6);
        Assert.Equal(small.Left + (small.Side / 2), held.Left + (held.Side / 2), 6);
        Assert.Equal(new PhotoBox(10, 20, 100), new PhotoBox(10, 20, 100).AtLeast(PortraitPackBuilder.MinBox));
    }

    [Fact]
    public void A_small_figure_is_cut_at_72_px_rather_than_left_out()
    {
        // Half the size: its head finder square is well under 72 px.
        var photo = new byte[Width * Height * 4];
        Ellipse(photo, Axis, 230 + 20, 15, 20);
        Fill(photo, Axis - 6, 266, Axis + 6, 276);
        Fill(photo, Axis - 35, 275, Axis + 35, 400);
        Fill(photo, Axis - 28, 400, Axis - 4, 530);
        Fill(photo, Axis + 4, 400, Axis + 28, 530);

        var crop = PortraitPackBuilder.CropPhoto(photo, Width, Height, Hyur, [1000001], PortraitPackOverrides.None);

        Assert.NotNull(crop.Head);
        Assert.True(crop.Head.Value.Side < PortraitPackBuilder.MinBox);
        Assert.NotNull(crop.Rgba);
        Assert.False(crop.FromOverride);
        Assert.Equal(PortraitPackBuilder.MinBox, crop.Box.Side);
    }

    // ------------------------------------------------------------------ per-NPC boxes (C9)

    [Fact]
    public void A_per_NPC_box_replaces_the_head_finders_square_for_every_giver_who_shares_the_photo()
    {
        var photo = Figure();
        var overrides = Overrides("""{ "overrides": { "1000002": { "photoBox": [100.5, 40, 150], "note": "owner adjust" } } }""");

        var crop = PortraitPackBuilder.CropPhoto(photo, Width, Height, Hyur, [1000001, 1000002], overrides);
        var plain = PortraitPackBuilder.CropPhoto(photo, Width, Height, Hyur, [1000001], overrides);

        Assert.Null(crop.Problem);
        Assert.True(crop.FromOverride);
        Assert.Equal(new PhotoBox(100.5, 40, 150), crop.Box);
        Assert.Equal(PortraitHeadCrop.Render(photo, Width, Height, new PhotoBox(100.5, 40, 150), 128), crop.Rgba);
        Assert.NotNull(crop.Head);

        Assert.False(plain.FromOverride);
        Assert.Equal(plain.Head, plain.Box);
        Assert.NotEqual(crop.Box, plain.Box);
    }

    [Fact]
    public void A_per_NPC_box_is_kept_even_where_the_frame_would_be_dropped_as_empty()
    {
        // The owner keeps faceless subjects (a helmet, a mask): his box frames what he chose, mostly empty or not.
        var overrides = Overrides("""{ "overrides": { "1000001": { "photoBox": [0, 0, 100], "note": "empty corner" } } }""");

        var crop = PortraitPackBuilder.CropPhoto(Figure(), Width, Height, Hyur, [1000001], overrides);

        Assert.NotNull(crop.Rgba);
        Assert.True(PortraitHeadCrop.Filled(crop.Rgba, 128) < PortraitHeadCrop.MinFilled);
    }

    [Theory]
    [InlineData(-1, 40, 150)]
    [InlineData(100, -0.5, 150)]
    [InlineData(300, 40, 81)]
    [InlineData(100, 600, 107)]
    [InlineData(0, 0, 707)]
    [InlineData(100, 40, 71.9)]
    public void A_per_NPC_box_outside_the_photo_or_under_72_px_is_refused(double x, double y, double side)
    {
        var json = $$"""{ "overrides": { "1000001": { "photoBox": [{{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, {{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, {{side.ToString(System.Globalization.CultureInfo.InvariantCulture)}}] } } }""";
        var overrides = Overrides(json);

        var crop = PortraitPackBuilder.CropPhoto(Figure(), Width, Height, Hyur, [1000001], overrides);

        Assert.NotNull(crop.Problem);
        Assert.Null(crop.Rgba);
        Assert.NotNull(PortraitPackOverrides.Check(new PhotoBox(x, y, side), Width, Height, PortraitPackBuilder.MinBox));
    }

    [Fact]
    public void A_per_NPC_box_flush_with_the_photos_edges_is_accepted()
    {
        Assert.Null(PortraitPackOverrides.Check(new PhotoBox(0, 0, Width), Width, Height, PortraitPackBuilder.MinBox));
        Assert.Null(PortraitPackOverrides.Check(new PhotoBox(Width - 72, Height - 72, 72), Width, Height, PortraitPackBuilder.MinBox));
    }

    [Fact]
    public void Givers_who_share_a_photo_but_disagree_on_its_box_are_refused()
    {
        var overrides = Overrides("""
            { "overrides": {
                "1000001": { "photoBox": [100, 40, 150] },
                "1000002": { "photoBox": [100, 41, 150] } } }
            """);

        var crop = PortraitPackBuilder.CropPhoto(Figure(), Width, Height, Hyur, [1000001, 1000002], overrides);

        Assert.Null(crop.Rgba);
        Assert.Contains("1000001 and 1000002", crop.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_per_NPC_box_for_an_NPC_that_is_no_quest_giver_is_reported()
    {
        var overrides = Overrides("""{ "overrides": { "1000001": { "photoBox": [100, 40, 150] }, "4242": { "photoBox": [100, 40, 150] } } }""");

        Assert.Equal([4242u], overrides.Unknown(new HashSet<uint> { 1000001, 1000003 }));
        Assert.Empty(overrides.Unknown(new HashSet<uint> { 1000001, 4242 }));
    }

    [Fact]
    public void The_overrides_file_names_every_entry_it_cannot_read()
    {
        var overrides = PortraitPackOverrides.Parse("""
            { "overrides": {
                "1000001": { "photoBox": [100, 40, 150], "note": "fine" },
                "Y'shtola": { "photoBox": [100, 40, 150] },
                "1000002": { "photoBox": [100, 40] },
                "1000003": { "photoBox": [100, 40, 0] },
                "1000004": { "photoBox": ["100", 40, 150] } } }
            """, out var errors);

        Assert.Equal([1000001u], overrides.Entries.Keys);
        Assert.Equal("fine", overrides.Entries[1000001].Note);
        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, e => e.Contains("Y'shtola", StringComparison.Ordinal));

        PortraitPackOverrides.Parse("{ }", out var noOverrides);
        Assert.Single(noOverrides);
        PortraitPackOverrides.Parse("not json", out var notJson);
        Assert.Single(notJson);
    }

    [Fact]
    public void The_shipped_overrides_file_reads_cleanly_and_every_box_is_at_least_72_px()
    {
        var path = Path.Combine(RepoRoot(), "tools", "portrait-pack", "portrait_pack_overrides.json");

        var overrides = PortraitPackOverrides.Load(path, out var errors);

        Assert.Empty(errors);
        Assert.NotEmpty(overrides.Entries);
        Assert.All(overrides.Entries.Values, e => Assert.True(e.Box.Side >= PortraitPackBuilder.MinBox && e.Box.Inside(Width, Height), e.Note));
    }

    // ------------------------------------------------------------------ helpers

    private const int HeadBottom = HeadTop + 78;

    private static PortraitPackOverrides Overrides(string json)
    {
        var overrides = PortraitPackOverrides.Parse(json, out var errors);
        Assert.Empty(errors);
        return overrides;
    }

    /// <summary>A front-facing figure on transparency: an elliptic head, a neck, a torso and two legs, feet at <see cref="Feet"/>.</summary>
    private static byte[] Figure()
    {
        var rgba = new byte[Width * Height * 4];
        Ellipse(rgba, Axis, HeadTop + 39, 29, 39);
        Fill(rgba, Axis - 12, 130, Axis + 12, 150);
        Fill(rgba, Axis - 70, 148, Axis + 70, 400);
        Fill(rgba, Axis - 55, 400, Axis - 8, Feet);
        Fill(rgba, Axis + 8, 400, Axis + 55, Feet);
        return rgba;
    }

    private static void Fill(byte[] rgba, int x0, int y0, int x1, int y1)
    {
        for (var y = Math.Max(0, y0); y <= Math.Min(Height - 1, y1); y++)
        {
            for (var x = Math.Max(0, x0); x <= Math.Min(Width - 1, x1); x++)
            {
                Paint(rgba, x, y);
            }
        }
    }

    private static void Ellipse(byte[] rgba, int cx, int cy, int rx, int ry)
    {
        for (var y = cy - ry; y <= cy + ry; y++)
        {
            for (var x = cx - rx; x <= cx + rx; x++)
            {
                var dx = (x - cx) / (double)rx;
                var dy = (y - cy) / (double)ry;
                if ((dx * dx) + (dy * dy) <= 1 && x >= 0 && y >= 0 && x < Width && y < Height)
                {
                    Paint(rgba, x, y);
                }
            }
        }
    }

    private static void Paint(byte[] rgba, int x, int y)
    {
        var o = ((y * Width) + x) * 4;
        rgba[o] = 200;
        rgba[o + 1] = 170;
        rgba[o + 2] = 150;
        rgba[o + 3] = 255;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tsukimichi.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Tsukimichi.sln not found above the test output");
    }
}
