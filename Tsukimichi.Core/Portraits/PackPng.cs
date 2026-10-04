using System.Buffers.Binary;
using System.IO.Compression;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The portrait pack's image format (feature plan v7 F4): plain PNG, read and written here so the pack's builder
/// (<c>Tsukimichi.DataGen --portrait-pack</c>) and the plugin agree on exactly what a pack image may be. The plugin
/// decodes every image of a downloaded pack before it is installed (<see cref="TryDecode"/>), so a damaged or hostile
/// file is refused before the game's texture loader ever sees it.
/// <para>
/// <see cref="TryDecode"/> is strict: the signature, every chunk's CRC, a header first, bit depth 8 or 16 (8 for a
/// palette), no interlace, the image data inflating to exactly the rows the header promises, valid row filters and
/// palette indices, and a closing IEND. Anything else is refused. The output is straight-alpha RGBA, 8 bits a channel.
/// With <c>strict</c> (how the plugin checks a pack image) it also refuses any chunk but IHDR, PLTE, tRNS, IDAT and IEND
/// and any byte after IEND, so the file the game's loader later reads holds nothing this decoder did not check.
/// </para>
/// </summary>
public static class PackPng
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>The largest side the decoder takes unless asked for less (pack images are 128 px; Garland photos under 1,000).</summary>
    public const int DefaultMaxSide = 2048;

    /// <summary>The most compressed image data the decoder takes, bytes.</summary>
    private const int MaxImageData = 32 * 1024 * 1024;

    /// <summary>
    /// Decodes <paramref name="png"/> into straight-alpha RGBA (<paramref name="rgba"/>, row-major, 4 bytes a pixel);
    /// false, with nothing out, when it is not a well-formed PNG this decoder takes or a side is over
    /// <paramref name="maxSide"/>. <paramref name="strict"/> refuses every other chunk and trailing bytes too.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> png, out int width, out int height, out byte[] rgba, int maxSide = DefaultMaxSide, bool strict = false)
    {
        width = height = 0;
        rgba = [];
        if (png.Length < Signature.Length + 12 + 13 || !png[..Signature.Length].SequenceEqual(Signature))
        {
            return false;
        }

        int w = 0, h = 0, depth = 0, colour = -1;
        byte[]? palette = null;
        byte[]? paletteAlpha = null;
        using var data = new MemoryStream();
        var at = Signature.Length;
        var first = true;
        var ended = false;
        while (at + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[at..]);
            if (length < 0 || length > png.Length - at - 12)
            {
                return false;
            }

            var typed = png.Slice(at + 4, 4 + length);
            if (BinaryPrimitives.ReadUInt32BigEndian(png[(at + 8 + length)..]) != PortraitMaskFile.Crc32(typed))
            {
                return false;
            }

            var type = typed[..4];
            var body = typed[4..];
            if (first != type.SequenceEqual("IHDR"u8))
            {
                return false;
            }

            if (type.SequenceEqual("IHDR"u8))
            {
                if (length != 13)
                {
                    return false;
                }

                w = BinaryPrimitives.ReadInt32BigEndian(body);
                h = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                depth = body[8];
                colour = body[9];
                if (w <= 0 || h <= 0 || w > maxSide || h > maxSide || body[10] != 0 || body[11] != 0 || body[12] != 0
                    || colour is not (0 or 2 or 3 or 4 or 6) || depth is not (8 or 16) || (colour == 3 && depth != 8))
                {
                    return false;
                }
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (length == 0 || length % 3 != 0 || length > 256 * 3 || palette is not null)
                {
                    return false;
                }

                palette = body.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                if (colour == 3)
                {
                    if (palette is null || length > palette.Length / 3)
                    {
                        return false;
                    }

                    paletteAlpha = body.ToArray();
                }
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (data.Length + length > MaxImageData)
                {
                    return false;
                }

                data.Write(body);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                ended = true;
                if (strict && at + 12 + length != png.Length)
                {
                    return false;
                }

                break;
            }
            else if (strict || (type[0] & 0x20) == 0)
            {
                // An unknown critical chunk: the image cannot be read safely. Strict: no ancillary chunk either.
                return false;
            }

            first = false;
            at += 12 + length;
        }

        if (!ended || data.Length == 0 || (colour == 3 && palette is null))
        {
            return false;
        }

        var channels = colour switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        var bpp = channels * (depth / 8);
        var stride = (long)w * bpp;
        var rawLength = (stride + 1) * h;
        if (rawLength > int.MaxValue / 2)
        {
            return false;
        }

        var raw = new byte[rawLength];
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

            // Exactly the rows promised: short is damaged, more is not what the header says.
            if (read != raw.Length || z.ReadByte() != -1)
            {
                return false;
            }
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var pal = palette ?? [];
        var s = (int)stride;
        var previous = new byte[s];
        var current = new byte[s];
        var output = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            var row = y * (s + 1);
            var filter = raw[row];
            if (filter > 4)
            {
                return false;
            }

            for (var i = 0; i < s; i++)
            {
                var x = raw[row + 1 + i];
                int left = i >= bpp ? current[i - bpp] : 0, up = previous[i], upLeft = i >= bpp ? previous[i - bpp] : 0;
                current[i] = filter switch
                {
                    0 => x,
                    1 => (byte)(x + left),
                    2 => (byte)(x + up),
                    3 => (byte)(x + ((left + up) >> 1)),
                    _ => (byte)(x + Paeth(left, up, upLeft)),
                };
            }

            var step = depth / 8;
            for (var px = 0; px < w; px++)
            {
                var o = ((y * w) + px) * 4;
                var p = px * bpp;
                switch (colour)
                {
                    case 0:
                        output[o] = output[o + 1] = output[o + 2] = current[p];
                        output[o + 3] = 255;
                        break;
                    case 2:
                        output[o] = current[p];
                        output[o + 1] = current[p + step];
                        output[o + 2] = current[p + (2 * step)];
                        output[o + 3] = 255;
                        break;
                    case 3:
                        var index = current[p];
                        if (index * 3 >= pal.Length)
                        {
                            return false;
                        }

                        output[o] = pal[index * 3];
                        output[o + 1] = pal[(index * 3) + 1];
                        output[o + 2] = pal[(index * 3) + 2];
                        output[o + 3] = paletteAlpha is not null && index < paletteAlpha.Length ? paletteAlpha[index] : (byte)255;
                        break;
                    case 4:
                        output[o] = output[o + 1] = output[o + 2] = current[p];
                        output[o + 3] = current[p + step];
                        break;
                    default:
                        output[o] = current[p];
                        output[o + 1] = current[p + step];
                        output[o + 2] = current[p + (2 * step)];
                        output[o + 3] = current[p + (3 * step)];
                        break;
                }
            }

            (previous, current) = (current, previous);
        }

        width = w;
        height = h;
        rgba = output;
        return true;
    }

    /// <summary>
    /// Encodes straight-alpha RGBA (<paramref name="rgba"/>, 4 bytes a pixel) as an 8-bit RGBA PNG, each row with the
    /// filter that packs best (the usual smallest-sum rule).
    /// </summary>
    public static byte[] EncodeRgba(ReadOnlySpan<byte> rgba, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException("The pixels are not width × height × 4 bytes.", nameof(rgba));
        }

        return Encode(rgba, width, height, colour: 6, bpp: 4, palette: default, paletteAlpha: default);
    }

    /// <summary>
    /// Encodes an indexed image (<paramref name="indices"/>, one byte a pixel) with <paramref name="paletteRgba"/>
    /// (4 bytes an entry, at most 256 entries) as an 8-bit palette PNG with a tRNS chunk for the entries' alpha.
    /// </summary>
    public static byte[] EncodeIndexed(ReadOnlySpan<byte> indices, int width, int height, ReadOnlySpan<byte> paletteRgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (indices.Length != width * height)
        {
            throw new ArgumentException("The indices are not width × height bytes.", nameof(indices));
        }

        var entries = paletteRgba.Length / 4;
        if (paletteRgba.Length % 4 != 0 || entries is 0 or > 256)
        {
            throw new ArgumentException("The palette must hold 1 to 256 RGBA entries.", nameof(paletteRgba));
        }

        foreach (var index in indices)
        {
            if (index >= entries)
            {
                throw new ArgumentException("An index is past the palette.", nameof(indices));
            }
        }

        var rgb = new byte[entries * 3];
        var alpha = new byte[entries];
        for (var i = 0; i < entries; i++)
        {
            rgb[i * 3] = paletteRgba[i * 4];
            rgb[(i * 3) + 1] = paletteRgba[(i * 4) + 1];
            rgb[(i * 3) + 2] = paletteRgba[(i * 4) + 2];
            alpha[i] = paletteRgba[(i * 4) + 3];
        }

        // tRNS may stop at the last entry that is not opaque.
        var keep = entries;
        while (keep > 0 && alpha[keep - 1] == 255)
        {
            keep--;
        }

        return Encode(indices, width, height, colour: 3, bpp: 1, rgb, alpha.AsSpan(0, keep));
    }

    private static byte[] Encode(ReadOnlySpan<byte> pixels, int width, int height, byte colour, int bpp, ReadOnlySpan<byte> palette, ReadOnlySpan<byte> paletteAlpha)
    {
        var stride = width * bpp;
        var raw = new byte[(stride + 1) * height];
        var candidate = new byte[stride];
        var best = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, stride);
            var above = y > 0 ? pixels.Slice((y - 1) * stride, stride) : default;

            // A palette image packs best unfiltered; RGBA takes the filter with the smallest sum of magnitudes.
            var bestFilter = 0;
            var bestScore = long.MaxValue;
            for (var filter = 0; filter <= (colour == 3 ? 0 : 4); filter++)
            {
                long score = 0;
                for (var i = 0; i < stride; i++)
                {
                    int x = row[i], left = i >= bpp ? row[i - bpp] : 0, up = above.IsEmpty ? 0 : above[i], upLeft = i >= bpp && !above.IsEmpty ? above[i - bpp] : 0;
                    var value = (byte)(filter switch
                    {
                        0 => x,
                        1 => x - left,
                        2 => x - up,
                        3 => x - ((left + up) >> 1),
                        _ => x - Paeth(left, up, upLeft),
                    });
                    candidate[i] = value;
                    score += value < 128 ? value : 256 - value;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestFilter = filter;
                    (best, candidate) = (candidate, best);
                }
            }

            raw[y * (stride + 1)] = (byte)bestFilter;
            best.CopyTo(raw, (y * (stride + 1)) + 1);
        }

        using var file = new MemoryStream();
        file.Write(Signature);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = colour;
        Chunk(file, "IHDR"u8, header);
        if (colour == 3)
        {
            Chunk(file, "PLTE"u8, palette);
            if (!paletteAlpha.IsEmpty)
            {
                Chunk(file, "tRNS"u8, paletteAlpha);
            }
        }

        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            z.Write(raw);
        }

        Chunk(file, "IDAT"u8, packed.ToArray());
        Chunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> body)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, body.Length);
        stream.Write(word);
        var typed = new byte[4 + body.Length];
        type.CopyTo(typed);
        body.CopyTo(typed.AsSpan(4));
        stream.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(word, PortraitMaskFile.Crc32(typed));
        stream.Write(word);
    }
}
