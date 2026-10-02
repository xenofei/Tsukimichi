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
    public void Pins_keep_the_order_they_were_pinned_across_save_and_load()
    {
        // Not sorted: the order is the order pinned (a route's steps), which the Todo overlay lists them in.
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [1] = [66038, 65576, 69000, 65577] });

        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        Assert.Equal([66038, 65576, 69000, 65577], json["1"]!.AsArray().Select(static n => (int)n!));
        Assert.Equal([66038u, 65576, 69000, 65577], PinsFile.Load(path)[1]);
    }

    [Fact]
    public void Pins_written_by_an_earlier_version_load_in_their_order()
    {
        // The shape every release wrote (content id string to an array of row ids); nothing to migrate.
        var path = tmp.File("pins.json");
        File.WriteAllText(path, """{ "18014398509481985": [ 67000, 65600, 66100 ], "2": [ 5 ] }""");

        var loaded = PinsFile.Load(path);

        Assert.Equal([67000u, 65600, 66100], loaded[18014398509481985UL]);
        Assert.Equal([5u], loaded[2]);
    }

    [Fact]
    public void A_null_pin_list_from_a_hand_edited_file_has_no_entry()
    {
        // The query runner, the chat notices and the overlay walk a character's list as they find it: a null one
        // would throw on every pin toggle or character switch.
        var path = tmp.File("pins.json");
        File.WriteAllText(path, """{ "1": null, "2": [ 5 ] }""");

        var loaded = PinsFile.Load(path);
        var shared = PinsFile.LoadShared(path);

        Assert.False(loaded.ContainsKey(1));
        Assert.Equal([5u], loaded[2]);
        Assert.True(shared.IsLoaded);
        Assert.False(shared.Value!.ContainsKey(1));
        Assert.Equal([5u], shared.Value[2]);
        Assert.True(File.Exists(path), "a null list is not a reason to quarantine the file");
    }

    [Fact]
    public void Pin_edits_append_new_pins_after_the_ones_held_and_keep_their_order()
    {
        // Multibox (D11): another client pinned 30 after this one loaded [10, 20]; this client then pins 40 and 10
        // (already held) and unpins nothing. The file keeps the existing order and appends only what is new.
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [1] = [10, 20, 30], [2] = [7] });
        var local = new Dictionary<ulong, List<uint>> { [1] = [10, 20, 40], [2] = [7] };

        var merged = PinsFile.SaveChanges(path, [new PinChange(1, 40, PinChangeKind.Pin), new PinChange(1, 10, PinChangeKind.Pin)], local);

        Assert.Equal([10u, 20, 30, 40], merged[1]);
        Assert.Equal([10u, 20, 30, 40], PinsFile.Load(path)[1]);
        Assert.Equal([7u], PinsFile.Load(path)[2]);
    }

    [Fact]
    public void Pin_edits_replayed_on_a_reload_keep_the_disk_order_first()
    {
        // ReloadPinsFromDisk: the file as another client wrote it, then this client's unsaved edits in order.
        var disk = new Dictionary<ulong, List<uint>> { [1] = [3, 1, 2] };

        PinsFile.Apply(disk, [new PinChange(1, 9, PinChangeKind.Pin), new PinChange(1, 1, PinChangeKind.Unpin), new PinChange(1, 4, PinChangeKind.Pin), new PinChange(1, 3, PinChangeKind.Pin)]);

        Assert.Equal([3u, 2, 9, 4], disk[1]);
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
        var marked = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Utc);
        var overrides = new Dictionary<uint, UniqueOverride>
        {
            [66038] = new UniqueOverride(true, "Actually unique", marked),
            [65576] = new UniqueOverride(false, null),
        };

        OverridesFile.Save(path, overrides);
        var loaded = OverridesFile.Load(path);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(new UniqueOverride(true, "Actually unique", marked), loaded[66038]);
        Assert.Equal(DateTimeKind.Utc, loaded[66038].MarkedUtc!.Value.Kind);
        Assert.Equal(new UniqueOverride(false, null), loaded[65576]);
        Assert.Null(loaded[65576].MarkedUtc);
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
