using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.DataGen;

/// <summary>
/// An RGBA canvas with just what the portrait contact sheet draws: images blended in, filled bands, lines, a circle and
/// labels in a 5 × 7 pixel font (capitals, digits and a little punctuation); saved as PNG.
/// </summary>
internal sealed class SheetCanvas
{
    /// <summary>The night plate behind every cell (the Giver card's well, flattened).</summary>
    public static readonly (byte R, byte G, byte B) Night = (28, 34, 64);

    private static readonly Dictionary<char, byte[]> Glyphs = BuildGlyphs();
    private readonly byte[] pixels;

    public SheetCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 22;
            pixels[i + 1] = 24;
            pixels[i + 2] = 34;
            pixels[i + 3] = 255;
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Blends one pixel at <paramref name="alpha"/> (0–1).</summary>
    public void Blend(int x, int y, double r, double g, double b, double alpha)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || alpha <= 0)
        {
            return;
        }

        alpha = Math.Min(1, alpha);
        var i = ((y * Width) + x) * 4;
        pixels[i] = (byte)Math.Round((r * alpha) + (pixels[i] * (1 - alpha)));
        pixels[i + 1] = (byte)Math.Round((g * alpha) + (pixels[i + 1] * (1 - alpha)));
        pixels[i + 2] = (byte)Math.Round((b * alpha) + (pixels[i + 2] * (1 - alpha)));
    }

    public void Fill(int left, int top, int width, int height, (byte R, byte G, byte B) colour, double alpha = 1)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                Blend(x, y, colour.R, colour.G, colour.B, alpha);
            }
        }
    }

    public void Circle(int centreX, int centreY, double radius, (byte R, byte G, byte B) colour, double alpha)
    {
        for (var step = 0; step < 1440; step++)
        {
            var angle = step * Math.PI / 720;
            Blend(centreX + (int)Math.Round(Math.Cos(angle) * radius), centreY + (int)Math.Round(Math.Sin(angle) * radius), colour.R, colour.G, colour.B, alpha);
        }
    }

    /// <summary>Writes <paramref name="text"/> in capitals at (left, top); returns the x after the last glyph.</summary>
    public int Text(int left, int top, string text, (byte R, byte G, byte B) colour, int maxWidth = int.MaxValue)
    {
        var x = left;
        foreach (var raw in text)
        {
            if ((long)x + 5 > (long)left + maxWidth)
            {
                break;
            }

            if (Glyphs.TryGetValue(char.ToUpperInvariant(raw), out var rows))
            {
                for (var row = 0; row < 7; row++)
                {
                    for (var col = 0; col < 5; col++)
                    {
                        if ((rows[row] & (0x10 >> col)) != 0)
                        {
                            Blend(x + col, top + row, colour.R, colour.G, colour.B, 1);
                        }
                    }
                }
            }

            x += 6;
        }

        return x;
    }

    public void Save(string path)
    {
        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = 8;
        header[9] = 6;
        Chunk(file, "IHDR", header);
        using var raw = new MemoryStream();
        for (var y = 0; y < Height; y++)
        {
            raw.WriteByte(0);
            raw.Write(pixels, y * Width * 4, Width * 4);
        }

        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(z);
        }

        Chunk(file, "IDAT", packed.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        stream.Write(word);
        var body = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body, 4);
        stream.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(word, PortraitMaskFile.Crc32(body));
        stream.Write(word);
    }

    /// <summary>A classic 5 × 7 character-LCD font: each glyph is seven rows of five bits, the left pixel the high bit.</summary>
    private static Dictionary<char, byte[]> BuildGlyphs() => new()
    {
        ['A'] = [0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['B'] = [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E],
        ['C'] = [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
        ['D'] = [0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C],
        ['E'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F],
        ['F'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
        ['G'] = [0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F],
        ['H'] = [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['I'] = [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['J'] = [0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C],
        ['K'] = [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11],
        ['L'] = [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
        ['M'] = [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11],
        ['N'] = [0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11],
        ['O'] = [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['P'] = [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
        ['Q'] = [0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D],
        ['R'] = [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
        ['S'] = [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E],
        ['T'] = [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
        ['U'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['V'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04],
        ['W'] = [0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A],
        ['X'] = [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
        ['Y'] = [0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04],
        ['Z'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
        ['0'] = [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
        ['1'] = [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['2'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
        ['3'] = [0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E],
        ['4'] = [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
        ['5'] = [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
        ['6'] = [0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E],
        ['7'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08],
        ['8'] = [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
        ['9'] = [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C],
        [','] = [0x00, 0x00, 0x00, 0x00, 0x0C, 0x04, 0x08],
        ['.'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C],
        ['-'] = [0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00],
        ['\''] = [0x0C, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00],
        ['&'] = [0x0C, 0x12, 0x14, 0x08, 0x15, 0x12, 0x0D],
        [':'] = [0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00],
        ['('] = [0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02],
        [')'] = [0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08],
        ['/'] = [0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x00],
        ['!'] = [0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04],
    };
}
