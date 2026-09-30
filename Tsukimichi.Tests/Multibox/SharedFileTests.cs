using System.Text.Json;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Multibox;

/// <summary>
/// Saves that two game clients can race (D11): the merge rule for the shared keyed files, and concurrent writers on one
/// folder that must leave neither a torn file nor a lost entry. The writers are threads with their own file handles,
/// which Windows locks and renames exactly as it does for two processes.
/// </summary>
public sealed class SharedFileTests : IDisposable
{
    private const int Saves = 150;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    [Fact]
    public void Merge_takes_touched_keys_from_local_and_the_rest_from_disk()
    {
        var disk = new Dictionary<int, string> { [1] = "disk-1", [2] = "disk-2", [3] = "disk-3" };
        var local = new Dictionary<int, string> { [1] = "local-1", [2] = "stale-2", [4] = "local-4" };

        var merged = KeyedMerge.Apply(disk, local, [1, 3, 4]);

        // 1 changed here, 3 removed here, 4 added here; 2 was not touched, so the disk's newer value stays.
        Assert.Equal(new Dictionary<int, string> { [1] = "local-1", [2] = "disk-2", [4] = "local-4" }, merged);
    }

    [Fact]
    public void Merge_with_nothing_touched_is_the_disk()
    {
        var disk = new Dictionary<int, string> { [1] = "a" };

        Assert.Equal(disk, KeyedMerge.Apply(disk, new Dictionary<int, string> { [2] = "b" }, []));
    }

