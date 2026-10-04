using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// Getting and installing the optional portrait pack (feature plan v7 F4, decision 8), with the transport injected so
/// nothing touches the network: the download is refused unless it is exactly the offered file (size, SHA-256, the pinned
/// address and GitHub's own redirect), every failure leaves nothing behind, the zip may hold only the pack's own images
/// (no zip-slip, no programs), each image must match its hash and decode, and an install is all or nothing.
/// </summary>
public sealed class PortraitPackInstallTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tsukimichi-pack-" + Guid.NewGuid().ToString("N")[..8]);

    public PortraitPackInstallTests() => Directory.CreateDirectory(root);

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

    // ------------------------------------------------------------------ the download

    [Fact]
    public async Task The_offered_file_downloads_from_the_pinned_address_with_progress()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        var transport = new FakeTransport(_ => PortraitPackResponse.Ok(new MemoryStream(zip), zip.Length));
        var target = Path.Combine(root, "download.part");
        var seen = new List<PortraitPackProgress>();

        var result = await PortraitPackDownload.RunAsync(transport, offer, target, new Collect(seen), CancellationToken.None);

        Assert.Equal(PortraitPackFailure.None, result);
        Assert.Equal(zip, File.ReadAllBytes(target));
        Assert.Equal([offer.DownloadUri], transport.Asked);
        Assert.Equal(zip.Length, seen[^1].Received);
        Assert.Equal(1f, seen[^1].Fraction);
    }

    [Fact]
    public async Task GitHubs_redirect_to_its_asset_host_is_followed()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        var asset = new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/1?sig=x");
        var transport = new FakeTransport(uri => uri == offer.DownloadUri
            ? PortraitPackResponse.Redirect(302, asset)
            : PortraitPackResponse.Ok(new MemoryStream(zip), zip.Length));

        Assert.Equal(PortraitPackFailure.None, await Run(transport, offer));
        Assert.Equal([offer.DownloadUri, asset], transport.Asked);
    }

    [Fact]
    public async Task A_redirect_anywhere_else_is_never_followed()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        var transport = new FakeTransport(_ => PortraitPackResponse.Redirect(302, new Uri("https://evil.example/pack.zip")));

        Assert.Equal(PortraitPackFailure.Redirected, await Run(transport, offer));
        Assert.Equal([offer.DownloadUri], transport.Asked);
        Assert.False(File.Exists(Path.Combine(root, "download.part")));
    }

    [Fact]
    public async Task A_redirect_loop_stops()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        var asset = new Uri("https://objects.githubusercontent.com/loop");
        var transport = new FakeTransport(_ => PortraitPackResponse.Redirect(302, asset));

        Assert.Equal(PortraitPackFailure.Redirected, await Run(transport, offer));
        Assert.Equal(PortraitPackDownload.MaxRedirects + 1, transport.Asked.Count);
    }

    [Fact]
    public async Task A_download_that_does_not_match_its_hash_is_discarded()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        var tampered = (byte[])zip.Clone();
        tampered[^30] ^= 0x01;
        var transport = new FakeTransport(_ => PortraitPackResponse.Ok(new MemoryStream(tampered), tampered.Length));

        Assert.Equal(PortraitPackFailure.HashMismatch, await Run(transport, offer));
        Assert.False(File.Exists(Path.Combine(root, "download.part")));
    }

    [Fact]
    public async Task A_download_of_the_wrong_size_is_refused_and_a_longer_one_is_cut_off()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);

        // Said up front: refused before a byte is read.
        Assert.Equal(PortraitPackFailure.TooLarge, await Run(new FakeTransport(_ => PortraitPackResponse.Ok(new MemoryStream(zip), zip.Length + 1)), offer));
        Assert.Equal(PortraitPackFailure.SizeMismatch, await Run(new FakeTransport(_ => PortraitPackResponse.Ok(new MemoryStream(zip), zip.Length - 1)), offer));

        // Not said: a body longer than the offer stops at the cap; a shorter one is refused at the end.
        var longer = zip.Concat(new byte[100_000]).ToArray();
        var stream = new CountingStream(longer);
        Assert.Equal(PortraitPackFailure.TooLarge, await Run(new FakeTransport(_ => PortraitPackResponse.Ok(stream, null)), offer));
        Assert.True(stream.Served <= zip.Length + 81_920);
        Assert.Equal(PortraitPackFailure.SizeMismatch, await Run(new FakeTransport(_ => PortraitPackResponse.Ok(new MemoryStream(zip[..^1]), null)), offer));
        Assert.False(File.Exists(Path.Combine(root, "download.part")));
    }

    [Theory]
    [InlineData(404, PortraitPackFailure.NotFound)]
    [InlineData(500, PortraitPackFailure.HttpError)]
    [InlineData(403, PortraitPackFailure.HttpError)]
    [InlineData(0, PortraitPackFailure.Offline)]
    public async Task Http_errors_and_no_connection_say_which(int status, PortraitPackFailure expected)
    {
        var offer = OfferFor(PackZip());
        var transport = new FakeTransport(_ => status == 0 ? PortraitPackResponse.NoResponse("no route to host") : PortraitPackResponse.Failed(status));
        Assert.Equal(expected, await Run(transport, offer));
    }

    [Fact]
    public async Task A_connection_dropped_mid_download_reads_as_offline()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        var transport = new FakeTransport(_ => PortraitPackResponse.Ok(new FailingStream(zip, failAfter: 10), zip.Length));
        Assert.Equal(PortraitPackFailure.Offline, await Run(transport, offer));
        Assert.False(File.Exists(Path.Combine(root, "download.part")));
    }

    [Fact]
    public async Task Cancelling_stops_the_download_and_leaves_nothing()
    {
        var zip = PackZip();
        var offer = OfferFor(zip);
        using var cancel = new CancellationTokenSource();
        var transport = new FakeTransport(_ =>
        {
            cancel.Cancel();
            return PortraitPackResponse.Ok(new MemoryStream(zip), zip.Length);
        });

        var result = await PortraitPackDownload.RunAsync(transport, offer, Path.Combine(root, "download.part"), null, cancel.Token);
        Assert.Equal(PortraitPackFailure.Cancelled, result);
        Assert.False(File.Exists(Path.Combine(root, "download.part")));
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

    // ------------------------------------------------------------------ the installed pack

    [Fact]
    public void An_installed_pack_loads_with_each_givers_image()
    {
        var store = new PortraitPackStore(Path.Combine(root, "portraits"));
        var zip = PackZip();
        var offer = OfferFor(zip);
        Assert.Equal(PortraitPackFailure.None, store.Install(SaveZip(zip), offer, CancellationToken.None, out var pack, out _));
        Assert.NotNull(pack);

        var loaded = store.Load(out var damaged);
        Assert.False(damaged);
        Assert.NotNull(loaded);
        Assert.Equal(offer.Sha256, loaded.Sha256);
        Assert.Equal("portraits-1", loaded.Tag);
        Assert.Equal(1, loaded.PackNumber);
        Assert.Equal(2, loaded.Faces);
        Assert.True(loaded.BytesOnDisk > 0);
        Assert.True(DateTime.UtcNow - loaded.InstalledUtc < TimeSpan.FromMinutes(5));
        Assert.Equal(3, loaded.Givers);
        Assert.True(loaded.TryGetPath(1012527, out var alias));
        Assert.EndsWith("1001000.png", alias, StringComparison.Ordinal);
        Assert.True(File.Exists(alias));
        Assert.False(loaded.Has(1009999));
        Assert.Equal(offer.Sha256[..12], Path.GetFileName(loaded.Folder));

        // Nothing else is left: no staging folder, no download.
        Assert.Equal([loaded.Folder], Directory.GetDirectories(store.Root));
    }

    [Fact]
    public void The_install_checks_the_file_it_extracts_against_the_offer_again()
    {
        // A download.part rewritten after the download's own check (another program, another game client): refused.
        var store = new PortraitPackStore(Path.Combine(root, "portraits"));
        var good = PackZip();
        var other = (byte[])good.Clone();
        other[good.Length / 2] ^= 0x01;
        Assert.Equal(PortraitPackFailure.HashMismatch, store.Install(SaveZip(other), OfferFor(good), CancellationToken.None, out var pack, out _));
        Assert.Null(pack);
        Assert.Equal(PortraitPackFailure.SizeMismatch, store.Install(SaveZip(good[..^1], "short.zip"), OfferFor(good), CancellationToken.None, out _, out _));
        Assert.Null(store.Load(out _));
    }

    [Fact]
    public void A_failed_install_keeps_the_pack_in_use()
    {
        var store = new PortraitPackStore(Path.Combine(root, "portraits"));
        var good = PackZip();
        Assert.Equal(PortraitPackFailure.None, store.Install(SaveZip(good), OfferFor(good), CancellationToken.None, out _, out _));

        var bad = PackZip(extra: ("../evil.png", Image(4)));
        Assert.Equal(PortraitPackFailure.UnsafeEntry, store.Install(SaveZip(bad, "bad.zip"), OfferFor(bad), CancellationToken.None, out var none, out _));
        Assert.Null(none);

        var loaded = store.Load(out var damaged);
        Assert.False(damaged);
        Assert.Equal(OfferFor(good).Sha256, loaded?.Sha256);
        Assert.Single(Directory.GetDirectories(store.Root));
    }

    [Fact]
    public void A_newer_pack_replaces_the_old_one_and_the_old_folder_goes_at_clean_up()
    {
        var store = new PortraitPackStore(Path.Combine(root, "portraits"));
        var first = PackZip();
        var second = PackZip(seed: 2);
        Assert.Equal(PortraitPackFailure.None, store.Install(SaveZip(first), OfferFor(first), CancellationToken.None, out _, out _));
        Assert.Equal(PortraitPackFailure.None, store.Install(SaveZip(second, "second.zip"), OfferFor(second), CancellationToken.None, out _, out _));

        Assert.Equal(OfferFor(second).Sha256, store.Load(out _)?.Sha256);
        Assert.Equal(2, Directory.GetDirectories(store.Root).Length);
        store.CleanUp(ignoreAge: true);
        Assert.Single(Directory.GetDirectories(store.Root));
    }

    [Fact]
    public void A_pack_left_half_extracted_is_never_used_and_is_cleaned_up()
    {
        var store = new PortraitPackStore(Path.Combine(root, "portraits"));

        // A crash mid-extract: a staging folder and no current.json.
        Directory.CreateDirectory(Path.Combine(store.Root, "0123456789ab.staging-deadbeef"));
        File.WriteAllBytes(Path.Combine(store.Root, "0123456789ab.staging-deadbeef", "1001000.png"), Image(1));
        var part = store.NewDownloadFile();
        File.WriteAllBytes(part, [1, 2, 3]);
        Assert.Null(store.Load(out var damaged));
        Assert.False(damaged);

        store.CleanUp(ignoreAge: true);
        Assert.Empty(Directory.GetDirectories(store.Root));
        Assert.False(File.Exists(part));
    }

    [Fact]
    public void A_pack_whose_files_went_missing_reads_as_damaged_and_can_be_repaired_or_removed()
    {
        var store = new PortraitPackStore(Path.Combine(root, "portraits"));
        var zip = PackZip();
        Assert.Equal(PortraitPackFailure.None, store.Install(SaveZip(zip), OfferFor(zip), CancellationToken.None, out var pack, out _));
        Assert.NotNull(pack);
        File.Delete(Path.Combine(pack.Folder, PortraitPackManifest.FileName));

        Assert.Null(store.Load(out var damaged));
        Assert.True(damaged);

        // Download again: the same pack over its broken copy.
        Assert.Equal(PortraitPackFailure.None, store.Install(SaveZip(zip), OfferFor(zip), CancellationToken.None, out _, out _));
        Assert.NotNull(store.Load(out damaged));
        Assert.False(damaged);

        // Remove: no pack, nothing damaged, no folder.
        Assert.True(store.Remove());
        Assert.Null(store.Load(out damaged));
        Assert.False(damaged);
        Assert.Empty(Directory.GetDirectories(store.Root));
    }

    [Fact]
    public void Disk_full_is_told_apart_from_other_disk_errors()
    {
        Assert.True(PortraitPackStore.IsDiskFull(new IOException("full", unchecked((int)0x80070070))));
        Assert.True(PortraitPackStore.IsDiskFull(new IOException("full", unchecked((int)0x80070027))));
        Assert.False(PortraitPackStore.IsDiskFull(new IOException("locked", unchecked((int)0x80070020))));
    }

    // ------------------------------------------------------------------ helpers

    private Task<PortraitPackFailure> Run(IPortraitPackTransport transport, PortraitPackOffer offer) =>
        PortraitPackDownload.RunAsync(transport, offer, Path.Combine(root, "download.part"), null, CancellationToken.None);

    private PortraitPackFailure Extract(byte[] zip, string target, out PortraitPackManifest? manifest) =>
        PortraitPackArchive.Extract(SaveZip(zip, "extract-" + Guid.NewGuid().ToString("N")[..6] + ".zip"), target, CancellationToken.None, out manifest, out _);

    private string SaveZip(byte[] zip, string name = "pack.zip")
    {
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, zip);
        return path;
    }

    private static PortraitPackOffer OfferFor(byte[] zip) =>
        PortraitPackTests.Offer(Convert.ToHexStringLower(SHA256.HashData(zip)), zip.Length);

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

    private sealed class FakeTransport(Func<Uri, PortraitPackResponse> answer) : IPortraitPackTransport
    {
        public List<Uri> Asked { get; } = [];

        public Task<PortraitPackResponse> GetAsync(Uri uri, CancellationToken cancellation)
        {
            Asked.Add(uri);
            return Task.FromResult(answer(uri));
        }
    }

    private sealed class Collect(List<PortraitPackProgress> into) : IProgress<PortraitPackProgress>
    {
        public void Report(PortraitPackProgress value) => into.Add(value);
    }

    /// <summary>A body that says how much of it was read.</summary>
    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public long Served { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = Read(buffer.Span);
            Served += n;
            return ValueTask.FromResult(n);
        }
    }

    /// <summary>A body whose connection drops after <c>failAfter</c> bytes.</summary>
    private sealed class FailingStream(byte[] bytes, int failAfter) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= failAfter)
            {
                throw new IOException("The connection was reset.");
            }

            var n = Read(buffer.Span[..Math.Min(buffer.Length, failAfter)]);
            return ValueTask.FromResult(n);
        }
    }
}
