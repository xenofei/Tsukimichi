using System.Security.Cryptography;
using System.Text;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The quest-giver photos that ship inside the plugin (<see cref="BundledPortraits"/>): the bundled pack loads at start
/// and every image is whole; an incomplete folder is no pack rather than a broken one; Settings › Look › Giver portraits
/// shows and hides the photos; Game art + photos becomes the default once; and the one-time clean-up deletes only what
/// the 1.20–1.22 download wrote in the config folder. No network and no game.
/// </summary>
public sealed class BundledPortraitsTests : IDisposable
{
    private const uint Giver = 1009999;

    private readonly string root = Path.Combine(Path.GetTempPath(), "tsukimichi-bundled-" + Guid.NewGuid().ToString("N")[..8]);

    public BundledPortraitsTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder; the OS clears it.
        }
    }

    private static string ShippedFolder => Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "assets", BundledPortraits.FolderName);

    // ------------------------------------------------------------------ the bundled pack

    [Fact]
    public void The_bundled_photos_load_at_start_with_each_givers_image()
    {
        var pack = BundledPortraits.Load(ShippedFolder, out var problem);

        Assert.Null(problem);
        Assert.NotNull(pack);
        Assert.Equal(BundledPortraits.Tag, pack.Tag);
        Assert.True(pack.Faces > 1000, $"{pack.Faces} photos");
        Assert.True(pack.Givers >= pack.Faces);
        Assert.Equal(64, pack.Sha256.Length);
        foreach (var (npc, file) in pack.Manifest.Entries)
        {
            Assert.True(pack.TryGetPath(npc, out var path));
            Assert.Equal(Path.Combine(pack.Folder, file), path);
            Assert.True(pack.BoxOf(npc) >= 72, $"{npc}: a photo cut from under 72 px would upscale on the plate");
        }
    }

    [Fact]
    public void Every_bundled_image_matches_its_hash_and_decodes_at_the_packs_side()
    {
        // The plugin checks only that each listed image is there at start; the hashes and the decode are checked here
        // (and by the builder before it bundles), so a damaged file never ships.
        var manifest = PortraitPackManifest.TryParse(File.ReadAllBytes(Path.Combine(ShippedFolder, PortraitPackManifest.FileName)), out var error);
        Assert.Null(error);
        Assert.NotNull(manifest);
        var bad = new List<string>();
        foreach (var (name, sha) in manifest.Files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(ShippedFolder, name));
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != sha)
            {
                bad.Add($"{name}: hash");
            }
            else if (!PackPng.TryDecode(bytes, out var width, out var height, out _, strict: true) || width != manifest.Side || height != manifest.Side)
            {
                bad.Add($"{name}: does not decode at {manifest.Side} px");
            }
        }

        var unlisted = Directory.EnumerateFiles(ShippedFolder).Select(Path.GetFileName).Where(n => n != PortraitPackManifest.FileName && !manifest.Files.ContainsKey(n!)).ToList();
        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
        Assert.Empty(unlisted);
    }

    [Fact]
    public void The_plugin_packages_the_photos_with_its_other_assets()
    {
        var project = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        Assert.Contains("""<Content Include="assets\portraits\*.png;assets\portraits\manifest.json" CopyToOutputDirectory="PreserveNewest" />""", project, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_without_a_manifest_or_missing_an_image_is_no_pack()
    {
        Assert.Null(BundledPortraits.Load(Path.Combine(root, "nothing-here"), out var none));
        Assert.NotNull(none);

        var folder = WritePack(Path.Combine(root, "pack"));
        Assert.NotNull(BundledPortraits.Load(folder, out _));

        File.WriteAllBytes(Path.Combine(folder, "2.png"), []);
        Assert.Null(BundledPortraits.Load(folder, out var empty));
        Assert.Contains("2.png", empty, StringComparison.Ordinal);

        File.Delete(Path.Combine(folder, "2.png"));
        Assert.Null(BundledPortraits.Load(folder, out var missing));
        Assert.Contains("2.png", missing, StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(folder, PortraitPackManifest.FileName), "{ not json");
        Assert.Null(BundledPortraits.Load(folder, out var broken));
        Assert.NotNull(broken);
    }

    [Fact]
    public void Reading_the_photos_stops_when_the_plugin_unloads()
    {
        var folder = WritePack(Path.Combine(root, "pack"));
        using var unload = new CancellationTokenSource();
        unload.Cancel();
        Assert.Throws<OperationCanceledException>(() => BundledPortraits.Load(folder, out _, unload.Token));
    }

    // ------------------------------------------------------------------ the switch

    [Fact]
    public void Giver_portraits_shows_the_photos_only_under_Game_art_and_photos()
    {
        var pack = BundledPortraits.Load(WritePack(Path.Combine(root, "pack")), out _);
        Assert.NotNull(pack);

        Assert.Same(pack, BundledPortraits.InUse(GiverPortraitMode.GameArtAndPack, pack));
        Assert.Null(BundledPortraits.InUse(GiverPortraitMode.GameArt, pack));
        Assert.Null(BundledPortraits.InUse(GiverPortraitMode.Off, pack));
        Assert.Null(BundledPortraits.InUse(GiverPortraitMode.GameArtAndPack, null));

        // On a plate: the giver the game has no art for wears the photo with photos on, and no face with them off.
        Assert.Equal(PortraitSource.Pack, PortraitIndex.Empty.WithPack(BundledPortraits.InUse(GiverPortraitMode.GameArtAndPack, pack)).For(Giver, 3, 0).Source);
        Assert.NotEqual(PortraitSource.Pack, PortraitIndex.Empty.WithPack(BundledPortraits.InUse(GiverPortraitMode.GameArt, pack)).For(Giver, 3, 0).Source);
    }

    [Fact]
    public void Game_art_and_photos_becomes_the_default_once_unless_game_art_was_chosen_with_the_pack()
    {
        // A configuration saved before, on Game art (the old default): photos on.
        Assert.Equal(GiverPortraitMode.GameArtAndPack, BundledPortraits.ModeOnceBundled(GiverPortraitMode.GameArt, hadFile: true, hadDownloadedPack: false));

        // Game art with the pack downloaded was the player's choice; Off and Game art + pack stay as they are.
        Assert.Equal(GiverPortraitMode.GameArt, BundledPortraits.ModeOnceBundled(GiverPortraitMode.GameArt, hadFile: true, hadDownloadedPack: true));
        Assert.Equal(GiverPortraitMode.Off, BundledPortraits.ModeOnceBundled(GiverPortraitMode.Off, hadFile: true, hadDownloadedPack: false));
        Assert.Equal(GiverPortraitMode.GameArtAndPack, BundledPortraits.ModeOnceBundled(GiverPortraitMode.GameArtAndPack, hadFile: true, hadDownloadedPack: true));

        // A fresh install keeps what it starts with (the new default).
        Assert.Equal(GiverPortraitMode.GameArtAndPack, BundledPortraits.ModeOnceBundled(GiverPortraitMode.GameArtAndPack, hadFile: false, hadDownloadedPack: false));
    }

    [Fact]
    public void An_old_configuration_with_the_packs_fields_still_loads()
    {
        // The plugin's configuration cannot be loaded here (it needs Dalamud; tools/Tsukimichi.LoadCheck deserializes an
        // old file for real). Here: the 1.22 field stays readable and is never written again, and the new default and
        // its one-time move are wired into Load.
        var config = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Config");
        var photos = File.ReadAllText(Path.Combine(config, "Configuration.PortraitPhotos.cs"));
        Assert.Matches(@"\[Newtonsoft\.Json\.JsonProperty\]\s*\[Obsolete\([^)]*\)\]\s*public bool PortraitPackOfferAnswered\s*\{\s*set \{ \}\s*\}", photos);
        var load = File.ReadAllText(Path.Combine(config, "Configuration.cs"));
        Assert.Contains("GiverPortraits { get; set; } = Core.Ui.GiverPortraitMode.GameArtAndPack;", load, StringComparison.Ordinal);
        Assert.Contains("ApplyBundledPhotos(config, hadFile,", load, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the old download, removed once

    [Fact]
    public void The_clean_up_deletes_only_what_the_old_download_wrote()
    {
        // The config folder as 1.22 left it: the pack, a staging copy, a download in progress, and the player's own files.
        var config = Path.Combine(root, "pluginConfigs", "Tsukimichi");
        var portraits = Path.Combine(config, "portraits");
        WritePack(Path.Combine(portraits, "2cd1fa9fd98f"));
        Directory.CreateDirectory(Path.Combine(portraits, "2cd1fa9fd98f.staging-0123abcd"));
        Directory.CreateDirectory(Path.Combine(portraits, "2cd1fa9fd98f.old-89abcdef"));
        File.WriteAllText(Path.Combine(portraits, "current.json"), """{ "folder": "2cd1fa9fd98f" }""");
        File.WriteAllText(Path.Combine(portraits, "download-0123456789ab.part"), "partial");
        File.WriteAllText(Path.Combine(config, "config.json"), "{}");
        Directory.CreateDirectory(Path.Combine(config, "characters"));
        File.WriteAllText(Path.Combine(config, "characters", "1.json"), "{}");

        Assert.True(BundledPortraits.HasDownloadedPack(portraits));
        Assert.Equal(5, BundledPortraits.RemoveDownloaded(portraits));

        Assert.False(Directory.Exists(portraits));
        Assert.False(BundledPortraits.HasDownloadedPack(portraits));
        Assert.True(File.Exists(Path.Combine(config, "config.json")));
        Assert.True(File.Exists(Path.Combine(config, "characters", "1.json")));

        // Once: there is nothing left to remove.
        Assert.Equal(0, BundledPortraits.RemoveDownloaded(portraits));
    }

    [Fact]
    public void Anything_else_in_the_old_folder_is_left_with_the_folder()
    {
        var portraits = Path.Combine(root, "portraits");
        WritePack(Path.Combine(portraits, "abcdefabcdef"));
        File.WriteAllText(Path.Combine(portraits, "current.json"), "{}");
        File.WriteAllText(Path.Combine(portraits, "my notes.txt"), "mine");
        Directory.CreateDirectory(Path.Combine(portraits, "My Photos"));

        Assert.Equal(2, BundledPortraits.RemoveDownloaded(portraits));

        Assert.True(File.Exists(Path.Combine(portraits, "my notes.txt")));
        Assert.True(Directory.Exists(Path.Combine(portraits, "My Photos")));
        Assert.False(Directory.Exists(Path.Combine(portraits, "abcdefabcdef")));
        Assert.False(File.Exists(Path.Combine(portraits, "current.json")));
    }

    [Fact]
    public void No_old_folder_is_nothing_to_do()
    {
        Assert.Equal(0, BundledPortraits.RemoveDownloaded(Path.Combine(root, "never-made")));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A pack folder of two images (1.png for <see cref="Giver"/>, 2.png for another) and its manifest.</summary>
    private static string WritePack(string folder)
    {
        Directory.CreateDirectory(folder);
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["1.png"] = Encoding.ASCII.GetBytes("first"),
            ["2.png"] = Encoding.ASCII.GetBytes("second"),
        };
        foreach (var (name, bytes) in images)
        {
            File.WriteAllBytes(Path.Combine(folder, name), bytes);
        }

        var files = images.ToDictionary(kv => kv.Key, kv => Convert.ToHexStringLower(SHA256.HashData(kv.Value)), StringComparer.Ordinal);
        var manifest = PortraitPackManifest.Create("2026.09.15.0000.0000", new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc), "test", files, new Dictionary<uint, string> { [Giver] = "1.png", [1009998] = "2.png" });
        File.WriteAllBytes(Path.Combine(folder, PortraitPackManifest.FileName), manifest.ToJson());
        return folder;
    }
}
