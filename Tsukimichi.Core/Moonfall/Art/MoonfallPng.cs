using System.Buffers.Binary;
using System.IO.Compression;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// A small PNG reader and writer for Moonfall's own pictures (the shipped scene paintings a recipe grades, the parity
/// fixtures, the offline renders): 8-bit greyscale, grey with alpha, RGB, RGBA and palette, not interlaced. Anything
/// else is refused (null), never guessed. Reading is off the framework thread with the scene build.
/// </summary>
public static class MoonfallPng
{
    /// <summary>The largest side read (a guard against a damaged header asking for gigabytes).</summary>
    public const int MaxSide = 8192;

    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Decodes a PNG to RGBA bytes; null (with a reason) when it is not one this reader takes.</summary>
    public static (byte[] Rgba, int Width, int Height)? Decode(ReadOnlySpan<byte> png, out string? error)
    {
        error = null;
        if (png.Length < 33 || !png[..8].SequenceEqual(Signature))
        {
            error = "not a PNG";
            return null;
        }

        int width = 0, height = 0, colourType = -1;
        byte[]? palette = null;
        byte[]? paletteAlpha = null;
        using var idat = new MemoryStream();
        var at = 8;
        while (at + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.Slice(at, 4));
            if (length < 0 || at + 12 + (long)length > png.Length)
            {
                error = "a chunk runs past the end";
                return null;
            }

            var type = png.Slice(at + 4, 4);
            var data = png.Slice(at + 8, length);
            if (type.SequenceEqual("IHDR"u8))
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                var depth = data[8];
                colourType = data[9];
                var interlace = data[12];
                if (width < 1 || height < 1 || width > MaxSide || height > MaxSide)
                {
                    error = $"size {width} x {height} out of range";
                    return null;
                }

                if (depth != 8 || interlace != 0 || colourType is not (0 or 2 or 3 or 4 or 6))
                {
                    error = $"bit depth {depth}, colour type {colourType}, interlace {interlace} not read";
                    return null;
                }
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                palette = data.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                paletteAlpha = data.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }

            at += 12 + length;
        }

        if (colourType < 0)
        {
            error = "no IHDR";
            return null;
        }

        var channels = colourType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        var stride = width * channels;
        var raw = new byte[(stride + 1) * height];
        try
        {
            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            var read = 0;
            while (read < raw.Length)
            {
                var got = z.Read(raw, read, raw.Length - read);
                if (got == 0)
                {
                    break;
                }

                read += got;
            }

            if (read < raw.Length)
            {
                error = "the image data is short";
                return null;
            }
        }
        catch (InvalidDataException ex)
        {
            error = "the image data is damaged: " + ex.Message;
            return null;
        }

        var pixels = new byte[stride * height];
        var previous = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var line = new Span<byte>(raw, (y * (stride + 1)) + 1, stride);
            var output = new Span<byte>(pixels, y * stride, stride);
            for (var x = 0; x < stride; x++)
            {
                var a = x >= channels ? output[x - channels] : 0;
                var b = previous[x];
                var c = x >= channels ? previous[x - channels] : 0;
                output[x] = filter switch
                {
                    0 => line[x],
                    1 => (byte)(line[x] + a),
                    2 => (byte)(line[x] + b),
                    3 => (byte)(line[x] + ((a + b) >> 1)),
                    4 => (byte)(line[x] + Paeth(a, b, c)),
                    _ => line[x],
                };
            }

            if (filter > 4)
            {
                error = $"filter {filter} on row {y}";
                return null;
            }

            output.CopyTo(previous);
        }

        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            switch (colourType)
            {
                case 0:
                    rgba[i * 4] = rgba[(i * 4) + 1] = rgba[(i * 4) + 2] = pixels[i];
                    rgba[(i * 4) + 3] = 255;
                    break;
                case 2:
                    rgba[i * 4] = pixels[i * 3];
                    rgba[(i * 4) + 1] = pixels[(i * 3) + 1];
                    rgba[(i * 4) + 2] = pixels[(i * 3) + 2];
                    rgba[(i * 4) + 3] = 255;
                    break;
                case 3:
                    var index = pixels[i];
                    if (palette is null || (index * 3) + 2 >= palette.Length)
                    {
                        error = "a palette index has no colour";
                        return null;
                    }

                    rgba[i * 4] = palette[index * 3];
                    rgba[(i * 4) + 1] = palette[(index * 3) + 1];
                    rgba[(i * 4) + 2] = palette[(index * 3) + 2];
                    rgba[(i * 4) + 3] = paletteAlpha is not null && index < paletteAlpha.Length ? paletteAlpha[index] : (byte)255;
                    break;
                case 4:
                    rgba[i * 4] = rgba[(i * 4) + 1] = rgba[(i * 4) + 2] = pixels[i * 2];
                    rgba[(i * 4) + 3] = pixels[(i * 2) + 1];
                    break;
                default:
                    Array.Copy(pixels, i * 4, rgba, i * 4, 4);
                    break;
            }
        }

        return (rgba, width, height);
    }

    /// <summary>Encodes RGBA bytes as a PNG (no filtering; the offline renders and fixtures).</summary>
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (width < 1 || height < 1 || rgba.Length < width * height * 4)
        {
            throw new ArgumentException("The pixels are shorter than width × height × 4.", nameof(rgba));
        }

        using var file = new MemoryStream();
        file.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        header[10] = header[11] = header[12] = 0;
        Chunk(file, "IHDR"u8, header);
        using var body = new MemoryStream();
        using (var z = new ZLibStream(body, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(rgba.Slice(y * width * 4, width * 4));
            }
        }

        Chunk(file, "IDAT"u8, body.ToArray());
        Chunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Chunk(Stream file, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        file.Write(word);
        file.Write(type);
        file.Write(data);
        var crc = Crc(Crc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        file.Write(word);
    }

    private static readonly uint[] CrcTable = BuildCrc();

    private static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            t[n] = c;
        }

        return t;
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }
}
