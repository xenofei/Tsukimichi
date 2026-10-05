using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Tsukimichi.Core.Portraits;

/// <summary>Why the portrait pack's archive was refused (feature plan v7 F4); <see cref="None"/> when it was whole.</summary>
public enum PortraitPackFailure : byte
{
    None = 0,

    /// <summary>The zip cannot be read.</summary>
    BadArchive,

    /// <summary>The zip holds a file the pack never has: a folder path, a program, anything but its images and manifest.</summary>
    UnsafeEntry,

    /// <summary>The manifest is missing or not one this build reads.</summary>
    BadManifest,

    /// <summary>An image does not match its hash in the manifest, or does not decode.</summary>
    BadImage,

    /// <summary>The disk is full.</summary>
    DiskFull,

    /// <summary>Another file-system error.</summary>
    DiskError,
}

/// <summary>
/// The portrait pack's archive (feature plan v7 F4). <c>Tsukimichi.DataGen --portrait-pack</c> writes it, then extracts
/// it with these same checks into the plugin's <c>assets/portraits</c> folder, which ships inside the plugin
/// (<see cref="BundledPortraits"/>). It trusts nothing in the zip. The zip may hold
/// exactly <c>manifest.json</c> and <c>portraits/&lt;name&gt;.png</c> images (an optional <c>portraits/</c> folder entry),
/// each name flat and safe (<see cref="PortraitPackManifest.IsSafeFileName"/>): any other entry (a path that climbs out,
/// an absolute path, a nested folder, a program, a duplicate) refuses the whole pack before a byte is written. Every
/// image must be listed in the manifest with its SHA-256, match it, decode as a PNG of the manifest's side holding only
/// the chunks the pack writes (<see cref="PackPng.TryDecode"/>, strict) and stay under <see cref="MaxImageBytes"/>; every listed image must be present.
/// Files are written only inside the target folder, each path checked again after it is made.
/// </summary>
public static class PortraitPackArchive
{
    /// <summary>The largest image a pack may hold, bytes (a 128 px PNG is about 10–40 KB).</summary>
    public const int MaxImageBytes = 1024 * 1024;

    /// <summary>The most bytes a pack may unpack to.</summary>
    public const long MaxTotalBytes = 256L * 1024 * 1024;

    private const string ImagePrefix = PortraitPackManifest.ImageFolder + "/";

    /// <summary>Whether <paramref name="ex"/> says the disk is full (ERROR_DISK_FULL or ERROR_HANDLE_DISK_FULL).</summary>
    public static bool IsDiskFull(IOException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var code = ex.HResult & 0xFFFF;
        return code is 0x70 or 0x27;
    }