    [Fact]
    public void Saving_pins_keeps_another_clients_pins_for_other_characters()
    {
        var path = tmp.File(Path.Combine("user", "pins.json"));
        // This client read the file when character 2 had one pin...
        var local = new Dictionary<ulong, List<uint>> { [1] = [], [2] = [100] };
        // ...then another client pinned a second quest for character 2.
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [2] = [100, 200] });

        local[1].Add(66038);
        var merged = PinsFile.SaveMerged(path, local, [1UL]);

        var onDisk = PinsFile.Load(path);
        Assert.Equal([66038u], onDisk[1]);
        Assert.Equal([100u, 200u], onDisk[2]);
        Assert.Equal(onDisk.Keys.Order(), merged.Keys.Order());
    }

    [Fact]
    public void Saving_pins_drops_emptied_characters()
    {
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [1] = [5], [2] = [6] });

        PinsFile.SaveMerged(path, new Dictionary<ulong, List<uint>> { [1] = [] }, [1UL]);

        Assert.Equal([2UL], PinsFile.Load(path).Keys);
    }

    [Fact]
    public void Saving_overrides_keeps_another_clients_verdicts_and_removes_only_the_touched_one()
    {
        var path = tmp.File("overrides.json");
        OverridesFile.Save(path, new Dictionary<uint, UniqueOverride>
        {
            [10] = new(true, null),
            [20] = new(false, "theirs"),
        });

        // This client knew only 10 and cleared it; 20 arrived from another client meanwhile.
        OverridesFile.SaveMerged(path, new Dictionary<uint, UniqueOverride>(), [10u]);

        var onDisk = OverridesFile.Load(path);
        Assert.Equal([20u], onDisk.Keys);
        Assert.Equal("theirs", onDisk[20].Note);
    }

    [Fact]
    public void A_merged_save_refuses_to_replace_a_file_it_cannot_read()
    {
        var path = tmp.File("pins.json");
        PinsFile.Save(path, new Dictionary<ulong, List<uint>> { [2] = [7] });

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => PinsFile.SaveMerged(path, new Dictionary<ulong, List<uint>> { [1] = [5] }, [1UL]));
        }

        Assert.Equal([2UL], PinsFile.Load(path).Keys);
    }

    [Fact]
    public void The_lock_waits_for_its_holder_and_gives_up_after_the_timeout()
    {
        var path = tmp.File("pins.json");
        using (SharedFile.Lock(path))
        {
            Assert.Throws<IOException>(() => SharedFile.Lock(path, TimeSpan.FromMilliseconds(100)).Dispose());
        }

        // Released on dispose, and the lock file with it.
        using (SharedFile.Lock(path, TimeSpan.FromMilliseconds(100)))
        {
        }

        Assert.False(File.Exists(path + SharedFile.LockSuffix));
    }

    [Fact]
    public async Task Two_clients_pinning_at_once_lose_no_pin_and_never_leave_a_torn_file()
    {
        var path = tmp.File(Path.Combine("user", "pins.json"));
        using var stop = new CancellationTokenSource();
        var torn = 0;
        var reads = 0;
        var reader = Task.Factory.StartNew(
            () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    var text = AtomicFile.Read(path);
                    if (text is null)
                    {
                        continue;
                    }

                    reads++;
                    try
                    {
                        JsonSerializer.Deserialize<Dictionary<ulong, List<uint>>>(text);
                    }
                    catch (JsonException)
                    {
                        torn++;
                    }

                    Thread.Sleep(1);
                }
            },
            TaskCreationOptions.LongRunning);

        var a = Task.Factory.StartNew(() => PinMany(path, 1), TaskCreationOptions.LongRunning);
        var b = Task.Factory.StartNew(() => PinMany(path, 2), TaskCreationOptions.LongRunning);
        await Task.WhenAll(a, b);
        await stop.CancelAsync();
        await reader;

        var pins = PinsFile.Load(path);
        var expected = Enumerable.Range(0, Saves).Select(static i => (uint)i).ToList();
        Assert.Equal(expected, pins[1]);
        Assert.Equal(expected, pins[2]);
        Assert.Equal(0, torn);
        Assert.True(reads > 0);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*" + AtomicFile.TempSuffix));
        Assert.False(File.Exists(path + SharedFile.LockSuffix));
    }

    [Fact]
    public async Task Two_clients_marking_verdicts_at_once_lose_none()
    {
        var path = tmp.File("overrides.json");

        void MarkMany(uint first)
        {
            var local = new Dictionary<uint, UniqueOverride>();
            for (var i = 0u; i < Saves; i++)
            {
                local[first + i] = new UniqueOverride(true, null);
                local = OverridesFile.SaveMerged(path, local, [first + i], lockTimeout: TimeSpan.FromSeconds(30));
            }
        }

        await Task.WhenAll(
            Task.Factory.StartNew(() => MarkMany(1_000), TaskCreationOptions.LongRunning),
            Task.Factory.StartNew(() => MarkMany(2_000), TaskCreationOptions.LongRunning));

        var verdicts = OverridesFile.Load(path);
        Assert.Equal(2 * Saves, verdicts.Count);
    }

    [Fact]
    public async Task Two_writers_on_one_file_leave_one_whole_save_every_time()
    {
        var path = tmp.File(Path.Combine("characters", "42.json"));
        var contents = new[] { new string('a', 64 * 1024), new string('b', 96 * 1024) };
        using var stop = new CancellationTokenSource();
        var torn = 0;
        var reader = Task.Factory.StartNew(
            () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    var text = AtomicFile.Read(path);
                    if (text is not null && text != contents[0] && text != contents[1])
                    {
                        torn++;
                    }

                    // Another client reads a file now and then, not in a loop that never lets go of it.
                    Thread.Sleep(1);
                }
            },
            TaskCreationOptions.LongRunning);

        await Task.WhenAll(
            Task.Factory.StartNew(() => WriteMany(path, contents[0]), TaskCreationOptions.LongRunning),
            Task.Factory.StartNew(() => WriteMany(path, contents[1]), TaskCreationOptions.LongRunning));
        await stop.CancelAsync();
        await reader;

        Assert.Equal(0, torn);
        Assert.Contains(File.ReadAllText(path), contents);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*" + AtomicFile.TempSuffix));
    }

    /// <summary>One client pinning quests 0..<see cref="Saves"/>-1 for its character, saving (merged) after each.</summary>
    private static void PinMany(string path, ulong character)
    {
        var local = new Dictionary<ulong, List<uint>> { [character] = [] };
        for (var i = 0u; i < Saves; i++)
        {
            local[character].Add(i);
            // The saved map is adopted, as the plugin does: the other client's pins come in, this one's list stays.
            local = PinsFile.SaveMerged(path, local, [character], lockTimeout: TimeSpan.FromSeconds(30));
        }
    }

    private static void WriteMany(string path, string content)
    {
        for (var i = 0; i < Saves; i++)
        {
            AtomicFile.Write(path, content);
        }
    }
}
