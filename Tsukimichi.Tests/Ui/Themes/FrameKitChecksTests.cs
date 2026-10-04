using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The Frames row's warnings (feature plan v7 T11; spec-1.17 §A3's bars): the compiled table is the kits'
/// <c>metrics.json</c> flags, each flag is a value under its bar, and the row shows only what the appearance draws.
/// </summary>
public sealed class FrameKitChecksTests
{
    private static readonly Dictionary<string, GlyphSetId> Sets = new()
    {
        ["medallion"] = GlyphSetId.Medallion,
        ["ishgard-glass"] = GlyphSetId.IshgardGlass,
        ["aether-crystal"] = GlyphSetId.AetherCrystal,
        ["astrologian-orrery"] = GlyphSetId.Orrery,
        ["sumi-to-kinpaku"] = GlyphSetId.Sumi,
    };

    [Fact]
    public void The_compiled_table_is_every_kits_recorded_flags()
    {
        var expected = new List<KitFlag>();
        foreach (var folder in Directory.GetDirectories(Path.Combine(OrnamentLayoutTests.AssetsDir(), "kits")).Order(StringComparer.Ordinal))
        {
            Assert.True(FrameKits.TryGet(Path.GetFileName(folder), out var kit));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "metrics.json")));
            foreach (var face in json.RootElement.GetProperty("faces").EnumerateObject().OrderBy(static f => f.Name, StringComparer.Ordinal))
            {
                var flags = face.Value.GetProperty("flags").EnumerateArray().ToArray();
                if (face.Value.GetProperty("own").GetBoolean())
                {
                    Assert.Empty(flags);
                    continue;
                }

                foreach (var f in flags)
                {
                    Assert.True(AppearanceStates.TryParse(f.GetProperty("a").GetString(), out var a));
                    Assert.True(AppearanceStates.TryParse(f.GetProperty("b").GetString(), out var b));
                    var kind = f.GetProperty("kind").GetString() switch
                    {
                        "pair" => KitFlagKind.Pair,
                        "ready" => KitFlagKind.ReadyLead,
                        _ => KitFlagKind.CompletedRecedes,
                    };
                    expected.Add(new KitFlag(kit.Id, Sets[face.Name], kind, a, b, (float)f.GetProperty("d").GetDouble(), f.GetProperty("level").GetString() == "hard", f.GetProperty("mode").GetString()!.StartsWith("machado", StringComparison.Ordinal), f.GetProperty("px").GetInt32()));
                }
            }
        }

        Assert.NotEmpty(expected);
        Assert.Equal(expected, FrameKitChecks.All);
    }

    [Fact]
    public void Every_pair_a_kits_G1_or_G1c_gate_misses_is_flagged_at_16_and_20_px()
    {
        // The Frames row says in words what the kit's own gates record: each pair a missed G1 (16 and 20 px) or G1c gate
        // names is one of that pairing's compiled flags, so the row never stays quiet about a recorded miss.
        string[] shortNames = ["Rdy", "RoJ", "Jrn", "Blk", "Done", "Comp", "Lock", "NotC"];
        var checkedSizes = new HashSet<int>();
        foreach (var folder in Directory.GetDirectories(Path.Combine(OrnamentLayoutTests.AssetsDir(), "kits")))
        {
            Assert.True(FrameKits.TryGet(Path.GetFileName(folder), out var kit));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "metrics.json")));
            foreach (var face in json.RootElement.GetProperty("faces").EnumerateObject())
            {
                if (face.Value.GetProperty("own").GetBoolean())
                {
                    continue;
                }

                foreach (var gate in face.Value.GetProperty("gates").EnumerateArray())
                {
                    var name = gate.GetProperty("gate").GetString()!;
                    if (!name.StartsWith("G1 weakest pair", StringComparison.Ordinal) && !name.StartsWith("G1c weakest pair", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    checkedSizes.Add(name.Contains(" 20 px", StringComparison.Ordinal) ? 20 : 16);
                    if (gate.GetProperty("pass").GetBoolean())
                    {
                        continue;
                    }

                    var states = gate.GetProperty("detail").GetString()!.Split('-');
                    var a = AppearanceStates.All[Array.IndexOf(shortNames, states[0])];
                    var b = AppearanceStates.All[Array.IndexOf(shortNames, states[1])];
                    Assert.True(
                        FrameKitChecks.All.Any(f => f.Kit == kit.Id && f.Set == Sets[face.Name] && f.Kind == KitFlagKind.Pair && f.A == a && f.B == b),
                        $"{kit.Key} {face.Name}: '{name}' misses {a}-{b}, but the Frames row has no flag for it");
                }
            }
        }

        Assert.Equal([16, 20], checkedSizes.Order());
    }

    [Fact]
    public void Every_flag_is_under_its_bar()
    {
        foreach (var flag in FrameKitChecks.All)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"{flag.Kit} {flag.Set} {flag.A}-{flag.B} {flag.Value}");
            switch (flag.Kind)
            {
                case KitFlagKind.Pair:
                    // G1 and G1c at 16 px; at 20 px only G1 (greyscale and deuteranopia on Night, bar 16).
                    Assert.Contains(flag.Px, new[] { 16, 20 });
                    Assert.False(flag.Px == 20 && flag.ColourVision, name);
                    Assert.True(flag.Value < (flag.Px == 20 ? 16.0 : flag.ColourVision ? 11.0 : 12.0) - 0.04, name);
                    Assert.Equal(flag.Value < 9.95, flag.Hard);
                    break;
                case KitFlagKind.ReadyLead:
                    Assert.Equal(16, flag.Px);
                    Assert.Equal(QuestState.Ready, flag.A);
                    Assert.True(flag.Value < 1.25, name);
                    break;
                default:
                    Assert.True(flag.Value > 0.8, name);
                    break;
            }

            // A set in its own kit is what it ships, and it passed its gates.
            Assert.NotEqual(GlyphSets.Get(flag.Set).DefaultFrames, flag.Kit);
        }
    }

    [Fact]
    public void The_row_names_only_what_the_appearance_draws_in_that_kit()
    {
        // Ishgard Glass in Brass frames: its Locked out and Not checked are hard to tell apart (the supervisor's case).
        var glassInBrass = AppearanceResolver.Resolve(new AppearanceConfig { Theme = "ishgard-glass", Frames = "brass" });
        var flags = FrameKitChecks.For(glassInBrass);
        Assert.Contains(flags, static f => f.Kind == KitFlagKind.Pair && f.Hard && f.Set == GlyphSetId.IshgardGlass
            && new[] { f.A, f.B }.Order().SequenceEqual(new[] { QuestState.Foreclosed, QuestState.Unknown }.Order()));
        Assert.True(flags[0].Hard, "hard pairs come first");

        // The theme's own kit, the default look, high contrast and Classic warn about nothing.
        Assert.Empty(FrameKitChecks.For(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "ishgard-glass" })));
        Assert.Empty(FrameKitChecks.For(ResolvedAppearance.Default));
        Assert.Empty(FrameKitChecks.For(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "ishgard-glass", Frames = "brass", HighContrast = true })));
        Assert.Empty(FrameKitChecks.For(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "classic", Frames = "silver" })));

        // Taking Locked out from another set leaves Glass's Locked out-Not checked pair out of the column.
        var mixed = AppearanceResolver.Resolve(new AppearanceConfig
        {
            Theme = "ishgard-glass",
            Frames = "brass",
            Glyphs = new Dictionary<string, string> { ["locked-out"] = "medallion" },
        });
        Assert.DoesNotContain(FrameKitChecks.For(mixed), static f => f.Set == GlyphSetId.IshgardGlass && (f.A == QuestState.Foreclosed || f.B == QuestState.Foreclosed));
    }
}
