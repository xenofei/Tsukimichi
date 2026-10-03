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
    private static readonly string[] Shipped = ["ishgard-glass", "aether-crystal", "astrologian-orrery", "sumi-to-kinpaku"];
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

        // ATLAS-CONTRACT §3: the plain form (the full finish alone) or, for a set with a flat finish of its own for
        // Decoration Plain, the nested form keyed by finish; a set has the 'plain' finish exactly when the catalog says so.
        var sprites = root.GetProperty("sprites");
        var nested = sprites.TryGetProperty("full", out _);
        var finishes = nested ? sprites.EnumerateObject().Select(static p => (p.Name, p.Value)).ToArray() : [("full", sprites)];
        Assert.True(Core.Ui.Themes.GlyphSets.TryGet(set, out var info));
        Assert.Equal(info.HasPlainFinish ? ["full", "plain"] : ["full"], finishes.Select(static f => f.Name));
        var rects = new List<AtlasRect>();
        foreach (var (finish, states) in finishes)
        {
            Assert.Equal(States.Order(StringComparer.Ordinal), states.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
            foreach (var state in States)
            {
                for (var size = 12; size <= 31; size++)
                {
                    var r = states.GetProperty(state).GetProperty(size.ToString(CultureInfo.InvariantCulture));
                    var rect = new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());
                    Assert.Equal(size, rect.Width);
                    Assert.Equal(size, rect.Height);
                    Assert.True(rect.X >= 1 && rect.Y >= 1 && rect.X + rect.Width <= width - 1 && rect.Y + rect.Height <= height - 1, $"{finish} {state} {size}: {rect}");
                    rects.Add(rect);
                }
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
        var faces = $"Tsukimichi/assets/ui/themes/{set}";
        string[] expected = set == "medallion"
            ? [$"{atlas}/medals.png", $"{atlas}/medals@2x.png", $"{faces}/faces.png", $"{faces}/faces@2x.png", $"{faces}/faces-row.png"]
            : [$"{atlas}/medals.png", $"{atlas}/medals@2x.png", $"{atlas}/row.png", $"{faces}/faces.png", $"{faces}/faces@2x.png", $"{faces}/faces-row.png"];
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
        Assert.Contains(recolour.EnumerateObject(), p => p.Value.GetString()!.Contains(hand, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ the frames axis (1.17 T11)

    private static readonly string[] Kits = ["astrolabe", "brass", "came", "kirikane", "silver"];

    /// <summary>Every frame kit the build writes.</summary>
    public static readonly TheoryData<string> ShippedKits = new(Kits);

    private static string KitsDir() => Path.Combine(OrnamentLayoutTests.AssetsDir(), "kits");

    private static readonly Dictionary<string, string> OwnKit = new()
    {
        ["medallion"] = "brass",
        ["aether-crystal"] = "silver",
        ["ishgard-glass"] = "came",
        ["astrologian-orrery"] = "astrolabe",
        ["sumi-to-kinpaku"] = "kirikane",
    };

    [Fact]
    public void Every_kit_folder_has_a_manifest_and_every_set_names_one()
    {
        Assert.Equal(Kits, Directory.GetDirectories(KitsDir()).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var kit in Kits)
        {
            Assert.True(File.Exists(Path.Combine(OrnamentLayoutTests.RepoRoot(), "tools", "themes", "kits", kit + ".json")), kit);
        }

        foreach (var set in Measured)
        {
            using var json = Json(OrnamentLayoutTests.RepoRoot(), "tools", "themes", "sets", set + ".json");
            Assert.Equal(OwnKit[set], json.RootElement.GetProperty("kit").GetString());
        }
    }

    [Theory]
    [MemberData(nameof(ShippedKits))]
    public void Every_set_is_measured_in_every_kit_and_its_own_kit_passes_every_gate(string kit)
    {
        using var json = Json(KitsDir(), kit, "metrics.json");
        var root = json.RootElement;
        Assert.Equal(kit, root.GetProperty("kit").GetString());
        Assert.True(root.GetProperty("pass").GetBoolean(), $"{kit}: the build recorded a failing own-kit or fit gate");
        Assert.All(root.GetProperty("fit").EnumerateArray(), static g => Assert.True(g.GetProperty("pass").GetBoolean(), g.ToString()));

        var faces = root.GetProperty("faces");
        Assert.Equal(Measured.Order(StringComparer.Ordinal), faces.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
        foreach (var entry in faces.EnumerateObject())
        {
            var own = OwnKit[entry.Name] == kit;
            Assert.Equal(own, entry.Value.GetProperty("own").GetBoolean());
            var gates = entry.Value.GetProperty("gates").EnumerateArray().ToArray();

            // Every gate a set passes is recorded for every tier group (G1, G1c, G2) and the row tier (G2L).
            var groups = entry.Value.GetProperty("tiers").EnumerateArray().Select(static t => t.GetProperty("name").GetString()!).ToArray();
            Assert.Contains("row", groups);
            Assert.Equal(MedalLayout.Tiers, entry.Value.GetProperty("tiers").EnumerateArray().SelectMany(static t => t.GetProperty("atlasTiers").EnumerateArray().Select(static x => x.GetInt32())).Order());
            foreach (var group in groups)
            {
                Assert.Contains(gates, g => g.GetProperty("tier").GetString() == group && g.GetProperty("gate").GetString() == "G2 Ready lead");
                Assert.Contains(gates, g => g.GetProperty("tier").GetString() == group && g.GetProperty("gate").GetString()!.StartsWith("G1 weakest pair 16 px grey", StringComparison.Ordinal));
                Assert.Equal(9, gates.Count(g => g.GetProperty("tier").GetString() == group && g.GetProperty("gate").GetString()!.StartsWith("G1c ", StringComparison.Ordinal)));
            }

            Assert.Contains(gates, static g => g.GetProperty("gate").GetString()!.StartsWith("G2L Ready lead", StringComparison.Ordinal));

            // Each recorded verdict is the value against the bar, judged here (a rebuilt file cannot loosen them).
            foreach (var g in gates)
            {
                var name = g.GetProperty("gate").GetString()!;
                var value = g.GetProperty("value").GetDouble();
                var bar = g.GetProperty("bar").GetDouble();
                Assert.Equal(GateBar(name), bar);
                var pass = name.Contains("Completed recedes", StringComparison.Ordinal) ? Math.Round(value, 2) <= bar
                    : name.Contains("Not checked under Ready", StringComparison.Ordinal) ? value <= bar
                    : name.StartsWith("G2 Ready lead", StringComparison.Ordinal) || name.StartsWith("G2L", StringComparison.Ordinal) ? Math.Round(value, 2) >= bar
                    : Math.Round(value, 1) >= bar;
                if (!name.Contains("Not checked under Ready", StringComparison.Ordinal))
                {
                    Assert.True(pass == g.GetProperty("pass").GetBoolean(), $"{kit} {entry.Name}: {g}");
                }
            }

            var failing = gates.Where(static g => !g.GetProperty("pass").GetBoolean()).Select(static g => $"{g.GetProperty("tier").GetString()}: {g.GetProperty("gate").GetString()}");
            Assert.Equal(failing, entry.Value.GetProperty("warnings").EnumerateArray().Select(static w => w.GetString()));
            Assert.Equal(!failing.Any(), entry.Value.GetProperty("pass").GetBoolean());

            // A set in its own kit is what it ships: it must pass, as its own metrics do.
            if (own)
            {
                Assert.True(entry.Value.GetProperty("pass").GetBoolean(), $"{entry.Name} fails in its own kit {kit}");
            }
        }
    }

    private static double GateBar(string gate) => gate switch
    {
        _ when gate.StartsWith("G1 weakest pair 16", StringComparison.Ordinal) => Weakest16,
        _ when gate.StartsWith("G1 weakest pair 20", StringComparison.Ordinal) => Weakest20,
        _ when gate.StartsWith("G1c", StringComparison.Ordinal) => CvdWeakest16,
        "G2 Ready lead" => ReadyLead,
        "G2 Completed recedes" => CompletedOfReady,
        "G2 every state visible" => SalienceMin,
        _ when gate.StartsWith("G2L Ready lead", StringComparison.Ordinal) => LightReadyLead,
        _ when gate.StartsWith("G2L lightness floor", StringComparison.Ordinal) => LightLumaFloor,
        _ when gate.StartsWith("G2L Not checked", StringComparison.Ordinal) => 1.0,
        _ => throw new InvalidOperationException($"unknown gate {gate}"),
    };

    [Theory]
    [MemberData(nameof(ShippedKits))]
    public void The_kit_pngs_are_the_ones_its_metrics_were_measured_from(string kit)
    {
        using var json = Json(KitsDir(), kit, "metrics.json");
        var pngs = json.RootElement.GetProperty("pngs").EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.GetString());
        var dir = $"Tsukimichi/assets/ui/kits/{kit}";
        string[] expected = Core.Ui.Themes.FrameKits.TryGet(kit, out var info) && Core.Ui.Themes.FrameKitMetals.HasOrnamentSprites(info.Id)
            ? [$"{dir}/frames-row.png", $"{dir}/frames.png", $"{dir}/frames@2x.png", $"{dir}/ornaments.png"]
            : [$"{dir}/frames-row.png", $"{dir}/frames.png", $"{dir}/frames@2x.png"];
        Assert.Equal(expected, pngs.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, sha) in pngs)
        {
            var bytes = File.ReadAllBytes(Path.Combine(OrnamentLayoutTests.RepoRoot(), path));
            Assert.True(string.Equals(sha, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), StringComparison.Ordinal), $"{path} is not the PNG metrics.json was measured from");
        }
    }

    [Fact]
    public void Kirikanes_ornament_strip_follows_the_size_rule_and_passes_its_gates()
    {
        // The Sumi to Kinpaku concept's size rule: the crest from 13 px, the small crest at 10-12 px only, the lozenge below
        // 10 px (2 px times the UI scale), and the corner mark only where its leaf bar is a whole device pixel.
        var folder = Path.Combine(KitsDir(), "kirikane");
        Assert.True(Core.Ui.Themes.KitOrnamentLayout.TryParse(File.ReadAllText(Path.Combine(folder, "ornaments.json")), out var layout, out var error), error);
        Assert.Equal((layout!.Width, layout.Height), OrnamentLayoutTests.PngSize(Path.Combine(folder, "ornaments.png")));
        var (sigilMin, _) = layout.Range(Core.Ui.Themes.KitOrnament.Sigil);
        Assert.Equal(Core.Ui.Themes.KitOrnaments.SigilMinPx, sigilMin);
        Assert.Equal((Core.Ui.Themes.KitOrnaments.SmallSigilMinPx, Core.Ui.Themes.KitOrnaments.SigilMinPx - 1), layout.Range(Core.Ui.Themes.KitOrnament.SigilSmall));
        Assert.True(layout.Range(Core.Ui.Themes.KitOrnament.Lozenge).Max < Core.Ui.Themes.KitOrnaments.SmallSigilMinPx);
        Assert.Equal(2, layout.Range(Core.Ui.Themes.KitOrnament.Lozenge).Min);
        Assert.Equal(Core.Ui.Themes.KitOrnaments.CornerMinPx, layout.Range(Core.Ui.Themes.KitOrnament.Corner).Min);

        using var json = Json(folder, "metrics.json");
        var gates = json.RootElement.GetProperty("ornaments").GetProperty("gates").EnumerateArray().ToArray();
        Assert.Contains(gates, static g => g.GetProperty("gate").GetString() == "Ornament contrast on kugane-lacquer");
        Assert.All(gates, static g => Assert.True(g.GetProperty("pass").GetBoolean(), g.ToString()));
        foreach (var g in gates.Where(static g => g.GetProperty("gate").GetString()!.StartsWith("Ornament contrast", StringComparison.Ordinal)))
        {
            Assert.True(g.GetProperty("value").GetDouble() >= 3.0, g.ToString());
        }
    }

    // ------------------------------------------------------------------ the dark palettes (G2D, 1.17)

    /// <summary>The 1.17 dark palettes' windows, from their design record, keyed by palette.</summary>
    private static Dictionary<string, string> DarkWindows()
    {
        using var json = Json(OrnamentLayoutTests.RepoRoot(), "docs", "design", "v7", "ui", "1.17", "palettes17.json");
        var palettes = json.RootElement.GetProperty("palettes");
        return new Dictionary<string, string>
        {
            ["dawn"] = palettes.GetProperty("dawn").GetProperty("Window").GetString()!,
            ["kugane-lacquer"] = palettes.GetProperty("kugane").GetProperty("Window").GetString()!,
        };
    }

    private const double MixReadyLead = 1.25;

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void Metrics_pass_the_dark_palette_salience_gates(string set)
    {
        using var json = Json(ThemesDir(), set, "metrics.json");
        var dark = json.RootElement.GetProperty("dark");
        var windows = DarkWindows();
        Assert.Equal(windows, dark.GetProperty("windows").EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.GetString()!));

        // The halo is Moon gold in the wash's footprint: .45 as shipped, .60 raised; never a recolour of the medal.
        var halos = dark.GetProperty("halos");
        Assert.Equal(("#F2D27A", 0.45, 3), (halos.GetProperty("default").GetProperty("color").GetString(), halos.GetProperty("default").GetProperty("alpha").GetDouble(), halos.GetProperty("default").GetProperty("radiusPx").GetInt32()));
        Assert.Equal(("#F2D27A", 0.60, 3), (halos.GetProperty("raised").GetProperty("color").GetString(), halos.GetProperty("raised").GetProperty("alpha").GetDouble(), halos.GetProperty("raised").GetProperty("radiusPx").GetInt32()));

        var tiers = json.RootElement.GetProperty("tiers").EnumerateArray().Select(static t => t.GetProperty("name").GetString()!).ToArray();
        foreach (var (palette, _) in windows)
        {
            var halo = dark.GetProperty("halo").GetProperty(palette).GetString()!;
            Assert.Contains(halo, new[] { "none", "default", "raised" });
            var byGroup = dark.GetProperty("tiers").GetProperty(palette);
            Assert.Equal(tiers.Order(StringComparer.Ordinal), byGroup.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
            foreach (var group in byGroup.EnumerateObject())
            {
                foreach (var px in new[] { "16", "20" })
                {
                    var e = group.Value.GetProperty(halo).GetProperty(px);
                    var sal = States.ToDictionary(static s => s, s => e.GetProperty("salience").GetProperty(s).GetDouble());
                    var next = sal.Where(static kv => kv.Key != "ready").Max(static kv => kv.Value);
                    var lead = Math.Round(sal["ready"] / next, 2);
                    var comp = Math.Round(sal["completed"] / sal["ready"], 2);
                    Assert.True(lead >= ReadyLead, $"{set} on {palette} ({halo} halo), {group.Name} {px} px: Ready leads by only {lead}x");
                    Assert.True(comp <= CompletedOfReady, $"{set} on {palette} ({halo} halo), {group.Name} {px} px: Completed is {comp}x Ready");
                }
            }
        }

        // The mix: Ready from one set beside the rest from another, on each dark window, row tier and 48 px.
        var mixes = dark.GetProperty("mix").EnumerateArray().ToArray();
        Assert.Equal(2 * 2 * 2 * 2 * (Measured.Length - 1), mixes.Length);
        Assert.All(mixes, m => Assert.True(m.GetProperty("lead").GetDouble() >= MixReadyLead, $"{set}: {m}"));
    }

    [Fact]
    public void Every_set_records_the_same_halo_per_dark_palette_the_least_that_passes()
    {
        var halos = Measured.Select(set =>
        {
            using var json = Json(ThemesDir(), set, "metrics.json");
            return json.RootElement.GetProperty("dark").GetProperty("halo").GetRawText();
        }).Distinct().ToArray();
        Assert.Single(halos);
    }

    // ------------------------------------------------------------------ the cross-set table per vision mode (1.17)

    private static readonly string[] CrossModes = ["grey", "deut", "machado-deut", "machado-prot", "machado-trit"];

    [Theory]
    [MemberData(nameof(MeasuredSets))]
    public void The_cross_set_table_is_recorded_per_vision_mode_in_the_neutral_kit(string set)
    {
        using var json = Json(ThemesDir(), set, "metrics.json");
        var cross = json.RootElement.GetProperty("cross");
        Assert.Equal("brass", cross.GetProperty("kit").GetString());
        var worst = cross.GetProperty("sets");
        var modes = cross.GetProperty("modes");
        Assert.Equal(Measured.Where(s => s != set).Order(StringComparer.Ordinal), modes.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
        foreach (var other in modes.EnumerateObject())
        {
            foreach (var which in new[] { "row", "hero" })
            {
                foreach (var px in new[] { "16", "20" })
                {
                    var byMode = other.Value.GetProperty(which).GetProperty(px);
                    Assert.Equal(CrossModes.Order(StringComparer.Ordinal), byMode.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal));
                    foreach (var pair in worst.GetProperty(other.Name).GetProperty(which).GetProperty(px).EnumerateObject())
                    {
                        // The worst-of-modes value is the least of the per-mode values.
                        var least = CrossModes.Min(m => byMode.GetProperty(m).GetProperty(pair.Name).GetDouble());
                        Assert.Equal(least, pair.Value.GetDouble(), 0.011);
                    }

                    Assert.All(CrossModes, m => Assert.Equal(States.Length * (States.Length - 1), byMode.GetProperty(m).EnumerateObject().Count()));
                }
            }
        }
    }
}
