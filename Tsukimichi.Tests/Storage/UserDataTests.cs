using System.Text.Json.Nodes;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class UserDataTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    [Fact]
    public void Pins_round_trip()
    {
        var path = tmp.File(Path.Combine("user", "pins.json"));
        var pins = new Dictionary<ulong, List<uint>>
        {
            [0x0040_0000_0000_0001UL] = [66038, 65576],
            [2] = [],
        };

        PinsFile.Save(path, pins);
        var loaded = PinsFile.Load(path);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(new uint[] { 66038, 65576 }, loaded[0x0040_0000_0000_0001UL]);
        Assert.Empty(loaded[2]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Pins_json_shape_uses_string_content_ids_and_numeric_row_ids()
    {
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [18014398509481985UL] = [66038] });

        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        Assert.True(json.ContainsKey("18014398509481985"));
        Assert.Equal(66038, (int)json["18014398509481985"]![0]!);
    }

    [Fact]
    public void Pins_missing_file_returns_empty()
    {
        var warnings = new List<string>();

        var loaded = PinsFile.Load(tmp.File("nope.json"), warnings);

        Assert.Empty(loaded);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Pins_corrupt_file_is_quarantined_with_warning()
    {
        var path = tmp.File("pins.json");
        File.WriteAllText(path, "{{{");
        var warnings = new List<string>();

        var loaded = PinsFile.Load(path, warnings);

        Assert.Empty(loaded);
        Assert.Single(warnings);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(tmp.Path, "pins.json.corrupt-*"));
    }

    [Fact]
    public void Pins_locked_file_returns_empty_with_warning_and_is_not_moved()
    {
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [1] = [66038] });
        var warnings = new List<string>();

        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var loaded = PinsFile.Load(path, warnings);

            Assert.Empty(loaded);
            Assert.Single(warnings);
            Assert.Contains("pins.json", warnings[0]);
        }

        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(tmp.Path, "pins.json.corrupt-*"));
        Assert.Equal([66038u], PinsFile.Load(path)[1]);
    }

    [Fact]
    public void Overrides_locked_file_returns_empty_with_warning_and_is_not_moved()
    {
        var path = tmp.File("overrides.json");
        OverridesFile.Save(path, new Dictionary<uint, UniqueOverride> { [66038] = new(true, "note") });
        var warnings = new List<string>();

        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Empty(OverridesFile.Load(path, warnings));
            Assert.Single(warnings);
        }

        Assert.True(File.Exists(path));
        Assert.Single(OverridesFile.Load(path));
    }

    [Fact]
    public void Overrides_round_trip()
    {
        var path = tmp.File(Path.Combine("user", "overrides.json"));
        var overrides = new Dictionary<uint, UniqueOverride>
        {
            [66038] = new UniqueOverride(true, "Actually unique"),
            [65576] = new UniqueOverride(false, null),
        };

        OverridesFile.Save(path, overrides);
        var loaded = OverridesFile.Load(path);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(new UniqueOverride(true, "Actually unique"), loaded[66038]);
        Assert.Equal(new UniqueOverride(false, null), loaded[65576]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Overrides_json_shape_uses_string_row_ids_and_unique_note_fields()
    {
        var path = tmp.File("overrides.json");
        OverridesFile.Save(path, new Dictionary<uint, UniqueOverride> { [66038] = new UniqueOverride(true, "n") });

        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        Assert.True((bool)json["66038"]!["unique"]!);
        Assert.Equal("n", (string?)json["66038"]!["note"]);
    }

    [Fact]
    public void Overrides_missing_file_returns_empty()
    {
        var warnings = new List<string>();

        var loaded = OverridesFile.Load(tmp.File("nope.json"), warnings);

        Assert.Empty(loaded);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Overrides_note_may_be_absent_in_json()
    {
        var path = tmp.File("overrides.json");
        File.WriteAllText(path, """{ "66038": { "unique": false } }""");

        var loaded = OverridesFile.Load(path);

        Assert.Equal(new UniqueOverride(false, null), loaded[66038]);
    }

    [Fact]
    public void Overrides_corrupt_file_is_quarantined_with_warning()
    {
        var path = tmp.File("overrides.json");
        File.WriteAllText(path, "[]");
        var warnings = new List<string>();

        var loaded = OverridesFile.Load(path, warnings);

        Assert.Empty(loaded);
        Assert.Single(warnings);
        Assert.Single(Directory.GetFiles(tmp.Path, "overrides.json.corrupt-*"));
    }
}
