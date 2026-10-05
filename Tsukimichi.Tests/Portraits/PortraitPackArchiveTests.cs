using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The portrait pack's archive (feature plan v7 F4), which the builder writes and checks before it bundles the photos
/// with the plugin: the zip may hold only the pack's own images (no zip-slip, no programs), each image must match its
/// hash and decode, and the writer makes the same bytes from the same images on any machine.
/// </summary>
public sealed class PortraitPackArchiveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tsukimichi-pack-" + Guid.NewGuid().ToString("N")[..8]);

    public PortraitPackArchiveTests() => Directory.CreateDirectory(root);

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

    // ------------------------------------------------------------------ the archive

    [Fact]
    public void A_whole_pack_extracts_its_images_and_manifest()
    {
        var target = Path.Combine(root, "pack");
        Assert.Equal(PortraitPackFailure.None, Extract(PackZip(), target, out var manifest));
        Assert.NotNull(manifest);
        Assert.True(File.Exists(Path.Combine(target, "1001000.png")));
        Assert.True(File.Exists(Path.Combine(target, "1002000.png")));
        Assert.True(File.Exists(Path.Combine(target, PortraitPackManifest.FileName)));
        Assert.Equal(3, Directory.GetFiles(target).Length);
    }

    [Theory]
    [InlineData("../evil.png")]
    [InlineData("portraits/../../evil.png")]
    [InlineData("portraits/../evil.png")]
    [InlineData("/evil.png")]
    [InlineData("C:/evil.png")]
    [InlineData("portraits\\..\\evil.png")]
    [InlineData("portraits/sub/evil.png")]
    [InlineData("portraits/evil.exe")]
    [InlineData("portraits/evil.dll")]
    [InlineData("evil.exe")]
    [InlineData("readme.txt")]
    [InlineData("portraits/EVIL.PNG")]
    public void A_pack_with_any_other_entry_is_refused_before_anything_is_written(string name)
    {
        var zip = PackZip(extra: (name, PackPng.EncodeRgba(new byte[PortraitPackManifest.ImageSide * PortraitPackManifest.ImageSide * 4], PortraitPackManifest.ImageSide, PortraitPackManifest.ImageSide)));
        var target = Path.Combine(root, "nested", "pack");
        Assert.Equal(PortraitPackFailure.UnsafeEntry, Extract(zip, target, out _));
        Assert.False(Directory.Exists(target));
        Assert.Empty(Directory.GetFiles(root, "evil*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("manifest.json", true, null)]
    [InlineData("portraits/", false, null)]
    [InlineData("portraits/1001000.png", false, "1001000.png")]
    public void The_pack_layout_is_recognised(string name, bool isManifest, string? image)
    {
        Assert.True(PortraitPackArchive.TryClassify(name, out var manifest, out var file));
        Assert.Equal(isManifest, manifest);
        Assert.Equal(image, file);
    }

    [Fact]
    public void An_image_that_does_not_match_its_hash_is_refused()
    {
        var zip = PackZip(swap: ("1001000.png", Image(9)));
        Assert.Equal(PortraitPackFailure.BadImage, Extract(zip, Path.Combine(root, "pack"), out _));
    }

    [Fact]
    public void An_image_that_does_not_decode_is_refused_even_when_its_hash_matches()
    {
        var notAnImage = Encoding.ASCII.GetBytes("MZ\u0090 not a picture at all");
        var zip = PackZip(replace: ("1001000.png", notAnImage));
        Assert.Equal(PortraitPackFailure.BadImage, Extract(zip, Path.Combine(root, "pack"), out _));

        var wrongSize = PackPng.EncodeRgba(new byte[16 * 16 * 4], 16, 16);
        Assert.Equal(PortraitPackFailure.BadImage, Extract(PackZip(replace: ("1001000.png", wrongSize)), Path.Combine(root, "pack2"), out _));
    }

    [Fact]
    public void A_missing_image_an_unlisted_one_a_duplicate_or_no_manifest_is_refused()
    {
        Assert.Equal(PortraitPackFailure.BadImage, Extract(PackZip(drop: "portraits/1002000.png"), Path.Combine(root, "a"), out _));
        Assert.Equal(PortraitPackFailure.UnsafeEntry, Extract(PackZip(extra: ("portraits/1003000.png", Image(3))), Path.Combine(root, "b"), out _));
        Assert.Equal(PortraitPackFailure.UnsafeEntry, Extract(PackZip(extra: ("portraits/1001000.png", Image(1))), Path.Combine(root, "c"), out _));
        Assert.Equal(PortraitPackFailure.BadManifest, Extract(PackZip(drop: PortraitPackManifest.FileName), Path.Combine(root, "d"), out _));
    }

    [Fact]
    public void An_image_bigger_than_the_cap_is_refused_unread()
    {
        var huge = new byte[PortraitPackArchive.MaxImageBytes + 1];
        Assert.Equal(PortraitPackFailure.UnsafeEntry, Extract(PackZip(replace: ("1001000.png", huge)), Path.Combine(root, "pack"), out _));
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_refused()
    {
        var path = Path.Combine(root, "not.zip");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("this is not a zip"));
        Assert.Equal(PortraitPackFailure.BadArchive, PortraitPackArchive.Extract(path, Path.Combine(root, "pack"), CancellationToken.None, out _, out _));
    }

    [Fact]
    public void The_same_images_make_the_same_zip()
    {
        Assert.Equal(PackZip(), PackZip());
    }

    [Fact]
    public void The_zip_holds_nothing_that_depends_on_the_machine_that_built_it()
    {
        // Every entry stored (no compressor whose output varies by runtime), the "version made by" platform byte
        // pinned to 0 (MS-DOS/Windows, whatever OS built it) and no external attributes (Unix permissions).
        var zip = PackZip();
        var headers = CentralDirectory(zip);
        Assert.Equal(3, headers.Count);
        Assert.All(headers, h =>
        {
            Assert.Equal(0, h.Method);
            Assert.Equal(0, h.Platform);
            Assert.Equal(0u, h.ExternalAttributes);
        });

        // And it still extracts as before.
        Assert.Equal(PortraitPackFailure.None, Extract(zip, Path.Combine(root, "pack"), out var manifest));
        Assert.NotNull(manifest);
    }

    [Fact]
    public void The_zip_writer_makes_these_exact_bytes()
    {
        // A pin on the writer's whole output for fixed bytes (not PNGs, whose deflate comes from the runtime's zlib): if
        // this changes (a .NET update to its zip writer, a change to the writer or the manifest's JSON), the next pack
        // build is not byte-identical to the last one from the same photos; docs/data/portrait-pack.md says what else is.
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["1001000.png"] = Encoding.ASCII.GetBytes("first image"),
            ["1002000.png"] = Encoding.ASCII.GetBytes("second image"),
        };
        var files = images.ToDictionary(kv => kv.Key, kv => Convert.ToHexStringLower(SHA256.HashData(kv.Value)), StringComparer.Ordinal);
        var manifest = PortraitPackManifest.Create("2026.09.15.0000.0000", PortraitPackManifest.BuiltDateOf("2026.09.15.0000.0000"), "test", files, new Dictionary<uint, string> { [1001000] = "1001000.png", [1002000] = "1002000.png" });
        using var zip = new MemoryStream();
        PortraitPackArchive.Write(zip, manifest, images);

        var hash = Convert.ToHexStringLower(SHA256.HashData(zip.ToArray()));
        Assert.True(hash == "f36b59077292501f9a7b03385e3c22667a26f7796bef901b7e600676d0408ecb", "the pack zip writer now writes " + hash);
    }

    [Fact]
    public void Disk_full_is_told_apart_from_other_disk_errors()
    {
        Assert.True(PortraitPackArchive.IsDiskFull(new IOException("full", unchecked((int)0x80070070))));
        Assert.True(PortraitPackArchive.IsDiskFull(new IOException("full", unchecked((int)0x80070027))));
        Assert.False(PortraitPackArchive.IsDiskFull(new IOException("locked", unchecked((int)0x80070020))));
    }

    // ------------------------------------------------------------------ helpers

    private PortraitPackFailure Extract(byte[] zip, string target, out PortraitPackManifest? manifest) =>
        PortraitPackArchive.Extract(SaveZip(zip, "extract-" + Guid.NewGuid().ToString("N")[..6] + ".zip"), target, CancellationToken.None, out manifest, out _);

    private string SaveZip(byte[] zip, string name = "pack.zip")
    {
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, zip);
        return path;
    }

    /// <summary>A plain image of the pack's side, its pixels varied by <paramref name="seed"/>.</summary>
    private static byte[] Image(int seed)
    {
        var side = PortraitPackManifest.ImageSide;
        var rgba = new byte[side * side * 4];
        for (var i = 0; i < side * side; i++)
        {
            rgba[i * 4] = (byte)(i + seed);
            rgba[(i * 4) + 1] = (byte)(seed * 40);
            rgba[(i * 4) + 2] = (byte)(i >> 3);
            rgba[(i * 4) + 3] = 255;
        }

        return PackPng.EncodeRgba(rgba, side, side);
    }

    /// <summary>
    /// A pack zip of two images (1001000.png for ids 1001000 and its alias 1012527, 1002000.png for 1002000), built by the
    /// same writer as the real one, then tampered with: <paramref name="swap"/> puts other bytes in an image (the manifest
    /// keeps the old hash), <paramref name="replace"/> does too and fixes the hash, <paramref name="extra"/> adds an entry,
    /// <paramref name="drop"/> leaves one out.
    /// </summary>
    private static byte[] PackZip(int seed = 1, (string Name, byte[] Bytes)? swap = null, (string Name, byte[] Bytes)? replace = null, (string Name, byte[] Bytes)? extra = null, string? drop = null)
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["1001000.png"] = Image(seed),
            ["1002000.png"] = Image(seed + 1),
        };
        if (replace is { } r)
        {
            images[r.Name] = r.Bytes;
        }

        var files = images.ToDictionary(kv => kv.Key, kv => Convert.ToHexStringLower(SHA256.HashData(kv.Value)), StringComparer.Ordinal);
        var manifest = PortraitPackManifest.Create("2026.09.15.0000.0000", new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), "test", files, new Dictionary<uint, string>
        {
            [1001000] = "1001000.png",
            [1012527] = "1001000.png",
            [1002000] = "1002000.png",
        });

        if (swap is { } s)
        {
            images[s.Name] = s.Bytes;
        }

        using var built = new MemoryStream();
        PortraitPackArchive.Write(built, manifest, images);
        if (extra is null && drop is null)
        {
            return built.ToArray();
        }

        // Rewritten entry by entry, so a test can add or drop one the writer would never write.
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(built.ToArray()), ZipArchiveMode.Read))
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                if (entry.FullName == drop)
                {
                    continue;
                }

                var copy = zip.CreateEntry(entry.FullName);
                using var from = entry.Open();
                using var to = copy.Open();
                from.CopyTo(to);
            }

            if (extra is { } e)
            {
                var added = zip.CreateEntry(e.Name);
                using var to = added.Open();
                to.Write(e.Bytes);
            }
        }

        return output.ToArray();
    }

    /// <summary>The zip's central directory, header by header: compression method, "version made by" platform, external attributes.</summary>
    private static List<(int Method, int Platform, uint ExternalAttributes)> CentralDirectory(byte[] zip)
    {
        var end = zip.Length - 22;
        Assert.Equal(0x06054b50u, BitConverter.ToUInt32(zip, end));
        int count = BitConverter.ToUInt16(zip, end + 10);
        var at = (int)BitConverter.ToUInt32(zip, end + 16);
        var headers = new List<(int, int, uint)>();
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(0x02014b50u, BitConverter.ToUInt32(zip, at));
            headers.Add((BitConverter.ToUInt16(zip, at + 10), zip[at + 5], BitConverter.ToUInt32(zip, at + 38)));
            at += 46 + BitConverter.ToUInt16(zip, at + 28) + BitConverter.ToUInt16(zip, at + 30) + BitConverter.ToUInt16(zip, at + 32);
        }

        return headers;
    }
}
