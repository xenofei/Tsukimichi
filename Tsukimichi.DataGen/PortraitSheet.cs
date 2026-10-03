using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Lumina.Data.Files;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

/// <summary>
/// The giver portrait contact sheet (feature plan v7 F3): every face the portrait index gives a giver, cropped as the
/// plugin crops it, with the framing guides of <see cref="PortraitFraming"/> drawn over each (crown, eye line and chin
/// bands, the circle), one page per family. Beside the pages, <c>portraits.md</c> lists each cell (icon, family, crop,
/// givers) and the coverage. A curator reads the sheet to name faces and to record per-icon crops in
/// <c>curated/giver_portraits.json</c>. The pages hold game art: they are for review, never committed or shipped.
/// </summary>
internal static class PortraitSheet
{
    private const int Cell = 192;

    /// <param name="only">Icons to draw alone and larger (four to a row), worn or not, for measuring a face; null draws
    /// every face a giver wears.</param>
    public static int Write(Lumina.GameData data, string outDir, string curatedDir, IReadOnlyCollection<uint>? only = null)
    {
        Directory.CreateDirectory(outDir);
        var columns = only is null ? 8 : 4;
        var perPage = only is null ? 48 : 8;
        var sourceCell = only is null ? 288 : 480;
        var prefix = only is null ? string.Empty : "focus-";
        var curated = CuratedData.Load(curatedDir);
        foreach (var warning in curated.Warnings)
        {
            Console.WriteLine($"  curated: {warning}");
        }

        var inputs = GiverPortraitSources.Read(data.Excel, icon => data.FileExists(RewardArtIndex.IconPath(icon)), line => Console.WriteLine($"  {line}"));
        var index = PortraitIndex.Build(inputs, curated.GiverPortraits);

        // Every face a giver wears, with the givers wearing it.
        var cells = new SortedDictionary<(int Rank, uint Icon), (PortraitSource Source, SortedSet<string> Givers)>();
        foreach (var giver in inputs.Givers)
        {
            foreach (var variant in index.Variants(giver.NpcId))
            {
                var key = (PortraitSources.Rank(variant.Source), variant.Icon);
                if (!cells.TryGetValue(key, out var cell))
                {
                    cell = (variant.Source, new SortedSet<string>(StringComparer.Ordinal));
                    cells[key] = cell;
                }

                cell.Givers.Add(giver.Name);
            }
        }

        if (only is not null)
        {
            var kept = new SortedDictionary<(int Rank, uint Icon), (PortraitSource Source, SortedSet<string> Givers)>();
            foreach (var icon in only)
            {
                var source = PortraitSources.FamilyOfIcon(icon);
                var key = (PortraitSources.Rank(source), icon);
                kept[key] = cells.TryGetValue(key, out var worn) ? worn : (source, new SortedSet<string>(StringComparer.Ordinal));
            }

            cells = kept;
        }

        // How many quests each face is picked for: the faces worth curating first.
        var picked = new Dictionary<uint, int>();
        foreach (var quest in inputs.Quests)
        {
            if (index.For(quest.GiverId, quest.Expansion, quest.BeastTribe) is { HasArt: true } portrait)
            {
                picked[portrait.Icon] = picked.GetValueOrDefault(portrait.Icon) + 1;
            }
        }

        var md = new StringBuilder();
        md.AppendLine("# Giver portrait contact sheet");
        md.AppendLine();
        md.AppendLine(CultureInfo.InvariantCulture, $"Written by `Tsukimichi.DataGen --portrait-sheet`. {cells.Count} faces worn by {index.GiversWithArt} of {index.GiverCount} giver ids, {picked.Values.Sum()} quests with a portrait. `<family>-<n>.png` shows each crop as the plugin draws it, with the framing guides: the crown band (8–12 %), the eye-line band (42–46 %), the chin band (78–84 %) and the circle. `<family>-<n>-source.png` shows the whole texture with a grid every 0.1 in texture coordinates (the 0.5 lines brighter) and the crop's box, for measuring a per-icon crop. Cells run left to right, top to bottom, {columns} to a row.");

        foreach (var family in cells.GroupBy(c => c.Value.Source))
        {
            var list = family.ToList();
            for (var page = 0; page * perPage < list.Count; page++)
            {
                var slice = list.Skip(page * perPage).Take(perPage).ToList();
                var file = $"{prefix}{family.Key}-{page + 1}.png";
                var sourceFile = $"{prefix}{family.Key}-{page + 1}-source.png";
                var rows = (slice.Count + columns - 1) / columns;
                var canvas = new Canvas(columns * Cell, rows * Cell);
                var sources = new Canvas(columns * sourceCell, rows * sourceCell);
                md.AppendLine();
                md.AppendLine(CultureInfo.InvariantCulture, $"## {file}");
                md.AppendLine();
                md.AppendLine("| Cell | Icon | Crop | Quests | Givers |");
                md.AppendLine("|---:|---:|---|---:|---|");
                for (var n = 0; n < slice.Count; n++)
                {
                    var ((_, icon), (source, givers)) = (slice[n].Key, slice[n].Value);
                    var crop = index.Crops.For(source, icon);
                    var texture = Load(data, icon);
                    canvas.DrawCrop(texture, crop, (n % columns) * Cell, (n / columns) * Cell, Cell);
                    sources.DrawSource(texture, crop, (n % columns) * sourceCell, (n / columns) * sourceCell, sourceCell);
                    md.AppendLine(CultureInfo.InvariantCulture, $"| {n + 1} | {icon:D6} | {crop.U0:0.###}, {crop.V0:0.###} → {crop.U1:0.###}, {crop.V1:0.###} | {picked.GetValueOrDefault(icon)} | {string.Join(", ", givers)} |");
                }

                canvas.Save(Path.Combine(outDir, file));
                sources.Save(Path.Combine(outDir, sourceFile));
                Console.WriteLine($"wrote:   {Path.Combine(outDir, file)} and its source page ({slice.Count} faces)");
            }
        }

        var mdFile = Path.Combine(outDir, prefix + "portraits.md");
        File.WriteAllText(mdFile, md.ToString());
        Console.WriteLine($"wrote:   {mdFile}");
        return 0;
    }

