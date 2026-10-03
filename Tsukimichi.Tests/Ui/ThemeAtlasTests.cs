using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The multi-theme build (feature plan v7 T4; theme-system.md §6.4 and §7.1): every shipped glyph set's atlas keeps
/// Medallion's cell layout to the pixel, its row strip covers every whole pixel size, and the <c>metrics.json</c> that
/// <c>tools/themes/build_themes.py</c> writes passes the per-set gates. The bars live here, not in the JSON, so a
/// rebuilt file cannot loosen them.
/// </summary>
public sealed class ThemeAtlasTests
{
    private static readonly string[] Shipped = ["ishgard-glass", "aether-crystal"];
    private static readonly string[] Measured = ["medallion", .. Shipped];

    /// <summary>Sets that ship their own atlases under <c>assets/ui/themes/&lt;set&gt;/</c>.</summary>
    public static readonly TheoryData<string> ShippedSets = new(Shipped);

    /// <summary>Every set the build measures (Medallion's atlas stays embedded at <c>assets/ui/</c>).</summary>
    public static readonly TheoryData<string> MeasuredSets = new(Measured);

    private static readonly string[] States =
        ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"];

    private const int PairCount = 28;
    private const double Weakest16 = 12.0;
    private const double Weakest20 = 16.0;
    private const double ReadyLead = 1.3;
    private const double CompletedOfReady = 0.8;
    private const double SalienceMin = 15.0;
    private const double CvdWeakest16 = 11.0;
    private const double LightReadyLead = 1.3;
    private const double LightLumaFloor = 0.70;

    /// <summary>Machado 2009 protanopia, deuteranopia and tritanopia: gated at 16 px on every ground (§7.1).</summary>
    private static readonly string[] CvdModes = ["machado-prot", "machado-deut", "machado-trit"];
    private static readonly string[] Grounds = ["night", "ishgard-snow", "daylight"];

    /// <summary>
    /// G2L's lead measures in the realism supervisor's order (spec-1.16 §A4.1): OKLab difference with lightness
    /// down-weighted, then chroma only, each needing Ready ≥ 1.3× the next state at 16 and 20 px.
    /// </summary>
    private static readonly string[] LightLeadMeasures = ["weighted", "chroma"];

    /// <summary>Snow's Ready wash, the one every set ships (spec-1.16 §A4.1): colour, alpha, radius in px.</summary>
    private static readonly (string Color, double Alpha, int Radius) ReadyWash = ("#F2D27A", 0.75, 3);

    private static string ThemesDir() => Path.Combine(OrnamentLayoutTests.AssetsDir(), "themes");

    private static JsonDocument Json(params string[] parts) => JsonDocument.Parse(File.ReadAllText(Path.Combine(parts)));

    /// <summary>The element re-serialised without its source whitespace (git may check one file out with CRLF).</summary>
    private static string Canon(JsonElement element) => JsonSerializer.Serialize(element);

    // ------------------------------------------------------------------ coverage

    [Fact]
    public void Every_theme_folder_is_a_measured_set_with_a_manifest()
    {
        var folders = Directory.GetDirectories(ThemesDir()).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        var measured = Measured.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(measured, folders);
        foreach (var set in measured)
        {
            Assert.True(File.Exists(Path.Combine(OrnamentLayoutTests.RepoRoot(), "tools", "themes", "sets", set + ".json")), set);
            Assert.True(File.Exists(Path.Combine(ThemesDir(), set, "metrics.json")), set);
        }
    }

    // ------------------------------------------------------------------ the hero atlas

