using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Multibox;

/// <summary>
/// Files another game client owns (D11, 1.2 review): a snapshot a newer plugin wrote, or one this client cannot parse,
/// is never quarantined by the scan or by a load for another client's character; a newer schema is never quarantined by
/// anyone; a merged save onto an unparsable shared file keeps the keys held in memory; and pin edits to one character
/// from two clients both survive.
/// </summary>
public sealed class SharedReadTests : IDisposable
{
    private static readonly IReadOnlySet<ulong> None = new HashSet<ulong>();

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Dir => tmp.File("characters");

    private static CharacterSnapshot Snapshot(ulong id) => new()
    {
        ContentId = id,
        Name = "Character " + id,
        World = 74,
        TakenUtc = new DateTime(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc),
        CompletedBits = [0xFF],
    };

    /// <summary>Writes character <paramref name="id"/> as a plugin with schema <c>CurrentSchemaVersion + 1</c> would.</summary>
    private string WriteNewerSnapshot(ulong id)
    {
        new JsonSnapshotStore(tmp.Path).Save(Snapshot(id));
        var path = Path.Combine(Dir, id + ".json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node["schemaVersion"] = CharacterSnapshot.CurrentSchemaVersion + 1;
        node["someNewField"] = "from the future";
        File.WriteAllText(path, node.ToJsonString());
        return path;
    }

    private string[] Quarantined(string fileName) => Directory.GetFiles(Dir, fileName + ".corrupt-*");

    [Fact]
    public void The_scan_skips_a_newer_schema_snapshot_and_leaves_it_untouched()
    {
        var path = WriteNewerSnapshot(7);
        var before = File.ReadAllText(path);
        var store = new JsonSnapshotStore(tmp.Path);

        var first = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), store, None);

        Assert.Empty(first.Changed);
        Assert.Contains(first.Warnings, static w => w.Contains("newer version", StringComparison.Ordinal));
        Assert.Equal(before, File.ReadAllText(path));
        Assert.Empty(Quarantined("7.json"));
        Assert.Empty(store.Warnings);

