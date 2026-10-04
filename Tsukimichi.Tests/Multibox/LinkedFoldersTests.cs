using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Multibox;

/// <summary>
/// Other launcher folders' characters (plan v7, 1.21.0 P3): which folder a path stands for, Auto-detect, and the
/// characters a scan of each folder gives the roster: read only, this folder's own characters left to it, live while
/// another process's heartbeat is fresh.
/// </summary>
public sealed class LinkedFoldersTests : IDisposable
{
    private static readonly ClientIdentity Me = new(1000, "me");

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static CharacterSnapshot Snapshot(ulong id, string name) => new()
    {
        ContentId = id,
        Name = name,
        World = 74,
        TakenUtc = new DateTime(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc),
        CompletedBits = [0xFF],
    };

    /// <summary>A roaming folder with a Tsukimichi config folder holding <paramref name="ids"/>; returns the config folder.</summary>
    private string Roaming(string name, params ulong[] ids)
    {
        var config = Path.Combine(tmp.Path, name, LinkedFolders.ConfigsFolder, LinkedFolders.PluginFolder);
        Directory.CreateDirectory(Path.Combine(config, LinkedFolders.CharactersFolder));
        var store = new JsonSnapshotStore(config);
        foreach (var id in ids)
        {
            store.Save(Snapshot(id, "Character " + id));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(config));
    }

    private static FolderScanResult Scan(LinkedFolders folders, string folder) =>
        FolderScan.Run(Path.Combine(folder, LinkedFolders.CharactersFolder), folders.KnownStamps(folder), new JsonSnapshotStore(folder), new HashSet<ulong>());

    [Fact]
    public void A_path_resolves_from_the_roaming_folder_down_to_tsukimichi()
    {
        var config = Roaming("XIVLauncher2", 7);
        var roaming = Path.Combine(tmp.Path, "XIVLauncher2");
        Assert.Equal(config, LinkedFolders.Resolve(roaming));
        Assert.Equal(config, LinkedFolders.Resolve(Path.Combine(roaming, LinkedFolders.ConfigsFolder)));
        Assert.Equal(config, LinkedFolders.Resolve("\"" + config + "\""));
        Assert.Null(LinkedFolders.Resolve(tmp.Path));
        Assert.Null(LinkedFolders.Resolve("   "));
    }

    [Fact]
    public void Auto_detect_finds_the_other_roaming_folders_not_this_one()
    {
        var own = Roaming("XIVLauncher", 1);
        var second = Roaming("XIVLauncher2", 2);
        var third = Roaming("Box3", 3);
        Directory.CreateDirectory(Path.Combine(tmp.Path, "Unrelated"));

        Assert.Equal([third, second], LinkedFolders.Candidates(own, [tmp.Path, tmp.Path]));
    }

    [Fact]
    public void Linked_characters_are_read_once_and_never_this_folders_own()
    {
        var a = Roaming("A", 10, 11);
        var b = Roaming("B", 11, 12, 1);
        var folders = new LinkedFolders();
        folders.Take(a, Scan(folders, a));
        folders.Take(b, Scan(folders, b));

        var shown = folders.Characters([a, b], new HashSet<ulong> { 1 }, Me, DateTime.UtcNow);
        Assert.Equal([10UL, 11UL, 12UL], shown.Select(static c => c.ContentId));
        Assert.Equal(a, shown.Single(static c => c.ContentId == 11).Folder);
        Assert.All(shown, static c => Assert.False(c.Live));

        // Unlinking a folder drops its characters.
        folders.Keep([b]);
        Assert.Equal([11UL, 12UL], folders.Characters([b], new HashSet<ulong> { 1 }, Me, DateTime.UtcNow).Select(static c => c.ContentId));
    }

    [Fact]
    public void A_fresh_heartbeat_of_another_process_marks_a_linked_character_live()
    {
        var folder = Roaming("A", 10, 11);
        var now = DateTime.UtcNow;
        var characters = Path.Combine(folder, LinkedFolders.CharactersFolder);
        HeartbeatFile.Write(characters, new Heartbeat(10, "Character 10", 74, 2000, "other", now, now));
        HeartbeatFile.Write(characters, new Heartbeat(11, "Character 11", 74, 3000, "gone", now.AddMinutes(-5), now.AddMinutes(-5)));
        var folders = new LinkedFolders();
        folders.Take(folder, Scan(folders, folder));

        var shown = folders.Characters([folder], new HashSet<ulong>(), Me, now);
        Assert.True(shown.Single(static c => c.ContentId == 10).Live);
        Assert.False(shown.Single(static c => c.ContentId == 11).Live);
    }

    [Fact]
    public void Reading_a_linked_folder_never_writes_or_moves_anything_there()
    {
        var folder = Roaming("A", 10);
        var characters = Path.Combine(folder, LinkedFolders.CharactersFolder);
        File.WriteAllText(Path.Combine(characters, "99.json"), "{ not json");
        var before = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(f => (f, File.GetLastWriteTimeUtc(f), new FileInfo(f).Length)).Order().ToArray();

        var folders = new LinkedFolders();
        var result = Scan(folders, folder);
        folders.Take(folder, result);
        folders.Take(folder, Scan(folders, folder));

        var after = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(f => (f, File.GetLastWriteTimeUtc(f), new FileInfo(f).Length)).Order().ToArray();
        Assert.Equal(before, after);
        Assert.NotEmpty(result.Warnings);
        Assert.Equal([10UL], folders.Characters([folder], new HashSet<ulong>(), Me, DateTime.UtcNow).Select(static c => c.ContentId));
    }
}