    [Theory]
    [MemberData(nameof(ShippedSets))]
    public void Atlas_json_matches_Medallions_cell_layout(string set)
    {
        using var theirs = Json(ThemesDir(), set, "medals.json");
        using var medallion = Json(OrnamentLayoutTests.AssetsDir(), "medals.json");
        var t = theirs.RootElement;
        var m = medallion.RootElement;
        Assert.Equal(Canon(m.GetProperty("size")), Canon(t.GetProperty("size")));
        Assert.Equal(Canon(m.GetProperty("tiers")), Canon(t.GetProperty("tiers")));

        var sprites = t.GetProperty("sprites");
        Assert.Equal(MedalLayout.SpriteCount, sprites.EnumerateObject().Count());
        foreach (var sprite in Enum.GetValues<MedalSprite>())
        {
            Assert.True(sprites.TryGetProperty(MedalLayout.Key(sprite), out var tiers), $"{set} has no sprite {MedalLayout.Key(sprite)}");
            Assert.Equal(Canon(m.GetProperty("sprites").GetProperty(MedalLayout.Key(sprite))), Canon(tiers));
            foreach (var tier in MedalLayout.Tiers)
            {
                var r = tiers.GetProperty(tier.ToString(CultureInfo.InvariantCulture));
                Assert.Equal(MedalLayout.Rect(sprite, tier), new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32()));
            }
        }
    }

    [Theory]
    [MemberData(nameof(ShippedSets))]
    public void Atlas_pngs_are_the_layout_size_and_2x(string set)
    {
        Assert.Equal((MedalLayout.Width, MedalLayout.Height), OrnamentLayoutTests.PngSize(Path.Combine(ThemesDir(), set, "medals.png")));
        Assert.Equal((MedalLayout.Width * 2, MedalLayout.Height * 2), OrnamentLayoutTests.PngSize(Path.Combine(ThemesDir(), set, "medals@2x.png")));
        var bytes = new[] { "medals.png", "medals@2x.png", "row.png" }.Sum(n => new FileInfo(Path.Combine(ThemesDir(), set, n)).Length);
        Assert.True(bytes < 3 * 1024 * 1024, $"{set}'s atlases are {bytes / 1024} KB; the per-set budget is 3 MB");
    }

    // ------------------------------------------------------------------ the row strip

    [Theory]
    [MemberData(nameof(ShippedSets))]
    public void Row_strip_has_every_state_at_every_whole_pixel_from_12_to_31_apart_by_the_runtime_pad(string set)
    {
        using var json = Json(ThemesDir(), set, "row.json");
        var root = json.RootElement;
        var width = root.GetProperty("size")[0].GetInt32();
        var height = root.GetProperty("size")[1].GetInt32();
        Assert.Equal((width, height), OrnamentLayoutTests.PngSize(Path.Combine(ThemesDir(), set, "row.png")));
        Assert.Equal(Enumerable.Range(12, 20), root.GetProperty("sizes").EnumerateArray().Select(static s => s.GetInt32()));

        var sprites = root.GetProperty("sprites");
        Assert.Equal(States.Order(StringComparer.Ordinal), sprites.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
        var rects = new List<AtlasRect>();
        foreach (var state in States)
        {
            for (var size = 12; size <= 31; size++)
            {
                var r = sprites.GetProperty(state).GetProperty(size.ToString(CultureInfo.InvariantCulture));
                var rect = new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());
                Assert.Equal(size, rect.Width);
                Assert.Equal(size, rect.Height);
                Assert.True(rect.X >= 1 && rect.Y >= 1 && rect.X + rect.Width <= width - 1 && rect.Y + rect.Height <= height - 1, $"{state} {size}: {rect}");
                rects.Add(rect);
            }
        }

        // The runtime's gap (ThemeAtlasRules.MinPad, 2 px at 1x), so bilinear sampling never bleeds a neighbour in.
        const int pad = Core.Ui.Themes.ThemeAtlasRules.MinPad;
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                var apart = a.X + a.Width + pad <= b.X || b.X + b.Width + pad <= a.X || a.Y + a.Height + pad <= b.Y || b.Y + b.Height + pad <= a.Y;
                Assert.True(apart, $"{set}: {a} and {b} are closer than {pad} px");
            }
        }
    }

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void The_shipped_pngs_are_the_ones_the_metrics_were_measured_from(string set)
    {
        // metrics.json records the SHA-256 of every PNG the build wrote with these numbers; a PNG re-exported or edited
        // by hand without a rebuild would ship atlases the gates never measured.
        using var json = Json(ThemesDir(), set, "metrics.json");
        var pngs = json.RootElement.GetProperty("pngs").EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.GetString());
        var atlas = set == "medallion" ? "Tsukimichi/assets/ui" : $"Tsukimichi/assets/ui/themes/{set}";
        string[] expected = set == "medallion"
            ? [$"{atlas}/medals.png", $"{atlas}/medals@2x.png"]
            : [$"{atlas}/medals.png", $"{atlas}/medals@2x.png", $"{atlas}/row.png"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), pngs.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, sha) in pngs)
        {
            var bytes = File.ReadAllBytes(Path.Combine(OrnamentLayoutTests.RepoRoot(), path));
            Assert.True(string.Equals(sha, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), StringComparison.Ordinal), $"{path} is not the PNG metrics.json was measured from; rebuild with tools/themes/build_themes.py");
        }
    }

    // ------------------------------------------------------------------ the per-set gates (§7.1)

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void Metrics_pass_the_per_set_gates(string set)
    {
        using var json = Json(ThemesDir(), set, "metrics.json");
        var root = json.RootElement;
        Assert.Equal(set, root.GetProperty("set").GetString());

        var tiers = root.GetProperty("tiers").EnumerateArray().ToArray();
        Assert.Contains(tiers, static t => t.GetProperty("name").GetString() == "row");
        var heroTiers = tiers.SelectMany(static t => t.GetProperty("atlasTiers").EnumerateArray().Select(static x => x.GetInt32())).Order().ToArray();
        Assert.Equal(MedalLayout.Tiers, heroTiers);

        foreach (var tier in tiers)
        {
            var name = $"{set} {tier.GetProperty("name").GetString()}";
            var measures = tier.GetProperty("measures").EnumerateArray().ToArray();
            foreach (var (px, bar) in new[] { (16, Weakest16), (20, Weakest20) })
            {
                foreach (var mode in new[] { "grey", "deut" })
                {
                    var measure = measures.Single(x => x.GetProperty("ground").GetString() == "night" && x.GetProperty("px").GetInt32() == px && x.GetProperty("mode").GetString() == mode);
                    var pairs = measure.GetProperty("pairs").EnumerateObject().ToArray();
                    Assert.Equal(PairCount, pairs.Length);
                    var weakest = pairs.MinBy(static p => p.Value.GetDouble());
                    // Judged at one decimal, the precision round 5 set the bar at (Medallion's row tier holds Blk-Lock at 12.0).
                    Assert.True(Math.Round(weakest.Value.GetDouble(), 1) >= bar, $"{name} {px} px {mode}: {weakest.Name} {weakest.Value} under {bar}");
                }
            }

            var salience = measures.Single(static x => x.GetProperty("ground").GetString() == "night" && x.GetProperty("px").GetInt32() == 16 && x.GetProperty("mode").GetString() == "grey")
                .GetProperty("salience");
            var sal = States.ToDictionary(static s => s, s => salience.GetProperty(s).GetDouble());
            var ready = sal["ready"];
            var next = sal.Where(static kv => kv.Key != "ready").Max(static kv => kv.Value);
            Assert.True(Math.Round(ready / next, 2) >= ReadyLead, $"{name}: Ready {ready} leads the next state {next} by only {ready / next:0.00}x");
            Assert.True(Math.Round(sal["completed"] / ready, 2) <= CompletedOfReady, $"{name}: Completed {sal["completed"]} is {sal["completed"] / ready:0.00}x Ready");
            foreach (var (state, value) in sal.Where(static kv => kv.Key != "not-checked"))
            {
                Assert.True(Math.Round(value, 1) >= SalienceMin, $"{name}: {state} salience {value}");
            }
        }

        Assert.True(root.GetProperty("pass").GetBoolean(), $"{set}: the build recorded a failing gate");
        Assert.All(root.GetProperty("gates").EnumerateArray(), static g => Assert.True(g.GetProperty("pass").GetBoolean(), g.ToString()));
    }

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void Metrics_pass_the_colour_vision_gates(string set)
    {
        using var json = Json(ThemesDir(), set, "metrics.json");
        foreach (var tier in json.RootElement.GetProperty("tiers").EnumerateArray())
        {
            var name = $"{set} {tier.GetProperty("name").GetString()}";
            var measures = tier.GetProperty("measures").EnumerateArray().ToArray();
            foreach (var ground in Grounds)
            {
                foreach (var mode in CvdModes)
                {
                    var measure = measures.Single(x => x.GetProperty("ground").GetString() == ground && x.GetProperty("px").GetInt32() == 16 && x.GetProperty("mode").GetString() == mode);
                    var pairs = measure.GetProperty("pairs").EnumerateObject().ToArray();
                    Assert.Equal(PairCount, pairs.Length);
                    var weakest = pairs.MinBy(static p => p.Value.GetDouble());
                    Assert.True(Math.Round(weakest.Value.GetDouble(), 1) >= CvdWeakest16, $"{name} 16 px {mode} on {ground}: {weakest.Name} {weakest.Value} under {CvdWeakest16}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void Metrics_pass_the_light_palette_Ready_gates_with_the_shipped_wash(string set)
    {
        using var json = Json(ThemesDir(), set, "metrics.json");
        var root = json.RootElement;
        var light = root.GetProperty("light");
        Assert.Equal("ishgard-snow", light.GetProperty("ground").GetString());
        Assert.Equal("#EEF1F6", light.GetProperty("window").GetString());
        Assert.Equal("row", light.GetProperty("tier").GetString());

        // Every set ships the .75 / 3 px wash; the old .90 / 4 px fallback is only recorded.
        Assert.Equal("default", light.GetProperty("use").GetString());
        var wash = root.GetProperty("readyWash");
        Assert.Equal(ReadyWash, (wash.GetProperty("color").GetString(), wash.GetProperty("alpha").GetDouble(), wash.GetProperty("radiusPx").GetInt32()));

        // Each recorded lead is Ready's salience over the loudest other state's (judged at two decimals); the luminance
        // lead leaves Not checked out, which the chroma check below holds under Ready instead.
        var variants = light.GetProperty("variants");
        double Salience(string px, string measure, string state) =>
            variants.GetProperty("default").GetProperty(px).GetProperty(measure).GetProperty("salience").GetProperty(state).GetDouble();
        double Lead(string variant, string px, string measure)
        {
            var v = variants.GetProperty(variant).GetProperty(px).GetProperty(measure);
            var sal = States.ToDictionary(static s => s, s => v.GetProperty("salience").GetProperty(s).GetDouble());
            var next = sal.Where(kv => kv.Key != "ready" && !(measure == "luma" && kv.Key == "not-checked")).Max(static kv => kv.Value);
            var lead = v.GetProperty("readyLead").GetDouble();
            Assert.Equal(Math.Round(sal["ready"] / next, 2), lead, 0.011);
            return lead;
        }

        foreach (var variant in new[] { "none", "default", "fallback" })
        {
            foreach (var px in new[] { "16", "20" })
            {
                foreach (var measure in LightLeadMeasures.Append("luma"))
                {
                    Lead(variant, px, measure);
                }
            }
        }

        // G2L: Ready leads by 1.3 under the weighted measure at both sizes, or failing that under chroma only, and the
        // record names the measure that passed; Ready's plain luminance salience stays at least .70 of the next state's
        // (Not checked left out); and Not checked stays under Ready on chroma.
        var passed = LightLeadMeasures.FirstOrDefault(m => Lead("default", "16", m) >= LightReadyLead && Lead("default", "20", m) >= LightReadyLead);
        Assert.True(passed is not null, $"{set}: Ready leads the next state on Ishgard Snow by under {LightReadyLead}x under every measure");
        Assert.Equal(passed, light.GetProperty("measure").GetString());
        foreach (var px in new[] { "16", "20" })
        {
            var floor = Lead("default", px, "luma");
            Assert.True(floor >= LightLumaFloor, $"{set} {px} px: Ready's luminance salience is only {floor}x the next state's (Not checked aside) on Ishgard Snow");
            var notChecked = Salience(px, "chroma", "not-checked");
            var ready = Salience(px, "chroma", "ready");
            Assert.True(notChecked < ready, $"{set} {px} px: Not checked ({notChecked}) is not under Ready ({ready}) on chroma on Ishgard Snow");
        }
    }

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void Metrics_carry_the_cross_set_table_against_every_other_set(string set)
    {
        using var json = Json(ThemesDir(), set, "metrics.json");
        var sets = json.RootElement.GetProperty("cross").GetProperty("sets");
        var others = Measured.Where(s => s != set).Order(StringComparer.Ordinal);
        Assert.Equal(others, sets.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
        foreach (var other in sets.EnumerateObject())
        {
            foreach (var which in new[] { "row", "hero" })
            {
                foreach (var px in new[] { "16", "20" })
                {
                    var pairs = other.Value.GetProperty(which).GetProperty(px).EnumerateObject().ToArray();
                    Assert.Equal(States.Length * (States.Length - 1), pairs.Length);
                    Assert.All(pairs, static p => Assert.True(p.Value.GetDouble() > 0, p.Name));
                }
            }
        }
    }

    // ------------------------------------------------------------------ the manifests

    [Theory]
    [MemberData(nameof(ShippedSets))]
    public void The_hand_seat_takes_Medallions_hand_enamel(string set)
    {
        using var json = Json(OrnamentLayoutTests.RepoRoot(), "tools", "themes", "sets", set + ".json");
        var layers = json.RootElement.GetProperty("sprites").GetProperty("other-job-hand").GetProperty("layers");
        var recolour = layers.EnumerateArray().Single(static l => l.ValueKind == JsonValueKind.Object).GetProperty("recolour");
        var hand = $"#{GlyphTokens.MedallionDetail.HandHex:X6}";
        Assert.Contains(recolour.EnumerateObject(), p => p.Value.GetString() == hand);
    }
}