        // Not read again (nor warned about) until it changes, and never reported removed.
        var second = FolderScan.Run(Dir, first.Stamps, store, None);
        Assert.Empty(second.Warnings);
        Assert.Empty(second.Removed);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void The_scan_never_quarantines_a_file_it_cannot_parse()
    {
        Directory.CreateDirectory(Dir);
        var path = Path.Combine(Dir, "8.json");
        File.WriteAllText(path, "{ not json");

        var scan = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), new JsonSnapshotStore(tmp.Path), None);

        Assert.Empty(scan.Changed);
        Assert.Single(scan.Warnings);
        Assert.True(File.Exists(path));
        Assert.Empty(Quarantined("8.json"));
    }

    [Fact]
    public void No_load_quarantines_a_newer_schema_snapshot()
    {
        var path = WriteNewerSnapshot(9);
        var store = new JsonSnapshotStore(tmp.Path);

        Assert.Null(store.Load(9));
        Assert.Null(store.Load(9, quarantine: true));
        Assert.Empty(store.List());
        Assert.Equal(SharedLoad.Newer, store.LoadShared(9).Status);

        Assert.True(File.Exists(path));
        Assert.Empty(Quarantined("9.json"));
        Assert.All(store.Warnings, static w => Assert.Contains("left in place", w, StringComparison.Ordinal));
    }

    [Fact]
    public void A_load_for_another_clients_character_leaves_a_corrupt_file_in_place()
    {
        Directory.CreateDirectory(Dir);
        var path = Path.Combine(Dir, "10.json");
        File.WriteAllText(path, "[1,2,3]");
        var store = new JsonSnapshotStore(tmp.Path);

        Assert.Null(store.Load(10, quarantine: false));
        Assert.Equal(SharedLoad.Invalid, store.LoadShared(10).Status);
        Assert.True(File.Exists(path));
        Assert.Empty(Quarantined("10.json"));
        Assert.Single(store.Warnings);

        // The owning client's load still moves a corrupt file aside.
        Assert.Null(store.Load(10));
        Assert.False(File.Exists(path));
        Assert.Single(Quarantined("10.json"));
    }

    [Fact]
    public void A_shared_read_of_user_files_never_quarantines()
    {
        var pins = tmp.File(Path.Combine("user", "pins.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(pins)!);
        File.WriteAllText(pins, "{ torn");

        Assert.Equal(SharedLoad.Invalid, PinsFile.LoadShared(pins).Status);
        Assert.Equal(SharedLoad.Missing, OverridesFile.LoadShared(tmp.File(Path.Combine("user", "overrides.json"))).Status);
        Assert.True(File.Exists(pins));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(pins)!, "*.corrupt-*"));
    }

    [Fact]
    public void A_merged_pin_save_onto_an_unparsable_file_keeps_every_character_held_in_memory()
    {
        var path = tmp.File("pins.json");
        File.WriteAllText(path, "{ torn");
        var local = new Dictionary<ulong, List<uint>> { [1] = [10], [2] = [20, 21], [3] = [30] };

        var merged = PinsFile.SaveMerged(path, local, [1UL]);

        // The untouched characters come from memory, not from an empty map standing in for the unreadable file.
        Assert.Equal([1UL, 2UL, 3UL], merged.Keys.Order());
        Assert.Equal([20u, 21u], PinsFile.Load(path)[2]);
        Assert.Single(Directory.GetFiles(tmp.Path, "pins.json.corrupt-*"));
    }

    [Fact]
    public void Pin_edits_onto_an_unparsable_file_keep_every_character_held_in_memory()
    {
        var path = tmp.File("pins.json");
        File.WriteAllText(path, "{ torn");
        var local = new Dictionary<ulong, List<uint>> { [1] = [10, 11], [2] = [20] };

        var merged = PinsFile.SaveChanges(path, [new PinChange(1, 11, PinChangeKind.Pin)], local);

        Assert.Equal([10u, 11u], merged[1]);
        Assert.Equal([20u], merged[2]);
        Assert.Equal([20u], PinsFile.Load(path)[2]);
    }

    [Fact]
    public void A_merged_verdict_save_onto_an_unparsable_file_keeps_the_verdicts_held_in_memory()
    {
        var path = tmp.File("overrides.json");
        File.WriteAllText(path, "not json at all");
        var local = new Dictionary<uint, UniqueOverride>
        {
            [10] = new(true, "mine"),
            [20] = new(false, "held"),
        };

        var merged = OverridesFile.SaveMerged(path, local, [10u]);

        Assert.Equal([10u, 20u], merged.Keys.Order());
        Assert.Equal("held", OverridesFile.Load(path)[20].Note);
    }

    [Fact]
    public void One_client_unpinning_while_another_pins_on_the_same_character_keeps_both()
    {
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [1] = [100], [2] = [200] });

        // Both clients read the file when character 1 had q100 pinned. A unpins q100; B pins q101.
        var a = new Dictionary<ulong, List<uint>> { [1] = [], [2] = [200] };
        var b = new Dictionary<ulong, List<uint>> { [1] = [100, 101], [2] = [200] };
        PinsFile.SaveChanges(path, [new PinChange(1, 100, PinChangeKind.Unpin)], a);
        var final = PinsFile.SaveChanges(path, [new PinChange(1, 101, PinChangeKind.Pin)], b);

        Assert.Equal([101u], final[1]);
        Assert.Equal([101u], PinsFile.Load(path)[1]);
        Assert.Equal([200u], PinsFile.Load(path)[2]);
    }

    [Fact]
    public async Task Two_clients_editing_one_characters_pins_at_once_lose_no_edit()
    {
        var path = tmp.File(Path.Combine("user", "pins.json"));
        const int Count = 60;
        // Client A pins 0..59 then unpins the even ones; client B pins 1000..1059, all on character 1.
        PinsFile.Save(path, new Dictionary<ulong, List<uint>>());

        void Edit(uint first, bool unpinEven)
        {
            var local = new Dictionary<ulong, List<uint>> { [1] = [] };
            for (var i = 0u; i < Count; i++)
            {
                local = PinsFile.SaveChanges(path, [new PinChange(1, first + i, PinChangeKind.Pin)], local, lockTimeout: TimeSpan.FromSeconds(30));
            }

            if (unpinEven)
            {
                for (var i = 0u; i < Count; i += 2)
                {
                    local = PinsFile.SaveChanges(path, [new PinChange(1, first + i, PinChangeKind.Unpin)], local, lockTimeout: TimeSpan.FromSeconds(30));
                }
            }
        }

        await Task.WhenAll(
            Task.Factory.StartNew(() => Edit(0, unpinEven: true), TaskCreationOptions.LongRunning),
            Task.Factory.StartNew(() => Edit(1000, unpinEven: false), TaskCreationOptions.LongRunning));

        var pins = PinsFile.Load(path)[1];
        var expected = Enumerable.Range(0, Count).Where(static i => i % 2 == 1).Select(static i => (uint)i)
            .Concat(Enumerable.Range(1000, Count).Select(static i => (uint)i))
            .Order();
        Assert.Equal(expected, pins.Order());
    }

    [Fact]
    public void Applying_pin_edits_is_a_set_change_and_idempotent()
    {
        var pins = new Dictionary<ulong, List<uint>> { [1] = [5], [2] = [7] };
        PinChange[] edits =
        [
            new(1, 6, PinChangeKind.Pin),
            new(1, 5, PinChangeKind.Unpin),
            new(1, 6, PinChangeKind.Pin),
            new(2, 0, PinChangeKind.Forget),
            new(3, 9, PinChangeKind.Unpin),
        ];

        PinsFile.Apply(pins, edits);
        PinsFile.Apply(pins, edits);

        Assert.Equal([1UL], pins.Keys);
        Assert.Equal([6u], pins[1]);
    }

    [Fact]
    public void Delete_all_data_keeps_only_the_pins_of_characters_live_elsewhere_and_empties_the_verdicts()
    {
        var pins = tmp.File("pins.json");
        var overrides = tmp.File("overrides.json");
        PinsFile.Save(pins, new Dictionary<ulong, List<uint>> { [1] = [10], [2] = [20] });
        OverridesFile.Save(overrides, new Dictionary<uint, UniqueOverride> { [5] = new(true, null) });

        var kept = PinsFile.KeepOnly(pins, static id => id == 2);
        OverridesFile.Clear(overrides);

        Assert.Equal([2UL], kept.Keys);
        Assert.Equal([2UL], PinsFile.Load(pins).Keys);
        Assert.True(File.Exists(overrides));
        Assert.Empty(OverridesFile.Load(overrides));

        // A merge from a client that still held character 1's pin writes only what it touched: 1 stays gone.
        PinsFile.SaveMerged(pins, new Dictionary<ulong, List<uint>> { [1] = [10], [3] = [30] }, [3UL]);
        Assert.Equal([2UL, 3UL], PinsFile.Load(pins).Keys.Order());
        Assert.False(File.Exists(pins + SharedFile.LockSuffix));
    }
}