    private static TexFile? Load(Lumina.GameData data, uint icon)
    {
        var path = RewardArtIndex.IconPath(icon);
        return data.GetFile<TexFile>(path.Replace(".tex", "_hr1.tex", StringComparison.Ordinal)) ?? data.GetFile<TexFile>(path);
    }

    /// <summary>An RGBA canvas with just what the sheet draws: a cropped texture, guide lines, a circle; saved as PNG.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly byte[] pixels = Fill(width, height);

        public void DrawCrop(TexFile? texture, PortraitCrop crop, int left, int top, int size)
        {
            const int Pad = 4;
            var box = size - (2 * Pad);
            if (texture is not null)
            {
                var source = texture.ImageData;
                int w = texture.Header.Width, h = texture.Header.Height;
                for (var y = 0; y < box; y++)
                {
                    for (var x = 0; x < box; x++)
                    {
                        var sx = Math.Clamp((int)((crop.U0 + ((crop.U1 - crop.U0) * (x + 0.5f) / box)) * w), 0, w - 1);
                        var sy = Math.Clamp((int)((crop.V0 + ((crop.V1 - crop.V0) * (y + 0.5f) / box)) * h), 0, h - 1);
                        var si = ((sy * w) + sx) * 4;
                        var a = source[si + 3];
                        Blend(left + Pad + x, top + Pad + y, source[si + 2], source[si + 1], source[si], a);
                    }
                }
            }

            // Guides: crown, eye line and chin bands (both edges), then the circle.
            foreach (var (from, to, r, g, b) in new[]
                     {
                         (PortraitFraming.CrownMin, PortraitFraming.CrownMax, (byte)90, (byte)170, (byte)255),
                         (PortraitFraming.EyeLineMin, PortraitFraming.EyeLineMax, (byte)255, (byte)80, (byte)80),
                         (PortraitFraming.ChinMin, PortraitFraming.ChinMax, (byte)90, (byte)220, (byte)120),
                     })
            {
                foreach (var at in new[] { from, to })
                {
                    var y = top + Pad + (int)(at * box);
                    for (var x = 0; x < box; x++)
                    {
                        Blend(left + Pad + x, y, r, g, b, 200);
                    }
                }
            }

            var centre = Pad + (box / 2f);
            for (var step = 0; step < 720; step++)
            {
                var angle = step * Math.PI / 360;
                Blend(left + (int)(centre + (Math.Cos(angle) * box / 2)), top + (int)(centre + (Math.Sin(angle) * box / 2)), 230, 200, 120, 255);
            }
        }

