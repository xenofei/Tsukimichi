using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The per-state mix's numbers (plan v7 T10; spec-1.17 §A3): the compiled <c>MixTable.g.cs</c> is the shipped sets'
/// <c>metrics.json</c> (own pairs and cross pairs per vision mode at the row tier, 16 px on Night, and the neutral-kit
/// salience), so the page judges what the build measured.
/// </summary>
public sealed class MixTableTests
{
    private static readonly string[] Modes = ["grey", "deut", "machado-deut", "machado-prot", "machado-trit"];

    private static readonly Dictionary<string, GlyphSetId> Sets = new()
    {
        ["medallion"] = GlyphSetId.Medallion,
        ["ishgard-glass"] = GlyphSetId.IshgardGlass,
        ["aether-crystal"] = GlyphSetId.AetherCrystal,
        ["astrologian-orrery"] = GlyphSetId.Orrery,
    };

    private static Dictionary<string, JsonElement> LoadMetrics()
    {
        var all = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var folder in Directory.GetDirectories(Path.Combine(OrnamentLayoutTests.AssetsDir(), "themes")))
        {
            var path = Path.Combine(folder, "metrics.json");
            if (File.Exists(path))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                all[Path.GetFileName(folder)] = json.RootElement.Clone();
            }
        }

        return all;
    }

    [Fact]
    public void Every_shipped_set_with_cross_numbers_is_in_the_table()
    {
        var metrics = LoadMetrics();
        var measured = metrics.Where(static m => m.Value.GetProperty("cross").TryGetProperty("neutralSalience", out _)).Select(static m => Sets[m.Key]).Order().ToArray();
        Assert.Equal(measured, MixTable.Sets.ToArray());

        // The Orrery's cross-set numbers exist, so it joins the per-state lists (the owner's decision); Sumi does not.
        Assert.Contains(GlyphSetId.Orrery, MixTable.Sets);
        Assert.DoesNotContain(GlyphSetId.Sumi, MixTable.Sets);

        // Within-set pairs never warn because every set passed its own gates in the build.
        foreach (var (_, m) in metrics)
        {
            Assert.True(m.GetProperty("pass").GetBoolean());
        }
    }

    [Fact]
    public void The_cross_pairs_are_every_sets_recorded_table_per_mode()
    {
        var metrics = LoadMetrics();
        var checkedPairs = 0;
        foreach (var (key, m) in metrics)
        {
            var set = Sets[key];
            foreach (var other in m.GetProperty("cross").GetProperty("modes").EnumerateObject())
            {
                var otherSet = Sets[other.Name];
                var table = other.Value.GetProperty("row").GetProperty("16");
                for (var mode = 0; mode < Modes.Length; mode++)
                {
                    foreach (var pair in table.GetProperty(Modes[mode]).EnumerateObject())
                    {
                        var (a, b) = States(pair.Name);
                        Assert.True(MixTable.TryPair(set, a, otherSet, b, (VisionMode)mode, out var value), $"{key} {pair.Name} {other.Name}");
                        Assert.Equal((float)pair.Value.GetDouble(), value);
                        checkedPairs++;
                    }
                }
            }
        }

        // Four sets, twelve ordered pairs, 56 state pairs each, five modes.
        Assert.Equal(4 * 3 * 56 * 5, checkedPairs);
    }

    [Fact]
    public void The_own_pairs_and_the_salience_are_every_sets_recorded_numbers()
    {
        foreach (var (key, m) in LoadMetrics())
        {
            var set = Sets[key];
            var row = m.GetProperty("tiers").EnumerateArray().Single(static t => t.GetProperty("name").GetString() == "row");
            foreach (var measure in row.GetProperty("measures").EnumerateArray())
            {
                if (measure.GetProperty("ground").GetString() != "night" || measure.GetProperty("px").GetInt32() != 16)
                {
                    continue;
                }

                var mode = (VisionMode)Array.IndexOf(Modes, measure.GetProperty("mode").GetString());
                foreach (var pair in measure.GetProperty("pairs").EnumerateObject())
                {
                    var (a, b) = States(pair.Name);
                    Assert.True(MixTable.TryPair(set, a, set, b, mode, out var value));
                    Assert.Equal((float)pair.Value.GetDouble(), value);
                    Assert.True(MixTable.TryPair(set, b, set, a, mode, out var reversed));
                    Assert.Equal(value, reversed);
                }
            }

            var salience = m.GetProperty("cross").GetProperty("neutralSalience");
            foreach (var (tier, name) in new[] { (MixTier.Row, "row"), (MixTier.Hero, "hero") })
            {
                foreach (var state in salience.GetProperty(name).EnumerateObject())
                {
                    Assert.True(AppearanceStates.TryParse(state.Name, out var s));
                    Assert.True(MixTable.TrySalience(set, s, tier, out var value));
                    Assert.Equal((float)state.Value.GetDouble(), value);
                }
            }
        }
    }

    [Fact]
    public void The_bars_are_the_supervisors_per_mode_bars()
    {
        Assert.Equal(12f, MixTable.CloseBar(VisionMode.Grey));
        Assert.Equal(12f, MixTable.CloseBar(VisionMode.Deut));
        Assert.Equal(11f, MixTable.CloseBar(VisionMode.MachadoDeut));
        Assert.Equal(11f, MixTable.CloseBar(VisionMode.MachadoProt));
        Assert.Equal(11f, MixTable.CloseBar(VisionMode.MachadoTrit));
        Assert.Equal(10f, MixTable.HardBar);
        Assert.Equal(1.25f, MixTable.ReadyLeadBar);
        Assert.Equal(0.80f, MixTable.CompletedOfReadyBar);

        // Judged at one decimal, as the build judges: 11.96 reads 12.0, 11.95 reads 11.9.
        Assert.False(MixTable.Under(11.96f, 12f));
        Assert.True(MixTable.Under(11.95f, 12f));
        Assert.False(MixTable.Under(11f, 11f));

        // Ratios at two decimals.
        Assert.False(MixTable.RatioUnder(1.2496f, 1.25f));
        Assert.True(MixTable.RatioUnder(1.244f, 1.25f));
        Assert.False(MixTable.RatioOver(0.8004f, 0.80f));
        Assert.True(MixTable.RatioOver(0.806f, 0.80f));
    }

    [Fact]
    public void An_unmeasured_set_or_the_same_state_twice_has_no_number()
    {
        Assert.False(MixTable.TryPair(GlyphSetId.Sumi, QuestState.Ready, GlyphSetId.Medallion, QuestState.Blocked, VisionMode.Grey, out _));
        Assert.False(MixTable.TryPair(GlyphSetId.Medallion, QuestState.Ready, GlyphSetId.AetherCrystal, QuestState.Ready, VisionMode.Grey, out _));
        Assert.False(MixTable.TrySalience(GlyphSetId.Classic, QuestState.Ready, MixTier.Row, out _));
    }

    private static (QuestState A, QuestState B) States(string pair)
    {
        var parts = pair.Split('|');
        Assert.True(AppearanceStates.TryParse(parts[0], out var a));
        Assert.True(AppearanceStates.TryParse(parts[1], out var b));
        return (a, b);
    }
}
