using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Multibox;

/// <summary>The multibox worker's look at <c>characters/</c> (D11): what another client saved, removed, and who is live.</summary>
public sealed class FolderScanTests : IDisposable
{
    private static readonly IReadOnlySet<ulong> None = new HashSet<ulong>();

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Dir => tmp.File("characters");

    private static CharacterSnapshot Snapshot(ulong id, int minute) => new()
    {
        ContentId = id,
        Name = "Character " + id,
        World = 74,
        TakenUtc = new DateTime(2026, 9, 30, 20, minute, 0, DateTimeKind.Utc),
        CompletedBits = [0xFF],
    };

    [Fact]
    public void The_first_scan_reads_every_snapshot_and_the_next_only_what_changed()
    {
        var other = new JsonSnapshotStore(tmp.Path);
        other.Save(Snapshot(1, 0));
        other.Save(Snapshot(2, 0));
        var mine = new JsonSnapshotStore(tmp.Path);

        var first = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), mine, None);
        Assert.Equal([1UL, 2UL], first.Changed.Select(static s => s.ContentId).Order());

        var quiet = FolderScan.Run(Dir, first.Stamps, mine, None);
        Assert.Empty(quiet.Changed);
        Assert.Empty(quiet.Removed);

        other.Save(Snapshot(2, 5));
        var changed = FolderScan.Run(Dir, quiet.Stamps, mine, None);
        var fresh = Assert.Single(changed.Changed);
        Assert.Equal(2UL, fresh.ContentId);
        Assert.Equal(5, fresh.TakenUtc.Minute);
    }

    [Fact]
    public void A_deleted_snapshot_is_reported_removed()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(Snapshot(1, 0));
        store.Save(Snapshot(2, 0));
        var first = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), store, None);

        store.Delete(1);
        var after = FolderScan.Run(Dir, first.Stamps, store, None);

        Assert.Equal([1UL], after.Removed);
        Assert.Empty(after.Changed);
    }

    [Fact]
    public void The_character_live_here_is_never_read_back_but_its_stamp_is_kept()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(Snapshot(1, 0));

        var scan = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), store, new HashSet<ulong> { 1 });

        Assert.Empty(scan.Changed);
        Assert.True(scan.Stamps.ContainsKey(1));
        Assert.Empty(scan.Removed);
    }

    [Fact]
    public void Heartbeats_sidecars_and_temporary_files_are_not_characters()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(Snapshot(1, 0));
        HeartbeatFile.Write(Dir, new Heartbeat(1, "Character 1", 74, 2000, "cccc", DateTime.UtcNow, DateTime.UtcNow));
        File.WriteAllText(Path.Combine(Dir, "1.accepted.json"), "{}");
        File.WriteAllText(Path.Combine(Dir, "1.json.2000-abcdef.tmp"), "{ half");

        var scan = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), store, None);

        Assert.Equal([1UL], scan.Changed.Select(static s => s.ContentId));
        Assert.Equal([1UL], scan.Heartbeats.Select(static h => h.ContentId));
        Assert.Empty(scan.Warnings);
    }

    [Fact]
    public void A_missing_folder_scans_as_empty()
    {
        var scan = FolderScan.Run(Dir, new Dictionary<ulong, FileStamp>(), new JsonSnapshotStore(tmp.Path), None);

        Assert.Empty(scan.Changed);
        Assert.Empty(scan.Heartbeats);
    }

    [Fact]
    public void Stale_temporary_files_are_swept_and_fresh_ones_kept()
    {
        Directory.CreateDirectory(Dir);
        var old = Path.Combine(Dir, "1.json.2000-aaaaaa.tmp");
        var fresh = Path.Combine(Dir, "2.json.2000-bbbbbb.tmp");
        File.WriteAllText(old, "x");
        File.WriteAllText(fresh, "y");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));

        Assert.Equal(1, AtomicFile.DeleteStaleTemps(Dir, TimeSpan.FromHours(1)));
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
    }
}