        /// <summary>The whole texture fitted into the cell, a 0.1 grid in texture coordinates, and the crop's box.</summary>
        public void DrawSource(TexFile? texture, PortraitCrop crop, int left, int top, int size)
        {
            if (texture is null)
            {
                return;
            }

            const int Pad = 4;
            var source = texture.ImageData;
            int w = texture.Header.Width, h = texture.Header.Height;
            var scale = (size - (2 * Pad)) / (float)Math.Max(w, h);
            int dw = (int)(w * scale), dh = (int)(h * scale);
            int ox = left + Pad + ((size - (2 * Pad) - dw) / 2), oy = top + Pad + ((size - (2 * Pad) - dh) / 2);
            for (var y = 0; y < dh; y++)
            {
                for (var x = 0; x < dw; x++)
                {
                    var si = ((Math.Min(h - 1, (int)(y / scale)) * w) + Math.Min(w - 1, (int)(x / scale))) * 4;
                    Blend(ox + x, oy + y, source[si + 2], source[si + 1], source[si], source[si + 3]);
                }
            }

            for (var tick = 0; tick <= 10; tick++)
            {
                var alpha = (byte)(tick == 5 ? 220 : 90);
                var gx = ox + (int)(tick / 10f * (dw - 1));
                var gy = oy + (int)(tick / 10f * (dh - 1));
                for (var y = 0; y < dh; y++)
                {
                    Blend(gx, oy + y, 120, 200, 255, alpha);
                }

                for (var x = 0; x < dw; x++)
                {
                    Blend(ox + x, gy, 120, 200, 255, alpha);
                }
            }

            int x0 = ox + (int)(crop.U0 * dw), x1 = ox + (int)(crop.U1 * dw), y0 = oy + (int)(crop.V0 * dh), y1 = oy + (int)(crop.V1 * dh);
            for (var x = x0; x <= x1; x++)
            {
                Blend(x, y0, 255, 220, 60, 255);
                Blend(x, y1, 255, 220, 60, 255);
            }

            for (var y = y0; y <= y1; y++)
            {
                Blend(x0, y, 255, 220, 60, 255);
                Blend(x1, y, 255, 220, 60, 255);
            }
        }

        public void Save(string path)
        {
            using var file = File.Create(path);
            file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
            header[8] = 8;
            header[9] = 6;
            Chunk(file, "IHDR", header);
            using var raw = new MemoryStream();
            for (var y = 0; y < height; y++)
            {
                raw.WriteByte(0);
                raw.Write(pixels, y * width * 4, width * 4);
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

        private void Blend(int x, int y, byte r, byte g, byte b, byte a)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            var i = ((y * width) + x) * 4;
            pixels[i] = (byte)(((r * a) + (pixels[i] * (255 - a))) / 255);
            pixels[i + 1] = (byte)(((g * a) + (pixels[i + 1] * (255 - a))) / 255);
            pixels[i + 2] = (byte)(((b * a) + (pixels[i + 2] * (255 - a))) / 255);
        }

        private static byte[] Fill(int width, int height)
        {
            var pixels = new byte[width * height * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                // The night plate the Giver card sits on.
                pixels[i] = 34;
                pixels[i + 1] = 36;
                pixels[i + 2] = 52;
                pixels[i + 3] = 255;
            }

            return pixels;
        }

        private static void Chunk(Stream stream, string type, byte[] data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length);
            var body = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type, body);
            data.CopyTo(body, 4);
            stream.Write(body);
            Span<byte> crc = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
            stream.Write(crc);
        }

        private static uint Crc32(byte[] bytes)
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
}
