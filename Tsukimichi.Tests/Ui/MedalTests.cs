using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Menphina's Medallion in the plugin (feature plan v6 G1, G2): the atlas layout against the generator's
/// <c>medals.json</c> and the PNGs, the tier rule, the shared geometry against the SVG masters, no mark on a lit moon,
/// the high-contrast token swap's contrast ladder, the round 5 tokens against gen5.py, and the vector meshes.
/// </summary>
public sealed class MedalTests
{
    private static string Round5Dir() => Path.Combine(OrnamentLayoutTests.RepoRoot(), "docs", "design", "moon-v6", "round5", "medallion-r5");

    // ------------------------------------------------------------------ the atlas

    [Fact]
    public void Layout_matches_the_generated_json()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(OrnamentLayoutTests.AssetsDir(), "medals.json")));
        var root = json.RootElement;
        Assert.Equal(MedalLayout.Width, root.GetProperty("size")[0].GetInt32());
        Assert.Equal(MedalLayout.Height, root.GetProperty("size")[1].GetInt32());
        Assert.Equal(MedalLayout.Tiers, root.GetProperty("tiers").EnumerateArray().Select(static t => t.GetInt32()).ToArray());

        var sprites = root.GetProperty("sprites");
        Assert.Equal(MedalLayout.SpriteCount, sprites.EnumerateObject().Count());
        foreach (var sprite in Enum.GetValues<MedalSprite>())
        {
            Assert.True(sprites.TryGetProperty(MedalLayout.Key(sprite), out var tiers), $"medals.json has no sprite {MedalLayout.Key(sprite)}");
            foreach (var tier in MedalLayout.Tiers)
            {
                var r = tiers.GetProperty(tier.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32()), MedalLayout.Rect(sprite, tier));
            }
        }
    }

    [Fact]
    public void Atlas_pngs_are_the_layout_size_and_2x_and_stay_small()
    {
        var one = Path.Combine(OrnamentLayoutTests.AssetsDir(), "medals.png");
        var two = Path.Combine(OrnamentLayoutTests.AssetsDir(), "medals@2x.png");
        Assert.Equal((MedalLayout.Width, MedalLayout.Height), OrnamentLayoutTests.PngSize(one));
        Assert.Equal((MedalLayout.Width * 2, MedalLayout.Height * 2), OrnamentLayoutTests.PngSize(two));
        Assert.True(new FileInfo(one).Length + new FileInfo(two).Length < 3 * 1024 * 1024, "the embedded atlases stay under 3 MB");
    }

    [Fact]
    public void Sprites_fit_the_atlas_and_never_touch()
    {
        var rects = Enum.GetValues<MedalSprite>().SelectMany(static s => MedalLayout.Tiers.Select(t => MedalLayout.Rect(s, t))).ToArray();
        foreach (var r in rects)
        {
            Assert.True(r.Width > 0 && r.X >= 1 && r.Y >= 1 && r.X + r.Width <= MedalLayout.Width - 1 && r.Y + r.Height <= MedalLayout.Height - 1, r.ToString());
        }

        for (var i = 0; i < rects.Length; i++)
        {
            for (var j = i + 1; j < rects.Length; j++)
            {
                var a = rects[i];
                var b = rects[j];
                var apart = a.X + a.Width + 1 <= b.X || b.X + b.Width + 1 <= a.X || a.Y + a.Height + 1 <= b.Y || b.Y + b.Height + 1 <= a.Y;
                Assert.True(apart, $"{a} and {b} touch");
            }
        }

        Assert.Equal(default, MedalLayout.Rect(MedalSprite.Ready, 50));
    }

    [Fact]
    public void Every_state_medal_comes_from_its_round_5_master()
    {
        foreach (var sprite in Enum.GetValues<MedalSprite>().Where(static s => s < MedalSprite.OtherJobTank))
        {
            Assert.True(File.Exists(Path.Combine(Round5Dir(), MedalLayout.Key(sprite) + ".svg")), MedalLayout.Key(sprite));
        }

        Assert.True(File.Exists(Path.Combine(OrnamentLayoutTests.RepoRoot(), "docs", "design", "moon-v6", "round5", "gen_atlas.py")));
    }

    [Theory]
    [InlineData(32f)]
    [InlineData(33f)]
    [InlineData(48f)]
    [InlineData(65f)]
    [InlineData(100f)]
    [InlineData(129f)]
    [InlineData(200f)]
    [InlineData(256f)]
    public void Pick_never_shrinks_more_than_one_and_a_half_times_or_enlarges(float size)
    {
        var (tier, twoX) = MedalLayout.Pick(size);
        var texels = twoX ? tier * 2 : tier;
        Assert.True(texels >= size, $"{size} px drawn from {texels}");
        Assert.True(texels / size <= 1.5f, $"{size} px drawn from {texels}");
    }

    [Fact]
    public void Pick_prefers_the_1x_atlas_and_enlarges_only_past_256()
    {
        Assert.Equal((48, false), MedalLayout.Pick(32f));
        Assert.Equal((128, false), MedalLayout.Pick(128f));
        Assert.Equal((96, true), MedalLayout.Pick(150f));
        Assert.Equal((128, true), MedalLayout.Pick(400f));
    }

    [Fact]
    public void Each_state_and_seat_has_its_sprite()
    {
        Assert.Equal(MedalSprite.Ready, MedalLayout.For(QuestState.Ready));
        Assert.Equal(MedalSprite.InJournal, MedalLayout.For(QuestState.Accepted));
        Assert.Equal(MedalSprite.LockedOut, MedalLayout.For(QuestState.Foreclosed));
        Assert.Equal(MedalSprite.NotChecked, MedalLayout.For(QuestState.Unknown));
        Assert.Equal(MedalSprite.OtherJobTank, MedalLayout.For(QuestState.ReadyOnOtherJob, JobSeat.Tank));
        Assert.Equal(MedalSprite.OtherJobHand, MedalLayout.For(QuestState.ReadyOnOtherJob));
        Assert.Equal(MedalLayout.SpriteCount, Enum.GetValues<QuestState>().Select(s => MedalLayout.For(s, JobSeat.Dps)).Distinct().Count() + 3);
    }

    // ------------------------------------------------------------------ the geometry table against the masters

    [Fact]
    public void Shared_geometry_matches_the_svg_masters()
    {
        var ready = File.ReadAllText(Path.Combine(Round5Dir(), "ready.svg"));
        var row = File.ReadAllText(Path.Combine(Round5Dir(), "_row", "ready.svg"));

        // The bezel's keyline ring starts at the keyline radius (gen5 ring(): "M{cx + r1} {cy}"), the well's clip is its radius.
        foreach (var svg in new[] { ready, row })
        {
            Assert.Contains($"M{64 + MedalArt.KeylineRadius:0.##} 64", svg, StringComparison.Ordinal);
            Assert.Contains($"<circle cx=\"64\" cy=\"64\" r=\"{MedalArt.WellRadius:0.##}\"/>", svg, StringComparison.Ordinal);
        }

        // The badge (hero master only): its keyline disc, and none on the row tier.
        var badge = $"<circle cx=\"{MedalArt.BadgeCenter.X:0}\" cy=\"{MedalArt.BadgeCenter.Y:0}\" r=\"{MedalArt.BadgeKeyline:0}\"";
        Assert.Contains(badge, ready, StringComparison.Ordinal);
        Assert.DoesNotContain(badge, row, StringComparison.Ordinal);

        // Completed's check polyline.
        var completed = File.ReadAllText(Path.Combine(Round5Dir(), "completed.svg"));
        Assert.Contains("M64 86L78 100L117.5 40", completed, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ no mark on the lit side

    [Theory]
    [InlineData(QuestState.Ready)]
    [InlineData(QuestState.ReadyOnOtherJob)]
    [InlineData(QuestState.Accepted)]
    [InlineData(QuestState.Blocked)]
    public void No_badge_covers_the_lit_moon(QuestState state)
    {
        Assert.NotEqual(MedalBadge.None, MedalArt.BadgeOf(state));
        var lit = state == QuestState.Blocked
            ? MedalArt.PhaseOutline(MedalArt.BlockedMoon, MedalArt.BlockedMoonRadius, MedalArt.BlockedTerminator, MedalArt.BlockedTilt)
            : MedalArt.PhaseOutline(MedalArt.SceneMoon, MedalArt.SceneMoonRadius, MedalArt.SceneMoonTerminator, MedalArt.SceneMoonTilt);
        var nearest = lit.Min(p => Vector2.Distance(p, MedalArt.BadgeCenter));
        Assert.True(nearest > MedalArt.BadgeKeyline + 2f, $"{state}: the lit moon comes within {nearest:0.0} of the badge's centre");
    }

    [Fact]
    public void The_repeat_arrow_never_crosses_the_lit_half()
    {
        var arrow = MedalArt.RepeatArrow();
        var nearest = arrow.Min(p => Vector2.Distance(p, MedalArt.DoneMoon));
        Assert.True(nearest > MedalArt.DoneMoonRadius + 2f, $"the arrow comes within {nearest:0.0} of the moon's centre");

        // And it stays inside the well, clear of the rim.
        Assert.True(arrow.Max(p => Vector2.Distance(p, MedalArt.Center)) < MedalArt.RimInner);
    }

    [Fact]
    public void States_without_a_badge_draw_none_and_the_badge_breaks_the_silhouette()
    {
        foreach (var state in new[] { QuestState.DoneThisCycle, QuestState.Completed, QuestState.Foreclosed, QuestState.Unknown })
        {
            Assert.Equal(MedalBadge.None, MedalArt.BadgeOf(state));
            Assert.Empty(MedalArt.Badge(MedalArt.BadgeOf(state), JobSeat.Hand, MedalTokens.Standard).Parts);
        }

        // concept.md: the badge's keyline reaches r 67.8 from the centre, past the medal's 63.2, still inside the 128 box.
        var reach = Vector2.Distance(MedalArt.BadgeCenter, MedalArt.Center) + MedalArt.BadgeKeyline;
        Assert.InRange(reach, MedalArt.KeylineRadius + 4f, 64f * MathF.Sqrt(2f));
    }

    // ------------------------------------------------------------------ the contrast ladder (high contrast as a token swap)

    public static TheoryData<string> ContrastPalettes() => ["dark", "light"];

    [Theory]
    [MemberData(nameof(ContrastPalettes))]
    public void Every_flat_ink_reads_on_the_ground(string variant)
    {
        var tokens = variant == "light" ? MedalTokens.HighContrastLight : MedalTokens.HighContrastDark;
        Assert.True(tokens.Flat);
        foreach (var (part, color) in tokens.Inks())
        {
            var ratio = ColorMath.Contrast(color, tokens.Ground);
            Assert.True(ratio >= 3f, $"{variant}: {part} is {ratio:0.00} : 1 on the ground");
        }
    }

    [Theory]
    [MemberData(nameof(ContrastPalettes))]
    public void States_sharing_the_crescent_are_a_ladder_rung_apart(string variant)
    {
        var tokens = variant == "light" ? MedalTokens.HighContrastLight : MedalTokens.HighContrastDark;

        // Ready, Ready on another job and In journal share one crescent; In journal adds the ribbon, so it is Ready and
        // Ready on another job that must part on luminance alone.
        var ready = ColorMath.Contrast(tokens.Ink(QuestState.Ready), tokens.Ink(QuestState.ReadyOnOtherJob));
        var journal = ColorMath.Contrast(tokens.Ink(QuestState.Accepted), tokens.Ink(QuestState.ReadyOnOtherJob));
        Assert.True(ready >= 3f, $"{variant}: Ready vs Ready on another job {ready:0.00} : 1");
        Assert.True(journal >= 3f, $"{variant}: In journal vs Ready on another job {journal:0.00} : 1");

        // The second marks (ribbon, clouds, arrow, check) part from the emblem they sit on.
        Assert.True(ColorMath.Contrast(tokens.Bright, tokens.Ink(QuestState.Accepted)) >= 1.5f || ColorMath.Contrast(tokens.Ground, tokens.Bright) >= 3f);
    }

    [Fact]
    public void Standard_badge_glyphs_read_on_their_seats()
    {
        foreach (var (badge, glyph) in new[]
                 {
                     (MedalBadge.Open, GlyphTokens.MedallionDetail.LockGiltHigh),
                     (MedalBadge.Journal, GlyphTokens.MedallionDetail.LockGiltHigh),
                     (MedalBadge.Closed, GlyphTokens.MedallionDetail.PewterHigh),
                 })
        {
            var (seat, _) = MedalArt.SeatColours(badge, JobSeat.Hand);
            var ratio = ColorMath.Contrast(glyph, seat);
            Assert.True(ratio >= 3f, $"{badge}: glyph on seat {ratio:0.00} : 1");
        }
    }

    [Fact]
    public void Standard_palette_draws_the_medallion_and_high_contrast_swaps_it()
    {
        Assert.Same(MedalTokens.Standard, MedalTokens.For(GlyphPalette.Standard));
        Assert.Same(MedalTokens.HighContrastDark, MedalTokens.For(GlyphPalette.HighContrastDark));
        Assert.Same(MedalTokens.HighContrastLight, MedalTokens.For(GlyphPalette.HighContrastLight));
        Assert.False(MedalTokens.Standard.Flat);

        // A flat medal paints only its palette's inks and ground (and their transparent fringes).
        foreach (var tokens in new[] { MedalTokens.HighContrastDark, MedalTokens.HighContrastLight })
        {
            var allowed = tokens.Inks().Select(static i => i.Color).Append(tokens.Ground)
                .Select(static c => MeshBuilder.Pack(c with { W = 1f })).ToHashSet();
            foreach (var state in Enum.GetValues<QuestState>())
            {
                foreach (var part in MedalArt.Medal(state, tokens).Parts)
                {
                    foreach (var color in part.Colors.Where(static c => c >> 24 != 0))
                    {
                        Assert.Contains(color | 0xFF000000u, allowed);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ the round 5 tokens

    [Fact]
    public void Medallion_detail_tokens_come_from_gen5()
    {
        // Plan v7 V1 adds the Completed face's basalt, hearts and highland from make_completed.py, the v7 face's generator.
        var gen5 = File.ReadAllText(Path.Combine(Round5Dir(), "_src", "gen5.py"))
            + File.ReadAllText(Path.Combine(OrnamentLayoutTests.RepoRoot(), "docs", "design", "v7", "ui", "completed-moon", "make_completed.py"));
        var hexes = Regex.Matches(gen5, "#([0-9A-Fa-f]{6})").Select(static m => Convert.ToUInt32(m.Groups[1].Value, 16)).ToHashSet();
        var fields = typeof(GlyphTokens.MedallionDetail).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (var field in fields.Where(static f => f.IsLiteral && f.FieldType == typeof(uint)))
        {
            if (field.Name is nameof(GlyphTokens.MedallionDetail.HandHex) or nameof(GlyphTokens.MedallionDetail.HandDeepHex))
            {
                continue;
            }

            Assert.Contains((uint)field.GetRawConstantValue()!, hexes);
        }

        foreach (var field in fields.Where(static f => f.FieldType == typeof(Vector4)))
        {
            var hex = (uint)typeof(GlyphTokens.MedallionDetail).GetField(field.Name + "Hex")!.GetRawConstantValue()!;
            Assert.Equal(ColorMath.FromHex(hex), (Vector4)field.GetValue(null)!);
        }
    }

    // ------------------------------------------------------------------ the meshes

    public static TheoryData<int> AllStates() => [.. Enum.GetValues<QuestState>().Select(static s => (int)s)];

    [Theory]
    [MemberData(nameof(AllStates))]
    public void Every_medal_builds_in_every_palette_with_valid_triangles(int stateValue)
    {
        var state = (QuestState)stateValue;
        foreach (var tokens in new[] { MedalTokens.Standard, MedalTokens.HighContrastDark, MedalTokens.HighContrastLight })
        {
            var mesh = MedalArt.Medal(state, tokens);
            Assert.NotEmpty(mesh.Parts);
            foreach (var part in mesh.Parts)
            {
                Assert.Equal(part.Positions.Length, part.Colors.Length);
                Assert.Equal(part.Positions.Length, part.Offsets.Length);
                Assert.Equal(0, part.Indices.Length % 3);
                Assert.All(part.Indices, i => Assert.True(i < part.Positions.Length));
                Assert.All(part.Positions, p => Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y)));
                Assert.All(part.Offsets, o => Assert.True(o.Length() <= 2f, $"{state}: AA offset {o}"));
            }

            // Rows draw dozens of these a frame: hold each under a vertex budget at row size.
            var (vertices, _) = mesh.Count(16f);
            Assert.True(vertices <= 4000, $"{state} ({tokens.Name}): {vertices} vertices at 16 px");
        }

        Assert.Same(MedalArt.Medal(state, MedalTokens.Standard), MedalArt.Medal(state, MedalTokens.Standard));
    }

    [Fact]
    public void The_medal_fills_its_keyline_disc_and_stays_in_its_box()
    {
        foreach (var state in Enum.GetValues<QuestState>())
        {
            var points = MedalArt.Medal(state, MedalTokens.Standard).Parts.SelectMany(static p => p.Positions).ToArray();
            // Only In journal's ribbon (wrapped over the rim at r 66) and Completed's check (struck out through it) pass the keyline.
            var reach = points.Max(p => Vector2.Distance(p, MedalArt.Center));
            Assert.InRange(reach, MedalArt.KeylineRadius - 0.01f, state is QuestState.Accepted or QuestState.Completed ? 72f : MedalArt.KeylineRadius + 0.01f);
            Assert.All(points, p => Assert.True(p.X >= -1f && p.X <= 129f && p.Y >= -4f && p.Y <= 129f, $"{state}: {p}"));
        }
    }

    [Fact]
    public void Row_glyphs_exist_for_the_three_drawn_badges_only()
    {
        foreach (var badge in new[] { MedalBadge.Open, MedalBadge.Closed, MedalBadge.Journal })
        {
            var mesh = MedalArt.RowGlyph(badge, MedalTokens.Standard);
            Assert.NotEmpty(mesh.Parts);
            var points = mesh.Parts.SelectMany(static p => p.Positions).ToArray();
            Assert.All(points, p => Assert.True(p.X > 0f && p.X < 128f && p.Y > 0f && p.Y < 128f, $"{badge}: {p}"));
        }

        Assert.Empty(MedalArt.RowGlyph(MedalBadge.Job, MedalTokens.Standard).Parts);
        Assert.Empty(MedalArt.RowGlyph(MedalBadge.None, MedalTokens.Standard).Parts);
    }

    [Fact]
    public void Luminance_weighs_the_channels_and_ignores_alpha()
    {
        Assert.Equal(0f, MedalMesh.Luminance(0xFF000000u));
        Assert.Equal(255f, MedalMesh.Luminance(0x00FFFFFFu), 3);
        Assert.Equal(255f * 0.2126f, MedalMesh.Luminance(0x000000FFu), 3);
        Assert.Equal(255f * 0.7152f, MedalMesh.Luminance(0x8000FF00u), 3);
        Assert.Equal(255f * 0.0722f, MedalMesh.Luminance(0x00FF0000u), 3);
    }

    [Fact]
    public void Brightest_is_the_brightest_vertex_of_any_part_and_stays_put()
    {
        var mesh = new MedalMesh([
            new MeshPart([Vector2.Zero], [Vector2.Zero], [0xFF202020u], [0], 0f),
            new MeshPart([Vector2.Zero, Vector2.One], [Vector2.Zero, Vector2.Zero], [0x10808080u, 0xFF404040u], [0, 1], 40f),
        ]);
        Assert.Equal(MedalMesh.Luminance(0x10808080u), mesh.Brightest, 3);
        Assert.Equal(mesh.Brightest, mesh.Brightest);
        Assert.Equal(0f, new MedalMesh([]).Brightest);

        var glyph = MedalArt.RowGlyph(MedalBadge.Journal, MedalTokens.Standard);
        var expected = glyph.Parts.SelectMany(static p => p.Colors).Max(MedalMesh.Luminance);
        Assert.Equal(expected, glyph.Brightest, 3);
    }

    [Fact]
    public void Ear_clipping_covers_a_concave_outline_exactly()
    {
        // An L shape, clockwise and anticlockwise: the triangles' area is the outline's.
        Vector2[] l = [new(0, 0), new(4, 0), new(4, 1), new(1, 1), new(1, 3), new(0, 3)];
        foreach (var loop in new[] { l, l.Reverse().ToArray() })
        {
            var triangles = MeshBuilder.Triangulate(loop);
            Assert.Equal(loop.Length - 2, triangles.Count);
            var area = triangles.Sum(t => MathF.Abs(MeshBuilder.SignedArea([loop[t.A], loop[t.B], loop[t.C]])) / 2f);
            Assert.Equal(6f, area, 3);
        }
    }

    // ------------------------------------------------------------------ the job badge

    [Fact]
    public void Roles_take_their_seats()
    {
        Assert.Equal(JobSeat.Tank, JobBadgeRules.SeatFor(1, false));
        Assert.Equal(JobSeat.Healer, JobBadgeRules.SeatFor(4, false));
        Assert.Equal(JobSeat.Dps, JobBadgeRules.SeatFor(2, false));
        Assert.Equal(JobSeat.Dps, JobBadgeRules.SeatFor(3, false));
        Assert.Equal(JobSeat.Hand, JobBadgeRules.SeatFor(0, false));
        Assert.Equal(JobSeat.Hand, JobBadgeRules.SeatFor(1, true));
        Assert.Equal(62019u, JobBadgeRules.IconFor(19));
        Assert.Equal(0u, JobBadgeRules.IconFor(0));
    }

    [Fact]
    public void Optical_offset_finds_a_glyph_sitting_high()
    {
        // A 40 px canvas with a solid 10 × 10 block whose centre is 6 px above and 2 px right of the canvas centre, plus
        // a faint glow (alpha 0.3) that must be ignored.
        const int n = 40;
        var pixels = new byte[n * n * 4];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var solid = x is >= 16 and < 26 && y is >= 8 and < 18;
                pixels[(y * n + x) * 4 + 3] = solid ? (byte)255 : y > 30 ? (byte)77 : (byte)0;
            }
        }

        var offset = JobBadgeRules.OpticalOffset(pixels, n, n, n * 4);
        Assert.Equal((20.5f - 19.5f) / n, offset.X, 4);
        Assert.Equal((12.5f - 19.5f) / n, offset.Y, 4);

        Assert.Equal(JobBadgeRules.DefaultOffset, JobBadgeRules.OpticalOffset(new byte[n * n * 4], n, n, n * 4));
        Assert.Equal(JobBadgeRules.DefaultOffset, JobBadgeRules.OpticalOffset(pixels, n, n, 4));
        Assert.True(JobBadgeRules.DefaultOffset.Y < 0f, "the game's job glyphs sit high, so the default lowers them");
    }
}