    /// <summary>
    /// Checks the zip at <paramref name="zipPath"/> and extracts it into <paramref name="target"/> (created; it must not
    /// exist). Returns <see cref="PortraitPackFailure.None"/> with the manifest, or the reason with
    /// <paramref name="detail"/>; on a failure whatever was written stays in <paramref name="target"/> for the caller to
    /// delete (it extracts into a staging folder).
    /// </summary>
    public static PortraitPackFailure Extract(string zipPath, string target, CancellationToken cancellation, out PortraitPackManifest? manifest, out string? detail)
    {
        manifest = null;
        detail = null;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            return Extract(zip, target, cancellation, out manifest, out detail);
        }
        catch (InvalidDataException ex)
        {
            detail = ex.Message;
            return PortraitPackFailure.BadArchive;
        }
        catch (IOException ex)
        {
            detail = ex.Message;
            return IsDiskFull(ex) ? PortraitPackFailure.DiskFull : PortraitPackFailure.DiskError;
        }
        catch (UnauthorizedAccessException ex)
        {
            detail = ex.Message;
            return PortraitPackFailure.DiskError;
        }
    }

    /// <summary>The checks and the extraction on an open zip (see the other overload).</summary>
    public static PortraitPackFailure Extract(ZipArchive zip, string target, CancellationToken cancellation, out PortraitPackManifest? manifest, out string? detail)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentException.ThrowIfNullOrEmpty(target);
        manifest = null;
        detail = null;

        // 1. Every entry is one the pack has, or nothing is written.
        ZipArchiveEntry? manifestEntry = null;
        var images = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        if (zip.Entries.Count > PortraitPackManifest.MaxFiles + 2)
        {
            detail = "too many files";
            return PortraitPackFailure.UnsafeEntry;
        }

        long total = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (!TryClassify(name, out var isManifest, out var image))
            {
                detail = Clip(name);
                return PortraitPackFailure.UnsafeEntry;
            }

            if (isManifest)
            {
                if (manifestEntry is not null || entry.Length > PortraitPackManifest.MaxManifestBytes)
                {
                    detail = Clip(name);
                    return PortraitPackFailure.UnsafeEntry;
                }

                manifestEntry = entry;
            }
            else if (image is not null)
            {
                if (!images.TryAdd(image, entry) || entry.Length > MaxImageBytes)
                {
                    detail = Clip(name);
                    return PortraitPackFailure.UnsafeEntry;
                }
            }

            total += Math.Max(0, entry.Length);
            if (total > MaxTotalBytes)
            {
                detail = "the pack unpacks too large";
                return PortraitPackFailure.UnsafeEntry;
            }
        }

        // 2. The manifest, and the images it lists: no more, no fewer.
        if (manifestEntry is null)
        {
            detail = "no manifest";
            return PortraitPackFailure.BadManifest;
        }

        var manifestBytes = ReadBounded(manifestEntry, PortraitPackManifest.MaxManifestBytes);
        if (manifestBytes is null)
        {
            detail = "the manifest is too large";
            return PortraitPackFailure.UnsafeEntry;
        }

        var read = PortraitPackManifest.TryParse(manifestBytes, out var problem);
        if (read is null)
        {
            detail = problem;
            return PortraitPackFailure.BadManifest;
        }

        foreach (var name in images.Keys)
        {
            if (!read.Files.ContainsKey(name))
            {
                detail = "an image the manifest does not list: " + Clip(name);
                return PortraitPackFailure.UnsafeEntry;
            }
        }

        foreach (var name in read.Files.Keys)
        {
            if (!images.ContainsKey(name))
            {
                detail = "a listed image is missing: " + Clip(name);
                return PortraitPackFailure.BadImage;
            }
        }

        // 3. Each image: its hash, then that it decodes, then written inside the target and nowhere else.
        Directory.CreateDirectory(target);
        var root = Path.GetFullPath(target);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        long unpacked = manifestBytes.Length;
        foreach (var (name, entry) in images.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            var bytes = ReadBounded(entry, MaxImageBytes);

            // What was read, not what the headers say, counts toward the cap.
            unpacked += bytes?.Length ?? 0;
            if (bytes is null || unpacked > MaxTotalBytes)
            {
                detail = Clip(name);
                return PortraitPackFailure.UnsafeEntry;
            }

            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), read.Files[name], StringComparison.Ordinal))
            {
                detail = "hash: " + Clip(name);
                return PortraitPackFailure.BadImage;
            }

            if (!PackPng.TryDecode(bytes, out var width, out var height, out _, maxSide: 512, strict: true) || width != read.Side || height != read.Side)
            {
                detail = "does not decode: " + Clip(name);
                return PortraitPackFailure.BadImage;
            }

            var path = Path.GetFullPath(Path.Combine(root, name));
            if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) || Path.GetDirectoryName(path) != root)
            {
                detail = Clip(name);
                return PortraitPackFailure.UnsafeEntry;
            }

            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(bytes);
        }

        // The manifest last: a folder with a manifest is a whole pack.
        File.WriteAllBytes(Path.Combine(root, PortraitPackManifest.FileName), manifestBytes);
        manifest = read;
        return PortraitPackFailure.None;
    }

    /// <summary>
    /// Writes a pack as <c>Tsukimichi.DataGen --portrait-pack</c> does: the manifest, then each image under
    /// <c>portraits/</c>, in name order, so the same inputs (the same images and manifest, whose build date is an input
    /// too: <see cref="PortraitPackManifest.BuiltDateOf"/>) always make the same bytes and the same SHA-256, on any
    /// machine. Nothing in the zip depends on the machine that wrote it:
    /// <list type="bullet">
    /// <item>every entry is stored, the manifest too (a PNG does not compress further, and a deflater's output can
    /// change with the runtime's zlib);</item>
    /// <item>every entry has the same fixed timestamp and no external attributes (.NET gives an entry made on Unix its
    /// file permissions there);</item>
    /// <item>the "version made by" platform byte, which .NET sets from the OS it runs on (0 on Windows, 3 on Unix) and
    /// offers no way to set, is rewritten to 0 in every central directory header after the zip is written;</item>
    /// <item>the zip is built in memory, a seekable stream, so .NET never writes data descriptors after the entries.</item>
    /// </list>
    /// What remains is .NET's zip writer itself: a .NET version that wrote its headers differently would make other
    /// bytes from the same inputs (a test pins the writer's output). <paramref name="images"/> must be exactly the manifest's.
    /// </summary>
    public static void Write(Stream output, PortraitPackManifest manifest, IReadOnlyDictionary<string, byte[]> images)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count != manifest.Files.Count || images.Keys.Any(name => !manifest.Files.ContainsKey(name)))
        {
            throw new ArgumentException("The images are not the manifest's.", nameof(images));
        }

        var stamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, byte[] bytes)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                entry.LastWriteTime = stamp;
                entry.ExternalAttributes = 0;
                using var stream = entry.Open();
                stream.Write(bytes);
            }

            Add(PortraitPackManifest.FileName, manifest.ToJson());
            foreach (var (name, bytes) in images.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                Add(ImagePrefix + name, bytes);
            }
        }

        var written = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        PinVersionMadeBy(written);
        output.Write(written);
    }

    /// <summary>
    /// Sets the platform byte of "version made by" to 0 (MS-DOS and Windows) in every central directory header of the
    /// zip in <paramref name="zip"/>, as written by <see cref="Write"/>: no archive comment, no Zip64.
    /// </summary>
    private static void PinVersionMadeBy(Span<byte> zip)
    {
        const uint EndSignature = 0x06054b50;
        const uint HeaderSignature = 0x02014b50;
        var end = zip.Length - 22;
        if (end < 0 || BinaryPrimitives.ReadUInt32LittleEndian(zip[end..]) != EndSignature)
        {
            throw new InvalidOperationException("The zip does not end with its directory record.");
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(zip[(end + 10)..]);
        var at = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(zip[(end + 16)..]));
        for (var i = 0; i < count; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(zip[at..]) != HeaderSignature)
            {
                throw new InvalidOperationException("The zip's central directory is not where its end record says.");
            }

            zip[at + 5] = 0;
            at += 46 + BinaryPrimitives.ReadUInt16LittleEndian(zip[(at + 28)..]) + BinaryPrimitives.ReadUInt16LittleEndian(zip[(at + 30)..]) + BinaryPrimitives.ReadUInt16LittleEndian(zip[(at + 32)..]);
        }
    }

    /// <summary>
    /// Whether a zip entry's name is one a pack may hold: <c>manifest.json</c>, the <c>portraits/</c> folder, or
    /// <c>portraits/&lt;safe name&gt;.png</c>. Every other name (backslashes, <c>..</c>, a leading slash or drive, a deeper
    /// folder, another extension) is refused.
    /// </summary>
    public static bool TryClassify(string name, out bool isManifest, out string? image)
    {
        isManifest = false;
        image = null;
        if (string.IsNullOrEmpty(name) || name.Contains('\\', StringComparison.Ordinal) || name.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        if (name == PortraitPackManifest.FileName)
        {
            isManifest = true;
            return true;
        }

        if (name == ImagePrefix)
        {
            return true;
        }

        if (name.StartsWith(ImagePrefix, StringComparison.Ordinal) && PortraitPackManifest.IsSafeFileName(name[ImagePrefix.Length..]))
        {
            image = name[ImagePrefix.Length..];
            return true;
        }

        return false;
    }

    /// <summary>The entry's bytes, read at most <paramref name="limit"/>; null when it holds more (whatever its header says).</summary>
    private static byte[]? ReadBounded(ZipArchiveEntry entry, int limit)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[16_384];
        int n;
        while ((n = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + n > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, n);
        }

        return buffer.ToArray();
    }

    private static string Clip(string text) => text.Length <= 60 ? text : text[..60] + "…";
}
