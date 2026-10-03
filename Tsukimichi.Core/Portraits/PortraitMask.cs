using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// A custom delivery portrait's keep mask (1.15 design spec A2.4): which pixels of the hr texture belong to the client
/// and which to the emblem's lettered ring, keyed out offline by <c>Tsukimichi.DataGen --portrait-masks</c> and shipped
/// in <c>curated/portrait_masks/</c> (the art itself never ships). The drawing code multiplies the texture's alpha by the
/// mask while it makes the graded copy, and draws the art only when <see cref="Matches"/> says the texture is the one
/// the mask was keyed from; otherwise (a patch changed the art) it draws the fallback until DataGen is rerun.
/// </summary>
/// <param name="Icon">The delivery icon id (061661 …).</param>
/// <param name="File">The mask file's full path (a 1-bit PNG at the hr size, read by <see cref="PortraitMaskFile.TryRead"/>).</param>
/// <param name="TexturePath">The game path of the hr texture the mask was keyed from.</param>
/// <param name="Sha256">The SHA-256 of that texture file's bytes, lower-case hex.</param>
public sealed record PortraitMask(uint Icon, string File, string TexturePath, string Sha256, int Width, int Height)
{
    /// <summary>The hash <see cref="Sha256"/> holds: SHA-256 of the texture file's raw bytes (the <c>.tex</c> as read from the game), lower-case hex.</summary>
    public static string HashOf(ReadOnlySpan<byte> textureFile) => Convert.ToHexStringLower(SHA256.HashData(textureFile));

    /// <summary>Whether <paramref name="textureFile"/> (the raw <see cref="TexturePath"/> bytes) is the art this mask was keyed from.</summary>
    public bool Matches(ReadOnlySpan<byte> textureFile) => string.Equals(HashOf(textureFile), Sha256, StringComparison.Ordinal);
}

/// <summary>
/// The keep masks' file format: a 1-bit greyscale PNG (white = keep), so it is small (about 0.5–1 KB) and any image
/// viewer shows it. <see cref="Write"/> and <see cref="TryRead"/> handle exactly what the masks need: one channel, bit
/// depth 1, no interlace; the reader takes every PNG row filter.
/// </summary>
public static class PortraitMaskFile
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Writes <paramref name="keep"/> (row-major, <paramref name="width"/> × <paramref name="height"/>) to <paramref name="path"/>.</summary>
    public static void Write(string path, ReadOnlySpan<bool> keep, int width, int height)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        System.IO.File.WriteAllBytes(path, Encode(keep, width, height));
    }

    /// <summary>The PNG bytes of a mask.</summary>
    public static byte[] Encode(ReadOnlySpan<bool> keep, int width, int height)
    {
        if (width <= 0 || height <= 0 || keep.Length != width * height)
        {
            throw new ArgumentException("the mask must hold width × height pixels");
        }

        var stride = (width + 7) / 8;
        var raw = new byte[(stride + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * (stride + 1);
            for (var x = 0; x < width; x++)
            {
                if (keep[(y * width) + x])
                {
                    raw[row + 1 + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            z.Write(raw);
        }

        using var file = new MemoryStream();
        file.Write(Signature);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 1; // bit depth
        header[9] = 0; // greyscale
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", packed.ToArray());
        Chunk(file, "IEND", []);
        return file.ToArray();
    }

    /// <summary>Reads a mask written by <see cref="Write"/>; false when the file is missing or is not a 1-bit greyscale PNG.</summary>
    public static bool TryRead(string path, out int width, out int height, out bool[] keep)
    {
        width = height = 0;
        keep = [];
        byte[] bytes;
        try
        {
            bytes = System.IO.File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }

        return TryDecode(bytes, out width, out height, out keep);
    }

    /// <summary>Decodes the PNG bytes of a mask; false when they are not a 1-bit greyscale PNG.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> bytes, out int width, out int height, out bool[] keep)
    {
        width = height = 0;
        keep = [];
        if (bytes.Length < Signature.Length + 25 || !bytes[..Signature.Length].SequenceEqual(Signature))
        {
            return false;
        }

        using var data = new MemoryStream();
        var at = Signature.Length;
        var sawHeader = false;
        while (at + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes[at..]);
            if (length < 0 || at + 12 + length > bytes.Length)
            {
                return false;
            }

            var type = Encoding.ASCII.GetString(bytes.Slice(at + 4, 4));
            var body = bytes.Slice(at + 8, length);
            if (type == "IHDR")
            {
                if (length != 13 || body[8] != 1 || body[9] != 0 || body[12] != 0)
                {
                    return false;
                }

                width = BinaryPrimitives.ReadInt32BigEndian(body);
                height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                sawHeader = width > 0 && height > 0 && width <= 8192 && height <= 8192;
            }
            else if (type == "IDAT")
            {
                data.Write(body);
            }
            else if (type == "IEND")
            {
                break;
            }

            at += 12 + length;
        }

        if (!sawHeader)
        {
            return false;
        }

        var stride = (width + 7) / 8;
        var raw = new byte[(stride + 1) * height];
        try
        {
            data.Position = 0;
            using var z = new ZLibStream(data, CompressionMode.Decompress);
            var read = 0;
            int n;
            while (read < raw.Length && (n = z.Read(raw, read, raw.Length - read)) > 0)
            {
                read += n;
            }

            if (read != raw.Length)
            {
                return false;
            }
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var previous = new byte[stride];
        var current = new byte[stride];
        keep = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * (stride + 1);
            var filter = raw[row];
            for (var i = 0; i < stride; i++)
            {
                // Bit depth 1: the filters' "left" neighbour is the previous byte (bpp rounds up to one byte).
                var x = raw[row + 1 + i];
                int left = i > 0 ? current[i - 1] : 0, up = previous[i], upLeft = i > 0 ? previous[i - 1] : 0;
                current[i] = filter switch
                {
                    0 => x,
                    1 => (byte)(x + left),
                    2 => (byte)(x + up),
                    3 => (byte)(x + ((left + up) >> 1)),
                    4 => (byte)(x + Paeth(left, up, upLeft)),
                    _ => x,
                };
            }

            if (filter > 4)
            {
                return false;
            }

            for (var px = 0; px < width; px++)
            {
                keep[(y * width) + px] = (current[px >> 3] & (0x80 >> (px & 7))) != 0;
            }

            (previous, current) = (current, previous);
        }

        return true;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Chunk(Stream stream, string type, byte[] body)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, body.Length);
        stream.Write(word);
        var typed = new byte[4 + body.Length];
        Encoding.ASCII.GetBytes(type, typed);
        body.CopyTo(typed, 4);
        stream.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(typed));
        stream.Write(word);
    }

    /// <summary>The PNG chunk CRC (IEEE 802.3, as zlib computes it).</summary>
    public static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
